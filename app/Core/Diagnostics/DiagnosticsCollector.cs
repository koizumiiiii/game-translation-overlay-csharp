using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;
using Microsoft.Win32;

namespace GameTranslationOverlay.Core.Diagnostics
{
    /// <summary>
    /// アプリケーションとシステムの診断情報を収集するクラス
    /// </summary>
    public class DiagnosticsCollector
    {
        #region シングルトンパターン

        private static readonly Lazy<DiagnosticsCollector> _instance = new Lazy<DiagnosticsCollector>(() => new DiagnosticsCollector());

        /// <summary>
        /// DiagnosticsCollectorのインスタンスを取得します
        /// </summary>
        public static DiagnosticsCollector Instance => _instance.Value;

        #endregion

        #region コンストラクタ

        private DiagnosticsCollector()
        {
            // 明示的な初期化は必要なし
        }

        #endregion

        #region システム情報

        /// <summary>
        /// システム情報を収集します
        /// </summary>
        /// <returns>システム情報を含むディクショナリ</returns>
        public Dictionary<string, string> CollectSystemInfo()
        {
            Dictionary<string, string> systemInfo = new Dictionary<string, string>();

            try
            {
                // OS情報
                systemInfo.Add("OS", Environment.OSVersion.ToString());
                systemInfo.Add("OS Platform", Environment.OSVersion.Platform.ToString());
                systemInfo.Add("OS Version", Environment.OSVersion.VersionString);
                systemInfo.Add("OS Service Pack", Environment.OSVersion.ServicePack);
                systemInfo.Add("64-bit OS", Environment.Is64BitOperatingSystem.ToString());
                systemInfo.Add("64-bit Process", Environment.Is64BitProcess.ToString());

                // システムリソース
                systemInfo.Add("Processor Count", Environment.ProcessorCount.ToString());
                systemInfo.Add("System Page Size", Environment.SystemPageSize.ToString() + " bytes");
                systemInfo.Add("Working Set", (Environment.WorkingSet / 1024 / 1024).ToString() + " MB");
                systemInfo.Add("System Memory", GetTotalPhysicalMemory().ToString() + " MB");

                // ユーザー情報
                systemInfo.Add("Machine Name", Environment.MachineName);
                systemInfo.Add("User Name", Environment.UserName);
                systemInfo.Add("User Domain", Environment.UserDomainName);

                // ディレクトリ情報
                systemInfo.Add("Current Directory", Environment.CurrentDirectory);
                systemInfo.Add("System Directory", Environment.SystemDirectory);
                systemInfo.Add("Temp Path", System.IO.Path.GetTempPath());

                // .NET情報
                systemInfo.Add(".NET Version", Environment.Version.ToString());
                systemInfo.Add("CLR Version", GetCLRVersion());

                // ディスプレイ情報
                systemInfo.Add("Screen Resolution", GetScreenResolution());

                // 時間情報
                systemInfo.Add("Time Zone", TimeZoneInfo.Local.DisplayName);
                systemInfo.Add("System Uptime", GetSystemUptime());

                // カルチャ情報
                systemInfo.Add("Current Culture", System.Globalization.CultureInfo.CurrentCulture.DisplayName);
                systemInfo.Add("UI Culture", System.Globalization.CultureInfo.CurrentUICulture.DisplayName);

                // Windows Formsのバージョンも取得
                systemInfo.Add("Windows Forms Version", GetWinFormsVersion());

                // デバイス情報（ディスク容量など）
                AddDriveInfo(systemInfo);
            }
            catch (Exception ex)
            {
                Logger.Instance.Error("DiagnosticsCollector", "Failed to collect system info", ex);
                systemInfo.Add("Error", "Failed to collect complete system info: " + ex.Message);
            }

            return systemInfo;
        }

        /// <summary>
        /// システムの合計物理メモリをMB単位で取得します
        /// </summary>
        private long GetTotalPhysicalMemory()
        {
            try
            {
                // WMIを使用してメモリ情報を取得することもできますが、
                // プロセスのパフォーマンス情報から簡易的に取得
                System.Diagnostics.PerformanceCounter ramCounter = new System.Diagnostics.PerformanceCounter("Memory", "Available MBytes");
                float availableMB = ramCounter.NextValue();

                // 合計は取得が複雑なので、WMIを使用せずに簡易的に取得
                // 実際のアプリでは、より正確な方法（WMI等）を検討
                return (long)availableMB;
            }
            catch
            {
                return -1; // 取得できなかった場合
            }
        }

