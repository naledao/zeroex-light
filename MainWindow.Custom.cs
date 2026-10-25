using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.ApplicationModel.DataTransfer;
using Windows.UI;

namespace ZeroExLight;
public sealed partial class MainWindow
{
    private const string ColorDragFormat="application/x-zeroex-color";
    private readonly ObservableCollection<LightStep> sequence=[];
    private readonly HashSet<LightStep> observedSteps=[];
    private readonly SequenceStore sequenceStore=new(Path.Combine(AppContext.BaseDirectory,"Presets","custom-sequence.json"));
    private MouseActivity? mouseActivity;
    private PlaybackController? player;
    private bool updatingTiming,loadingDraft=true,wiredConnection;
    private bool transientView=>App.Args.FirstOrDefault()?.StartsWith("--capture")==true||App.Args.FirstOrDefault() is "--verify-custom-ui" or "--verify-custom-device" or "--verify-retained-exit";
    private bool Playing=>player?.Running==true;
    private bool CanEdit=>!busy&&!Playing;
    private void InitializeCustom(IntPtr hwnd)
    {
        SequenceList.ItemsSource=sequence;
        SourcePalette.ItemsSource=Palette.ItemsSource;SourcePalette.SelectedIndex=0;
        sequence.CollectionChanged+=Sequence_CollectionChanged;
        try
        {
            mouseActivity=new MouseActivity(hwnd);
            player=new PlaybackController(session,mouseActivity);
            player.Updated+=Playback_Updated;
            player.Failed+=error=>{SetStatus("播放已停止",error.Message+" 原设置备份已保留。",InfoBarSeverity.Warning);PlaybackText.Text="播放已停止";UpdateControls();};
        }
        catch(Exception error){SetStatus("活动检测未就绪",error.Message,InfoBarSeverity.Warning);}
        if(!transientView)
        {
            try{foreach(var step in sequenceStore.Load())sequence.Add(step);}
            catch(Exception error){CustomStatus.Title="未能读取编排";CustomStatus.Message="原文件会保留备份。"+error.Message;CustomStatus.Severity=InfoBarSeverity.Warning;CustomStatus.IsOpen=true;}
        }
        if(App.Args.FirstOrDefault() is "--capture-custom-demo" or "--capture-custom-narrow" or "--capture-custom-bottom")
            foreach(var choice in ((IEnumerable<ColorChoice>)Palette.ItemsSource).Where(c=>c.Name is "红色" or "蓝色" or "绿色"))AddColor(choice,sequence.Count);
        loadingDraft=false;
        SequenceList.SelectedIndex=sequence.Count>0?0:-1;
        RefreshSequence();UpdateCustomControls();
    }
    private void Sequence_CollectionChanged(object? sender,NotifyCollectionChangedEventArgs e)
    {
        foreach(var old in observedSteps.Where(x=>!sequence.Contains(x)).ToArray()){old.PropertyChanged-=Step_Changed;observedSteps.Remove(old);}
        foreach(var step in sequence.Where(x=>!observedSteps.Contains(x))){step.PropertyChanged+=Step_Changed;observedSteps.Add(step);}
        RefreshSequence();SaveDraft();
    }
    private void Step_Changed(object? sender,PropertyChangedEventArgs e)
    {
        if(e.PropertyName is nameof(LightStep.HoldSeconds) or nameof(LightStep.FadeSeconds)){RefreshSequence();SaveDraft();}
    }
    private void RefreshSequence()
    {
        for(int i=0;i<sequence.Count;i++)sequence[i].Order=i+1;
        SequenceEmpty.Visibility=sequence.Count==0?Visibility.Visible:Visibility.Collapsed;
        double duration=sequence.Sum(x=>x.HoldSeconds+x.FadeSeconds);
        SequenceSummary.Text=sequence.Count==0?"拖入第一个颜色，也可以点“添加所选颜色”。":$"{sequence.Count}/32 项 · 每轮 {duration:0.##} 秒 · 首尾渐变";
        if(duration<.45&&sequence.Count>0)SequenceSummary.Text+=" · 总时长至少 0.45 秒";
        if(!Playing){PlaybackText.Text=sequence.Count>0?"等待播放":"等待编排";PlaybackDetail.Text=sequence.Count>0?"可先进行界面预览":"添加颜色后可预览";}
        UpdateCustomControls();
    }
    private void SaveDraft()
    {
        if(loadingDraft||transientView)return;
        try{sequenceStore.Save(sequence);DraftState.Text="编排已自动保存";}
        catch(Exception error){DraftState.Text="编排尚未保存";SetStatus("保存未完成",error.Message,InfoBarSeverity.Warning);}
    }
    private bool ValidSequence
    {
        get{try{_=new SequenceTimeline(sequence);return true;}catch{return false;}}
    }
    private void UpdateCustomControls()
    {
        if(SequenceEditor is null)return;
        int index=SequenceList.SelectedIndex;
        SourcePalette.IsEnabled=CanEdit;SequenceList.IsEnabled=CanEdit;
        CustomConnectButton.IsEnabled=!busy&&!Playing;
        AddStepButton.IsEnabled=CanEdit&&sequence.Count<32&&SourcePalette.SelectedItem is ColorChoice;
        MoveUpButton.IsEnabled=CanEdit&&index>0;
        MoveDownButton.IsEnabled=CanEdit&&index>=0&&index<sequence.Count-1;
        RemoveStepButton.IsEnabled=CanEdit&&index>=0;ClearStepsButton.IsEnabled=CanEdit&&sequence.Count>0;
        HoldTime.IsEnabled=CanEdit&&index>=0;FadeTime.IsEnabled=CanEdit&&index>=0;
        PreviewSequenceButton.IsEnabled=!busy&&!Playing&&ValidSequence&&player is not null;
        StartSequenceButton.IsEnabled=!busy&&!Playing&&connected&&!wiredConnection&&ValidSequence&&player is not null;
        PauseSequenceButton.IsEnabled=!busy&&Playing;
        PauseSequenceButton.Content=player?.Paused==true?"继续":"暂停";
        StopSequenceButton.IsEnabled=!busy&&(Playing||session.Dirty);
        StopSequenceButton.Content=Playing&&player!.Preview?"停止预览":Playing?"停止并恢复":"恢复原设置";
    }
    private void Source_SelectionChanged(object sender,SelectionChangedEventArgs e)=>UpdateCustomControls();
    private void AddColor(ColorChoice color,int index)
    {
        if(sequence.Count>=32){SetStatus("序列已满","每个序列最多 32 项，请先移除不需要的颜色。");return;}
        var step=new LightStep{Name=color.Name,R=color.R,G=color.G,B=color.B};
        sequence.Insert(Math.Clamp(index,0,sequence.Count),step);SequenceList.SelectedItem=step;
    }
    private void AddStep_Click(object sender,RoutedEventArgs e)
    {if(CanEdit&&SourcePalette.SelectedItem is ColorChoice color)AddColor(color,sequence.Count);}
    private void Source_DragItemsStarting(object sender,DragItemsStartingEventArgs e)
    {
        if(!CanEdit){e.Cancel=true;return;}
        if(e.Items.FirstOrDefault() is ColorChoice color){e.Data.SetData(ColorDragFormat,color.Hex);e.Data.RequestedOperation=DataPackageOperation.Copy;}
    }
    private void Sequence_DragOver(object sender,DragEventArgs e)
    {
        if(!e.DataView.Contains(ColorDragFormat))return;
        e.AcceptedOperation=CanEdit&&sequence.Count<32?DataPackageOperation.Copy:DataPackageOperation.None;e.Handled=true;
    }
    private async void Sequence_Drop(object sender,DragEventArgs e)
    {
        if(!e.DataView.Contains(ColorDragFormat))return;
        e.Handled=true;if(!CanEdit)return;
        try
        {
            int index=sequence.Count;var position=e.GetPosition(SequenceList);
            for(int i=0;i<sequence.Count;i++)
                if(SequenceList.ContainerFromIndex(i) is ListViewItem item&&position.Y<item.TransformToVisual(SequenceList).TransformPoint(new(0,0)).Y+item.ActualHeight/2){index=i;break;}
            if(await AcceptColorData(e.DataView,index))e.AcceptedOperation=DataPackageOperation.Copy;
        }
        catch(Exception error){SetStatus("未能添加颜色",error.Message,InfoBarSeverity.Warning);}
    }
    private async Task<bool> AcceptColorData(DataPackageView data,int index)
    {
        if(!CanEdit||!data.Contains(ColorDragFormat))return false;
        var hex=(await data.GetDataAsync(ColorDragFormat)) as string;
        var color=((IEnumerable<ColorChoice>)SourcePalette.ItemsSource).FirstOrDefault(c=>c.Hex==hex);
        if(color is null||!CanEdit||sequence.Count>=32)return false;
        AddColor(color,index);return true;
    }
    private void Sequence_SelectionChanged(object sender,SelectionChangedEventArgs e)
    {
        if(HoldTime is null||FadeTime is null)return;
        updatingTiming=true;
        if(SequenceList.SelectedItem is LightStep step){HoldTime.Value=step.HoldSeconds;FadeTime.Value=step.FadeSeconds;}
        else{HoldTime.Value=1;FadeTime.Value=3;}
        updatingTiming=false;UpdateCustomControls();
    }
    private void Timing_ValueChanged(NumberBox sender,NumberBoxValueChangedEventArgs e)
    {
        if(updatingTiming||!CanEdit||SequenceList.SelectedItem is not LightStep step)return;
        if(!double.IsFinite(e.NewValue))return;
        if(sender==HoldTime)step.HoldSeconds=Math.Clamp(e.NewValue,0,30);else step.FadeSeconds=Math.Clamp(e.NewValue,0,30);
    }
    private void MoveSelected(int direction)
    {
        int index=SequenceList.SelectedIndex,next=index+direction;
        if(!CanEdit||index<0||next<0||next>=sequence.Count)return;
        var step=sequence[index];sequence.Move(index,next);SequenceList.SelectedItem=step;
    }
    private void MoveUp_Click(object sender,RoutedEventArgs e)=>MoveSelected(-1);
    private void MoveDown_Click(object sender,RoutedEventArgs e)=>MoveSelected(1);
    private void RemoveStep_Click(object sender,RoutedEventArgs e)
    {
        int index=SequenceList.SelectedIndex;if(!CanEdit||index<0)return;
        sequence.RemoveAt(index);SequenceList.SelectedIndex=Math.Min(index,sequence.Count-1);
    }
    private void ClearSteps_Click(object sender,RoutedEventArgs e){if(CanEdit)sequence.Clear();}
    private void StartPlayback(bool preview)
    {
        if(busy||Playing||player is null)return;
        if(SequenceList.SelectedItem is LightStep&&(!double.IsFinite(HoldTime.Value)||!double.IsFinite(FadeTime.Value)))
            throw new ArgumentException("请填入有效的停留和渐变秒数。");
        if(!preview&&(!connected||wiredConnection))throw new IOException("渐变播放请先使用 2.4G 无线连接。");
        player.Start(sequence,preview);UpdateControls();
        SetStatus(preview?"界面预览中":"播放已开始",preview?"预览只显示在窗口中。":"有鼠标活动时播放，静置后暂停发色。",InfoBarSeverity.Success);
    }
    private void StartSequence_Click(object sender,RoutedEventArgs e)
    {try{StartPlayback(false);}catch(Exception error){SetStatus("无法开始播放",error.Message,InfoBarSeverity.Warning);}}
    private void PreviewSequence_Click(object sender,RoutedEventArgs e)
    {try{StartPlayback(true);}catch(Exception error){SetStatus("无法预览",error.Message,InfoBarSeverity.Warning);}}
    private void PauseSequence_Click(object sender,RoutedEventArgs e){player?.TogglePause();UpdateCustomControls();}
    private async Task StopPlayback()
    {
        if(player is not null)await player.Stop();
        PlaybackText.Text="已停止";PlaybackDetail.Text="编排已保留，可以继续编辑";UpdateControls();
    }
    private async void StopSequence_Click(object sender,RoutedEventArgs e)=>await Run(async()=>
    {
        bool preview=Playing&&player!.Preview;
        await StopPlayback();
        if(!preview&&session.Dirty)await RestoreCore();
        else SetStatus(preview?"预览已停止":"播放已停止","编排已保留，可以继续编辑。",InfoBarSeverity.Success);
    });
    private void Playback_Updated(PlaybackUpdate update)
    {
        var color=update.Sample.Color;
        PlaybackSwatch.Background=new SolidColorBrush(Color.FromArgb(255,color.R,color.G,color.B));
        PlaybackText.Text=update.State switch{PlaybackState.IdlePaused=>"静置暂停 · 等待鼠标活动",PlaybackState.ManualPaused=>"已暂停",_=>update.Preview?"界面预览中":"渐变播放中"};
        PlaybackDetail.Text=$"序列预览 · 第 {update.Sample.Index+1} → {update.Sample.NextIndex+1} 项 · {color.Hex}";
    }
}
