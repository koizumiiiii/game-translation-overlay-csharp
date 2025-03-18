using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using PaddleOCRSharp;
using System.Collections.Generic;

namespace GameTranslationOverlay.Core.OCR
{
    public class PaddleOcrEngine : IOcrEngine
    {
        private PaddleOCREngine _paddleOcr;
        private OCRModelConfig _modelConfig;
        private bool _enablePreprocessing = false;

        public PaddleOcrEngine()
        {
            // コンストラクタ
        }

        public async Task InitializeAsync()
        {
            await Task.Run(() =>
            {
                try
                {
                    // アプリケーションディレクトリを取得
                    string appDir = AppDomain.CurrentDomain.BaseDirectory;

                    // 検索するパスのリスト
                    List<string> possibleModelDirs = new List<string>
                    {
                        Path.Combine(appDir, "PaddleOCRModels"),
                        Path.Combine(appDir, "inference"),
                        Path.Combine(Path.GetDirectoryName(typeof(PaddleOCREngine).Assembly.Location), "inference"),
                        Path.Combine(appDir, @"..\..\..\PaddleOCRModels"),
                        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "PaddleOCRSharp")
                    };

                    Debug.WriteLine("Searching for PaddleOCR model directories:");
                    string foundModelDir = null;

                    foreach (string dir in possibleModelDirs)
                    {
                        Debug.WriteLine($"  - Checking: {dir} - {(Directory.Exists(dir) ? "exists" : "not found")}");
                        if (Directory.Exists(dir))
                        {
                            foundModelDir = dir;
                            break;
                        }
                    }

                    if (foundModelDir == null)
                    {
                        throw new DirectoryNotFoundException("Could not find model directory");
                    }

                    Debug.WriteLine($"Using model directory: {foundModelDir}");

                    // PaddleOCR の設定
                    _modelConfig = new OCRModelConfig();

                    // 最新の初期化方法を使用
                    try
                    {
                        _paddleOcr = new PaddleOCREngine();
                        Debug.WriteLine("PaddleOCR engine initialized with default constructor");
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Failed to initialize PaddleOCR with default constructor: {ex.Message}");

                        try
                        {
                            // モデル設定のみを使用
                            _paddleOcr = new PaddleOCREngine(_modelConfig);
                            Debug.WriteLine("PaddleOCR engine initialized with model config");
                        }
                        catch (Exception ex2)
                        {
                            Debug.WriteLine($"Failed to initialize PaddleOCR with model config: {ex2.Message}");
                            throw;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Failed to initialize PaddleOCR: {ex.GetType().Name}: {ex.Message}");
                    DumpDirectoryInfo();
                    throw;
                }
            });
        }

        public async Task<string> RecognizeTextAsync(Rectangle region)
        {
            if (_paddleOcr == null)
            {
                throw new InvalidOperationException("PaddleOCR engine is not initialized");
            }

            return await Task.Run(() =>
            {
                try
                {
                    using (Bitmap screenCapture = CaptureScreen(region))
                    {
                        Bitmap processedImage = screenCapture;

                        var result = _paddleOcr.DetectText(processedImage);
                        return result?.Text ?? string.Empty;
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error in OCR recognition: {ex.Message}");
                    return string.Empty;
                }
            });
        }

        public async Task<List<TextRegion>> DetectTextRegionsAsync(Bitmap image)
        {
        if (_paddleOcr == null)
        {
        throw new InvalidOperationException("PaddleOCR engine is not initialized");
        }

        return await Task.Run(() =>
        {
        try
        {
        Bitmap processedImage = image;

        // 画像サイズの制限チェック
        if (image.Width > 2500 || image.Height > 1100)
                {
                    Debug.WriteLine($"大きな画像サイズを検出: {image.Width}x{image.Height} - リサイズを適用します");
                    float scale = Math.Min(2500f / image.Width, 1100f / image.Height);
                    using (Bitmap resized = new Bitmap((int)(image.Width * scale), (int)(image.Height * scale)))
                    {
                        using (Graphics g = Graphics.FromImage(resized))
                        {
                            g.DrawImage(image, 0, 0, resized.Width, resized.Height);
                        }
                        processedImage = resized;
                    }
                }

                var result = _paddleOcr.DetectText(processedImage);
                List<TextRegion> textRegions = new List<TextRegion>();

                    if (result != null)
                    {
                        // リフレクションを使用してBoxesプロパティにアクセス
                        var boxesProperty = result.GetType().GetProperty("Boxes");
                        if (boxesProperty != null)
                        {
                            var boxes = boxesProperty.GetValue(result) as System.Collections.IEnumerable;
                            if (boxes != null)
                            {
                                foreach (var box in boxes)
                                {
                                    // ボックスオブジェクトからプロパティを取得
                                    var textProperty = box.GetType().GetProperty("Text");
                                    var scoreProperty = box.GetType().GetProperty("Score");
                                    var boxPointsProperty = box.GetType().GetProperty("BoxPoints");

                                    string text = textProperty?.GetValue(box)?.ToString() ?? "";
                                    float score = 0.0f;
                                    if (scoreProperty != null)
                                    {
                                        var scoreValue = scoreProperty.GetValue(box);
                                        if (scoreValue is float floatScore)
                                        {
                                            score = floatScore;
                                        }
                                    }

                                    Rectangle bounds = new Rectangle(0, 0, 0, 0);
                                    if (boxPointsProperty != null)
                                    {
                                        var boxPoints = boxPointsProperty.GetValue(box) as Array;
                                        if (boxPoints != null && boxPoints.Length >= 4)
                                        {
                                            // 座標点から矩形を作成
                                            try
                                            {
                                                dynamic point0 = boxPoints.GetValue(0);
                                                dynamic point1 = boxPoints.GetValue(1);
                                                dynamic point3 = boxPoints.GetValue(3);

                                                int x = (int)point0.X;
                                                int y = (int)point0.Y;
                                                int width = (int)(point1.X - point0.X);
                                                int height = (int)(point3.Y - point0.Y);

                                                bounds = new Rectangle(x, y, width, height);
                                            }
                                            catch (Exception ex)
                                            {
                                                Debug.WriteLine($"Error extracting box points: {ex.Message}");
                                            }
                                        }
                                    }

                                    TextRegion region = new TextRegion
                                    {
                                        Text = text,
                                        Bounds = bounds,
                                        Confidence = score
                                    };
                                    textRegions.Add(region);
                                }
                            }
                        }
                    }

                    return textRegions;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error in text region detection: {ex.Message}");
                    return new List<TextRegion>();
                }
            });
        }

        public void EnablePreprocessing(bool enable)
        {
            _enablePreprocessing = enable;
        }

        private Bitmap CaptureScreen(Rectangle region)
        {
            // 画面キャプチャ機能の実装
            Bitmap bitmap = new Bitmap(region.Width, region.Height);

            using (Graphics g = Graphics.FromImage(bitmap))
            {
                g.CopyFromScreen(region.Left, region.Top, 0, 0, region.Size);
            }

            return bitmap;
        }

        private void DumpDirectoryInfo()
        {
            string appDir = AppDomain.CurrentDomain.BaseDirectory;
            Debug.WriteLine($"Application directory: {appDir}");

            // 期待されるモデルディレクトリ
            Debug.WriteLine("Expected model directories:");
            Debug.WriteLine($"  - {Path.Combine(appDir, "PaddleOCRModels")}");
            Debug.WriteLine($"  - {Path.Combine(appDir, "inference")}");

            // アプリケーションディレクトリ内のファイル
            Debug.WriteLine("Files in application directory:");
            try
            {
                foreach (var file in Directory.GetFiles(appDir))
                {
                    Debug.WriteLine($"  - {Path.GetFileName(file)}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error listing files: {ex.Message}");
            }

            // アプリケーションディレクトリ内のディレクトリ
            Debug.WriteLine("Directories in application directory:");
            try
            {
                foreach (var dir in Directory.GetDirectories(appDir))
                {
                    Debug.WriteLine($"  - {Path.GetFileName(dir)}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error listing directories: {ex.Message}");
            }
        }

        public void Dispose()
        {
            _paddleOcr?.Dispose();
        }
    }
}