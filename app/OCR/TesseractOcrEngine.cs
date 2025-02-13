using System;
using System.Drawing;
using System.Threading.Tasks;
using Tesseract;

namespace GameTranslationOverlay.Core.OCR
{
    /// <summary>
    /// OCR implementation using Tesseract
    /// </summary>
    public class TesseractOcrEngine : IOcrEngine, IDisposable
    {
        private TesseractEngine _engine;
        private bool _isInitialized;
        private bool _disposed;

        /// <summary>
        /// Initializes the Tesseract engine
        /// </summary>
        public async Task InitializeAsync()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(TesseractOcrEngine));

            try
            {
                await Task.Run(() =>
                {
                    _engine = new TesseractEngine(@"./tessdata", "jpn", EngineMode.Default);
                    _isInitialized = true;
                });
            }
            catch (Exception ex)
            {
                _isInitialized = false;
                throw new InvalidOperationException("Failed to initialize OCR engine.", ex);
            }
        }

        /// <summary>
        /// Performs OCR on the specified region
        /// </summary>
        public async Task<string> RecognizeTextAsync(Rectangle region)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(TesseractOcrEngine));

            if (!_isInitialized)
                throw new InvalidOperationException("OCR engine is not initialized.");

            if (region.Width <= 0 || region.Height <= 0)
                throw new ArgumentException("Invalid region size.", nameof(region));

            return await Task.Run(() =>
            {
                using (var bitmap = new Bitmap(region.Width, region.Height))
                {
                    using (var graphics = Graphics.FromImage(bitmap))
                    {
                        graphics.CopyFromScreen(region.Location, Point.Empty, region.Size);
                    }

                    using (var pix = PixConverter.ToPix(bitmap))
                    {
                        using (var page = _engine.Process(pix))
                        {
                            return page.GetText();
                        }
                    }
                }
            });
        }

        /// <summary>
        /// Disposes of resources
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
                return;

            if (disposing)
            {
                _engine?.Dispose();
            }

            _disposed = true;
        }
    }
}