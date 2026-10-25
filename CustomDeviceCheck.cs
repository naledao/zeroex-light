using System.Diagnostics;
using System.Text.Json;
using Microsoft.UI.Xaml.Controls;

namespace ZeroExLight;
public sealed partial class MainWindow
{
    private async Task VerifyCustomDevice(string output)
    {
        var transitions=new List<object>();bool silence=false,restored=false;string? error=null;
        busy=true;UpdateControls();Pages.SelectedIndex=1;
        try
        {
            if(player is null||mouseActivity is null)throw new IOException("Raw Input 未就绪。");
            mouseActivity.Pid=0x11fe;
            SetStatus("实机验证准备中","请用 2.4G，并轻轻移动鼠标，等待测试连接。退出 ATK HUB。");
            var deadline=DateTime.UtcNow.AddSeconds(90);
            while(!mouseActivity.Recent&&DateTime.UtcNow<deadline)await Task.Delay(100);
            if(!mouseActivity.Recent)throw new TimeoutException("没有检测到这只鼠标的活动。");
            var info=await Task.Run(()=>session.Connect(false));
            connected=true;wiredConnection=false;
            CustomConnectionText.Text=$"2.4G 已连接 · 固件 {info.Firmware}";
            sequence.Clear();
            foreach(string name in new[]{"红色","蓝色","绿色"})
            {
                var color=((IEnumerable<ColorChoice>)Palette.ItemsSource).First(c=>c.Name==name);
                AddColor(color,sequence.Count);sequence[^1].HoldSeconds=.5;sequence[^1].FadeSeconds=2;
            }
            PlaybackState? previous=null;
            void Observe(PlaybackUpdate update)
            {
                if(update.State==previous)return;
                previous=update.State;
                transitions.Add(new{state=update.State.ToString(),commands=session.CommandCount,verifiedFrames=session.VerifiedPlaybackUpdates});
            }
            player.Updated+=Observe;
            SetStatus("实机播放测试","请轻移约 10 秒观察渐变，随后放稳约 5 秒。测试结束后会恢复原设置。");
            player.Start(sequence,false);
            var timer=Stopwatch.StartNew();double quietSince=-1;long quietCount=-1;
            while(timer.Elapsed.TotalSeconds<60&&player.Running)
            {
                await Task.Delay(100);
                if(previous==PlaybackState.IdlePaused&&!player.Sending&&session.VerifiedPlaybackUpdates>=3)
                {
                    if(quietSince<0){quietSince=timer.Elapsed.TotalSeconds;quietCount=session.CommandCount;}
                    if(session.CommandCount!=quietCount)throw new IOException("静置暂停时出现控制通信。");
                    if(timer.Elapsed.TotalSeconds-quietSince>=3){silence=true;break;}
                }
                else quietSince=-1;
            }
            await player.Stop();player.Updated-=Observe;
            if(!silence)throw new IOException("没有完成至少 3 秒的静置无通信检查。");
        }
        catch(Exception exception){error=exception.Message;}
        finally
        {
            if(player is not null)await player.Stop();
            if(session.Dirty&&mouseActivity is not null)
            {
                SetStatus("测试已停止","请轻轻移动鼠标以完成原设置恢复。",InfoBarSeverity.Informational);
                var deadline=DateTime.UtcNow.AddSeconds(90);
                while(!mouseActivity.Recent&&DateTime.UtcNow<deadline)await Task.Delay(100);
                if(mouseActivity.Recent)
                {
                    try{await Task.Run(session.Restore);restored=await Task.Run(session.VerifyOriginal);}
                    catch(Exception exception){error=(error is null?"":error+" ")+exception.Message;}
                }
            }
            else if(connected)restored=await Task.Run(session.VerifyOriginal);
            busy=false;UpdateControls();
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);
            File.WriteAllText(output,JsonSerializer.Serialize(new{success=error is null&&silence&&restored,wireless=true,verifiedFrames=session.VerifiedPlaybackUpdates,idleSilentForAtLeast3Seconds=silence,original96BytesRestored=restored,pendingRecovery=session.Dirty,transitions,error},new JsonSerializerOptions{WriteIndented=true}));
        }
    }
}
