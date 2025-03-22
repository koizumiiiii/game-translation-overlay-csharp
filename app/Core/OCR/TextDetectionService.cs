using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using GameTranslationOverlay.Core.OCR;
using GameTranslationOverlay.Core.Translation.Services;
using GameTranslationOverlay.Core.Utils;
using GameTranslationOverlay.Utils;

namespace GameTranslationOverlay.Core.OCR
{
    /// <summary>
    /// テキスト検出サービス - 改良版
    /// ゲーム画面からのテキスト検出と通知を管理
    /// </summary>
    public class TextDetectionService : IDisposable
    {
        // OCRエンジン
        private readonly IOcrEngine _ocrEngine;

        // タイマー関連
        private Timer _detectionTimer;
        private int _detectionInterval = 1000; // 1秒ごとに検出（調整可能）

        // テキスト検出関連
        private List<TextRegion> _detectedRegions = new List<TextRegion>();
        private IntPtr _targetWindowHandle;
        private float _minimumConfidence = 0.6f; // 最低信頼度（これより低いテキストは無視）

        // 差分検出関連
        private readonly DifferenceDetector _differenceDetector;
        private bool _useDifferenceDetection = true;

        // 状態管理
        private bool _disposed = false;
        private bool _isRunning = false;
        private string _lastErrorMessage = string.Empty;
        private int _consecutiveErrors = 0;
        private const int MAX_CONSECUTIVE_ERRORS = 5;

        // テキスト領域が検出されなかった連続回数
        private int _noRegionsDetectedCount = 0;

        // テキスト領域が検出されなくなったと判断するしきい値
        private const int NO_REGIONS_THRESHOLD = 3;

        // テキスト変更検知
        private string _lastDetectedText = string.Empty;
        private DateTime _lastTextChangeTime = DateTime.MinValue;
        private bool _useChangeDetection = true;

        // パフォーマンス調整
        private bool _dynamicIntervalEnabled = true;
        private int _minDetectionInterval = 300;  // 最小感覚（ミリ秒）
        private int _maxDetectionInterval = 2000; // 最大間隔（ミリ秒）

        private readonly AdaptiveDetectionInterval _adaptiveInterval;

        private readonly SmartOcrRegionManager _regionManager;
        private bool _useSmartRegions = true;

        // 処理のキャンセル制御
        private bool _processingActive = false;

        // リソースモニタリング
        private readonly Stopwatch _processingStopwatch = new Stopwatch();
        private long _totalProcessingTime = 0;
        private int _processedFrames = 0;
        private DateTime _lastResourceCheckTime = DateTime.MinValue;
        private const int RESOURCE_CHECK_INTERVAL_MS = 10000; // 10秒ごとにリソースチェック

        private string _currentProcessingStatus = "アイドル";
        private int _progressPercentage = 0;

        /// <summary>
        /// 現在の処理状態を取得
        /// </summary>
        public string CurrentStatus => _currentProcessingStatus;

        /// <summary>
        /// 進行状況（パーセント）を取得
        /// </summary>
        public int ProgressPercentage => _progressPercentage;

        /// <summary>
        /// 処理状態を更新
        /// </summary>
        /// <param name="status">新しい状態</param>
        /// <param name="progressPercentage">進行状況（0-100）</param>
        private void UpdateProcessingStatus(string status, int progressPercentage = -1)
        {
            _currentProcessingStatus = status;
            if (progressPercentage >= 0)
            {
                _progressPercentage = Math.Min(100, Math.Max(0, progressPercentage));
            }

            // 処理状態の変更を通知するイベントを追加できます（必要に応じて）
            // OnProcessingStatusChanged?.Invoke(this, new StatusChangedEventArgs(_currentProcessingStatus, _progressPercentage));

            Debug.WriteLine($"処理状態: {status}, 進行状況: {(_progressPercentage >= 0 ? _progressPercentage + "%" : "不明")}");
        }

        /// <summary>
        /// 処理状態変更イベント - 進行状態表示UIとの連携用
        /// </summary>
        // public event EventHandler<StatusChangedEventArgs> OnProcessingStatusChanged;

        /// <summary>
        /// 翻訳先言語の設定（最適化のため）
        /// </summary>
        public string TargetLanguage { get; set; } = "ja";

        /// <summary>
        /// IsRunningプロパティ - サービスが動作中かどうか
        /// </summary>
        public bool IsRunning => _isRunning;

