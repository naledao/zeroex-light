using System.Text.Json;

namespace ZeroExLight;

public sealed class RecoveryRecord
{
    public ushort Pid {get;set;}
    public string Serial {get;set;}="";
    public string Firmware {get;set;}="";
    public byte Profile {get;set;}
    public string OriginalHex {get;set;}="";
    public string BackupFile {get;set;}="";
    public List<ushort> Modified {get;set;}=[];
    public bool RetainedOnExit {get;set;}
    public DateTimeOffset CreatedAt {get;set;}
}
public sealed record DeviceInfo(string Firmware,int Profile,int DpiSlot,int Battery,bool Charging,bool Wired,string BackupFile);

public sealed class LightSession : IDisposable
{
    private readonly object gate=new();
    private HidDevice? device;
    private RecoveryRecord? recovery;
    private byte[]? playbackPair;
    private int playbackSlot=-1;
    private long verifiedPlaybackUpdates;
    private readonly string statePath;
    public string BackupDirectory {get;}
    public bool Dirty {get {lock(gate) return recovery?.Modified.Count>0;}}
    public bool RetainedOnExit {get {lock(gate) return recovery?.RetainedOnExit==true;}}
    public long CommandCount=>device?.CommandsSent??0;
    public long VerifiedPlaybackUpdates=>Interlocked.Read(ref verifiedPlaybackUpdates);
    public bool VerifyOriginal()
    {
        lock(gate)
        {
            CheckProfile();
            return device!.ReadConfiguration().SequenceEqual(Convert.FromHexString(recovery!.OriginalHex));
        }
    }
    internal byte[] SnapshotForVerification()
    {
        lock(gate){CheckProfile();return device!.ReadConfiguration();}
    }
    public LightSession()
    {
        BackupDirectory=Path.Combine(AppContext.BaseDirectory,"Backups");
        statePath=Path.Combine(BackupDirectory,"pending-session.json");
    }
    private static readonly JsonSerializerOptions JsonOptions=new(){WriteIndented=true};
    private void Save()
    {
        Directory.CreateDirectory(BackupDirectory);
        File.WriteAllText(statePath+".tmp",JsonSerializer.Serialize(recovery,JsonOptions));
        File.Move(statePath+".tmp",statePath,true);
    }
    public DeviceInfo Connect(bool wired)
    {
        lock(gate)
        {
            ushort pid=wired?(ushort)0x11cc:(ushort)0x11fe;
            var endpoints=HidDevice.Enumerate(pid);
            if(endpoints.Count!=1) throw new IOException(wired?"没有找到唯一的有线 ZERO EX。请检查数据线。":"没有找到唯一的 NANO 接收器。请插入接收器，并移动鼠标唤醒。");
            device?.Dispose();device=null;playbackPair=null;Interlocked.Exchange(ref verifiedPlaybackUpdates,0);
            var candidate=new HidDevice(endpoints[0]);
            try
            {
                var version=candidate.Exchange(18);
                var firmware=$"{version[5]:X}.{version[6]:X2}";
                var identity=candidate.Exchange(16);
                if(identity[5]!=1||identity[6]!=4) throw new IOException("接收器连接的设备不是已验证的 ATK ZERO EX，已停止操作。");
                if(firmware!="3.01") throw new IOException($"当前固件 {firmware} 尚未验证。本版仅支持已实测的 3.01。");
                var profile=candidate.Exchange(14)[5];
                var config=candidate.ReadConfiguration();
                RecoveryRecord? pending=recovery?.Modified.Count>0?recovery:null;
                if(pending is null&&File.Exists(statePath))
                    pending=JsonSerializer.Deserialize<RecoveryRecord>(File.ReadAllText(statePath));
                if(pending?.Modified.Count>0)
                {
                    if(pending.Pid!=pid||pending.Serial!=endpoints[0].Serial||pending.Profile!=profile||pending.Firmware!=firmware||Convert.FromHexString(pending.OriginalHex).Length!=96||pending.Modified.Any(a=>a is not(44 or 52 or 60 or 68 or 76)))
                        throw new IOException("有尚未恢复的设置记录。请使用上次的连接方式和配置档重新连接，再恢复原设置。");
                    recovery=pending;
                }
                else
                {
                    Directory.CreateDirectory(BackupDirectory);
                    recovery=new RecoveryRecord{Pid=pid,Serial=endpoints[0].Serial,Firmware=firmware,Profile=profile,OriginalHex=Convert.ToHexString(config),CreatedAt=DateTimeOffset.Now};
                    recovery.BackupFile=Path.Combine(BackupDirectory,$"ZERO-EX-{DateTime.Now:yyyyMMdd-HHmmss-fff}.json");
                    File.WriteAllText(recovery.BackupFile,JsonSerializer.Serialize(recovery,JsonOptions));
                    Save();
                }
                var power=candidate.Exchange(4);
                device=candidate;
                return new DeviceInfo(firmware,profile+1,config[4]+1,power[5],power[6]!=0,wired,recovery.BackupFile);
            }
            catch {candidate.Dispose();throw;}
        }
    }
    private void CheckProfile()
    {
        if(device is null||recovery is null) throw new IOException("请先连接鼠标。");
        if(device.Exchange(14)[5]!=recovery.Profile) throw new IOException("鼠标配置档已切换。请回到连接时的配置档，再应用或恢复灯光。");
    }
    private void WriteTracked(ushort address,byte[] bytes)
    {
        bool recordChanged=recovery!.RetainedOnExit;
        recovery.RetainedOnExit=false;
        if(!recovery!.Modified.Contains(address))
        {
            recovery.Modified.Add(address);
            recordChanged=true;
        }
        if(recordChanged)Save(); // Persist before sending: a timed-out write may already have reached the device.
        device!.Write(address,bytes);
    }
    public void RetainCurrentOnExit()
    {
        lock(gate)
        {
            if(recovery?.Modified.Count>0){recovery.RetainedOnExit=true;Save();}
        }
    }
    public void EndPlayback(){lock(gate){playbackPair=null;playbackSlot=-1;}}
    public void PlaybackFrame(Rgb color,bool resumed,Func<bool> active)
    {
        lock(gate)
        {
            if(device is null||recovery is null)throw new IOException("请先连接鼠标。");
            device.CanSend=active;
            try
            {
                if(resumed)playbackPair=null;
                CheckProfile();
                var header=device.Read(0);int slot=header[4];
                if(slot>7)throw new IOException("当前 DPI 档位无效。");
                ushort address=(ushort)(44+(slot/2)*8);
                if(playbackPair is null||slot!=playbackSlot)
                {
                    playbackPair=device.Read(address);playbackSlot=slot;
                    var effect=device.Read(76);
                    if(effect[0]!=1||effect[6]!=1)
                    {
                        effect[0]=1;effect[1]=0x54;effect[6]=1;effect[7]=0x54;
                        WriteTracked(76,effect);
                    }
                }
                int offset=slot%2*4;
                if(playbackPair[offset]==color.R&&playbackPair[offset+1]==color.G&&playbackPair[offset+2]==color.B)return;
                var next=(byte[])playbackPair.Clone();
                next[offset]=color.R;next[offset+1]=color.G;next[offset+2]=color.B;
                next[offset+3]=(byte)((0x55-color.R-color.G-color.B)&255);
                WriteTracked(address,next);playbackPair=next;Interlocked.Increment(ref verifiedPlaybackUpdates);
            }
            catch{playbackPair=null;throw;}
            finally{device.CanSend=null;}
        }
    }
    public void Apply(byte red,byte green,byte blue)
    {
        lock(gate)
        {
            playbackPair=null;
            CheckProfile();
            var before=device!.ReadConfiguration();
            var index=before[4];
            if(index>7) throw new IOException("当前 DPI 档位无效。");
            ushort address=(ushort)(44+(index/2)*8);
            var color=device.Read(address);
            int offset=(index%2)*4;
            Array.Copy(new[]{red,green,blue,(byte)((0x55-red-green-blue)&255)},0,color,offset,4);
            var effect=device.Read(76);
            effect[0]=1;effect[1]=0x54;effect[6]=1;effect[7]=0x54;
            WriteTracked(76,effect);
            WriteTracked(address,color);
            var after=device.ReadConfiguration();
            if(!before.Take(44).SequenceEqual(after.Take(44))) throw new IOException("DPI 或性能配置意外变化，请先恢复设置并停止使用。");
        }
    }
    public void Restore()
    {
        lock(gate)
        {
            playbackPair=null;
            CheckProfile();
            if(recovery!.RetainedOnExit){recovery.RetainedOnExit=false;Save();}
            var before=device!.ReadConfiguration();
            var original=Convert.FromHexString(recovery!.OriginalHex);
            var errors=new List<string>();
            foreach(var address in recovery.Modified.AsEnumerable().Reverse().ToArray())
            {
                try
                {
                    device.Write(address,original.Skip(address).Take(8).ToArray());
                    recovery.Modified.Remove(address);Save();
                }
                catch(Exception e) {errors.Add(e.Message);}
            }
            if(errors.Count>0) throw new IOException("恢复尚未完成。请移动鼠标唤醒后，再点恢复原设置。 "+errors[0]);
            var after=device.ReadConfiguration();
            if(!before.Take(44).SequenceEqual(after.Take(44))) throw new IOException("恢复后 DPI 相关配置核对不一致。");
        }
    }
    public (int Battery,bool Charging) Power()
    {
        lock(gate)
        {
            if(device is null) throw new IOException("未连接。");
            var p=device.Exchange(4);return(p[5],p[6]!=0);
        }
    }
    public void Dispose(){lock(gate){device?.Dispose();device=null;}}
}

