using System;
using System.Diagnostics;
using System.Drawing;
using System.Threading.Tasks;
using Windows.Media.Ocr;
using Windows.Graphics.Imaging;
using Tesseract;
using System.IO;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Storage.Streams;
using System.Drawing.Imaging;

namespace GameTranslationOverlay.Core.OCR.Benchmark
{
    public class OcrBenchmark : IDisposable
    {
        private TesseractEngine _tesseractEngine;
        private OcrEngine _windowsOcrEngine;
        private bool _disposed;

        public OcrBenchmark()
        {
            try
            {
                // Initialize Tesseract with additional configuration
                _tesseractEngine = new TesseractEngine(@"./tessdata", "eng", EngineMode.Default);

                // OCR設定を調整
                _tesseractEngine.SetVariable("tessedit_pageseg_mode", "6");  // PSM_SPARSE_TEXT - 疎らなテキスト
                _tesseractEngine.SetVariable("tessedit_char_whitelist", "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789.,!?:"); // 認識する文字を制限
                _tesseractEngine.SetVariable("classify_bln_numeric_mode", "1"); // 数字認識モードを有効化

                // Windows OCR initialization
                _windowsOcrEngine = OcrEngine.TryCreateFromLanguage(new Windows.Globalization.Language("en-US"));

                if (_windowsOcrEngine == null)
                {
                    Debug.WriteLine("Failed to create Windows OCR engine. Make sure the English language pack is installed.");
                    throw new InvalidOperationException("Windows OCR engine initialization failed");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error initializing OCR engines: {ex.Message}");
                throw;
            }
        }

        public class BenchmarkResult
        {
            public double ProcessingTimeMs { get; set; }
            public string RecognizedText { get; set; }
            public double MemoryUsageMB { get; set; }
            public double Accuracy { get; set; }
        }

        public async Task<BenchmarkResult> RunTesseractBenchmark(Bitmap image, string groundTruth = null)
        {
            var stopwatch = new Stopwatch();
            var startMemory = GC.GetTotalMemory(true);

            string text = null;
            stopwatch.Start();

            await Task.Run(() => {
                using (var processedImage = PreprocessImage(image))
                using (var pix = PixConverter.ToPix(processedImage))
                using (var page = _tesseractEngine.Process(pix))
                {
                    text = page.GetText().Trim();
                }
            });

            stopwatch.Stop();

            var endMemory = GC.GetTotalMemory(false);
            var memoryUsed = (endMemory - startMemory) / (1024.0 * 1024.0);

            return new BenchmarkResult
            {
                ProcessingTimeMs = stopwatch.ElapsedMilliseconds,
                RecognizedText = text,
                MemoryUsageMB = memoryUsed,
                Accuracy = groundTruth != null ? CalculateAccuracy(text, groundTruth) : 0
            };
        }

        private Bitmap PreprocessImage(Bitmap original)
        {
            var copy = new Bitmap(original.Width, original.Height);
            using (var g = Graphics.FromImage(copy))
            {
                // 背景を白に
                g.Clear(Color.White);

                // 画像を描画
                using (var attributes = new ImageAttributes())
                {
                    // コントラストを強調
                    var colorMatrix = new ColorMatrix(new float[][]
                    {
                new float[] {2.0f, 0, 0, 0, 0},
                new float[] {0, 2.0f, 0, 0, 0},
                new float[] {0, 0, 2.0f, 0, 0},
                new float[] {0, 0, 0, 1.0f, 0},
                new float[] {-0.5f, -0.5f, -0.5f, 0, 1.0f}
                    });

                    attributes.SetColorMatrix(colorMatrix);

                    g.DrawImage(original,
                        new Rectangle(0, 0, copy.Width, copy.Height),
                        0, 0, original.Width, original.Height,
                        GraphicsUnit.Pixel,
                        attributes);
                }
            }
            return copy;
        }

        public async Task<BenchmarkResult> RunWindowsOcrBenchmark(Bitmap image, string groundTruth = null)
        {
            var stopwatch = new Stopwatch();
            var startMemory = GC.GetTotalMemory(true);

            stopwatch.Start();
            try
            {
                var softwareBitmap = await ConvertToSoftwareBitmap(image);
                var ocrResult = await _windowsOcrEngine.RecognizeAsync(softwareBitmap).AsTask();
                var text = ocrResult.Text;
                stopwatch.Stop();

                var endMemory = GC.GetTotalMemory(false);
                var memoryUsed = (endMemory - startMemory) / (1024.0 * 1024.0);

                return new BenchmarkResult
                {
                    ProcessingTimeMs = stopwatch.ElapsedMilliseconds,
                    RecognizedText = text,
                    MemoryUsageMB = memoryUsed,
                    Accuracy = groundTruth != null ? CalculateAccuracy(text, groundTruth) : 0
                };
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Windows OCR error: {ex.Message}");
                throw;
            }
        }

        private double CalculateAccuracy(string recognized, string groundTruth)
        {
            if (string.IsNullOrEmpty(recognized) || string.IsNullOrEmpty(groundTruth))
                return 0;

            var distance = ComputeLevenshteinDistance(recognized, groundTruth);
            var maxLength = Math.Max(recognized.Length, groundTruth.Length);
            return 1 - (distance / (double)maxLength);
        }

        private int ComputeLevenshteinDistance(string s, string t)
        {
            var m = s.Length;
            var n = t.Length;
            var d = new int[m + 1, n + 1];

            for (var i = 0; i <= m; i++)
                d[i, 0] = i;
            for (var j = 0; j <= n; j++)
                d[0, j] = j;

            for (var j = 1; j <= n; j++)
            {
                for (var i = 1; i <= m; i++)
                {
                    if (s[i - 1] == t[j - 1])
                        d[i, j] = d[i - 1, j - 1];
                    else
                        d[i, j] = Math.Min(Math.Min(
                            d[i - 1, j] + 1,     // deletion
                            d[i, j - 1] + 1),    // insertion
                            d[i - 1, j - 1] + 1); // substitution
                }
            }

            return d[m, n];
        }

        private async Task<SoftwareBitmap> ConvertToSoftwareBitmap(Bitmap bitmap)
        {
            var raStream = new InMemoryRandomAccessStream();
            var memoryStream = new MemoryStream();
            try
            {
                // ビットマップをメモリストリームに保存
                bitmap.Save(memoryStream, System.Drawing.Imaging.ImageFormat.Bmp);
                memoryStream.Position = 0;

                // メモリストリームの内容をInMemoryRandomAccessStreamにコピー
                byte[] buffer = memoryStream.ToArray();
                var dataWriter = new DataWriter(raStream);
                dataWriter.WriteBytes(buffer);
                await dataWriter.StoreAsync();
                await dataWriter.FlushAsync();
                dataWriter.Dispose();

                // ストリームの位置を先頭に戻す
                raStream.Seek(0);

                // デコードとSoftwareBitmapの作成
                var decoder = await BitmapDecoder.CreateAsync(raStream);
                var frame = await decoder.GetFrameAsync(0);
                var softwareBitmap = await frame.GetSoftwareBitmapAsync();

                return softwareBitmap;
            }
            finally
            {
                // リソースの解放
                memoryStream.Dispose();
                raStream.Dispose();
            }
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
                _tesseractEngine?.Dispose();
                _tesseractEngine = null;
            }

            _disposed = true;
        }
    }
}