        /// <summary>
        /// 検出間隔（ミリ秒）
        /// </summary>
        public int DetectionInterval
        {
            get => _detectionInterval;
            set
            {
                if (value >= _minDetectionInterval && value <= _maxDetectionInterval)
                {
                    _detectionInterval = value;
                    if (_detectionTimer != null)
                    {
                        _detectionTimer.Interval = _detectionInterval;
                    }
                    Debug.WriteLine($"検出間隔を {_detectionInterval}ms に設定しました");
                }
            }
        }

        /// <summary>
        /// コンストラクタ
        /// </summary>
        /// <param name="ocrEngine">OCRエンジン</param>
        public TextDetectionService(IOcrEngine ocrEngine)
        {
            _ocrEngine = ocrEngine ?? throw new ArgumentNullException(nameof(ocrEngine));

            // 定期的なテキスト検出用タイマー
            _detectionTimer = new Timer
            {
                Interval = _detectionInterval,
                Enabled = false
            };
            _detectionTimer.Tick += DetectionTimer_Tick;

            // 差分検出器の初期化
            _differenceDetector = new DifferenceDetector();

            // アダプティブ間隔調整の初期化
            _adaptiveInterval = new AdaptiveDetectionInterval(_minDetectionInterval, _maxDetectionInterval, _detectionInterval);

            // スマートOCR領域マネージャーの初期化
            _regionManager = new SmartOcrRegionManager(new Size(3, 3), 3, 5);

            Debug.WriteLine("TextDetectionService: 初期化されました");
        }

        // スマート領域検出の有効/無効を切り替えるメソッドを追加
        /// <summary>
        /// スマート領域検出の有効/無効を設定
        /// </summary>
        /// <param name="enable">有効にする場合はtrue</param>
        public void EnableSmartRegions(bool enable)
        {
            _useSmartRegions = enable;
            Debug.WriteLine($"スマート領域検出を {(_useSmartRegions ? "有効" : "無効")} にしました");

            // 無効化した場合は領域マネージャーをリセット
            if (!enable)
            {
                _regionManager.Reset();
            }
        }

        /// <summary>
        /// 対象ウィンドウを設定
        /// </summary>
        /// <param name="windowHandle">対象ウィンドウのハンドル</param>
        public void SetTargetWindow(IntPtr windowHandle)
        {
            _targetWindowHandle = windowHandle;
            Debug.WriteLine($"検出対象ウィンドウを設定: {windowHandle.ToInt64():X}");
        }

        /// <summary>
        /// 最低信頼度を設定
        /// </summary>
        /// <param name="threshold">信頼度しきい値（0.0～1.0）</param>
        public void SetMinimumConfidence(float threshold)
        {
            _minimumConfidence = Math.Max(0.0f, Math.Min(1.0f, threshold));
            Debug.WriteLine($"信頼度しきい値を {_minimumConfidence:F2} に設定しました");
        }

        /// <summary>
        /// テキスト変更検知の有効/無効を設定
        /// </summary>
        /// <param name="enable">有効にする場合はtrue</param>
        public void EnableChangeDetection(bool enable)
        {
            _useChangeDetection = enable;
            Debug.WriteLine($"テキスト変更検知を {(_useChangeDetection ? "有効" : "無効")} にしました");
        }

        /// <summary>
        /// 差分検出の有効/無効を設定
        /// </summary>
        /// <param name="enable">有効にする場合はtrue</param>
        public void EnableDifferenceDetection(bool enable)
        {
            _useDifferenceDetection = enable;
            Debug.WriteLine($"差分検出を {(_useDifferenceDetection ? "有効" : "無効")} にしました");
        }

        /// <summary>
        /// 動的間隔調整の有効/無効を設定
        /// </summary>
        /// <param name="enable">有効にする場合はtrue</param>
        public void EnableDynamicInterval(bool enable)
        {
            _dynamicIntervalEnabled = enable;
            Debug.WriteLine($"動的間隔調整を {(_dynamicIntervalEnabled ? "有効" : "無効")} にしました");
        }

        /// <summary>
        /// テキスト検出を開始
        /// </summary>
        public void Start()
        {
            if (_targetWindowHandle == IntPtr.Zero)
            {
                Debug.WriteLine("対象ウィンドウが設定されていないため、検出を開始できません");
                _lastErrorMessage = "対象ウィンドウが設定されていません";
                return;
            }

            if (!_isRunning)
            {
                _isRunning = true;
                _detectionTimer.Start();
                Debug.WriteLine("テキスト検出を開始しました");
            }
        }

