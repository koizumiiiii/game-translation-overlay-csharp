using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using PaddleOCRSharp;

namespace GameTranslationOverlay.Core.OCR
{
    public class RecognitionResult
    {
        public string Text { get; set; }
        public double Confidence { get; set; }
        public Rectangle Bounds { get; set; }
        public double Angle { get; set; }
    }

    public class PaddleOcrEngine : IOcrEngine, IDisposable
    {
        private readonly OCRModelConfig _modelConfig;
        private readonly OCRParameter _parameter;
        private PaddleOCREngine _engine;
        private bool _isInitialized;
        private bool _disposed;

        public PaddleOcrEngine(bool enableAngleDetection = true)
        {
            _modelConfig = null;  // nullに変更（デフォルトパスを使用）
            _parameter = new OCRParameter
            {
                use_angle_cls = enableAngleDetection,
                det_db_thresh = 0.3f,
                det_db_box_thresh = 0.6f
            };
            Debug.WriteLine("PaddleOCR Parameter options:");
            foreach (var prop in _parameter.GetType().GetProperties())
            {
                Debug.WriteLine($"{prop.Name}: {prop.GetValue(_parameter)}");
            }
        }

        public async Task InitializeAsync()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(PaddleOcrEngine));

            try
            {
                await Task.Run(() =>
                {
                    _engine = new PaddleOCREngine(_modelConfig, _parameter);
                    _isInitialized = true;
                    Debug.WriteLine("PaddleOCR Engine initialized successfully");
                });
            }
            catch (Exception ex)
            {
                _isInitialized = false;
                Debug.WriteLine($"PaddleOCR initialization failed: {ex.Message}");
                throw new InvalidOperationException("Failed to initialize OCR engine.", ex);
            }
        }

        public async Task<string> RecognizeTextAsync(Rectangle region)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(PaddleOcrEngine));

            if (!_isInitialized)
                throw new InvalidOperationException("OCR engine is not initialized.");

            return await Task.Run(() =>
            {
                using (var bitmap = new Bitmap(region.Width, region.Height))
                {
                    using (var graphics = Graphics.FromImage(bitmap))
                    {
                        graphics.CopyFromScreen(region.Location, Point.Empty, region.Size);
                    }

                    try
                    {
                        var ocrResult = _engine.DetectText(bitmap);
                        Debug.WriteLine("OCR Result properties:");
                        foreach (var prop in ocrResult.GetType().GetProperties())
                        {
                            Debug.WriteLine($"{prop.Name}: {prop.GetValue(ocrResult)}");
                        }
                        return ocrResult.Text;
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"OCR recognition failed: {ex.Message}");
                        throw;
                    }
                }
            });
        }

        public async Task<IEnumerable<RecognitionResult>> RecognizeDetailedAsync(Rectangle region)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(PaddleOcrEngine));

            if (!_isInitialized)
                throw new InvalidOperationException("OCR engine is not initialized.");

            return await Task.Run(() =>
            {
                using (var bitmap = new Bitmap(region.Width, region.Height))
                {
                    using (var graphics = Graphics.FromImage(bitmap))
                    {
                        graphics.CopyFromScreen(region.Location, Point.Empty, region.Size);
                    }

                    try
                    {
                        var ocrResult = _engine.DetectText(bitmap);
                        Debug.WriteLine("OCR Result properties:");
                        foreach (var prop in ocrResult.GetType().GetProperties())
                        {
                            Debug.WriteLine($"{prop.Name}: {prop.GetValue(ocrResult)}");
                        }

                        return new List<RecognitionResult>
                        {
                            new RecognitionResult
                            {
                                Text = ocrResult.Text,
                                Confidence = 1.0, // 実際の値は後で調整
                                Bounds = region,
                                Angle = 0
                            }
                        };
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"OCR recognition failed: {ex.Message}");
                        throw;
                    }
                }
            });
        }

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
                Debug.WriteLine("PaddleOCR Engine disposed");
            }

            _disposed = true;
        }
    }
}