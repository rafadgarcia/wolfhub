using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using Microsoft.Win32;

namespace WolfHub
{
    internal enum ButtonGlyph { None, Play, Pencil, More, Plus, Settings2, Eye, EyeOff, ArrowLeftRight, Save, ChevronDown, ShieldCheck, RefreshCw }

    internal static class IconResources
    {
        private static readonly Dictionary<string, Bitmap> cache = new Dictionary<string, Bitmap>(StringComparer.OrdinalIgnoreCase);
        private static readonly object sync = new object();

        public static string Name(ButtonGlyph glyph)
        {
            switch (glyph)
            {
                case ButtonGlyph.Play: return "play";
                case ButtonGlyph.Pencil: return "pencil";
                case ButtonGlyph.More: return "ellipsis";
                case ButtonGlyph.Plus: return "plus";
                case ButtonGlyph.Settings2: return "settings-2";
                case ButtonGlyph.Eye: return "eye";
                case ButtonGlyph.EyeOff: return "eye-off";
                case ButtonGlyph.ArrowLeftRight: return "arrow-left-right";
                case ButtonGlyph.Save: return "save";
                case ButtonGlyph.ChevronDown: return "chevron-down";
                case ButtonGlyph.ShieldCheck: return "shield-check";
                case ButtonGlyph.RefreshCw: return "refresh-cw";
                default: return null;
            }
        }

        public static void Draw(Graphics graphics, string name, Rectangle bounds, Color color)
        {
            if (String.IsNullOrEmpty(name) || bounds.Width <= 0 || bounds.Height <= 0) return;
            Bitmap image = Get(name);
            if (image == null) return;
            var matrix = new System.Drawing.Imaging.ColorMatrix(new float[][] {
                new float[]{0,0,0,0,0},
                new float[]{0,0,0,0,0},
                new float[]{0,0,0,0,0},
                new float[]{0,0,0,1,0},
                new float[]{color.R/255F,color.G/255F,color.B/255F,0,1}
            });
            using (var attributes = new System.Drawing.Imaging.ImageAttributes())
            {
                attributes.SetColorMatrix(matrix);
                var oldInterpolation = graphics.InterpolationMode;
                var oldPixelOffset = graphics.PixelOffsetMode;
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                graphics.DrawImage(image, bounds, 0, 0, image.Width, image.Height, GraphicsUnit.Pixel, attributes);
                graphics.InterpolationMode = oldInterpolation;
                graphics.PixelOffsetMode = oldPixelOffset;
            }
        }

