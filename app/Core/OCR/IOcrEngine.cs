using System.Drawing;
using System.Threading.Tasks;

namespace GameTranslationOverlay.Core.OCR
{
    /// <summary>
    /// Interface for OCR engine operations
    /// </summary>
    public interface IOcrEngine
    {
        /// <summary>
        /// Performs OCR on the specified region of the screen
        /// </summary>
        /// <param name="region">Region to perform OCR on</param>
        /// <returns>Recognized text</returns>
        Task<string> RecognizeTextAsync(Rectangle region);

        /// <summary>
        /// Initializes the OCR engine
        /// </summary>
        Task InitializeAsync();

        /// <summary>
        /// Releases resources used by the OCR engine
        /// </summary>
        void Dispose();
    }
}