using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Threading.Tasks;
using System.Linq;
using GameTranslationOverlay.Core.Utils;

namespace GameTranslationOverlay.Core.OCR
{
    /// <summary>
    /// OCRエンジンを管理するクラス（複数エンジンから単一エンジンへ簡素化）
    /// </summary>
    public class OcrManager : IDisposable, IOcrEngine
    {
        // OCRエンジン
        private PaddleOcrEngine _paddleOcrEngine;

        // 適応型プリプロセッサ
        private AdaptivePreprocessor _adaptivePreprocessor;

        // 処理設定
        private float _confidenceThreshold = 0.6f;
        private bool _usePreprocessing = true;
        private bool _isDisposed = false;
        private bool _useAdaptiveMode = true; // 適応モードを使用するかどうか

        // 段階的スキャン設定
        private bool _useProgressiveScan = true;
        private const int MAX_SCAN_ATTEMPTS = 3;

        // 並行処理の制御
        private readonly object _ocrLock = new object();
        private bool _processingActive = false;
        private int _consecutiveErrors = 0;
        private const int MAX_CONSECUTIVE_ERRORS = 5;

        // パフォーマンス測定とトラッキング
        private readonly Stopwatch _stopwatch = new Stopwatch();
        private readonly List<double> _processingTimes = new List<double>();
        private const int MAX_TIMING_SAMPLES = 50;

        // OCRキャッシュ（同一画像に対する重複OCR処理を防止）
        private readonly Dictionary<int, List<TextRegion>> _ocrCache = new Dictionary<int, List<TextRegion>>();
        private const int MAX_CACHE_ENTRIES = 20;
        private DateTime _lastCacheCleanupTime = DateTime.MinValue;

        /// <summary>
        /// コンストラクタ
        /// </summary>
        public OcrManager()
        {
            // 適応型プリプロセッサの初期化
            _adaptivePreprocessor = new AdaptivePreprocessor();
            Debug.WriteLine("OcrManager: 初期化されました");
        }

