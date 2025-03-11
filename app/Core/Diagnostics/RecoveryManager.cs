using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Threading;

namespace GameTranslationOverlay.Core.Diagnostics
{
    /// <summary>
    /// アプリケーションの回復機能を管理するクラス
    /// </summary>
    public class RecoveryManager
    {
        #region シングルトンパターン

        private static readonly Lazy<RecoveryManager> _instance = new Lazy<RecoveryManager>(() => new RecoveryManager());

        /// <summary>
        /// RecoveryManagerのインスタンスを取得します
        /// </summary>
        public static RecoveryManager Instance => _instance.Value;

        #endregion

        #region プロパティとフィールド

        private readonly object _lockObject = new object();
        private readonly Dictionary<string, ErrorTracker> _errorTrackers = new Dictionary<string, ErrorTracker>();
        private bool _isSafeModeEnabled = false;
        private bool _isInitialized = false;
        private readonly int _defaultErrorThreshold = 3;
        private readonly TimeSpan _defaultErrorTimeWindow = TimeSpan.FromMinutes(5);
        private string _recoveryLogPath;
        private readonly List<RecoveryAction> _recoveryActions = new List<RecoveryAction>();

        /// <summary>
        /// セーフモードが有効かどうかを取得します
        /// </summary>
        public bool IsSafeModeEnabled => _isSafeModeEnabled;

        /// <summary>
        /// セーフモードが有効になった時に発生するイベント
        /// </summary>
        public event EventHandler<SafeModeEventArgs> SafeModeEnabled;

        /// <summary>
        /// セーフモードが無効になった時に発生するイベント
        /// </summary>
        public event EventHandler SafeModeDisabled;

        #endregion

        #region 内部クラス

        /// <summary>
        /// エラーの追跡情報を管理するクラス
        /// </summary>
        private class ErrorTracker
        {
            public int ErrorCount { get; private set; }
            public DateTime FirstErrorTime { get; private set; }
            public DateTime LastErrorTime { get; private set; }
            public int Threshold { get; set; }
            public TimeSpan TimeWindow { get; set; }

            public ErrorTracker(int threshold, TimeSpan timeWindow)
            {
                ErrorCount = 0;
                Threshold = threshold;
                TimeWindow = timeWindow;
            }

            public bool AddError()
            {
                DateTime now = DateTime.Now;

                // 初めてのエラー、または時間枠を超えた場合はカウンターをリセット
                if (ErrorCount == 0 || (now - LastErrorTime) > TimeWindow)
                {
                    ErrorCount = 1;
                    FirstErrorTime = now;
                }
                else
                {
                    ErrorCount++;
                }

                LastErrorTime = now;

                // 閾値を超えたかどうかを返す
                return ErrorCount >= Threshold;
            }

            public void Reset()
            {
                ErrorCount = 0;
            }
        }

        /// <summary>
        /// セーフモードイベントの引数クラス
        /// </summary>
        public class SafeModeEventArgs : EventArgs
        {
            public string Reason { get; }
            public string Source { get; }
            public DateTime Timestamp { get; }

            public SafeModeEventArgs(string source, string reason)
            {
                Source = source;
                Reason = reason;
                Timestamp = DateTime.Now;
            }
        }

        /// <summary>
        /// 回復アクションを定義するクラス
        /// </summary>
        public class RecoveryAction
        {
            public string Name { get; }
            public Func<Exception, bool> Condition { get; }
            public Action<Exception> Action { get; }

            public RecoveryAction(string name, Func<Exception, bool> condition, Action<Exception> action)
            {
                Name = name;
                Condition = condition;
                Action = action;
            }
        }

        #endregion

        #region コンストラクタ

        private RecoveryManager()
        {
            // 明示的な初期化が必要
        }

        #endregion

        #region 初期化

        /// <summary>
        /// 回復マネージャーを初期化します
        /// </summary>
        /// <param name="recoveryLogPath">回復ログを保存するパス</param>
        public void Initialize(string recoveryLogPath)
        {
            lock (_lockObject)
            {
                try
                {
                    _recoveryLogPath = recoveryLogPath;

                    // 回復ログディレクトリが存在しない場合は作成
                    string directory = System.IO.Path.GetDirectoryName(_recoveryLogPath);
                    if (!System.IO.Directory.Exists(directory))
                    {
                        System.IO.Directory.CreateDirectory(directory);
                    }

                    _isInitialized = true;
                    Logger.Instance.Info("RecoveryManager", "Recovery manager initialized successfully");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to initialize recovery manager: {ex.Message}");
                    _isInitialized = false;
                }
            }
        }

        #endregion

        #region エラー追跡

