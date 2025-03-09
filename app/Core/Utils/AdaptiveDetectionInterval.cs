// GameTranslationOverlay/Core/OCR/AdaptiveDetectionInterval.cs
using System;
using System.Diagnostics;

namespace GameTranslationOverlay.Core.OCR
{
    /// <summary>
    /// テキスト検出の間隔を動的に調整するクラス。
    /// 画面の変化状況やテキスト検出の結果に基づいて最適な間隔を提供します。
    /// </summary>
    public class AdaptiveDetectionInterval
    {
        // 検出間隔の設定範囲
        private readonly int _minIntervalMs;
        private readonly int _maxIntervalMs;
        private readonly int _defaultIntervalMs;

        // 現在の検出間隔
        private int _currentIntervalMs;

        // 最後の変化検出時刻
        private DateTime _lastChangeTime = DateTime.MinValue;

        // テキストが現在表示されているかどうか
        private bool _isTextCurrentlyDisplayed = false;

        // 連続して変化がない回数
        private int _noChangeCount = 0;

        // 連続して変化があった回数
        private int _consecutiveChangeCount = 0;

        // 急速な増減を制限するための安定化パラメータ
        private readonly int _stabilizationFactor;

        // 統計情報
        private int _intervalIncreaseCount = 0;
        private int _intervalDecreaseCount = 0;

        /// <summary>
        /// コンストラクタ
        /// </summary>
        /// <param name="minIntervalMs">最小検出間隔（ミリ秒）</param>
        /// <param name="maxIntervalMs">最大検出間隔（ミリ秒）</param>
        /// <param name="defaultIntervalMs">デフォルト検出間隔（ミリ秒）</param>
        /// <param name="stabilizationFactor">安定化係数（高いほど間隔変更が緩やかになる、デフォルト=3）</param>
        public AdaptiveDetectionInterval(int minIntervalMs = 200, int maxIntervalMs = 2000, int defaultIntervalMs = 500, int stabilizationFactor = 3)
        {
            // パラメータの検証
            if (minIntervalMs <= 0)
                throw new ArgumentException("最小間隔は0より大きい値である必要があります", nameof(minIntervalMs));

            if (maxIntervalMs <= minIntervalMs)
                throw new ArgumentException("最大間隔は最小間隔より大きい値である必要があります", nameof(maxIntervalMs));

            if (defaultIntervalMs < minIntervalMs || defaultIntervalMs > maxIntervalMs)
                throw new ArgumentException("デフォルト間隔は最小間隔と最大間隔の間の値である必要があります", nameof(defaultIntervalMs));

            if (stabilizationFactor <= 0)
                throw new ArgumentException("安定化係数は0より大きい値である必要があります", nameof(stabilizationFactor));

            _minIntervalMs = minIntervalMs;
            _maxIntervalMs = maxIntervalMs;
            _defaultIntervalMs = defaultIntervalMs;
            _currentIntervalMs = defaultIntervalMs;
            _stabilizationFactor = stabilizationFactor;

            Debug.WriteLine($"AdaptiveDetectionInterval: 初期化 (最小間隔={minIntervalMs}ms, 最大間隔={maxIntervalMs}ms, デフォルト間隔={defaultIntervalMs}ms)");
        }

        /// <summary>
        /// 画面変化とテキスト検出状態に基づいて検出間隔を更新
        /// </summary>
        /// <param name="hasScreenChanged">画面に変化があったかどうか</param>
        /// <param name="isTextDetected">テキストが検出されたかどうか</param>
        public void UpdateInterval(bool hasScreenChanged, bool isTextDetected)
        {
            // テキスト表示状態の記録
            bool wasTextDisplayed = _isTextCurrentlyDisplayed;
            _isTextCurrentlyDisplayed = isTextDetected;

            // テキスト表示状態が変わった、または画面に変化があった場合
            if (_isTextCurrentlyDisplayed != wasTextDisplayed || hasScreenChanged)
            {
                // 変化を検出したので時間を記録
                _lastChangeTime = DateTime.Now;
                _noChangeCount = 0;
                _consecutiveChangeCount++;

                // 連続変化回数が閾値を超えたら間隔を短くする
                if (_consecutiveChangeCount >= _stabilizationFactor)
                {
                    DecreaseInterval();
                    _consecutiveChangeCount = 0;
                }
            }
            else
            {
                // 変化がない状態が続いている
                _consecutiveChangeCount = 0;
                _noChangeCount++;

                // 変化がない状態が続いた場合、徐々に間隔を広げる
                if (_noChangeCount >= _stabilizationFactor)
                {
                    TimeSpan timeSinceLastChange = DateTime.Now - _lastChangeTime;

                    // 最後の変化から一定時間経過していれば間隔を広げる
                    if (timeSinceLastChange.TotalSeconds > 3)
                    {
                        IncreaseInterval();
                        _noChangeCount = 0;
                    }
                }
            }

            // デバッグ情報
            Debug.WriteLine($"検出間隔更新: {_currentIntervalMs}ms (変化={hasScreenChanged}, テキスト検出={isTextDetected})");
        }

