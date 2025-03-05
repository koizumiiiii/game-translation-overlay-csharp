using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading.Tasks;

namespace GameTranslationOverlay.Core.OCR
{
    /// <summary>
    /// Tesseract OCRエンジンの実装（非推奨・現在は使用されていません）
    /// </summary>
    public class TesseractOcrEngine : IOcrEngine
    {
        // PaddleOCRを使用するため、このエンジンは実際には使用されません
        public TesseractOcrEngine()
        {
            System.Diagnostics.Debug.WriteLine("TesseractOcrEngine is deprecated and not used in this application.");
        }

        public Task InitializeAsync()
        {
            // 何もしない
            return Task.CompletedTask;
        }

        public Task<string> RecognizeTextAsync(Rectangle region)
        {
            // 空の結果を返す
            return Task.FromResult(string.Empty);
        }

        public Task<List<TextRegion>> DetectTextRegionsAsync(Bitmap image)
        {
            // 空のリストを返す
            return Task.FromResult(new List<TextRegion>());
        }

        public void EnablePreprocessing(bool enable)
        {
            // 何もしない
        }

        public void Dispose()
        {
            // 何もしない
        }
    }
}