        /// <summary>
        /// CLRのバージョン情報を取得します
        /// </summary>
        private string GetCLRVersion()
        {
            Type type = typeof(Environment);
            PropertyInfo property = type.GetProperty("Version", BindingFlags.Public | BindingFlags.Static);
            return property?.GetValue(null)?.ToString() ?? "Unknown";
        }

        /// <summary>
        /// 画面解像度情報を取得します
        /// </summary>
        private string GetScreenResolution()
        {
            try
            {
                System.Windows.Forms.Screen primaryScreen = System.Windows.Forms.Screen.PrimaryScreen;
                return $"{primaryScreen.Bounds.Width}x{primaryScreen.Bounds.Height} ({primaryScreen.BitsPerPixel} bits)";
            }
            catch
            {
                return "Unknown";
            }
        }

        /// <summary>
        /// システムの起動時間を取得します
        /// </summary>
        private string GetSystemUptime()
        {
            try
            {
                TimeSpan uptime = TimeSpan.FromMilliseconds(Environment.TickCount);
                return $"{uptime.Days}d {uptime.Hours}h {uptime.Minutes}m {uptime.Seconds}s";
            }
            catch
            {
                return "Unknown";
            }
        }

        /// <summary>
        /// Windows Formsのバージョンを取得します
        /// </summary>
        private string GetWinFormsVersion()
        {
            try
            {
                return typeof(System.Windows.Forms.Form).Assembly.GetName().Version.ToString();
            }
            catch
            {
                return "Unknown";
            }
        }

