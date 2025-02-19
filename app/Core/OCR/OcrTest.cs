using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Threading.Tasks;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Drawing.Drawing2D;
using Tesseract;

namespace GameTranslationOverlay.Core.OCR
{
    public class OcrTest
    {
        private const int MIN_FONT_SIZE_PIXELS = 10;

        public class TestResult
        {
            public string Configuration { get; }
            public string RecognizedText { get; }
            public double Accuracy { get; }
            public long ProcessingTime { get; }

            public TestResult(string configuration, string recognizedText, double accuracy, long processingTime)
            {
                Configuration = configuration;
                RecognizedText = recognizedText;
                Accuracy = accuracy;
                ProcessingTime = processingTime;
            }
        }

        public static async Task<List<TestResult>> RunTests(Rectangle region)
        {
            return await Task.Run(() =>
            {
                var results = new List<TestResult>();

                // フォントサイズが小さい場合のスケーリング係数を計算
                double scaleFactor = CalculateOptimalScale(region.Height);

                var configurations = new[]
                {
                    // 基本設定（スケーリングあり/なし）
                    new { Mode = EngineMode.Default, Psm = PageSegMode.SingleBlock, Scale = scaleFactor },
                    new { Mode = EngineMode.Default, Psm = PageSegMode.Auto, Scale = scaleFactor },
                    
                    // スケーリングが不要な場合は元のサイズでも試行
                    new { Mode = EngineMode.Default, Psm = PageSegMode.SingleBlock, Scale = 1.0 },
                    new { Mode = EngineMode.Default, Psm = PageSegMode.Auto, Scale = 1.0 }
                };

                var tessDataPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tessdata");
                Debug.WriteLine($"Tesseract data path: {tessDataPath}");
                Debug.WriteLine($"Region height: {region.Height}px, Scale factor: {scaleFactor}");

                foreach (var config in configurations)
                {
                    var stopwatch = new Stopwatch();
                    stopwatch.Start();

                    try
                    {
                        using (var engine = new TesseractEngine(tessDataPath, "eng+jpn", config.Mode))
                        using (var bitmap = new Bitmap(region.Width, region.Height))
                        using (var graphics = Graphics.FromImage(bitmap))
                        {
                            graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                            graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                            graphics.SmoothingMode = SmoothingMode.HighQuality;

                            graphics.CopyFromScreen(region.Location, Point.Empty, region.Size);

                            // スケーリング処理
                            var scaledBitmap = config.Scale != 1.0
                                ? ScaleImage(bitmap, config.Scale)
                                : bitmap;

                            using (var pix = PixConverter.ToPix(scaledBitmap))
                            using (var page = engine.Process(pix))
                            {
                                var text = page.GetText().Trim();
                                stopwatch.Stop();

                                // 認識テキストの評価
                                double accuracy = CalculateTextQuality(text);

                                results.Add(new TestResult(
                                    $"Mode: {config.Mode}, Scale: {config.Scale:F1}",
                                    text,
                                    accuracy,
                                    stopwatch.ElapsedMilliseconds
                                ));

                                Debug.WriteLine($"Configuration: {config.Mode}, Scale: {config.Scale:F1}");
                                Debug.WriteLine($"Recognized: {text}");
                                Debug.WriteLine($"Time: {stopwatch.ElapsedMilliseconds}ms");
                                Debug.WriteLine("-------------------");
                            }

                            if (config.Scale != 1.0 && scaledBitmap != bitmap)
                            {
                                scaledBitmap.Dispose();
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error with configuration - Mode: {config.Mode}, Scale: {config.Scale}");
                        Debug.WriteLine($"Error: {ex.Message}");
                        if (ex.InnerException != null)
                        {
                            Debug.WriteLine($"Inner Error: {ex.InnerException.Message}");
                        }
                        Debug.WriteLine("-------------------");
                    }
                }

                return results;
            });
        }

        private static double CalculateOptimalScale(int height)
        {
            // 最小フォントサイズを下回る場合はスケーリングを適用
            if (height < MIN_FONT_SIZE_PIXELS)
            {
                return (double)MIN_FONT_SIZE_PIXELS / height;
            }
            return 1.0;
        }

        private static Bitmap ScaleImage(Bitmap original, double scale)
        {
            int newWidth = (int)(original.Width * scale);
            int newHeight = (int)(original.Height * scale);
            var scaled = new Bitmap(newWidth, newHeight);

            using (var graphics = Graphics.FromImage(scaled))
            {
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.DrawImage(original, 0, 0, newWidth, newHeight);
            }

            return scaled;
        }

        private static double CalculateTextQuality(string text)
        {
            // 文字列の品質を評価
            // 有効な文字（アルファベット、数字、句読点、日本語文字）の割合を計算
            int validChars = text.Count(c =>
                char.IsLetterOrDigit(c) ||
                char.IsPunctuation(c) ||
                char.IsWhiteSpace(c) ||
                (c >= 0x3040 && c <= 0x309F) || // ひらがな
                (c >= 0x30A0 && c <= 0x30FF) || // カタカナ
                (c >= 0x4E00 && c <= 0x9FFF)    // 漢字
            );

            return (double)validChars / text.Length;
        }
    }
}