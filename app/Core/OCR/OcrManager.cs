using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using GameTranslationOverlay.Core.Utils;

namespace GameTranslationOverlay.Core.OCR
{
    /// <summary>
    /// OCRエンジンの選択と管理を行うクラス
    /// </summary>
    public class OcrManager : IDisposable
    {
        // OCRエンジン
        private readonly PaddleOcrEngine _paddleOcrEngine;
        private readonly TesseractOcrEngine _tesseractOcrEngine;
        private IOcrEngine _primaryEngine;
        private IOcrEngine _fallbackEngine;

        // 処理設定
        private bool _useFallback = true;
        private float _confidenceThreshold = 0.6f;
        private bool _isDisposed = false;

        // パフォーマンス測定
        private readonly Stopwatch _stopwatch = new Stopwatch();
        private int _paddleSuccessCount = 0;
        private int _tesseractSuccessCount = 0;
        private int _paddleFailCount = 0;
        private int _tesseractFailCount = 0;

        /// <summary>
        /// OCRマネージャーのコンストラクタ
        /// </summary>
        public OcrManager()
        {
            // 両方のOCRエンジンを初期化
            _paddleOcrEngine = new PaddleOcrEngine(ImagePreprocessor.JapaneseTextPreset);
            _tesseractOcrEngine = new TesseractOcrEngine(ImagePreprocessor.JapaneseTextPreset);

            // 初期設定としてPaddleOCRをプライマリに、Tesseractをフォールバックに
            _primaryEngine = _paddleOcrEngine;
            _fallbackEngine = _tesseractOcrEngine;
        }

        /// <summary>
        /// OCRエンジンを初期化
        /// </summary>
        public async Task InitializeAsync()
        {
            // 両方のエンジンを並列で初期化
            var initTasks = new Task[]
            {
                _paddleOcrEngine.InitializeAsync(),
                _tesseractOcrEngine.InitializeAsync()
            };

            try
            {
                // どちらかが失敗しても続行
                await Task.WhenAll(initTasks);
                Debug.WriteLine("Both OCR engines initialized successfully");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error initializing one or more OCR engines: {ex.Message}");

                // エラーが発生したエンジンをチェック
                bool paddleInitialized = initTasks[0].IsCompleted && !initTasks[0].IsFaulted;
                bool tesseractInitialized = initTasks[1].IsCompleted && !initTasks[1].IsFaulted;

                // 初期化に成功したエンジンのみを使用
                if (paddleInitialized && !tesseractInitialized)
                {
                    _primaryEngine = _paddleOcrEngine;
                    _fallbackEngine = null;
                    _useFallback = false;
                    Debug.WriteLine("Only PaddleOCR engine initialized successfully");
                }
                else if (!paddleInitialized && tesseractInitialized)
                {
                    _primaryEngine = _tesseractOcrEngine;
                    _fallbackEngine = null;
                    _useFallback = false;
                    Debug.WriteLine("Only Tesseract engine initialized successfully");
                }
                else if (!paddleInitialized && !tesseractInitialized)
                {
                    throw new InvalidOperationException("Failed to initialize both OCR engines");
                }
            }
        }

        /// <summary>
        /// テキストを認識
        /// </summary>
        public async Task<string> RecognizeTextAsync(Rectangle region)
        {
            _stopwatch.Restart();

            try
            {
                // プライマリエンジンを使用して認識
                string primaryResult = await _primaryEngine.RecognizeTextAsync(region);

                // 認識結果のチェック
                bool primarySuccess = !string.IsNullOrWhiteSpace(primaryResult) && !primaryResult.StartsWith("OCR Error");

                if (primarySuccess)
                {
                    UpdateSuccessCount(_primaryEngine == _paddleOcrEngine);
                    return primaryResult;
                }

                // プライマリが失敗し、フォールバックが有効な場合
                if (_useFallback && _fallbackEngine != null)
                {
                    UpdateFailCount(_primaryEngine == _paddleOcrEngine);

                    // フォールバックを使用
                    string fallbackResult = await _fallbackEngine.RecognizeTextAsync(region);

                    bool fallbackSuccess = !string.IsNullOrWhiteSpace(fallbackResult) && !fallbackResult.StartsWith("OCR Error");

                    if (fallbackSuccess)
                    {
                        UpdateSuccessCount(_fallbackEngine == _paddleOcrEngine);
                        return fallbackResult;
                    }

                    UpdateFailCount(_fallbackEngine == _paddleOcrEngine);
                }

                // 両方失敗
                return primaryResult; // プライマリの結果を返す
            }
            finally
            {
                _stopwatch.Stop();
                Debug.WriteLine($"OCR recognition took {_stopwatch.ElapsedMilliseconds}ms");

                // 成功率に基づいてエンジン入れ替えを検討
                SwapEnginesIfNeeded();
            }
        }

