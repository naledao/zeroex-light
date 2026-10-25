using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace ZeroExLight;

public sealed record HidEndpoint(string Path,ushort Pid,string Serial);

public sealed class HidDevice : IDisposable
{
    internal Func<bool>? CanSend {get;set;}
    private long commandsSent;
    internal long CommandsSent=>Interlocked.Read(ref commandsSent);
    private readonly SafeFileHandle handle;
    public HidEndpoint Endpoint {get;}
    private const uint Pending=997;
    public HidDevice(HidEndpoint endpoint)
    {
        Endpoint=endpoint;
        handle=Native.CreateFile(endpoint.Path,0xC0000000,3,IntPtr.Zero,3,0x40000000,IntPtr.Zero);
        if(handle.IsInvalid) throw new Win32Exception(Marshal.GetLastWin32Error(),"无法打开鼠标控制接口，请退出 ATK HUB 后重试。");
    }
    public static List<HidEndpoint> Enumerate(ushort pid)
    {
        Native.HidD_GetHidGuid(out var guid);
        var set=Native.SetupDiGetClassDevs(ref guid,null,IntPtr.Zero,0x12);
        if(set==new IntPtr(-1)) throw new Win32Exception(Marshal.GetLastWin32Error());
        var result=new List<HidEndpoint>();
        try
        {
            for(uint index=0;;index++)
            {
                var info=new Native.InterfaceData{Size=Marshal.SizeOf<Native.InterfaceData>()};
                if(!Native.SetupDiEnumDeviceInterfaces(set,IntPtr.Zero,ref guid,index,ref info))
                { if(Marshal.GetLastWin32Error()==259) break; throw new Win32Exception(Marshal.GetLastWin32Error()); }
                Native.SetupDiGetDeviceInterfaceDetail(set,ref info,IntPtr.Zero,0,out var size,IntPtr.Zero);
                var detail=Marshal.AllocHGlobal((int)size);
                try
                {
                    Marshal.WriteInt32(detail,IntPtr.Size==8?8:6);
                    if(!Native.SetupDiGetDeviceInterfaceDetail(set,ref info,detail,size,out _,IntPtr.Zero)) continue;
                    var path=Marshal.PtrToStringUni(detail+4)!;
                    if(!path.Contains($"vid_373b&pid_{pid:x4}",StringComparison.OrdinalIgnoreCase)) continue;
                    using var metadata=Native.CreateFile(path,0,3,IntPtr.Zero,3,0,IntPtr.Zero);
                    if(metadata.IsInvalid) continue;
                    var attrs=new Native.Attributes{Size=Marshal.SizeOf<Native.Attributes>()};
                    if(!Native.HidD_GetAttributes(metadata,ref attrs)||attrs.Vendor!=0x373b||attrs.Product!=pid) continue;
                    if(!Native.HidD_GetPreparsedData(metadata,out var preparsed)) continue;
                    try
                    {
                        if(Native.HidP_GetCaps(preparsed,out var caps)<0||caps.UsagePage!=0xff02||caps.Usage!=2||caps.InputLength!=17||caps.OutputLength!=17) continue;
                        // Report ID 8 was verified against this device's actual descriptor.
                        var str=Marshal.AllocHGlobal(512);
                        string serial="";
                        try { if(Native.HidD_GetSerialNumberString(metadata,str,512)) serial=Marshal.PtrToStringUni(str)??""; }
                        finally { Marshal.FreeHGlobal(str); }
                        result.Add(new HidEndpoint(path,pid,serial.Length>0?serial:path));
                    }
                    finally { Native.HidD_FreePreparsedData(preparsed); }
                }
                finally { Marshal.FreeHGlobal(detail); }
            }
        }
        finally { Native.SetupDiDestroyDeviceInfoList(set); }
        return result;
    }
    private byte[] Io(byte[] buffer,bool write,int timeout=1800)
    {
        var pin=GCHandle.Alloc(buffer,GCHandleType.Pinned);
        var ev=Native.CreateEvent(IntPtr.Zero,true,false,null);
        if(ev==IntPtr.Zero) { pin.Free(); throw new Win32Exception(Marshal.GetLastWin32Error()); }
        var ov=new Native.Overlapped{Event=ev};
        try
        {
            bool ok=write?Native.WriteFile(handle,pin.AddrOfPinnedObject(),(uint)buffer.Length,out var transferred,ref ov):Native.ReadFile(handle,pin.AddrOfPinnedObject(),(uint)buffer.Length,out transferred,ref ov);
            if(!ok)
            {
                var error=Marshal.GetLastWin32Error();
                if(error!=Pending) throw new Win32Exception(error);
                var wait=Native.WaitForSingleObject(ev,(uint)timeout);
                if(wait!=0)
                {
                    Native.CancelIoEx(handle,ref ov);
                    Native.GetOverlappedResult(handle,ref ov,out _,true);
                    throw new TimeoutException("鼠标可能已休眠或断开。请移动鼠标唤醒，再重新连接或恢复设置。");
                }
                if(!Native.GetOverlappedResult(handle,ref ov,out transferred,false)) throw new Win32Exception(Marshal.GetLastWin32Error());
            }
            if(write&&transferred!=buffer.Length) throw new IOException("控制报告未完整发送。");
            return buffer.Take((int)transferred).ToArray();
        }
        finally { Native.CloseHandle(ev); pin.Free(); }
    }
    public static byte[] Frame(byte command,ushort address=0,byte[]? data=null,byte readLength=0)
    {
        if(command is not(4 or 7 or 8 or 14 or 16 or 18)) throw new ArgumentException("Unsupported command");
        if(command==7 && (data is null||data.Length!=8||address is not(44 or 52 or 60 or 68 or 76))) throw new ArgumentException("Write outside lighting region");
        if(command==8&&(readLength is <1 or >8||address+readLength>96)) throw new ArgumentException("Read outside backed-up region");
        var frame=new byte[17];frame[0]=8;frame[1]=command;frame[3]=(byte)(address>>8);frame[4]=(byte)address;frame[5]=command==7?(byte)data!.Length:readLength;
        if(data is not null) Array.Copy(data,0,frame,6,data.Length);
        frame[16]=(byte)((0x55-frame.Take(16).Sum(x=>(int)x))&255);
        return frame;
    }
    public byte[] Exchange(byte command,ushort address=0,byte[]? data=null,byte length=0)
    {
        if(CanSend is not null&&!CanSend())throw new PlaybackIdleException();
        Interlocked.Increment(ref commandsSent);
        Io(Frame(command,address,data,length),true);
        var end=DateTime.UtcNow.AddSeconds(3);
        while(DateTime.UtcNow<end)
        {
            var reply=Io(new byte[17],false);
            if(reply.Length!=17||reply[0]!=8||reply[1]!=command) continue;
            if((reply.Sum(x=>(int)x)&255)!=0x55) throw new IOException("鼠标回复校验失败，请重新连接。");
            if(reply[2]!=0) throw new IOException($"设备未接受操作（状态 {reply[2]}）。");
            if(command==8&&(reply[3]!=(byte)(address>>8)||reply[4]!=(byte)address||reply[5]!=length)) throw new IOException("配置回复地址不匹配。");
            return reply.Skip(1).ToArray();
        }
        throw new TimeoutException("没有收到匹配的鼠标回复。");
    }
    public byte[] Read(ushort address)=>Exchange(8,address,length:8).Skip(5).Take(8).ToArray();
    public byte[] ReadConfiguration()=>Enumerable.Range(0,12).SelectMany(i=>Read((ushort)(i*8))).ToArray();
    public void Write(ushort address,byte[] data)
    {
        Exchange(7,address,data);
        if(!Read(address).SequenceEqual(data)) throw new IOException("写入后的配置核对不一致，请恢复原设置。");
    }
    public void Dispose()=>handle.Dispose();

