using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using GameTranslationOverlay.Core.OCR;
using GameTranslationOverlay.Core.Translation.Services;
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
        private IntPtr _targetWindowHandle = IntPtr.Zero;
        private float _minimumConfidence = 0.6f; // 最低信頼度（これより低いテキストは無視）

        // 状態管理
        private bool _disposed = false;
        private bool _isRunning = false;
        private bool _isProcessing = false;
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
        private int _minDetectionInterval = 300;  // 最小間隔（ミリ秒）
        private int _maxDetectionInterval = 2000; // 最大間隔（ミリ秒）
        private AdaptiveDetectionInterval _adaptiveInterval;

        // スマートOCR処理
        private bool _useSmartRegions = true;
        private SmartOcrRegionManager _regionManager;

        // 進捗状況
        private string _processingStatus = "";
        private int _processingProgress = 0;

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

            // アダプティブ間隔管理の初期化
            _adaptiveInterval = new AdaptiveDetectionInterval(_minDetectionInterval, _maxDetectionInterval);

            // スマート領域検出の初期化
            _regionManager = new SmartOcrRegionManager();

            Debug.WriteLine("TextDetectionService: 初期化されました");
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
        /// 動的間隔調整の有効/無効を設定
        /// </summary>
        /// <param name="enable">有効にする場合はtrue</param>
        public void EnableDynamicInterval(bool enable)
        {
            _dynamicIntervalEnabled = enable;
            Debug.WriteLine($"動的間隔調整を {(_dynamicIntervalEnabled ? "有効" : "無効")} にしました");
        }

        /// <summary>
        /// スマート領域検出の有効/無効を設定
        /// </summary>
        /// <param name="enable">有効にする場合はtrue</param>
        public void EnableSmartRegions(bool enable)
        {
            _useSmartRegions = enable;
            Debug.WriteLine($"スマート領域検出を {(_useSmartRegions ? "有効" : "無効")} にしました");
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

        /// <summary>
        /// 進捗状況の更新
        /// </summary>
        private void UpdateProcessingStatus(string status, int progress)
        {
            _processingStatus = status;
            _processingProgress = progress;
            OnProcessingStatusChanged?.Invoke(this, new ProcessingStatusEventArgs(status, progress));
        }

        /// <summary>
        /// テキスト検出タイマーのイベントハンドラ
        /// </summary>
        private async void DetectionTimer_Tick(object sender, EventArgs e)
        {
            // 既に処理中の場合はスキップ
            if (_isProcessing)
            {
                Debug.WriteLine("前回の処理が完了していないため、スキップします");
                return;
            }

            _isProcessing = true;
            Bitmap windowCapture = null;

            try
            {
                // タイマーを一時停止
                _detectionTimer.Stop();

                UpdateProcessingStatus("処理開始", 0);

                if (_targetWindowHandle == IntPtr.Zero || !WindowUtils.IsWindowValid(_targetWindowHandle))
                {
                    Debug.WriteLine("対象ウィンドウが無効なため、検出をスキップします");
                    _lastErrorMessage = "対象ウィンドウが無効です";
                    return;
                }

                // ターゲットウィンドウの領域を取得
                Rectangle windowRect = WindowUtils.GetWindowRect(_targetWindowHandle);

                // ウィンドウが最小化されていないか確認
                if (windowRect.Width <= 0 || windowRect.Height <= 0)
                {
                    Debug.WriteLine("ウィンドウが最小化されているか、サイズが無効です");
                    _lastErrorMessage = "ウィンドウが最小化されています";
                    return;
                }

                // ウィンドウ領域をキャプチャ
                UpdateProcessingStatus("画面キャプチャ中", 10);
                windowCapture = ScreenCapture.CaptureWindow(_targetWindowHandle);
                if (windowCapture == null)
                {
                    Debug.WriteLine("ウィンドウキャプチャに失敗しました");
                    _lastErrorMessage = "ウィンドウキャプチャに失敗しました";
                    _consecutiveErrors++;
                    return;
                }

                // 差分検出（最適化）
                UpdateProcessingStatus("差分検出中", 20);
                bool hasChange = true; // 差分検出ロジックは別途実装

                // 差分がある場合のみOCR処理を実行
                if (hasChange)
                {
                    UpdateProcessingStatus("テキスト検出中", 30);
                    List<TextRegion> allRegions = new List<TextRegion>();

                    // スマート領域検出を使用するかどうかで処理を分岐
                    if (_useSmartRegions)
                    {
                        // アクティブな領域のみを処理
                        List<Rectangle> regionsToProcess = _regionManager.GetActiveRegions();

                        // 初回または有力な領域がない場合は全画面処理
                        if (regionsToProcess.Count == 0 || (regionsToProcess.Count == 1 && regionsToProcess[0].Equals(windowRect)))
                        {
                            Debug.WriteLine("アクティブな領域がないため、全画面処理を実行します");
                            UpdateProcessingStatus("全画面OCR処理中", 40);
                            var regions = await _ocrEngine.DetectTextRegionsAsync(windowCapture);

                            // 領域マネージャーを更新
                            _regionManager.UpdateRegions(windowRect, regions);
                            allRegions.AddRange(regions);
                        }
                        else
                        {
                            Debug.WriteLine($"{regionsToProcess.Count}個のアクティブ領域を処理します");
                            UpdateProcessingStatus($"{regionsToProcess.Count}個の領域を処理中", 40);

                            // 各アクティブ領域を個別に処理
                            int processedRegions = 0;
                            foreach (var region in regionsToProcess)
                            {
                                // 領域がウィンドウ内に収まるように調整
                                Rectangle safeRegion = new Rectangle(
                                    Math.Max(0, region.X - windowRect.X),
                                    Math.Max(0, region.Y - windowRect.Y),
                                    Math.Min(windowCapture.Width - (region.X - windowRect.X), region.Width),
                                    Math.Min(windowCapture.Height - (region.Y - windowRect.Y), region.Height)
                                );

                                // 有効なサイズかチェック
                                if (safeRegion.Width <= 0 || safeRegion.Height <= 0)
                                {
                                    processedRegions++;
                                    continue;
                                }

                                Bitmap regionBitmap = null;

                                try
                                {
                                    // 部分画像を切り出し
                                    regionBitmap = new Bitmap(safeRegion.Width, safeRegion.Height);
                                    using (Graphics g = Graphics.FromImage(regionBitmap))
                                    {
                                        g.DrawImage(windowCapture,
                                            new Rectangle(0, 0, safeRegion.Width, safeRegion.Height),
                                            safeRegion,
                                            GraphicsUnit.Pixel);
                                    }

                                    // OCR処理
                                    var regionTexts = await _ocrEngine.DetectTextRegionsAsync(regionBitmap);

                                    // 座標を元のウィンドウ座標に変換
                                    foreach (var text in regionTexts)
                                    {
                                        text.Bounds = new Rectangle(
                                            text.Bounds.X + (region.X - windowRect.X),
                                            text.Bounds.Y + (region.Y - windowRect.Y),
                                            text.Bounds.Width,
                                            text.Bounds.Height
                                        );
                                    }

                                    allRegions.AddRange(regionTexts);
                                }
                                catch (Exception ex)
                                {
                                    Debug.WriteLine($"領域処理エラー: {ex.Message}");
                                }
                                finally
                                {
                                    // 必ず解放
                                    if (regionBitmap != null)
                                    {
                                        regionBitmap.Dispose();
                                        regionBitmap = null;
                                    }

                                    // 進捗状況の更新
                                    processedRegions++;
                                    int progress = 40 + (processedRegions * 20 / regionsToProcess.Count);
                                    UpdateProcessingStatus($"領域処理中 ({processedRegions}/{regionsToProcess.Count})", progress);
                                }
                            }

                            // 領域マネージャーを更新
                            _regionManager.UpdateRegions(windowRect, allRegions);
                        }
                    }
                    else
                    {
                        // 従来の方法（全画面処理）
                        UpdateProcessingStatus("全画面OCR処理中", 40);
                        allRegions = await _ocrEngine.DetectTextRegionsAsync(windowCapture);
                    }

                    // 最低信頼度でフィルタリング
                    UpdateProcessingStatus("テキスト処理中", 60);
                    allRegions = allRegions.Where(r => r.Confidence >= _minimumConfidence).ToList();

                    // アダプティブ間隔の更新
                    if (_dynamicIntervalEnabled)
                    {
                        _adaptiveInterval.UpdateInterval(hasChange, allRegions.Count > 0);
                        DetectionInterval = _adaptiveInterval.GetCurrentInterval();
                    }

                    // 前回と今回の検出結果を比較
                    bool hadRegionsBefore = _detectedRegions.Count > 0;
                    bool hasRegionsNow = allRegions.Count > 0;

                    // スクリーン座標に変換
                    if (hasRegionsNow)
                    {
                        UpdateProcessingStatus("テキスト変換中", 70);
                        // テキスト内容の連結（変更検出用）
                        string currentText = string.Join(" ", allRegions.Select(r => r.Text));

                        // 言語の検出と最適化
                        UpdateProcessingStatus("言語検出中", 80);
                        OptimizeForLanguage(allRegions);

                        foreach (var region in allRegions)
                        {
                            // キャプチャ内の相対座標からスクリーン座標に変換
                            region.Bounds = new Rectangle(
                                windowRect.Left + region.Bounds.X,
                                windowRect.Top + region.Bounds.Y,
                                region.Bounds.Width,
                                region.Bounds.Height);
                        }

                        // テキスト変更の検出
                        bool textChanged = !currentText.Equals(_lastDetectedText);

                        // テキスト変更検知が有効で、変更があった場合のみ通知
                        if (!_useChangeDetection || textChanged)
                        {
                            UpdateProcessingStatus("テキスト通知中", 90);
                            // 検出結果を保存
                            _detectedRegions = allRegions;
                            _noRegionsDetectedCount = 0;
                            _consecutiveErrors = 0;

                            // 結果を通知
                            Debug.WriteLine($"{allRegions.Count}個のテキスト領域を検出しました");
                            OnRegionsDetected?.Invoke(this, allRegions);

                            // テキスト変更時刻を更新
                            if (textChanged)
                            {
                                _lastTextChangeTime = DateTime.Now;
                                _lastDetectedText = currentText;

                                // 間隔を短くする（テキストが変わった = アクティブな状態）
                                if (_dynamicIntervalEnabled)
                                {
                                    AdjustIntervalForActivity(true);
                                }
                            }
                        }
                        else
                        {
                            Debug.WriteLine("テキストに変更がないため通知をスキップします");
                            UpdateProcessingStatus("変更なし", 100);

                            // 長時間変化がない場合は間隔を長くする
                            if (_dynamicIntervalEnabled && (DateTime.Now - _lastTextChangeTime).TotalSeconds > 10)
                            {
                                AdjustIntervalForActivity(false);
                            }
                        }
                    }
                    else
                    {
                        // テキスト領域が検出されなかった
                        Debug.WriteLine("テキスト領域は検出されませんでした");
                        UpdateProcessingStatus("テキストなし", 100);
                        _noRegionsDetectedCount++;

                        // しきい値を超えて検出されなかった場合
                        if (hadRegionsBefore && _noRegionsDetectedCount >= NO_REGIONS_THRESHOLD)
                        {
                            _detectedRegions.Clear();
                            _lastDetectedText = string.Empty;
                            OnNoRegionsDetected?.Invoke(this, EventArgs.Empty);
                            Debug.WriteLine($"テキスト領域が{NO_REGIONS_THRESHOLD}回連続で検出されなかったため、クリーンアップイベントを発行します");
                            _noRegionsDetectedCount = 0;
                        }

                        // 動的間隔調整（テキストがない = 非アクティブな状態）
                        if (_dynamicIntervalEnabled)
                        {
                            AdjustIntervalForActivity(false);
                        }
                    }
                }
                else
                {
                    // 差分がない場合の間隔調整
                    UpdateProcessingStatus("変更なし", 100);
                    if (_dynamicIntervalEnabled)
                    {
                        _adaptiveInterval.UpdateInterval(false, false);
                        DetectionInterval = _adaptiveInterval.GetCurrentInterval();
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"テキスト検出エラー: {ex.Message}");
                _lastErrorMessage = $"検出エラー: {ex.Message}";
                _consecutiveErrors++;

                // 連続エラーが多すぎる場合はリカバリーを試みる
                if (_consecutiveErrors >= MAX_CONSECUTIVE_ERRORS)
                {
                    Debug.WriteLine($"連続エラーが{MAX_CONSECUTIVE_ERRORS}回発生したため、リカバリーを試みます");
                    _consecutiveErrors = 0;
                    _noRegionsDetectedCount = 0;
                    _detectedRegions.Clear();
                    _lastDetectedText = string.Empty;

                    // クリーンアップイベントを発行
                    OnNoRegionsDetected?.Invoke(this, EventArgs.Empty);
                }
            }
            finally
            {
                // キャプチャ画像の確実な解放
                if (windowCapture != null)
                {
                    windowCapture.Dispose();
                    windowCapture = null;
                }

                // 処理フラグを解除
                _isProcessing = false;

                // タイマーを再開（オブジェクトが破棄されていない場合のみ）
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
                // アクティブ時は間隔を短くする
                int newInterval = Math.Max(_detectionInterval / 2, _minDetectionInterval);
                if (newInterval != _detectionInterval)
                {
                    Debug.WriteLine($"アクティブなテキスト検出を確認: 間隔を {_detectionInterval}ms から {newInterval}ms に短縮します");
                    DetectionInterval = newInterval;
                }
            }
            else
            {
                // 非アクティブ時は間隔を長くする
                int newInterval = Math.Min(_detectionInterval * 3 / 2, _maxDetectionInterval);
                if (newInterval != _detectionInterval)
                {
                    Debug.WriteLine($"非アクティブ状態を確認: 間隔を {_detectionInterval}ms から {newInterval}ms に延長します");
                    DetectionInterval = newInterval;
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
        /// テキスト領域検出イベント
        /// </summary>
        public event EventHandler<List<TextRegion>> OnRegionsDetected;

        /// <summary>
        /// テキスト領域がなくなったことを通知するイベント
        /// </summary>
        public event EventHandler OnNoRegionsDetected;

        /// <summary>
        /// 処理状態変更イベント
        /// </summary>
        public event EventHandler<ProcessingStatusEventArgs> OnProcessingStatusChanged;

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

    /// <summary>
    /// 処理状態イベント引数
    /// </summary>
    public class ProcessingStatusEventArgs : EventArgs
    {
        public string Status { get; }
        public int Progress { get; }

        public ProcessingStatusEventArgs(string status, int progress)
        {
            Status = status;
            Progress = progress;
        }
    }
}