        /// <summary>
        /// テキスト領域を検出
        /// </summary>
        public async Task<List<TextRegion>> DetectTextRegionsAsync(Bitmap image)
        {
            _stopwatch.Restart();

            try
            {
                // プライマリエンジンを使用して検出
                var primaryRegions = await _primaryEngine.DetectTextRegionsAsync(image);

                // 検出結果のチェック
                bool primarySuccess = primaryRegions.Count > 0;

                if (primarySuccess)
                {
                    // 信頼度でフィルタリング
                    primaryRegions = primaryRegions
                        .Where(r => r.Confidence >= _confidenceThreshold)
                        .ToList();

                    if (primaryRegions.Count > 0)
                    {
                        UpdateSuccessCount(_primaryEngine == _paddleOcrEngine);
                        return primaryRegions;
                    }
                }

                // プライマリが失敗し、フォールバックが有効な場合
                if (_useFallback && _fallbackEngine != null)
                {
                    UpdateFailCount(_primaryEngine == _paddleOcrEngine);

                    // フォールバックを使用
                    var fallbackRegions = await _fallbackEngine.DetectTextRegionsAsync(image);

                    bool fallbackSuccess = fallbackRegions.Count > 0;

                    if (fallbackSuccess)
                    {
                        // 信頼度でフィルタリング
                        fallbackRegions = fallbackRegions
                            .Where(r => r.Confidence >= _confidenceThreshold)
                            .ToList();

                        if (fallbackRegions.Count > 0)
                        {
                            UpdateSuccessCount(_fallbackEngine == _paddleOcrEngine);
                            return fallbackRegions;
                        }
                    }

                    UpdateFailCount(_fallbackEngine == _paddleOcrEngine);
                }

                // 両方失敗した場合、プライマリの結果を返す
                return primaryRegions;
            }
            finally
            {
                _stopwatch.Stop();
                Debug.WriteLine($"OCR detection took {_stopwatch.ElapsedMilliseconds}ms");

                // 成功率に基づいてエンジン入れ替えを検討
                SwapEnginesIfNeeded();
            }
        }

        /// <summary>
        /// 使用するOCRエンジンを変更
        /// </summary>
        public void SetOcrEngine(string engineName)
        {
            switch (engineName.ToLower())
            {
                case "paddle":
                case "paddleocr":
                    _primaryEngine = _paddleOcrEngine;
                    _fallbackEngine = _tesseractOcrEngine;
                    break;

                case "tesseract":
                    _primaryEngine = _tesseractOcrEngine;
                    _fallbackEngine = _paddleOcrEngine;
                    break;

                case "auto":
                default:
                    // パフォーマンス統計に基づいて自動的に選択
                    if (_paddleSuccessCount + _paddleFailCount > 0 &&
                        _tesseractSuccessCount + _tesseractFailCount > 0)
                    {
                        float paddleSuccessRate = (float)_paddleSuccessCount / (_paddleSuccessCount + _paddleFailCount);
                        float tesseractSuccessRate = (float)_tesseractSuccessCount / (_tesseractSuccessCount + _tesseractFailCount);

                        if (paddleSuccessRate > tesseractSuccessRate)
                        {
                            _primaryEngine = _paddleOcrEngine;
                            _fallbackEngine = _tesseractOcrEngine;
                        }
                        else
                        {
                            _primaryEngine = _tesseractOcrEngine;
                            _fallbackEngine = _paddleOcrEngine;
                        }
                    }
                    break;
            }

            Debug.WriteLine($"OCR engine set to: Primary={GetEngineName(_primaryEngine)}, Fallback={GetEngineName(_fallbackEngine)}");
        }

        /// <summary>
        /// フォールバック機能の有効/無効を設定
        /// </summary>
        public void SetUseFallback(bool enable)
        {
            _useFallback = enable;
            Debug.WriteLine($"OCR fallback {(_useFallback ? "enabled" : "disabled")}");
        }

        /// <summary>
        /// 信頼度閾値を設定
        /// </summary>
        public void SetConfidenceThreshold(float threshold)
        {
            _confidenceThreshold = Math.Max(0.0f, Math.Min(1.0f, threshold));
            Debug.WriteLine($"OCR confidence threshold set to {_confidenceThreshold}");
        }