        /// <summary>
        /// OCRエンジンの初期化
        /// </summary>
        public async Task InitializeAsync()
        {
            try
            {
                Debug.WriteLine("OCRエンジンの初期化を開始...");

                // PaddleOCRエンジンの初期化
                _paddleOcrEngine = new PaddleOcrEngine();
                await _paddleOcrEngine.InitializeAsync();

                // リソースマネージャーに登録
                ResourceManager.TrackResource(_paddleOcrEngine);

                Debug.WriteLine("OCRエンジンの初期化が完了しました");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"OCRエンジン初期化エラー: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// テキスト領域を検出する（段階的スキャンを使用）
        /// </summary>
        /// <param name="image">検出対象の画像</param>
        /// <returns>検出されたテキスト領域のリスト</returns>
        public async Task<List<TextRegion>> DetectTextRegionsAsync(Bitmap image)
        {
            // 並行処理のチェック
            if (_processingActive)
            {
                Debug.WriteLine("OCR: 前回の処理が完了していないため、処理をスキップします");
                return new List<TextRegion>();
            }

            if (image == null)
            {
                Debug.WriteLine("OCR: 入力画像がnullです");
                return new List<TextRegion>();
            }

            _processingActive = true;
            _stopwatch.Restart();

            // キャッシュのクリーンアップ（定期的）
            if ((DateTime.Now - _lastCacheCleanupTime).TotalSeconds > 60)
            {
                CleanupCache();
                _lastCacheCleanupTime = DateTime.Now;
            }

            // 画像のハッシュコードを計算（キャッシュに使用）
            int imageHash = CalculateImageHash(image);

            try
            {
                // キャッシュをチェック
                if (_ocrCache.TryGetValue(imageHash, out var cachedRegions))
                {
                    _processingActive = false;
                    Debug.WriteLine($"OCR: キャッシュから{cachedRegions.Count}個のテキスト領域を取得");
                    return cachedRegions;
                }

                List<TextRegion> regions = new List<TextRegion>();

                if (_useProgressiveScan)
                {
                    // 段階的スキャン（複数の閾値・設定で試行）
                    regions = await ScanWithMultipleSettingsAsync(image);
                }
                else
                {
                    // 通常の単一スキャン
                    regions = await ScanWithCurrentSettingsAsync(image);
                }

                // 結果をフィルタリング（信頼度閾値に基づく）
                regions = FilterByConfidence(regions);

                // 結果に基づいて設定を調整
                if (_useAdaptiveMode)
                {
                    _adaptivePreprocessor.AdjustSettings(regions.Count);
                }

                // 結果をキャッシュに追加
                if (regions.Count > 0)
                {
                    AddToCache(imageHash, regions);
                }

                // 連続エラーカウンターをリセット
                _consecutiveErrors = 0;

                // パフォーマンス計測
                _stopwatch.Stop();
                RecordProcessingTime(_stopwatch.ElapsedMilliseconds);

                Debug.WriteLine($"OCR: {regions.Count}個のテキスト領域を検出（処理時間: {_stopwatch.ElapsedMilliseconds}ms）");

                return regions;
            }
            catch (Exception ex)
            {
                _stopwatch.Stop();
                _consecutiveErrors++;
                Debug.WriteLine($"OCR: テキスト領域検出エラー: {ex.Message}");

                // 連続エラーが多い場合、リソースをクリーンアップ
                if (_consecutiveErrors >= MAX_CONSECUTIVE_ERRORS)
                {
                    Debug.WriteLine($"OCR: 連続エラーが{MAX_CONSECUTIVE_ERRORS}回発生したため、リソースをクリーンアップします");
                    CleanupResources();
                    _consecutiveErrors = 0;
                }

                return new List<TextRegion>();
            }
            finally
            {
                _processingActive = false;
            }
        }

        /// <summary>
        /// 画像のハッシュ値を計算（簡易的な方法）
        /// </summary>
        private int CalculateImageHash(Bitmap image)
        {
            try
            {
                // 単純なハッシュ計算（完全なマッチングではなく近似）
                int hash = 17;
                hash = hash * 31 + image.Width;
                hash = hash * 31 + image.Height;

                // 数ポイントをサンプリングして特徴を抽出
                int samplingSize = 5;
                int stepX = Math.Max(1, image.Width / samplingSize);
                int stepY = Math.Max(1, image.Height / samplingSize);

                for (int y = 0; y < image.Height; y += stepY)
                {
                    for (int x = 0; x < image.Width; x += stepX)
                    {
                        if (x < image.Width && y < image.Height)
                        {
                            Color pixel = image.GetPixel(x, y);
                            // 色の大まかな特徴だけを使用
                            int colorValue = (pixel.R / 32) * 1000000 + (pixel.G / 32) * 1000 + (pixel.B / 32);
                            hash = hash * 31 + colorValue;
                        }
                    }
                }

                return hash;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"画像ハッシュ計算エラー: {ex.Message}");
                // エラー時は固有のハッシュを返す（キャッシュヒットしないよう）
                return image.GetHashCode();
            }
        }

        /// <summary>
        /// キャッシュにテキスト領域を追加
        /// </summary>
        private void AddToCache(int imageHash, List<TextRegion> regions)
        {
            try
            {
                // キャッシュが大きすぎる場合、古いエントリを削除
                if (_ocrCache.Count >= MAX_CACHE_ENTRIES)
                {
                    int oldestKey = _ocrCache.Keys.First();
                    _ocrCache.Remove(oldestKey);
                }

                // 結果のディープコピーを保存（参照の問題を避けるため）
                var regionsCopy = regions.Select(r => new TextRegion
                {
                    Text = r.Text,
                    Confidence = r.Confidence,
                    Bounds = r.Bounds
                }).ToList();

                _ocrCache[imageHash] = regionsCopy;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"キャッシュ追加エラー: {ex.Message}");
            }
        }

        /// <summary>
        /// キャッシュのクリーンアップ
        /// </summary>
        private void CleanupCache()
        {
            try
            {
                // キャッシュが一定サイズを超えたらクリアする
                if (_ocrCache.Count > MAX_CACHE_ENTRIES / 2)
                {
                    Debug.WriteLine($"OCRキャッシュをクリーンアップしています（現在のエントリ数: {_ocrCache.Count}）");
                    _ocrCache.Clear();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"キャッシュクリーンアップエラー: {ex.Message}");
            }
        }

        /// <summary>
        /// リソースのクリーンアップ
        /// </summary>
        private void CleanupResources()
        {
            try
            {
                // キャッシュのクリア
                _ocrCache.Clear();

                // リソースマネージャーのクリーンアップ促進
                ResourceManager.CleanupDeadReferences();

                // GCの実行を促進
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"リソースクリーンアップエラー: {ex.Message}");
            }
        }