        /// <summary>
        /// エラーを記録し、必要に応じて回復アクションを実行します
        /// </summary>
        /// <param name="source">エラーのソース</param>
        /// <param name="exception">例外情報</param>
        /// <param name="errorThreshold">エラーの閾値（省略可）</param>
        /// <param name="timeWindow">エラーの時間枠（省略可）</param>
        /// <returns>回復アクションが実行されたかどうか</returns>
        public bool RecordError(string source, Exception exception, int? errorThreshold = null, TimeSpan? timeWindow = null)
        {
            if (!_isInitialized)
            {
                System.Diagnostics.Debug.WriteLine("Recovery manager is not initialized");
                return false;
            }

            lock (_lockObject)
            {
                try
                {
                    // エラーをログに記録
                    Logger.Instance.Error(source, "Error recorded by recovery manager", exception);

                    // エラートラッカーを取得または作成
                    if (!_errorTrackers.ContainsKey(source))
                    {
                        int threshold = errorThreshold ?? _defaultErrorThreshold;
                        TimeSpan window = timeWindow ?? _defaultErrorTimeWindow;
                        _errorTrackers[source] = new ErrorTracker(threshold, window);
                    }
                    else if (errorThreshold.HasValue || timeWindow.HasValue)
                    {
                        // 既存のトラッカーがあり、新しい閾値や時間枠が指定された場合は更新
                        if (errorThreshold.HasValue)
                        {
                            _errorTrackers[source].Threshold = errorThreshold.Value;
                        }
                        if (timeWindow.HasValue)
                        {
                            _errorTrackers[source].TimeWindow = timeWindow.Value;
                        }
                    }

                    // エラーを追加し、閾値を超えたかどうかを確認
                    bool thresholdExceeded = _errorTrackers[source].AddError();

                    // 回復アクションの実行
                    bool actionTaken = false;
                    if (thresholdExceeded)
                    {
                        actionTaken = ExecuteRecoveryActions(source, exception);

                        // エラー状態の重大度に応じてセーフモードを有効化
                        if (!_isSafeModeEnabled && ShouldEnableSafeMode(source, exception))
                        {
                            EnableSafeMode(source, $"Multiple errors ({_errorTrackers[source].ErrorCount}) detected from {source}");
                        }
                    }

                    // 回復ログに記録
                    LogRecoveryAction(source, exception, thresholdExceeded, actionTaken);

                    return actionTaken;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error in RecordError: {ex.Message}");
                    return false;
                }
            }
        }

        /// <summary>
        /// 特定のソースのエラーカウンターをリセットします
        /// </summary>
        /// <param name="source">エラーソース</param>
        public void ResetErrorCounter(string source)
        {
            lock (_lockObject)
            {
                if (_errorTrackers.ContainsKey(source))
                {
                    _errorTrackers[source].Reset();
                    Logger.Instance.Info("RecoveryManager", $"Error counter reset for source: {source}");
                }
            }
        }

        /// <summary>
        /// すべてのエラーカウンターをリセットします
        /// </summary>
        public void ResetAllErrorCounters()
        {
            lock (_lockObject)
            {
                foreach (var tracker in _errorTrackers.Values)
                {
                    tracker.Reset();
                }
                Logger.Instance.Info("RecoveryManager", "All error counters reset");
            }
        }

        #endregion

        #region セーフモード

