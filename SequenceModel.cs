using System.ComponentModel;
using System.Text.Json;

namespace ZeroExLight;

public readonly record struct Rgb(byte R,byte G,byte B)
{
    public string Hex=>$"#{R:X2}{G:X2}{B:X2}";
    public static Rgb Lerp(Rgb a,Rgb b,double t)
    {
        t=Math.Clamp(t,0,1);
        byte Mix(byte x,byte y)=>(byte)Math.Round(x+(y-x)*t);
        return new(Mix(a.R,b.R),Mix(a.G,b.G),Mix(a.B,b.B));
    }
}

public sealed class LightStep : INotifyPropertyChanged
{
    public Guid Id {get;set;}=Guid.NewGuid();
    public string Name {get;set;}="";
    public byte R {get;set;}
    public byte G {get;set;}
    public byte B {get;set;}
    private double holdSeconds=1,fadeSeconds=3;
    private int order;
    public double HoldSeconds {get=>holdSeconds;set{holdSeconds=value;Changed(nameof(HoldSeconds));Changed(nameof(TimeSummary));}}
    public double FadeSeconds {get=>fadeSeconds;set{fadeSeconds=value;Changed(nameof(FadeSeconds));Changed(nameof(TimeSummary));}}
    [System.Text.Json.Serialization.JsonIgnore] public int Order {get=>order;set{order=value;Changed(nameof(Order));}}
    [System.Text.Json.Serialization.JsonIgnore] public Rgb Color=>new(R,G,B);
    [System.Text.Json.Serialization.JsonIgnore] public string Hex=>Color.Hex;
    [System.Text.Json.Serialization.JsonIgnore] public string TimeSummary=>$"停留 {HoldSeconds:0.##} 秒 · 渐变 {FadeSeconds:0.##} 秒";
    public event PropertyChangedEventHandler? PropertyChanged;
    private void Changed(string name)=>PropertyChanged?.Invoke(this,new(name));
    public LightStep Copy()=>new(){Id=Id,Name=Name,R=R,G=G,B=B,HoldSeconds=HoldSeconds,FadeSeconds=FadeSeconds};
}

public readonly record struct SequenceSample(Rgb Color,int Index,int NextIndex,bool InFade);

public sealed class SequenceTimeline
{
    private readonly LightStep[] steps;
    public double Duration {get;}
    public int Count=>steps.Length;
    public SequenceTimeline(IEnumerable<LightStep> source)
    {
        steps=source.Select(x=>x.Copy()).ToArray();
        if(steps.Length is <1 or >32)throw new ArgumentException("请编排 1–32 个颜色。");
        foreach(var step in steps)
            if(!double.IsFinite(step.HoldSeconds)||!double.IsFinite(step.FadeSeconds)||step.HoldSeconds is <0 or >30||step.FadeSeconds is <0 or >30)
                throw new ArgumentException("停留和渐变时间须在 0–30 秒之间。");
        Duration=steps.Sum(s=>s.HoldSeconds+s.FadeSeconds);
        if(Duration<.45)throw new ArgumentException("整个循环至少需要 0.45 秒，请增加停留或渐变时间。");
    }
    public SequenceSample At(double seconds)
    {
        if(!double.IsFinite(seconds))throw new ArgumentException("Invalid playback position");
        double position=((seconds%Duration)+Duration)%Duration;
        for(int i=0;i<steps.Length;i++)
        {
            var current=steps[i];int next=(i+1)%steps.Length;
            if(position<current.HoldSeconds)return new(current.Color,i,next,false);
            position-=current.HoldSeconds;
            if(position<current.FadeSeconds)return new(Rgb.Lerp(current.Color,steps[next].Color,position/current.FadeSeconds),i,next,true);
            position-=current.FadeSeconds;
        }
        return new(steps[0].Color,0,steps.Length==1?0:1,false);
    }
}

public enum PlaybackState { Stopped,Playing,IdlePaused,ManualPaused }
public sealed class PlaybackClock
{
    private readonly SequenceTimeline timeline;
    private bool wasActive;
    public double Position {get;private set;}
    public PlaybackState State {get;private set;}=PlaybackState.Stopped;
    public bool ManualPause {get;set;}
    public PlaybackClock(SequenceTimeline timeline)=>this.timeline=timeline;
    public SequenceSample Tick(double delta,bool active)
    {
        if(!double.IsFinite(delta)||delta<0)throw new ArgumentException("Invalid elapsed time");
        if(ManualPause){State=PlaybackState.ManualPaused;wasActive=false;}
        else if(!active){State=PlaybackState.IdlePaused;wasActive=false;}
        else {State=PlaybackState.Playing;if(wasActive)Position=(Position+delta)%timeline.Duration;wasActive=true;}
        return timeline.At(Position);
    }
}

public sealed class SequenceStore
{
    private readonly string path;
    private bool preserveInvalid;
    public SequenceStore(string path)=>this.path=path;
    public LightStep[] Load()
    {
        if(!File.Exists(path))return [];
        try
        {
            var steps=JsonSerializer.Deserialize<LightStep[]>(File.ReadAllText(path))??throw new IOException("序列文件为空。");
            if(steps.Length>32||steps.Any(s=>s is null||s.Name.Length>40||s.Id==Guid.Empty)||steps.Select(s=>s.Id).Distinct().Count()!=steps.Length)
                throw new IOException("序列文件内容无效。");
            if(steps.Any(s=>!double.IsFinite(s.HoldSeconds)||!double.IsFinite(s.FadeSeconds)||s.HoldSeconds is <0 or >30||s.FadeSeconds is <0 or >30))throw new IOException("序列时间无效。");
            return steps;
        }
        catch{preserveInvalid=true;throw;}
    }
    public void Save(IEnumerable<LightStep> source)
    {
        var steps=source.ToArray();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        if(preserveInvalid&&File.Exists(path)){File.Copy(path,path+$".invalid-{DateTime.Now:yyyyMMdd-HHmmss}",false);preserveInvalid=false;}
        File.WriteAllText(path+".tmp",JsonSerializer.Serialize(steps,new JsonSerializerOptions{WriteIndented=true}));
        File.Move(path+".tmp",path,true);
    }
}
