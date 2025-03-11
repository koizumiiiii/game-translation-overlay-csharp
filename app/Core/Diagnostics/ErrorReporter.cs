using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Runtime.CompilerServices;

namespace GameTranslationOverlay.Core.Diagnostics
{
    /// <summary>
    /// アプリケーションのエラーを報告するクラス
    /// </summary>
    public class ErrorReporter
    {
        #region シングルトンパターン

        private static readonly Lazy<ErrorReporter> _instance = new Lazy<ErrorReporter>(() => new ErrorReporter());

        /// <summary>
        /// ErrorReporterのインスタンスを取得します
        /// </summary>
        public static ErrorReporter Instance => _instance.Value;

        #endregion

        #region プロパティとフィールド

        private readonly object _lockObject = new object();
        private string _reportDirectory;
        private bool _isInitialized = false;
        private bool _showErrorDialog = true;
        private Action<Exception, string> _customErrorHandler;

        /// <summary>
        /// エラーダイアログを表示するかどうかを設定または取得します
        /// </summary>
        public bool ShowErrorDialog
        {
            get => _showErrorDialog;
            set => _showErrorDialog = value;
        }

        /// <summary>
        /// カスタムエラーハンドラを設定します
        /// </summary>
        public Action<Exception, string> CustomErrorHandler
        {
            get => _customErrorHandler;
            set => _customErrorHandler = value;
        }

        #endregion

        #region コンストラクタ

        private ErrorReporter()
        {
            // 明示的な初期化が必要
        }

        #endregion

        #region 初期化

        /// <summary>
        /// エラーレポーターを初期化し、グローバル例外ハンドラを設定します
        /// </summary>
        /// <param name="reportDirectory">エラーレポートを保存するディレクトリパス</param>
        /// <param name="showErrorDialog">エラー発生時にダイアログを表示するか</param>
        public void Initialize(string reportDirectory, bool showErrorDialog = true)
        {
            lock (_lockObject)
            {
                try
                {
                    _reportDirectory = reportDirectory;
                    _showErrorDialog = showErrorDialog;

                    // レポートディレクトリが存在しない場合は作成
                    if (!System.IO.Directory.Exists(_reportDirectory))
                    {
                        System.IO.Directory.CreateDirectory(_reportDirectory);
                    }

                    // グローバル例外ハンドラの設定
                    AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
                    Application.ThreadException += OnThreadException;
                    TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

                    _isInitialized = true;

                    // 初期化成功ログ
                    Logger.Instance.Info("ErrorReporter", "Error reporter initialized successfully");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to initialize error reporter: {ex.Message}");
                    _isInitialized = false;
                }
            }
        }

        #endregion

        #region 例外ハンドラ

        /// <summary>
        /// アプリケーションドメインの未処理例外を処理します
        /// </summary>
        private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            Exception exception = e.ExceptionObject as Exception;
            bool isTerminating = e.IsTerminating;

            string source = "UnhandledException";
            string errorReportPath = ReportError(exception, source);

            Logger.Instance.Fatal(source, "Unhandled exception occurred", exception);

            if (isTerminating)
            {
                Logger.Instance.Fatal(source, "Application is terminating due to unhandled exception", exception);
            }

            // カスタムエラーハンドラがある場合は呼び出し
            _customErrorHandler?.Invoke(exception, errorReportPath);

            // エラーダイアログを表示
            if (_showErrorDialog)
            {
                ShowErrorMessageBox(exception, errorReportPath, isTerminating);
            }
        }

        /// <summary>
        /// UIスレッドの未処理例外を処理します
        /// </summary>
        private void OnThreadException(object sender, ThreadExceptionEventArgs e)
        {
            Exception exception = e.Exception;
            string source = "ThreadException";
            string errorReportPath = ReportError(exception, source);

            Logger.Instance.Fatal(source, "Thread exception occurred", exception);

            // カスタムエラーハンドラがある場合は呼び出し
            _customErrorHandler?.Invoke(exception, errorReportPath);

            // エラーダイアログを表示
            if (_showErrorDialog)
            {
                ShowErrorMessageBox(exception, errorReportPath, false);
            }
        }