        /// <summary>
        /// 間隔を増加（速度を遅く）
        /// </summary>
        private void IncreaseInterval()
        {
            int oldInterval = _currentIntervalMs;

            // 現在の間隔に対して10%増加させるが、最大値を超えないようにする
            int increment = Math.Max(50, _currentIntervalMs / 10);
            _currentIntervalMs = Math.Min(_currentIntervalMs + increment, _maxIntervalMs);

            if (_currentIntervalMs != oldInterval)
            {
                _intervalIncreaseCount++;
                Debug.WriteLine($"検出間隔を増加: {oldInterval}ms → {_currentIntervalMs}ms");
            }
        }

        /// <summary>
        /// 間隔を減少（速度を速く）
        /// </summary>
        private void DecreaseInterval()
        {
            int oldInterval = _currentIntervalMs;

            // 現在の間隔に対して20%減少させるが、最小値を下回らないようにする
            int decrement = Math.Max(50, _currentIntervalMs / 5);
            _currentIntervalMs = Math.Max(_currentIntervalMs - decrement, _minIntervalMs);

            if (_currentIntervalMs != oldInterval)
            {
                _intervalDecreaseCount++;
                Debug.WriteLine($"検出間隔を減少: {oldInterval}ms → {_currentIntervalMs}ms");
            }
        }

        /// <summary>
        /// 一時的に間隔を大幅に増加（低活動時）
        /// </summary>
        public void TemporarilyIncreaseInterval()
        {
            int oldInterval = _currentIntervalMs;

            // 間隔を大幅に増加（最大値の75%まで）
            _currentIntervalMs = Math.Min(_currentIntervalMs * 3 / 2, _maxIntervalMs * 3 / 4);

            if (_currentIntervalMs != oldInterval)
            {
                _intervalIncreaseCount++;
                Debug.WriteLine($"検出間隔を一時的に大幅増加: {oldInterval}ms → {_currentIntervalMs}ms");
            }
        }

        /// <summary>
        /// 一時的に間隔を大幅に減少（高活動時）
        /// </summary>
        public void TemporarilyDecreaseInterval()
        {
            int oldInterval = _currentIntervalMs;

            // 間隔を大幅に減少（最小値の150%まで）
            _currentIntervalMs = Math.Max(_currentIntervalMs / 2, (int)(_minIntervalMs * 1.5));

            if (_currentIntervalMs != oldInterval)
            {
                _intervalDecreaseCount++;
                Debug.WriteLine($"検出間隔を一時的に大幅減少: {oldInterval}ms → {_currentIntervalMs}ms");
            }
        }

        /// <summary>
        /// 現在の検出間隔を取得
        /// </summary>
        /// <returns>現在の検出間隔（ミリ秒）</returns>
        public int GetCurrentInterval()
        {
            return _currentIntervalMs;
        }

        /// <summary>
        /// デフォルト値に戻す
        /// </summary>
        public void Reset()
        {
            _currentIntervalMs = _defaultIntervalMs;
            _lastChangeTime = DateTime.MinValue;
            _isTextCurrentlyDisplayed = false;
            _noChangeCount = 0;
            _consecutiveChangeCount = 0;
            Debug.WriteLine($"検出間隔をリセット: {_currentIntervalMs}ms");
        }

        /// <summary>
        /// 統計情報を取得
        /// </summary>
        /// <returns>統計情報を含む文字列</returns>
        public string GetStatistics()
        {
            return string.Format(
                "AdaptiveDetectionInterval 統計:\n" +
                "現在の間隔: {0}ms\n" +
                "最小間隔: {1}ms\n" +
                "最大間隔: {2}ms\n" +
                "デフォルト間隔: {3}ms\n" +
                "間隔増加回数: {4}\n" +
                "間隔減少回数: {5}",
                _currentIntervalMs,
                _minIntervalMs,
                _maxIntervalMs,
                _defaultIntervalMs,
                _intervalIncreaseCount,
                _intervalDecreaseCount
            );
        }
    }
}