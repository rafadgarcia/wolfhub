using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Win32;
using System.Web.Script.Serialization;

namespace WolfHub
{
    internal sealed class WolfHubStateSnapshot
    {
        public int Version = 1;
        public string CapturedAtUtc;
        public string ActivePowerSchemeGuid;
        public bool HideFileExtExists;
        public int HideFileExt;
        public bool HiddenExists;
        public int Hidden;
        public bool ContextMenuTreeExists;
        public string ContextMenuRegFile;
        public bool WingetAvailable;
        public List<string> KnownInstalledPackages = new List<string>();
    }

    internal static class StateBackup
    {
        private const string ExplorerKey = @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced";
        private const string ContextMenuSubKey = @"Software\Classes\CLSID\{86ca1aa0-34aa-4e8b-a509-50c905bae2a2}";
        private const string ContextMenuRegKey = @"HKCU\Software\Classes\CLSID\{86ca1aa0-34aa-4e8b-a509-50c905bae2a2}";
        private static readonly string BackupDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WolfHub", "backups");
        private static readonly string BaselinePath = Path.Combine(BackupDirectory, "baseline.json");

        public static string BaselineFile { get { return BaselinePath; } }
        public static bool HasBaseline { get { return File.Exists(BaselinePath); } }

        public static string EnsureBaseline(IEnumerable<string> packageIds, IEnumerable<string> installedPackageIds, bool wingetAvailable)
        {
            if (!HasBaseline) CaptureTo(BaselinePath, packageIds, installedPackageIds, wingetAvailable);
            return BaselinePath;
        }

        public static void CaptureBaseline(IEnumerable<string> packageIds, IEnumerable<string> installedPackageIds, bool wingetAvailable, bool replace)
        {
            if (HasBaseline && !replace) return;
            CaptureTo(BaselinePath, packageIds, installedPackageIds, wingetAvailable);
        }

        public static WolfHubStateSnapshot LoadBaseline()
        {
            if (!HasBaseline) return null;
            var snapshot = new JavaScriptSerializer().Deserialize<WolfHubStateSnapshot>(File.ReadAllText(BaselinePath, Encoding.UTF8));
            if (snapshot == null || snapshot.Version != 1) throw new InvalidDataException("Unsupported or invalid WolfHub backup file.");
            return snapshot;
        }

        public static string Describe(WolfHubStateSnapshot baseline, IEnumerable<string> currentKnownInstalled, bool wingetAvailable)
        {
            if (baseline == null) return "Ainda não há backup do estado inicial. O WolfHub criará um automaticamente antes do primeiro ajuste compatível.";
            string currentScheme = ReadActivePowerScheme();
            int currentHideFileExt; bool currentHideFileExtExists;
            int currentHidden; bool currentHiddenExists;
            ReadExplorerValue("HideFileExt", out currentHideFileExt, out currentHideFileExtExists);
            ReadExplorerValue("Hidden", out currentHidden, out currentHiddenExists);
            bool currentContext;
            using (RegistryKey currentKey = Registry.CurrentUser.OpenSubKey(ContextMenuSubKey, false)) currentContext = currentKey != null;
            string when = baseline.CapturedAtUtc;
            DateTime parsed;
            if (DateTime.TryParse(when, null, System.Globalization.DateTimeStyles.RoundtripKind, out parsed)) when = parsed.ToLocalTime().ToString("g");
            return "Backup inicial: " + when + "\r\n" +
                "Plano de energia salvo: " + ShowValue(baseline.ActivePowerSchemeGuid) + " | atual: " + ShowValue(currentScheme) + "\r\n" +
                "Extensões (HideFileExt): " + ShowDword(baseline.HideFileExtExists, baseline.HideFileExt) + " | atual: " + ShowDword(currentHideFileExtExists, currentHideFileExt) + "\r\n" +
                "Itens ocultos (Hidden): " + ShowDword(baseline.HiddenExists, baseline.Hidden) + " | atual: " + ShowDword(currentHiddenExists, currentHidden) + "\r\n" +
                "Menu clássico: " + (baseline.ContextMenuTreeExists ? "presente" : "ausente") + " | atual: " + (currentContext ? "presente" : "ausente") + "\r\n" +
                "Pacotes conhecidos instalados: " + (baseline.WingetAvailable ? baseline.KnownInstalledPackages.Count.ToString() : "inventário não disponível") + " no backup | " + (wingetAvailable ? (currentKnownInstalled ?? Enumerable.Empty<string>()).Count().ToString() : "WinGet não consultado/disponível") + " agora (inventário, não restauração automática).\r\n\r\n" +
                "O backup restaura o plano de energia e as opções de registro listadas. Programas instalados/atualizados e planos adicionais não são removidos automaticamente.";
        }

