using Microsoft.UI.Xaml;
using System.Text.Json;
using System.Runtime.InteropServices;

namespace ZeroExLight;
public partial class App : Application
{
    private Window? window;
    // Held for the process lifetime, including diagnostic modes. A second instance
    // must never share the control replies or overwrite a pending recovery record.
    private readonly Mutex ownership=new(false,@"Local\ZeroExLight.373B.Control");
    private bool ownsControl;
    public static readonly string[] Args = Environment.GetCommandLineArgs().Skip(1).ToArray();
    public App()
    {
        InitializeComponent();
        UnhandledException += (_, e) => {
            try { File.AppendAllText(Path.Combine(AppContext.BaseDirectory,"crash.log"), DateTimeOffset.Now+" "+e.Exception+Environment.NewLine); } catch { }
        };
    }
    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        try { ownsControl=ownership.WaitOne(0); }
        catch(AbandonedMutexException) { ownsControl=true; }
        if(!ownsControl)
        {
            const string message="灯光控制程序已经在运行。请使用已打开的窗口；同一时间只允许一个实例控制鼠标。";
            if(Args.Length>=2&&Args[0] is "--self-test" or "--probe" or "--verify-protocol" or "--verify-custom-ui" or "--verify-custom-device" or "--verify-retained-exit")
                WriteResult(Args[1],new{success=false,alreadyRunning=true,error=message});
            else MessageBox(IntPtr.Zero,message,"ZERO EX · 灯光控制",0x40);
            Exit();return;
        }
        if (Args.Length >= 2 && Args[0] is "--self-test" or "--probe" or "--verify-protocol")
        {
            object result;
            try
            {
                result = await Task.Run(() => Args[0]=="--self-test" ? Diagnostics.SelfTest() : Diagnostics.Probe(Args[0]=="--verify-protocol"));
            }
            catch(Exception e) { result = new { success=false, error=e.ToString() }; }
            WriteResult(Args[1],result);
            Exit(); return;
        }
        window = new MainWindow();
        window.Activate();
    }
    private static void WriteResult(string path,object result)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        File.WriteAllText(path,JsonSerializer.Serialize(result,new JsonSerializerOptions{WriteIndented=true}));
    }
    internal static void WriteDiagnostic(string path,object result)=>WriteResult(path,result);
    [DllImport("user32.dll",CharSet=CharSet.Unicode,EntryPoint="MessageBoxW")]
    private static extern int MessageBox(IntPtr owner,string text,string caption,uint type);
}
