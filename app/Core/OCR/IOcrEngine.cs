using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading.Tasks;

namespace GameTranslationOverlay.Core.OCR
{
    /// <summary>
    /// OCRエンジンのインターフェース
    /// </summary>
    public interface IOcrEngine : IDisposable
    {
        /// <summary>
        /// 初期化
        /// </summary>
        Task InitializeAsync();

        /// <summary>
        /// テキスト認識
        /// </summary>
        /// <param name="region">認識対象の領域</param>
        /// <returns>認識したテキスト</returns>
        Task<string> RecognizeTextAsync(Rectangle region);

        /// <summary>
        /// 画像からテキスト領域を検出
        /// </summary>
        /// <param name="image">テキスト検出対象の画像</param>
        /// <returns>検出したテキスト領域のリスト</returns>
        Task<List<TextRegion>> DetectTextRegionsAsync(Bitmap image);
    }
}