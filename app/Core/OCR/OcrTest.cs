using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing;
using System.Threading.Tasks;
using System;
using Tesseract;

public class OcrTest
{
    private const string TEST_TEXT = "This is a sample game text";
    public record TestResult(
        string Configuration,
        string RecognizedText,
        double Accuracy,
        long ProcessingTime
    );

    public static async Task<List<TestResult>> RunTests(Rectangle region)
    {
        var results = new List<TestResult>();
        var configurations = new[]
        {
            // エンジンモードの組み合わせ
            new { Mode = EngineMode.Default, Psm = "3", PreProcess = false },
            new { Mode = EngineMode.LstmOnly, Psm = "3", PreProcess = false },
            new { Mode = EngineMode.LstmOnly, Psm = "6", PreProcess = false },
            new { Mode = EngineMode.LstmOnly, Psm = "7", PreProcess = false },
            new { Mode = EngineMode.LstmOnly, Psm = "7", PreProcess = true },
            // 他の組み合わせを追加
        };

        foreach (var config in configurations)
        {
            var stopwatch = new Stopwatch();
            stopwatch.Start();

            using var engine = new TesseractEngine(@"./tessdata", "eng", config.Mode);
            using var bitmap = new Bitmap(region.Width, region.Height);
            using var graphics = Graphics.FromImage(bitmap);

            if (config.PreProcess)
            {
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                graphics.SmoothingMode = SmoothingMode.HighQuality;
            }

            graphics.CopyFromScreen(region.Location, Point.Empty, region.Size);

            using var processedBitmap = config.PreProcess ? PreprocessImage(bitmap) : bitmap;
            using var pix = PixConverter.ToPix(processedBitmap);
            using var page = engine.Process(pix);

            page.SetVariable("tesseract_pageseg_mode", config.Psm);
            var text = page.GetText().Trim();

            stopwatch.Stop();

            results.Add(new TestResult(
                $"Mode: {config.Mode}, PSM: {config.Psm}, PreProcess: {config.PreProcess}",
                text,
                CalculateAccuracy(text, TEST_TEXT),
                stopwatch.ElapsedMilliseconds
            ));

            Debug.WriteLine($"Configuration: {config.Mode}, {config.Psm}, {config.PreProcess}");
            Debug.WriteLine($"Recognized: {text}");
            Debug.WriteLine($"Time: {stopwatch.ElapsedMilliseconds}ms");
            Debug.WriteLine("-------------------");
        }

        return results;
    }

    private static double CalculateAccuracy(string recognized, string expected)
    {
        // 単純なレーベンシュタイン距離を使用
        int distance = ComputeLevenshteinDistance(recognized.ToLower(), expected.ToLower());
        return 1.0 - ((double)distance / Math.Max(recognized.Length, expected.Length));
    }

    private static int ComputeLevenshteinDistance(string s, string t)
    {
        int[,] d = new int[s.Length + 1, t.Length + 1];

        for (int i = 0; i <= s.Length; i++)
            d[i, 0] = i;

        for (int j = 0; j <= t.Length; j++)
            d[0, j] = j;

        for (int j = 1; j <= t.Length; j++)
            for (int i = 1; i <= s.Length; i++)
                d[i, j] = Math.Min(Math.Min(
                    d[i - 1, j] + 1,
                    d[i, j - 1] + 1),
                    d[i - 1, j - 1] + (s[i - 1] == t[j - 1] ? 0 : 1));

        return d[s.Length, t.Length];
    }

    private static Bitmap PreprocessImage(Bitmap original)
    {
        var processed = new Bitmap(original.Width, original.Height);
        using (var graphics = Graphics.FromImage(processed))
        {
            var matrix = new ColorMatrix
            {
                Matrix33 = 1.0f,
                Matrix00 = 1.5f,
                Matrix11 = 1.5f,
                Matrix22 = 1.5f
            };

            var attributes = new ImageAttributes();
            attributes.SetColorMatrix(matrix);

            graphics.DrawImage(original,
                new Rectangle(0, 0, original.Width, original.Height),
                0, 0, original.Width, original.Height,
                GraphicsUnit.Pixel,
                attributes);
        }
        return processed;
    }
}