public static class Diagnostics
{
    public static object SelfTest()
    {
        var tests=new List<string>();
        void Assert(bool condition,string name){if(!condition)throw new Exception(name);tests.Add(name);}
        Assert(Convert.ToHexString(HidDevice.Frame(18))=="081200000000000000000000000000003B","Version request equals captured hardware frame");
        Assert(Convert.ToHexString(HidDevice.Frame(8,52,readLength:8))=="0808000034080000000000000000000009","Big-endian EEPROM read request equals captured hardware frame");
        var rgb=new byte[]{0,255,255,0x57,255,0,255,0x57};
        var write=HidDevice.Frame(7,52,rgb);
        Assert(write.Length==17&&write[0]==8&&write[1]==7&&write[4]==52&&write[5]==8&&(write.Sum(x=>(int)x)&255)==0x55,"Bounded RGB write framing and checksum");
        bool refused=false;try{HidDevice.Frame(7,0,new byte[8]);}catch(ArgumentException){refused=true;}
        Assert(refused,"Non-lighting write rejected");
        refused=false;try{HidDevice.Frame(13);}catch(ArgumentException){refused=true;}
        Assert(refused,"Firmware-upgrade command rejected");
        foreach(var color in new[]{new byte[]{0,255,255},new byte[]{255,0,0},new byte[]{0,255,0},new byte[]{0,0,255},new byte[]{255,0,255},new byte[]{255,255,0},new byte[]{255,255,255}})
            Assert(((color.Sum(x=>(int)x)+(byte)((0x55-color.Sum(x=>(int)x))&255))&255)==0x55,"RGB checksum "+Convert.ToHexString(color));
        tests.AddRange(CustomDiagnostics.Run());
        return new{success=true,tests};
    }
    public static object Probe(bool verifyWrite)
    {
        var endpoints=HidDevice.Enumerate(0x11fe);
        if(endpoints.Count!=1) endpoints=HidDevice.Enumerate(0x11cc);
        if(endpoints.Count!=1) throw new IOException("Expected one verified device");
        using var device=new HidDevice(endpoints[0]);
        var version=device.Exchange(18);var id=device.Exchange(16);var profile=device.Exchange(14);
        if(version[5]!=3||version[6]!=1||id[5]!=1||id[6]!=4) throw new IOException("Unsupported firmware or mouse");
        var before=device.ReadConfiguration();
        if(verifyWrite)
        {
            ushort address=(ushort)(44+(before[4]/2)*8);
            device.Write(address,device.Read(address)); // Verify the native write path without changing any value.
        }
        var after=device.ReadConfiguration();
        return new{success=true,firmware="3.01",transport=endpoints[0].Pid==0x11fe?"wireless":"wired",profile=profile[5],currentDpiSlot=before[4]+1,configurationUnchanged=before.SequenceEqual(after),nativeWritePathVerified=verifyWrite,configurationHex=Convert.ToHexString(after)};
    }
}
