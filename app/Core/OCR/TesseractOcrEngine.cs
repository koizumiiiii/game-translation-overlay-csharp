using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using GameTranslationOverlay.Core.Utils;
using Tesseract;

namespace GameTranslationOverlay.Core.OCR
{
    public class TesseractOcrEngine : IOcrEngine
    {
        private TesseractEngine _tesseractEngine;
        private bool _isDisposed = false;
        private string _language = "jpn";
        private string _dataPath = "tessdata";

        public async Task InitializeAsync()
        {
            await Task.Run(() =>
            {
                try
                {
                    if (!Directory.Exists(_dataPath))
                    {
                        Debug.WriteLine($"Directory does not exist: {_dataPath}");
                        throw new DirectoryNotFoundException($"Tesseract data directory not found: {_dataPath}");
                    }

                    _tesseractEngine = new TesseractEngine(_dataPath, _language, EngineMode.Default);
                    Debug.WriteLine("Tesseract engine initialized successfully");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Failed to initialize Tesseract: {ex.Message}");
                    throw;
                }
            });
        }

        public async Task<string> RecognizeTextAsync(Rectangle region)
        {
            if (_tesseractEngine == null)
            {
                throw new InvalidOperationException("Tesseract engine not initialized");
            }

            return await Task.Run(() =>
            {
                try
                {
                    using (Bitmap screenshot = ScreenCapture.CaptureRegion(region))
                    {
                        if (screenshot == null)
                        {
                            Debug.WriteLine("Screenshot is null");
                            return string.Empty;
                        }

                        using (var page = _tesseractEngine.Process(screenshot))
                        {
                            string result = page.GetText();
                            return result?.Trim() ?? string.Empty;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error in Tesseract recognition: {ex.Message}");
                    return $"OCR Error: {ex.Message}";
                }
            });
        }

        // 新規追加メソッド - テキスト領域の検出
        public async Task<List<TextRegion>> DetectTextRegionsAsync(Bitmap image)
        {
            if (_tesseractEngine == null)
            {
                throw new InvalidOperationException("Tesseract engine not initialized");
            }

            return await Task.Run(() =>
            {
                List<TextRegion> regions = new List<TextRegion>();

                try
                {
                    using (var page = _tesseractEngine.Process(image))
                    {
                        using (var iterator = page.GetIterator())
                        {
                            iterator.Begin();

                            do
                            {
                                if (iterator.IsAtBeginningOf(PageIteratorLevel.TextLine))
                                {
                                    Rect bounds;
                                    if (iterator.TryGetBoundingBox(PageIteratorLevel.TextLine, out bounds))
                                    {
                                        string lineText = iterator.GetText(PageIteratorLevel.TextLine);
                                        float confidence = iterator.GetConfidence(PageIteratorLevel.TextLine) / 100.0f;

                                        if (!string.IsNullOrWhiteSpace(lineText))
                                        {
                                            regions.Add(new TextRegion(
                                                new Rectangle(bounds.X1, bounds.Y1, bounds.Width, bounds.Height),
                                                lineText.Trim(),
                                                confidence
                                            ));
                                        }
                                    }
                                }
                            } while (iterator.Next(PageIteratorLevel.TextLine));
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error detecting text regions with Tesseract: {ex.Message}");
                }

                return regions;
            });
        }

        public void Dispose()
        {
            if (!_isDisposed)
            {
                _tesseractEngine?.Dispose();
                _isDisposed = true;
            }
        }
    }
}