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

        // パフォーマンス測定
        private readonly Stopwatch _stopwatch = new Stopwatch();
        private readonly List<double> _processingTimes = new List<double>();
        private const int MAX_TIMING_SAMPLES = 50;

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
            if (image == null)
                return new List<TextRegion>();

            _stopwatch.Restart();
            List<TextRegion> regions = new List<TextRegion>();

            try
            {
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

                // パフォーマンス計測
                _stopwatch.Stop();
                RecordProcessingTime(_stopwatch.ElapsedMilliseconds);

                Debug.WriteLine($"{regions.Count}個のテキスト領域を検出（処理時間: {_stopwatch.ElapsedMilliseconds}ms）");
                return regions;
            }
            catch (Exception ex)
            {
                _stopwatch.Stop();
                Debug.WriteLine($"テキスト領域検出エラー: {ex.Message}");
                return new List<TextRegion>();
            }
        }

        /// <summary>
        /// 現在の設定でスキャンを実行
        /// </summary>
        private async Task<List<TextRegion>> ScanWithCurrentSettingsAsync(Bitmap image)
        {
            Bitmap processedImage = image;

            // 前処理を適用
            if (_usePreprocessing)
            {
                processedImage = _adaptivePreprocessor.ApplyPreprocessing(image);
            }

            // OCRエンジンでテキスト領域を検出
            var regions = await _paddleOcrEngine.DetectTextRegionsAsync(processedImage);

            // 前処理で作成した新しい画像を解放
            if (_usePreprocessing && processedImage != image)
            {
                processedImage.Dispose();
            }

            return regions;
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

                // 再試行
                regions = await ScanWithCurrentSettingsAsync(image);

                // 設定を元に戻す
                _confidenceThreshold = originalThreshold;

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
            _processingTimes.Add(milliseconds);

            // サンプル数を制限
            if (_processingTimes.Count > MAX_TIMING_SAMPLES)
            {
                _processingTimes.RemoveAt(0);
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
                   $"平均処理時間: {avgTime:F1}ms";
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