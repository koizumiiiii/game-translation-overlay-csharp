using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using GameTranslationOverlay.Core.Configuration;
using GameTranslationOverlay.Core.Licensing;
using GameTranslationOverlay.Core.Security;

namespace GameTranslationOverlay
{
    static class Program
    {
        // アプリケーションのバージョン
        public static readonly string AppVersion = "1.0.0";

        // デバッグログのファイルパス
        private static readonly string LogFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "GameTranslationOverlay",
            "logs",
            $"app_{DateTime.Now:yyyyMMdd_HHmmss}.log");

        /// <summary>
        /// アプリケーションのメイン エントリ ポイントです。
        /// </summary>
        [STAThread]
        static void Main()
        {
            try
            {
                // ログディレクトリを作成
                EnsureLogDirectoryExists();

                // ログの開始
                StartLogging();

                LogInfo($"アプリケーション起動: バージョン {AppVersion}");

                // アプリケーション設定の初期化
                LogInfo("アプリケーション設定を初期化しています...");
                var settings = AppSettings.Instance;

                // アプリケーションバージョンの更新
                if (settings.AppVersion != AppVersion)
                {
                    settings.AppVersion = AppVersion;
                    settings.SaveSettings();
                    LogInfo($"アプリケーションバージョンを更新しました: {AppVersion}");
                }

                LogInfo($"設定を読み込みました: バージョン={settings.AppVersion}, デバッグモード={settings.DebugModeEnabled}");

                // ライセンス管理の初期化
                LogInfo("ライセンス状態を初期化しています...");
                var licenseManager = LicenseManager.Instance;

                // ライセンスの検証
                licenseManager.VerifyLicense();
                LogInfo($"ライセンス状態: タイプ={licenseManager.CurrentLicenseType}, 有効={licenseManager.IsLicenseValid}");

                // デバッグモードの設定によって動作を変更
                if (settings.DebugModeEnabled)
                {
                    LogInfo("デバッグモードが有効: 全機能にアクセス可能");

                    // デバッグモード用の追加設定
                    if (string.IsNullOrEmpty(settings.LicenseKey) && Debugger.IsAttached)
                    {
                        // 開発モードでの動作確認用にProライセンスを生成
                        string testLicenseKey = licenseManager.GenerateLicenseKey(LicenseType.Pro, 12);
                        LogInfo($"開発用ライセンスキーを生成しました: {testLicenseKey}");

                        // 実際のアプリケーションでは表示だけにして設定はしない
                        // settings.LicenseKey = testLicenseKey;
                        // settings.SaveSettings();
                    }
                }

                // APIキー管理の初期化チェック
                CheckApiKeyStatus();

                // アプリケーション表示設定
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);

                // メインフォームを起動
                LogInfo("メインフォームを起動します");
                Application.Run(new MainForm());
            }
            catch (Exception ex)
            {
                string errorMessage = $"アプリケーション起動中にエラーが発生しました: {ex.Message}";
                LogError(errorMessage);
                LogError($"詳細: {ex.StackTrace}");

                MessageBox.Show(
                    $"アプリケーションの初期化中にエラーが発生しました。\n\n{ex.Message}",
                    "起動エラー",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);

                // 重大なエラーが発生した場合、環境によってはログファイルの保存先を通知
                if (Debugger.IsAttached)
                {
                    MessageBox.Show(
                        $"ログファイルは以下の場所に保存されています:\n{LogFilePath}",
                        "デバッグ情報",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                }
            }
        }

        /// <summary>
        /// ログディレクトリの存在を確認し、必要に応じて作成する
        /// </summary>
        private static void EnsureLogDirectoryExists()
        {
            string logDirectory = Path.GetDirectoryName(LogFilePath);
            if (!Directory.Exists(logDirectory))
            {
                Directory.CreateDirectory(logDirectory);
            }
        }

        /// <summary>
        /// ログ機能を開始
        /// </summary>
        private static void StartLogging()
        {
            // デバッグ出力をファイルにリダイレクト
            TextWriterTraceListener listener = new TextWriterTraceListener(LogFilePath);
            Trace.Listeners.Add(listener);
            Trace.AutoFlush = true;

            // ログファイルのヘッダー
            Trace.WriteLine($"-----------------------------------------------------------");
            Trace.WriteLine($"GameTranslationOverlay ログ - バージョン {AppVersion}");
            Trace.WriteLine($"開始時刻: {DateTime.Now}");
            Trace.WriteLine($"実行環境: {Environment.OSVersion}, CLR {Environment.Version}");
            Trace.WriteLine($"-----------------------------------------------------------");
        }

        /// <summary>
        /// APIキーの状態を確認
        /// </summary>
        private static void CheckApiKeyStatus()
        {
            try
            {
                var settings = AppSettings.Instance;

                // カスタムAPIキーが設定されているか確認
                if (!string.IsNullOrEmpty(settings.CustomApiKey))
                {
                    LogInfo("カスタムAPIキーが設定されています");
                }
                else
                {
                    // ビルトインキーを試行
                    string apiKey = ApiKeyProtector.Instance.GetDecryptedApiKey();
                    if (string.IsNullOrEmpty(apiKey))
                    {
                        LogWarning("APIキーが設定されていないため、AI翻訳機能が制限されます");
                    }
                    else
                    {
                        LogInfo("ビルトインAPIキーを使用します");
                    }
                }
            }
            catch (Exception ex)
            {
                LogWarning($"APIキー確認中にエラーが発生しました: {ex.Message}");
            }
        }

        /// <summary>
        /// 情報レベルのログを出力
        /// </summary>
        private static void LogInfo(string message)
        {
            string formattedMessage = $"[INFO] {DateTime.Now:HH:mm:ss.fff} - {message}";
            Debug.WriteLine(formattedMessage);
        }

        /// <summary>
        /// 警告レベルのログを出力
        /// </summary>
        private static void LogWarning(string message)
        {
            string formattedMessage = $"[WARN] {DateTime.Now:HH:mm:ss.fff} - {message}";
            Debug.WriteLine(formattedMessage);
        }

        /// <summary>
        /// エラーレベルのログを出力
        /// </summary>
        private static void LogError(string message)
        {
            string formattedMessage = $"[ERROR] {DateTime.Now:HH:mm:ss.fff} - {message}";
            Debug.WriteLine(formattedMessage);
        }
    }
}