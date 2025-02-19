using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading.Tasks;
using System.IO;
using System.Diagnostics;
using System.Linq;

namespace GameTranslationOverlay.Core.OCR.Benchmark
{
    public class OcrBenchmarkTest
    {
        private readonly string[] _testScenarios = {
            "dialog_text",
            "menu_text",
            "system_message",
            "battle_text",
            "item_description"
        };

        private readonly Dictionary<string, string> _groundTruth = new Dictionary<string, string>
        {
            { "dialog_text", "Hard to believe an esper's been found frozen there a thousand years after the War of the Magi..." },
            { "menu_text", "Items\nAbilities\nEquip\nStatus\nFormation\nConfiguration\nQuick Save\nSave\nLoad" },
            { "system_message", "Save complete." },
            { "battle_text", "Fire\n168" },
            { "item_description", "A pendant worn by the girl who pilots the Magitek armor." }
        };

        public async Task RunAllTests()
        {
            var results = new List<(string Scenario, OcrBenchmark.BenchmarkResult Tesseract)>();

            using (var benchmark = new OcrBenchmark())
            {
                foreach (var scenario in _testScenarios)
                {
                    Debug.WriteLine($"\nTesting scenario: {scenario}");
                    var imagePath = Path.Combine("Core", "OCR", "Resources", "TestImages", $"{scenario}.png");

                    try
                    {
                        using (var image = new Bitmap(imagePath))
                        {
                            var groundTruth = _groundTruth[scenario];
                            var tesseractResult = await benchmark.RunTesseractBenchmark(image, groundTruth);
                            results.Add((scenario, tesseractResult));

                            Debug.WriteLine($"Scenario: {scenario}");
                            Debug.WriteLine($"Tesseract - Time: {tesseractResult.ProcessingTimeMs}ms, Accuracy: {tesseractResult.Accuracy:P2}");
                            Debug.WriteLine($"Recognized Text:\n{tesseractResult.RecognizedText}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error testing scenario {scenario}: {ex.Message}");
                        continue;
                    }
                }
            }

            PrintComparisonResults(results);
        }

        private void PrintComparisonResults(List<(string Scenario, OcrBenchmark.BenchmarkResult Tesseract)> results)
        {
            Debug.WriteLine("\n=== Benchmark Results ===");
            Debug.WriteLine("Scenario\t\tTime(ms)\tMemory(MB)\tAccuracy");
            Debug.WriteLine("------------------------------------------------------------------------");

            foreach (var result in results)
            {
                PrintResultRow(result.Scenario, result.Tesseract);
            }

            var avgTime = results.Average(r => r.Tesseract.ProcessingTimeMs);
            var avgMemory = results.Average(r => r.Tesseract.MemoryUsageMB);
            var avgAccuracy = results.Average(r => r.Tesseract.Accuracy);

            Debug.WriteLine("\n=== Averages ===");
            Debug.WriteLine($"Time: {avgTime:F2}ms\tMemory: {avgMemory:F2}MB\tAccuracy: {avgAccuracy:P2}");
        }

        private void PrintResultRow(string scenario, OcrBenchmark.BenchmarkResult result)
        {
            Debug.WriteLine($"{scenario,-15}\t{result.ProcessingTimeMs,8:F2}\t{result.MemoryUsageMB,8:F2}\t{result.Accuracy:P2}");
        }

        private (double Time, double Memory, double Accuracy) CalculateAverages(List<OcrBenchmark.BenchmarkResult> results)
        {
            if (results.Count == 0)
                return (0, 0, 0);

            return (
                Time: results.ConvertAll(r => r.ProcessingTimeMs).Average(),
                Memory: results.ConvertAll(r => r.MemoryUsageMB).Average(),
                Accuracy: results.ConvertAll(r => r.Accuracy).Average()
            );
        }
    }
}