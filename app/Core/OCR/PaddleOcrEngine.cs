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
    public class PaddleOcrEngine : IOcrEngine, IDisposable
    {
        private PaddleOCREngine _paddleOcr;
        private OCRModelConfig _modelConfig;
        private bool _enablePreprocessing = false;
        private PreprocessingOptions _preprocessingOptions;
        private bool _isDisposed = false;

        // 最大画像サイズの制限
        private const int MAX_IMAGE_DIMENSION = 1920;

        // プロパティの追加
        public void SetPreprocessingOptions(PreprocessingOptions options)
        {
            _preprocessingOptions = options;
        }

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

            if (_isDisposed)
            {
                throw new ObjectDisposedException(nameof(PaddleOcrEngine));
            }

            return await Task.Run(() =>
            {
                // 処理結果を格納する変数
                List<TextRegion> textRegions = new List<TextRegion>();
                // 処理中の画像を格納する変数
                Bitmap processedImage = null;

                try
                {
                    // メモリ使用量をログに記録（デバッグ用）
                    Debug.WriteLine($"メモリ使用量: {GC.GetTotalMemory(false) / (1024 * 1024)}MB");

                    // 画像サイズをチェックし、必要に応じてリサイズ
                    if (image.Width > MAX_IMAGE_DIMENSION || image.Height > MAX_IMAGE_DIMENSION)
                    {
                        Debug.WriteLine($"大きな画像サイズを検出: {image.Width}x{image.Height} - リサイズを適用します");

                        // リサイズ比率の計算（最大次元に基づく）
                        float scale = (float)MAX_IMAGE_DIMENSION / Math.Max(image.Width, image.Height);
                        int newWidth = (int)(image.Width * scale);
                        int newHeight = (int)(image.Height * scale);

                        Debug.WriteLine($"リサイズ後のサイズ: {newWidth}x{newHeight}");

                        // 新しいビットマップの作成
                        processedImage = new Bitmap(newWidth, newHeight);

                        using (Graphics g = Graphics.FromImage(processedImage))
                        {
                            // 高品質リサイズ設定
                            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                            g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                            g.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;

                            // 画像の描画
                            g.DrawImage(image, 0, 0, newWidth, newHeight);
                        }
                    }
                    else
                    {
                        // サイズが十分小さい場合は元の画像を使用（コピーして安全に）
                        processedImage = new Bitmap(image);
                    }

                    // 前処理の適用
                    if (_enablePreprocessing && _preprocessingOptions != null)
                    {
                        Bitmap preprocessedImage = ApplyPreprocessing(processedImage, _preprocessingOptions);
                        // 古い画像を解放
                        processedImage.Dispose();
                        // 前処理後の画像を使用
                        processedImage = preprocessedImage;
                    }

                    // OCR検出処理
                    var result = _paddleOcr.DetectText(processedImage);

                    // TextRegionへの変換処理
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
                                    // 既存のリフレクションコード...
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
                finally
                {
                    // リソースの解放
                    processedImage?.Dispose();
                }
            });
        }

        // 前処理を適用するメソッド
        private Bitmap ApplyPreprocessing(Bitmap image, PreprocessingOptions options)
        {
            // 入力検証
            if (image == null || options == null)
                return image;

            // 前処理後の画像
            Bitmap result = new Bitmap(image);

            try
            {
                // コントラスト調整
                if (options.ContrastLevel != 1.0f)
                {
                    result = AdjustContrast(result, options.ContrastLevel);
                }

                // 明るさ調整
                if (options.BrightnessLevel != 1.0f)
                {
                    result = AdjustBrightness(result, options.BrightnessLevel);
                }

                // シャープネス
                if (options.SharpnessLevel > 0.0f)
                {
                    result = ApplySharpen(result, options.SharpnessLevel);
                }

                // ノイズ除去
                if (options.NoiseReduction > 0)
                {
                    result = ApplyNoiseReduction(result, options.NoiseReduction);
                }

                // サイズ調整
                if (options.ScaleFactor != 1.0f)
                {
                    result = ResizeImage(result, options.ScaleFactor);
                }

                // パディング
                if (options.Padding > 0)
                {
                    // 大きな画像の場合はパディングを制限
                    int padding = options.Padding;
                    if (result.Width > MAX_IMAGE_DIMENSION - 100 || result.Height > MAX_IMAGE_DIMENSION - 100)
                    {
                        padding = Math.Min(padding, 2); // 最大2pxまで制限
                        Debug.WriteLine($"大きな画像のためパディングを制限: {options.Padding}→{padding}");
                    }
                    result = AddPadding(result, padding);
                }

                return result;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"画像前処理でエラー: {ex.Message}");
                // エラーが発生した場合は元の画像を返す
                return image;
            }
        }

        // 前処理メソッドの実装（基本的な例）
        private Bitmap AdjustContrast(Bitmap image, float level)
        {
            // 実装例（簡略化）
            // 実際の実装ではより高度な画像処理アルゴリズムを使用
            return image;
        }

        private Bitmap AdjustBrightness(Bitmap image, float level)
        {
            // 同様に実装
            return image;
        }

        private Bitmap ApplySharpen(Bitmap image, float strength)
        {
            // 同様に実装
            return image;
        }

        private Bitmap ApplyNoiseReduction(Bitmap image, int level)
        {
            // 同様に実装
            return image;
        }

        private Bitmap ResizeImage(Bitmap image, float scale)
        {
            // すでにリサイズロジックがあるが、前処理用に再利用
            int newWidth = (int)(image.Width * scale);
            int newHeight = (int)(image.Height * scale);

            Bitmap resized = new Bitmap(newWidth, newHeight);
            using (Graphics g = Graphics.FromImage(resized))
            {
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                g.DrawImage(image, 0, 0, newWidth, newHeight);
            }

            return resized;
        }

        private Bitmap AddPadding(Bitmap image, int padding)
        {
            if (padding <= 0)
                return image;

            int newWidth = image.Width + (padding * 2);
            int newHeight = image.Height + (padding * 2);

            Bitmap paddedImage = new Bitmap(newWidth, newHeight);
            using (Graphics g = Graphics.FromImage(paddedImage))
            {
                g.Clear(Color.White); // 白い背景で埋める
                g.DrawImage(image, padding, padding, image.Width, image.Height);
            }

            return paddedImage;
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
            if (!_isDisposed)
            {
                _paddleOcr?.Dispose();
                _isDisposed = true;
            }
        }
    }
}