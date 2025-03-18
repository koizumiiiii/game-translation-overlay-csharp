using System;
using System.Diagnostics;
using System.Threading;
using System.Windows.Forms;
using GameTranslationOverlay.Core.OCR;
using GameTranslationOverlay.Core.Utils;
using GameTranslationOverlay.Forms;
using GameTranslationOverlay.Core.Diagnostics;
using System.IO;

namespace GameTranslationOverlay
{
    static class Program
    {
        // グローバルな例外ハンドラー用のイベント
        public static event EventHandler<UnhandledExceptionEventArgs> UnhandledException;

        // クラッシュログ用のパス
        private static readonly string CrashLogPath = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "GameTranslationOverlay",
            "crash_log.txt");

        /// <summary>
        /// アプリケーションのメインエントリーポイント
        /// </summary>
        [STAThread]
        static void Main()
        {
            // 未処理の例外ハンドラ
            AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
            {
                Exception ex = (Exception)args.ExceptionObject;
                try
                {
                    using (StreamWriter writer = new StreamWriter("crash_log.txt", true))
                    {
                        writer.WriteLine($"[{DateTime.Now}] 未処理の例外が発生しました:");
                        writer.WriteLine(ex.ToString());
                        writer.WriteLine(new string('-', 80));
                    }
                }
                catch { }

                MessageBox.Show($"未処理の例外が発生しました: {ex.Message}\n\n{ex.StackTrace}",
                    "エラー", MessageBoxButtons.OK, MessageBoxIcon.Error);
            };

            // UIスレッドの例外ハンドラ
            Application.ThreadException += (sender, args) =>
            {
                try
                {
                    using (StreamWriter writer = new StreamWriter("crash_log.txt", true))
                    {
                        writer.WriteLine($"[{DateTime.Now}] UIスレッドの例外:");
                        writer.WriteLine(args.Exception.ToString());
                        writer.WriteLine(new string('-', 80));
                    }
                }
                catch { }

                MessageBox.Show($"UIスレッドで例外が発生しました: {args.Exception.Message}\n\n{args.Exception.StackTrace}",
                    "エラー", MessageBoxButtons.OK, MessageBoxIcon.Error);
            };

            try
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);

