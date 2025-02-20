using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Linq;
using System.Drawing.Drawing2D;

namespace GameTranslationOverlay.Core.OCR
{
    public class OcrTest
    {
        private const int MIN_FONT_SIZE_PIXELS = 10;

        public class TestResult
        {
            public string EngineName { get; }
            public string Configuration { get; }
            public string RecognizedText { get; }
            public double Accuracy { get; }
            public long ProcessingTime { get; }
            public Dictionary<string, object> AdditionalInfo { get; }

            public TestResult(
                string engineName,
                string configuration,
                string recognizedText,
                double accuracy,
                long processingTime,
                Dictionary<string, object> additionalInfo = null)
            {
                EngineName = engineName;
                Configuration = configuration;
                RecognizedText = recognizedText;
                Accuracy = accuracy;
                ProcessingTime = processingTime;
                AdditionalInfo = additionalInfo ?? new Dictionary<string, object>();
            }
        }

        public static async Task<List<TestResult>> RunTests(Rectangle region)
        {
            var results = new List<TestResult>();
            double scaleFactor = CalculateOptimalScale(region.Height);

            // TesseractOCRのテスト
            var tesseractResults = await RunTesseractTests(region, scaleFactor);
            results.AddRange(tesseractResults);

            // PaddleOCRのテスト
            var paddleResults = await RunPaddleTests(region, scaleFactor);
            results.AddRange(paddleResults);

            return results;
        }

        private static async Task<List<TestResult>> RunTesseractTests(Rectangle region, double scaleFactor)
        {
            var results = new List<TestResult>();
            using (var engine = new TesseractOcrEngine())
            {
                await engine.InitializeAsync();

                var stopwatch = new Stopwatch();
                stopwatch.Start();
                var text = await engine.RecognizeTextAsync(region);
                stopwatch.Stop();

                results.Add(new TestResult(
                    "Tesseract",
                    $"Scale: {scaleFactor:F1}",
                    text,
                    CalculateTextQuality(text),
                    stopwatch.ElapsedMilliseconds
                ));
            }
            return results;
        }

        private static async Task<List<TestResult>> RunPaddleTests(Rectangle region, double scaleFactor)
        {
            var results = new List<TestResult>();
            using (var engine = new PaddleOcrEngine())
            {
                await engine.InitializeAsync();

                // 基本的な認識テスト
                var stopwatch = new Stopwatch();
                stopwatch.Start();
                var text = await engine.RecognizeTextAsync(region);
                stopwatch.Stop();

                results.Add(new TestResult(
                    "PaddleOCR",
                    "Basic Recognition",
                    text,
                    CalculateTextQuality(text),
                    stopwatch.ElapsedMilliseconds
                ));

                // 詳細な認識テスト
                stopwatch.Restart();
                var detailedResults = await engine.RecognizeDetailedAsync(region);
                stopwatch.Stop();

                foreach (var detail in detailedResults)
                {
                    results.Add(new TestResult(
                        "PaddleOCR",
                        "Detailed Recognition",
                        detail.Text,
                        detail.Confidence,
                        stopwatch.ElapsedMilliseconds,
                        new Dictionary<string, object>
                        {
                            { "Bounds", detail.Bounds },
                            { "Angle", detail.Angle }
                        }
                    ));
                }
            }
            return results;
        }

        private static double CalculateOptimalScale(int height)
        {
            return height < MIN_FONT_SIZE_PIXELS ? (double)MIN_FONT_SIZE_PIXELS / height : 1.0;
        }

        private static double CalculateTextQuality(string text)
        {
            int validChars = text.Count(c =>
                char.IsLetterOrDigit(c) ||
                char.IsPunctuation(c) ||
                char.IsWhiteSpace(c) ||
                (c >= 0x3040 && c <= 0x309F) || // ひらがな
                (c >= 0x30A0 && c <= 0x30FF) || // カタカナ
                (c >= 0x4E00 && c <= 0x9FFF)    // 漢字
            );

            return text.Length > 0 ? (double)validChars / text.Length : 0;
        }
    }
}