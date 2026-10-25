using System.Text.Json;
using Windows.ApplicationModel.DataTransfer;

namespace ZeroExLight;
public static class CustomDiagnostics
{
    public static string[] Run()
    {
        var results=new List<string>();
        void Check(bool condition,string name){if(!condition)throw new Exception(name);results.Add(name);}
        var red=new LightStep{Name="红色",R=255,HoldSeconds=1,FadeSeconds=2};
        var blue=new LightStep{Name="蓝色",B=255,HoldSeconds=1,FadeSeconds=2};
        var timeline=new SequenceTimeline([red,blue]);
        Check(timeline.At(0).Color==new Rgb(255,0,0),"Sequence starts at first color");
        Check(timeline.At(2).Color==new Rgb(128,0,128),"Gradient interpolates intermediate color");
        Check(timeline.At(5).Color==new Rgb(128,0,128)&&timeline.At(5).NextIndex==0,"Last color fades back to first");
        Check(timeline.At(6).Color==new Rgb(255,0,0),"Loop wraps at exact duration");
        red.R=0;Check(timeline.At(0).Color.R==255,"Playback snapshot independent of draft edits");
        var clock=new PlaybackClock(timeline);
        clock.Tick(0,true);clock.Tick(2,true);clock.Tick(100,false);
        Check(clock.Position==2&&clock.State==PlaybackState.IdlePaused,"Idle freezes sequence clock");
        clock.Tick(100,true);Check(clock.Position==2,"Motion resume does not jump across idle time");
        clock.Tick(1,true);clock.ManualPause=true;clock.Tick(50,true);
        Check(clock.Position==3&&clock.State==PlaybackState.ManualPaused,"Manual pause freezes clock");
        clock.ManualPause=false;clock.Tick(40,true);Check(clock.Position==3,"Manual resume keeps sequence position");
        bool rejected=false;try{_=new SequenceTimeline([new(){HoldSeconds=0,FadeSeconds=0}]);}catch(ArgumentException){rejected=true;}
        Check(rejected,"Zero duration loop rejected");
        rejected=false;try{_=new SequenceTimeline([new(){FadeSeconds=double.NaN}]);}catch(ArgumentException){rejected=true;}
        Check(rejected,"Invalid timing rejected");
        var one=new SequenceTimeline([blue]);Check(one.At(999).Color==blue.Color,"Single color remains stable");
        var duplicate=blue.Copy();duplicate.Id=Guid.NewGuid();
        var payload=JsonSerializer.Serialize(new[]{blue,duplicate});
        var decoded=JsonSerializer.Deserialize<LightStep[]>(payload)!;
        Check(decoded.Length==2&&decoded[0].Color==decoded[1].Color&&decoded[0].Id!=decoded[1].Id&&decoded[0].FadeSeconds==2,"Preset round trip preserves repeated colors and timings");
        return results.ToArray();
    }
}

public sealed partial class MainWindow
{
    private async Task VerifyCustomUi(string path)
    {
        var tests=new List<string>();
        void Check(bool value,string name){if(!value)throw new Exception(name);tests.Add(name);}
        object result;
        try
        {
            Pages.SelectedIndex=1;
            var package=new DataPackage();package.SetData(ColorDragFormat,"#FF0000");
            Check(await AcceptColorData(package.GetView(),0),"Drag data inserts color into empty sequence");
            Check(await AcceptColorData(package.GetView(),1),"Same color can be inserted repeatedly");
            Check(sequence.Count==2&&sequence[0].Id!=sequence[1].Id,"Duplicate colors retain independent step identity");
            package=new();package.SetData(ColorDragFormat,"#0000FF");await AcceptColorData(package.GetView(),1);
            Check(sequence.Select(s=>s.Hex).SequenceEqual(new[]{"#FF0000","#0000FF","#FF0000"}),"Drop insertion order is preserved");
            SequenceList.SelectedIndex=1;MoveSelected(-1);
            Check(sequence[0].Hex=="#0000FF","Move button reorders selected step");
            HoldTime.Value=2;FadeTime.Value=4;
            Check(sequence[0].HoldSeconds==2&&sequence[0].FadeSeconds==4,"Selected timing controls update step");
            StartPlayback(true);await Task.Delay(250);
            Check(player!.Running&&player.Preview&&!connected&&!session.Dirty,"Offline preview runs without device changes");
            player.TogglePause();Check(player.Paused,"Preview pause toggles state");
            await StopPlayback();
            Check(!Playing&&sequence.Count==3&&!session.Dirty,"Stopping preview preserves draft and hardware state");
            var original=sequence[0].Id;Pages.SelectedIndex=0;Pages.SelectedIndex=1;
            Check(sequence[0].Id==original,"Tab switch preserves editor state");
            var temp=Path.Combine(Path.GetDirectoryName(Path.GetFullPath(path))!,"preset-test-"+Guid.NewGuid()+".json");
            var store=new SequenceStore(temp);store.Save(sequence);var restored=store.Load();
            Check(restored.Select(s=>s.Id).SequenceEqual(sequence.Select(s=>s.Id))&&restored[0].FadeSeconds==4,"Sequence file preserves order, identities and timing");
            File.Delete(temp);
            result=new{success=true,tests,hardwareConnected=connected,hardwareModified=session.Dirty};
        }
        catch(Exception error){await StopPlayback();result=new{success=false,error=error.ToString(),tests};}
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        File.WriteAllText(path,JsonSerializer.Serialize(result,new JsonSerializerOptions{WriteIndented=true}));
    }
}