    private static class Native
    {
        [StructLayout(LayoutKind.Sequential)] public struct InterfaceData {public int Size;public Guid ClassGuid;public uint Flags;public UIntPtr Reserved;}
        [StructLayout(LayoutKind.Sequential)] public struct Attributes {public int Size;public ushort Vendor,Product,Version;}
        [StructLayout(LayoutKind.Sequential)] public struct Caps {public ushort Usage,UsagePage,InputLength,OutputLength,FeatureLength;[MarshalAs(UnmanagedType.ByValArray,SizeConst=27)] public ushort[] Rest;}
        [StructLayout(LayoutKind.Sequential)] public struct Overlapped {public UIntPtr Internal,InternalHigh;public uint Offset,OffsetHigh;public IntPtr Event;}
        [DllImport("setupapi.dll",CharSet=CharSet.Unicode,SetLastError=true,EntryPoint="SetupDiGetClassDevsW")] public static extern IntPtr SetupDiGetClassDevs(ref Guid guid,string? enumerator,IntPtr hwnd,uint flags);
        [DllImport("setupapi.dll",SetLastError=true)] [return:MarshalAs(UnmanagedType.Bool)] public static extern bool SetupDiEnumDeviceInterfaces(IntPtr set,IntPtr data,ref Guid guid,uint index,ref InterfaceData info);
        [DllImport("setupapi.dll",CharSet=CharSet.Unicode,SetLastError=true,EntryPoint="SetupDiGetDeviceInterfaceDetailW")] [return:MarshalAs(UnmanagedType.Bool)] public static extern bool SetupDiGetDeviceInterfaceDetail(IntPtr set,ref InterfaceData info,IntPtr detail,uint size,out uint required,IntPtr data);
        [DllImport("setupapi.dll")] public static extern bool SetupDiDestroyDeviceInfoList(IntPtr set);
        [DllImport("hid.dll")] public static extern void HidD_GetHidGuid(out Guid guid);
        [DllImport("hid.dll")] [return:MarshalAs(UnmanagedType.U1)] public static extern bool HidD_GetAttributes(SafeFileHandle handle,ref Attributes attrs);
        [DllImport("hid.dll")] [return:MarshalAs(UnmanagedType.U1)] public static extern bool HidD_GetPreparsedData(SafeFileHandle handle,out IntPtr data);
        [DllImport("hid.dll")] [return:MarshalAs(UnmanagedType.U1)] public static extern bool HidD_FreePreparsedData(IntPtr data);
        [DllImport("hid.dll")] public static extern int HidP_GetCaps(IntPtr data,out Caps caps);
        [DllImport("hid.dll")] [return:MarshalAs(UnmanagedType.U1)] public static extern bool HidD_GetSerialNumberString(SafeFileHandle handle,IntPtr buffer,uint length);
        [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true,EntryPoint="CreateFileW")] public static extern SafeFileHandle CreateFile(string path,uint access,uint share,IntPtr security,uint creation,uint flags,IntPtr template);
        [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true,EntryPoint="CreateEventW")] public static extern IntPtr CreateEvent(IntPtr security,bool manual,bool initial,string? name);
        [DllImport("kernel32.dll",SetLastError=true)] public static extern bool ReadFile(SafeFileHandle h,IntPtr buffer,uint length,out uint transferred,ref Overlapped ov);
        [DllImport("kernel32.dll",SetLastError=true)] public static extern bool WriteFile(SafeFileHandle h,IntPtr buffer,uint length,out uint transferred,ref Overlapped ov);
        [DllImport("kernel32.dll",SetLastError=true)] public static extern bool GetOverlappedResult(SafeFileHandle h,ref Overlapped ov,out uint transferred,bool wait);
        [DllImport("kernel32.dll")] public static extern bool CancelIoEx(SafeFileHandle h,ref Overlapped ov);
        [DllImport("kernel32.dll")] public static extern uint WaitForSingleObject(IntPtr handle,uint time);
        [DllImport("kernel32.dll")] public static extern bool CloseHandle(IntPtr handle);
    }
}