        /// <summary>
        /// セーフモードを有効にするかどうかを判断します
        /// </summary>
        private bool ShouldEnableSafeMode(string source, Exception exception)
        {
            // 複数回のエラーが発生している場合にセーフモードを有効化
            if (_errorTrackers.ContainsKey(source) && _errorTrackers[source].ErrorCount >= _errorTrackers[source].Threshold)
            {
                return true;
            }

            // 特定の重大なエラータイプの場合に即座にセーフモードを有効化
            if (exception is OutOfMemoryException ||
                exception is StackOverflowException ||
                exception is System.Threading.ThreadAbortException)
            {
                return true;
            }

            // 複数のソースでエラーが発生している場合
            int sourcesWithErrors = _errorTrackers.Count(t => t.Value.ErrorCount > 0);
            if (sourcesWithErrors >= 3)
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// セーフモードを有効にします
        /// </summary>
        /// <param name="source">セーフモードを有効にした原因のソース</param>
        /// <param name="reason">セーフモードを有効にした理由</param>
        public void EnableSafeMode(string source, string reason)
        {
            lock (_lockObject)
            {
                if (!_isSafeModeEnabled)
                {
                    _isSafeModeEnabled = true;
                    Logger.Instance.Warning("RecoveryManager", $"Safe mode enabled. Source: {source}, Reason: {reason}");

                    // セーフモード有効化イベントを発火
                    SafeModeEnabled?.Invoke(this, new SafeModeEventArgs(source, reason));

                    // 回復ログに記録
                    LogSafeModeChange(true, source, reason);
                }
            }
        }

        /// <summary>
        /// セーフモードを無効にします
        /// </summary>
        public void DisableSafeMode()
        {
            lock (_lockObject)
            {
                if (_isSafeModeEnabled)
                {
                    _isSafeModeEnabled = false;
                    Logger.Instance.Info("RecoveryManager", "Safe mode disabled");

                    // セーフモード無効化イベントを発火
                    SafeModeDisabled?.Invoke(this, EventArgs.Empty);

                    // 回復ログに記録
                    LogSafeModeChange(false, "Manual", "Safe mode manually disabled");
                }
            }
        }

        #endregion

        #region 回復アクション

        /// <summary>
        /// 回復アクションを登録します
        /// </summary>
        /// <param name="name">アクション名</param>
        /// <param name="condition">アクションの実行条件</param>
        /// <param name="action">実行するアクション</param>
        public void RegisterRecoveryAction(string name, Func<Exception, bool> condition, Action<Exception> action)
        {
            lock (_lockObject)
            {
                _recoveryActions.Add(new RecoveryAction(name, condition, action));
                Logger.Instance.Info("RecoveryManager", $"Recovery action registered: {name}");
            }
        }

        /// <summary>
        /// 回復アクションを実行します
        /// </summary>
        /// <param name="source">エラーソース</param>
        /// <param name="exception">例外情報</param>
        /// <returns>アクションが実行されたかどうか</returns>
        private bool ExecuteRecoveryActions(string source, Exception exception)
        {
            bool actionTaken = false;

            // 登録されている回復アクションを順番に確認
            foreach (var recoveryAction in _recoveryActions)
            {
                try
                {
                    // 条件を満たす場合にアクションを実行
                    if (recoveryAction.Condition(exception))
                    {
                        Logger.Instance.Info("RecoveryManager", $"Executing recovery action: {recoveryAction.Name} for source: {source}");
                        recoveryAction.Action(exception);
                        actionTaken = true;
                    }
                }
                catch (Exception ex)
                {
                    Logger.Instance.Error("RecoveryManager", $"Error executing recovery action {recoveryAction.Name}", ex);
                }
            }

            return actionTaken;
        }

        #endregion

        #region ロギング

        /// <summary>
        /// 回復アクションをログに記録します
        /// </summary>
        private void LogRecoveryAction(string source, Exception exception, bool thresholdExceeded, bool actionTaken)
        {
            if (string.IsNullOrEmpty(_recoveryLogPath))
                return;

            try
            {
                using (StreamWriter writer = new StreamWriter(_recoveryLogPath, true))
                {
                    StringBuilder logEntry = new StringBuilder();
                    logEntry.AppendLine($"[{DateTime.Now}] Source: {source}");
                    logEntry.AppendLine($"Exception: {exception.GetType().Name}: {exception.Message}");

                    if (thresholdExceeded)
                    {
                        logEntry.AppendLine($"Error threshold exceeded. Count: {_errorTrackers[source].ErrorCount}");
                    }

                    if (actionTaken)
                    {
                        logEntry.AppendLine("Recovery action(s) taken");
                    }

                    logEntry.AppendLine(new string('-', 40));
                    writer.Write(logEntry.ToString());
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error writing to recovery log: {ex.Message}");
            }
        }

        /// <summary>
        /// セーフモードの変更をログに記録します
        /// </summary>
        private void LogSafeModeChange(bool enabled, string source, string reason)
        {
            if (string.IsNullOrEmpty(_recoveryLogPath))
                return;

            try
            {
                using (StreamWriter writer = new StreamWriter(_recoveryLogPath, true))
                {
                    StringBuilder logEntry = new StringBuilder();
                    logEntry.AppendLine($"[{DateTime.Now}] Safe Mode {(enabled ? "Enabled" : "Disabled")}");
                    logEntry.AppendLine($"Source: {source}");
                    logEntry.AppendLine($"Reason: {reason}");
                    logEntry.AppendLine(new string('-', 40));
                    writer.Write(logEntry.ToString());
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error writing to recovery log: {ex.Message}");
            }
        }

        #endregion

        #region 標準回復アクション

        /// <summary>
        /// 標準的な回復アクションを登録します
        /// </summary>
        public void RegisterStandardRecoveryActions()
        {
            // メモリ不足エラーの処理
            RegisterRecoveryAction(
                "Memory Cleanup",
                ex => ex is OutOfMemoryException || ex.Message.Contains("memory"),
                ex => {
                    Logger.Instance.Warning("RecoveryManager", "Performing memory cleanup");
                    GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true, true);
                }
            );

            // I/O エラーの処理
            RegisterRecoveryAction(
                "I/O Error Handling",
                ex => ex is IOException || ex is UnauthorizedAccessException,
                ex => {
                    Logger.Instance.Warning("RecoveryManager", "Handling I/O error");
                    Thread.Sleep(1000); // 短い待機で再試行のタイミングをずらす
                }
            );

            // ネットワークエラーの処理
            RegisterRecoveryAction(
                "Network Error Handling",
                ex => ex is System.Net.WebException || ex is System.Net.Sockets.SocketException,
                ex => {
                    Logger.Instance.Warning("RecoveryManager", "Handling network error");
                    // 自動再接続ロジックなどを実行
                }
            );

            // UI関連エラーの処理
            RegisterRecoveryAction(
                "UI Error Handling",
                ex => ex is System.ComponentModel.InvalidAsynchronousStateException ||
                      ex is System.InvalidOperationException && ex.Message.Contains("thread"),
                ex => {
                    Logger.Instance.Warning("RecoveryManager", "Handling UI thread error");
                    // UIスレッドの状態をリセットするアクションなど
                }
            );

            Logger.Instance.Info("RecoveryManager", "Standard recovery actions registered");
        }

        #endregion
    }
}