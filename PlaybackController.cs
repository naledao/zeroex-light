using Microsoft.UI.Xaml;
using System.Diagnostics;

namespace ZeroExLight;
public sealed class PlaybackIdleException : OperationCanceledException { }
public sealed record PlaybackUpdate(PlaybackState State,SequenceSample Sample,bool Preview);

public sealed class PlaybackController
{
    private readonly LightSession session;
    private readonly MouseActivity activity;
    private readonly DispatcherTimer timer=new(){Interval=TimeSpan.FromMilliseconds(50)};
    private PlaybackClock? clock;
    private Task? sending;
    private long previousTick,nextSend;
    private Rgb? lastColor;
    private bool needsResume=true,preview;
    private int generation;
    private volatile bool maySend;
    private PlaybackUpdate? lastUpdate;
    public bool Running=>clock is not null;
    public bool Paused=>clock?.ManualPause==true;
    public bool Preview=>preview;
    public bool Sending=>sending is {IsCompleted:false};
    public event Action<PlaybackUpdate>? Updated;
    public event Action<Exception>? Failed;
    public PlaybackController(LightSession session,MouseActivity activity)
    {this.session=session;this.activity=activity;timer.Tick+=Tick;}
    public void Start(IEnumerable<LightStep> source,bool previewOnly)
    {
        if(Running)throw new InvalidOperationException("请先停止当前播放。");
        clock=new PlaybackClock(new SequenceTimeline(source));preview=previewOnly;
        Interlocked.Increment(ref generation);lastColor=null;lastUpdate=null;needsResume=true;previousTick=Stopwatch.GetTimestamp();nextSend=0;
        timer.Start();Tick(null!,null!);
    }
    public void TogglePause(){if(clock is not null){clock.ManualPause=!clock.ManualPause;maySend=!clock.ManualPause;needsResume=true;}}
    private void Tick(object? sender,object e)
    {
        if(clock is null)return;
        long now=Stopwatch.GetTimestamp();double delta=(now-previousTick)/(double)Stopwatch.Frequency;previousTick=now;
        var sample=clock.Tick(delta,preview||activity.Recent);
        maySend=clock.State==PlaybackState.Playing&&!preview;
        timer.Interval=TimeSpan.FromMilliseconds(clock.State==PlaybackState.Playing?50:200);
        var update=new PlaybackUpdate(clock.State,sample,preview);
        if(update!=lastUpdate){lastUpdate=update;Updated?.Invoke(update);}
        if(clock.State!=PlaybackState.Playing){needsResume=true;return;}
        if(preview||sending is {IsCompleted:false}||now<nextSend)return;
        if(lastColor==sample.Color&&!needsResume)return;
        bool resume=needsResume;needsResume=false;
        nextSend=now+(long)(.45*Stopwatch.Frequency);
        sending=Send(sample.Color,resume,generation);
    }
    private async Task Send(Rgb color,bool resume,int capturedGeneration)
    {
        try
        {
            await Task.Run(()=>session.PlaybackFrame(color,resume,()=>Volatile.Read(ref generation)==capturedGeneration&&maySend&&activity.Recent));
            if(generation==capturedGeneration)lastColor=color;
        }
        catch(PlaybackIdleException){needsResume=true;}
        catch(Exception error)
        {
            if(generation!=capturedGeneration)return;
            timer.Stop();clock=null;maySend=false;Interlocked.Increment(ref generation);
            Failed?.Invoke(error);
        }
    }
    public async Task Stop()
    {
        timer.Stop();clock=null;maySend=false;Interlocked.Increment(ref generation);
        if(sending is not null)await sending;
        sending=null;session.EndPlayback();lastColor=null;needsResume=true;
    }
}