        /// <summary>
        /// ドライブ情報を追加します
        /// </summary>
        private void AddDriveInfo(Dictionary<string, string> systemInfo)
        {
            try
            {
                System.IO.DriveInfo[] drives = System.IO.DriveInfo.GetDrives();

                foreach (System.IO.DriveInfo drive in drives)
                {
                    try
                    {
                        if (drive.IsReady)
                        {
                            string driveName = $"Drive {drive.Name} ({drive.DriveType})";
                            double freeGB = Math.Round((double)drive.TotalFreeSpace / 1024 / 1024 / 1024, 2);
                            double totalGB = Math.Round((double)drive.TotalSize / 1024 / 1024 / 1024, 2);
                            string driveInfo = $"{freeGB} GB free of {totalGB} GB ({drive.DriveFormat})";

                            systemInfo.Add(driveName, driveInfo);
                        }
                    }
                    catch
                    {
                        // 個別ドライブのエラーは無視して続行
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.Warning("DiagnosticsCollector", "Failed to collect drive info: " + ex.Message);
            }
        }

        #endregion

        #region アプリケーション情報

        /// <summary>
        /// アプリケーション情報を収集します
        /// </summary>
        /// <returns>アプリケーション情報を含むディクショナリ</returns>
        public Dictionary<string, string> CollectApplicationInfo()
        {
            Dictionary<string, string> appInfo = new Dictionary<string, string>();

            try
            {
                // 実行アセンブリの情報
                Assembly assembly = Assembly.GetExecutingAssembly();
                AssemblyName assemblyName = assembly.GetName();

                appInfo.Add("Application Name", assemblyName.Name);
                appInfo.Add("Application Version", assemblyName.Version.ToString());

                // アセンブリ属性から情報を取得
                object[] titleAttributes = assembly.GetCustomAttributes(typeof(AssemblyTitleAttribute), false);
                if (titleAttributes.Length > 0)
                {
                    AssemblyTitleAttribute titleAttribute = (AssemblyTitleAttribute)titleAttributes[0];
                    appInfo.Add("Assembly Title", titleAttribute.Title);
                }

                object[] descAttributes = assembly.GetCustomAttributes(typeof(AssemblyDescriptionAttribute), false);
                if (descAttributes.Length > 0)
                {
                    AssemblyDescriptionAttribute descAttribute = (AssemblyDescriptionAttribute)descAttributes[0];
                    appInfo.Add("Assembly Description", descAttribute.Description);
                }

                object[] companyAttributes = assembly.GetCustomAttributes(typeof(AssemblyCompanyAttribute), false);
                if (companyAttributes.Length > 0)
                {
                    AssemblyCompanyAttribute companyAttribute = (AssemblyCompanyAttribute)companyAttributes[0];
                    appInfo.Add("Company", companyAttribute.Company);
                }

                object[] productAttributes = assembly.GetCustomAttributes(typeof(AssemblyProductAttribute), false);
                if (productAttributes.Length > 0)
                {
                    AssemblyProductAttribute productAttribute = (AssemblyProductAttribute)productAttributes[0];
                    appInfo.Add("Product", productAttribute.Product);
                }

                object[] copyrightAttributes = assembly.GetCustomAttributes(typeof(AssemblyCopyrightAttribute), false);
                if (copyrightAttributes.Length > 0)
                {
                    AssemblyCopyrightAttribute copyrightAttribute = (AssemblyCopyrightAttribute)copyrightAttributes[0];
                    appInfo.Add("Copyright", copyrightAttribute.Copyright);
                }

                // アプリケーションのパス情報
                appInfo.Add("Executable Path", Assembly.GetExecutingAssembly().Location);
                appInfo.Add("Base Directory", AppDomain.CurrentDomain.BaseDirectory);

                // プロセス情報
                System.Diagnostics.Process currentProcess = System.Diagnostics.Process.GetCurrentProcess();
                appInfo.Add("Process ID", currentProcess.Id.ToString());
                appInfo.Add("Process Name", currentProcess.ProcessName);
                appInfo.Add("Start Time", currentProcess.StartTime.ToString());
                appInfo.Add("Total Process Memory", (currentProcess.PrivateMemorySize64 / 1024 / 1024).ToString() + " MB");
                appInfo.Add("Process Threads", currentProcess.Threads.Count.ToString());
                appInfo.Add("Process Handles", currentProcess.HandleCount.ToString());

                // .NETランタイム情報
                appInfo.Add("App Domain", AppDomain.CurrentDomain.FriendlyName);
                appInfo.Add("Target Framework", GetTargetFramework());

                // コマンドライン引数
                appInfo.Add("Command Line", Environment.CommandLine);

                // 実行ユーザー情報
                appInfo.Add("Running As User", Environment.UserName);
                appInfo.Add("Is Administrator", IsRunningAsAdministrator().ToString());
            }
            catch (Exception ex)
            {
                Logger.Instance.Error("DiagnosticsCollector", "Failed to collect application info", ex);
                appInfo.Add("Error", "Failed to collect complete application info: " + ex.Message);
            }

            return appInfo;
        }

        /// <summary>
        /// ターゲットフレームワークの情報を取得します
        /// </summary>
        private string GetTargetFramework()
        {
            try
            {
                Assembly assembly = Assembly.GetExecutingAssembly();
                Type targetFrameworkAttribute = typeof(System.Runtime.Versioning.TargetFrameworkAttribute);
                if (assembly.IsDefined(targetFrameworkAttribute, false))
                {
                    System.Runtime.Versioning.TargetFrameworkAttribute attribute =
                        (System.Runtime.Versioning.TargetFrameworkAttribute)assembly.GetCustomAttribute(targetFrameworkAttribute);
                    return attribute.FrameworkName;
                }
            }
            catch { }

            return "Unknown";
        }

        /// <summary>
        /// 管理者権限で実行されているかを確認します
        /// </summary>
        private bool IsRunningAsAdministrator()
        {
            try
            {
                System.Security.Principal.WindowsIdentity identity = System.Security.Principal.WindowsIdentity.GetCurrent();
                System.Security.Principal.WindowsPrincipal principal = new System.Security.Principal.WindowsPrincipal(identity);
                return principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
            }
            catch
            {
                return false;
            }
        }

        #endregion

        #region パフォーマンス指標

        /// <summary>
        /// 現在のパフォーマンス指標を収集します
        /// </summary>
        /// <returns>パフォーマンス指標を含むディクショナリ</returns>
        public Dictionary<string, string> CollectPerformanceMetrics()
        {
            Dictionary<string, string> metrics = new Dictionary<string, string>();

            try
            {
                // プロセス情報
                System.Diagnostics.Process currentProcess = System.Diagnostics.Process.GetCurrentProcess();

                // メモリ使用量
                metrics.Add("Private Memory", FormatBytes(currentProcess.PrivateMemorySize64));
                metrics.Add("Working Set", FormatBytes(currentProcess.WorkingSet64));
                metrics.Add("Virtual Memory", FormatBytes(currentProcess.VirtualMemorySize64));
                metrics.Add("Paged Memory", FormatBytes(currentProcess.PagedMemorySize64));
                metrics.Add("Paged System Memory", FormatBytes(currentProcess.PagedSystemMemorySize64));
                metrics.Add("Non-Paged System Memory", FormatBytes(currentProcess.NonpagedSystemMemorySize64));

                // CPU使用時間
                metrics.Add("Total Processor Time", currentProcess.TotalProcessorTime.ToString());
                metrics.Add("User Processor Time", currentProcess.UserProcessorTime.ToString());
                metrics.Add("Privileged Processor Time", currentProcess.PrivilegedProcessorTime.ToString());

                // その他のプロセス情報
                metrics.Add("Thread Count", currentProcess.Threads.Count.ToString());
                metrics.Add("Handle Count", currentProcess.HandleCount.ToString());
                metrics.Add("GC Total Memory", FormatBytes(GC.GetTotalMemory(false)));

                // GC情報
                metrics.Add("GC Max Generation", GC.MaxGeneration.ToString());
                for (int i = 0; i <= GC.MaxGeneration; i++)
                {
                    metrics.Add($"GC Collection Count (Gen {i})", GC.CollectionCount(i).ToString());
                }

                // CPU使用率（推定）
                float cpuUsage = EstimateCpuUsage(currentProcess);
                metrics.Add("Estimated CPU Usage", cpuUsage.ToString("F2") + "%");

                // メモリ使用率（推定）
                double memoryUsagePercentage = EstimateMemoryUsage();
                metrics.Add("Estimated Memory Usage", memoryUsagePercentage.ToString("F2") + "%");

                // アプリケーション実行時間
                TimeSpan runTime = DateTime.Now - currentProcess.StartTime;
                metrics.Add("Application Uptime", $"{runTime.Days}d {runTime.Hours}h {runTime.Minutes}m {runTime.Seconds}s");
            }
            catch (Exception ex)
            {
                Logger.Instance.Error("DiagnosticsCollector", "Failed to collect performance metrics", ex);
                metrics.Add("Error", "Failed to collect complete performance metrics: " + ex.Message);
            }

            return metrics;
        }

        /// <summary>
        /// バイト単位のサイズを人間が読みやすい形式にフォーマットします
        /// </summary>
        private string FormatBytes(long bytes)
        {
            string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
            int suffixIndex = 0;
            double size = bytes;

            while (size >= 1024 && suffixIndex < suffixes.Length - 1)
            {
                size /= 1024;
                suffixIndex++;
            }

            return $"{size:F2} {suffixes[suffixIndex]}";
        }

        /// <summary>
        /// CPU使用率を推定します
        /// </summary>
        private float EstimateCpuUsage(System.Diagnostics.Process process)
        {
            try
            {
                TimeSpan oldTotalProcessorTime = process.TotalProcessorTime;
                DateTime oldDateTime = DateTime.Now;

                // 短い待機
                System.Threading.Thread.Sleep(500);

                TimeSpan newTotalProcessorTime = process.TotalProcessorTime;
                DateTime newDateTime = DateTime.Now;

                TimeSpan processorTimeDifference = newTotalProcessorTime - oldTotalProcessorTime;
                TimeSpan timeDifference = newDateTime - oldDateTime;

                double cpuUsageRatio = processorTimeDifference.TotalMilliseconds / (Environment.ProcessorCount * timeDifference.TotalMilliseconds);
                return (float)(cpuUsageRatio * 100);
            }
            catch
            {
                return -1; // 取得できなかった場合
            }
        }

        /// <summary>
        /// メモリ使用率を推定します
        /// </summary>
        private double EstimateMemoryUsage()
        {
            try
            {
                System.Diagnostics.Process process = System.Diagnostics.Process.GetCurrentProcess();
                long privateBytes = process.PrivateMemorySize64;

                // 簡易的な推定のため、物理メモリに対する比率を計算
                // （より正確な方法はWMIを使用）
                double totalPhysicalMemoryMB = GetTotalPhysicalMemory();

                if (totalPhysicalMemoryMB > 0)
                {
                    double privateMB = privateBytes / (1024.0 * 1024.0);
                    return (privateMB / totalPhysicalMemoryMB) * 100.0;
                }
                return -1;
            }
            catch
            {
                return -1;
            }
        }

        #endregion

        #region 診断スナップショット

        /// <summary>
        /// 現在の診断情報のスナップショットを作成します
        /// </summary>
        /// <returns>診断情報を含むディクショナリ</returns>
        public Dictionary<string, Dictionary<string, string>> CreateDiagnosticSnapshot()
        {
            Dictionary<string, Dictionary<string, string>> snapshot = new Dictionary<string, Dictionary<string, string>>();

            // 各種情報を収集
            snapshot.Add("System", CollectSystemInfo());
            snapshot.Add("Application", CollectApplicationInfo());
            snapshot.Add("Performance", CollectPerformanceMetrics());
            snapshot.Add("Environment", CollectEnvironmentVariables());

            return snapshot;
        }

        /// <summary>
        /// 診断スナップショットをファイルに保存します
        /// </summary>
        /// <param name="filePath">保存先ファイルパス</param>
        /// <returns>成功したかどうか</returns>
        public bool SaveDiagnosticSnapshot(string filePath)
        {
            try
            {
                Dictionary<string, Dictionary<string, string>> snapshot = CreateDiagnosticSnapshot();
                StringBuilder sb = new StringBuilder();

                sb.AppendLine("=== Diagnostic Snapshot ===");
                sb.AppendLine($"Date: {DateTime.Now}");
                sb.AppendLine();

                foreach (var category in snapshot)
                {
                    sb.AppendLine($"=== {category.Key} ===");
                    foreach (var item in category.Value)
                    {
                        sb.AppendLine($"{item.Key}: {item.Value}");
                    }
                    sb.AppendLine();
                }

                System.IO.File.WriteAllText(filePath, sb.ToString());
                Logger.Instance.Info("DiagnosticsCollector", $"Diagnostic snapshot saved to: {filePath}");
                return true;
            }
            catch (Exception ex)
            {
                Logger.Instance.Error("DiagnosticsCollector", "Failed to save diagnostic snapshot", ex);
                return false;
            }
        }

        #endregion

        #region 環境変数

        /// <summary>
        /// 環境変数情報を収集します
        /// </summary>
        /// <returns>環境変数を含むディクショナリ</returns>
        public Dictionary<string, string> CollectEnvironmentVariables()
        {
            Dictionary<string, string> envVars = new Dictionary<string, string>();

            try
            {
                // すべての環境変数を取得
                foreach (System.Collections.DictionaryEntry entry in Environment.GetEnvironmentVariables())
                {
                    string key = entry.Key.ToString();
                    string value = entry.Value?.ToString() ?? string.Empty;

                    // セキュリティに関わる可能性のある変数は値を隠す
                    if (key.Contains("KEY") || key.Contains("SECRET") || key.Contains("TOKEN") ||
                        key.Contains("PASSWORD") || key.Contains("CREDENTIAL"))
                    {
                        value = "*****";
                    }

                    envVars.Add(key, value);
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.Error("DiagnosticsCollector", "Failed to collect environment variables", ex);
                envVars.Add("Error", "Failed to collect environment variables: " + ex.Message);
            }

            return envVars;
        }

        #endregion

        #region レジストリ情報

        /// <summary>
        /// 指定したレジストリパスの情報を収集します
        /// </summary>
        /// <param name="registryPath">レジストリパス</param>
        /// <returns>レジストリ情報を含むディクショナリ</returns>
        public Dictionary<string, string> CollectRegistryInfo(string registryPath)
        {
            Dictionary<string, string> registryInfo = new Dictionary<string, string>();

            try
            {
                using (RegistryKey key = Registry.LocalMachine.OpenSubKey(registryPath))
                {
                    if (key != null)
                    {
                        string[] valueNames = key.GetValueNames();
                        foreach (string valueName in valueNames)
                        {
                            object value = key.GetValue(valueName);
                            registryInfo.Add(valueName, value?.ToString() ?? string.Empty);
                        }
                    }
                    else
                    {
                        registryInfo.Add("Error", $"Registry key not found: {registryPath}");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.Error("DiagnosticsCollector", $"Failed to collect registry info for path: {registryPath}", ex);
                registryInfo.Add("Error", $"Failed to collect registry info: {ex.Message}");
            }

            return registryInfo;
        }

        #endregion
    }
}