        private static Bitmap Get(string name)
        {
            lock (sync)
            {
                Bitmap result;
                if (cache.TryGetValue(name, out result)) return result;
                string resource = "WolfHub.Icons." + name + ".png";
                using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resource))
                {
                    if (stream == null) return null;
                    using (var source = Image.FromStream(stream)) result = new Bitmap(source);
                }
                cache.Add(name, result);
                return result;
            }
        }
    }

    internal static class GraphicsExtensions
    {
        [DllImport("dwmapi.dll", PreserveSig = true)]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int valueSize);

        public static void ApplyDarkTitleBar(IntPtr handle)
        {
            if (handle == IntPtr.Zero) return;
            try
            {
                int enabled = 1;
                int hr = DwmSetWindowAttribute(handle, 20, ref enabled, sizeof(int));
                if (hr != 0) DwmSetWindowAttribute(handle, 19, ref enabled, sizeof(int));
            }
            catch { }
        }

        public static Color ResolveParentBackColor(Control control, Color fallback)
        {
            Control current = control != null ? control.Parent : null;
            while (current != null)
            {
                if (current.BackColor != Color.Transparent && current.BackColor.A == 255) return current.BackColor;
                current = current.Parent;
            }
            return fallback;
        }

        public static GraphicsPath CreateRoundedRectanglePath(RectangleF r, float radius)
        {
            var p = new GraphicsPath();
            float d = Math.Min(radius * 2F, Math.Min(r.Width, r.Height));
            if (d <= 1F) { p.AddRectangle(r); return p; }
            p.AddArc(r.Left, r.Top, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Top, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.Left, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        public static void FillRoundedRectangle(this Graphics g, Brush brush, Rectangle r, int radius)
        {
            using (var p = CreateRoundedRectanglePath(new RectangleF(r.X, r.Y, r.Width, r.Height), radius))
                g.FillPath(brush, p);
        }
    }

    internal static class UiSpacing
    {
        public static int Scale(int logical, int dpi)
        {
            if (dpi <= 0) dpi = 96;
            return Math.Max(0, (int)Math.Round(logical * dpi / 96.0, MidpointRounding.AwayFromZero));
        }

        public static int Scale(Control parent, int logical)
        {
            if (parent == null) return logical;
            if (!parent.IsHandleCreated) return logical;
            using (Graphics graphics = parent.CreateGraphics())
                return Scale(logical, (int)Math.Round(graphics.DpiX));
        }
    }

    internal class RoundedSurfacePanel : Panel
    {
        public Color BorderColor = Color.FromArgb(41, 61, 76);
        public int Radius = 12;

        public RoundedSurfacePanel()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            e.Graphics.Clear(GraphicsExtensions.ResolveParentBackColor(this, Color.FromArgb(11, 17, 27)));
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            var rect = new RectangleF(0.5F, 0.5F, Math.Max(1F, Width - 2F), Math.Max(1F, Height - 2F));
            using (var path = GraphicsExtensions.CreateRoundedRectanglePath(rect, Radius))
            using (var fill = new SolidBrush(BackColor))
                e.Graphics.FillPath(fill, path);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            var rect = new RectangleF(0.5F, 0.5F, Math.Max(1F, Width - 2F), Math.Max(1F, Height - 2F));
            using (var path = GraphicsExtensions.CreateRoundedRectanglePath(rect, Radius))
            using (var pen = new Pen(BorderColor, 1F))
                e.Graphics.DrawPath(pen, path);
        }
    }

    internal sealed class RoundedButton : Button
    {
        private bool primary;
        private ButtonGlyph glyph;
        private bool hover, pressed;

        public bool Primary
        {
            get { return primary; }
            set { if (primary != value) { primary = value; Invalidate(); } }
        }

        public ButtonGlyph Glyph
        {
            get { return glyph; }
            set { if (glyph != value) { glyph = value; Invalidate(); } }
        }

        public RoundedButton()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            Cursor = Cursors.Hand;
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            e.Graphics.Clear(GraphicsExtensions.ResolveParentBackColor(this, Color.FromArgb(21, 34, 49)));
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(GraphicsExtensions.ResolveParentBackColor(this, Color.FromArgb(21, 34, 49)));
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            var rect = new RectangleF(0.5F, 0.5F, Math.Max(1F, Width - 2F), Math.Max(1F, Height - 2F));
            Color fill, border, text;
            if (!Enabled)
            {
                fill = Color.FromArgb(20, 31, 44);
                border = Color.FromArgb(34, 50, 64);
                text = Color.FromArgb(106, 128, 144);
            }
            else if (Primary)
            {
                fill = pressed ? Color.FromArgb(42, 166, 182) : (hover ? Color.FromArgb(78, 212, 228) : Color.FromArgb(57, 197, 214));
                border = Color.FromArgb(95, 224, 238);
                text = Color.FromArgb(8, 22, 32);
            }
            else
            {
                fill = pressed ? Color.FromArgb(22, 36, 50) : (hover ? Color.FromArgb(35, 55, 74) : BackColor);
                border = hover ? Color.FromArgb(66, 98, 118) : Color.FromArgb(43, 65, 84);
                text = ForeColor;
            }

            using (var path = GraphicsExtensions.CreateRoundedRectanglePath(rect, 8F))
            using (var brush = new SolidBrush(fill))
            using (var pen = new Pen(border, 1F))
            {
                e.Graphics.FillPath(brush, path);
                e.Graphics.DrawPath(pen, path);
            }

            Font renderFont = Primary ? new Font(Font, FontStyle.Bold) : Font;
            try
            {
                int iconSize = Glyph == ButtonGlyph.None ? 0 : Math.Max(14, (int)Math.Round(15 * e.Graphics.DpiX / 96F));
                string label = Text ?? "";
                if (String.IsNullOrEmpty(label) && iconSize > 0)
                {
                    var iconRect = new Rectangle((Width - iconSize) / 2, (Height - iconSize) / 2, iconSize, iconSize);
                    IconResources.Draw(e.Graphics, IconResources.Name(Glyph), iconRect, text);
                }
                else if (iconSize > 0)
                {
                    int gap = Math.Max(6, (int)Math.Round(6 * e.Graphics.DpiX / 96F));
                    Size textSize = TextRenderer.MeasureText(e.Graphics, label, renderFont, new Size(Width, Height), TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);
                    int totalW = iconSize + gap + textSize.Width;
                    int startX = Math.Max(8, (Width - totalW) / 2);
                    var iconRect = new Rectangle(startX, (Height - iconSize) / 2, iconSize, iconSize);
                    IconResources.Draw(e.Graphics, IconResources.Name(Glyph), iconRect, text);
                    var textRect = new Rectangle(iconRect.Right + gap, 0, Math.Max(0, Width - (iconRect.Right + gap) - 6), Height);
                    TextRenderer.DrawText(e.Graphics, label, renderFont, textRect, text, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
                }
                else
                {
                    TextRenderer.DrawText(e.Graphics, label, renderFont, new Rectangle(6, 0, Math.Max(0, Width - 12), Height), text, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
                }
            }
            finally
            {
                if (Primary && ! Object.ReferenceEquals(renderFont, Font)) renderFont.Dispose();
            }
        }

        protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); hover = true; Invalidate(); }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); hover = false; pressed = false; Invalidate(); }
        protected override void OnMouseDown(MouseEventArgs e) { base.OnMouseDown(e); pressed = true; Invalidate(); }
        protected override void OnMouseUp(MouseEventArgs e) { base.OnMouseUp(e); pressed = false; Invalidate(); }
        protected override void OnEnabledChanged(EventArgs e) { base.OnEnabledChanged(e); Invalidate(); }
    }

    internal sealed class NavEntryButton : Button
    {
        public bool Selected;
        public string Subtitle = "";
        private bool hover;

        public NavEntryButton()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            Cursor = Cursors.Hand;
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            e.Graphics.Clear(GraphicsExtensions.ResolveParentBackColor(this, Color.FromArgb(16, 26, 39)));
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(GraphicsExtensions.ResolveParentBackColor(this, Color.FromArgb(16, 26, 39)));
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            var rect = new RectangleF(0.5F, 0.5F, Math.Max(1F, Width - 2F), Math.Max(1F, Height - 2F));
            Color bg = Selected ? Color.FromArgb(29, 60, 74) : (hover ? Color.FromArgb(26, 42, 58) : Color.FromArgb(21, 34, 49));
            Color border = Selected ? Color.FromArgb(57, 197, 214) : (hover ? Color.FromArgb(54, 80, 100) : Color.FromArgb(33, 50, 66));
            using (var path = GraphicsExtensions.CreateRoundedRectanglePath(rect, 8F))
            using (var fill = new SolidBrush(bg))
            using (var pen = new Pen(border, 1F))
            {
                e.Graphics.FillPath(fill, path);
                e.Graphics.DrawPath(pen, path);
            }
            if (Selected)
            {
                using (var bar = new SolidBrush(Color.FromArgb(57, 197, 214)))
                    e.Graphics.FillRoundedRectangle(bar, new Rectangle(6, 10, 3, Math.Max(8, Height - 20)), 2);
            }
            int left = Selected ? 16 : 12;
            Color titleColor = Selected ? Color.FromArgb(242, 250, 252) : Color.FromArgb(218, 234, 242);
            Color subColor = Selected ? Color.FromArgb(156, 218, 228) : Color.FromArgb(138, 160, 176);
            using (var titleFont = new Font("Segoe UI", 9.75F, Selected ? FontStyle.Bold : FontStyle.Regular))
            using (var subFont = new Font("Segoe UI", 8F))
            {
                TextRenderer.DrawText(e.Graphics, Text, titleFont, new Rectangle(left, 6, Math.Max(0, Width - left - 10), 20), titleColor, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
                if (!String.IsNullOrEmpty(Subtitle))
                    TextRenderer.DrawText(e.Graphics, Subtitle, subFont, new Rectangle(left, 26, Math.Max(0, Width - left - 10), 16), subColor, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
            }
        }

        protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); hover = true; Invalidate(); }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); hover = false; Invalidate(); }
    }

    internal sealed class StatusBadgeControl : Control
    {
        private bool isActive;
        public bool IsActive { get { return isActive; } set { if (isActive != value) { isActive = value; Invalidate(); } } }

        public StatusBadgeControl()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            e.Graphics.Clear(GraphicsExtensions.ResolveParentBackColor(this, Color.FromArgb(21, 34, 49)));
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(GraphicsExtensions.ResolveParentBackColor(this, Color.FromArgb(21, 34, 49)));
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            var rect = new RectangleF(0.5F, 0.5F, Math.Max(1F, Width - 2F), Math.Max(1F, Height - 2F));
            Color fill = isActive ? Color.FromArgb(19, 50, 46) : Color.FromArgb(22, 35, 49);
            Color border = isActive ? Color.FromArgb(40, 108, 90) : Color.FromArgb(41, 61, 76);
            Color fg = isActive ? Color.FromArgb(84, 214, 160) : Color.FromArgb(156, 178, 192);
            using (var path = GraphicsExtensions.CreateRoundedRectanglePath(rect, 8F))
            using (var brush = new SolidBrush(fill))
            using (var pen = new Pen(border, 1F))
            {
                e.Graphics.FillPath(brush, path);
                e.Graphics.DrawPath(pen, path);
            }
            Size textSize = TextRenderer.MeasureText(e.Graphics, Text, Font, new Size(int.MaxValue, int.MaxValue), TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);
            int dotSize = 7, gap = 6;
            int totalW = dotSize + gap + textSize.Width;
            int startX = Math.Max(8, (Width - totalW) / 2);
            using (var dot = new SolidBrush(fg))
                e.Graphics.FillEllipse(dot, startX, (Height - dotSize) / 2, dotSize, dotSize);
            var textRect = new Rectangle(startX + dotSize + gap, 0, Math.Max(0, Width - (startX + dotSize + gap) - 4), Height);
            TextRenderer.DrawText(e.Graphics, Text, Font, textRect, fg, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
        }

        protected override void OnTextChanged(EventArgs e) { base.OnTextChanged(e); Invalidate(); }
    }

    internal sealed class CatalogPackage
    {
        public string Id;
        public string Name;
        public string WingetId;
        public string Category;
        public string Description;
        public bool Installed;
        public bool Selected;
    }

    internal sealed class CatalogProfile
    {
        public string Id;
        public string Name;
        public string Description;
        public List<string> Packages = new List<string>();
    }

    internal static class SystemActions
    {
        public const string InstallRoot = @"C:\wolfhub";

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);

        public static void TurnOffMonitor(IntPtr formHandle)
        {
            ThreadPool.QueueUserWorkItem(delegate
            {
                Thread.Sleep(350);
                try { SendMessage(new IntPtr(0xFFFF), 0x0112, new IntPtr(0xF170), new IntPtr(2)); } catch { }
            });
        }

        public static bool IsAdministrator()
        {
            try
            {
                var p = new WindowsPrincipal(WindowsIdentity.GetCurrent());
                return p.IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch { return false; }
        }

        public static string ResolveRepoRoot()
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            DirectoryInfo dir = new DirectoryInfo(baseDir);
            for (int i = 0; i < 8 && dir != null; i++)
            {
                if (File.Exists(Path.Combine(dir.FullName, "config", "packages.json")) && File.Exists(Path.Combine(dir.FullName, "config", "modules.json")))
                    return dir.FullName;
                dir = dir.Parent;
            }
            return baseDir;
        }

        public static string ReadRepoText(string repoRoot, string relativePath, string resourceName)
        {
            string path = Path.Combine(repoRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(path)) return File.ReadAllText(path, Encoding.UTF8);
            using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName))
            {
                if (stream == null) throw new FileNotFoundException("WolfHub resource was not packaged: " + relativePath);
                using (var reader = new StreamReader(stream, Encoding.UTF8)) return reader.ReadToEnd();
            }
        }

        public static string ExtractAudioAppResource()
        {
            const string resourceName = "WolfHub.Audio.WolfHubAudio.exe";
            using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName))
            {
                if (stream == null) return null;
                string stamp = File.GetLastWriteTimeUtc(Assembly.GetExecutingAssembly().Location).Ticks.ToString();
                string directory = Path.Combine(Path.GetTempPath(), "WolfHub", "modules", stamp);
                Directory.CreateDirectory(directory);
                string path = Path.Combine(directory, "WolfHubAudio.exe");
                if (File.Exists(path)) return path;
                using (var file = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None)) stream.CopyTo(file);
                return path;
            }
        }

        public static bool IsInstalledInWolfHub(string exeName)
        {
            return File.Exists(Path.Combine(InstallRoot, exeName));
        }

        public static string InstallToolToWolfHub(string sourcePath, string targetExeName, string shortcutTitle, string description, bool createDesktopShortcut)
        {
            if (String.IsNullOrEmpty(sourcePath) || !File.Exists(sourcePath))
                throw new FileNotFoundException("Arquivo de origem não encontrado: " + sourcePath);

            Directory.CreateDirectory(InstallRoot);
            string targetPath = Path.Combine(InstallRoot, targetExeName);
            if (!String.Equals(Path.GetFullPath(sourcePath), Path.GetFullPath(targetPath), StringComparison.OrdinalIgnoreCase))
            {
                File.Copy(sourcePath, targetPath, true);
            }

            if (createDesktopShortcut)
            {
                string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                string lnkPath = Path.Combine(desktop, shortcutTitle + ".lnk");
                CreateShortcut(lnkPath, targetPath, InstallRoot, description);
            }

            string startMenu = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "WolfHub");
            Directory.CreateDirectory(startMenu);
            CreateShortcut(Path.Combine(startMenu, shortcutTitle + ".lnk"), targetPath, InstallRoot, description);

            return targetPath;
        }

        public static void CreateShortcut(string shortcutPath, string targetPath, string workingDir, string description)
        {
            Type shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType == null) return;
            object shell = Activator.CreateInstance(shellType);
            try
            {
                object shortcut = shellType.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell, new object[] { shortcutPath });
                Type scType = shortcut.GetType();
                scType.InvokeMember("TargetPath", BindingFlags.SetProperty, null, shortcut, new object[] { targetPath });
                scType.InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, shortcut, new object[] { workingDir });
                scType.InvokeMember("Description", BindingFlags.SetProperty, null, shortcut, new object[] { description ?? "" });
                scType.InvokeMember("IconLocation", BindingFlags.SetProperty, null, shortcut, new object[] { targetPath + ",0" });
                scType.InvokeMember("Save", BindingFlags.InvokeMethod, null, shortcut, null);
            }
            finally
            {
                if (Marshal.IsComObject(shell)) Marshal.ReleaseComObject(shell);
            }
        }

        public static string RunProcessCapture(string fileName, string arguments)
        {
            var psi = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8
            };
            using (var p = Process.Start(psi))
            {
                string output = p.StandardOutput.ReadToEnd();
                p.WaitForExit(15000);
                return output ?? "";
            }
        }
    }

    internal sealed class MainForm : Form
    {
        private readonly string repoRoot;
        private FeatureCatalog featureCatalog;
        private readonly ToolTip tips = new ToolTip();
        private readonly List<CatalogPackage> packages = new List<CatalogPackage>();
        private readonly List<CatalogProfile> profiles = new List<CatalogProfile>();

        private Panel headerPanel, sidebarPanel, contentHost, footerPanel;
        private Label brandTitle, brandSubtitle, statusLabel, envLabel;
        private RoundedButton openFolderButton, elevateHeaderButton;
        private readonly List<NavEntryButton> navButtons = new List<NavEntryButton>();

        private Panel toolkitPage, packagesPage, tweaksPage, backupPage;
        private Panel toolkitViewport, packagesViewport, tweaksViewport;
        private int activeTabIndex;
        private string selectedCategory = "Todos";
        private readonly List<RoundedButton> categoryButtons = new List<RoundedButton>();
        private Label packageSummaryLabel;
        private RoundedButton installSelectedButton, upgradeAllButton;
        private bool checkingWinget;
        private bool wingetInventoryChecked;
        private bool wingetInventoryAvailable;
        private string capturePreviewPath;
        private int captureTab;
        private Label backupTitle, backupSubtitle, backupDetails;
        private RoundedButton createBackupButton, restoreBackupButton;

        public MainForm(string[] args)
        {
            repoRoot = SystemActions.ResolveRepoRoot();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == "--capture-preview") capturePreviewPath = args[i + 1];
                if (args[i] == "--tab") int.TryParse(args[i + 1], out captureTab);
            }

            tips.InitialDelay = 180;
            tips.ReshowDelay = 80;
            tips.AutoPopDelay = 12000;
            tips.ShowAlways = true;

            LoadCatalogAndProfiles();
            BuildUi();
            SwitchTab(captureTab);

            Shown += delegate
            {
                LayoutMainWindow();
                if (!String.IsNullOrEmpty(capturePreviewPath))
                {
                    var t = new System.Windows.Forms.Timer { Interval = 350 };
                    t.Tick += delegate
                    {
                        t.Stop(); t.Dispose();
                        try
                        {
                            Activate(); BringToFront(); Refresh(); Application.DoEvents();
                            using (var bmp = new Bitmap(Width, Height))
                            {
                                DrawToBitmap(bmp, new Rectangle(0, 0, Width, Height));
                                Directory.CreateDirectory(Path.GetDirectoryName(capturePreviewPath));
                                bmp.Save(capturePreviewPath, System.Drawing.Imaging.ImageFormat.Png);
                            }
                        }
                        catch { }
                        Close();
                    };
                    t.Start();
                }
                else
                {
                    RefreshWingetInstalledAsync();
                }
            };
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            GraphicsExtensions.ApplyDarkTitleBar(Handle);
        }

        private void LoadCatalogAndProfiles()
        {
            packages.Clear();
            profiles.Clear();
            try
            {
                var serializer = new JavaScriptSerializer();
                string packageJson = SystemActions.ReadRepoText(repoRoot, "config/packages.json", "WolfHub.Config.packages.json");
                if (!String.IsNullOrWhiteSpace(packageJson))
                {
                    var root = serializer.Deserialize<Dictionary<string, object>>(packageJson);
                    var list = root != null && root.ContainsKey("packages") ? root["packages"] as System.Collections.ArrayList : null;
                    if (list != null)
                    {
                        foreach (Dictionary<string, object> item in list)
                        {
                            var package = new CatalogPackage
                            {
                                Id = GetStr(item, "id"),
                                Name = GetStr(item, "name"),
                                WingetId = GetStr(item, "wingetId"),
                                Category = GetStr(item, "category"),
                                Description = GetStr(item, "description")
                            };
                            if (String.IsNullOrWhiteSpace(package.Id) || String.IsNullOrWhiteSpace(package.Name) || String.IsNullOrWhiteSpace(package.Category) || !Regex.IsMatch(package.WingetId ?? "", @"^[A-Za-z0-9][A-Za-z0-9._+-]*$"))
                                throw new InvalidDataException("The package catalog contains an invalid id, name, category or WinGet identifier.");
                            if (packages.Any(x => String.Equals(x.Id, package.Id, StringComparison.OrdinalIgnoreCase)))
                                throw new InvalidDataException("Duplicate package catalog id: " + package.Id);
                            packages.Add(package);
                        }
                    }
                }

                string profDir = Path.Combine(repoRoot, "profiles");
                if (Directory.Exists(profDir) && Directory.GetFiles(profDir, "*.json").Length > 0)
                {
                    foreach (string file in Directory.GetFiles(profDir, "*.json"))
                    {
                        var dict = serializer.Deserialize<Dictionary<string, object>>(File.ReadAllText(file, Encoding.UTF8));
                        if (dict == null) continue;
                        var prof = new CatalogProfile
                        {
                            Id = GetStr(dict, "id"),
                            Name = GetStr(dict, "name"),
                            Description = GetStr(dict, "description")
                        };
                        var pkgList = dict.ContainsKey("packages") ? dict["packages"] as System.Collections.ArrayList : null;
                        if (pkgList != null)
                        {
                            foreach (object pid in pkgList) if (pid != null) prof.Packages.Add(pid.ToString());
                        }
                        profiles.Add(prof);
                    }
                }
                else
                {
                    foreach (string profileId in new[] { "minimal", "personal", "support" })
                    {
                        string json = SystemActions.ReadRepoText(repoRoot, "profiles/" + profileId + ".json", "WolfHub.Profiles." + profileId + ".json");
                        var dict = serializer.Deserialize<Dictionary<string, object>>(json);
                        if (dict == null) continue;
                        var prof = new CatalogProfile { Id = GetStr(dict, "id"), Name = GetStr(dict, "name"), Description = GetStr(dict, "description") };
                        var pkgList = dict.ContainsKey("packages") ? dict["packages"] as System.Collections.ArrayList : null;
                        if (pkgList != null) foreach (object pid in pkgList) if (pid != null) prof.Packages.Add(pid.ToString());
                        profiles.Add(prof);
                    }
                }
                var packageIds = new HashSet<string>(packages.Select(x => x.Id), StringComparer.OrdinalIgnoreCase);
                foreach (CatalogProfile profile in profiles)
                    foreach (string id in profile.Packages)
                        if (!packageIds.Contains(id)) throw new InvalidDataException("Profile '" + profile.Id + "' refers to unknown package '" + id + "'.");
                featureCatalog = FeatureCatalog.Load(
                    SystemActions.ReadRepoText(repoRoot, "config/features.json", "WolfHub.Config.features.json"),
                    SystemActions.ReadRepoText(repoRoot, "config/modules.json", "WolfHub.Config.modules.json"));
            }
            catch (Exception ex) { throw new InvalidOperationException("Não foi possível carregar o catálogo modular do WolfHub: " + ex.Message, ex); }
        }

        private static string GetStr(Dictionary<string, object> d, string key)
        {
            object v;
            return d != null && d.TryGetValue(key, out v) && v != null ? v.ToString() : "";
        }

        private static string EscapePowerShellLiteral(string value)
        {
            return (value ?? "").Replace("'", "''").Replace("\r", " ").Replace("\n", " ");
        }

        private void BuildUi()
        {
            AutoScaleMode = AutoScaleMode.Dpi;
            AutoScaleDimensions = new SizeF(96F, 96F);
            BackColor = Color.FromArgb(11, 17, 27);
            ForeColor = Color.FromArgb(237, 248, 250);
            Font = new Font("Segoe UI", 9F);
            Text = "WolfHub · Central de Ferramentas & Setup";
            ClientSize = new Size(980, 680);
            MinimumSize = new Size(880, 620);
            StartPosition = FormStartPosition.CenterScreen;
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

            headerPanel = new Panel { BackColor = BackColor };
            brandTitle = new Label { Text = "WolfHub", Font = new Font("Segoe UI", 17F, FontStyle.Bold), ForeColor = Color.FromArgb(237, 248, 250), TextAlign = ContentAlignment.MiddleLeft, UseMnemonic = false };
            brandSubtitle = new Label { Text = "Toolkit pessoal, instalador pós-formatação e ajustes rápidos do Windows", Font = new Font("Segoe UI", 9.25F), ForeColor = Color.FromArgb(164, 185, 198), TextAlign = ContentAlignment.MiddleLeft, UseMnemonic = false };
            openFolderButton = CreateButton("Abrir C:\\wolfhub", delegate
            {
                Directory.CreateDirectory(SystemActions.InstallRoot);
                Process.Start("explorer.exe", SystemActions.InstallRoot);
            });
            openFolderButton.Width = 148;
            tips.SetToolTip(openFolderButton, "Abre a pasta C:\\wolfhub onde os utilitários instalados ficam centralizados.");

            elevateHeaderButton = CreateButton("Modo Admin", RestartElevated);
            elevateHeaderButton.Glyph = ButtonGlyph.ShieldCheck;
            elevateHeaderButton.Width = 136;
            elevateHeaderButton.Visible = !SystemActions.IsAdministrator();
            tips.SetToolTip(elevateHeaderButton, "Reinicia o WolfHub como Administrador para desbloquear ajustes globais do sistema.");

            headerPanel.Controls.AddRange(new Control[] { brandTitle, brandSubtitle, openFolderButton, elevateHeaderButton });

            sidebarPanel = new RoundedSurfacePanel { BackColor = Color.FromArgb(16, 26, 39), BorderColor = Color.FromArgb(41, 61, 76), Radius = 12 };
            var navTitle = new Label { Text = "Módulos", Font = new Font("Segoe UI", 13F, FontStyle.Bold), ForeColor = Color.FromArgb(237, 248, 250), BackColor = sidebarPanel.BackColor, TextAlign = ContentAlignment.MiddleLeft };
            sidebarPanel.Controls.Add(navTitle);

            AddNavButton(ModuleName("toolkit", "Meu Toolkit"), "Utilitários WolfHub independentes", 0);
            AddNavButton(ModuleName("packages", "Pós-Formatação"), "Perfis e instalação seletiva por WinGet", 1);
            AddNavButton(ModuleName("windows-tweaks", "Energia & Tweaks"), "Ajustes revisáveis do Windows", 2);
            AddNavButton(ModuleName("system-backup", "Backup do Sistema"), "Estado inicial e restauração dos ajustes", 3);

            contentHost = new Panel { BackColor = BackColor };
            BuildToolkitPage();
            BuildPackagesPage();
            BuildTweaksPage();
            BuildBackupPage();
            contentHost.Controls.AddRange(new Control[] { toolkitPage, packagesPage, tweaksPage, backupPage });

            footerPanel = new RoundedSurfacePanel { BackColor = Color.FromArgb(16, 26, 39), BorderColor = Color.FromArgb(41, 61, 76), Radius = 12 };
            envLabel = new Label
            {
                Text = (SystemActions.IsAdministrator() ? "● Executando como Administrador" : "● Modo usuário padrão") + "  ·  Pasta destino: C:\\wolfhub",
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Color.FromArgb(214, 232, 242),
                BackColor = footerPanel.BackColor,
                TextAlign = ContentAlignment.MiddleLeft,
                UseMnemonic = false
            };
            statusLabel = new Label
            {
                Text = "Passe o mouse sobre qualquer ferramenta ou programa para ver a descrição detalhada.",
                Font = new Font("Segoe UI", 8.75F),
                ForeColor = Color.FromArgb(146, 168, 184),
                BackColor = footerPanel.BackColor,
                TextAlign = ContentAlignment.MiddleLeft,
                AutoEllipsis = true,
                UseMnemonic = false
            };
            footerPanel.Controls.AddRange(new Control[] { envLabel, statusLabel });

            Controls.AddRange(new Control[] { headerPanel, sidebarPanel, contentHost, footerPanel });
            Resize += delegate { LayoutMainWindow(); };
        }

        private void AddNavButton(string title, string subtitle, int tabIndex)
        {
            var btn = new NavEntryButton { Text = title, Subtitle = subtitle, Height = 52 };
            btn.Click += delegate { SwitchTab(tabIndex); };
            navButtons.Add(btn);
            sidebarPanel.Controls.Add(btn);
        }

        private void SwitchTab(int index)
        {
            activeTabIndex = index;
            for (int i = 0; i < navButtons.Count; i++)
            {
                navButtons[i].Selected = (i == index);
                navButtons[i].Invalidate();
            }
            if (toolkitPage != null) toolkitPage.Visible = (index == 0);
            if (packagesPage != null) packagesPage.Visible = (index == 1);
            if (tweaksPage != null) tweaksPage.Visible = (index == 2);
            if (backupPage != null) backupPage.Visible = (index == 3);
            if (index == 3) RefreshBackupDetails();
            LayoutMainWindow();
        }

        private string ModuleName(string id, string fallback)
        {
            ModuleInfo module = featureCatalog == null ? null : featureCatalog.GetModule(id);
            return module == null || String.IsNullOrWhiteSpace(module.Name) ? fallback : module.Name;
        }

        private string ModulePurpose(string id, string fallback)
        {
            ModuleInfo module = featureCatalog == null ? null : featureCatalog.GetModule(id);
            return module == null || String.IsNullOrWhiteSpace(module.Purpose) ? fallback : module.Purpose;
        }

        private string ModuleDependencies(string id)
        {
            ModuleInfo module = featureCatalog == null ? null : featureCatalog.GetModule(id);
            return module == null ? "" : module.Dependencies;
        }

        private IEnumerable<string> KnownPackageIds()
        {
            return packages.Select(x => x.WingetId).Where(x => !String.IsNullOrWhiteSpace(x));
        }

        private IEnumerable<string> KnownInstalledPackageIds()
        {
            return packages.Where(x => x.Installed).Select(x => x.WingetId).Where(x => !String.IsNullOrWhiteSpace(x));
        }

        private bool ConfirmFeature(string featureId, string details)
        {
            try
            {
                FeatureInfo feature = featureCatalog.GetFeature(featureId);
                var text = new StringBuilder();
                text.AppendLine(feature.Name);
                text.AppendLine();
                text.AppendLine("O que muda: " + feature.Changes);
                text.AppendLine("Dependências: " + feature.Dependencies);
                text.AppendLine("Permissões: " + feature.Permission);
                text.AppendLine("Reversão: " + feature.Reversible);
                if (!String.IsNullOrWhiteSpace(details)) text.AppendLine("\r\nDetalhes desta ação: " + details);
                if (featureId.StartsWith("tweaks.", StringComparison.OrdinalIgnoreCase) || featureId.StartsWith("packages.", StringComparison.OrdinalIgnoreCase) || featureId == "drivers.vendor-installer")
                    text.AppendLine("\r\nAntes da ação, o WolfHub criará o backup inicial se ainda não existir.");
                return MessageBox.Show(text.ToString(), "Confirmar ação · WolfHub", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes;
            }
            catch (Exception ex)
            {
                SetStatus("Ação bloqueada: não foi possível carregar sua descrição de segurança. " + ex.Message);
                return false;
            }
        }

        private void EnsureInitialBackup()
        {
            bool existed = StateBackup.HasBaseline;
            StateBackup.EnsureBaseline(KnownPackageIds(), KnownInstalledPackageIds(), wingetInventoryChecked && wingetInventoryAvailable);
            if (!existed) SetStatus("Backup inicial criado antes da alteração em " + StateBackup.BaselineFile);
        }

        private void BuildBackupPage()
        {
            backupPage = new Panel { BackColor = BackColor, Visible = false };
            backupTitle = new Label { Text = "Backup do estado inicial", Font = new Font("Segoe UI", 16F, FontStyle.Bold), ForeColor = Color.FromArgb(237, 248, 250), TextAlign = ContentAlignment.MiddleLeft, UseMnemonic = false };
            backupSubtitle = new Label { Text = "O WolfHub salva o estado antes do primeiro ajuste compatível para permitir comparação e restauração.", Font = new Font("Segoe UI", 9.25F), ForeColor = Color.FromArgb(164, 185, 198), TextAlign = ContentAlignment.MiddleLeft, AutoEllipsis = true, UseMnemonic = false };
            var panel = new RoundedSurfacePanel { BackColor = Color.FromArgb(21, 34, 49), BorderColor = Color.FromArgb(41, 61, 76), Radius = 12 };
            backupDetails = new Label { Text = "Carregando estado do backup...", Font = new Font("Segoe UI", 10F), ForeColor = Color.FromArgb(214, 232, 242), BackColor = panel.BackColor, TextAlign = ContentAlignment.TopLeft, UseMnemonic = false };
            panel.Controls.Add(backupDetails);
            createBackupButton = CreateButton("Criar backup inicial", delegate { CreateOrReplaceBaseline(); });
            createBackupButton.Primary = true;
            createBackupButton.Glyph = ButtonGlyph.Save;
            restoreBackupButton = CreateButton("Revisar e restaurar", delegate { RestoreInitialState(); });
            restoreBackupButton.Glyph = ButtonGlyph.RefreshCw;
            tips.SetToolTip(createBackupButton, "Salva o estado compatível do Windows e a lista resumida de pacotes conhecidos.");
            tips.SetToolTip(restoreBackupButton, "Compara o estado atual com o backup, cria uma cópia de segurança atual e pede confirmação antes de restaurar.");
            backupPage.Controls.AddRange(new Control[] { backupTitle, backupSubtitle, panel, createBackupButton, restoreBackupButton });
            backupPage.Tag = panel;
        }

        private void LayoutBackupPage()
        {
            if (backupPage == null) return;
            int dpi = 96; if (backupPage.IsHandleCreated) using (Graphics g = backupPage.CreateGraphics()) dpi = Math.Max(96, (int)Math.Round(g.DpiX));
            int w = backupPage.ClientSize.Width;
            backupTitle.SetBounds(0, 0, w, UiSpacing.Scale(32, dpi));
            backupSubtitle.SetBounds(0, UiSpacing.Scale(32, dpi), w, UiSpacing.Scale(34, dpi));
            var panel = backupPage.Tag as Control;
            int top = UiSpacing.Scale(78, dpi), height = UiSpacing.Scale(245, dpi), pad = UiSpacing.Scale(18, dpi);
            panel.SetBounds(0, top, w, height);
            backupDetails.SetBounds(pad, pad, Math.Max(100, panel.ClientSize.Width - pad * 2), Math.Max(100, panel.ClientSize.Height - pad * 2));
            int buttonW = UiSpacing.Scale(190, dpi), buttonH = UiSpacing.Scale(36, dpi), gap = UiSpacing.Scale(10, dpi);
            createBackupButton.SetBounds(0, top + height + gap, buttonW, buttonH);
            restoreBackupButton.SetBounds(buttonW + gap, top + height + gap, UiSpacing.Scale(205, dpi), buttonH);
        }

        private void RefreshBackupDetails()
        {
            if (backupDetails == null) return;
            try
            {
                backupDetails.Text = StateBackup.Describe(StateBackup.LoadBaseline(), KnownInstalledPackageIds(), wingetInventoryChecked && wingetInventoryAvailable);
                createBackupButton.Text = StateBackup.HasBaseline ? "Substituir backup inicial" : "Criar backup inicial";
                restoreBackupButton.Enabled = StateBackup.HasBaseline;
            }
            catch (Exception ex)
            {
                backupDetails.Text = "Não foi possível ler o backup: " + ex.Message;
                restoreBackupButton.Enabled = false;
            }
        }

        private void CreateOrReplaceBaseline()
        {
            bool replace = StateBackup.HasBaseline;
            string prompt = replace
                ? "Substituir o backup inicial atual pelo estado deste computador agora? Isso altera o ponto de referência usado pela restauração."
                : "Criar um backup do estado atual antes de continuar? O arquivo ficará em %LOCALAPPDATA%\\WolfHub\\backups e não será enviado para a rede.";
            if (MessageBox.Show(prompt, "Backup do estado inicial", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            try
            {
                StateBackup.CaptureBaseline(KnownPackageIds(), KnownInstalledPackageIds(), wingetInventoryChecked && wingetInventoryAvailable, replace);
                RefreshBackupDetails();
                SetStatus("Backup inicial salvo em " + StateBackup.BaselineFile);
            }
            catch (Exception ex) { SetStatus("Não foi possível criar o backup: " + ex.Message); }
        }

        private void RestoreInitialState()
        {
            WolfHubStateSnapshot snapshot;
            try { snapshot = StateBackup.LoadBaseline(); }
            catch (Exception ex) { SetStatus("Backup inválido: " + ex.Message); return; }
            if (snapshot == null) { SetStatus("Crie um backup inicial antes de restaurar."); return; }
            string comparison = StateBackup.Describe(snapshot, KnownInstalledPackageIds(), wingetInventoryChecked && wingetInventoryAvailable);
            string prompt = comparison + "\r\n\r\nUma cópia do estado atual será guardada antes da restauração. Restaurar os ajustes suportados?";
            if (MessageBox.Show(prompt, "Revisar restauração", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
            try
            {
                StateBackup.RestoreBaseline(KnownPackageIds(), KnownInstalledPackageIds(), wingetInventoryChecked && wingetInventoryAvailable);
                RebuildTweaksCards();
                RefreshBackupDetails();
                SetStatus("Estado compatível restaurado. O Explorer pode precisar ser reiniciado para refletir o menu de contexto.");
                MessageBox.Show("A restauração dos ajustes compatíveis terminou. Programas instalados e planos adicionais não foram removidos.", "Restauração concluída", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex) { SetStatus("Falha na restauração: " + ex.Message); MessageBox.Show(ex.Message, "Falha na restauração", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }

        private void LayoutMainWindow()
        {
            int dpi = 96;
            if (IsHandleCreated) using (Graphics g = CreateGraphics()) dpi = Math.Max(96, (int)Math.Round(g.DpiX));
            int pad = UiSpacing.Scale(16, dpi), gap = UiSpacing.Scale(14, dpi);
            int w = Math.Max(0, ClientSize.Width - pad * 2);
            int h = Math.Max(0, ClientSize.Height - pad * 2);
            int headerH = UiSpacing.Scale(58, dpi);
            int footerH = UiSpacing.Scale(58, dpi);
            int sideW = UiSpacing.Scale(216, dpi);

            headerPanel.SetBounds(pad, pad, w, headerH);
            int btnH = UiSpacing.Scale(36, dpi), btnY = (headerH - btnH) / 2;
            int right = w;
            if (elevateHeaderButton.Visible)
            {
                int ew = UiSpacing.Scale(136, dpi);
                elevateHeaderButton.SetBounds(right - ew, btnY, ew, btnH);
                right = elevateHeaderButton.Left - UiSpacing.Scale(8, dpi);
            }
            int fw = UiSpacing.Scale(148, dpi);
            openFolderButton.SetBounds(right - fw, btnY, fw, btnH);
            int brandW = Math.Max(160, openFolderButton.Left - UiSpacing.Scale(16, dpi));
            brandTitle.SetBounds(0, UiSpacing.Scale(2, dpi), brandW, UiSpacing.Scale(30, dpi));
            brandSubtitle.SetBounds(0, UiSpacing.Scale(32, dpi), brandW, UiSpacing.Scale(22, dpi));

            int bodyY = pad + headerH + UiSpacing.Scale(10, dpi);
            int bodyH = Math.Max(100, ClientSize.Height - bodyY - pad - footerH - UiSpacing.Scale(10, dpi));
            sidebarPanel.SetBounds(pad, bodyY, sideW, bodyH);
            int sidePad = UiSpacing.Scale(12, dpi);
            sidebarPanel.Controls[0].SetBounds(sidePad, sidePad, sideW - sidePad * 2, UiSpacing.Scale(28, dpi));
            int navY = sidePad + UiSpacing.Scale(36, dpi);
            foreach (var btn in navButtons)
            {
                int nh = UiSpacing.Scale(52, dpi);
                btn.SetBounds(sidePad, navY, sideW - sidePad * 2, nh);
                navY += nh + UiSpacing.Scale(8, dpi);
            }

            int contentX = sidebarPanel.Right + gap;
            int contentW = Math.Max(200, ClientSize.Width - pad - contentX);
            contentHost.SetBounds(contentX, bodyY, contentW, bodyH);
            toolkitPage.SetBounds(0, 0, contentW, bodyH);
            packagesPage.SetBounds(0, 0, contentW, bodyH);
            tweaksPage.SetBounds(0, 0, contentW, bodyH);
            backupPage.SetBounds(0, 0, contentW, bodyH);

            footerPanel.SetBounds(pad, bodyY + bodyH + UiSpacing.Scale(10, dpi), w, footerH);
            int fPadX = UiSpacing.Scale(16, dpi);
            envLabel.SetBounds(fPadX, UiSpacing.Scale(8, dpi), w - fPadX * 2, UiSpacing.Scale(20, dpi));
            statusLabel.SetBounds(fPadX, UiSpacing.Scale(29, dpi), w - fPadX * 2, UiSpacing.Scale(20, dpi));

            LayoutToolkitPage();
            LayoutPackagesPage();
            LayoutTweaksPage();
            LayoutBackupPage();
        }

        // ==================== TAB 1: MEU TOOLKIT ====================
        private Label toolkitTitle, toolkitSubtitle;
        private RoundedButton installAllToolkitButton;

        private void BuildToolkitPage()
        {
            toolkitPage = new Panel { BackColor = BackColor };
            toolkitTitle = new Label { Text = "Meu Toolkit · Instalação em C:\\wolfhub", Font = new Font("Segoe UI", 16F, FontStyle.Bold), ForeColor = Color.FromArgb(237, 248, 250), TextAlign = ContentAlignment.MiddleLeft, UseMnemonic = false };
            toolkitSubtitle = new Label { Text = "Execute suas ferramentas direto daqui ou instale em C:\\wolfhub com atalho na Área de Trabalho.", Font = new Font("Segoe UI", 9.25F), ForeColor = Color.FromArgb(164, 185, 198), TextAlign = ContentAlignment.MiddleLeft, UseMnemonic = false, AutoEllipsis = true };
            installAllToolkitButton = CreateButton("Instalar Tudo em C:\\wolfhub", InstallFullWolfHubSuite);
            installAllToolkitButton.Primary = true;
            installAllToolkitButton.Glyph = ButtonGlyph.Plus;
            tips.SetToolTip(installAllToolkitButton, "Copia o WolfHub Launcher, WolfHub Audio, Desligar Monitor e Mixer para C:\\wolfhub e cria os atalhos na Área de Trabalho.");

            toolkitViewport = new Panel { BackColor = BackColor, AutoScroll = true };
            toolkitPage.Controls.AddRange(new Control[] { toolkitTitle, toolkitSubtitle, installAllToolkitButton, toolkitViewport });
            RebuildToolkitCards();
        }

        private void RebuildToolkitCards()
        {
            if (toolkitViewport == null) return;
            toolkitViewport.SuspendLayout();
            toolkitViewport.Controls.Clear();

            string audioSource = Path.Combine(repoRoot, "audio-app", "dist", "WolfHubAudio.exe");
            if (!File.Exists(audioSource)) audioSource = SystemActions.ExtractAudioAppResource();
            string monitorSource = Path.Combine(repoRoot, "Desligar Monitor.exe");
            string mixerSource = Path.Combine(repoRoot, "MixerVolumeWindows10.exe");
            string selfSource = Application.ExecutablePath;
            string driversBat = Path.Combine(repoRoot, "DriversRafa", "Drivers B660M TUF", "Installer (Pack).bat");

            toolkitViewport.Controls.Add(CreateToolkitCard(
                "volume-2",
                ModuleName("audio-switcher", "WolfHub Audio"),
                ModulePurpose("audio-switcher", "Aplicativo independente com perfis, teste de saídas, medidor de microfone e bandeja."),
                GetAudioStatus(audioSource),
                SystemActions.IsInstalledInWolfHub("WolfHubAudio.exe"),
                "Abrir Audio Switcher",
                delegate { LaunchOrInstallTool(audioSource, "WolfHubAudio.exe", false, "WolfHub Audio"); },
                GetAudioInstallLabel(audioSource),
                delegate { LaunchOrInstallTool(audioSource, "WolfHubAudio.exe", true, "WolfHub Audio"); }
            ));

            toolkitViewport.Controls.Add(CreateToolkitCard(
                "eye-off",
                "Desligar Monitor Instantaneamente",
                "Apaga a tela dos monitores na hora via comando nativo de energia ou pelo utilitário portátil Desligar Monitor.exe, sem suspender os programas abertos.",
                SystemActions.IsInstalledInWolfHub("Desligar Monitor.exe") ? "Instalado em C:\\wolfhub" : "Utilitário portátil",
                SystemActions.IsInstalledInWolfHub("Desligar Monitor.exe"),
                "Desligar Tela Agora",
                delegate
                {
                    SetStatus("Desligando o monitor...");
                    SystemActions.TurnOffMonitor(Handle);
                },
                "Instalar (C:\\wolfhub + Atalho)",
                delegate { LaunchOrInstallTool(monitorSource, "Desligar Monitor.exe", true, "Desligar Monitor"); }
            ));

            toolkitViewport.Controls.Add(CreateToolkitCard(
                "speaker",
                "Mixer de Volume Clássico (Windows 10)",
                "Abre o mixer clássico de volume por aplicativo (MixerVolumeWindows10.exe) ou permite instalá-lo em C:\\wolfhub com atalho rápido.",
                SystemActions.IsInstalledInWolfHub("MixerVolumeWindows10.exe") ? "Instalado em C:\\wolfhub" : "Utilitário portátil",
                SystemActions.IsInstalledInWolfHub("MixerVolumeWindows10.exe"),
                "Abrir Mixer",
                delegate { LaunchOrInstallTool(mixerSource, "MixerVolumeWindows10.exe", false, "Mixer de Volume"); },
                "Instalar (C:\\wolfhub + Atalho)",
                delegate { LaunchOrInstallTool(mixerSource, "MixerVolumeWindows10.exe", true, "Mixer de Volume"); }
            ));

            toolkitViewport.Controls.Add(CreateToolkitCard(
                "settings-2",
                "WolfHub Central (Este Launcher)",
                "Instala esta central do WolfHub em C:\\wolfhub\\WolfHub.exe e cria atalho na Área de Trabalho e no Menu Iniciar.",
                SystemActions.IsInstalledInWolfHub("WolfHub.exe") ? "Instalado em C:\\wolfhub" : "Executando do projeto",
                SystemActions.IsInstalledInWolfHub("WolfHub.exe"),
                "Criar Atalho no Desktop",
                delegate { LaunchOrInstallTool(selfSource, "WolfHub.exe", true, "WolfHub"); },
                null,
                null
            ));

            if (File.Exists(driversBat))
            {
                toolkitViewport.Controls.Add(CreateToolkitCard(
                    "shield-check",
                    "Pack de Drivers ASUS B660M TUF (DriversRafa)",
                    "Pacote local de drivers chipset Intel, MEI, Serial IO, Realtek LAN 2.5GbE e Realtek Audio para formatação da placa-mãe ASUS TUF Gaming B660M-PLUS D4.",
                    "Disponível localmente",
                    true,
                    "Abrir Pasta de Drivers",
                    delegate { Process.Start("explorer.exe", Path.GetDirectoryName(driversBat)); },
                    "Executar Installer (Pack)",
                    delegate
                    {
                        if (ConfirmFeature("drivers.vendor-installer", "Placa-mãe de destino: ASUS TUF Gaming B660M-PLUS D4. Confirme o modelo e a versão do Windows antes de prosseguir."))
                        {
                            try
                            {
                                EnsureInitialBackup();
                                Process.Start(new ProcessStartInfo { FileName = driversBat, WorkingDirectory = Path.GetDirectoryName(driversBat), UseShellExecute = true });
                                SetStatus("Instalador de driver aberto. O WolfHub não valida compatibilidade nem acompanha a instalação.");
                            }
                            catch (Exception ex) { SetStatus("Não foi possível abrir o instalador: " + ex.Message); }
                        }
                    }
                ));
            }

            toolkitViewport.ResumeLayout(false);
            LayoutToolkitPage();
        }

        private Control CreateToolkitCard(string iconName, string title, string description, string badgeText, bool badgeActive, string primaryActionText, Action primaryAction, string installActionText, Action installAction)
        {
            var card = new RoundedSurfacePanel { BackColor = Color.FromArgb(21, 34, 49), BorderColor = Color.FromArgb(41, 61, 76), Radius = 12, Height = 112 };
            var titleLbl = new Label { Text = title, Font = new Font("Segoe UI", 11F, FontStyle.Bold), ForeColor = Color.FromArgb(237, 248, 250), BackColor = card.BackColor, TextAlign = ContentAlignment.MiddleLeft, AutoEllipsis = true, UseMnemonic = false };
            var descLbl = new Label { Text = description, Font = new Font("Segoe UI", 8.75F), ForeColor = Color.FromArgb(164, 185, 198), BackColor = card.BackColor, TextAlign = ContentAlignment.TopLeft, UseMnemonic = false };
            var badge = new StatusBadgeControl { Text = badgeText, IsActive = badgeActive, Font = new Font("Segoe UI", 8.25F, FontStyle.Bold) };

            var runBtn = CreateButton(primaryActionText, primaryAction);
            runBtn.Primary = true;
            runBtn.Glyph = ButtonGlyph.Play;
            tips.SetToolTip(runBtn, description);

            RoundedButton instBtn = null;
            if (!String.IsNullOrEmpty(installActionText) && installAction != null)
            {
                instBtn = CreateButton(installActionText, installAction);
                instBtn.Glyph = ButtonGlyph.Save;
                tips.SetToolTip(instBtn, "Copia o executável para C:\\wolfhub e cria um atalho na Área de Trabalho e no Menu Iniciar.");
                card.Controls.Add(instBtn);
            }

            tips.SetToolTip(titleLbl, description);
            tips.SetToolTip(descLbl, description);
            card.Controls.AddRange(new Control[] { titleLbl, descLbl, badge, runBtn });

            card.Resize += delegate
            {
                int dpi = 96; if (card.IsHandleCreated) using (Graphics g = card.CreateGraphics()) dpi = Math.Max(96, (int)Math.Round(g.DpiX));
                int pad = UiSpacing.Scale(16, dpi), w = card.ClientSize.Width, h = card.ClientSize.Height;
                int badgeW = UiSpacing.Scale(168, dpi), badgeH = UiSpacing.Scale(26, dpi);
                badge.SetBounds(w - pad - badgeW, UiSpacing.Scale(12, dpi), badgeW, badgeH);
                titleLbl.SetBounds(pad, UiSpacing.Scale(12, dpi), Math.Max(100, badge.Left - pad - 10), UiSpacing.Scale(22, dpi));
                descLbl.SetBounds(pad, UiSpacing.Scale(36, dpi), Math.Max(100, w - pad * 2), UiSpacing.Scale(34, dpi));

                int btnH = UiSpacing.Scale(32, dpi), btnY = h - pad - btnH + UiSpacing.Scale(4, dpi);
                int right = w - pad;
                if (instBtn != null)
                {
                    int iw = UiSpacing.Scale(210, dpi);
                    instBtn.SetBounds(right - iw, btnY, iw, btnH);
                    right = instBtn.Left - UiSpacing.Scale(8, dpi);
                }
                int rw = UiSpacing.Scale(176, dpi);
                runBtn.SetBounds(right - rw, btnY, rw, btnH);
            };
            return card;
        }

        private static string ReadProductVersion(string path)
        {
            if (String.IsNullOrWhiteSpace(path) || !File.Exists(path)) return "";
            try
            {
                string value = FileVersionInfo.GetVersionInfo(path).ProductVersion;
                if (String.IsNullOrWhiteSpace(value)) value = FileVersionInfo.GetVersionInfo(path).FileVersion;
                return value ?? "";
            }
            catch { return ""; }
        }

        private string GetAudioStatus(string sourcePath)
        {
            string installedPath = Path.Combine(SystemActions.InstallRoot, "WolfHubAudio.exe");
            if (!File.Exists(installedPath)) return "Pronto para abrir · módulo independente";
            string installedVersion = ReadProductVersion(installedPath);
            string sourceVersion = ReadProductVersion(sourcePath);
            Version installed, source;
            if (Version.TryParse(installedVersion, out installed) && Version.TryParse(sourceVersion, out source) && source > installed)
                return "Atualização disponível · " + installedVersion + " → " + sourceVersion;
            return "Instalado · " + (String.IsNullOrWhiteSpace(installedVersion) ? "versão não informada" : "v" + installedVersion);
        }

        private string GetAudioInstallLabel(string sourcePath)
        {
            string installedPath = Path.Combine(SystemActions.InstallRoot, "WolfHubAudio.exe");
            if (!File.Exists(installedPath)) return "Instalar módulo";
            string installedVersion = ReadProductVersion(installedPath);
            string sourceVersion = ReadProductVersion(sourcePath);
            Version installed, source;
            if (Version.TryParse(installedVersion, out installed) && Version.TryParse(sourceVersion, out source) && source > installed)
                return "Atualizar Audio";
            return "Reinstalar Audio";
        }

        private void LaunchOrInstallTool(string sourcePath, string exeName, bool install, string shortcutName)
        {
            try
            {
                string installedPath = Path.Combine(SystemActions.InstallRoot, exeName);
                string actualSource = File.Exists(sourcePath) ? sourcePath : (File.Exists(installedPath) ? installedPath : null);
                if (actualSource == null)
                {
                    MessageBox.Show("Não foi possível localizar '" + exeName + "'.", "WolfHub", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                if (install)
                {
                    string versionDetail = exeName == "WolfHubAudio.exe"
                        ? "WolfHub Audio é um módulo independente. Versão instalada: " + (ReadProductVersion(installedPath) ?? "não instalada") + "; pacote local: " + (ReadProductVersion(actualSource) ?? "indisponível") + ". As preferências em %LOCALAPPDATA%\\WolfHubAudio\\settings.xml serão preservadas."
                        : "Destino: " + installedPath + ". Os dados de usuário do aplicativo não serão apagados.";
                    if (!ConfirmFeature("toolkit.install", "Arquivo: " + exeName + ". " + versionDetail)) return;
                    string target = SystemActions.InstallToolToWolfHub(actualSource, exeName, shortcutName, shortcutName + " — instalado pelo WolfHub", true);
                    RebuildToolkitCards();
                    SetStatus(shortcutName + " instalado em " + target + " e atalho criado na Área de Trabalho!");
                }
                else
                {
                    Process.Start(new ProcessStartInfo { FileName = actualSource, WorkingDirectory = Path.GetDirectoryName(actualSource), UseShellExecute = true });
                    SetStatus(shortcutName + " iniciado.");
                }
            }
            catch (Exception ex)
            {
                SetStatus("Erro: " + ex.Message);
                MessageBox.Show(ex.Message, "WolfHub", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void InstallFullWolfHubSuite()
        {
            if (!ConfirmFeature("toolkit.install-suite", "Destino atual: " + SystemActions.InstallRoot + ". Inclui WolfHub Audio independente e os utilitários portáteis disponíveis nesta cópia.")) return;
            try
            {
                int count = 0;
                string selfPath = Application.ExecutablePath;
                SystemActions.InstallToolToWolfHub(selfPath, "WolfHub.exe", "WolfHub", "Central de Ferramentas WolfHub", true);
                count++;

                string audioPath = Path.Combine(repoRoot, "audio-app", "dist", "WolfHubAudio.exe");
                if (File.Exists(audioPath))
                {
                    SystemActions.InstallToolToWolfHub(audioPath, "WolfHubAudio.exe", "WolfHub Audio", "Gerenciador de Dispositivos de Áudio WolfHub", true);
                    count++;
                }

                string monitorPath = Path.Combine(repoRoot, "Desligar Monitor.exe");
                if (File.Exists(monitorPath))
                {
                    SystemActions.InstallToolToWolfHub(monitorPath, "Desligar Monitor.exe", "Desligar Monitor", "Desligar tela imediatamente", true);
                    count++;
                }

                string mixerPath = Path.Combine(repoRoot, "MixerVolumeWindows10.exe");
                if (File.Exists(mixerPath))
                {
                    SystemActions.InstallToolToWolfHub(mixerPath, "MixerVolumeWindows10.exe", "Mixer de Volume", "Mixer clássico do Windows 10", false);
                    count++;
                }

                RebuildToolkitCards();
                SetStatus("Pacote WolfHub instalado em C:\\wolfhub (" + count + " executáveis + atalhos na Área de Trabalho)!");
                MessageBox.Show("Instalação concluída!\n\n• Pasta criada: C:\\wolfhub\n• Executáveis copiados: " + count + "\n• Atalhos criados na Área de Trabalho e no Menu Iniciar.", "WolfHub Instalado", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                SetStatus("Erro ao instalar pacote: " + ex.Message);
                MessageBox.Show(ex.Message, "WolfHub", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void LayoutToolkitPage()
        {
            if (toolkitPage == null || toolkitViewport == null) return;
            int dpi = 96; if (toolkitPage.IsHandleCreated) using (Graphics g = toolkitPage.CreateGraphics()) dpi = Math.Max(96, (int)Math.Round(g.DpiX));
            int w = toolkitPage.ClientSize.Width, h = toolkitPage.ClientSize.Height;
            int btnW = UiSpacing.Scale(218, dpi), btnH = UiSpacing.Scale(36, dpi);
            installAllToolkitButton.SetBounds(Math.Max(0, w - btnW), UiSpacing.Scale(8, dpi), btnW, btnH);
            int textW = Math.Max(120, installAllToolkitButton.Left - UiSpacing.Scale(12, dpi));
            toolkitTitle.SetBounds(0, 0, textW, UiSpacing.Scale(32, dpi));
            toolkitSubtitle.SetBounds(0, UiSpacing.Scale(32, dpi), textW, UiSpacing.Scale(20, dpi));

            int top = UiSpacing.Scale(60, dpi);
            toolkitViewport.SetBounds(0, top, w, Math.Max(80, h - top));

            int cardH = UiSpacing.Scale(112, dpi), gap = UiSpacing.Scale(10, dpi);
            int totalH = toolkitViewport.Controls.Count * (cardH + gap);
            bool scroll = toolkitViewport.ClientSize.Height > 0 && totalH > toolkitViewport.ClientSize.Height;
            int usableW = Math.Max(200, toolkitViewport.ClientSize.Width - (scroll ? SystemInformation.VerticalScrollBarWidth + 4 : 0));
            int y = 0;
            foreach (Control c in toolkitViewport.Controls)
            {
                c.SetBounds(0, y, usableW, cardH);
                y += cardH + gap;
            }
        }

        // ==================== TAB 2: PÓS-FORMATAÇÃO (WINGET) ====================
        private Label pkgTitle, pkgSubtitle;
        private Panel pkgToolbarPanel, pkgActionStrip;

        private void BuildPackagesPage()
        {
            packagesPage = new Panel { BackColor = BackColor, Visible = false };
            pkgTitle = new Label { Text = "Instalador Pós-Formatação (WinGet)", Font = new Font("Segoe UI", 16F, FontStyle.Bold), ForeColor = Color.FromArgb(237, 248, 250), TextAlign = ContentAlignment.MiddleLeft, UseMnemonic = false };
            pkgSubtitle = new Label { Text = "Passe o mouse em cada item para ver para que ele serve (ex: PawnIO, Shizuku, Runtimes). Escolha um perfil ou marque manualmente.", Font = new Font("Segoe UI", 9F), ForeColor = Color.FromArgb(164, 185, 198), TextAlign = ContentAlignment.MiddleLeft, AutoEllipsis = true, UseMnemonic = false };

            pkgToolbarPanel = new Panel { BackColor = BackColor };
            string[] cats = new[] { "Todos", "Runtimes", "Navegadores & Comunicação", "Games", "Hardware & Diagnóstico", "Utilitários & Mídia", "Desenvolvimento" };
            foreach (string cat in cats)
            {
                string c = cat;
                string shortLabel = cat == "Navegadores & Comunicação" ? "Web & Discord" : (cat == "Hardware & Diagnóstico" ? "Hardware & Shizuku" : (cat == "Utilitários & Mídia" ? "Utilitários" : cat));
                var b = CreateButton(shortLabel, delegate { selectedCategory = c; UpdateCategoryButtons(); RebuildPackagesList(); });
                b.Tag = c;
                b.Height = 30;
                b.Primary = (c == selectedCategory);
                tips.SetToolTip(b, "Filtrar categoria: " + c);
                categoryButtons.Add(b);
                pkgToolbarPanel.Controls.Add(b);
            }

            pkgActionStrip = new RoundedSurfacePanel { BackColor = Color.FromArgb(16, 26, 39), BorderColor = Color.FromArgb(41, 61, 76), Radius = 10 };
            var presetLabel = new Label { Text = "Perfis:", Font = new Font("Segoe UI", 9F, FontStyle.Bold), ForeColor = Color.FromArgb(186, 206, 218), BackColor = pkgActionStrip.BackColor, TextAlign = ContentAlignment.MiddleLeft, AutoSize = true, UseMnemonic = false };
            pkgActionStrip.Controls.Add(presetLabel);

            foreach (var prof in profiles)
            {
                CatalogProfile p = prof;
                string shortName = p.Id == "personal" ? "PC Completo" : (p.Id == "support" ? "Diagnóstico" : "Essencial");
                var pb = CreateButton(shortName, delegate { ApplyPackageProfile(p); });
                pb.Height = 30;
                pb.Width = 104;
                tips.SetToolTip(pb, p.Name + ": " + p.Description);
                pkgActionStrip.Controls.Add(pb);
            }

            var clearBtn = CreateButton("Limpar", delegate
            {
                foreach (var p in packages) p.Selected = false;
                RebuildPackagesList();
            });
            clearBtn.Height = 30; clearBtn.Width = 68;
            pkgActionStrip.Controls.Add(clearBtn);

            packageSummaryLabel = new Label { Text = "0 marcados", Font = new Font("Segoe UI", 8.75F, FontStyle.Bold), ForeColor = Color.FromArgb(85, 216, 230), BackColor = pkgActionStrip.BackColor, TextAlign = ContentAlignment.MiddleRight, UseMnemonic = false, AutoEllipsis = true };
            installSelectedButton = CreateButton("Instalar Marcados", InstallSelectedPackages);
            installSelectedButton.Primary = true;
            installSelectedButton.Glyph = ButtonGlyph.Play;
            installSelectedButton.Height = 32;
            installSelectedButton.Width = 152;
            tips.SetToolTip(installSelectedButton, "Executa o WinGet para instalar todos os programas marcados.");

            upgradeAllButton = CreateButton("Atualizar Tudo", RunWingetUpgradeAll);
            upgradeAllButton.Glyph = ButtonGlyph.RefreshCw;
            upgradeAllButton.Height = 32;
            upgradeAllButton.Width = 126;
            tips.SetToolTip(upgradeAllButton, "Executa 'winget upgrade --all' em uma janela do PowerShell para atualizar todos os programas do PC.");

            pkgActionStrip.Controls.AddRange(new Control[] { packageSummaryLabel, upgradeAllButton, installSelectedButton });

            packagesViewport = new Panel { BackColor = BackColor, AutoScroll = true };
            packagesPage.Controls.AddRange(new Control[] { pkgTitle, pkgSubtitle, pkgToolbarPanel, pkgActionStrip, packagesViewport });
            RebuildPackagesList();
        }

        private void UpdateCategoryButtons()
        {
            foreach (var b in categoryButtons)
                b.Primary = String.Equals(b.Tag as string, selectedCategory, StringComparison.Ordinal);
        }

        private void ApplyPackageProfile(CatalogProfile profile)
        {
            foreach (var pkg in packages)
                pkg.Selected = profile.Packages.Contains(pkg.Id) && !pkg.Installed;
            selectedCategory = "Todos";
            UpdateCategoryButtons();
            RebuildPackagesList();
            SetStatus("Perfil '" + profile.Name + "' selecionado (" + packages.Count(x => x.Selected) + " pacotes prontos para instalar).");
        }

        private void RebuildPackagesList()
        {
            if (packagesViewport == null) return;
            packagesViewport.SuspendLayout();
            packagesViewport.Controls.Clear();

            var filtered = packages.Where(p => selectedCategory == "Todos" || String.Equals(p.Category, selectedCategory, StringComparison.OrdinalIgnoreCase)).ToList();
            foreach (var pkg in filtered)
            {
                packagesViewport.Controls.Add(CreatePackageRow(pkg));
            }
            UpdatePackageSummary();
            packagesViewport.ResumeLayout(false);
            LayoutPackagesPage();
        }

        private Control CreatePackageRow(CatalogPackage pkg)
        {
            var row = new RoundedSurfacePanel
            {
                BackColor = pkg.Selected ? Color.FromArgb(22, 43, 58) : Color.FromArgb(21, 34, 49),
                BorderColor = pkg.Selected ? Color.FromArgb(57, 197, 214) : Color.FromArgb(41, 61, 76),
                Radius = 10,
                Height = 64,
                Cursor = Cursors.Hand
            };

            var check = new CheckBox
            {
                Checked = pkg.Selected,
                AutoSize = true,
                BackColor = row.BackColor,
                ForeColor = Color.FromArgb(237, 248, 250)
            };

            var nameLbl = new Label
            {
                Text = pkg.Name + "   ·   " + pkg.Category,
                Font = new Font("Segoe UI", 9.75F, FontStyle.Bold),
                ForeColor = Color.FromArgb(237, 248, 250),
                BackColor = row.BackColor,
                TextAlign = ContentAlignment.MiddleLeft,
                AutoEllipsis = true,
                Cursor = Cursors.Hand,
                UseMnemonic = false
            };

            var descLbl = new Label
            {
                Text = pkg.Description + "  (" + pkg.WingetId + ")",
                Font = new Font("Segoe UI", 8.5F),
                ForeColor = Color.FromArgb(154, 178, 194),
                BackColor = row.BackColor,
                TextAlign = ContentAlignment.MiddleLeft,
                AutoEllipsis = true,
                Cursor = Cursors.Hand,
                UseMnemonic = false
            };

            var badge = new StatusBadgeControl
            {
                Text = pkg.Installed ? "Instalado" : (pkg.Selected ? "Marcado" : "Disponível"),
                IsActive = pkg.Installed || pkg.Selected,
                Font = new Font("Segoe UI", 8F, FontStyle.Bold)
            };

            string tooltipText = pkg.Name + " (" + pkg.WingetId + ")\n" + pkg.Description;
            tips.SetToolTip(row, tooltipText);
            tips.SetToolTip(nameLbl, tooltipText);
            tips.SetToolTip(descLbl, tooltipText);
            tips.SetToolTip(badge, tooltipText);

            Action toggle = delegate
            {
                pkg.Selected = !pkg.Selected;
                check.Checked = pkg.Selected;
                row.BackColor = pkg.Selected ? Color.FromArgb(22, 43, 58) : Color.FromArgb(21, 34, 49);
                row.BorderColor = pkg.Selected ? Color.FromArgb(57, 197, 214) : Color.FromArgb(41, 61, 76);
                check.BackColor = row.BackColor;
                nameLbl.BackColor = row.BackColor;
                descLbl.BackColor = row.BackColor;
                badge.Text = pkg.Installed ? "Instalado" : (pkg.Selected ? "Marcado" : "Disponível");
                badge.IsActive = pkg.Installed || pkg.Selected;
                row.Invalidate();
                UpdatePackageSummary();
                SetStatus(pkg.Name + ": " + pkg.Description);
            };

            check.Click += delegate { toggle(); };
            row.Click += delegate { toggle(); };
            nameLbl.Click += delegate { toggle(); };
            descLbl.Click += delegate { toggle(); };

            row.MouseEnter += delegate { SetStatus(pkg.Name + " — " + pkg.Description); };
            nameLbl.MouseEnter += delegate { SetStatus(pkg.Name + " — " + pkg.Description); };
            descLbl.MouseEnter += delegate { SetStatus(pkg.Name + " — " + pkg.Description); };

            row.Controls.AddRange(new Control[] { check, nameLbl, descLbl, badge });
            row.Resize += delegate
            {
                int dpi = 96; if (row.IsHandleCreated) using (Graphics g = row.CreateGraphics()) dpi = Math.Max(96, (int)Math.Round(g.DpiX));
                int pad = UiSpacing.Scale(14, dpi), w = row.ClientSize.Width, h = row.ClientSize.Height;
                check.Location = new Point(pad, (h - check.Height) / 2);
                int badgeW = UiSpacing.Scale(106, dpi), badgeH = UiSpacing.Scale(26, dpi);
                badge.SetBounds(w - pad - badgeW, (h - badgeH) / 2, badgeW, badgeH);
                int textX = pad + UiSpacing.Scale(26, dpi);
                int textW = Math.Max(100, badge.Left - textX - UiSpacing.Scale(10, dpi));
                nameLbl.SetBounds(textX, UiSpacing.Scale(10, dpi), textW, UiSpacing.Scale(22, dpi));
                descLbl.SetBounds(textX, UiSpacing.Scale(32, dpi), textW, UiSpacing.Scale(20, dpi));
            };
            return row;
        }

        private void UpdatePackageSummary()
        {
            int sel = packages.Count(x => x.Selected);
            int inst = packages.Count(x => x.Installed);
            if (packageSummaryLabel != null)
                packageSummaryLabel.Text = sel + " marcado(s)  ·  " + inst + " já instalado(s)";
            if (installSelectedButton != null)
                installSelectedButton.Enabled = sel > 0;
        }

        private void RefreshWingetInstalledAsync()
        {
            if (checkingWinget) return;
            checkingWinget = true;
            ThreadPool.QueueUserWorkItem(delegate
            {
                string output = "";
                bool available = false;
                try
                {
                    output = SystemActions.RunProcessCapture("winget", "list --disable-interactivity");
                    available = true;
                }
                catch { }
                finally
                {
                    checkingWinget = false;
                    if (IsHandleCreated && !IsDisposed)
                    {
                        try
                        {
                            BeginInvoke((MethodInvoker)delegate
                            {
                                wingetInventoryChecked = true;
                                wingetInventoryAvailable = available;
                                foreach (var pkg in packages)
                                    pkg.Installed = available && !String.IsNullOrEmpty(output) && output.IndexOf(pkg.WingetId, StringComparison.OrdinalIgnoreCase) >= 0;
                                RebuildPackagesList();
                                RefreshBackupDetails();
                            });
                        }
                        catch { }
                    }
                }
            });
        }

        private void InstallSelectedPackages()
        {
            if (!wingetInventoryChecked) { SetStatus("Aguarde a verificação do WinGet antes de instalar."); return; }
            if (!wingetInventoryAvailable) { SetStatus("WinGet não está disponível. Nenhum pacote foi iniciado."); return; }
            var selected = packages.Where(x => x.Selected).ToList();
            if (selected.Count == 0) return;

            var sb = new StringBuilder();
            sb.AppendLine("Pacotes selecionados para instalação via WinGet:\n");
            foreach (var p in selected.Take(12)) sb.AppendLine("• " + p.Name + " (" + p.WingetId + ")");
            if (selected.Count > 12) sb.AppendLine("... e mais " + (selected.Count - 12) + " pacote(s).");

            if (!ConfirmFeature("packages.install", sb.ToString())) return;
            try { EnsureInitialBackup(); }
            catch (Exception ex) { SetStatus("Instalação cancelada: não foi possível criar o backup inicial. " + ex.Message); return; }

            var cmdBuilder = new StringBuilder();
            cmdBuilder.Append("$Host.UI.RawUI.WindowTitle = 'WolfHub - Instalador WinGet'; ");
            int idx = 0;
            foreach (var p in selected)
            {
                idx++;
                cmdBuilder.AppendFormat("$packageName = '{0}'; $packageId = '{1}'; ", EscapePowerShellLiteral(p.Name), EscapePowerShellLiteral(p.WingetId));
                cmdBuilder.AppendFormat("Write-Host ('[{0}/{1}] Instalando ' + $packageName + ' (' + $packageId + ')...') -ForegroundColor Cyan; ", idx, selected.Count);
                cmdBuilder.Append("winget install --exact --id $packageId --accept-package-agreements --accept-source-agreements --disable-interactivity; ");
            }
            cmdBuilder.Append("Write-Host \"`nConcluído! Pressione Enter para fechar.\" -ForegroundColor Green; Read-Host");

            string encodedCommand = Convert.ToBase64String(Encoding.Unicode.GetBytes(cmdBuilder.ToString()));
            Process.Start(new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = "-NoProfile -ExecutionPolicy Bypass -EncodedCommand " + encodedCommand,
                UseShellExecute = true
            });
            SetStatus("Instalação de " + selected.Count + " pacote(s) iniciada no PowerShell.");
        }

        private void RunWingetUpgradeAll()
        {
            if (!wingetInventoryChecked) { SetStatus("Aguarde a verificação do WinGet antes de atualizar."); return; }
            if (!wingetInventoryAvailable) { SetStatus("WinGet não está disponível. Nenhuma atualização foi iniciada."); return; }
            if (!ConfirmFeature("packages.upgrade-all", "O WinGet poderá atualizar qualquer aplicativo para o qual encontre uma versão mais nova.")) return;
            try { EnsureInitialBackup(); }
            catch (Exception ex) { SetStatus("Atualização cancelada: não foi possível criar o backup inicial. " + ex.Message); return; }
            Process.Start(new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = "-NoProfile -ExecutionPolicy Bypass -Command \"$Host.UI.RawUI.WindowTitle = 'WolfHub - Atualizar Programas'; Write-Host 'Atualizando todos os pacotes via WinGet...' -ForegroundColor Cyan; winget upgrade --all --accept-package-agreements --accept-source-agreements; Write-Host '`nConcluído!' -ForegroundColor Green; Read-Host 'Pressione Enter para fechar'\"",
                UseShellExecute = true
            });
            SetStatus("Atualização global do WinGet iniciada.");
        }

        private void LayoutPackagesPage()
        {
            if (packagesPage == null || packagesViewport == null) return;
            int dpi = 96; if (packagesPage.IsHandleCreated) using (Graphics g = packagesPage.CreateGraphics()) dpi = Math.Max(96, (int)Math.Round(g.DpiX));
            int w = packagesPage.ClientSize.Width, h = packagesPage.ClientSize.Height;
            pkgTitle.SetBounds(0, 0, w, UiSpacing.Scale(32, dpi));
            pkgSubtitle.SetBounds(0, UiSpacing.Scale(32, dpi), w, UiSpacing.Scale(20, dpi));

            pkgToolbarPanel.SetBounds(0, UiSpacing.Scale(56, dpi), w, UiSpacing.Scale(34, dpi));
            int cx = 0, cGap = UiSpacing.Scale(6, dpi), ch = UiSpacing.Scale(30, dpi);
            foreach (var b in categoryButtons)
            {
                int bw = TextRenderer.MeasureText(b.Text, b.Font, new Size(int.MaxValue, int.MaxValue), TextFormatFlags.NoPrefix).Width + UiSpacing.Scale(20, dpi);
                b.SetBounds(cx, 2, bw, ch);
                cx += bw + cGap;
            }

            int stripY = UiSpacing.Scale(96, dpi), stripH = UiSpacing.Scale(46, dpi);
            pkgActionStrip.SetBounds(0, stripY, w, stripH);
            int sx = UiSpacing.Scale(10, dpi);
            var presetLbl = pkgActionStrip.Controls[0];
            presetLbl.Location = new Point(sx, (stripH - presetLbl.Height) / 2);
            sx = presetLbl.Right + UiSpacing.Scale(6, dpi);
            for (int i = 1; i <= profiles.Count + 1 && i < pkgActionStrip.Controls.Count - 3; i++)
            {
                Control btn = pkgActionStrip.Controls[i];
                int bw = UiSpacing.Scale(btn.Text == "Limpar" ? 66 : 98, dpi);
                int bh = UiSpacing.Scale(30, dpi);
                btn.SetBounds(sx, (stripH - bh) / 2, bw, bh);
                sx += bw + UiSpacing.Scale(5, dpi);
            }

            int instW = UiSpacing.Scale(148, dpi), upgW = UiSpacing.Scale(122, dpi), actH = UiSpacing.Scale(32, dpi);
            installSelectedButton.SetBounds(w - UiSpacing.Scale(10, dpi) - instW, (stripH - actH) / 2, instW, actH);
            upgradeAllButton.SetBounds(installSelectedButton.Left - UiSpacing.Scale(6, dpi) - upgW, (stripH - actH) / 2, upgW, actH);
            packageSummaryLabel.SetBounds(sx, 0, Math.Max(0, upgradeAllButton.Left - sx - 8), stripH);

            int listTop = stripY + stripH + UiSpacing.Scale(10, dpi);
            packagesViewport.SetBounds(0, listTop, w, Math.Max(80, h - listTop));

            int rowH = UiSpacing.Scale(64, dpi), gap = UiSpacing.Scale(8, dpi);
            int totalH = packagesViewport.Controls.Count * (rowH + gap);
            bool scroll = packagesViewport.ClientSize.Height > 0 && totalH > packagesViewport.ClientSize.Height;
            int usableW = Math.Max(200, packagesViewport.ClientSize.Width - (scroll ? SystemInformation.VerticalScrollBarWidth + 4 : 0));
            int y = 0;
            foreach (Control c in packagesViewport.Controls)
            {
                c.SetBounds(0, y, usableW, rowH);
                y += rowH + gap;
            }
        }

        // ==================== TAB 3: ENERGIA & TWEAKS ====================
        private Label tweaksTitle, tweaksSubtitle;

        private void BuildTweaksPage()
        {
            tweaksPage = new Panel { BackColor = BackColor, Visible = false };
            tweaksTitle = new Label { Text = "Planos de Energia (Desktop & Notebook) e Ajustes do Windows", Font = new Font("Segoe UI", 16F, FontStyle.Bold), ForeColor = Color.FromArgb(237, 248, 250), TextAlign = ContentAlignment.MiddleLeft, UseMnemonic = false };
            tweaksSubtitle = new Label { Text = "Alterne entre modos de energia para jogos/trabalho ou economia em notebook, e aplique ajustes práticos no Windows.", Font = new Font("Segoe UI", 9F), ForeColor = Color.FromArgb(164, 185, 198), TextAlign = ContentAlignment.MiddleLeft, AutoEllipsis = true, UseMnemonic = false };
            tweaksViewport = new Panel { BackColor = BackColor, AutoScroll = true };
            tweaksPage.Controls.AddRange(new Control[] { tweaksTitle, tweaksSubtitle, tweaksViewport });
            RebuildTweaksCards();
        }

        private void RebuildTweaksCards()
        {
            if (tweaksViewport == null) return;
            tweaksViewport.SuspendLayout();
            tweaksViewport.Controls.Clear();

            string activeScheme = "";
            try { activeScheme = SystemActions.RunProcessCapture("powercfg", "/getactivescheme"); } catch { }

            bool isUltimate = activeScheme.IndexOf("e9a42b02-d5df-448d-aa00-03f14749eb61", StringComparison.OrdinalIgnoreCase) >= 0 || activeScheme.IndexOf("Desempenho", StringComparison.OrdinalIgnoreCase) >= 0 && activeScheme.IndexOf("Máximo", StringComparison.OrdinalIgnoreCase) >= 0;
            bool isHigh = activeScheme.IndexOf("8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c", StringComparison.OrdinalIgnoreCase) >= 0;
            bool isBalanced = activeScheme.IndexOf("381b4222-f694-41f0-9685-ff5bb260df2e", StringComparison.OrdinalIgnoreCase) >= 0;
            bool isSaver = activeScheme.IndexOf("a1841308-3541-4fab-bc81-f71556f20b4a", StringComparison.OrdinalIgnoreCase) >= 0;

            tweaksViewport.Controls.Add(CreateTweakCard(
                "Energia · Desempenho Máximo (Desktop & Games)",
                "Desbloqueia e ativa o plano oculto 'Desempenho Máximo' (Ultimate Performance - GUID e9a42b02-d5df-448d-aa00-03f14749eb61). Elimina micro-latências de frequência da CPU para jogos e workstation.",
                isUltimate ? "Ativo no Windows" : "Disponível",
                isUltimate,
                "Ativar Desempenho Máximo",
                delegate { ApplyPowerPlan("e9a42b02-d5df-448d-aa00-03f14749eb61", "Desempenho Máximo (Desktop)"); }
            ));

            tweaksViewport.Controls.Add(CreateTweakCard(
                "Energia · Energia Máxima em Notebook / Alto Desempenho",
                "Executa exatamente o seu script 'ENERGIA MAXIMA EM NOTEBOOK': duplica e ativa o plano de Alto Desempenho (GUID 8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c) para evitar throttling na tomada.",
                isHigh ? "Ativo no Windows" : "Disponível",
                isHigh,
                "Ativar Alto Desempenho",
                delegate { ApplyPowerPlan("8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c", "Alto Desempenho"); }
            ));

            tweaksViewport.Controls.Add(CreateTweakCard(
                "Energia · Equilibrado (Padrão Recomendado) / Economia",
                "Restaura o plano Equilibrado padrão do Windows (381b4222-f694-41f0-9685-ff5bb260df2e) para uso diário silencioso ou ativa o modo Economia de Bateria.",
                isBalanced ? "Equilibrado Ativo" : (isSaver ? "Economia Ativa" : "Disponível"),
                isBalanced || isSaver,
                "Ativar Equilibrado",
                delegate { ApplyPowerPlan("381b4222-f694-41f0-9685-ff5bb260df2e", "Equilibrado"); },
                "Economia de Bateria",
                delegate { ApplyPowerPlan("a1841308-3541-4fab-bc81-f71556f20b4a", "Economia de Energia"); }
            ));

            bool showExt = GetExplorerDword("HideFileExt", 1) == 0;
            bool showHidden = GetExplorerDword("Hidden", 2) == 1;
            tweaksViewport.Controls.Add(CreateTweakCard(
                "Windows Explorer · Mostrar Extensões (.exe, .dll) e Arquivos Ocultos",
                "Configura o Explorer para sempre exibir a extensão real dos arquivos e pastas ocultas — essencial para segurança, programação e suporte técnico.",
                showExt && showHidden ? "Ativado no Explorer" : "Padrão Oculto",
                showExt && showHidden,
                showExt && showHidden ? "Restaurar Padrão" : "Mostrar Extensões e Ocultos",
                delegate { ToggleExplorerExtensions(!(showExt && showHidden)); }
            ));

            bool classicMenu = IsClassicContextMenuEnabled();
            tweaksViewport.Controls.Add(CreateTweakCard(
                "Windows 11 · Menu de Contexto Clássico (Botão Direito Completo)",
                "Restaura o menu de botão direito completo do Windows 10 no Windows 11 (sem precisar clicar em 'Mostrar mais opções' para ver o WinRAR, 7-Zip, Git, etc.).",
                classicMenu ? "Menu Clássico Ativo" : "Menu Win11 Padrão",
                classicMenu,
                classicMenu ? "Usar Menu Padrão Win11" : "Ativar Menu Clássico",
                delegate { ToggleClassicContextMenu(!classicMenu); }
            ));

            tweaksViewport.Controls.Add(CreateTweakCard(
                "Rede & Manutenção · Limpar Cache DNS + Resetar Rede",
                "Executa 'ipconfig /flushdns' para limpar cache de nomes travados e abre diagnóstico rápido de conectividade.",
                "Ação imediata",
                false,
                "Limpar Cache DNS",
                delegate
                {
                    if (!ConfirmFeature("tweaks.flush-dns", "O cache será apagado agora; o Windows o recria automaticamente durante o uso.")) return;
                    try
                    {
                        SystemActions.RunProcessCapture("ipconfig", "/flushdns");
                        SetStatus("Cache DNS do Windows limpo com sucesso (ipconfig /flushdns).");
                        MessageBox.Show("Cache de resolução DNS limpo com sucesso!", "Rede WolfHub", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    catch (Exception ex) { SetStatus("Erro: " + ex.Message); }
                },
                "Painel de Energia Clássico",
                delegate { Process.Start("control.exe", "powercfg.cpl"); }
            ));

            tweaksViewport.ResumeLayout(false);
            LayoutTweaksPage();
        }

        private Control CreateTweakCard(string title, string description, string badgeText, bool badgeActive, string primaryBtnText, Action primaryAction, string secondaryBtnText = null, Action secondaryAction = null)
        {
            var card = new RoundedSurfacePanel { BackColor = Color.FromArgb(21, 34, 49), BorderColor = Color.FromArgb(41, 61, 76), Radius = 12, Height = 104 };
            var titleLbl = new Label { Text = title, Font = new Font("Segoe UI", 10.5F, FontStyle.Bold), ForeColor = Color.FromArgb(237, 248, 250), BackColor = card.BackColor, TextAlign = ContentAlignment.MiddleLeft, AutoEllipsis = true, UseMnemonic = false };
            var descLbl = new Label { Text = description, Font = new Font("Segoe UI", 8.5F), ForeColor = Color.FromArgb(156, 178, 194), BackColor = card.BackColor, TextAlign = ContentAlignment.TopLeft, UseMnemonic = false };
            var badge = new StatusBadgeControl { Text = badgeText, IsActive = badgeActive, Font = new Font("Segoe UI", 8F, FontStyle.Bold) };

            var btn1 = CreateButton(primaryBtnText, primaryAction);
            btn1.Primary = !badgeActive;
            tips.SetToolTip(btn1, description);

            RoundedButton btn2 = null;
            if (!String.IsNullOrEmpty(secondaryBtnText) && secondaryAction != null)
            {
                btn2 = CreateButton(secondaryBtnText, secondaryAction);
                card.Controls.Add(btn2);
            }

            tips.SetToolTip(titleLbl, description);
            tips.SetToolTip(descLbl, description);
            card.Controls.AddRange(new Control[] { titleLbl, descLbl, badge, btn1 });

            card.Resize += delegate
            {
                int dpi = 96; if (card.IsHandleCreated) using (Graphics g = card.CreateGraphics()) dpi = Math.Max(96, (int)Math.Round(g.DpiX));
                int pad = UiSpacing.Scale(14, dpi), w = card.ClientSize.Width, h = card.ClientSize.Height;
                int badgeW = UiSpacing.Scale(158, dpi), badgeH = UiSpacing.Scale(25, dpi);
                badge.SetBounds(w - pad - badgeW, UiSpacing.Scale(10, dpi), badgeW, badgeH);
                titleLbl.SetBounds(pad, UiSpacing.Scale(10, dpi), Math.Max(100, badge.Left - pad - 10), UiSpacing.Scale(22, dpi));
                descLbl.SetBounds(pad, UiSpacing.Scale(33, dpi), Math.Max(100, w - pad * 2), UiSpacing.Scale(32, dpi));

                int bh = UiSpacing.Scale(30, dpi), by = h - pad - bh + UiSpacing.Scale(4, dpi);
                int right = w - pad;
                int b1w = UiSpacing.Scale(210, dpi);
                btn1.SetBounds(right - b1w, by, b1w, bh);
                right = btn1.Left - UiSpacing.Scale(8, dpi);
                if (btn2 != null)
                {
                    int b2w = UiSpacing.Scale(180, dpi);
                    btn2.SetBounds(right - b2w, by, b2w, bh);
                }
            };
            return card;
        }

        private void ApplyPowerPlan(string guid, string friendlyName)
        {
            if (!ConfirmFeature("tweaks.power-plan", "Plano escolhido: " + friendlyName + " (" + guid + ").")) return;
            try
            {
                EnsureInitialBackup();
                SystemActions.RunProcessCapture("powercfg", "-duplicatescheme " + guid);
                SystemActions.RunProcessCapture("powercfg", "/S " + guid);
                RebuildTweaksCards();
                SetStatus("Plano de energia '" + friendlyName + "' ativado no Windows!");
            }
            catch (Exception ex)
            {
                SetStatus("Não foi possível ativar o plano de energia: " + ex.Message);
            }
        }

        private static int GetExplorerDword(string name, int defaultValue)
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", false))
                {
                    if (key != null)
                    {
                        object v = key.GetValue(name);
                        if (v is int) return (int)v;
                    }
                }
            }
            catch { }
            return defaultValue;
        }

        private void ToggleExplorerExtensions(bool enable)
        {
            if (!ConfirmFeature("tweaks.explorer", enable ? "Mostrar extensões e arquivos/pastas ocultos no Explorador." : "Restaurar os valores padrão de extensões e itens ocultos.")) return;
            try
            {
                EnsureInitialBackup();
                using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", true))
                {
                    if (key != null)
                    {
                        key.SetValue("HideFileExt", enable ? 0 : 1, RegistryValueKind.DWord);
                        key.SetValue("Hidden", enable ? 1 : 2, RegistryValueKind.DWord);
                    }
                }
                RebuildTweaksCards();
                SetStatus(enable ? "Extensões de arquivo e itens ocultos ativados no Windows Explorer." : "Configuração padrão do Explorer restaurada.");
            }
            catch (Exception ex) { SetStatus("Erro ao atualizar registro: " + ex.Message); }
        }

        private static bool IsClassicContextMenuEnabled()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Classes\CLSID\{86ca1aa0-34aa-4e8b-a509-50c905bae2a2}\InprocServer32", false))
                {
                    return key != null;
                }
            }
            catch { return false; }
        }

        private void ToggleClassicContextMenu(bool enable)
        {
            if (!ConfirmFeature("tweaks.context-menu", enable ? "Ativar o menu clássico do Windows 11 para o usuário atual." : "Remover a configuração do menu clássico para o usuário atual.")) return;
            try
            {
                EnsureInitialBackup();
                string path = @"Software\Classes\CLSID\{86ca1aa0-34aa-4e8b-a509-50c905bae2a2}";
                if (enable)
                {
                    using (var key = Registry.CurrentUser.CreateSubKey(path + @"\InprocServer32"))
                    {
                        if (key != null) key.SetValue("", "", RegistryValueKind.String);
                    }
                }
                else
                {
                    Registry.CurrentUser.DeleteSubKeyTree(path, false);
                }
                RebuildTweaksCards();
                SetStatus(enable ? "Menu de contexto clássico ativado (reinicie o Explorer ou faça logoff para ver)." : "Menu padrão do Windows 11 restaurado.");
            }
            catch (Exception ex) { SetStatus("Erro ao alterar menu de contexto: " + ex.Message); }
        }

        private void LayoutTweaksPage()
        {
            if (tweaksPage == null || tweaksViewport == null) return;
            int dpi = 96; if (tweaksPage.IsHandleCreated) using (Graphics g = tweaksPage.CreateGraphics()) dpi = Math.Max(96, (int)Math.Round(g.DpiX));
            int w = tweaksPage.ClientSize.Width, h = tweaksPage.ClientSize.Height;
            tweaksTitle.SetBounds(0, 0, w, UiSpacing.Scale(32, dpi));
            tweaksSubtitle.SetBounds(0, UiSpacing.Scale(32, dpi), w, UiSpacing.Scale(20, dpi));

            int top = UiSpacing.Scale(58, dpi);
            tweaksViewport.SetBounds(0, top, w, Math.Max(80, h - top));

            int cardH = UiSpacing.Scale(104, dpi), gap = UiSpacing.Scale(10, dpi);
            int totalH = tweaksViewport.Controls.Count * (cardH + gap);
            bool scroll = tweaksViewport.ClientSize.Height > 0 && totalH > tweaksViewport.ClientSize.Height;
            int usableW = Math.Max(200, tweaksViewport.ClientSize.Width - (scroll ? SystemInformation.VerticalScrollBarWidth + 4 : 0));
            int y = 0;
            foreach (Control c in tweaksViewport.Controls)
            {
                c.SetBounds(0, y, usableW, cardH);
                y += cardH + gap;
            }
        }

        private RoundedButton CreateButton(string text, Action action)
        {
            var b = new RoundedButton
            {
                Text = text,
                AutoSize = false,
                Height = 34,
                Width = 120,
                Font = new Font("Segoe UI", 9F, FontStyle.Regular),
                ForeColor = Color.FromArgb(237, 248, 250),
                BackColor = Color.FromArgb(28, 45, 61)
            };
            if (action != null) b.Click += delegate { action(); };
            return b;
        }

        private void SetStatus(string text)
        {
            if (statusLabel != null) statusLabel.Text = text;
        }

        private void RestartElevated()
        {
            if (SystemActions.IsAdministrator()) return;
            try
            {
                var psi = new ProcessStartInfo(Application.ExecutablePath) { Verb = "runas", UseShellExecute = true };
                Process.Start(psi);
                Close();
            }
            catch { SetStatus("Elevação como administrador cancelada."); }
        }
    }

    internal static class Program
    {
        private static string GetJsonString(Dictionary<string, object> row, string key)
        {
            object value;
            return row != null && row.TryGetValue(key, out value) && value != null ? value.ToString() : "";
        }

        [STAThread]
        private static void Main(string[] args)
        {
            if (args.Contains("--self-test"))
            {
                string root = SystemActions.ResolveRepoRoot();
                string packageJson = SystemActions.ReadRepoText(root, "config/packages.json", "WolfHub.Config.packages.json");
                string featureJson = SystemActions.ReadRepoText(root, "config/features.json", "WolfHub.Config.features.json");
                string modulesJson = SystemActions.ReadRepoText(root, "config/modules.json", "WolfHub.Config.modules.json");
                FeatureCatalog catalog = FeatureCatalog.Load(featureJson, modulesJson);
                foreach (string required in new[] { "packages.install", "packages.upgrade-all", "toolkit.install", "drivers.vendor-installer", "tweaks.power-plan", "tweaks.explorer", "tweaks.context-menu" }) catalog.GetFeature(required);
                if (catalog.GetModule("audio-switcher") == null) throw new InvalidOperationException("audio-switcher module metadata is missing.");
                using (Stream audioResource = Assembly.GetExecutingAssembly().GetManifestResourceStream("WolfHub.Audio.WolfHubAudio.exe"))
                    if (audioResource == null || audioResource.Length == 0) throw new InvalidOperationException("The standalone WolfHub Audio module was not packaged.");
                var serializer = new JavaScriptSerializer();
                var rootObject = serializer.Deserialize<Dictionary<string, object>>(packageJson);
                var packageRows = rootObject != null && rootObject.ContainsKey("packages") ? rootObject["packages"] as System.Collections.ArrayList : null;
                if (packageRows == null || packageRows.Count == 0) throw new InvalidOperationException("Package catalog is empty.");
                var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (Dictionary<string, object> row in packageRows) ids.Add(GetJsonString(row, "id"));
                foreach (string profileId in new[] { "minimal", "personal", "support" })
                {
                    string json = SystemActions.ReadRepoText(root, "profiles/" + profileId + ".json", "WolfHub.Profiles." + profileId + ".json");
                    var profile = serializer.Deserialize<Dictionary<string, object>>(json);
                    var refs = profile != null && profile.ContainsKey("packages") ? profile["packages"] as System.Collections.ArrayList : null;
                    if (refs == null) throw new InvalidOperationException("Profile has no package list: " + profileId);
                    foreach (object id in refs) if (id == null || !ids.Contains(id.ToString())) throw new InvalidOperationException("Unknown package in profile " + profileId + ": " + id);
                }
                var sample = new WolfHubStateSnapshot { CapturedAtUtc = "2026-01-01T00:00:00.0000000Z", ActivePowerSchemeGuid = "381b4222-f694-41f0-9685-ff5bb260df2e", KnownInstalledPackages = new List<string> { "Google.Chrome" } };
                var roundTrip = serializer.Deserialize<WolfHubStateSnapshot>(serializer.Serialize(sample));
                if (roundTrip == null || roundTrip.Version != 1 || roundTrip.KnownInstalledPackages.Count != 1) throw new InvalidOperationException("Backup schema round-trip failed.");
                Console.WriteLine("WolfHub self-test OK: modules, action manifest, profiles, catalog and backup schema; root: " + root);
                return;
            }
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm(args));
        }
    }
}
