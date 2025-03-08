using System;
using System.Diagnostics;

namespace GameTranslationOverlay.Core.Utils
{
    /// <summary>
    /// 検出間隔を動的に調整するためのクラス
    /// 画面の変化率やシステムの状態に応じて最適な検出間隔を提供
    /// </summary>
    public class AdaptiveDetectionInterval
    {
        // 間隔の設定範囲
        private readonly int _minIntervalMs;    // 最小間隔（ミリ秒）
        private readonly int _maxIntervalMs;    // 最大間隔（ミリ秒）
        private readonly int _defaultIntervalMs; // デフォルト間隔（ミリ秒）

        // 現在の間隔と状態管理
        private int _currentIntervalMs;         // 現在の間隔
        private DateTime _lastChangeTime;       // 最後に変化が検出された時刻
        private bool _isTextCurrentlyDisplayed; // テキスト表示状態
        private int _inactivityCounter;         // 非アクティブカウンター
        private int _activityCounter;           // アクティブカウンター

        // アクティビティの記録
        private const int MAX_HISTORY_SIZE = 10; // 履歴サイズ
        private readonly bool[] _activityHistory; // アクティビティ履歴
        private int _historyIndex;              // 履歴インデックス

        // パフォーマンス調整用パラメータ
        private readonly double _increaseRatio;  // 増加率（非アクティブ時）
        private readonly double _decreaseRatio;  // 減少率（アクティブ時）
        private readonly int _activityThreshold; // アクティビティ閾値

        /// <summary>
        /// コンストラクタ
        /// </summary>
        /// <param name="minIntervalMs">最小検出間隔（ミリ秒）</param>
        /// <param name="maxIntervalMs">最大検出間隔（ミリ秒）</param>
        /// <param name="defaultIntervalMs">デフォルト検出間隔（ミリ秒）</param>
        public AdaptiveDetectionInterval(
            int minIntervalMs = 200,
            int maxIntervalMs = 2000,
            int defaultIntervalMs = 500)
        {
            // パラメータの検証と設定
            _minIntervalMs = Math.Max(50, minIntervalMs);
            _maxIntervalMs = Math.Max(_minIntervalMs + 100, maxIntervalMs);
            _defaultIntervalMs = Math.Max(_minIntervalMs, Math.Min(_maxIntervalMs, defaultIntervalMs));

            // 状態の初期化
            _currentIntervalMs = _defaultIntervalMs;
            _lastChangeTime = DateTime.MinValue;
            _isTextCurrentlyDisplayed = false;
            _inactivityCounter = 0;
            _activityCounter = 0;

            // アクティビティ履歴の初期化
            _activityHistory = new bool[MAX_HISTORY_SIZE];
            _historyIndex = 0;

            // パフォーマンス調整パラメータの設定
            _increaseRatio = 1.2;  // 20%増加（非アクティブ時）
            _decreaseRatio = 0.7;  // 30%減少（アクティブ時）
            _activityThreshold = 3; // 3回連続でアクティブ/非アクティブを検出したら調整

            Debug.WriteLine($"AdaptiveDetectionInterval: 初期化 (最小={_minIntervalMs}ms, 最大={_maxIntervalMs}ms, デフォルト={_defaultIntervalMs}ms)");
        }

        /// <summary>
        /// 活動状態に基づいて間隔を更新
        /// </summary>
        /// <param name="hasScreenChanged">画面に変化があったか</param>
        /// <param name="isTextDetected">テキストが検出されたか</param>
        public void UpdateInterval(bool hasScreenChanged, bool isTextDetected)
        {
            // テキスト表示状態の記録
            bool wasTextDisplayed = _isTextCurrentlyDisplayed;
            _isTextCurrentlyDisplayed = isTextDetected;

            // アクティビティの判定
            bool isActive = hasScreenChanged || (isTextDetected != wasTextDisplayed);

            // アクティビティ履歴の更新
            _activityHistory[_historyIndex] = isActive;
            _historyIndex = (_historyIndex + 1) % MAX_HISTORY_SIZE;

            // テキスト表示状態が変わった、または画面に変化があった場合
            if (isActive)
            {
                // アクティブ状態のカウント更新
                _activityCounter++;
                _inactivityCounter = 0;
                _lastChangeTime = DateTime.Now;

                // アクティブカウンターがしきい値を超えたら間隔を短くする
                if (_activityCounter >= _activityThreshold)
                {
                    DecreaseInterval();
                    _activityCounter = 0;
                }
            }
            else
            {
                // 非アクティブ状態のカウント更新
                _inactivityCounter++;
                _activityCounter = 0;

                // 一定時間変化がない場合、徐々に間隔を広げる
                TimeSpan timeSinceLastChange = DateTime.Now - _lastChangeTime;
                if (timeSinceLastChange.TotalSeconds > 5 || _inactivityCounter >= _activityThreshold)
                {
                    IncreaseInterval();
                    _inactivityCounter = 0;
                }
            }
        }

