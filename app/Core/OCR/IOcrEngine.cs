using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading.Tasks;

namespace GameTranslationOverlay.Core.OCR
{
    public interface IOcrEngine : IDisposable
    {
        Task InitializeAsync();
        Task<string> RecognizeTextAsync(Rectangle region);
        Task<List<TextRegion>> DetectTextRegionsAsync(Bitmap image);
        void EnablePreprocessing(bool enable);
    }
}