        /// <summary>
        /// 前処理オプションを設定
        /// </summary>
        public void SetPreprocessingOptions(PreprocessingOptions options)
        {
            _paddleOcrEngine.SetPreprocessingOptions(options);
            _tesseractOcrEngine.SetPreprocessingOptions(options);
            Debug.WriteLine("OCR preprocessing options updated");
        }

        /// <summary>
        /// 前処理の有効/無効を設定
        /// </summary>
        public void EnablePreprocessing(bool enable)
        {
            _paddleOcrEngine.EnablePreprocessing(enable);
            _tesseractOcrEngine.EnablePreprocessing(enable);
            Debug.WriteLine($"OCR preprocessing {(enable ? "enabled" : "disabled")}");
        }

        /// <summary>
        /// 現在のプライマリエンジンのインスタンスを取得
        /// </summary>
        /// <returns>プライマリOCRエンジンのインスタンス</returns>
        public GameTranslationOverlay.Core.OCR.IOcrEngine GetPrimaryEngine()
        {
            return _primaryEngine;
        }

        /// <summary>
        /// 現在のフォールバックエンジン名を取得
        /// </summary>
        public string GetFallbackEngineName()
        {
            return _useFallback ? GetEngineName(_fallbackEngine) : "None";
        }

        /// <summary>
        /// エンジン名を取得
        /// </summary>
        private string GetEngineName(IOcrEngine engine)
        {
            if (engine == _paddleOcrEngine)
                return "PaddleOCR";
            else if (engine == _tesseractOcrEngine)
                return "Tesseract";
            else
                return "Unknown";
        }

        /// <summary>
        /// 成功カウントを更新
        /// </summary>
        private void UpdateSuccessCount(bool isPaddle)
        {
            if (isPaddle)
                _paddleSuccessCount++;
            else
                _tesseractSuccessCount++;
        }

        /// <summary>
        /// 失敗カウントを更新
        /// </summary>
        private void UpdateFailCount(bool isPaddle)
        {
            if (isPaddle)
                _paddleFailCount++;
            else
                _tesseractFailCount++;
        }

        /// <summary>
        /// 成功率に基づいてエンジンを入れ替え
        /// </summary>
        private void SwapEnginesIfNeeded()
        {
            // 十分なデータが集まったら評価
            const int MIN_SAMPLES = 20;
            if (_paddleSuccessCount + _paddleFailCount >= MIN_SAMPLES &&
                _tesseractSuccessCount + _tesseractFailCount >= MIN_SAMPLES)
            {
                float paddleSuccessRate = (float)_paddleSuccessCount / (_paddleSuccessCount + _paddleFailCount);
                float tesseractSuccessRate = (float)_tesseractSuccessCount / (_tesseractSuccessCount + _tesseractFailCount);

                // 明らかな差がある場合のみ入れ替え
                const float THRESHOLD = 0.15f; // 15%以上の差
                if (paddleSuccessRate > tesseractSuccessRate + THRESHOLD && _primaryEngine != _paddleOcrEngine)
                {
                    _primaryEngine = _paddleOcrEngine;
                    _fallbackEngine = _tesseractOcrEngine;
                    Debug.WriteLine($"Switched primary engine to PaddleOCR (success rate: {paddleSuccessRate:P2} vs {tesseractSuccessRate:P2})");
                }
                else if (tesseractSuccessRate > paddleSuccessRate + THRESHOLD && _primaryEngine != _tesseractOcrEngine)
                {
                    _primaryEngine = _tesseractOcrEngine;
                    _fallbackEngine = _paddleOcrEngine;
                    Debug.WriteLine($"Switched primary engine to Tesseract (success rate: {tesseractSuccessRate:P2} vs {paddleSuccessRate:P2})");
                }

                // 統計をリセット（環境変化に対応するため）
                if (_paddleSuccessCount + _paddleFailCount + _tesseractSuccessCount + _tesseractFailCount > 100)
                {
                    _paddleSuccessCount /= 2;
                    _paddleFailCount /= 2;
                    _tesseractSuccessCount /= 2;
                    _tesseractFailCount /= 2;
                }
            }
        }

        /// <summary>
        /// リソースの解放
        /// </summary>
        public void Dispose()
        {
            if (!_isDisposed)
            {
                _paddleOcrEngine?.Dispose();
                _tesseractOcrEngine?.Dispose();
                _isDisposed = true;
            }
        }
    }
}