        /// <summary>
        /// テキスト検出を停止
        /// </summary>
        public void Stop()
        {
            if (_isRunning)
            {
                _detectionTimer.Stop();
                _isRunning = false;
                Debug.WriteLine("テキスト検出を停止しました");
            }
        }

        /// <summary>
        /// 検出間隔を設定
        /// </summary>
        /// <param name="milliseconds">検出間隔（ミリ秒）</param>
        public void SetDetectionInterval(int milliseconds)
        {
            DetectionInterval = milliseconds;
        }

        /// <summary>
        /// 検出されたテキスト領域を取得
        /// </summary>
        public List<TextRegion> GetDetectedRegions()
        {
            return new List<TextRegion>(_detectedRegions);
        }

        /// <summary>
        /// 特定座標のテキスト領域を取得
        /// </summary>
        public TextRegion GetRegionAt(Point point)
        {
            return _detectedRegions.FirstOrDefault(r => r.Bounds.Contains(point));
        }

        private DateTime _processingStartTime;
        private readonly int _processingTimeoutMs = 10000; // 10秒タイムアウト

        /// <summary>
        /// テキスト検出タイマーのイベントハンドラ
        /// </summary>
        private async void DetectionTimer_Tick(object sender, EventArgs e)
        {
            // 一時停止中のタイマーをもう一度止める必要はないので条件確認
            if (_detectionTimer.Enabled)
            {
                _detectionTimer.Stop(); // 処理中はタイマーを停止
            }

            // タイムアウト判定を追加
            if (_processingActive && (DateTime.Now - _processingStartTime).TotalMilliseconds > _processingTimeoutMs)
            {
                Logger.Instance.LogWarning("TextDetectionService",
                    $"OCR処理がタイムアウト ({_processingTimeoutMs}ms) したためリセットします");
                _processingActive = false;
            }

            if (_processingActive)
            {
                Logger.Instance.LogDebug("TextDetectionService", "前回の処理が完了していないためスキップします");

                // タイマーを再開して次の処理に備える
                if (!_disposed && _isRunning && _targetWindowHandle != IntPtr.Zero)
                {
                    _detectionTimer.Start();
                }
                return;
            }

            _processingActive = true; // 処理開始フラグON
            _processingStartTime = DateTime.Now; // 処理開始時間を記録
            _processingStopwatch.Restart();

            try
            {
                // 既存のOCR処理...
            }
            catch (Exception ex)
            {
                // Loggerを使用した詳細なエラーログ
                Logger.Instance.LogError("テキスト検出エラー: " + ex.Message, ex);
                _consecutiveErrors++;
            }
            finally
            {
                // 処理完了フラグを解除 - 必ず実行されるように
                _processingActive = false;
                UpdateProcessingStatus("完了", 100);

                // タイマー再開
                if (!_disposed && _isRunning && _targetWindowHandle != IntPtr.Zero)
                {
                    _detectionTimer.Start();
                }
            }
        }

        /// <summary>
        /// アクティビティに基づいて検出間隔を調整
        /// </summary>
        private void AdjustIntervalForActivity(bool isActive)
        {
            if (!_dynamicIntervalEnabled)
                return;

            if (isActive)
            {
                // アダプティブ間隔を使用する場合
                if (_adaptiveInterval != null)
                {
                    _adaptiveInterval.TemporarilyDecreaseInterval();
                    DetectionInterval = _adaptiveInterval.GetCurrentInterval();
                }
                else
                {
                    // 従来の実装（互換性のため）
                    int newInterval = Math.Max(_detectionInterval / 2, _minDetectionInterval);
                    if (newInterval != _detectionInterval)
                    {
                        Debug.WriteLine($"アクティブなテキスト検出を確認: 間隔を {_detectionInterval}ms から {newInterval}ms に短縮します");
                        DetectionInterval = newInterval;
                    }
                }
            }
            else
            {
                // アダプティブ間隔を使用する場合
                if (_adaptiveInterval != null)
                {
                    _adaptiveInterval.TemporarilyIncreaseInterval();
                    DetectionInterval = _adaptiveInterval.GetCurrentInterval();
                }
                else
                {
                    // 従来の実装（互換性のため）
                    int newInterval = Math.Min(_detectionInterval * 3 / 2, _maxDetectionInterval);
                    if (newInterval != _detectionInterval)
                    {
                        Debug.WriteLine($"非アクティブ状態を確認: 間隔を {_detectionInterval}ms から {newInterval}ms に延長します");
                        DetectionInterval = newInterval;
                    }
                }
            }
        }

