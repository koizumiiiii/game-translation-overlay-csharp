using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace GameTranslationOverlay.Core.Diagnostics
{
    /// <summary>
    /// アプリケーションログを管理するクラス
    /// </summary>
    public class Logger : ILogger
    {
        #region シングルトンパターン

        private static readonly Lazy<Logger> _instance = new Lazy<Logger>(() => new Logger());

        /// <summary>
        /// Loggerのインスタンスを取得します
        /// </summary>
        public static Logger Instance => _instance.Value;

        #endregion

        #region ログレベル

        /// <summary>
        /// ログレベルを定義します
        /// </summary>
        public enum LogLevel
        {
            Debug = 0,
            Info = 1,
            Warning = 2,
            Error = 3,
            Fatal = 4
        }

        #endregion

        #region プロパティ

        private readonly object _lockObject = new object();
        private StreamWriter _logWriter;
        private string _logFilePath;
        private long _currentLogFileSize;
        private long _maxLogFileSize = 5 * 1024 * 1024; // 5MB
        private int _maxLogFiles = 5;
        private LogLevel _minimumLogLevel = LogLevel.Info; // デフォルトはInfo以上
        private bool _isInitialized = false;

        /// <summary>
        /// ログに記録する最小レベルを設定または取得します
        /// </summary>
        public LogLevel MinimumLogLevel
        {
            get => _minimumLogLevel;
            set => _minimumLogLevel = value;
        }

        /// <summary>
        /// ログファイルの最大サイズ（バイト）
        /// </summary>
        public long MaxLogFileSize
        {
            get => _maxLogFileSize;
            set => _maxLogFileSize = value;
        }

        /// <summary>
        /// 保持する最大ログファイル数
        /// </summary>
        public int MaxLogFiles
        {
            get => _maxLogFiles;
            set => _maxLogFiles = value;
        }

        #endregion

        #region コンストラクタ

        private Logger()
        {
            // 明示的な初期化が必要
        }

        #endregion

        #region 初期化

        /// <summary>
        /// ロガーを初期化します
        /// </summary>
        /// <param name="logDirectoryPath">ログファイルを保存するディレクトリパス</param>
        /// <param name="minimumLogLevel">記録する最小ログレベル</param>
        public void Initialize(string logDirectoryPath, LogLevel minimumLogLevel = LogLevel.Info)
        {
            lock (_lockObject)
            {
                try
                {
                    _minimumLogLevel = minimumLogLevel;

                    // ログディレクトリが存在しない場合は作成
                    if (!Directory.Exists(logDirectoryPath))
                    {
                        Directory.CreateDirectory(logDirectoryPath);
                    }

                    // ログファイル名の設定（現在の日付を含む）
                    string logFileName = $"GameTranslationOverlay_{DateTime.Now:yyyyMMdd}.log";
                    _logFilePath = Path.Combine(logDirectoryPath, logFileName);

                    // ファイルが既に存在する場合はサイズをチェック
                    if (File.Exists(_logFilePath))
                    {
                        FileInfo fileInfo = new FileInfo(_logFilePath);
                        _currentLogFileSize = fileInfo.Length;
                    }
                    else
                    {
                        _currentLogFileSize = 0;
                    }

                    // ログファイルを開く（追記モード）
                    _logWriter = new StreamWriter(_logFilePath, true, Encoding.UTF8)
                    {
                        AutoFlush = true
                    };

                    _isInitialized = true;

                    // 初期化成功のログを記録
                    LogInternal(LogLevel.Info, "Logger", "Logger initialized successfully", null);
                }
                catch (Exception ex)
                {
                    // 初期化エラーはコンソールに出力
                    System.Diagnostics.Debug.WriteLine($"Failed to initialize logger: {ex.Message}");
                    _isInitialized = false;
                }
            }
        }

        #endregion

        #region ILogger インターフェース実装

        /// <summary>
        /// デバッグレベルのログを記録します
        /// </summary>
        /// <param name="message">ログメッセージ</param>
        public void LogDebug(string message)
        {
            Log(LogLevel.Debug, "Application", message);
        }

        /// <summary>
        /// デバッグレベルのログをカテゴリ付きで記録します
        /// </summary>
        /// <param name="category">ログカテゴリ</param>
        /// <param name="message">ログメッセージ</param>
        public void LogDebug(string category, string message)
        {
            Log(LogLevel.Debug, category, message);
        }

        /// <summary>
        /// 情報レベルのログを記録します
        /// </summary>
        /// <param name="message">ログメッセージ</param>
        public void LogInfo(string message)
        {
            Log(LogLevel.Info, "Application", message);
        }

        /// <summary>
        /// 警告レベルのログを記録します
        /// </summary>
        /// <param name="message">ログメッセージ</param>
        public void LogWarning(string message)
        {
            Log(LogLevel.Warning, "Application", message);
        }

        /// <summary>
        /// エラーレベルのログを記録します
        /// </summary>
        /// <param name="message">ログメッセージ</param>
        public void LogError(string message)
        {
            Log(LogLevel.Error, "Application", message);
        }

        /// <summary>
        /// 例外を含むエラーレベルのログを記録します
        /// </summary>
        /// <param name="message">ログメッセージ</param>
        /// <param name="exception">例外オブジェクト</param>
        public void LogError(string message, Exception exception)
        {
            Log(LogLevel.Error, "Application", message, exception);
        }

        #endregion

        #region ログメソッド

        /// <summary>
        /// 情報レベルのログを記録します
        /// </summary>
        /// <param name="source">ログソース（クラス名など）</param>
        /// <param name="message">ログメッセージ</param>
        public void Info(string source, string message)
        {
            Log(LogLevel.Info, source, message);
        }

        /// <summary>
        /// 情報レベルのログを記録します（LogInfo という名前でも呼び出し可能）
        /// </summary>
        /// <param name="message">ログメッセージ</param>
        /// <param name="exception">例外オブジェクト（省略可）</param>
        public void LogInfo(string message, Exception exception)
        {
            Log(LogLevel.Info, "Application", message, exception);
        }

        /// <summary>
        /// 警告レベルのログを記録します
        /// </summary>
        /// <param name="source">ログソース（クラス名など）</param>
        /// <param name="message">ログメッセージ</param>
        public void Warning(string source, string message)
        {
            Log(LogLevel.Warning, source, message);
        }

        /// <summary>
        /// 警告レベルのログを記録します（LogWarning という名前でも呼び出し可能）
        /// </summary>
        /// <param name="message">ログメッセージ</param>
        /// <param name="exception">例外オブジェクト（省略可）</param>
        public void LogWarning(string message, Exception exception)
        {
            Log(LogLevel.Warning, "Application", message, exception);
        }

        /// <summary>
        /// エラーレベルのログを記録します
        /// </summary>
        /// <param name="source">ログソース（クラス名など）</param>
        /// <param name="message">ログメッセージ</param>
        /// <param name="exception">例外オブジェクト（省略可）</param>
        public void Error(string source, string message, Exception exception = null)
        {
            Log(LogLevel.Error, source, message, exception);
        }

        /// <summary>
        /// 致命的エラーレベルのログを記録します
        /// </summary>
        /// <param name="source">ログソース（クラス名など）</param>
        /// <param name="message">ログメッセージ</param>
        /// <param name="exception">例外オブジェクト（省略可）</param>
        public void Fatal(string source, string message, Exception exception = null)
        {
            Log(LogLevel.Fatal, source, message, exception);
        }

        /// <summary>
        /// 指定されたレベルでログを記録します
        /// </summary>
        /// <param name="level">ログレベル</param>
        /// <param name="source">ログソース（クラス名など）</param>
        /// <param name="message">ログメッセージ</param>
        /// <param name="exception">例外オブジェクト（省略可）</param>
        public void Log(LogLevel level, string source, string message, Exception exception = null)
        {
            if (level < _minimumLogLevel)
                return;

            LogInternal(level, source, message, exception);
        }

        /// <summary>
        /// 内部ログ記録処理
        /// </summary>
        private void LogInternal(LogLevel level, string source, string message, Exception exception)
        {
            if (!_isInitialized)
            {
                // 初期化されていない場合はデバッグ出力のみ
                System.Diagnostics.Debug.WriteLine($"[{level}] {source}: {message}");
                if (exception != null)
                {
                    System.Diagnostics.Debug.WriteLine($"Exception: {exception}");
                }
                return;
            }

            lock (_lockObject)
            {
                try
                {
                    // ログローテーションが必要かチェック
                    CheckRotation();

                    // ログメッセージのフォーマット
                    string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                    string threadId = Thread.CurrentThread.ManagedThreadId.ToString();
                    string levelStr = level.ToString().ToUpper().PadRight(7);

                    // 基本ログメッセージの構築
                    StringBuilder logBuilder = new StringBuilder();
                    logBuilder.AppendLine($"[{timestamp}] [{threadId}] [{levelStr}] [{source}] {message}");

                    // 例外情報がある場合は追加
                    if (exception != null)
                    {
                        logBuilder.AppendLine($"Exception: {exception.GetType().Name}: {exception.Message}");
                        logBuilder.AppendLine($"StackTrace: {exception.StackTrace}");

                        // 内部例外がある場合はそれも追加
                        Exception innerEx = exception.InnerException;
                        while (innerEx != null)
                        {
                            logBuilder.AppendLine($"Inner Exception: {innerEx.GetType().Name}: {innerEx.Message}");
                            logBuilder.AppendLine($"StackTrace: {innerEx.StackTrace}");
                            innerEx = innerEx.InnerException;
                        }
                    }

                    string logEntry = logBuilder.ToString();

                    // ログをファイルに書き込み
                    _logWriter.Write(logEntry);
                    _currentLogFileSize += Encoding.UTF8.GetByteCount(logEntry);

                    // 重要度の高いログはデバッグ出力にも表示
                    if (level >= LogLevel.Warning)
                    {
                        System.Diagnostics.Debug.WriteLine(logEntry);
                    }
                }
                catch (Exception ex)
                {
                    // ログ記録中のエラーはデバッグ出力に記録
                    System.Diagnostics.Debug.WriteLine($"Error writing to log: {ex.Message}");
                }
            }
        }

        #endregion

        #region ログローテーション

        /// <summary>
        /// ログローテーションが必要かチェックし、必要なら実行します
        /// </summary>
        private void CheckRotation()
        {
            if (_currentLogFileSize >= _maxLogFileSize)
            {
                RotateLogFiles();
            }
        }

        /// <summary>
        /// ログファイルのローテーションを実行します
        /// </summary>
        private void RotateLogFiles()
        {
            try
            {
                // 現在のログファイルを閉じる
                _logWriter.Close();
                _logWriter.Dispose();

                // 既存のローテーションログファイルを移動
                for (int i = _maxLogFiles - 1; i > 0; i--)
                {
                    string sourceFile = $"{_logFilePath}.{i - 1}";
                    string targetFile = $"{_logFilePath}.{i}";

                    if (File.Exists(sourceFile))
                    {
                        if (File.Exists(targetFile))
                        {
                            File.Delete(targetFile);
                        }
                        File.Move(sourceFile, targetFile);
                    }
                }

                // 現在のログファイルを.1に移動
                if (File.Exists(_logFilePath))
                {
                    string targetFile = $"{_logFilePath}.1";
                    if (File.Exists(targetFile))
                    {
                        File.Delete(targetFile);
                    }
                    File.Move(_logFilePath, targetFile);
                }

                // 新しいログファイルを作成
                _logWriter = new StreamWriter(_logFilePath, true, Encoding.UTF8)
                {
                    AutoFlush = true
                };
                _currentLogFileSize = 0;

                // ローテーション情報をログに記録
                LogInternal(LogLevel.Info, "Logger", "Log rotation performed", null);
            }
            catch (Exception ex)
            {
                // ローテーション中のエラーはデバッグ出力に記録
                System.Diagnostics.Debug.WriteLine($"Error during log rotation: {ex.Message}");

                // 最低限の復旧処理
                try
                {
                    if (_logWriter == null || _logWriter.BaseStream == null)
                    {
                        _logWriter = new StreamWriter(_logFilePath, true, Encoding.UTF8)
                        {
                            AutoFlush = true
                        };
                        _currentLogFileSize = 0;
                    }
                }
                catch
                {
                    // 復旧失敗の場合はデバッグ出力のみ
                    System.Diagnostics.Debug.WriteLine("Failed to recover logger after rotation error");
                }
            }
        }

        #endregion

        #region リソース解放

        /// <summary>
        /// ロガーのリソースを解放します
        /// </summary>
        public void Shutdown()
        {
            lock (_lockObject)
            {
                if (_logWriter != null)
                {
                    try
                    {
                        LogInternal(LogLevel.Info, "Logger", "Logger shutdown", null);
                        _logWriter.Flush();
                        _logWriter.Close();
                        _logWriter.Dispose();
                        _logWriter = null;
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error shutting down logger: {ex.Message}");
                    }
                }
                _isInitialized = false;
            }
        }

        #endregion
    }
}