        /// <summary>
        /// 現在の設定でスキャンを実行
        /// </summary>
        private async Task<List<TextRegion>> ScanWithCurrentSettingsAsync(Bitmap image)
        {
            Bitmap processedImage = null;

            try
            {
                // 前処理を適用
                if (_usePreprocessing)
                {
                    processedImage = _adaptivePreprocessor.ApplyPreprocessing(image);
                    if (processedImage == null)
                    {
                        Debug.WriteLine("前処理に失敗したため、元の画像を使用します");
                        processedImage = new Bitmap(image);
                    }
                }
                else
                {
                    // 元の画像のコピーを使用（安全のため）
                    processedImage = new Bitmap(image);
                }

                // リソースマネージャーに登録
                ResourceManager.TrackResource(processedImage);

                // OCRエンジンでテキスト領域を検出
                var regions = await _paddleOcrEngine.DetectTextRegionsAsync(processedImage);

                return regions;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"OCRスキャンエラー: {ex.Message}");
                throw;
            }
            finally
            {
                // 前処理で作成した新しい画像を解放
                if (processedImage != null && processedImage != image)
                {
                    ResourceManager.ReleaseResource(processedImage);
                }
            }
        }

        /// <summary>
        /// 段階的に複数の設定でスキャンを試行
        /// </summary>
        private async Task<List<TextRegion>> ScanWithMultipleSettingsAsync(Bitmap image)
        {
            // 最初に現在の設定で試行
            var regions = await ScanWithCurrentSettingsAsync(image);

            // 結果があれば終了
            if (regions.Count > 0)
                return regions;

            // 異なる設定でさらに試行
            for (int attempt = 1; attempt < MAX_SCAN_ATTEMPTS; attempt++)
            {
                Debug.WriteLine($"テキスト検出再試行 ({attempt}/{MAX_SCAN_ATTEMPTS - 1})...");

                // 閾値を一時的に下げる
                float originalThreshold = _confidenceThreshold;
                _confidenceThreshold = Math.Max(_confidenceThreshold - (0.1f * attempt), 0.1f);

                // 次のプリセットを試す（一時的に）
                if (_usePreprocessing && _useAdaptiveMode)
                {
                    _adaptivePreprocessor.TryNextPreset();
                }

                try
                {
                    // 再試行
                    regions = await ScanWithCurrentSettingsAsync(image);
                }
                finally
                {
                    // 設定を元に戻す
                    _confidenceThreshold = originalThreshold;
                }

                // 結果があれば終了
                if (regions.Count > 0)
                {
                    Debug.WriteLine($"再試行で{regions.Count}個のテキスト領域を検出");
                    return regions;
                }
            }

            // すべての試行が失敗した場合は空のリストを返す
            return new List<TextRegion>();
        }

        /// <summary>
        /// 信頼度閾値に基づいてテキスト領域をフィルタリング
        /// </summary>
        private List<TextRegion> FilterByConfidence(List<TextRegion> regions)
        {
            return regions.FindAll(r => r.Confidence >= _confidenceThreshold);
        }

        /// <summary>
        /// 処理時間を記録する
        /// </summary>
        private void RecordProcessingTime(long milliseconds)
        {
            try
            {
                _processingTimes.Add(milliseconds);

                // サンプル数を制限
                if (_processingTimes.Count > MAX_TIMING_SAMPLES)
                {
                    _processingTimes.RemoveAt(0);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"処理時間記録エラー: {ex.Message}");
            }
        }

        /// <summary>
        /// スペシフィック領域のテキストを認識する
        /// </summary>
        public async Task<string> RecognizeTextAsync(Rectangle region)
        {
            try
            {
                // スクリーンキャプチャは呼び出し元で行われることを想定
                // 非同期操作を模擬するために小さな遅延を挿入
                await Task.Delay(1);
                return "OCR Manager: RecognizeTextAsync not directly implemented. Use PaddleOcrEngine instead.";
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"テキスト認識エラー: {ex.Message}");
                return string.Empty;
            }
        }

        /// <summary>
        /// 使用するOCRエンジンを設定（互換性のために維持）
        /// </summary>
        public void SetOcrEngine(string engineName)
        {
            // PaddleOCRのみをサポートするため、実質的には何もしない
            Debug.WriteLine($"使用できるOCRエンジンはPaddleOCRのみです。指定されたエンジン: {engineName}");
        }

        /// <summary>
        /// フォールバックの有効・無効を設定（互換性のために維持）
        /// </summary>
        public void SetUseFallback(bool enable)
        {
            // 単一エンジンのため、フォールバックは使用しない
            Debug.WriteLine($"フォールバック機能は使用されません。");
        }

        /// <summary>
        /// 信頼度閾値を設定
        /// </summary>
        public void SetConfidenceThreshold(float threshold)
        {
            _confidenceThreshold = Math.Max(0.0f, Math.Min(1.0f, threshold));
            Debug.WriteLine($"信頼度閾値を {_confidenceThreshold:F2} に設定しました");
        }

        /// <summary>
        /// 前処理オプションを設定
        /// </summary>
        public void SetPreprocessingOptions(GameTranslationOverlay.Core.Utils.PreprocessingOptions options)
        {
            if (options != null && _adaptivePreprocessor != null)
            {
                // プロパティを通じて設定を適用
                _adaptivePreprocessor.CurrentPreprocessingOptions = options;
                Debug.WriteLine("前処理オプションを設定しました");
            }
        }

        /// <summary>
        /// 前処理の有効・無効を設定
        /// </summary>
        public void EnablePreprocessing(bool enable)
        {
            _usePreprocessing = enable;
            Debug.WriteLine($"前処理を {(_usePreprocessing ? "有効" : "無効")} にしました");
        }

        /// <summary>
        /// 適応モードの有効・無効を設定
        /// </summary>
        public void EnableAdaptiveMode(bool enable)
        {
            _useAdaptiveMode = enable;
            Debug.WriteLine($"適応モードを {(_useAdaptiveMode ? "有効" : "無効")} にしました");
        }

        /// <summary>
        /// 段階的スキャンの有効・無効を設定
        /// </summary>
        public void EnableProgressiveScan(bool enable)
        {
            _useProgressiveScan = enable;
            Debug.WriteLine($"段階的スキャンを {(_useProgressiveScan ? "有効" : "無効")} にしました");
        }

        /// <summary>
        /// 現在の設定状態を取得
        /// </summary>
        public string GetStatusSummary()
        {
            double avgTime = 0;
            if (_processingTimes.Count > 0)
            {
                avgTime = _processingTimes.Sum() / _processingTimes.Count;
            }

            return $"信頼度閾値: {_confidenceThreshold:F2}, " +
                   $"前処理: {(_usePreprocessing ? "有効" : "無効")}, " +
                   $"適応モード: {(_useAdaptiveMode ? "有効" : "無効")}, " +
                   $"段階的スキャン: {(_useProgressiveScan ? "有効" : "無効")}, " +
                   $"平均処理時間: {avgTime:F1}ms, " +
                   $"キャッシュエントリ数: {_ocrCache.Count}";
        }

        /// <summary>
        /// OCRキャッシュをクリア
        /// </summary>
        public void ClearCache()
        {
            _ocrCache.Clear();
            Debug.WriteLine("OCRキャッシュをクリアしました");
        }

        /// <summary>
        /// 適応型プリプロセッサの状態サマリーを取得
        /// </summary>
        public string GetPreprocessorStatusSummary()
        {
            return _adaptivePreprocessor?.GetStatusSummary() ?? "プリプロセッサが初期化されていません";
        }

        /// <summary>
        /// 設定をデフォルトに戻す
        /// </summary>
        public void ResetToDefault()
        {
            _confidenceThreshold = 0.6f;
            _usePreprocessing = true;
            _useAdaptiveMode = true;
            _useProgressiveScan = true;

            if (_adaptivePreprocessor != null)
            {
                _adaptivePreprocessor.ResetToDefault();
            }

            ClearCache();
            Debug.WriteLine("すべての設定をデフォルトに戻しました");
        }

        /// <summary>
        /// ゲームプロファイルを適用
        /// </summary>
        public void ApplyGameProfile(string profileName)
        {
            if (_adaptivePreprocessor != null)
            {
                _adaptivePreprocessor.ApplyGameProfile(profileName);
            }
        }

        /// <summary>
        /// 使用しているプライマリエンジンの名前を取得（互換性用）
        /// </summary>
        public string GetPrimaryEngineName()
        {
            return "PaddleOCR";
        }

        /// <summary>
        /// リソースの解放
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// リソースの解放（内部実装）
        /// </summary>
        protected virtual void Dispose(bool disposing)
        {
            if (!_isDisposed)
            {
                if (disposing)
                {
                    // マネージドリソースの破棄
                    _paddleOcrEngine?.Dispose();
                    _ocrCache.Clear();
                    _processingTimes.Clear();
                }

                _isDisposed = true;
            }
        }

        /// <summary>
        /// デストラクタ
        /// </summary>
        ~OcrManager()
        {
            Dispose(false);
        }
    }
}