        /// <summary>
        /// 一時的にアクティブ状態として間隔を短くする
        /// </summary>
        public void TemporarilyDecreaseInterval()
        {
            int oldInterval = _currentIntervalMs;
            _currentIntervalMs = Math.Max((int)(_currentIntervalMs * 0.5), _minIntervalMs);

            if (oldInterval != _currentIntervalMs)
            {
                Debug.WriteLine($"AdaptiveDetectionInterval: 一時的に間隔を短縮 {oldInterval}ms → {_currentIntervalMs}ms");
            }
        }

        /// <summary>
        /// 一時的に非アクティブ状態として間隔を長くする
        /// </summary>
        public void TemporarilyIncreaseInterval()
        {
            int oldInterval = _currentIntervalMs;
            _currentIntervalMs = Math.Min((int)(_currentIntervalMs * 1.5), _maxIntervalMs);

            if (oldInterval != _currentIntervalMs)
            {
                Debug.WriteLine($"AdaptiveDetectionInterval: 一時的に間隔を延長 {oldInterval}ms → {_currentIntervalMs}ms");
            }
        }

        /// <summary>
        /// アクティブ状態に基づいて間隔を短くする（内部実装）
        /// </summary>
        private void DecreaseInterval()
        {
            int oldInterval = _currentIntervalMs;
            _currentIntervalMs = Math.Max((int)(_currentIntervalMs * _decreaseRatio), _minIntervalMs);

            if (oldInterval != _currentIntervalMs)
            {
                Debug.WriteLine($"AdaptiveDetectionInterval: 間隔を短縮 {oldInterval}ms → {_currentIntervalMs}ms");
            }
        }

        /// <summary>
        /// 非アクティブ状態に基づいて間隔を長くする（内部実装）
        /// </summary>
        private void IncreaseInterval()
        {
            int oldInterval = _currentIntervalMs;
            _currentIntervalMs = Math.Min((int)(_currentIntervalMs * _increaseRatio), _maxIntervalMs);

            if (oldInterval != _currentIntervalMs)
            {
                Debug.WriteLine($"AdaptiveDetectionInterval: 間隔を延長 {oldInterval}ms → {_currentIntervalMs}ms");
            }
        }

        /// <summary>
        /// システム負荷に基づいて間隔を調整
        /// </summary>
        /// <param name="cpuUsage">現在のCPU使用率（0-100）</param>
        public void AdjustForSystemLoad(float cpuUsage)
        {
            // CPU使用率が高い場合は間隔を長くする
            if (cpuUsage > 70)
            {
                TemporarilyIncreaseInterval();
            }
            // CPU使用率が低い場合は間隔を短くする（ただし最近アクティブだった場合のみ）
            else if (cpuUsage < 30 && CountActiveHistory() > MAX_HISTORY_SIZE / 2)
            {
                TemporarilyDecreaseInterval();
            }
        }

        /// <summary>
        /// 履歴内のアクティブ状態の数をカウント
        /// </summary>
        private int CountActiveHistory()
        {
            int count = 0;
            for (int i = 0; i < MAX_HISTORY_SIZE; i++)
            {
                if (_activityHistory[i])
                {
                    count++;
                }
            }
            return count;
        }

        /// <summary>
        /// 現在の検出間隔を取得
        /// </summary>
        public int GetCurrentInterval()
        {
            return _currentIntervalMs;
        }

        /// <summary>
        /// デフォルト検出間隔に戻す
        /// </summary>
        public void RestoreDefaultInterval()
        {
            int oldInterval = _currentIntervalMs;
            _currentIntervalMs = _defaultIntervalMs;

            if (oldInterval != _currentIntervalMs)
            {
                Debug.WriteLine($"AdaptiveDetectionInterval: デフォルト間隔に戻す {oldInterval}ms → {_currentIntervalMs}ms");
            }
        }

        /// <summary>
        /// 状態をデフォルトにリセット
        /// </summary>
        public void Reset()
        {
            _currentIntervalMs = _defaultIntervalMs;
            _inactivityCounter = 0;
            _activityCounter = 0;
            _historyIndex = 0;

            for (int i = 0; i < MAX_HISTORY_SIZE; i++)
            {
                _activityHistory[i] = false;
            }

            Debug.WriteLine($"AdaptiveDetectionInterval: 状態をリセットしました (間隔={_currentIntervalMs}ms)");
        }
    }
}