        public static void RestoreBaseline(IEnumerable<string> packageIds, IEnumerable<string> installedPackageIds, bool wingetAvailable)
        {
            WolfHubStateSnapshot baseline = LoadBaseline();
            if (baseline == null) throw new InvalidOperationException("Nenhum backup inicial foi encontrado.");
            string recovery = Path.Combine(BackupDirectory, "before-restore-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + ".json");
            CaptureTo(recovery, packageIds, installedPackageIds, wingetAvailable);

            if (!String.IsNullOrWhiteSpace(baseline.ActivePowerSchemeGuid))
            {
                if (!Regex.IsMatch(baseline.ActivePowerSchemeGuid, @"^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$"))
                    throw new InvalidDataException("O backup contém um GUID de plano de energia inválido.");
                RunChecked("powercfg.exe", "/S " + baseline.ActivePowerSchemeGuid);
            }

            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(ExplorerKey, true))
            {
                if (key == null) throw new InvalidOperationException("Não foi possível abrir as opções do Explorer para restauração.");
                RestoreDword(key, "HideFileExt", baseline.HideFileExtExists, baseline.HideFileExt);
                RestoreDword(key, "Hidden", baseline.HiddenExists, baseline.Hidden);
            }

            string contextFile = String.IsNullOrWhiteSpace(baseline.ContextMenuRegFile) ? "" : Path.Combine(BackupDirectory, baseline.ContextMenuRegFile);
            if (baseline.ContextMenuTreeExists)
            {
                if (Path.IsPathRooted(baseline.ContextMenuRegFile) || !String.Equals(Path.GetFileName(baseline.ContextMenuRegFile), baseline.ContextMenuRegFile, StringComparison.Ordinal) || !baseline.ContextMenuRegFile.EndsWith("-context-menu.reg", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("O backup contém um caminho de registro inválido.");
                if (!File.Exists(contextFile)) throw new FileNotFoundException("O backup do menu de contexto está ausente.", contextFile);
                RunChecked("reg.exe", "import " + Quote(contextFile));
            }
            else
            {
                using (RegistryKey parent = Registry.CurrentUser.OpenSubKey(@"Software\Classes\CLSID", true))
                {
                    if (parent != null) parent.DeleteSubKeyTree("{86ca1aa0-34aa-4e8b-a509-50c905bae2a2}", false);
                }
            }
        }

        private static void CaptureTo(string jsonPath, IEnumerable<string> packageIds, IEnumerable<string> installedPackageIds, bool wingetAvailable)
        {
            Directory.CreateDirectory(BackupDirectory);
            var snapshot = new WolfHubStateSnapshot
            {
                CapturedAtUtc = DateTime.UtcNow.ToString("o"),
                ActivePowerSchemeGuid = ReadActivePowerScheme()
            };
            ReadExplorerValue("HideFileExt", out snapshot.HideFileExt, out snapshot.HideFileExtExists);
            ReadExplorerValue("Hidden", out snapshot.Hidden, out snapshot.HiddenExists);
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(ContextMenuSubKey, false)) snapshot.ContextMenuTreeExists = key != null;
            string regName = Path.GetFileNameWithoutExtension(jsonPath) + "-context-menu.reg";
            string regPath = Path.Combine(BackupDirectory, regName);
            if (snapshot.ContextMenuTreeExists)
            {
                RunChecked("reg.exe", "export " + Quote(ContextMenuRegKey) + " " + Quote(regPath) + " /y");
                snapshot.ContextMenuRegFile = regName;
            }
            snapshot.WingetAvailable = wingetAvailable;
            var known = new HashSet<string>((packageIds ?? Enumerable.Empty<string>()).Where(x => !String.IsNullOrWhiteSpace(x)), StringComparer.OrdinalIgnoreCase);
            snapshot.KnownInstalledPackages = (installedPackageIds ?? Enumerable.Empty<string>()).Where(x => known.Contains(x)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            File.WriteAllText(jsonPath, new JavaScriptSerializer().Serialize(snapshot), new UTF8Encoding(false));
        }

        private static string ReadActivePowerScheme()
        {
            try
            {
                string output = SystemActions.RunProcessCapture("powercfg.exe", "/getactivescheme");
                Match match = Regex.Match(output ?? "", @"[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}");
                return match.Success ? match.Value : "";
            }
            catch { return ""; }
        }

        private static void ReadExplorerValue(string name, out int value, out bool exists)
        {
            value = 0; exists = false;
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(ExplorerKey, false))
                {
                    object raw = key == null ? null : key.GetValue(name, null);
                    if (raw is int) { value = (int)raw; exists = true; }
                }
            }
            catch { }
        }

        private static void RestoreDword(RegistryKey key, string name, bool exists, int value)
        {
            if (exists) key.SetValue(name, value, RegistryValueKind.DWord);
            else key.DeleteValue(name, false);
        }

        private static void RunChecked(string fileName, string arguments)
        {
            var info = new ProcessStartInfo { FileName = fileName, Arguments = arguments, UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
            using (Process process = Process.Start(info))
            {
                string stdout = process.StandardOutput.ReadToEnd();
                string stderr = process.StandardError.ReadToEnd();
                process.WaitForExit();
                if (process.ExitCode != 0) throw new InvalidOperationException(fileName + " falhou (" + process.ExitCode + "): " + (stderr + " " + stdout).Trim());
            }
        }

        private static string Quote(string value) { return "\"" + (value ?? "").Replace("\"", "\\\"") + "\""; }
        private static string ShowValue(string value) { return String.IsNullOrWhiteSpace(value) ? "indisponível" : value; }
        private static string ShowDword(bool exists, int value) { return exists ? value.ToString() : "padrão do Windows (valor não definido)"; }
    }
}
