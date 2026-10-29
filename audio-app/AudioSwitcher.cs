using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using System.Xml.Serialization;

namespace WolfHubAudio
{
    internal enum Flow { Render = 0, Capture = 1 }
    internal enum Role { Console = 0, Multimedia = 1, Communications = 2 }

    internal sealed class RecordingLevelMonitor : IDisposable
    {
        internal const string MicrophonePrivacySettingsUri="ms-settings:privacy-microphone";
        private readonly string deviceId;
        private IAudioClient client;
        private IAudioCaptureClient capture;
        private IntPtr mixFormat=IntPtr.Zero, capturePointer=IntPtr.Zero;
        private WaveFormatEx format;
        private ushort encoding;
        private bool started;
        private AutoResetEvent readyEvent;

        public RecordingLevelMonitor(string id) { deviceId = id; }
        public void Start()
        {
            if (String.IsNullOrEmpty(deviceId)) return;
            var enumerator=(IMMDeviceEnumerator)new MMDeviceEnumeratorCom();
            IMMDevice device=null;
            try
            {
                Marshal.ThrowExceptionForHR(enumerator.GetDevice(deviceId,out device));
                Guid clientId=typeof(IAudioClient).GUID; IntPtr clientPointer;
                Marshal.ThrowExceptionForHR(device.Activate(ref clientId,23,IntPtr.Zero,out clientPointer));
                try { client=(IAudioClient)Marshal.GetObjectForIUnknown(clientPointer); }
                finally { Marshal.Release(clientPointer); }
            }
            finally { AudioCore.Release(device); AudioCore.Release(enumerator); }

            Marshal.ThrowExceptionForHR(client.GetMixFormat(out mixFormat));
            format=(WaveFormatEx)Marshal.PtrToStructure(mixFormat,typeof(WaveFormatEx));
            encoding=format.FormatTag;
            if(format.FormatTag==0xFFFE&&format.ExtraSize>=22)
            {
                var sub=(AudioSubFormat)Marshal.PtrToStructure(IntPtr.Add(mixFormat,24),typeof(AudioSubFormat));
                if(sub.Data2==0&&sub.Data3==0x0010&&sub.Data4!=null&&sub.Data4.Length==8&&sub.Data4[0]==0x80&&sub.Data4[1]==0&&sub.Data4[2]==0&&sub.Data4[3]==0xAA&&sub.Data4[4]==0&&sub.Data4[5]==0x38&&sub.Data4[6]==0x9B&&sub.Data4[7]==0x71) encoding=(ushort)sub.Data1;
            }
            if(format.Channels<1||format.BlockAlign<1||format.SamplesPerSecond<8000)
                throw new InvalidOperationException("O formato de captura deste microfone não é compatível.");
            Guid sessionGuid=Guid.NewGuid();
            readyEvent=new AutoResetEvent(false);
            Marshal.ThrowExceptionForHR(client.Initialize(0,0x40000,0,0,mixFormat,ref sessionGuid));
            Guid captureId=typeof(IAudioCaptureClient).GUID;
            Marshal.ThrowExceptionForHR(client.GetService(ref captureId,out capturePointer));
            capture=(IAudioCaptureClient)Marshal.GetObjectForIUnknown(capturePointer);
            Marshal.ThrowExceptionForHR(client.SetEventHandle(readyEvent.SafeWaitHandle.DangerousGetHandle()));
            Marshal.ThrowExceptionForHR(client.Start()); started=true;
        }
        public float ReadAvailablePeak()
        {
            if(readyEvent==null||!readyEvent.WaitOne(0))return 0;
            float peak=0; uint packetFrames;
            Marshal.ThrowExceptionForHR(capture.GetNextPacketSize(out packetFrames));
            while(packetFrames>0)
            {
                IntPtr data; uint frames,flags; ulong devicePosition,qpcPosition;
                int result=capture.GetBuffer(out data,out frames,out flags,out devicePosition,out qpcPosition);
                if(result<0)Marshal.ThrowExceptionForHR(result);
                try { if((flags&2)==0&&data!=IntPtr.Zero)peak=Math.Max(peak,ReadPeak(data,frames,format,encoding)); }
                finally { if(frames>0)Marshal.ThrowExceptionForHR(capture.ReleaseBuffer(frames)); }
                Marshal.ThrowExceptionForHR(capture.GetNextPacketSize(out packetFrames));
            }
            return Math.Max(0,Math.Min(1,peak));
        }
        internal static string DescribeCaptureError(Exception error)
        {
            if(error==null)return "Falha ao acessar o microfone.";
            int code=error.HResult;
            if(code==unchecked((int)0x80004001))return "Driver sem suporte à leitura de nível";
            if(code==unchecked((int)0x88890004))return "Dispositivo desconectado ou reconfigurado";
            if(code==unchecked((int)0x88890008))return "Formato de captura não suportado pelo dispositivo";
            if(code==unchecked((int)0x80070005))return "Acesso ao microfone bloqueado pelo Windows";
            return "Falha ao acessar o microfone (0x"+code.ToString("X8")+")";
        }
        internal static string DescribeCaptureHelp(Exception error)
        {
            if(error==null)return "Confira se o microfone está conectado e disponível no Windows.";
            int code=error.HResult;
            if(code==unchecked((int)0x80070005))return "O Windows bloqueou o acesso. Abra Configurações > Privacidade e segurança > Microfone e ative Acesso ao microfone e Permitir que aplicativos da área de trabalho acessem o microfone. Depois, feche e abra o WolfHub Audio.";
            if(code==unchecked((int)0x80004001))return "Este driver não implementa a captura compartilhada necessária para o medidor. Confira se há um driver atualizado do fabricante ou teste outro endpoint de microfone.";
            if(code==unchecked((int)0x88890004))return "O endpoint foi removido ou reiniciado durante a captura. Reconecte o dispositivo e atualize a lista de dispositivos.";
            if(code==unchecked((int)0x88890008))return "O formato de captura solicitado não é aceito pelo endpoint. Confira o formato padrão nas propriedades de Som do Windows.";
            return DescribeCaptureError(error)+". Confira se o microfone está conectado, habilitado e disponível nas configurações de Som do Windows.";
        }
        internal static bool IsPermissionDenied(Exception error) { return error!=null&&error.HResult==unchecked((int)0x80070005); }
        public void Dispose()
        {
            if(started&&client!=null)try { client.Stop(); } catch { }
            started=false;
            if(capturePointer!=IntPtr.Zero) { Marshal.Release(capturePointer); capturePointer=IntPtr.Zero; }
            if(mixFormat!=IntPtr.Zero) { Marshal.FreeCoTaskMem(mixFormat); mixFormat=IntPtr.Zero; }
            if(readyEvent!=null) { readyEvent.Dispose(); readyEvent=null; }
            AudioCore.Release(capture); capture=null;
            AudioCore.Release(client); client=null;
        }
        internal static float ReadPeak(IntPtr data,uint frames,WaveFormatEx format,ushort encoding)
        {
            int bytesPerSample=format.BitsPerSample/8;
            if(bytesPerSample<1||format.BlockAlign<bytesPerSample*format.Channels)return 0;
            double peak=0;
            for(uint frame=0;frame<frames;frame++)
            for(int channel=0;channel<format.Channels;channel++)
            {
                int offset=(int)(frame*format.BlockAlign)+channel*bytesPerSample;
                double sample=0;
                if(encoding==3&&format.BitsPerSample==32) sample=new SampleBits { Int32=Marshal.ReadInt32(data,offset) }.Single;
                else if(encoding==3&&format.BitsPerSample==64) sample=new SampleBits { Int64=Marshal.ReadInt64(data,offset) }.Double;
                else if(encoding==1&&format.BitsPerSample==8) sample=(Marshal.ReadByte(data,offset)-128)/128.0;
                else if(encoding==1&&format.BitsPerSample==16) sample=Marshal.ReadInt16(data,offset)/32768.0;
                else if(encoding==1&&format.BitsPerSample==24)
                {
                    int value=Marshal.ReadByte(data,offset)|(Marshal.ReadByte(data,offset+1)<<8)|(Marshal.ReadByte(data,offset+2)<<16);
                    if((value&0x800000)!=0)value|=unchecked((int)0xFF000000);
                    sample=value/8388608.0;
                }
                else if(encoding==1&&format.BitsPerSample==32) sample=Marshal.ReadInt32(data,offset)/2147483648.0;
                else throw new InvalidOperationException("A codificação de captura do microfone não é suportada.");
                if(!Double.IsNaN(sample)&&!Double.IsInfinity(sample))peak=Math.Max(peak,Math.Abs(sample));
            }
            return (float)Math.Min(1,peak);
        }
    }

    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("C8ADBD64-E71E-48a0-A4DE-185C395CD317")]
    internal interface IAudioCaptureClient
    {
        [PreserveSig] int GetBuffer(out IntPtr data,out uint frames,out uint flags,out ulong devicePosition,out ulong qpcPosition);
        [PreserveSig] int ReleaseBuffer(uint frames);
        [PreserveSig] int GetNextPacketSize(out uint frames);
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct WaveFormatEx
    {
        public ushort FormatTag, Channels;
        public uint SamplesPerSecond, AverageBytesPerSecond;
        public ushort BlockAlign, BitsPerSample, ExtraSize;
    }
    [StructLayout(LayoutKind.Sequential)]
    internal struct AudioSubFormat
    {
        public uint Data1;
        public ushort Data2, Data3;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst=8)] public byte[] Data4;
    }
    [StructLayout(LayoutKind.Explicit)]
    internal struct SampleBits
    {
        [FieldOffset(0)] public int Int32;
        [FieldOffset(0)] public float Single;
        [FieldOffset(0)] public long Int64;
        [FieldOffset(0)] public double Double;
    }
    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("1CB9AD4C-DBFA-4c32-B178-C2F568A703B2")]
    internal interface IAudioClient
    {
        [PreserveSig] int Initialize(int shareMode, uint streamFlags, long bufferDuration, long periodicity, IntPtr format, ref Guid sessionGuid);
        [PreserveSig] int GetBufferSize(out uint frames);
        [PreserveSig] int GetStreamLatency(out long latency);
        [PreserveSig] int GetCurrentPadding(out uint padding);
        [PreserveSig] int IsFormatSupported(int shareMode, IntPtr format, out IntPtr closestMatch);
        [PreserveSig] int GetMixFormat(out IntPtr format);
        [PreserveSig] int GetDevicePeriod(out long defaultPeriod, out long minimumPeriod);
        [PreserveSig] int Start();
        [PreserveSig] int Stop();
        [PreserveSig] int Reset();
        [PreserveSig] int SetEventHandle(IntPtr eventHandle);
        [PreserveSig] int GetService(ref Guid iid, out IntPtr service);
    }
    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("F294ACFC-3146-4483-A7BF-ADDCA7C260E2")]
    internal interface IAudioRenderClient
    {
        [PreserveSig] int GetBuffer(uint frames, out IntPtr data);
        [PreserveSig] int ReleaseBuffer(uint frames, uint flags);
    }

    [Serializable]
    public sealed class Preferences
    {
        // Kept for migration of older single-output preference files.
        public string PreferredOutputId = "";
        public string PreferredMicId = "";
        public bool AutoStart;
        public List<DeviceAlias> Aliases = new List<DeviceAlias>();
        public List<string> HiddenEndpointIds = new List<string>();
        public List<AudioProfile> Profiles = new List<AudioProfile>();
        public string ActiveProfileId = "";
        // The profile selected in the window can differ from the one last applied to Windows.
        public string AppliedProfileId = "";
        public bool AppliedProfileStateInitialized;
        public AudioProfile ActiveProfile()
        {
            if (Profiles == null) Profiles = new List<AudioProfile>();
            if (Profiles.Count == 0) Profiles.Add(new AudioProfile { Id=Guid.NewGuid().ToString("N"), Name="Default", PrimaryOutputId=PreferredOutputId, RecordingDeviceId=PreferredMicId });
            var profile=Profiles.FirstOrDefault(x=>x.Id==ActiveProfileId);
            if(profile==null) { profile=Profiles[0]; ActiveProfileId=profile.Id; }
            return profile;
        }
        public void Normalize()
        {
            if(Aliases==null) Aliases=new List<DeviceAlias>();
            if(HiddenEndpointIds==null) HiddenEndpointIds=new List<string>();
            if(Profiles==null) Profiles=new List<AudioProfile>();
            ActiveProfile();
            if(!AppliedProfileStateInitialized) { AppliedProfileId=ActiveProfileId; AppliedProfileStateInitialized=true; }
            else if(!String.IsNullOrEmpty(AppliedProfileId)&&!Profiles.Any(x=>x.Id==AppliedProfileId)) AppliedProfileId="";
        }
        public string GetAlias(string id) { if(Aliases==null) Aliases=new List<DeviceAlias>(); var a = Aliases.FirstOrDefault(x => x.Id == id); return a == null ? null : a.Name; }
        public bool IsEndpointHidden(string id) { if(HiddenEndpointIds==null)HiddenEndpointIds=new List<string>(); return HiddenEndpointIds.Contains(id); }
        public void SetEndpointHidden(string id,bool hidden)
        {
            if(HiddenEndpointIds==null)HiddenEndpointIds=new List<string>();
            HiddenEndpointIds.RemoveAll(x=>x==id);
            if(hidden&&!String.IsNullOrEmpty(id))HiddenEndpointIds.Add(id);
        }
        public void SetAlias(string id, string name)
        {
            if(Aliases==null) Aliases=new List<DeviceAlias>();
            Aliases.RemoveAll(x => x.Id == id);
            if (!String.IsNullOrWhiteSpace(name)) Aliases.Add(new DeviceAlias { Id = id, Name = name.Trim() });
        }
    }
    [Serializable] public sealed class DeviceAlias { public string Id = ""; public string Name = ""; }
    [Serializable] public sealed class AudioProfile
    {
        public string Id = Guid.NewGuid().ToString("N");
        public string Name = "New profile";
        public string PrimaryOutputId = "";
        public string SecondaryOutputId = "";
        public string RecordingDeviceId = "";
    }
    internal sealed class AudioChoice
    {
        public string Id, Label;
        public override string ToString() { return Label; }
    }

    internal sealed class RoundedButton : Button
    {
        public bool Primary;
        public ButtonGlyph Glyph;
        private bool hover, pressed;
        public RoundedButton() { FlatStyle=FlatStyle.Flat; FlatAppearance.BorderSize=0; Cursor=Cursors.Hand; TextAlign=ContentAlignment.MiddleCenter; }
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            var bounds=new Rectangle(0,0,Width-1,Height-1); int radius=8;
            Color fill=Primary?Color.FromArgb(57,197,214):Color.FromArgb(28,45,61);
            if(!Enabled) fill=Color.FromArgb(25,37,51); else if(pressed) fill=Primary?Color.FromArgb(36,158,175):Color.FromArgb(40,61,77); else if(hover) fill=Primary?Color.FromArgb(85,216,230):Color.FromArgb(36,55,72);
            using(var path=new System.Drawing.Drawing2D.GraphicsPath())
            using(var brush=new SolidBrush(fill))
            using(var pen=new Pen(Primary?Color.FromArgb(57,197,214):Color.FromArgb(41,61,76)))
            {
                path.AddArc(bounds.X,bounds.Y,radius,radius,180,90); path.AddArc(bounds.Right-radius,bounds.Y,radius,radius,270,90);
                path.AddArc(bounds.Right-radius,bounds.Bottom-radius,radius,radius,0,90); path.AddArc(bounds.X,bounds.Bottom-radius,radius,radius,90,90); path.CloseFigure();
                e.Graphics.FillPath(brush,path); if(!Primary)e.Graphics.DrawPath(pen,path);
            }
            Color textColor=Primary?Color.FromArgb(11,17,27):Color.FromArgb(237,248,250); if(!Enabled)textColor=Color.FromArgb(120,142,158);
            var textBounds=bounds;
            if(Glyph!=ButtonGlyph.None) { Color glyphColor=!Enabled?Color.FromArgb(120,142,158):(Primary?Color.FromArgb(11,17,27):Color.FromArgb(164,185,198)); int scale=Math.Max(1,(int)Math.Round(e.Graphics.DpiX/96F)); int size=Math.Min(18*scale,Math.Max(12,bounds.Height-10)); int inset=12*scale; IconResources.Draw(e.Graphics,IconResources.Name(Glyph),new Rectangle(bounds.X+inset,bounds.Y+(bounds.Height-size)/2,size,size),glyphColor); textBounds.X+=inset+size+2*scale; textBounds.Width-=inset+size+10*scale; }
            TextRenderer.DrawText(e.Graphics,Text,Font,textBounds,Glyph==ButtonGlyph.None?textColor:textColor,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);
            if(Focused) ControlPaint.DrawFocusRectangle(e.Graphics,Rectangle.Inflate(bounds,-4,-4));
        }
        protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); hover=true; Invalidate(); }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); hover=false; pressed=false; Invalidate(); }
        protected override void OnMouseDown(MouseEventArgs e) { base.OnMouseDown(e); pressed=true; Invalidate(); }
        protected override void OnMouseUp(MouseEventArgs e) { base.OnMouseUp(e); pressed=false; Invalidate(); }
        protected override void OnEnabledChanged(EventArgs e) { base.OnEnabledChanged(e); Invalidate(); }
    }

    internal enum ButtonGlyph { None, Play, Pencil, More, Plus, Settings2, Eye, EyeOff, ArrowLeftRight, Save, ChevronDown, ShieldCheck, RefreshCw }
    internal static class IconResources
    {
        private static readonly Dictionary<string,Bitmap> cache=new Dictionary<string,Bitmap>(StringComparer.OrdinalIgnoreCase);
        private static readonly string[] names={"volume-2","speaker","mic","play","pencil","ellipsis","plus","settings-2","eye","eye-off","arrow-left-right","save","chevron-down","shield-check","refresh-cw"};
        private static readonly object sync=new object();
        public static string Name(ButtonGlyph glyph)
        {
            switch(glyph)
            {
                case ButtonGlyph.Play:return "play"; case ButtonGlyph.Pencil:return "pencil"; case ButtonGlyph.More:return "ellipsis";
                case ButtonGlyph.Plus:return "plus"; case ButtonGlyph.Settings2:return "settings-2"; case ButtonGlyph.Eye:return "eye";
                case ButtonGlyph.EyeOff:return "eye-off"; case ButtonGlyph.ArrowLeftRight:return "arrow-left-right"; case ButtonGlyph.Save:return "save";
                case ButtonGlyph.ChevronDown:return "chevron-down"; case ButtonGlyph.ShieldCheck:return "shield-check"; case ButtonGlyph.RefreshCw:return "refresh-cw";
                default:throw new ArgumentOutOfRangeException("glyph");
            }
        }
        public static void Draw(Graphics graphics,string name,Rectangle bounds,Color color)
        {
            if(bounds.Width<=0||bounds.Height<=0)return;
            Bitmap image=Get(name);
            var matrix=new System.Drawing.Imaging.ColorMatrix(new float[][] {
                new float[]{0,0,0,0,0}, new float[]{0,0,0,0,0}, new float[]{0,0,0,0,0},
                new float[]{0,0,0,1,0}, new float[]{color.R/255F,color.G/255F,color.B/255F,0,1}
            });
            using(var attributes=new System.Drawing.Imaging.ImageAttributes())
            {
                attributes.SetColorMatrix(matrix);
                var oldInterpolation=graphics.InterpolationMode; var oldPixelOffset=graphics.PixelOffsetMode;
                graphics.InterpolationMode=System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic; graphics.PixelOffsetMode=System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                graphics.DrawImage(image,bounds,0,0,image.Width,image.Height,GraphicsUnit.Pixel,attributes);
                graphics.InterpolationMode=oldInterpolation; graphics.PixelOffsetMode=oldPixelOffset;
            }
        }
        private static Bitmap Get(string name)
        {
            lock(sync)
            {
                Bitmap result; if(cache.TryGetValue(name,out result))return result;
                string resource="WolfHubAudio.Icons."+name+".png";
                using(Stream stream=System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream(resource))
                {
                    if(stream==null)throw new InvalidOperationException("Missing embedded Lucide icon: "+name);
                    using(var source=Image.FromStream(stream))result=new Bitmap(source);
                }
                cache.Add(name,result); return result;
            }
        }
        public static void ValidateResources()
        {
            foreach(string name in names)
            {
                Bitmap image=Get(name);
                if(image.Width!=96||image.Height!=96||image.PixelFormat!=System.Drawing.Imaging.PixelFormat.Format32bppArgb)throw new InvalidOperationException("Invalid Lucide resource: "+name);
                bool visible=false;
                for(int y=0;y<image.Height&&!visible;y+=3)for(int x=0;x<image.Width;x+=3)if(image.GetPixel(x,y).A>0){visible=true;break;}
                if(!visible)throw new InvalidOperationException("Empty Lucide resource: "+name);
            }
            using(var tinted=new Bitmap(48,48,System.Drawing.Imaging.PixelFormat.Format32bppArgb))
            using(var graphics=Graphics.FromImage(tinted))
            {
                graphics.Clear(Color.Transparent); Draw(graphics,"volume-2",new Rectangle(4,4,40,40),Color.FromArgb(57,197,214));
                bool colored=false; for(int y=0;y<tinted.Height&&!colored;y++)for(int x=0;x<tinted.Width;x++){Color c=tinted.GetPixel(x,y);if(c.A>0&&c.R==57&&c.G==197&&c.B==214){colored=true;break;}}
                if(!colored)throw new InvalidOperationException("Lucide icon tinting failed.");
            }
        }
        public static Bitmap CreateImage(string name,int size,Color color)
        {
            var image=new Bitmap(size,size,System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using(var g=Graphics.FromImage(image)){g.Clear(Color.Transparent);Draw(g,name,new Rectangle(0,0,size,size),color);}
            return image;
        }
    }
    internal sealed class LucideIconControl : Control
    {
        public string IconName;
        public Color IconColor=Color.FromArgb(57,197,214);
        public int ContentSize;
        public LucideIconControl() { SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.SupportsTransparentBackColor,true); BackColor=Color.Transparent; }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e); if(String.IsNullOrEmpty(IconName))return;
            int logical=ContentSize>0?ContentSize:Math.Min(ClientSize.Width,ClientSize.Height);
            int size=Math.Max(1,Math.Min((int)Math.Round(logical*e.Graphics.DpiX/96F),Math.Min(ClientSize.Width,ClientSize.Height)));
            var bounds=new Rectangle((ClientSize.Width-size)/2,(ClientSize.Height-size)/2,size,size);
            IconResources.Draw(e.Graphics,IconName,bounds,Enabled?IconColor:Color.FromArgb(120,142,158));
        }
    }
    internal sealed class ProfileEntryButton : Button
    {
        public bool Selected, Applied;
        private bool hover;
        public ProfileEntryButton() { FlatStyle=FlatStyle.Flat; FlatAppearance.BorderSize=0; Cursor=Cursors.Hand; TextAlign=ContentAlignment.MiddleLeft; }
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias; var r=new Rectangle(0,0,Width-1,Height-1);
            Color background=Selected?Color.FromArgb(37,71,83):(hover?Color.FromArgb(28,45,61):Color.FromArgb(16,26,39));
            using(var path=new System.Drawing.Drawing2D.GraphicsPath()) using(var fill=new SolidBrush(background)) using(var border=new Pen(Selected?Color.FromArgb(57,197,214):background))
            {
                int rad=8; path.AddArc(r.Left,r.Top,rad,rad,180,90); path.AddArc(r.Right-rad,r.Top,rad,rad,270,90); path.AddArc(r.Right-rad,r.Bottom-rad,rad,rad,0,90); path.AddArc(r.Left,r.Bottom-rad,rad,rad,90,90); path.CloseFigure();
                e.Graphics.FillPath(fill,path); e.Graphics.DrawPath(border,path);
            }
            if(Selected) using(var b=new SolidBrush(Color.FromArgb(57,197,214))) e.Graphics.FillRectangle(b,5,8,3,Height-16);
            TextRenderer.DrawText(e.Graphics,Text,Font,new Rectangle(18,0,Width-48,Height),Color.FromArgb(237,248,250),TextFormatFlags.Left|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);
            if(Applied) { using(var b=new SolidBrush(Color.FromArgb(84,214,160))) e.Graphics.FillEllipse(b,Width-24,(Height-8)/2,8,8); }
            if(Focused) ControlPaint.DrawFocusRectangle(e.Graphics,Rectangle.Inflate(r,-4,-4));
        }
        protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); hover=true; Invalidate(); }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); hover=false; Invalidate(); }
    }
    internal sealed class VisibilityToggle : Control
    {
        private bool isChecked;
        public bool Checked { get { return isChecked; } set { if(isChecked!=value) { isChecked=value; Invalidate(); AccessibleDescription=isChecked?"Dispositivo visível nas listas do Windows":"Dispositivo oculto nas listas do Windows"; } } }
        public event EventHandler Toggled;
        public VisibilityToggle()
        {
            SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.Selectable,true);
            TabStop=true; Cursor=Cursors.Hand; AccessibleRole=AccessibleRole.CheckButton; AccessibleName="Visibilidade do dispositivo";
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e); e.Graphics.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            int h=Math.Min(24,Height-4),w=Math.Min(48,Width-4),x=(Width-w)/2,y=(Height-h)/2;
            Color track=!Enabled?Color.FromArgb(35,47,59):(Checked?Color.FromArgb(57,197,214):Color.FromArgb(54,69,82));
            using(var brush=new SolidBrush(track)) e.Graphics.FillRoundedRectangle(brush,new Rectangle(x,y,w,h),h/2);
            int dot=h-6,dx=Checked?x+w-dot-3:x+3;
            using(var brush=new SolidBrush(Checked?Color.FromArgb(11,17,27):Color.FromArgb(237,248,250)))e.Graphics.FillEllipse(brush,dx,y+3,dot,dot);
            if(Focused)ControlPaint.DrawFocusRectangle(e.Graphics,Rectangle.Inflate(ClientRectangle,-1,-1));
        }
        protected override void OnClick(EventArgs e)
        {
            if(!Enabled)return;
            Focus(); Checked=!Checked; base.OnClick(e); if(Toggled!=null)Toggled(this,EventArgs.Empty);
        }
        protected override void OnKeyDown(KeyEventArgs e)
        {
            if(e.KeyCode==Keys.Space) { e.Handled=true; OnClick(EventArgs.Empty); }
            else base.OnKeyDown(e);
        }
    }
    internal static class AudioDeviceUiState
    {
        public static string Visibility(AudioDevice device) { return device.IsHidden?"Oculto":"Visível"; }
        public static bool CanTest(AudioDevice device) { return device!=null&&device.Flow==Flow.Render&&device.IsActive&&!device.IsDisabled; }
        public static string Summary(AudioDevice device)
        {
            var items=new List<string>();
            items.Add(device.Flow==Flow.Render?"Output":"Recording"); items.Add(Visibility(device));
            items.Add(device.IsActive?"Ativo no Windows":(device.IsDisabled?"Desativado":"Indisponível"));
            if(device.IsDefault)items.Add("Padrão atual");
            if(device.IsPreferred)items.Add("Usado no perfil atual");
            if(!device.IsDefault&&!device.IsPreferred)items.Add("Não está em uso");
            return String.Join(" · ",items.ToArray());
        }
    }
    internal static class RecordingMeterPresentation
    {
        private const float SignalThreshold=0.005F;
        private const float RepaintThreshold=0.015F;
        public static string GetLabel(float peak,bool disabled,bool permissionDenied,string error)
        {
            if(disabled)return permissionDenied?"Medidor desativado · permissão necessária":"Nível indisponível";
            if(Single.IsNaN(peak)||Single.IsInfinity(peak)||peak<SignalThreshold)return "Sem sinal";
            float normalized=Math.Max(0F,Math.Min(1F,peak));
            return "Nível de entrada · "+Math.Round(normalized*100F)+"%";
        }
        public static bool ShouldRedraw(float previousPeak,float currentPeak,bool wasDisabled,bool isDisabled)
        {
            if(wasDisabled!=isDisabled)return true;
            bool hadSignal=!Single.IsNaN(previousPeak)&&previousPeak>=SignalThreshold;
            bool hasSignal=!Single.IsNaN(currentPeak)&&currentPeak>=SignalThreshold;
            return hadSignal!=hasSignal||Math.Abs(currentPeak-previousPeak)>=RepaintThreshold;
        }
    }
    internal sealed class DeviceVisibilityEntry : RoundedSurfacePanel
    {
        public readonly AudioDevice Device;
        public readonly VisibilityToggle Toggle=new VisibilityToggle();
        public event Action<AudioDevice,bool> VisibilityRequested;
        public event Action<AudioDevice> RenameRequested;
        public event Action<AudioDevice> TestRequested;
        private readonly Preferences preferences;
        private readonly ToolTip tips=new ToolTip();
        private readonly Label nameLabel,summaryLabel;
        private readonly RoundedButton renameButton,testButton;
        private readonly LucideIconControl visibilityIcon;
        public DeviceVisibilityEntry(AudioDevice device,Preferences preferences)
        {
            Device=device; this.preferences=preferences; Height=78; Margin=new Padding(0,0,0,8); BackColor=Color.FromArgb(21,34,49); BorderColor=Color.FromArgb(41,61,76); Radius=10;
            var icon=new LucideIconControl { IconName=device.Flow==Flow.Render?"volume-2":"mic",IconColor=Color.FromArgb(57,197,214),ContentSize=24 };
            nameLabel=new Label { AutoEllipsis=true,Font=new Font("Segoe UI",10F,FontStyle.Bold),ForeColor=Color.FromArgb(237,248,250),BackColor=BackColor };
            summaryLabel=new Label { AutoEllipsis=true,Font=new Font("Segoe UI",8.75F),ForeColor=Color.FromArgb(164,185,198),BackColor=BackColor };
            var fullName=preferences.GetAlias(device.Id);
            nameLabel.Text=String.IsNullOrWhiteSpace(fullName)?device.Name:fullName+"  ·  "+device.Name;
            summaryLabel.Text=AudioDeviceUiState.Summary(device);
            renameButton=new RoundedButton { Text="Apelido",Width=96,Height=34,Glyph=ButtonGlyph.Pencil,Font=new Font("Segoe UI",9F),ForeColor=Color.FromArgb(237,248,250),BackColor=Color.FromArgb(28,45,61) };
            renameButton.Click+=delegate { if(RenameRequested!=null)RenameRequested(Device); };
            if(device.Flow==Flow.Render)
            {
                testButton=new RoundedButton { Text="Testar",Width=82,Height=34,Glyph=ButtonGlyph.Play,Font=new Font("Segoe UI",9F),ForeColor=Color.FromArgb(237,248,250),BackColor=Color.FromArgb(28,45,61),Enabled=AudioDeviceUiState.CanTest(device) };
                testButton.Click+=delegate { if(TestRequested!=null)TestRequested(Device); };
                tips.SetToolTip(testButton,testButton.Enabled?"Reproduzir tom neste endpoint (pede confirmação).":"Teste disponível apenas para saídas ativas.");
                Controls.Add(testButton);
            }
            else
            {
                testButton=null;
                var unavailable=new Label { Text="Sem teste de gravação",AutoSize=false,TextAlign=ContentAlignment.MiddleRight,Font=new Font("Segoe UI",8.5F),ForeColor=Color.FromArgb(120,142,158),BackColor=BackColor };
                Controls.Add(unavailable); tips.SetToolTip(unavailable,"O WolfHub não faz teste de gravação neste painel.");
            }
            Toggle.Checked=!device.IsHidden;
            visibilityIcon=new LucideIconControl { IconName=Toggle.Checked?"eye":"eye-off",IconColor=Color.FromArgb(164,185,198),ContentSize=16 };
            Toggle.Toggled+=delegate { visibilityIcon.IconName=Toggle.Checked?"eye":"eye-off"; visibilityIcon.Invalidate(); tips.SetToolTip(visibilityIcon,Toggle.Checked?"Este dispositivo está visível; ocultar dispositivo":"Este dispositivo está oculto; mostrar dispositivo"); if(VisibilityRequested!=null)VisibilityRequested(Device,Toggle.Checked); };
            Controls.AddRange(new Control[]{icon,nameLabel,summaryLabel,renameButton,Toggle,visibilityIcon});
            tips.SetToolTip(icon,nameLabel.Text); tips.SetToolTip(nameLabel,device.Name); tips.SetToolTip(summaryLabel,summaryLabel.Text); tips.SetToolTip(Toggle,"Alternar a visibilidade global deste endpoint no Windows.");
            tips.SetToolTip(visibilityIcon,Toggle.Checked?"Este dispositivo está visível; ocultar dispositivo":"Este dispositivo está oculto; mostrar dispositivo");
            Resize+=delegate { LayoutEntry(icon); }; LayoutEntry(icon);
        }
        protected override void Dispose(bool disposing) { if(disposing)tips.Dispose(); base.Dispose(disposing); }
        private void LayoutEntry(Control icon)
        {
            int pad=12,right=Math.Max(pad,ClientSize.Width-pad);
            icon.SetBounds(pad,15,46,46);
            int toggleLeft=right-50; Toggle.SetBounds(toggleLeft,25,50,28); visibilityIcon.SetBounds(toggleLeft-26,29,18,18);
            renameButton.SetBounds(toggleLeft-8-renameButton.Width,22,renameButton.Width,34);
            if(testButton!=null)testButton.SetBounds(renameButton.Left-8-testButton.Width,22,testButton.Width,34);
            else if(Controls.Count>0)
            {
                var unavailable=Controls.OfType<Label>().FirstOrDefault(x=>x.Text=="Sem teste de gravação");
                if(unavailable!=null)unavailable.SetBounds(renameButton.Left-8-145,22,145,34);
            }
            int textWidth=Math.Max(80,(testButton==null?renameButton.Left-12:testButton.Left-12)-(pad+58));
            nameLabel.SetBounds(pad+58,14,textWidth,24); summaryLabel.SetBounds(pad+58,40,textWidth,22);
        }
    }
    internal sealed class DeviceVisibilityDialog : Form
    {
        private readonly Func<List<AudioDevice>> enumerate;
        private readonly Func<AudioDevice,bool,bool> setVisibility;
        private readonly Action<AudioDevice> renameDevice,testDevice;
        private readonly Preferences preferences;
        private readonly FlowLayoutPanel entries=new FlowLayoutPanel();
        private readonly Label countLabel=new Label();
        public DeviceVisibilityDialog(Func<List<AudioDevice>> enumerate,Func<AudioDevice,bool,bool> setVisibility,Action<AudioDevice> renameDevice,Action<AudioDevice> testDevice,Preferences preferences)
        {
            this.enumerate=enumerate; this.setVisibility=setVisibility; this.renameDevice=renameDevice; this.testDevice=testDevice; this.preferences=preferences;
            Text="Gerenciar visibilidade dos dispositivos"; StartPosition=FormStartPosition.CenterParent; Width=940; Height=640; MinimumSize=new Size(760,520); BackColor=Color.FromArgb(11,17,27); ForeColor=Color.FromArgb(237,248,250); Font=new Font("Segoe UI",9.5F);
            var title=new Label { Text="Gerenciar dispositivos",Left=18,Top=14,Width=620,Height=30,Font=new Font("Segoe UI",18F,FontStyle.Bold),ForeColor=Color.FromArgb(237,248,250) };
            var description=new Label { Text="Visibilidade global, apelidos e identificação de saídas de áudio.",Left=18,Top=47,Width=720,Height=24,ForeColor=Color.FromArgb(164,185,198) };
            countLabel.ForeColor=Color.FromArgb(164,185,198); countLabel.TextAlign=ContentAlignment.MiddleLeft;
            entries.FlowDirection=FlowDirection.TopDown; entries.WrapContents=false; entries.AutoScroll=true; entries.BackColor=BackColor; entries.Padding=new Padding(0,2,4,2);
            var note=new Label { Text="Visibilidade registrada pelo WolfHub; mudanças feitas por outras ferramentas podem não aparecer. Gravação não possui teste neste painel.",Left=18,Height=40,ForeColor=Color.FromArgb(120,142,158),TextAlign=ContentAlignment.MiddleLeft,AutoEllipsis=true };
            var close=new RoundedButton { Text="Fechar",Width=104,Height=36,Font=new Font("Segoe UI",9F),ForeColor=Color.FromArgb(237,248,250),BackColor=Color.FromArgb(28,45,61) }; close.Click+=delegate { Close(); };
            var refresh=new RoundedButton { Text="Atualizar dispositivos",Width=184,Height=34,Glyph=ButtonGlyph.RefreshCw,Font=new Font("Segoe UI",9F),ForeColor=Color.FromArgb(237,248,250),BackColor=Color.FromArgb(28,45,61) }; refresh.Click+=delegate { ReloadEntries(); };
            Controls.AddRange(new Control[]{title,description,countLabel,refresh,entries,note,close});
            Resize+=delegate
            {
                int w=Math.Max(320,ClientSize.Width-36); title.Width=w; description.Width=w; countLabel.SetBounds(18,74,Math.Max(120,w-refresh.Width-12),22); refresh.SetBounds(ClientSize.Width-18-refresh.Width,68,refresh.Width,34);
                entries.SetBounds(18,100,w,Math.Max(120,ClientSize.Height-166)); note.SetBounds(18,ClientSize.Height-58,Math.Max(180,w-close.Width-18),40); close.SetBounds(ClientSize.Width-122,ClientSize.Height-54,104,36);
                ResizeRows();
            };
            Load+=delegate { ReloadEntries(); };
        }
        private void ReloadEntries()
        {
            entries.SuspendLayout(); entries.Controls.Clear();
            List<AudioDevice> devices;
            try { devices=enumerate(); } catch(Exception ex) { countLabel.Text="Não foi possível consultar dispositivos: "+ex.Message; entries.ResumeLayout(); return; }
            countLabel.Text=devices.Count+" dispositivos detectados · Output e Recording";
            foreach(var device in devices.OrderBy(x=>x.Flow).ThenBy(x=>x.Name,StringComparer.CurrentCultureIgnoreCase))
            {
                var row=new DeviceVisibilityEntry(device,preferences); row.Width=Math.Max(300,entries.ClientSize.Width-SystemInformation.VerticalScrollBarWidth-8);
                row.VisibilityRequested+=delegate(AudioDevice d,bool visible) { setVisibility(d,visible); ReloadEntries(); };
                row.RenameRequested+=delegate(AudioDevice d) { renameDevice(d); ReloadEntries(); };
                row.TestRequested+=delegate(AudioDevice d) { testDevice(d); };
                entries.Controls.Add(row);
            }
            entries.ResumeLayout(); ResizeRows();
        }
        private void ResizeRows()
        {
            int width=Math.Max(300,entries.ClientSize.Width-SystemInformation.VerticalScrollBarWidth-8);
            foreach(Control row in entries.Controls)row.Width=width;
        }
    }
    internal sealed class AudioStatusIndicator : Control
    {
        public string CurrentText="";
        public string FullText="";
        public bool HasDefault;
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e); using(var dot=new SolidBrush(HasDefault?Color.FromArgb(84,214,160):Color.FromArgb(120,142,158))) e.Graphics.FillEllipse(dot,1,(Height-8)/2,8,8);
            TextRenderer.DrawText(e.Graphics,CurrentText,Font,new Rectangle(15,0,Math.Max(0,Width-15),Height),Color.FromArgb(164,185,198),TextFormatFlags.Left|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);
        }
    }
    internal static class GraphicsExtensions
    {
        public static void FillRoundedRectangle(this Graphics g,Brush brush,Rectangle r,int radius)
        { using(var p=new System.Drawing.Drawing2D.GraphicsPath()) { p.AddArc(r.Left,r.Top,radius,radius,180,90); p.AddArc(r.Right-radius,r.Top,radius,radius,270,90); p.AddArc(r.Right-radius,r.Bottom-radius,radius,radius,0,90); p.AddArc(r.Left,r.Bottom-radius,radius,radius,90,90); p.CloseFigure(); g.FillPath(brush,p); } }
    }
    internal class RoundedSurfacePanel : Panel
    {
        public Color BorderColor=Color.FromArgb(41,61,76);
        public int Radius=12;
        public RoundedSurfacePanel() { SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer,true); }
        protected override void OnPaintBackground(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using(var fill=new SolidBrush(BackColor)) e.Graphics.FillRoundedRectangle(fill,new Rectangle(0,0,Width-1,Height-1),Radius);
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e); e.Graphics.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using(var path=new System.Drawing.Drawing2D.GraphicsPath()) using(var pen=new Pen(BorderColor))
            { int r=Radius; path.AddArc(0,0,r,r,180,90); path.AddArc(Width-r-1,0,r,r,270,90); path.AddArc(Width-r-1,Height-r-1,r,r,0,90); path.AddArc(0,Height-r-1,r,r,90,90); path.CloseFigure(); e.Graphics.DrawPath(pen,path); }
        }
    }
    internal sealed class ThemedComboHost : Panel
    {
        private bool focused,hover;
        private readonly ComboBox combo;
        private readonly ComboDisplayLabel selectedLabel;
        public ThemedComboHost(ComboBox combo)
        {
            this.combo=combo; Height=40; MinimumSize=new Size(80,40); BackColor=Color.FromArgb(21,34,49); Cursor=Cursors.Hand;
            combo.Dock=DockStyle.None; combo.Margin=Padding.Empty; Controls.Add(combo);
            combo.ForeColor=BackColor;
            selectedLabel=new ComboDisplayLabel { AutoEllipsis=true,TextAlign=ContentAlignment.MiddleLeft,BackColor=BackColor,ForeColor=Color.FromArgb(237,248,250),Font=combo.Font,Cursor=Cursors.Hand,Padding=new Padding(8,0,38,0) };
            selectedLabel.Click+=delegate { combo.Focus(); combo.DroppedDown=true; }; Controls.Add(selectedLabel);
            combo.GotFocus+=delegate { focused=true; selectedLabel.HasFocus=true; Invalidate(); }; combo.LostFocus+=delegate { focused=false; selectedLabel.HasFocus=false; Invalidate(); };
            MouseEnter+=delegate { hover=true; selectedLabel.Hover=true; Invalidate(); }; MouseLeave+=delegate { hover=false; selectedLabel.Hover=false; Invalidate(); };
            combo.MouseEnter+=delegate { hover=true; selectedLabel.Hover=true; Invalidate(); }; combo.MouseLeave+=delegate { hover=false; selectedLabel.Hover=false; Invalidate(); };
            var arrow=new LucideIconControl { BackColor=BackColor,Cursor=Cursors.Hand,IconName="chevron-down",IconColor=Color.FromArgb(164,185,198),ContentSize=18,AccessibleName="Abrir lista de dispositivos" };
            arrow.Click+=delegate { combo.Focus(); combo.DroppedDown=!combo.DroppedDown; }; Controls.Add(arrow);
            combo.SelectedIndexChanged+=delegate { UpdateSelectedLabel(); };
            Resize+=delegate
            {
                combo.Bounds=new Rectangle(1,1,Math.Max(80,Width-2),Height-2);
                selectedLabel.Bounds=new Rectangle(1,1,Math.Max(30,Width-2),Height-2);
                arrow.Bounds=new Rectangle(Math.Max(1,Width-33),1,31,Height-2);
                selectedLabel.BringToFront(); arrow.BringToFront();
            };
            UpdateSelectedLabel();
        }
        private void UpdateSelectedLabel()
        {
            var choice=combo.SelectedItem as AudioChoice; selectedLabel.Text=choice==null?"Selecione um dispositivo":choice.Label;
            selectedLabel.ForeColor=choice!=null&&choice.Label.StartsWith("Indisponível")?Color.FromArgb(255,198,109):Color.FromArgb(237,248,250);
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e); e.Graphics.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias; Color c=focused?Color.FromArgb(57,197,214):(hover?Color.FromArgb(85,216,230):Color.FromArgb(41,61,76));
            using(var path=new System.Drawing.Drawing2D.GraphicsPath()) using(var pen=new Pen(c,focused?1.6F:1F)) { int r=9; path.AddArc(0,0,r,r,180,90); path.AddArc(Width-r-1,0,r,r,270,90); path.AddArc(Width-r-1,Height-r-1,r,r,0,90); path.AddArc(0,Height-r-1,r,r,90,90); path.CloseFigure(); e.Graphics.DrawPath(pen,path); }
        }
    }
    internal sealed class ComboDisplayLabel : Label
    {
        public bool HasFocus,Hover;
        protected override void OnPaintBackground(PaintEventArgs e) { using(var b=new SolidBrush(BackColor)) e.Graphics.FillRectangle(b,ClientRectangle); }
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            var textBounds=new Rectangle(Padding.Left,0,Math.Max(0,Width-Padding.Left-Padding.Right),Height);
            TextRenderer.DrawText(e.Graphics,Text,Font,textBounds,ForeColor,TextFormatFlags.Left|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis|TextFormatFlags.SingleLine|TextFormatFlags.NoPrefix);
            Color border=HasFocus?Color.FromArgb(57,197,214):(Hover?Color.FromArgb(85,216,230):Color.FromArgb(41,61,76));
            using(var path=new System.Drawing.Drawing2D.GraphicsPath()) using(var pen=new Pen(border,HasFocus?1.6F:1F))
            { int r=8; path.AddArc(0,0,r,r,180,90); path.AddArc(Width-r-1,0,r,r,270,90); path.AddArc(Width-r-1,Height-r-1,r,r,0,90); path.AddArc(0,Height-r-1,r,r,90,90); path.CloseFigure(); e.Graphics.DrawPath(pen,path); }
        }
    }
    internal sealed class DarkColorTable : ProfessionalColorTable
    {
        public override Color ToolStripDropDownBackground { get { return Color.FromArgb(21,34,49); } }
        public override Color ImageMarginGradientBegin { get { return Color.FromArgb(16,26,39); } }
        public override Color ImageMarginGradientMiddle { get { return Color.FromArgb(16,26,39); } }
        public override Color ImageMarginGradientEnd { get { return Color.FromArgb(16,26,39); } }
        public override Color MenuItemSelected { get { return Color.FromArgb(37,71,83); } }
        public override Color MenuItemBorder { get { return Color.FromArgb(57,197,214); } }
        public override Color MenuBorder { get { return Color.FromArgb(41,61,76); } }
    }

    internal sealed class AudioDevice
    {
        public string Id, Name;
        public Flow Flow;
        public uint State;
        public bool IsDefault;
        public bool IsPreferred;
        public bool IsHidden;
        public bool IsActive { get { return (State & 1) != 0 && (State & 0xE) == 0; } }
        public bool IsDisabled { get { return (State & 2) != 0; } }
        public string TypeName { get { return Flow == Flow.Render ? "Output" : "Microphone"; } }
    }

    [ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
    internal class MMDeviceEnumeratorCom { }

    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("A95664D2-9614-4F35-A746-DE8DB63617E6")]
    internal interface IMMDeviceEnumerator
    {
        [PreserveSig]
        int EnumAudioEndpoints(Flow flow, uint stateMask, out IMMDeviceCollection devices);
        [PreserveSig]
        int GetDefaultAudioEndpoint(Flow flow, Role role, out IMMDevice device);
        [PreserveSig]
        int GetDevice([MarshalAs(UnmanagedType.LPWStr)] string id, out IMMDevice device);
        [PreserveSig]
        int RegisterEndpointNotificationCallback(IntPtr callback);
        [PreserveSig]
        int UnregisterEndpointNotificationCallback(IntPtr callback);
    }
    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("0BD7A1BE-7A1A-44DB-8397-CC5392387B5E")]
    internal interface IMMDeviceCollection
    {
        [PreserveSig]
        int GetCount(out uint count);
        [PreserveSig]
        int Item(uint index, out IMMDevice device);
    }
    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("D666063F-1587-4E43-81F1-B948E807363F")]
    internal interface IMMDevice
    {
        [PreserveSig]
        int Activate(ref Guid iid, uint context, IntPtr activationParams, out IntPtr instance);
        [PreserveSig]
        int OpenPropertyStore(uint access, out IPropertyStore store);
        [PreserveSig]
        int GetId(out IntPtr id);
        [PreserveSig]
        int GetState(out uint state);
    }
    [StructLayout(LayoutKind.Sequential)] internal struct PropertyKey { public Guid FormatId; public uint PropertyId; }
    [StructLayout(LayoutKind.Explicit)] internal struct PropVariant
    {
        [FieldOffset(0)] public ushort VariantType;
        [FieldOffset(8)] public IntPtr Pointer;
    }
    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99")]
    internal interface IPropertyStore
    {
        [PreserveSig]
        int GetCount(out uint count);
        [PreserveSig]
        int GetAt(uint index, out PropertyKey key);
        [PreserveSig]
        int GetValue(ref PropertyKey key, out PropVariant value);
        [PreserveSig]
        int SetValue(ref PropertyKey key, ref PropVariant value);
        [PreserveSig]
        int Commit();
    }

    // Windows' PolicyConfig COM API exposes the system default endpoint and visibility switch.
    [ComImport, Guid("870AF99C-171D-4F9E-AF0D-E63DF40C2BC9")]
    internal class PolicyConfigClientCom { }
    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("F8679F50-850A-41CF-9C72-430F290290C8")]
    internal interface IPolicyConfig
    {
        [PreserveSig] int GetMixFormat(); [PreserveSig] int GetDeviceFormat(); [PreserveSig] int ResetDeviceFormat(); [PreserveSig] int SetDeviceFormat();
        [PreserveSig] int GetProcessingPeriod(); [PreserveSig] int SetProcessingPeriod(); [PreserveSig] int GetShareMode(); [PreserveSig] int SetShareMode();
        [PreserveSig] int GetPropertyValue(); [PreserveSig] int SetPropertyValue(); [PreserveSig] int SetDefaultEndpoint([MarshalAs(UnmanagedType.LPWStr)] string id, Role role);
        [PreserveSig] int SetEndpointVisibility([MarshalAs(UnmanagedType.LPWStr)] string id, [MarshalAs(UnmanagedType.Bool)] bool visible);
    }

    internal static class AudioCore
    {
        private static PropertyKey FriendlyName { get { return new PropertyKey { FormatId = new Guid("A45C254E-DF1C-4EFD-8020-67D146A850E0"), PropertyId = 14 }; } }
        [DllImport("propsys.dll", PreserveSig = true)] private static extern int PropVariantToStringAlloc(ref PropVariant value, out IntPtr text);
        internal static void Release(object value) { if(value != null && Marshal.IsComObject(value)) Marshal.ReleaseComObject(value); }

        public static List<AudioDevice> Enumerate(Preferences preferences)
        {
            var result = new List<AudioDevice>();
            var enumerator = (IMMDeviceEnumerator)new MMDeviceEnumeratorCom();
            foreach (Flow flow in new[] { Flow.Render, Flow.Capture })
            {
                IMMDeviceCollection collection = null;
                Marshal.ThrowExceptionForHR(enumerator.EnumAudioEndpoints(flow, 0xF, out collection));
                if(collection == null) continue;
                uint count; Marshal.ThrowExceptionForHR(collection.GetCount(out count));
                string defaultId = null;
                try { IMMDevice d; Marshal.ThrowExceptionForHR(enumerator.GetDefaultAudioEndpoint(flow, Role.Multimedia, out d)); IntPtr p; d.GetId(out p); defaultId = Marshal.PtrToStringUni(p); Marshal.FreeCoTaskMem(p); Release(d); } catch { }
                for (uint i = 0; i < count; i++)
                {
                    IMMDevice device; if (collection.Item(i, out device) < 0) continue;
                    IntPtr idPtr = IntPtr.Zero, namePtr = IntPtr.Zero;
                    try
                    {
                        device.GetId(out idPtr); string id = Marshal.PtrToStringUni(idPtr);
                        string name = id;
                        IPropertyStore store; if (device.OpenPropertyStore(0, out store) >= 0)
                        {
                            PropVariant value; PropertyKey friendlyName = FriendlyName; if (store.GetValue(ref friendlyName, out value) >= 0)
                            {
                                if (PropVariantToStringAlloc(ref value, out namePtr) >= 0 && namePtr != IntPtr.Zero) name = Marshal.PtrToStringUni(namePtr);
                                if (namePtr != IntPtr.Zero) Marshal.FreeCoTaskMem(namePtr);
                            }
                            Release(store);
                        }
                        uint state; device.GetState(out state);
                        AudioProfile profile=preferences.ActiveProfile();
                        result.Add(new AudioDevice { Id=id, Name=name, Flow=flow, State=state, IsDefault=id == defaultId, IsPreferred=flow == Flow.Render ? profile.PrimaryOutputId == id : profile.RecordingDeviceId == id, IsHidden=preferences.IsEndpointHidden(id) });
                    }
                    finally { if(idPtr != IntPtr.Zero) Marshal.FreeCoTaskMem(idPtr); Release(device); }
                }
                Release(collection);
            }
            Release(enumerator);
            return result;
        }
        public static void SetDefault(string id, bool communicationToo)
        {
            var policy = (IPolicyConfig)new PolicyConfigClientCom();
            try
            {
                Marshal.ThrowExceptionForHR(policy.SetDefaultEndpoint(id, Role.Console));
                Marshal.ThrowExceptionForHR(policy.SetDefaultEndpoint(id, Role.Multimedia));
                if (communicationToo) Marshal.ThrowExceptionForHR(policy.SetDefaultEndpoint(id, Role.Communications));
            }
            finally { Release(policy); }
        }
        public static void PlayToneOnEndpoint(string id)
        {
            var enumerator = (IMMDeviceEnumerator)new MMDeviceEnumeratorCom();
            IMMDevice device = null; IAudioClient client = null; IAudioRenderClient render = null;
            IntPtr mixFormat = IntPtr.Zero, renderPointer = IntPtr.Zero;
            bool started = false;
            AutoResetEvent readyEvent=null;
            try
            {
                Marshal.ThrowExceptionForHR(enumerator.GetDevice(id, out device));
                Guid clientId = typeof(IAudioClient).GUID;
                IntPtr clientPointer;
                Marshal.ThrowExceptionForHR(device.Activate(ref clientId, 23, IntPtr.Zero, out clientPointer));
                try { client = (IAudioClient)Marshal.GetObjectForIUnknown(clientPointer); }
                finally { Marshal.Release(clientPointer); }

                Marshal.ThrowExceptionForHR(client.GetMixFormat(out mixFormat));
                var format = (WaveFormatEx)Marshal.PtrToStructure(mixFormat, typeof(WaveFormatEx));
                ushort effectiveFormat=format.FormatTag;
                if(format.FormatTag==0xFFFE && format.ExtraSize>=22)
                {
                    var sub=(AudioSubFormat)Marshal.PtrToStructure(IntPtr.Add(mixFormat,24),typeof(AudioSubFormat));
                    if(sub.Data2==0 && sub.Data3==0x0010 && sub.Data4!=null && sub.Data4.Length==8 && sub.Data4[0]==0x80 && sub.Data4[1]==0x00 && sub.Data4[2]==0x00 && sub.Data4[3]==0xAA && sub.Data4[4]==0x00 && sub.Data4[5]==0x38 && sub.Data4[6]==0x9B && sub.Data4[7]==0x71)
                        effectiveFormat=(ushort)sub.Data1;
                }
                if (format.Channels < 1 || format.Channels > 8 || format.SamplesPerSecond < 8000 || format.AverageBytesPerSecond == 0)
                    throw new InvalidOperationException("O formato de áudio deste dispositivo não é compatível com o teste.");
                Guid sessionGuid=Guid.NewGuid();
                readyEvent=new AutoResetEvent(false);
                Marshal.ThrowExceptionForHR(client.Initialize(0, 0x40000, 0, 0, mixFormat, ref sessionGuid));
                uint bufferFrames;
                Marshal.ThrowExceptionForHR(client.GetBufferSize(out bufferFrames));
                Guid renderId = typeof(IAudioRenderClient).GUID;
                Marshal.ThrowExceptionForHR(client.GetService(ref renderId, out renderPointer));
                render = (IAudioRenderClient)Marshal.GetObjectForIUnknown(renderPointer);

                Marshal.ThrowExceptionForHR(client.SetEventHandle(readyEvent.SafeWaitHandle.DangerousGetHandle()));
                Marshal.ThrowExceptionForHR(client.Start()); started = true;
                int totalFrames = (int)(format.SamplesPerSecond * 0.72);
                int writtenFrames = 0;
                double phase = 0;
                while (writtenFrames < totalFrames)
                {
                    uint padding;
                    Marshal.ThrowExceptionForHR(client.GetCurrentPadding(out padding));
                    uint available = bufferFrames > padding ? bufferFrames - padding : 0;
                    uint frames = (uint)Math.Min(Math.Min((int)available, totalFrames - writtenFrames), 1024);
                    if (frames == 0) { readyEvent.WaitOne(80); continue; }
                    IntPtr data;
                    Marshal.ThrowExceptionForHR(render.GetBuffer(frames, out data));
                    try
                    {
                        double fade = Math.Min(1, Math.Min((writtenFrames + 1) / 600.0, (totalFrames - writtenFrames) / 600.0));
                        double step = 2.0 * Math.PI * 660 / format.SamplesPerSecond;
                        if (format.BitsPerSample == 16 && effectiveFormat == 1)
                        {
                            for (int frame = 0; frame < frames; frame++)
                            {
                                short sample = (short)(Math.Sin(phase) * 5000 * fade);
                                for (int channel = 0; channel < format.Channels; channel++) Marshal.WriteInt16(data, (frame * format.Channels + channel) * 2, sample);
                                phase += step; fade = Math.Min(1, Math.Min((writtenFrames + frame + 1) / 600.0, (totalFrames - writtenFrames - frame) / 600.0));
                            }
                        }
                        else if (format.BitsPerSample == 32 && effectiveFormat == 3)
                        {
                            for (int frame = 0; frame < frames; frame++)
                            {
                                float sample = (float)(Math.Sin(phase) * 0.16 * fade);
                                for (int channel = 0; channel < format.Channels; channel++) Marshal.WriteInt32(data, (frame * format.Channels + channel) * 4, BitConverter.ToInt32(BitConverter.GetBytes(sample), 0));
                                phase += step; fade = Math.Min(1, Math.Min((writtenFrames + frame + 1) / 600.0, (totalFrames - writtenFrames - frame) / 600.0));
                            }
                        }
                        else throw new InvalidOperationException("O formato de áudio deste dispositivo não é compatível com o tom de teste.");
                    }
                    finally { Marshal.ThrowExceptionForHR(render.ReleaseBuffer(frames, 0)); }
                    writtenFrames += (int)frames;
                }
                Thread.Sleep(90);
            }
            finally
            {
                if (started && client != null) try { client.Stop(); } catch { }
                if(readyEvent!=null)readyEvent.Dispose();
                if (renderPointer != IntPtr.Zero) Marshal.Release(renderPointer);
                if (mixFormat != IntPtr.Zero) Marshal.FreeCoTaskMem(mixFormat);
                Release(render); Release(client); Release(device); Release(enumerator);
            }
        }
        public static void SetVisible(string id, bool visible)
        {
            var policy = (IPolicyConfig)new PolicyConfigClientCom();
            Marshal.ThrowExceptionForHR(policy.SetEndpointVisibility(id, visible));
            Release(policy);
        }
    }

    internal sealed class MainForm : Form
    {
        private readonly NotifyIcon tray = new NotifyIcon();
        private readonly Preferences preferences;
        private readonly string preferenceFile;
        private bool allowExit;
        private bool previewMode;
        [DllImport("dwmapi.dll", PreserveSig=true)] private static extern int DwmSetWindowAttribute(IntPtr hwnd,int attribute,ref int value,int valueSize);
        private readonly System.Windows.Forms.Timer refreshTimer = new System.Windows.Forms.Timer();
        private readonly System.Windows.Forms.Timer clickTimer = new System.Windows.Forms.Timer();
        private readonly ComboBox primaryCombo = new ComboBox();
        private readonly ComboBox secondaryCombo = new ComboBox();
        private readonly ComboBox recordingCombo = new ComboBox();
        private readonly Panel recordingMeter = new Panel();
        private readonly Label recordingMeterLabel = new Label();
        private readonly System.Windows.Forms.Timer recordingMeterTimer = new System.Windows.Forms.Timer();
        private RoundedButton recordingPrivacyButton;
        private RoundedButton recordingMainButton;
        private RecordingLevelMonitor recordingMonitor;
        private float recordingPeak;
        private string recordingMonitorError;
        private string recordingMonitorHelp;
        private string recordingMonitorDeviceId="";
        private bool recordingPermissionDenied;
        private readonly CheckBox autoStartCheck = new CheckBox();
        private List<AudioDevice> cachedDevices = new List<AudioDevice>();
        private bool loadingUi;
        private RoundedButton profileSaveButton;
        private Label statusLabel;
        private Label profileTitle, activeBadge, deviceCountLabel, settingsStatusLabel;
        private AudioStatusIndicator audioHeaderState;
        private FlowLayoutPanel profileList;
        private Panel homePanel, settingsPanel, profileSidebar, mainRightHost;
        private TableLayoutPanel rootLayout, homeLayout, outputCards;
        private Control primaryCard, secondaryCard, recordingCard;
        private string displayedProfileId="";
        private string baselinePrimaryId="",baselineSecondaryId="",baselineRecordingId="";
        private readonly ToolTip tips=new ToolTip();
        private ToolStripMenuItem trayProfilesMenu;

        public MainForm(string[] args)
        {
            previewMode=args.Contains("--ui-preview");
            Text = "WolfHub Audio Switcher"; Width = 1080; Height = 640; MinimumSize = new Size(820, 620);
            StartPosition = FormStartPosition.CenterScreen;
            preferenceFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WolfHubAudio", "settings.xml");
            preferences = LoadPreferences(preferenceFile);
            displayedProfileId=preferences.ActiveProfile().Id;
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            BuildUi(); BuildTray(); RefreshDevices(); LoadProfileControls();
            recordingCombo.SelectedIndexChanged+=delegate { if(!loadingUi)StartRecordingMonitor(); };
            recordingMeterTimer.Interval=100; recordingMeterTimer.Tick+=delegate { UpdateRecordingMeter(); };
            VisibleChanged+=delegate { if(Visible) { StartRecordingMonitor(); recordingMeterTimer.Start(); UpdateRecordingMeter(); } else { recordingMeterTimer.Stop(); StopRecordingMonitor(); } };
            refreshTimer.Interval = 4000; refreshTimer.Tick += delegate { RefreshDevices(); }; refreshTimer.Start();
            clickTimer.Interval = 250; clickTimer.Tick += delegate { clickTimer.Stop(); ShowStatus(); };
            if (args.Contains("--tray")) { WindowState = FormWindowState.Minimized; ShowInTaskbar = false; }
        }

        private void BuildUi()
        {
            BackColor=Color.FromArgb(11,17,27); ForeColor=Color.FromArgb(237,248,250); Font=new Font("Segoe UI",9F);
            Text="WolfHub Audio"; Width=1080; Height=640; MinimumSize=new Size(820,620);
            var root=new TableLayoutPanel { Dock=DockStyle.Fill,ColumnCount=2,RowCount=2,Padding=new Padding(14,10,14,12),BackColor=BackColor };
            rootLayout=root;
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,218)); root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute,72)); root.RowStyles.Add(new RowStyle(SizeType.Percent,100));
            var header=new Panel { Dock=DockStyle.Fill };
            header.Controls.Add(new Label { Text="WolfHub Audio",Left=4,Top=6,AutoSize=true,Font=new Font("Segoe UI",16F,FontStyle.Bold),ForeColor=Color.FromArgb(237,248,250) });
            header.Controls.Add(new Label { Text="Gerenciador de dispositivos de áudio",Left=6,Top=38,AutoSize=true,Font=new Font("Segoe UI",9.5F),ForeColor=Color.FromArgb(164,185,198) });
            audioHeaderState=new AudioStatusIndicator { CurrentText="",AutoSize=false,ForeColor=Color.FromArgb(164,185,198),Font=new Font("Segoe UI",9F),Anchor=AnchorStyles.Top|AnchorStyles.Right,Top=17,Width=260,Height=34 };
            var settingsNav=CreateButton("Configurações",delegate { ShowSettings(true); }); settingsNav.Glyph=ButtonGlyph.Settings2; settingsNav.Width=152; settingsNav.Height=38; settingsNav.Anchor=AnchorStyles.Top|AnchorStyles.Right; settingsNav.Top=15;
            header.Controls.Add(settingsNav); header.Controls.Add(audioHeaderState);
            Action layoutHeader=delegate
            {
                settingsNav.Left=header.ClientSize.Width-settingsNav.Width;
                int right=settingsNav.Left-12; int statusWidth=Math.Min(320,Math.Max(0,right-300));
                audioHeaderState.Width=statusWidth; audioHeaderState.Left=Math.Max(278,right-statusWidth);
            };
            header.Resize+=delegate { layoutHeader(); }; layoutHeader();
            root.Controls.Add(header,0,0); root.SetColumnSpan(header,2);

            var sidebar=CreateSurfacePanel(); profileSidebar=sidebar; sidebar.BackColor=Color.FromArgb(16,26,39); sidebar.Dock=DockStyle.Fill; sidebar.Padding=new Padding(10); sidebar.Margin=new Padding(0,2,12,0);
            var sideLayout=new TableLayoutPanel { Dock=DockStyle.Fill,ColumnCount=1,RowCount=3,BackColor=sidebar.BackColor };
            sideLayout.RowStyles.Add(new RowStyle(SizeType.Absolute,36)); sideLayout.RowStyles.Add(new RowStyle(SizeType.Percent,100)); sideLayout.RowStyles.Add(new RowStyle(SizeType.Absolute,42));
            sideLayout.Controls.Add(new Label { Text="Perfis",Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleLeft,ForeColor=Color.FromArgb(164,185,198),Font=new Font("Segoe UI",10F,FontStyle.Bold) },0,0);
            profileList=new FlowLayoutPanel { Dock=DockStyle.Fill,FlowDirection=FlowDirection.TopDown,WrapContents=false,AutoScroll=true,BackColor=sidebar.BackColor,Padding=new Padding(0,2,2,2) }; sideLayout.Controls.Add(profileList,0,1);
            var newProfileButton=CreateButton("Novo perfil",NewProfile); newProfileButton.Dock=DockStyle.Fill; newProfileButton.Primary=true; newProfileButton.Glyph=ButtonGlyph.Plus; newProfileButton.Height=38; sideLayout.Controls.Add(newProfileButton,0,2);
            sidebar.Controls.Add(sideLayout); root.Controls.Add(sidebar,0,1);

            var rightHost=new Panel { Dock=DockStyle.Fill,BackColor=BackColor,Margin=new Padding(0,2,0,0) }; mainRightHost=rightHost;
            homePanel=new Panel { Dock=DockStyle.Fill,BackColor=BackColor,AutoScroll=true };
            settingsPanel=new Panel { Dock=DockStyle.Fill,BackColor=BackColor,Visible=false };
            BuildHomePanel(); BuildSettingsPanel(); rightHost.Controls.Add(homePanel); rightHost.Controls.Add(settingsPanel); root.Controls.Add(rightHost,1,1); Controls.Add(root);
            homePanel.Resize+=delegate { UpdateResponsiveLayout(); };
            RefreshProfileList();
            FormClosing += delegate(object s, FormClosingEventArgs e) { if (!allowExit&&!previewMode) { e.Cancel=true; Hide(); tray.ShowBalloonTip(1200,"WolfHub Audio","O aplicativo continua na bandeja do sistema.",ToolTipIcon.Info); } };
        }
        private Panel CreateSurfacePanel()
        {
            return new RoundedSurfacePanel { BackColor=Color.FromArgb(21,34,49),BorderColor=Color.FromArgb(41,61,76),Radius=12 };
        }
        private void BuildHomePanel()
        {
            homeLayout=new TableLayoutPanel { Dock=DockStyle.Top,ColumnCount=1,RowCount=4,Padding=new Padding(2,2,2,8),BackColor=BackColor,AutoSize=false,Height=474 };
            homeLayout.RowStyles.Add(new RowStyle(SizeType.Absolute,56)); homeLayout.RowStyles.Add(new RowStyle(SizeType.Absolute,170)); homeLayout.RowStyles.Add(new RowStyle(SizeType.Absolute,170)); homeLayout.RowStyles.Add(new RowStyle(SizeType.Absolute,66));
            var top=new Panel { Dock=DockStyle.Fill };
            profileTitle=new Label { Text="Perfil",Left=0,Top=0,Width=190,Height=34,AutoEllipsis=true,Font=new Font("Segoe UI",20F,FontStyle.Bold),ForeColor=Color.FromArgb(237,248,250) }; top.Controls.Add(profileTitle);
            var rename=CreateButton("Editar",RenameProfile); rename.Glyph=ButtonGlyph.Pencil; rename.SetBounds(200,2,91,34); tips.SetToolTip(rename,"Renomear perfil selecionado"); top.Controls.Add(rename);
            var more=CreateButton("Mais",ShowProfileMenu); more.Glyph=ButtonGlyph.More; more.SetBounds(297,2,82,34); tips.SetToolTip(more,"Renomear ou excluir este perfil"); top.Controls.Add(more);
            activeBadge=new Label { Text="Ativo no Windows",AutoSize=true,BackColor=Color.FromArgb(20,53,49),ForeColor=Color.FromArgb(84,214,160),Padding=new Padding(10,6,10,6),Anchor=AnchorStyles.Top|AnchorStyles.Right,Top=4,Font=new Font("Segoe UI",9F) }; top.Controls.Add(activeBadge);
            top.Resize+=delegate { activeBadge.Left=top.ClientSize.Width-activeBadge.Width-2; };
            homeLayout.Controls.Add(top,0,0);
            outputCards=new TableLayoutPanel { Dock=DockStyle.Fill,ColumnCount=2,RowCount=1,BackColor=BackColor,Margin=new Padding(0) };
            outputCards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50)); outputCards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50)); outputCards.RowStyles.Add(new RowStyle(SizeType.Percent,100));
            primaryCard=CreateDeviceCard("volume-2","Saída principal","Dispositivo usado ao ativar o perfil.",primaryCombo,"Definir principal",ActivatePrimary,true,delegate { TestCombo(primaryCombo); });
            secondaryCard=CreateDeviceCard("speaker","Saída secundária","Alternativa para troca rápida; não ativa o Windows.",secondaryCombo,null,null,true,delegate { TestCombo(secondaryCombo); });
            outputCards.Controls.Add(primaryCard,0,0); outputCards.Controls.Add(secondaryCard,1,0); homeLayout.Controls.Add(outputCards,0,1);
            recordingCard=CreateDeviceCard("mic","Dispositivo de gravação","Microfone utilizado ao ativar o perfil.",recordingCombo,"Definir gravação",ActivateRecording,false,null); homeLayout.Controls.Add(recordingCard,0,2);
            var footer=CreateSurfacePanel(); footer.BackColor=Color.FromArgb(16,26,39); footer.Dock=DockStyle.Fill; footer.Margin=new Padding(0,5,0,0);
            deviceCountLabel=new Label { Text="",ForeColor=Color.FromArgb(164,185,198),Font=new Font("Segoe UI",10F),TextAlign=ContentAlignment.MiddleLeft,AutoEllipsis=true };
            statusLabel=new Label { Text="Perfil pronto para editar.",ForeColor=Color.FromArgb(120,142,158),Font=new Font("Segoe UI",9F),TextAlign=ContentAlignment.MiddleLeft,AutoEllipsis=true };
            profileSaveButton=CreateButton("Salvar alterações",SaveCurrentProfile); profileSaveButton.Primary=true; profileSaveButton.Glyph=ButtonGlyph.Save; profileSaveButton.Width=176; profileSaveButton.Height=38; profileSaveButton.Enabled=false;
            footer.Controls.AddRange(new Control[]{deviceCountLabel,statusLabel,profileSaveButton});
            footer.Resize+=delegate { profileSaveButton.SetBounds(Math.Max(14,footer.ClientSize.Width-190),14,176,38); deviceCountLabel.SetBounds(14,8,Math.Max(130,footer.ClientSize.Width-216),21); statusLabel.SetBounds(14,31,Math.Max(130,footer.ClientSize.Width-216),20); };
            homeLayout.Controls.Add(footer,0,3);
            homePanel.Controls.Add(homeLayout); UpdateResponsiveLayout();
        }
        private void BuildSettingsPanel()
        {
            settingsPanel.AutoScroll=true;
            var title=new Label { Text="Configurações globais",AutoSize=false,Font=new Font("Segoe UI",22F,FontStyle.Bold),ForeColor=Color.FromArgb(237,248,250) };
            var subtitle=new Label { Text="Preferências do WolfHub Audio, independentes do perfil selecionado.",AutoSize=false,Font=new Font("Segoe UI",10F),ForeColor=Color.FromArgb(164,185,198) };
            settingsPanel.Controls.Add(title); settingsPanel.Controls.Add(subtitle);

            var devices=CreateSettingsSection("Dispositivos","Apelidos e visibilidade são aplicados globalmente.",148);
            var aliases=CreateButton("Gerenciar apelidos",RenameSelected); aliases.Glyph=ButtonGlyph.Pencil; aliases.Width=190; aliases.Height=38;
            var visibility=CreateButton("Gerenciar visibilidade",ShowDeviceVisibility); visibility.Glyph=ButtonGlyph.Eye; visibility.Width=210; visibility.Height=38;
            devices.Controls.Add(aliases); devices.Controls.Add(visibility);

            var startup=CreateSettingsSection("Inicialização","Inicie o WolfHub Audio automaticamente ao entrar no Windows.",112);
            autoStartCheck.Text="Iniciar com o Windows"; autoStartCheck.AutoSize=true; autoStartCheck.ForeColor=Color.FromArgb(237,248,250); autoStartCheck.BackColor=startup.BackColor; autoStartCheck.FlatStyle=FlatStyle.Flat; autoStartCheck.UseVisualStyleBackColor=false; autoStartCheck.Checked=preferences.AutoStart;
            autoStartCheck.CheckedChanged+=delegate { if(loadingUi)return; preferences.AutoStart=autoStartCheck.Checked; SavePreferences(); SetStatus(autoStartCheck.Checked?"Inicialização automática ativada.":"Inicialização automática desativada."); };
            startup.Controls.Add(autoStartCheck);

            var system=CreateSettingsSection("Sistema","Elevação é solicitada somente para ações que precisam dela.",112);
            bool isAdmin=IsAdministrator();
            system.Visible=!isAdmin;
            var elevate=CreateButton("Reiniciar como administrador",RestartElevated); elevate.Glyph=ButtonGlyph.ShieldCheck; elevate.Width=250; elevate.Height=38; system.Controls.Add(elevate);

            settingsStatusLabel=new Label { Text="",ForeColor=Color.FromArgb(169,195,204),TextAlign=ContentAlignment.MiddleLeft,AutoEllipsis=true };
            var back=CreateButton("Voltar aos perfis",delegate { ShowSettings(false); }); back.Width=180; back.Height=36;
            settingsPanel.Controls.AddRange(new Control[]{devices,startup,system,settingsStatusLabel,back});
            Action layout=delegate
            {
                int left=18, width=Math.Max(360,settingsPanel.ClientSize.Width-36-(settingsPanel.VerticalScroll.Visible?SystemInformation.VerticalScrollBarWidth:0));
                title.SetBounds(left,16,width,36); subtitle.SetBounds(left,53,width,26);
                devices.SetBounds(left,91,width,148); startup.SetBounds(left,251,width,112); system.SetBounds(left,375,width,112);
                aliases.SetBounds(16,91,190,38); visibility.SetBounds(218,91,210,38);
                autoStartCheck.SetBounds(16,68,Math.Max(200,width-32),26); elevate.SetBounds(16,68,250,38);
                int statusTop=isAdmin?375:495;
                settingsStatusLabel.SetBounds(left,statusTop,width,26); back.SetBounds(left,statusTop+34,180,36);
                settingsPanel.AutoScrollMinSize=new Size(0,isAdmin?458:578);
            };
            settingsPanel.Resize+=delegate { layout(); }; layout();
        }
        private RoundedSurfacePanel CreateSettingsSection(string title,string description,int height)
        {
            var card=new RoundedSurfacePanel { Height=height,BackColor=Color.FromArgb(21,34,49),BorderColor=Color.FromArgb(41,61,76),Radius=12 };
            card.Controls.Add(new Label { Text=title,Left=16,Top=12,Width=400,Height=24,Font=new Font("Segoe UI",11F,FontStyle.Bold),ForeColor=Color.FromArgb(237,248,250),BackColor=card.BackColor });
            card.Controls.Add(new Label { Text=description,Left=16,Top=38,Width=600,Height=24,Font=new Font("Segoe UI",9.5F),ForeColor=Color.FromArgb(164,185,198),BackColor=card.BackColor,AutoEllipsis=true });
            return card;
        }
        private void ShowSettings(bool show)
        {
            homePanel.Visible=!show; settingsPanel.Visible=show;
            if(profileSidebar!=null)profileSidebar.Visible=!show;
            if(rootLayout!=null&&mainRightHost!=null)
            {
                rootLayout.SetColumn(mainRightHost,show?0:1);
                rootLayout.SetColumnSpan(mainRightHost,show?2:1);
            }
            if(show) SetStatus("Configurações globais");
        }
        private void ShowProfileMenu()
        {
            var menu=new ContextMenuStrip(); menu.Items.Add("Renomear",null,delegate { RenameProfile(); }); menu.Items.Add("Excluir",null,delegate { DeleteProfile(); }); StyleContextMenu(menu); menu.Show(Cursor.Position);
        }
        private void StyleContextMenu(ContextMenuStrip menu)
        {
            menu.Renderer=new ToolStripProfessionalRenderer(new DarkColorTable()); menu.BackColor=Color.FromArgb(21,34,49); menu.ForeColor=Color.FromArgb(237,248,250); menu.Font=new Font("Segoe UI",9.5F); menu.ShowImageMargin=false;
            foreach(ToolStripItem item in menu.Items) item.ForeColor=Color.FromArgb(237,248,250);
        }
        private Panel CreateDeviceCard(string glyph,string heading,string help,ComboBox combo,string buttonText,Action buttonAction,bool showTest,Action testAction)
        {
            var card=CreateSurfacePanel(); card.Dock=DockStyle.Fill; card.Margin=new Padding(4); card.Padding=Padding.Empty; card.BackColor=Color.FromArgb(21,34,49);
            var icon=new LucideIconControl { IconName=glyph,IconColor=Color.FromArgb(57,197,214),ContentSize=24,Width=48,Height=48,Left=16,Top=14 };
            var title=new Label { Text=heading,AutoEllipsis=true,Font=new Font("Segoe UI",12F,FontStyle.Bold),ForeColor=Color.FromArgb(237,248,250),TextAlign=ContentAlignment.MiddleLeft };
            var description=new Label { Text=help,AutoEllipsis=false,Font=new Font("Segoe UI",9.75F),ForeColor=Color.FromArgb(164,185,198),TextAlign=ContentAlignment.TopLeft,UseMnemonic=false };
            tips.SetToolTip(description,glyph=="speaker"?help+" Selecione a saída e use Salvar alterações no rodapé; isso não a ativa no Windows.":help);
            combo.Font=new Font("Segoe UI",10F); var comboHost=CreateThemedCombo(combo); comboHost.Height=40;
            RoundedButton main=null;
            if(!String.IsNullOrEmpty(buttonText)) { main=CreateButton(buttonText,buttonAction); main.Width=showTest?158:190; main.Height=36; main.MinimumSize=new Size(118,36); main.Primary=buttonText.StartsWith("Definir"); card.Controls.Add(main); }
            RoundedButton test=null;
            if(showTest) { test=CreateButton("Testar",testAction); test.Width=92; test.Height=36; test.MinimumSize=new Size(82,36); test.Glyph=ButtonGlyph.Play; card.Controls.Add(test); tips.SetToolTip(test,"Tocar um tom de teste nesta saída; o padrão anterior será restaurado."); }
            card.Controls.AddRange(new Control[]{icon,title,description,comboHost});
            if(glyph=="mic")
            {
                recordingCard=card;
                recordingMeterLabel.Text="Sem sinal";
                recordingMeterLabel.Font=new Font("Segoe UI",9F); recordingMeterLabel.ForeColor=Color.FromArgb(120,142,158); recordingMeterLabel.AutoEllipsis=true;
                recordingMeter.BackColor=Color.FromArgb(11,17,27); recordingMeter.Height=8; recordingMeter.Paint+=delegate(object sender,PaintEventArgs e)
                {
                    Color track=recordingMeterDisabled?Color.FromArgb(30,42,53):Color.FromArgb(32,51,67);
                    using(var background=new SolidBrush(track))e.Graphics.FillRectangle(background,0,0,recordingMeter.ClientSize.Width,recordingMeter.Height);
                    int width=(int)(recordingMeter.ClientSize.Width*Math.Max(0,Math.Min(1,recordingPeak)));
                    if(!recordingMeterDisabled&&recordingPeak>=0.005F&&width>0) using(var brush=new SolidBrush(recordingPeak>=0.85?Color.FromArgb(255,112,136):Color.FromArgb(57,197,214))) e.Graphics.FillRectangle(brush,0,0,width,recordingMeter.Height);
                };
                card.Controls.Add(recordingMeterLabel); card.Controls.Add(recordingMeter);
                tips.SetToolTip(recordingMeter,"Nível instantâneo captado pelo dispositivo selecionado. O áudio não é gravado.");
                recordingPrivacyButton=CreateButton("Abrir permissões",OpenMicrophonePrivacySettings); recordingPrivacyButton.Width=160; recordingPrivacyButton.Height=30; recordingPrivacyButton.Visible=false; recordingPrivacyButton.Glyph=ButtonGlyph.Settings2;
                card.Controls.Add(recordingPrivacyButton); tips.SetToolTip(recordingPrivacyButton,"Abrir Privacidade e segurança > Microfone nas Configurações do Windows.");
            }
            Action position=delegate
            {
                int pad=16; int right=Math.Max(pad,card.ClientSize.Width-pad);
                int iconSize=Math.Max(42,Math.Min(54,Math.Min(card.ClientSize.Height/3,card.ClientSize.Width/9)));
                int textLeft=pad+iconSize+12; int selectorTop=14+iconSize+10;
                icon.SetBounds(pad,14,iconSize,iconSize); title.SetBounds(textLeft,12,Math.Max(50,right-textLeft),24); description.SetBounds(textLeft,36,Math.Max(50,right-textLeft),36);
                comboHost.SetBounds(pad,selectorTop,Math.Max(100,card.ClientSize.Width-pad*2),40);
                if(glyph=="mic") recordingMainButton=main;
                int actionTop=Math.Max(selectorTop+48,card.ClientSize.Height-44); int available=card.ClientSize.Width-pad*2;
                int groupWidth=(main==null?0:main.Width)+(test==null?0:(main==null?0:8)+test.Width);
                if(groupWidth>available)
                {
                    int actionWidth=Math.Max(118,available);
                    if(main!=null)main.SetBounds(pad,actionTop,actionWidth,36);
                    if(test!=null)test.SetBounds(pad,actionTop+40,actionWidth,36);
                }
                else
                {
                    if(main==null) { if(test!=null)test.SetBounds(card.ClientSize.Width-pad-test.Width,actionTop,test.Width,36); }
                    else { int left=card.ClientSize.Width-pad-groupWidth; main.SetBounds(left,actionTop,main.Width,36); if(test!=null)test.SetBounds(main.Right+8,actionTop,test.Width,36); }
                }
                if(glyph=="mic") PositionRecordingControls();
            };
            card.Resize+=delegate { position(); }; position(); return card;
        }
        private RoundedButton CreateButton(string text,Action action)
        {
            var b=new RoundedButton { Text=text,AutoSize=false,Height=36,Width=112,Margin=new Padding(3),Font=new Font("Segoe UI",9.5F),ForeColor=Color.FromArgb(237,248,250),BackColor=Color.FromArgb(28,45,61) };
            b.Click+=delegate { action(); }; return b;
        }
        private void AddButton(TableLayoutPanel panel,string text,int row,int column,Action action) { panel.Controls.Add(CreateButton(text,action),column,row); }
        private ThemedComboHost CreateThemedCombo(ComboBox combo)
        {
            combo.Dock=DockStyle.None; combo.DropDownStyle=ComboBoxStyle.DropDownList; combo.BackColor=Color.FromArgb(21,34,49); combo.ForeColor=Color.FromArgb(237,248,250); combo.FlatStyle=FlatStyle.Flat; combo.IntegralHeight=false; combo.DrawMode=DrawMode.OwnerDrawFixed; combo.ItemHeight=26; combo.MaxDropDownItems=9;
            combo.DrawItem+=delegate(object sender,DrawItemEventArgs e)
            {
                var choice=e.Index>=0?(combo.Items[e.Index] as AudioChoice):(combo.SelectedItem as AudioChoice); if(choice==null||e.Bounds.Width<=0||e.Bounds.Height<=0)return;
                bool selected=e.Index>=0&&(e.State&DrawItemState.Selected)!=0;
                using(var brush=new SolidBrush(selected?Color.FromArgb(37,71,83):combo.BackColor)) e.Graphics.FillRectangle(brush,e.Bounds);
                var textBounds=Rectangle.Inflate(e.Bounds,-7,0); if(e.Index<0)textBounds.Width=Math.Max(0,textBounds.Width-35);
                Color itemText=choice.Label.StartsWith("Indisponível")?Color.FromArgb(255,198,109):(selected?Color.FromArgb(57,197,214):Color.FromArgb(237,248,250));
                TextRenderer.DrawText(e.Graphics,choice.Label,combo.Font,textBounds,itemText,TextFormatFlags.Left|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis|TextFormatFlags.NoPrefix);
            };
            combo.SelectedIndexChanged+=delegate { var choice=combo.SelectedItem as AudioChoice; if(choice!=null)tips.SetToolTip(combo,choice.Label); UpdateDirtyState(); };
            return new ThemedComboHost(combo);
        }
        private void UpdateResponsiveLayout()
        {
            if(homePanel==null||homeLayout==null||outputCards==null)return;
            int available=Math.Max(360,homePanel.ClientSize.Width-(homePanel.VerticalScroll.Visible?SystemInformation.VerticalScrollBarWidth:0));
            homeLayout.Width=available-4;
            bool stack=available<760;
            outputCards.SuspendLayout();
            if(stack)
            {
                outputCards.ColumnCount=1; outputCards.RowCount=2; outputCards.ColumnStyles.Clear(); outputCards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100)); outputCards.RowStyles.Clear(); outputCards.RowStyles.Add(new RowStyle(SizeType.Absolute,170)); outputCards.RowStyles.Add(new RowStyle(SizeType.Absolute,170));
                outputCards.SetCellPosition(primaryCard,new TableLayoutPanelCellPosition(0,0)); outputCards.SetCellPosition(secondaryCard,new TableLayoutPanelCellPosition(0,1)); outputCards.Height=340; homeLayout.RowStyles[1].Height=340;
            }
            else
            {
                outputCards.ColumnCount=2; outputCards.RowCount=1; outputCards.ColumnStyles.Clear(); outputCards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50)); outputCards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50)); outputCards.RowStyles.Clear(); outputCards.RowStyles.Add(new RowStyle(SizeType.Percent,100));
                outputCards.SetCellPosition(primaryCard,new TableLayoutPanelCellPosition(0,0)); outputCards.SetCellPosition(secondaryCard,new TableLayoutPanelCellPosition(1,0)); outputCards.Height=170; homeLayout.RowStyles[1].Height=170;
            }
            outputCards.ResumeLayout(); homeLayout.Height=56+outputCards.Height+170+66+12;
        }
        private void StartRecordingMonitor()
        {
            if(!Visible)return;
            string selectedId=SelectedId(recordingCombo);
            if(selectedId==recordingMonitorDeviceId&&recordingMonitor!=null)return;
            if(recordingMonitor!=null) { recordingMonitor.Dispose(); recordingMonitor=null; }
            recordingMonitorDeviceId=selectedId;
            recordingPeak=0; recordingMonitorError=null; recordingMonitorHelp=null; recordingPermissionDenied=false;
            var device=cachedDevices.FirstOrDefault(x=>x.Id==selectedId&&x.Flow==Flow.Capture&&x.IsActive&&!x.IsDisabled);
            if(device==null) { recordingMonitorError="Dispositivo indisponível"; recordingMonitorHelp="Conecte ou habilite o microfone no Windows e atualize os dispositivos."; return; }
            recordingMonitor=new RecordingLevelMonitor(device.Id);
            try { recordingMonitor.Start(); }
            catch(Exception ex) { recordingMonitorError=RecordingLevelMonitor.DescribeCaptureError(ex); recordingMonitorHelp=RecordingLevelMonitor.DescribeCaptureHelp(ex); recordingPermissionDenied=RecordingLevelMonitor.IsPermissionDenied(ex); recordingMonitor.Dispose(); recordingMonitor=null; recordingMonitorDeviceId=""; }
        }
        private void StopRecordingMonitor()
        {
            if(recordingMonitor!=null) { recordingMonitor.Dispose(); recordingMonitor=null; }
            bool hadSignal=recordingPeak>=0.005F; recordingMonitorDeviceId=""; recordingPeak=0;
            if(hadSignal&&!recordingMeter.IsDisposed)recordingMeter.Invalidate();
        }
        private bool recordingMeterDisabled;
        private void PositionRecordingControls()
        {
            if(recordingCard==null||recordingPrivacyButton==null)return;
            int pad=16; int reserved=recordingPrivacyButton.Visible?380:200;
            int width=Math.Max(100,recordingCard.ClientSize.Width-pad*2-reserved);
            recordingMeterLabel.SetBounds(pad,119,width,20); recordingMeter.SetBounds(pad,143,width,8);
            int rightOfMain=recordingMainButton==null?recordingCard.ClientSize.Width-pad:recordingMainButton.Left-8;
            recordingPrivacyButton.SetBounds(Math.Max(pad,rightOfMain-recordingPrivacyButton.Width),132,recordingPrivacyButton.Width,30);
        }
        private void OpenMicrophonePrivacySettings()
        {
            try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName=RecordingLevelMonitor.MicrophonePrivacySettingsUri,UseShellExecute=true }); SetStatus("Configurações de privacidade do microfone abertas."); }
            catch(Exception ex) { SetStatus("Não foi possível abrir as configurações de microfone: "+ex.Message); }
        }
        private void UpdateRecordingMeter()
        {
            if(recordingMeter.IsDisposed)return;
            float previousPeak=recordingPeak; bool wasDisabled=recordingMeterDisabled; bool wasPermissionVisible=recordingPrivacyButton!=null&&recordingPrivacyButton.Visible;
            if(recordingMonitor!=null)
            {
                try { recordingPeak=recordingMonitor.ReadAvailablePeak(); }
                catch(Exception ex) { recordingMonitorError=RecordingLevelMonitor.DescribeCaptureError(ex); recordingMonitorHelp=RecordingLevelMonitor.DescribeCaptureHelp(ex); recordingPermissionDenied=RecordingLevelMonitor.IsPermissionDenied(ex); StopRecordingMonitor(); }
            }
            recordingMeterDisabled=recordingMonitorError!=null;
            if(recordingPrivacyButton!=null)recordingPrivacyButton.Visible=recordingPermissionDenied;
            if(wasPermissionVisible!=(recordingPrivacyButton!=null&&recordingPrivacyButton.Visible))PositionRecordingControls();
            string label=RecordingMeterPresentation.GetLabel(recordingPeak,recordingMeterDisabled,recordingPermissionDenied,recordingMonitorError);
            if(recordingMeterLabel.Text!=label)
            {
                recordingMeterLabel.Text=label;
                recordingMeterLabel.ForeColor=recordingMeterDisabled?Color.FromArgb(120,142,158):(recordingPeak<0.005F?Color.FromArgb(120,142,158):Color.FromArgb(164,185,198));
            }
            string help=recordingMonitorError!=null?(recordingMonitorHelp??recordingMonitorError):(recordingPeak<0.005F?"Sem sinal do microfone. Fale ou produza som próximo ao dispositivo para verificar o nível.":"Nível de pico lido localmente. WolfHub Audio não grava nem armazena o áudio.");
            if(tips.GetToolTip(recordingMeterLabel)!=help)tips.SetToolTip(recordingMeterLabel,help);
            if(RecordingMeterPresentation.ShouldRedraw(previousPeak,recordingPeak,wasDisabled,recordingMeterDisabled))recordingMeter.Invalidate();
        }
        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            refreshTimer.Stop(); refreshTimer.Dispose(); clickTimer.Stop(); clickTimer.Dispose(); recordingMeterTimer.Stop(); recordingMeterTimer.Dispose();
            StopRecordingMonitor(); tips.Dispose(); tray.Visible=false; tray.Dispose();
            base.OnFormClosed(e);
        }
        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            try { int enabled=1; int hr=DwmSetWindowAttribute(Handle,20,ref enabled,sizeof(int)); if(hr!=0)DwmSetWindowAttribute(Handle,19,ref enabled,sizeof(int)); } catch { }
        }
        private void SetStatus(string text) { if(statusLabel!=null) statusLabel.Text=text; if(settingsStatusLabel!=null)settingsStatusLabel.Text=text; }
        private bool IsDirty()
        {
            if(String.IsNullOrEmpty(displayedProfileId))return false;
            return baselinePrimaryId!=SelectedId(primaryCombo)||baselineSecondaryId!=SelectedId(secondaryCombo)||baselineRecordingId!=SelectedId(recordingCombo);
        }
        private void UpdateDirtyState()
        {
            bool dirty=IsDirty();
            if(profileSaveButton!=null)profileSaveButton.Enabled=dirty;
            if(dirty)
            {
                SetStatus("Alterações não salvas neste perfil.");
            }
            else if(statusLabel!=null&&statusLabel.Text=="Alterações não salvas neste perfil.")SetStatus("Perfil pronto para editar.");
        }
        private void RefreshProfileList()
        {
            if(profileList==null)return; profileList.SuspendLayout(); profileList.Controls.Clear();
            foreach(var p in preferences.Profiles)
            {
                string id=p.Id; bool selected=id==displayedProfileId,applied=id==preferences.AppliedProfileId;
                var b=new ProfileEntryButton { Text=p.Name,Selected=selected,Applied=applied,Width=Math.Max(180,profileList.ClientSize.Width-24),Height=40,Font=new Font("Segoe UI",10F,selected?FontStyle.Bold:FontStyle.Regular),Margin=new Padding(0,3,0,3) };
                tips.SetToolTip(b,applied?"Este perfil foi aplicado ao Windows":"Selecionar este perfil"); b.Click+=delegate { SelectProfile(id); }; profileList.Controls.Add(b);
            }
            profileList.ResumeLayout();
        }
        private void UpdateProfileHeader()
        {
            var p=preferences.Profiles.FirstOrDefault(x=>x.Id==displayedProfileId); if(p==null)return;
            profileTitle.Text=p.Name; tips.SetToolTip(profileTitle,p.Name); activeBadge.Text=p.Id==preferences.AppliedProfileId?"Ativo no Windows":"Ainda não aplicado";
            activeBadge.ForeColor=p.Id==preferences.AppliedProfileId?Color.FromArgb(84,214,160):Color.FromArgb(164,185,198);
            activeBadge.BackColor=p.Id==preferences.AppliedProfileId?Color.FromArgb(20,53,49):Color.FromArgb(28,45,61);
            RefreshProfileList(); UpdateDirtyState();
        }
        private void SelectProfile(string id)
        {
            if(id==displayedProfileId)return;
            if(IsDirty())
            {
                var answer=MessageBox.Show("Salvar as alterações deste perfil antes de trocar?\n\nSim: salvar e trocar\nNão: descartar e trocar\nCancelar: continuar aqui","Alterações não salvas",MessageBoxButtons.YesNoCancel,MessageBoxIcon.Question);
                if(answer==DialogResult.Cancel) { RefreshProfileList(); return; }
                if(answer==DialogResult.Yes) SaveCurrentProfile();
            }
            displayedProfileId=id; preferences.ActiveProfileId=id; SavePreferences(); LoadProfileControls(); UpdateProfileHeader();
        }
        private void RefreshDevices()
        {
            try
            {
                cachedDevices=AudioCore.Enumerate(preferences);
                string primary=SelectedId(primaryCombo),secondary=SelectedId(secondaryCombo),recording=SelectedId(recordingCombo);
                loadingUi=true;
                try { FillCombo(primaryCombo,Flow.Render,primary); FillCombo(secondaryCombo,Flow.Render,secondary); FillCombo(recordingCombo,Flow.Capture,recording); }
                finally { loadingUi=false; }
                bool selectedRecordingAvailable=cachedDevices.Any(x=>x.Id==recording&&x.Flow==Flow.Capture&&x.IsActive&&!x.IsDisabled);
                if(recording!=recordingMonitorDeviceId||(recordingMonitor==null&&recordingMonitorError!=null&&selectedRecordingAvailable))StartRecordingMonitor();
                if(deviceCountLabel!=null&&deviceCountLabel.Text!=cachedDevices.Count+" dispositivos")deviceCountLabel.Text=cachedDevices.Count+" dispositivos";
                if(audioHeaderState!=null)
                {
                    var currentOut=cachedDevices.FirstOrDefault(x=>x.Flow==Flow.Render&&x.IsDefault); var currentMic=cachedDevices.FirstOrDefault(x=>x.Flow==Flow.Capture&&x.IsDefault);
                    string outName=currentOut==null?"desconhecida":preferences.GetAlias(currentOut.Id)??currentOut.Name; string micName=currentMic==null?"desconhecido":preferences.GetAlias(currentMic.Id)??currentMic.Name;
                    string currentText="Saída ativa: "+outName,fullText="Saída atual: "+outName+"\nGravação atual: "+micName; bool hasDefault=currentOut!=null;
                    if(audioHeaderState.CurrentText!=currentText||audioHeaderState.FullText!=fullText||audioHeaderState.HasDefault!=hasDefault)
                    {
                        audioHeaderState.CurrentText=currentText; audioHeaderState.FullText=fullText; audioHeaderState.HasDefault=hasDefault; audioHeaderState.Invalidate(); tips.SetToolTip(audioHeaderState,fullText);
                    }
                }
            }
            catch(Exception ex) { SetStatus("Não foi possível consultar os dispositivos de áudio: "+ex.Message); }
        }
        private void FillCombo(ComboBox combo,Flow flow,string selectedId)
        {
            combo.BeginUpdate(); combo.Items.Clear();
            var available=cachedDevices.Where(x=>x.Flow==flow&&x.IsActive&&!x.IsDisabled&&!x.IsHidden).ToList();
            foreach(var d in available)
            {
                string alias=preferences.GetAlias(d.Id); string display=String.IsNullOrWhiteSpace(alias)?d.Name:alias+"  ·  "+d.Name;
                if(d.IsDefault) display+="  ·  padrão atual no Windows";
                if(d.IsDisabled) display+="  (disabled)";
                combo.Items.Add(new AudioChoice { Id=d.Id,Label=display });
            }
            if(!String.IsNullOrEmpty(selectedId)&&!available.Any(x=>x.Id==selectedId))
            {
                var saved=cachedDevices.FirstOrDefault(x=>x.Id==selectedId);
                string name=saved==null?selectedId:(preferences.GetAlias(saved.Id)??saved.Name);
                string state=saved!=null&&saved.IsHidden?"Oculto globalmente · ":"Indisponível ou desconectado · ";
                combo.Items.Add(new AudioChoice { Id=selectedId,Label=state+name });
            }
            combo.EndUpdate(); SelectComboId(combo,selectedId);
        }
        private string SelectedId(ComboBox combo) { var choice=combo.SelectedItem as AudioChoice; return choice==null?"":choice.Id; }
        private void SelectComboId(ComboBox combo,string id,bool selectFirstIfMissing=true)
        {
            int index=-1; for(int i=0;i<combo.Items.Count;i++) { var c=combo.Items[i] as AudioChoice; if(c!=null&&c.Id==id) { index=i; break; } }
            combo.SelectedIndex=index>=0?index:(selectFirstIfMissing&&combo.Items.Count>0?0:-1);
        }
        private void LoadProfileControls()
        {
            loadingUi=true;
            try
            {
                AudioProfile profile=preferences.ActiveProfile();
                displayedProfileId=profile.Id;
                string primaryId=profile.PrimaryOutputId;
                if(String.IsNullOrEmpty(primaryId)) { var current=cachedDevices.FirstOrDefault(x=>x.Flow==Flow.Render&&x.IsDefault&&x.IsActive&&!x.IsDisabled); if(current!=null)primaryId=current.Id; }
                string recordingId=profile.RecordingDeviceId;
                if(String.IsNullOrEmpty(recordingId)) { var current=cachedDevices.FirstOrDefault(x=>x.Flow==Flow.Capture&&x.IsDefault&&x.IsActive&&!x.IsDisabled); if(current!=null)recordingId=current.Id; }
                SelectComboId(primaryCombo,primaryId); SelectComboId(secondaryCombo,profile.SecondaryOutputId,false); SelectComboId(recordingCombo,recordingId);
                baselinePrimaryId=SelectedId(primaryCombo); baselineSecondaryId=SelectedId(secondaryCombo); baselineRecordingId=SelectedId(recordingCombo);
            }
            finally { loadingUi=false; }
            StartRecordingMonitor();
            UpdateProfileHeader();
        }
        private void SaveCurrentProfile()
        {
            var profile=preferences.Profiles.FirstOrDefault(x=>x.Id==displayedProfileId); if(profile==null)return;
            profile.PrimaryOutputId=SelectedId(primaryCombo); profile.SecondaryOutputId=SelectedId(secondaryCombo); profile.RecordingDeviceId=SelectedId(recordingCombo);
            baselinePrimaryId=profile.PrimaryOutputId; baselineSecondaryId=profile.SecondaryOutputId; baselineRecordingId=profile.RecordingDeviceId;
            preferences.ActiveProfileId=profile.Id; preferences.PreferredOutputId=profile.PrimaryOutputId; preferences.PreferredMicId=profile.RecordingDeviceId; SavePreferences();
            UpdateProfileHeader(); SetStatus("Perfil '"+profile.Name+"' salvo.");
        }
        private void NewProfile()
        {
            if(IsDirty()) { var answer=MessageBox.Show("Salvar as alterações do perfil atual antes de criar outro?","Alterações não salvas",MessageBoxButtons.YesNoCancel,MessageBoxIcon.Question); if(answer==DialogResult.Cancel)return; if(answer==DialogResult.Yes)SaveCurrentProfile(); }
            string name=Prompt.Show("Dê um nome para o novo perfil:","Novo perfil",""); if(String.IsNullOrWhiteSpace(name))return;
            var p=new AudioProfile { Id=Guid.NewGuid().ToString("N"),Name=name.Trim() };
            preferences.Profiles.Add(p); preferences.ActiveProfileId=p.Id; displayedProfileId=p.Id; LoadProfileControls(); SavePreferences(); RefreshTrayProfiles(); SetStatus("Perfil '"+p.Name+"' criado.");
        }
        private void RenameProfile()
        {
            var p=preferences.Profiles.FirstOrDefault(x=>x.Id==displayedProfileId); if(p==null)return; string name=Prompt.Show("Nome do perfil:","Renomear perfil",p.Name); if(String.IsNullOrWhiteSpace(name))return;
            p.Name=name.Trim(); SavePreferences(); UpdateProfileHeader(); RefreshTrayProfiles(); SetStatus("Perfil renomeado para '"+p.Name+"'.");
        }
        private void DeleteProfile()
        {
            var p=preferences.Profiles.FirstOrDefault(x=>x.Id==displayedProfileId); if(p==null)return; if(preferences.Profiles.Count<=1) { MessageBox.Show("Mantenha pelo menos um perfil.","Perfis de áudio"); return; }
            if(IsDirty()) { var dirtyAnswer=MessageBox.Show("Salvar as alterações antes de excluir este perfil?\n\nSim: salvar\nNão: descartar\nCancelar: voltar","Alterações não salvas",MessageBoxButtons.YesNoCancel,MessageBoxIcon.Question); if(dirtyAnswer==DialogResult.Cancel)return; if(dirtyAnswer==DialogResult.Yes)SaveCurrentProfile(); }
            if(MessageBox.Show("Excluir o perfil '"+p.Name+"'?","Excluir perfil",MessageBoxButtons.YesNo,MessageBoxIcon.Warning)!=DialogResult.Yes)return;
            preferences.Profiles.Remove(p); if(preferences.AppliedProfileId==p.Id)preferences.AppliedProfileId=""; preferences.ActiveProfileId=preferences.Profiles[0].Id; displayedProfileId=preferences.ActiveProfileId; LoadProfileControls(); SavePreferences(); RefreshTrayProfiles(); SetStatus("Perfil excluído. Aplique um perfil para associar novamente os padrões atuais.");
        }
        private void ActivatePrimary()
        {
            SaveCurrentProfile(); var p=preferences.Profiles.FirstOrDefault(x=>x.Id==displayedProfileId); var output=p==null?null:cachedDevices.FirstOrDefault(x=>x.Id==p.PrimaryOutputId&&x.Flow==Flow.Render&&x.IsActive&&!x.IsDisabled); if(output==null) { MessageBox.Show("Selecione uma saída principal disponível.","Perfil de áudio"); return; }
            try
            {
                AudioCore.SetDefault(output.Id,true);
                var mic=cachedDevices.FirstOrDefault(x=>x.Id==p.RecordingDeviceId && x.Flow==Flow.Capture&&x.IsActive&&!x.IsDisabled); if(mic!=null) AudioCore.SetDefault(mic.Id,true);
                preferences.AppliedProfileId=p.Id; preferences.ActiveProfileId=p.Id; SavePreferences(); RefreshDevices(); UpdateProfileHeader(); Notify("Perfil aplicado",p.Name+" · "+(preferences.GetAlias(output.Id)??output.Name)); SetStatus("Perfil aplicado ao Windows.");
            } catch(Exception ex) { ShowError(ex); }
        }
        private void ActivateRecording()
        {
            SaveCurrentProfile(); var p=preferences.Profiles.FirstOrDefault(x=>x.Id==displayedProfileId); var mic=p==null?null:cachedDevices.FirstOrDefault(x=>x.Id==p.RecordingDeviceId&&x.Flow==Flow.Capture&&x.IsActive&&!x.IsDisabled);
            if(mic==null) { MessageBox.Show("Selecione um dispositivo de gravação disponível.","Perfil de áudio"); return; }
            if(MessageBox.Show("Definir '"+mic.Name+"' como dispositivo padrão de gravação do Windows?","Confirmar alteração",MessageBoxButtons.YesNo,MessageBoxIcon.Question)!=DialogResult.Yes)return;
            try { AudioCore.SetDefault(mic.Id,true); RefreshDevices(); Notify("Gravação alterada",preferences.GetAlias(mic.Id)??mic.Name); SetStatus("Dispositivo de gravação aplicado."); } catch(Exception ex) { ShowError(ex); }
        }
        private void TestCombo(ComboBox combo)
        {
            string id=SelectedId(combo); var d=cachedDevices.FirstOrDefault(x=>x.Id==id&&x.Flow==Flow.Render&&x.IsActive&&!x.IsDisabled); if(d==null) { MessageBox.Show("Selecione uma saída de áudio disponível.","Teste de saída"); return; } TestDevice(d);
        }
        private void BuildTray()
        {
            tray.Icon=Icon; tray.Text="WolfHub Audio"; tray.Visible=true;
            var menu=new ContextMenuStrip();
            menu.Items.Add("Abrir WolfHub Audio",null,delegate { ShowWindow(); });
            trayProfilesMenu=new ToolStripMenuItem("Perfis");
            menu.Items.Add(trayProfilesMenu);
            menu.Items.Add("Consultar áudio atual",null,delegate { ShowStatus(); });
            var switchItem=new ToolStripMenuItem("Alternar saída principal / secundária",IconResources.CreateImage("arrow-left-right",18,Color.FromArgb(164,185,198)),delegate { SwitchPreferred(); }); menu.Items.Add(switchItem);
            var testItem=new ToolStripMenuItem("Testar saída atual",IconResources.CreateImage("play",18,Color.FromArgb(164,185,198)),delegate { TestCurrent(); }); menu.Items.Add(testItem);
            menu.Items.Add(new ToolStripSeparator()); menu.Items.Add("Sair",null,delegate { allowExit=true; Close(); });
            StyleContextMenu(menu);
            menu.Opening+=delegate { RefreshTrayProfiles(); };
            tray.ContextMenuStrip=menu;
            tray.MouseClick += delegate(object s,MouseEventArgs e) { if(e.Button==MouseButtons.Left) { clickTimer.Stop(); clickTimer.Start(); } };
            tray.MouseDoubleClick += delegate(object s,MouseEventArgs e) { if(e.Button==MouseButtons.Left) { clickTimer.Stop(); SwitchPreferred(); } };
        }
        private void RefreshTrayProfiles()
        {
            if(trayProfilesMenu==null)return;
            trayProfilesMenu.DropDownItems.Clear();
            foreach(var profile in preferences.Profiles)
            {
                string id=profile.Id;
                var item=new ToolStripMenuItem(profile.Name) { Checked=id==preferences.AppliedProfileId,CheckOnClick=false };
                item.Click+=delegate { ActivateProfileFromTray(id); };
                trayProfilesMenu.DropDownItems.Add(item);
            }
            if(trayProfilesMenu.DropDownItems.Count==0) { trayProfilesMenu.Enabled=false; trayProfilesMenu.DropDownItems.Add(new ToolStripMenuItem("Nenhum perfil disponível") { Enabled=false }); }
            else trayProfilesMenu.Enabled=true;
        }
        private void ActivateProfileFromTray(string profileId)
        {
            var profile=preferences.Profiles.FirstOrDefault(x=>x.Id==profileId); if(profile==null)return;
            if(IsDirty())
            {
                var answer=MessageBox.Show("Salvar as alterações do perfil aberto antes de aplicar '"+profile.Name+"'?\n\nSim: salvar e continuar\nNão: descartar e continuar\nCancelar: não aplicar","Alterações não salvas",MessageBoxButtons.YesNoCancel,MessageBoxIcon.Question);
                if(answer==DialogResult.Cancel)return;
                if(answer==DialogResult.Yes)SaveCurrentProfile();
            }
            try
            {
                var devices=AudioCore.Enumerate(preferences);
                var output=devices.FirstOrDefault(x=>x.Id==profile.PrimaryOutputId&&x.Flow==Flow.Render&&x.IsActive&&!x.IsDisabled);
                var mic=devices.FirstOrDefault(x=>x.Id==profile.RecordingDeviceId&&x.Flow==Flow.Capture&&x.IsActive&&!x.IsDisabled);
                if(output==null) { Notify("Perfil incompleto",profile.Name+" precisa de uma saída principal disponível."); return; }
                AudioCore.SetDefault(output.Id,true);
                if(mic!=null) AudioCore.SetDefault(mic.Id,true);
                preferences.ActiveProfileId=profile.Id; preferences.AppliedProfileId=profile.Id; preferences.PreferredOutputId=profile.PrimaryOutputId; preferences.PreferredMicId=profile.RecordingDeviceId; displayedProfileId=profile.Id;
                LoadProfileControls(); SavePreferences(); RefreshDevices(); RefreshTrayProfiles();
                Notify("Perfil aplicado",profile.Name+" · "+(preferences.GetAlias(output.Id)??output.Name));
            }
            catch(Exception ex) { Notify("Não foi possível aplicar o perfil",ex.Message); }
        }
        private void ShowWindow() { Show(); ShowInTaskbar=true; WindowState=FormWindowState.Normal; Activate(); }
        private void SetDefaultSelected() { ActivatePrimary(); }
        private void TestSelected() { TestCombo(primaryCombo); }
        private void TestCurrent() { try { var d=AudioCore.Enumerate(preferences).FirstOrDefault(x=>x.Flow==Flow.Render && x.IsDefault); if(d!=null) TestDevice(d); } catch(Exception ex) { ShowError(ex); } }
        private void TestDevice(AudioDevice d)
        {
            if(MessageBox.Show("Reproduzir um tom curto diretamente em '"+(preferences.GetAlias(d.Id)??d.Name)+"'? O teste não altera a saída padrão do Windows.","Teste de saída",MessageBoxButtons.YesNo,MessageBoxIcon.Question)!=DialogResult.Yes) return;
            try
            {
                AudioCore.PlayToneOnEndpoint(d.Id);
                SetStatus("Tom de teste reproduzido em '"+(preferences.GetAlias(d.Id)??d.Name)+"'.");
            } catch(Exception ex) { ShowError(ex); }
        }
        private void RenameSelected()
        {
            var d=ChooseDevice("Selecione um dispositivo para apelidar"); if(d==null)return;
            RenameDevice(d);
        }
        private void RenameDevice(AudioDevice d)
        {
            if(d==null)return;
            string current=preferences.GetAlias(d.Id) ?? d.Name;
            string value=Prompt.Show("Este apelido aparece somente no WolfHub Audio:","Apelido do dispositivo",current);
            if(value==null)return; preferences.SetAlias(d.Id,value==d.Name?"":value); SavePreferences(); RefreshDevices(); SetStatus("Apelido global atualizado.");
        }
        private void SetPrioritySelected() { SaveCurrentProfile(); SetStatus("Saída principal e gravação do perfil salvas como preferidas."); }
        private void ShowDeviceVisibility()
        {
            try
            {
                using(var dialog=new DeviceVisibilityDialog(delegate { return AudioCore.Enumerate(preferences); },SetDeviceVisibility,RenameDevice,TestDevice,preferences))dialog.ShowDialog(this);
                RefreshDevices();
            }
            catch(Exception ex) { ShowError(ex); }
        }
        private bool SetDeviceVisibility(AudioDevice device,bool visible)
        {
            if(device==null)return false;
            if(visible==!device.IsHidden)return true;
            string operation=visible?"mostrar":"ocultar";
            string impact=device.IsDefault||device.IsPreferred?"\n\nEste dispositivo está em uso como padrão ou no perfil selecionado.":"";
            if(MessageBox.Show("Isso vai "+operation+" '"+device.Name+"' nas listas de dispositivos de áudio do Windows."+impact+"\n\nContinuar?","Confirmar alteração",MessageBoxButtons.YesNo,MessageBoxIcon.Warning)!=DialogResult.Yes)return false;
            if(!IsAdministrator())
            {
                if(MessageBox.Show("Esta ação pode exigir privilégios de administrador. Reiniciar como administrador agora?","Permissão de administrador",MessageBoxButtons.YesNo,MessageBoxIcon.Question)!=DialogResult.Yes)return false;
                RestartElevated(); return false;
            }
            try
            {
                AudioCore.SetVisible(device.Id,visible); preferences.SetEndpointHidden(device.Id,!visible); SavePreferences(); RefreshDevices();
                Notify("Dispositivo atualizado",device.Name+" foi "+(visible?"mostrado":"ocultado")+"."); SetStatus("Visibilidade global atualizada."); return true;
            }
            catch(Exception ex) { ShowError(ex); return false; }
        }
        private AudioDevice ChooseDevice(string title)
        {
            using(var dialog=new DevicePicker(cachedDevices,preferences,title)) if(dialog.ShowDialog(this)==DialogResult.OK) return dialog.Selected;
            return null;
        }
        private void SwitchPreferred()
        {
            try
            {
                AudioProfile profile=preferences.Profiles.FirstOrDefault(x=>x.Id==preferences.AppliedProfileId);
                if(profile==null) { Notify("Alternância de áudio","Aplique um perfil antes de alternar as saídas."); return; }
                var primary=cachedDevices.FirstOrDefault(x=>x.Id==profile.PrimaryOutputId&&x.Flow==Flow.Render&&x.IsActive&&!x.IsDisabled);
                var secondary=cachedDevices.FirstOrDefault(x=>x.Id==profile.SecondaryOutputId&&x.Flow==Flow.Render&&x.IsActive&&!x.IsDisabled);
                if(primary==null||secondary==null) { Notify("Alternância de áudio","Configure uma saída principal e uma secundária disponíveis no perfil aplicado."); return; }
                var current=cachedDevices.FirstOrDefault(x=>x.Flow==Flow.Render&&x.IsDefault);
                var target=current!=null&&current.Id==primary.Id?secondary:primary;
                AudioCore.SetDefault(target.Id,true); RefreshDevices();
                Notify(profile.Name+" · áudio alternado",preferences.GetAlias(target.Id)??target.Name);
            } catch(Exception ex) { Notify("Falha ao alternar áudio",ex.Message); }
        }
        private void ShowStatus()
        {
            try
            {
                var devices=AudioCore.Enumerate(preferences); var current=devices.FirstOrDefault(x=>x.Flow==Flow.Render&&x.IsDefault); var mic=devices.FirstOrDefault(x=>x.Flow==Flow.Capture&&x.IsDefault);
                var profile=preferences.Profiles.FirstOrDefault(x=>x.Id==preferences.AppliedProfileId); Notify("Áudio atual · "+(profile==null?"sem perfil aplicado":profile.Name),"Saída: "+(current==null?"desconhecida":preferences.GetAlias(current.Id)??current.Name)+" | Microfone: "+(mic==null?"desconhecido":preferences.GetAlias(mic.Id)??mic.Name));
            } catch(Exception ex) { Notify("Status de áudio",ex.Message); }
        }
        private void Notify(string title,string text) { tray.BalloonTipTitle=title; tray.BalloonTipText=text.Length>220?text.Substring(0,217)+"...":text; tray.BalloonTipIcon=ToolTipIcon.Info; tray.ShowBalloonTip(1300); }
        private void ShowError(Exception ex) { SetStatus("Erro: "+ex.Message); MessageBox.Show(ex.Message,"WolfHub Audio",MessageBoxButtons.OK,MessageBoxIcon.Error); }
        private static bool IsAdministrator() { var p=new System.Security.Principal.WindowsPrincipal(System.Security.Principal.WindowsIdentity.GetCurrent()); return p.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator); }
        private void RestartElevated()
        {
            if(IsAdministrator()) { SetStatus("O aplicativo já está executando como administrador."); return; }
            try { var psi=new System.Diagnostics.ProcessStartInfo(Application.ExecutablePath,"--elevated"); psi.Verb="runas"; System.Diagnostics.Process.Start(psi); allowExit=true; Application.Exit(); }
            catch(System.ComponentModel.Win32Exception) { Notify("Elevação cancelada","O aplicativo continuará sem privilégios de administrador."); }
        }
        private static Preferences LoadPreferences(string path)
        {
            Preferences p; try { using(var f=File.OpenRead(path)) p=(Preferences)new XmlSerializer(typeof(Preferences)).Deserialize(f); } catch { p=new Preferences(); }
            p.Normalize(); return p;
        }
        private void SavePreferences()
        {
            try { Directory.CreateDirectory(Path.GetDirectoryName(preferenceFile)); using(var f=File.Create(preferenceFile)) new XmlSerializer(typeof(Preferences)).Serialize(f,preferences); }
            catch(Exception ex) { ShowError(ex); }
            try { using(var key=Microsoft.Win32.Registry.CurrentUser.OpenSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Run",true)) { if(key!=null) { if(preferences.AutoStart) key.SetValue("WolfHubAudio", "\""+Application.ExecutablePath+"\" --tray"); else key.DeleteValue("WolfHubAudio",false); } } } catch { }
        }
    }

    internal sealed class DevicePicker : Form
    {
        private readonly ComboBox combo=new ComboBox(); private readonly List<AudioDevice> devices;
        public AudioDevice Selected { get { var c=combo.SelectedItem as AudioChoice; return c==null?null:devices.FirstOrDefault(x=>x.Id==c.Id); } }
        public DevicePicker(List<AudioDevice> devices,Preferences preferences,string title)
        {
            this.devices=devices; Text=title; Width=520; Height=194; FormBorderStyle=FormBorderStyle.FixedDialog; StartPosition=FormStartPosition.CenterParent; MinimizeBox=false; MaximizeBox=false; BackColor=Color.FromArgb(11,17,27); ForeColor=Color.FromArgb(237,248,250); Font=new Font("Segoe UI",9.5F);
            var label=new Label { Text=title,Left=14,Top=12,Width=470,ForeColor=Color.FromArgb(237,248,250),BackColor=BackColor };
            combo.DropDownStyle=ComboBoxStyle.DropDownList; combo.BackColor=Color.FromArgb(21,34,49); combo.ForeColor=Color.FromArgb(237,248,250); combo.FlatStyle=FlatStyle.Flat; combo.DrawMode=DrawMode.OwnerDrawFixed; combo.ItemHeight=25;
            combo.DrawItem+=delegate(object s,DrawItemEventArgs e) { if(e.Index<0)return; var c=combo.Items[e.Index] as AudioChoice; if(c==null)return; bool selected=(e.State&DrawItemState.Selected)!=0; using(var b=new SolidBrush(selected?Color.FromArgb(37,71,83):combo.BackColor))e.Graphics.FillRectangle(b,e.Bounds); TextRenderer.DrawText(e.Graphics,c.Label,combo.Font,Rectangle.Inflate(e.Bounds,-6,0),selected?Color.FromArgb(57,197,214):Color.FromArgb(237,248,250),TextFormatFlags.Left|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis); };
            foreach(var d in devices) combo.Items.Add(new AudioChoice { Id=d.Id,Label=d.TypeName+" · "+(preferences.GetAlias(d.Id)??d.Name) });
            if(combo.Items.Count>0)combo.SelectedIndex=0;
            var comboHost=new ThemedComboHost(combo); comboHost.SetBounds(14,40,470,40);
            var ok=new RoundedButton { Text="Continuar",Left=310,Top=108,Width=82,Height=36,Primary=true,DialogResult=DialogResult.OK,ForeColor=Color.FromArgb(237,248,250),Font=new Font("Segoe UI",9F) };
            var cancel=new RoundedButton { Text="Cancelar",Left=402,Top=108,Width=82,Height=36,DialogResult=DialogResult.Cancel,ForeColor=Color.FromArgb(237,248,250),Font=new Font("Segoe UI",9F) };
            Controls.AddRange(new Control[]{label,comboHost,ok,cancel}); AcceptButton=ok; CancelButton=cancel;
        }
    }
    internal sealed class Prompt : Form
    {
        private TextBox input;
        private Prompt(string message,string title,string initial)
        {
            Text=title; Width=440; Height=155; FormBorderStyle=FormBorderStyle.FixedDialog; StartPosition=FormStartPosition.CenterParent; MinimizeBox=false; MaximizeBox=false; BackColor=Color.FromArgb(11,17,27); ForeColor=Color.FromArgb(237,248,250); Font=new Font("Segoe UI",9.5F);
            var label=new Label { Text=message,Left=12,Top=12,Width=400,Height=24,ForeColor=Color.FromArgb(237,248,250),BackColor=BackColor };
            input=new TextBox { Left=12,Top=42,Width=400,Height=28,Text=initial??"",BackColor=Color.FromArgb(21,34,49),ForeColor=Color.FromArgb(237,248,250),BorderStyle=BorderStyle.FixedSingle };
            var ok=new RoundedButton { Text="Salvar",Left=250,Top=82,Width=76,Height=34,DialogResult=DialogResult.OK,Primary=true,ForeColor=Color.FromArgb(237,248,250),Font=new Font("Segoe UI",9F) };
            var cancel=new RoundedButton { Text="Cancelar",Left=337,Top=82,Width=76,Height=34,DialogResult=DialogResult.Cancel,ForeColor=Color.FromArgb(237,248,250),Font=new Font("Segoe UI",9F) };
            Controls.AddRange(new Control[]{label,input,ok,cancel}); AcceptButton=ok; CancelButton=cancel;
        }
        public static string Show(string message,string title,string initial)
        { using(var p=new Prompt(message,title,initial)) return p.ShowDialog()==DialogResult.OK?p.input.Text:null; }
    }

    internal static class Program
    {
        [STAThread] private static void Main(string[] args)
        {
            if(args.Contains("--self-test"))
            {
                try
                {
                    if(RecordingMeterPresentation.GetLabel(0,false,false,null)!="Sem sinal"||RecordingMeterPresentation.GetLabel(0.236F,false,false,null)!="Nível de entrada · 24%"||RecordingMeterPresentation.GetLabel(0,true,true,"permission")!="Medidor desativado · permissão necessária"||RecordingMeterPresentation.GetLabel(0,true,false,"unavailable")!="Nível indisponível")throw new InvalidOperationException("Recording meter presentation check failed.");
                    if(!RecordingMeterPresentation.ShouldRedraw(0,0.02F,false,false)||RecordingMeterPresentation.ShouldRedraw(0.03F,0.031F,false,false)||!RecordingMeterPresentation.ShouldRedraw(0,0,false,true))throw new InvalidOperationException("Recording meter redraw threshold check failed.");
                    IconResources.ValidateResources();
                    var devices=AudioCore.Enumerate(new Preferences());
                    VerifyCapturePeak();
                    VerifyCaptureErrors();
                    var prefs=new Preferences(); prefs.Normalize();
                    prefs.Profiles[0].Name="Gaming"; prefs.Profiles[0].PrimaryOutputId="primary"; prefs.Profiles[0].SecondaryOutputId="secondary"; prefs.Profiles[0].RecordingDeviceId="mic";
                    prefs.SetEndpointHidden("hidden-endpoint",true);
                    prefs.Profiles.Add(new AudioProfile { Id="work",Name="Work",PrimaryOutputId="work-primary",SecondaryOutputId="work-secondary",RecordingDeviceId="work-mic" });
                    prefs.ActiveProfileId="work"; prefs.AppliedProfileId=prefs.Profiles[0].Id; prefs.AppliedProfileStateInitialized=true;
                    var draft=new AudioProfile { Id="draft",Name="Draft" }; prefs.Profiles.Add(draft); draft.Name="Renamed"; prefs.Profiles.Remove(draft);
                    if(prefs.Profiles.Any(x=>x.Id=="draft")) throw new InvalidOperationException("Profile deletion check failed.");
                    var serializer=new XmlSerializer(typeof(Preferences));
                    using(var stream=new MemoryStream()) { serializer.Serialize(stream,prefs); stream.Position=0; var restored=(Preferences)serializer.Deserialize(stream); if(restored.Profiles.Count!=2||restored.ActiveProfile().Name!="Work"||restored.ActiveProfile().SecondaryOutputId!="work-secondary"||restored.AppliedProfileId!=restored.Profiles[0].Id||!restored.IsEndpointHidden("hidden-endpoint")) throw new InvalidOperationException("Profile/global preference persistence check failed."); }
                    using(var writer=new StringWriter()) { var legacy=new Preferences(); serializer.Serialize(writer,legacy); string oldXml=writer.ToString().Replace("<HiddenEndpointIds />",""); using(var reader=new StringReader(oldXml)) { var restored=(Preferences)serializer.Deserialize(reader); restored.Normalize(); if(restored.HiddenEndpointIds==null||restored.HiddenEndpointIds.Count!=0)throw new InvalidOperationException("Legacy XML compatibility check failed."); } }
                    if(!AudioDeviceUiState.CanTest(new AudioDevice { Flow=Flow.Render,State=1 })||AudioDeviceUiState.CanTest(new AudioDevice { Flow=Flow.Capture,State=1 })||AudioDeviceUiState.CanTest(new AudioDevice { Flow=Flow.Render,State=2 }))throw new InvalidOperationException("Device management test availability check failed.");
                    Console.WriteLine("Embedded Lucide icons OK (15); Core Audio enumeration OK ({0} endpoints); profile create/rename/delete and XML save/load OK; selected/applied state preserved.",devices.Count); Environment.ExitCode=0;
                }
                catch(Exception ex) { Console.Error.WriteLine("Core Audio enumeration failed: "+ex); Environment.ExitCode=1; }
                return;
            }
            bool created; string mutexName=args.Contains("--ui-preview")?"Local\\WolfHubAudioSwitcherPreview":"Local\\WolfHubAudioSwitcher";
            using(var mutex=new Mutex(true,mutexName,out created))
            {
                if(!created && args.Contains("--elevated")) { try { created=mutex.WaitOne(5000); } catch(AbandonedMutexException) { created=true; } }
                if(!created) { MessageBox.Show("O WolfHub Audio já está em execução na bandeja do sistema."); return; }
                Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false); Application.Run(new MainForm(args));
            }
        }
        private static void VerifyCapturePeak()
        {
            byte[] pcm16={0x00,0x40}; IntPtr memory=Marshal.AllocHGlobal(pcm16.Length);
            try
            {
                Marshal.Copy(pcm16,0,memory,pcm16.Length);
                float level=RecordingLevelMonitor.ReadPeak(memory,1,new WaveFormatEx { Channels=1,BitsPerSample=16,BlockAlign=2 },1);
                if(Math.Abs(level-0.5F)>0.001F)throw new InvalidOperationException("PCM16 capture peak calculation failed.");
            }
            finally { Marshal.FreeHGlobal(memory); }
            if(!new AudioDevice { State=1 }.IsActive||new AudioDevice { State=9 }.IsActive)
                throw new InvalidOperationException("Audio endpoint active-state validation failed.");
        }
        private static void VerifyCaptureErrors()
        {
            if(RecordingLevelMonitor.DescribeCaptureError(new COMException("not implemented",unchecked((int)0x80004001)))!="Driver sem suporte à leitura de nível")
                throw new InvalidOperationException("Capture driver error description check failed.");
            if(RecordingLevelMonitor.DescribeCaptureError(new COMException("device invalidated",unchecked((int)0x88890004)))!="Dispositivo desconectado ou reconfigurado")
                throw new InvalidOperationException("Capture endpoint invalidation description check failed.");
            if(RecordingLevelMonitor.DescribeCaptureError(new COMException("access denied",unchecked((int)0x80070005)))!="Acesso ao microfone bloqueado pelo Windows")
                throw new InvalidOperationException("Capture privacy error description check failed.");
            if(!RecordingLevelMonitor.DescribeCaptureHelp(new COMException("access denied",unchecked((int)0x80070005))).Contains("aplicativos da área de trabalho"))
                throw new InvalidOperationException("Capture privacy help text check failed.");
            if(!RecordingLevelMonitor.IsPermissionDenied(new COMException("access denied",unchecked((int)0x80070005)))||RecordingLevelMonitor.IsPermissionDenied(new COMException("not implemented",unchecked((int)0x80004001))))
                throw new InvalidOperationException("Capture permission-state detection check failed.");
            if(RecordingLevelMonitor.MicrophonePrivacySettingsUri!="ms-settings:privacy-microphone")
                throw new InvalidOperationException("Microphone privacy settings URI check failed.");
        }
    }
}
