using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Windowing;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Graphics;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.UI;

namespace ZeroExLight;
public sealed record ColorChoice(string Name,byte R,byte G,byte B)
{
    public SolidColorBrush Brush=>new(Color.FromArgb(255,R,G,B));
    public string Hex=>$"#{R:X2}{G:X2}{B:X2}";
    public override string ToString()=>Name;
}
public sealed partial class MainWindow : Window
{
    private readonly LightSession session=new();
    private bool busy,connected,allowClose;
    public MainWindow()
    {
        InitializeComponent();
        UpdateExitGuidance();
        Palette.ItemsSource=new[]{new ColorChoice("青色",0,255,255),new ColorChoice("红色",255,0,0),new ColorChoice("绿色",0,255,0),new ColorChoice("蓝色",0,0,255),new ColorChoice("紫色",255,0,255),new ColorChoice("黄色",255,255,0),new ColorChoice("白色",255,255,255)};
        Palette.SelectedIndex=0;
        var hwnd=WinRT.Interop.WindowNative.GetWindowHandle(this);
        InitializeCustom(hwnd);
        var appWindow=AppWindow.GetFromWindowId(Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd));
        appWindow.SetIcon(Path.Combine(AppContext.BaseDirectory,"Assets","ZeroExLight.ico"));
        var scale=GetDpiForWindow(hwnd)/96.0;
        var area=DisplayArea.GetFromWindowId(appWindow.Id,DisplayAreaFallback.Primary).WorkArea;
        void SetMinimumSize()
        {
            if(appWindow.Presenter is OverlappedPresenter presenter)
            {
                double currentScale=GetDpiForWindow(hwnd)/96.0;
                presenter.PreferredMinimumWidth=(int)(460*currentScale);
                presenter.PreferredMinimumHeight=(int)(460*currentScale);
            }
        }
        SetMinimumSize();
        bool narrowCapture=App.Args.Length>=2&&App.Args[0] is "--capture-narrow" or "--capture-custom-narrow" or "--capture-exit-narrow";
        appWindow.Resize(new SizeInt32(Math.Min((int)((narrowCapture?460:760)*scale),area.Width-60),Math.Min((int)(830*scale),area.Height-60)));
        appWindow.Closing+=Closing;
        Closed+=(_,_)=>{mouseActivity?.Dispose();session.Dispose();};
        Root.Loaded+=async (_,_)=>
        {
            Root.XamlRoot.Changed+=(_,_)=>SetMinimumSize();
            if(App.Args.Length>=2&&App.Args[0].StartsWith("--capture"))
            {
                if(App.Args[0].Contains("custom"))Pages.SelectedIndex=1;
                if(App.Args[0]=="--capture-exit-restore")RestoreOnExit.IsChecked=true;
                await Task.Delay(700);
                if(App.Args[0].StartsWith("--capture-exit")){LightingScroll.ChangeView(null,double.MaxValue,null);await Task.Delay(150);}
                if(App.Args[0]=="--capture-custom-bottom"){CustomScroll.ChangeView(null,double.MaxValue,null);await Task.Delay(150);}
                await Capture(App.Args[1]);
                allowClose=true;Close();
            }
            if(App.Args.Length>=2&&App.Args[0]=="--verify-custom-ui")
            {
                await VerifyCustomUi(App.Args[1]);allowClose=true;Close();
            }
            if(App.Args.Length>=2&&App.Args[0]=="--verify-custom-device")
            {
                await VerifyCustomDevice(App.Args[1]);
                if(!session.Dirty){allowClose=true;Close();}
            }
            if(App.Args.Length>=2&&App.Args[0]=="--verify-retained-exit")await VerifyRetainedExit(App.Args[1]);
        };
    }
    private void SetStatus(string title,string message,InfoBarSeverity severity=InfoBarSeverity.Informational)
    {
        Status.Title=title;Status.Message=message;Status.Severity=severity;Status.IsOpen=true;
        if(CustomStatus is not null){CustomStatus.Title=title;CustomStatus.Message=message;CustomStatus.Severity=severity;CustomStatus.IsOpen=true;}
    }
    private void UpdateControls()
    {
        ConnectButton.IsEnabled=!busy&&!Playing;
        ApplyButton.IsEnabled=connected&&!busy&&!Playing&&Palette.SelectedItem is ColorChoice;
        RestoreButton.IsEnabled=session.Dirty&&!busy;
        Transport.IsEnabled=!busy&&!session.Dirty&&!Playing;
        Palette.IsEnabled=!busy;
        UpdateCustomControls();
    }
    private async Task Run(Func<Task> operation)
    {
        if(busy)return;
        busy=true;UpdateControls();
        try{await operation();}
        catch(Exception e){if(!connected)ConnectionText.Text="尚未连接";SetStatus(session.Dirty?"需要恢复设置":"操作未完成",e.Message,InfoBarSeverity.Warning);}
        finally{busy=false;UpdateControls();}
    }
    private async void Connect_Click(object sender,RoutedEventArgs e)=>await Run(async()=>
    {
        await StopPlayback();
        connected=false;ConnectionText.Text="正在连接";
        SetStatus("正在连接","正在读取并备份灯光配置……");
        bool wired=Transport.SelectedIndex==1;
        var info=await Task.Run(()=>session.Connect(wired));
        connected=true;
        wiredConnection=wired;
        if(mouseActivity is not null)mouseActivity.Pid=wired?(ushort)0x11cc:(ushort)0x11fe;
        ConnectionText.Text=wired?"USB 有线已连接":"2.4G 无线已连接";
        DeviceText.Text=$"固件 {info.Firmware} · 配置档 {info.Profile} · DPI 档位 {info.DpiSlot}";
        BatteryText.Text=$"连接时 {info.Battery}%"+(info.Charging?" · 充电中":"");
        ConnectButton.Content="重新连接";
        CustomConnectButton.Content="重新连接";
        CustomConnectionText.Text=wired?"USB 有线已连接 · 渐变播放请用 2.4G":$"2.4G 已连接 · 固件 {info.Firmware}";
        if(session.Dirty&&session.RetainedOnExit)
            SetStatus("已载入保留灯光的记录","上次选择了退出保留灯光；原设置备份仍可随时恢复。",InfoBarSeverity.Success);
        else SetStatus(session.Dirty?"发现待恢复的设置":"原设置已备份",session.Dirty?"上次的恢复记录已载入，可继续恢复原设置。":"选择颜色后点击应用。DPI 数值和回报率保持原样。",session.Dirty?InfoBarSeverity.Warning:InfoBarSeverity.Success);
    });
    private void Palette_SelectionChanged(object sender,SelectionChangedEventArgs e)
    {
        if(Palette.SelectedItem is ColorChoice c) SelectionText.Text=$"已选择 {c.Name} · {c.Hex}";
        if(ApplyButton is not null)UpdateControls();
    }
    private async void Apply_Click(object sender,RoutedEventArgs e)=>await Run(async()=>
    {
        if(Palette.SelectedItem is not ColorChoice c)return;
        SetStatus("正在应用",$"正在设置{c.Name}并核对结果……");
        await Task.Run(()=>session.Apply(c.R,c.G,c.B));
        SetStatus($"已应用{c.Name}","颜色配置已写入并读回核对。可随时恢复连接前的灯光设置。",InfoBarSeverity.Success);
    });
    private async Task RestoreCore()
    {
        await StopPlayback();
        SetStatus("正在恢复","正在恢复并核对连接前的灯光设置……");
        await Task.Run(session.Restore);
        SetStatus("已恢复原设置","修改过的灯光配置已恢复并核对，DPI 数值未改变。",InfoBarSeverity.Success);
    }
    private async void Restore_Click(object sender,RoutedEventArgs e)=>await Run(RestoreCore);
    private void ExitPreference_Changed(object sender,RoutedEventArgs e)=>UpdateExitGuidance();
    private void UpdateExitGuidance()
    {
        bool restore=RestoreOnExit?.IsChecked==true;
        string message=restore?"退出时恢复原灯光配置，原配置关闭时灯会熄灭。":"默认退出保留最后设置的颜色，循环渐变随软件退出而停止。";
        if(ExitBehaviorDescription is not null)ExitBehaviorDescription.Text=message;
        if(CustomExitBehaviorHint is not null)CustomExitBehaviorHint.Text=(restore?"退出将恢复原灯光。":"退出保留最后颜色。")+"静置后仍按原设置休眠熄灯。";
    }
    private async void Closing(AppWindow sender,AppWindowClosingEventArgs e)
    {
        if(allowClose)return;
        if(busy){e.Cancel=true;SetStatus("操作进行中","请等待当前操作完成后再退出。");return;}
        if(!Playing&&!session.Dirty){allowClose=true;return;}
        e.Cancel=true;
        bool restoreOnExit=RestoreOnExit.IsChecked==true;
        await Run(async()=>
        {
            try
            {
                await StopPlayback();
                if(session.Dirty)
                {
                    if(restoreOnExit)await RestoreCore();
                    else await Task.Run(session.RetainCurrentOnExit);
                }
                beforeCloseVerification?.Invoke();
                allowClose=true;Close();
            }
            catch(Exception ex)
            {
                SetStatus(restoreOnExit?"退出前恢复未完成":"退出记录未保存",ex.Message,InfoBarSeverity.Warning);
                var dialog=new ContentDialog{XamlRoot=Root.XamlRoot,Title=restoreOnExit?"原设置尚未恢复":"退出记录尚未保存",Content=restoreOnExit?"请移动鼠标唤醒，再点恢复原设置。配置备份和恢复记录已保留；也可以保留当前设置退出。":"保留灯光的标记未能保存。原设置备份和恢复记录仍保留，可以返回重试，或保留当前灯光退出。",PrimaryButtonText="保留当前设置退出",CloseButtonText=restoreOnExit?"返回恢复":"返回",DefaultButton=ContentDialogButton.Close};
                if(await dialog.ShowAsync()==ContentDialogResult.Primary){allowClose=true;Close();}
            }
        });
    }
    private void Backup_Click(object sender,RoutedEventArgs e)
    {
        try{Directory.CreateDirectory(session.BackupDirectory);Process.Start(new ProcessStartInfo("explorer.exe",session.BackupDirectory){UseShellExecute=true});}
        catch(Exception ex){SetStatus("无法打开备份文件夹",ex.Message,InfoBarSeverity.Warning);}
    }
    private async Task Capture(string path)
    {
        var bitmap=new RenderTargetBitmap();await bitmap.RenderAsync(Root);
        var pixels=await bitmap.GetPixelsAsync();
        var folder=await StorageFolder.GetFolderFromPathAsync(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var file=await folder.CreateFileAsync(Path.GetFileName(path),CreationCollisionOption.ReplaceExisting);
        using var stream=await file.OpenAsync(FileAccessMode.ReadWrite);
        var encoder=await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId,stream);
        encoder.SetPixelData(BitmapPixelFormat.Bgra8,BitmapAlphaMode.Premultiplied,(uint)bitmap.PixelWidth,(uint)bitmap.PixelHeight,96,96,pixels.ToArray());
        await encoder.FlushAsync();
    }
    [DllImport("user32.dll")] private static extern uint GetDpiForWindow(IntPtr hwnd);
}