        /// <summary>
        /// 言語に基づいた最適化を行う
        /// </summary>
        private void OptimizeForLanguage(List<TextRegion> regions)
        {
            if (regions == null || regions.Count == 0)
                return;

            // 対象言語に基づいた最適化
            try
            {
                // 英語か日本語かを検出（サンプルとしていくつかの領域をチェック）
                int sampleSize = Math.Min(regions.Count, 3);
                int japaneseCount = 0;

                for (int i = 0; i < sampleSize; i++)
                {
                    string detectedLang = LanguageManager.DetectLanguage(regions[i].Text);
                    if (detectedLang == "ja")
                    {
                        japaneseCount++;
                    }
                }

                // 主に日本語と判断された場合、しきい値を調整
                if (japaneseCount > sampleSize / 2)
                {
                    if (TargetLanguage != "ja" && _minimumConfidence > 0.5f)
                    {
                        // 日本語テキストに対してより寛容なしきい値を設定
                        float oldThreshold = _minimumConfidence;
                        _minimumConfidence = Math.Max(0.5f, _minimumConfidence - 0.1f);
                        Debug.WriteLine($"日本語テキストを検出したため、しきい値を {oldThreshold:F2} から {_minimumConfidence:F2} に調整しました");
                    }
                }
                // 主に英語と判断された場合
                else
                {
                    if (TargetLanguage != "en" && _minimumConfidence < 0.6f)
                    {
                        // 英語テキストに対してより厳格なしきい値を設定
                        float oldThreshold = _minimumConfidence;
                        _minimumConfidence = Math.Min(0.6f, _minimumConfidence + 0.05f);
                        Debug.WriteLine($"英語テキストを検出したため、しきい値を {oldThreshold:F2} から {_minimumConfidence:F2} に調整しました");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"言語最適化中にエラーが発生しました: {ex.Message}");
                // エラーでも処理は続行
            }
        }

        /// <summary>
        /// リソース使用状況のチェックと最適化
        /// </summary>
        private void CheckResourceUsage()
        {
            try
            {
                // リソースカウントが高すぎる場合、クリーンアップを実行
                int resourceCount = ResourceManager.GetResourceCount();
                if (resourceCount > 100)
                {
                    Debug.WriteLine($"リソース数が多いため({resourceCount})、クリーンアップを実行します");
                    int cleaned = ResourceManager.CleanupDeadReferences();
                    Debug.WriteLine($"{cleaned}個のリソース参照をクリーンアップしました");
                }

                // メモリ使用量が高すぎる場合、GCを促進
                long memoryUsage = GC.GetTotalMemory(false) / (1024 * 1024); // MB単位
                if (memoryUsage > 300) // 300MB以上でクリーンアップ
                {
                    Debug.WriteLine($"メモリ使用量が高いため({memoryUsage}MB)、クリーンアップを実行します");
                    ResourceManager.PerformEmergencyCleanup();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"リソースチェック中にエラー: {ex.Message}");
            }
        }

        /// <summary>
        /// テキスト領域検出イベント
        /// </summary>
        public event EventHandler<List<TextRegion>> OnRegionsDetected;

        /// <summary>
        /// テキスト領域がなくなったことを通知するイベント
        /// </summary>
        public event EventHandler OnNoRegionsDetected;

        /// <summary>
        /// リソースの破棄
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// リソースの破棄（内部実装）
        /// </summary>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    // マネージドリソースの破棄
                    Stop();
                    if (_detectionTimer != null)
                    {
                        _detectionTimer.Tick -= DetectionTimer_Tick;
                        _detectionTimer.Dispose();
                        _detectionTimer = null;
                    }

                    // 差分検出器の破棄
                    _differenceDetector?.Dispose();

                    // 領域マネージャーをリセット
                    _regionManager?.Reset();

                    // 検出領域をクリア
                    _detectedRegions?.Clear();
                    _detectedRegions = null;
                }

                // アンマネージドリソースの破棄（必要に応じて）

                _disposed = true;
            }
        }

        /// <summary>
        /// ファイナライザ
        /// </summary>
        ~TextDetectionService()
        {
            Dispose(false);
        }
    }
}