        /// <summary>
        /// 未監視のタスク例外を処理します
        /// </summary>
        private void OnUnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
        {
            Exception exception = e.Exception;
            string source = "TaskException";
            string errorReportPath = ReportError(exception, source);

            Logger.Instance.Fatal(source, "Unobserved task exception occurred", exception);

            // 例外を監視済みとしてマーク（クラッシュを防止）
            e.SetObserved();

            // カスタムエラーハンドラがある場合は呼び出し
            _customErrorHandler?.Invoke(exception, errorReportPath);

            // エラーダイアログを表示
            if (_showErrorDialog)
            {
                ShowErrorMessageBox(exception, errorReportPath, false);
            }
        }

        #endregion

        #region エラーレポート

        /// <summary>
        /// エラーをレポートし、レポートファイルのパスを返します
        /// </summary>
        /// <param name="exception">報告する例外</param>
        /// <param name="source">例外ソース</param>
        /// <param name="additionalInfo">追加情報（オプション）</param>
        /// <param name="callerMemberName">呼び出し元メンバー名（自動）</param>
        /// <param name="callerFilePath">呼び出し元ファイルパス（自動）</param>
        /// <param name="callerLineNumber">呼び出し元行番号（自動）</param>
        /// <returns>エラーレポートファイルのパス</returns>
        public string ReportError(
            Exception exception,
            string source,
            string additionalInfo = null,
            [CallerMemberName] string callerMemberName = "",
            [CallerFilePath] string callerFilePath = "",
            [CallerLineNumber] int callerLineNumber = 0)
        {
            if (!_isInitialized)
            {
                System.Diagnostics.Debug.WriteLine("Error reporter is not initialized");
                return null;
            }

            try
            {
                lock (_lockObject)
                {
                    // エラーレポートのファイル名（日時を含む）
                    string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                    string fileName = $"ErrorReport_{timestamp}.txt";
                    string filePath = System.IO.Path.Combine(_reportDirectory, fileName);

                    // エラーレポートの内容を構築
                    StringBuilder reportBuilder = new StringBuilder();
                    reportBuilder.AppendLine("======= Error Report =======");
                    reportBuilder.AppendLine($"Timestamp: {DateTime.Now}");
                    reportBuilder.AppendLine($"Source: {source}");
                    reportBuilder.AppendLine();

                    reportBuilder.AppendLine("--- Exception Details ---");
                    AppendExceptionDetails(reportBuilder, exception);
                    reportBuilder.AppendLine();

                    reportBuilder.AppendLine("--- Caller Information ---");
                    reportBuilder.AppendLine($"Member: {callerMemberName}");
                    reportBuilder.AppendLine($"File: {callerFilePath}");
                    reportBuilder.AppendLine($"Line: {callerLineNumber}");
                    reportBuilder.AppendLine();

                    if (!string.IsNullOrEmpty(additionalInfo))
                    {
                        reportBuilder.AppendLine("--- Additional Information ---");
                        reportBuilder.AppendLine(additionalInfo);
                        reportBuilder.AppendLine();
                    }

                    reportBuilder.AppendLine("--- System Information ---");
                    AppendSystemInformation(reportBuilder);
                    reportBuilder.AppendLine();

                    reportBuilder.AppendLine("--- Stack Trace ---");
                    reportBuilder.AppendLine(exception?.StackTrace ?? "No stack trace available");

                    // レポートをファイルに保存
                    System.IO.File.WriteAllText(filePath, reportBuilder.ToString());

                    // ログにエラーを記録
                    Logger.Instance.Error(source, $"Error report saved to: {filePath}", exception);

                    return filePath;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to create error report: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 例外の詳細情報をレポートに追加します
        /// </summary>
        private void AppendExceptionDetails(StringBuilder builder, Exception exception)
        {
            if (exception == null)
            {
                builder.AppendLine("No exception information available");
                return;
            }

            builder.AppendLine($"Type: {exception.GetType().FullName}");
            builder.AppendLine($"Message: {exception.Message}");
            builder.AppendLine($"Source: {exception.Source}");
            builder.AppendLine($"Target Site: {exception.TargetSite}");

            // Data コレクションの内容
            if (exception.Data.Count > 0)
            {
                builder.AppendLine("Data:");
                foreach (System.Collections.DictionaryEntry entry in exception.Data)
                {
                    builder.AppendLine($"  {entry.Key}: {entry.Value}");
                }
            }

            // InnerException がある場合は再帰的に追加
            if (exception.InnerException != null)
            {
                builder.AppendLine();
                builder.AppendLine("--- Inner Exception ---");
                AppendExceptionDetails(builder, exception.InnerException);
            }
        }

        /// <summary>
        /// システム情報をレポートに追加します
        /// </summary>
        private void AppendSystemInformation(StringBuilder builder)
        {
            try
            {
                builder.AppendLine($"OS: {Environment.OSVersion}");
                builder.AppendLine($".NET Version: {Environment.Version}");
                builder.AppendLine($"64-bit OS: {Environment.Is64BitOperatingSystem}");
                builder.AppendLine($"64-bit Process: {Environment.Is64BitProcess}");
                builder.AppendLine($"Processor Count: {Environment.ProcessorCount}");
                builder.AppendLine($"System Directory: {Environment.SystemDirectory}");
                builder.AppendLine($"Machine Name: {Environment.MachineName}");
                builder.AppendLine($"User Name: {Environment.UserName}");
                builder.AppendLine($"Current Directory: {Environment.CurrentDirectory}");
                builder.AppendLine($"Command Line: {Environment.CommandLine}");
                builder.AppendLine($"Working Set: {Environment.WorkingSet / 1024 / 1024} MB");
                builder.AppendLine($"System Page Size: {Environment.SystemPageSize} bytes");

                // アプリケーション情報
                System.Reflection.Assembly assembly = System.Reflection.Assembly.GetExecutingAssembly();
                System.Reflection.AssemblyName assemblyName = assembly.GetName();
                builder.AppendLine($"Application: {assemblyName.Name}");
                builder.AppendLine($"Version: {assemblyName.Version}");
                builder.AppendLine($"Culture: {assemblyName.CultureInfo.DisplayName}");
            }
            catch (Exception ex)
            {
                builder.AppendLine($"Error gathering system information: {ex.Message}");
            }
        }

        /// <summary>
        /// エラーメッセージボックスを表示します
        /// </summary>
        private void ShowErrorMessageBox(Exception exception, string reportPath, bool isTerminating)
        {
            try
            {
                string message = $"アプリケーションでエラーが発生しました。\n\n" +
                                 $"エラー: {exception.Message}\n\n" +
                                 $"詳細なエラーレポートが以下に保存されました:\n{reportPath}";

                string caption = isTerminating ?
                                "致命的エラー - アプリケーションは終了します" :
                                "エラー";

                MessageBoxButtons buttons = MessageBoxButtons.OK;
                MessageBoxIcon icon = isTerminating ?
                                      MessageBoxIcon.Error :
                                      MessageBoxIcon.Warning;

                // UI スレッドで MessageBox を表示
                if (Application.OpenForms.Count > 0 && Application.OpenForms[0].InvokeRequired)
                {
                    Application.OpenForms[0].Invoke(new Action(() =>
                    {
                        MessageBox.Show(message, caption, buttons, icon);
                    }));
                }
                else
                {
                    MessageBox.Show(message, caption, buttons, icon);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to show error message box: {ex.Message}");
            }
        }

        #endregion

        #region 手動エラー報告

        /// <summary>
        /// 例外を手動で報告します
        /// </summary>
        /// <param name="exception">報告する例外</param>
        /// <param name="source">例外のソース</param>
        /// <param name="additionalInfo">追加情報（オプション）</param>
        /// <param name="showDialog">エラーダイアログを表示するか</param>
        /// <returns>エラーレポートファイルのパス</returns>
        public string ReportException(
            Exception exception,
            string source,
            string additionalInfo = null,
            bool showDialog = false)
        {
            string reportPath = ReportError(exception, source, additionalInfo);

            Logger.Instance.Error(source, "Error reported manually", exception);

            // カスタムエラーハンドラがある場合は呼び出し
            _customErrorHandler?.Invoke(exception, reportPath);

            // エラーダイアログを表示
            if (showDialog && _showErrorDialog)
            {
                ShowErrorMessageBox(exception, reportPath, false);
            }

            return reportPath;
        }

        #endregion

        #region リソース解放

        /// <summary>
        /// エラーレポーターのリソースを解放します
        /// </summary>
        public void Shutdown()
        {
            lock (_lockObject)
            {
                if (_isInitialized)
                {
                    // グローバル例外ハンドラの解除
                    AppDomain.CurrentDomain.UnhandledException -= OnUnhandledException;
                    Application.ThreadException -= OnThreadException;
                    TaskScheduler.UnobservedTaskException -= OnUnobservedTaskException;

                    _isInitialized = false;

                    // ログにシャットダウン情報を記録
                    Logger.Instance.Info("ErrorReporter", "Error reporter shutdown");
                }
            }
        }

        #endregion
    }
}