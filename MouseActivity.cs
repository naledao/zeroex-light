using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;

namespace ZeroExLight;

// Receive existing Windows mouse reports. Never query the vendor control endpoint.
public sealed class MouseActivity : IDisposable
{
    private readonly IntPtr hwnd;
    private readonly SubclassProc callback;
    private readonly Dictionary<IntPtr,bool> devices=[];
    private long lastActivity=long.MinValue;
    private ushort pid=0x11fe;
    private bool disposed;
    public ushort Pid {get=>pid;set{pid=value;devices.Clear();Interlocked.Exchange(ref lastActivity,long.MinValue);}}
    public double IdleSeconds {get{long value=Interlocked.Read(ref lastActivity);return value==long.MinValue?double.PositiveInfinity:Math.Max(0,(Environment.TickCount64-value)/1000.0);}}
    public bool Recent=>!disposed&&IdleSeconds<2;
    public MouseActivity(IntPtr hwnd)
    {
        this.hwnd=hwnd;callback=WindowMessage;
        if(!SetWindowSubclass(hwnd,callback,73,UIntPtr.Zero))throw new Win32Exception(Marshal.GetLastWin32Error());
        var registration=new InputDevice{Page=1,Usage=2,Flags=0x2100,Window=hwnd};
        if(!RegisterRawInputDevices([registration],1,(uint)Marshal.SizeOf<InputDevice>()))
        {RemoveWindowSubclass(hwnd,callback,73);throw new Win32Exception(Marshal.GetLastWin32Error());}
    }
    private IntPtr WindowMessage(IntPtr window,uint message,UIntPtr wParam,IntPtr lParam,UIntPtr id,UIntPtr data)
    {
        if(message==0xfe)devices.Clear();
        if(message==0xff&&!disposed)
        {
            uint size=0;
            if(GetRawInputData(lParam,0x10000003,IntPtr.Zero,ref size,(uint)Marshal.SizeOf<InputHeader>())!=uint.MaxValue&&size>=48&&size<4096)
            {
                var buffer=Marshal.AllocHGlobal((int)size);
                try
                {
                    if(GetRawInputData(lParam,0x10000003,buffer,ref size,(uint)Marshal.SizeOf<InputHeader>())!=uint.MaxValue)
                    {
                        var header=Marshal.PtrToStructure<InputHeader>(buffer);
                        if(header.Type==0&&Matches(header.Device))
                        {
                            int offset=Marshal.SizeOf<InputHeader>();
                            int buttons=(ushort)Marshal.ReadInt16(buffer,offset+4);
                            int x=Marshal.ReadInt32(buffer,offset+12),y=Marshal.ReadInt32(buffer,offset+16);
                            if(buttons!=0||x!=0||y!=0)
                            {
                                uint age=unchecked(GetTickCount()-(uint)GetMessageTime());
                                Interlocked.Exchange(ref lastActivity,Environment.TickCount64-age);
                            }
                        }
                    }
                }
                finally{Marshal.FreeHGlobal(buffer);}
            }
        }
        return DefSubclassProc(window,message,wParam,lParam);
    }
    private bool Matches(IntPtr device)
    {
        if(devices.TryGetValue(device,out bool match))return match;
        uint length=0;GetRawInputDeviceInfo(device,0x20000007,null,ref length);
        if(length==0||length>4096)return false;
        var name=new StringBuilder((int)length+1);
        match=GetRawInputDeviceInfo(device,0x20000007,name,ref length)!=uint.MaxValue&&name.ToString().Contains($"vid_373b&pid_{pid:x4}",StringComparison.OrdinalIgnoreCase);
        devices[device]=match;return match;
    }
    public void Dispose()
    {
        if(disposed)return;disposed=true;
        RegisterRawInputDevices([new InputDevice{Page=1,Usage=2,Flags=1}],1,(uint)Marshal.SizeOf<InputDevice>());
        RemoveWindowSubclass(hwnd,callback,73);
    }
    [StructLayout(LayoutKind.Sequential)] private struct InputDevice{public ushort Page,Usage;public uint Flags;public IntPtr Window;}
    [StructLayout(LayoutKind.Sequential)] private struct InputHeader{public uint Type,Size;public IntPtr Device;public UIntPtr Param;}
    private delegate IntPtr SubclassProc(IntPtr hwnd,uint msg,UIntPtr wParam,IntPtr lParam,UIntPtr id,UIntPtr data);
    [DllImport("comctl32.dll",SetLastError=true)] private static extern bool SetWindowSubclass(IntPtr hwnd,SubclassProc callback,UIntPtr id,UIntPtr data);
    [DllImport("comctl32.dll")] private static extern bool RemoveWindowSubclass(IntPtr hwnd,SubclassProc callback,UIntPtr id);
    [DllImport("comctl32.dll")] private static extern IntPtr DefSubclassProc(IntPtr hwnd,uint msg,UIntPtr wParam,IntPtr lParam);
    [DllImport("user32.dll",SetLastError=true)] private static extern bool RegisterRawInputDevices(InputDevice[] devices,uint count,uint size);
    [DllImport("user32.dll")] private static extern uint GetRawInputData(IntPtr input,uint command,IntPtr data,ref uint size,uint headerSize);
    [DllImport("user32.dll",CharSet=CharSet.Unicode,EntryPoint="GetRawInputDeviceInfoW")] private static extern uint GetRawInputDeviceInfo(IntPtr device,uint command,StringBuilder? data,ref uint size);
    [DllImport("user32.dll")] private static extern int GetMessageTime();
    [DllImport("kernel32.dll")] private static extern uint GetTickCount();
}
