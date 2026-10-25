using System.Text.Json;
using System.Runtime.InteropServices;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml.Controls;

namespace ZeroExLight;
public sealed partial class MainWindow
{
    private Action? beforeCloseVerification;
    [DllImport("user32.dll",EntryPoint="PostMessageW",SetLastError=true)]
    private static extern bool PostNativeClose(IntPtr window,uint message,IntPtr wParam,IntPtr lParam);
    // Exercise the real window-close handler, rather than duplicating exit logic.
    private async Task VerifyRetainedExit(string output)
    {
        busy=true;UpdateControls();
        try
        {
            if(mouseActivity is null)throw new IOException("鼠标活动检测未就绪。");
            mouseActivity.Pid=0x11fe;
            SetStatus("退出保留测试","请轻轻移动无线鼠标。程序将应用青色，随后正常退出并保留该颜色。");
            var deadline=DateTime.UtcNow.AddSeconds(90);
            while(!mouseActivity.Recent&&DateTime.UtcNow<deadline)await Task.Delay(100);
            if(!mouseActivity.Recent)throw new TimeoutException("未检测到无线鼠标活动。");
            await Task.Run(()=>session.Connect(false));connected=true;wiredConnection=false;
            await Task.Run(()=>session.Apply(0,255,255));
            var expected=await Task.Run(session.SnapshotForVerification);
            SetStatus("青色已应用","约 3 秒后软件将正常退出。请观察退出后灯是否仍亮。",InfoBarSeverity.Success);
            await Task.Delay(3000);
            long commandsBeforeClose=session.CommandCount;
            var hwnd=WinRT.Interop.WindowNative.GetWindowHandle(this);
            beforeCloseVerification=()=>
            {
                App.WriteDiagnostic(output,new{
                    success=RestoreOnExit.IsChecked!=true&&session.RetainedOnExit&&session.CommandCount==commandsBeforeClose,
                    defaultRestoreOnExit=RestoreOnExit.IsChecked==true,
                    retainedRecord=session.RetainedOnExit,
                    commandsBeforeClose,commandsAfterCloseHandler=session.CommandCount,
                    expectedConfigurationHex=Convert.ToHexString(expected),
                    backupDirectory=session.BackupDirectory,
                    physicalObservationRequired=true
                });
            };
            busy=false;UpdateControls();
            // Window.Close bypasses AppWindow.Closing. Request the system-menu
            // close command, exercising the same handler as the titlebar X.
            if(!PostNativeClose(hwnd,0x0112,(IntPtr)0xF060,IntPtr.Zero))throw new IOException("无法请求正常关闭窗口。");
        }
        catch(Exception error)
        {
            busy=false;UpdateControls();
            SetStatus("退出测试未完成",error.Message,InfoBarSeverity.Warning);
            App.WriteDiagnostic(output,new{success=false,error=error.ToString(),pendingRecovery=session.Dirty});
            if(!session.Dirty){allowClose=true;Close();}
        }
    }
}