                // MainFormのインスタンス化と実行
                MainForm mainForm = new MainForm();
                Application.Run(mainForm);
            }
            catch (Exception ex)
            {
                try
                {
                    using (StreamWriter writer = new StreamWriter("crash_log.txt", true))
                    {
                        writer.WriteLine($"[{DateTime.Now}] Application.Run中の例外:");
                        writer.WriteLine(ex.ToString());
                        writer.WriteLine(new string('-', 80));
                    }
                }
                catch { }

                MessageBox.Show($"アプリケーション実行中に例外が発生しました: {ex.Message}\n\n{ex.StackTrace}",
                    "エラー", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// Loggerを初期化
        /// </summary>
        private static void InitializeLogger()
        {
            try
            {
                Debug.WriteLine("Loggerを初期化しています...");
                
                // ログディレクトリの取得
                string logDir = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "GameTranslationOverlay",
                    "Logs");
                
                // Loggerの初期化
                Logger.Instance.Initialize(logDir, Logger.LogLevel.Debug);
                
                Debug.WriteLine("Loggerの初期化が完了しました");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Loggerの初期化に失敗しました: {ex.Message}");
                // ロガー初期化失敗はクリティカルではないため、例外を再スローしない
            }
        }
        
        /// <summary>
        /// OCRマネージャーを初期化
        /// </summary>
        private static OcrManager InitializeOcrManager()
        {
            try
            {
                Debug.WriteLine("OCRマネージャーを初期化しています...");
                var ocrManager = new OcrManager();

                // 非同期初期化を同期的に実行（シンプルにするため）
                ocrManager.InitializeAsync().GetAwaiter().GetResult();

                Debug.WriteLine("OCRマネージャーの初期化が完了しました");
                return ocrManager;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"OCRマネージャーの初期化に失敗しました: {ex.Message}");
                throw new ApplicationException("OCRエンジンの初期化に失敗しました", ex);
            }
        }

        /// <summary>
        /// アプリケーションのディレクトリを確認・作成
        /// </summary>
        private static void EnsureApplicationDirectories()
        {
            try
            {
                string appDataDir = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "GameTranslationOverlay");

                // アプリケーションのデータディレクトリを確認・作成
                if (!System.IO.Directory.Exists(appDataDir))
                {
                    System.IO.Directory.CreateDirectory(appDataDir);
                }

                // 設定ディレクトリ
                string settingsDir = System.IO.Path.Combine(appDataDir, "Settings");
                if (!System.IO.Directory.Exists(settingsDir))
                {
                    System.IO.Directory.CreateDirectory(settingsDir);
                }

                // ログディレクトリ
                string logDir = System.IO.Path.Combine(appDataDir, "Logs");
                if (!System.IO.Directory.Exists(logDir))
                {
                    System.IO.Directory.CreateDirectory(logDir);
                }

                Debug.WriteLine($"アプリケーションディレクトリを確認・作成しました: {appDataDir}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ディレクトリ確認・作成エラー: {ex.Message}");
                // 致命的ではないエラーのため、例外は再スローしない
            }
        }

        /// <summary>
        /// アプリケーション終了時のリソースクリーンアップ
        /// </summary>
        private static void CleanupResources()
        {
            try
            {
                Debug.WriteLine("リソースのクリーンアップを実行しています...");

                // ResourceManagerのリソース解放
                int disposedCount = ResourceManager.DisposeAll();
                Debug.WriteLine($"{disposedCount}個のリソースを解放しました");

                // Loggerのシャットダウン
                Logger.Instance.Shutdown();
                Debug.WriteLine("Loggerをシャットダウンしました");

                // 明示的なGC実行（通常は必要ないが、終了時は安全のため）
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"リソースクリーンアップ中にエラーが発生しました: {ex.Message}");
            }
        }

        /// <summary>
        /// UIスレッドでの例外ハンドラー
        /// </summary>
        private static void Application_ThreadException(object sender, ThreadExceptionEventArgs e)
        {
            HandleException(e.Exception, "UIスレッドでの未処理例外");
        }

        /// <summary>
        /// 未処理の例外ハンドラー
        /// </summary>
        private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            if (e.ExceptionObject is Exception ex)
            {
                HandleException(ex, "未処理の例外");
            }
            else
            {
                HandleFatalError(new Exception("不明なエラー"), "未処理の例外（不明）");
            }
        }

        /// <summary>
        /// 例外を処理
        /// </summary>
        private static void HandleException(Exception ex, string context)
        {
            try
            {
                // 内部イベントを発行（カスタム処理のため）
                UnhandledException?.Invoke(null, new UnhandledExceptionEventArgs(ex, false));

                // エラーをログに記録
                LogError(ex, context);

                // エラーをユーザーに通知
                MessageBox.Show(
                    $"エラーが発生しました: {ex.Message}\n\n詳細はログファイルを確認してください。",
                    "エラー",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
            catch
            {
                // 例外ハンドラー内のエラーは無視（さらなる例外を防ぐため）
            }
        }

        /// <summary>
        /// 致命的なエラーを処理
        /// </summary>
        private static void HandleFatalError(Exception ex, string context)
        {
            try
            {
                // 内部イベントを発行
                UnhandledException?.Invoke(null, new UnhandledExceptionEventArgs(ex, true));

                // エラーをログに記録
                LogError(ex, context + " (致命的)");

                // リソースのクリーンアップを試行
                CleanupResources();

                // エラーをユーザーに通知
                MessageBox.Show(
                    $"致命的なエラーが発生したため、アプリケーションを終了します:\n{ex.Message}\n\n詳細はログファイルを確認してください。",
                    "致命的なエラー",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);

                // アプリケーションを終了
                Environment.Exit(1);
            }
            catch
            {
                // 最終手段として強制終了
                Environment.Exit(2);
            }
        }

        /// <summary>
        /// エラーをログファイルに記録
        /// </summary>
        private static void LogError(Exception ex, string context)
        {
            try
            {
                // Loggerを使用してエラーログを記録
                Logger.Instance.LogError($"[{context}] {ex.Message}", ex);
                
                // エラーログディレクトリを確認
                string logDir = System.IO.Path.GetDirectoryName(CrashLogPath);
                if (!System.IO.Directory.Exists(logDir))
                {
                    System.IO.Directory.CreateDirectory(logDir);
                }

                // クラッシュログを出力
                string errorLog = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {context}\n" +
                                 $"例外: {ex.GetType().FullName}\n" +
                                 $"メッセージ: {ex.Message}\n" +
                                 $"スタックトレース:\n{ex.StackTrace}\n\n" +
                                 $"内部例外: {ex.InnerException?.Message}\n" +
                                 $"システム情報: {Environment.OSVersion}, {Environment.ProcessorCount} CPUs\n" +
                                 $"メモリ: {GC.GetTotalMemory(false) / (1024 * 1024)} MB\n" +
                                 $"-----------------------------------\n\n";

                // ファイルに追記
                System.IO.File.AppendAllText(CrashLogPath, errorLog);

                Debug.WriteLine($"エラーログを保存しました: {CrashLogPath}");
            }
            catch (Exception logEx)
            {
                Debug.WriteLine($"エラーログの保存に失敗しました: {logEx.Message}");
            }
        }
    }
}