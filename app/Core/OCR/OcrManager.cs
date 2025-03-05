using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Threading.Tasks;
using GameTranslationOverlay.Core.Utils;

namespace GameTranslationOverlay.Core.OCR
{
    public class OcrManager : IDisposable, IOcrEngine
    {
        // OCRエンジン
        private PaddleOcrEngine _primaryEngine;
        private bool _isDisposed = false;
        private float _confidenceThreshold = 0.6f;

        // パフォーマンス測定
        private readonly Stopwatch _stopwatch = new Stopwatch();

        public OcrManager()
        {
            _primaryEngine = new PaddleOcrEngine();
        }

        public async Task InitializeAsync()
        {
            try
            {
                Debug.WriteLine("Initializing OCR Manager...");
                await _primaryEngine.InitializeAsync();
                Debug.WriteLine("OCR engines initialized successfully");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error initializing OCR engine: {ex.GetType().Name}: {ex.Message}");
                throw;
            }
        }

        public async Task<string> RecognizeTextAsync(Rectangle region)
        {
            if (_isDisposed) throw new ObjectDisposedException(nameof(OcrManager));

            try
            {
                _stopwatch.Restart();
                string result = await _primaryEngine.RecognizeTextAsync(region);
                _stopwatch.Stop();
                Debug.WriteLine($"OCR recognition completed in {_stopwatch.ElapsedMilliseconds}ms");
                return result;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in OCR recognition: {ex.Message}");
                return string.Empty;
            }
        }

        public async Task<List<TextRegion>> DetectTextRegionsAsync(Bitmap image)
        {
            if (_isDisposed) throw new ObjectDisposedException(nameof(OcrManager));

            try
            {
                _stopwatch.Restart();
                var result = await _primaryEngine.DetectTextRegionsAsync(image);
                _stopwatch.Stop();

                // 信頼度でフィルタリング
                result = result.FindAll(r => r.Confidence >= _confidenceThreshold);

                Debug.WriteLine($"Text region detection completed in {_stopwatch.ElapsedMilliseconds}ms, found {result.Count} regions");
                return result;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in text region detection: {ex.Message}");
                return new List<TextRegion>();
            }
        }

        public void EnablePreprocessing(bool enable)
        {
            if (_isDisposed) throw new ObjectDisposedException(nameof(OcrManager));
            _primaryEngine.EnablePreprocessing(enable);
        }

        /// <summary>
        /// OCR結果の信頼度閾値を設定する
        /// </summary>
        /// <param name="threshold">信頼度閾値（0.0～1.0）</param>
        public void SetConfidenceThreshold(float threshold)
        {
            _confidenceThreshold = Math.Max(0.0f, Math.Min(1.0f, threshold));
            Debug.WriteLine($"Confidence threshold set to {_confidenceThreshold:F2}");
        }

        /// <summary>
        /// 画像前処理のオプションを設定する
        /// </summary>
        /// <param name="options">前処理オプション</param>
        public void SetPreprocessingOptions(PreprocessingOptions options)
        {
            // この実装では何もしないが、インターフェイスの互換性のために残す
            Debug.WriteLine("SetPreprocessingOptions called (no effect in this implementation)");
        }

        /// <summary>
        /// 主要OCRエンジンの名前を取得
        /// </summary>
        public string GetPrimaryEngineName()
        {
            return "PaddleOCR";
        }

        public void Dispose()
        {
            if (!_isDisposed)
            {
                _primaryEngine?.Dispose();
                _isDisposed = true;
                Debug.WriteLine("OcrManager disposed");
            }
        }
    }
}