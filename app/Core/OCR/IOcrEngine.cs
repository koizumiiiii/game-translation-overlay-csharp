using System.Collections.Generic;
using System.Drawing;
using System.Threading.Tasks;

namespace GameTranslationOverlay.Core.OCR
{
    public interface IOcrEngine : System.IDisposable
    {
        Task<string> RecognizeTextAsync(Rectangle region);
        Task InitializeAsync();

        // 新規追加メソッド - テキスト領域検出
        Task<List<TextRegion>> DetectTextRegionsAsync(Bitmap image);
    }
}