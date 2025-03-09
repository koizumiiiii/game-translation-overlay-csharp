using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Diagnostics;
using OCRNamespace = GameTranslationOverlay.Core.OCR;

namespace GameTranslationOverlay.Core.Utils
{
    /// <summary>
    /// 画像前処理ユーティリティクラス
    /// </summary>
    public static class ImagePreprocessor
    {
        /// <summary>
        /// 日本語テキスト用のプリセット
        /// </summary>
        public static OCRNamespace.PreprocessingOptions JapaneseTextPreset => new OCRNamespace.PreprocessingOptions
        {
            ContrastLevel = 1.3f,
            BrightnessLevel = 1.1f,
            SharpnessLevel = 0.5f,
            NoiseReduction = 1,
            Threshold = 0,
            ScaleFactor = 1.0f,
            Padding = 2
        };

        /// <summary>
        /// 英語テキスト用のプリセット
        /// </summary>
        public static OCRNamespace.PreprocessingOptions EnglishTextPreset => new OCRNamespace.PreprocessingOptions
        {
            ContrastLevel = 1.2f,
            BrightnessLevel = 1.0f,
            SharpnessLevel = 0.3f,
            NoiseReduction = 0,
            Threshold = 0,
            ScaleFactor = 1.0f,
            Padding = 0
        };

        /// <summary>
        /// ゲームテキスト用の軽量プリセット
        /// </summary>
        public static OCRNamespace.PreprocessingOptions GameTextLightPreset => new OCRNamespace.PreprocessingOptions
        {
            ContrastLevel = 1.1f,
            BrightnessLevel = 1.0f,
            SharpnessLevel = 0.2f,
            NoiseReduction = 0,
            Threshold = 0,
            ScaleFactor = 1.0f,
            Padding = 0
        };

        /// <summary>
        /// OCR名前空間のPreprocessingOptionsを使用して画像を処理
        /// </summary>
        /// <param name="source">元の画像</param>
        /// <param name="options">前処理オプション</param>
        /// <returns>前処理を適用した画像</returns>
        public static Bitmap ProcessImage(Bitmap source, OCRNamespace.PreprocessingOptions options)
        {
            if (source == null)
                return null;

            if (options == null)
                return new Bitmap(source);

            try
            {
                // 元の画像をコピー
                Bitmap processedImage = new Bitmap(source);

                // コントラスト調整
                if (options.ContrastLevel != 1.0f)
                {
                    processedImage = AdjustContrast(processedImage, options.ContrastLevel);
                }

                // 明るさ調整
                if (options.BrightnessLevel != 1.0f)
                {
                    processedImage = AdjustBrightness(processedImage, options.BrightnessLevel);
                }

                // シャープネス調整
                if (options.SharpnessLevel > 0)
                {
                    processedImage = ApplySharpen(processedImage, options.SharpnessLevel);
                }

                // ノイズ軽減
                if (options.NoiseReduction > 0)
                {
                    processedImage = ReduceNoise(processedImage, options.NoiseReduction);
                }

                // 二値化処理
                if (options.Threshold > 0)
                {
                    processedImage = ApplyThreshold(processedImage, options.Threshold);
                }

                // スケーリング
                if (options.ScaleFactor != 1.0f)
                {
                    processedImage = Resize(processedImage, options.ScaleFactor);
                }

                // パディング
                if (options.Padding > 0)
                {
                    processedImage = AddPadding(processedImage, options.Padding);
                }

                return processedImage;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"画像処理エラー: {ex.Message}");
                return new Bitmap(source);
            }
        }

        /// <summary>
        /// Utils名前空間のPreprocessingOptionsを使用して画像を処理
        /// </summary>
        /// <param name="source">元の画像</param>
        /// <param name="options">前処理オプション</param>
        /// <returns>前処理を適用した画像</returns>
        public static Bitmap ProcessImage(Bitmap source, PreprocessingOptions options)
        {
            if (source == null)
                return null;

            if (options == null)
                return new Bitmap(source);

            try
            {
                // OCR名前空間のPreprocessingOptionsに変換
                OCRNamespace.PreprocessingOptions ocrOptions = new OCRNamespace.PreprocessingOptions
                {
                    ContrastLevel = options.ContrastLevel,
                    BrightnessLevel = options.BrightnessLevel,
                    SharpnessLevel = options.SharpnessLevel,
                    NoiseReduction = options.NoiseReduction,
                    Threshold = options.Threshold,
                    ScaleFactor = options.ScaleFactor,
                    Padding = options.Padding
                };

                // 変換したオプションで処理
                return ProcessImage(source, ocrOptions);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"画像処理エラー (Utils名前空間): {ex.Message}");
                return new Bitmap(source);
            }
        }

        // 以下、画像処理用の内部メソッド

        /// <summary>
        /// コントラスト調整
        /// </summary>
        private static Bitmap AdjustContrast(Bitmap image, float contrastLevel)
        {
            if (image == null)
                return null;

            try
            {
                Bitmap adjustedImage = new Bitmap(image.Width, image.Height);
                float factor = (259 * (contrastLevel + 255)) / (255 * (259 - contrastLevel));

                using (Graphics g = Graphics.FromImage(adjustedImage))
                {
                    ColorMatrix cm = new ColorMatrix(new float[][]
                    {
                        new float[] {factor, 0, 0, 0, 0},
                        new float[] {0, factor, 0, 0, 0},
                        new float[] {0, 0, factor, 0, 0},
                        new float[] {0, 0, 0, 1, 0},
                        new float[] {-128 * factor + 128, -128 * factor + 128, -128 * factor + 128, 0, 1}
                    });

                    ImageAttributes attributes = new ImageAttributes();
                    attributes.SetColorMatrix(cm);

                    g.DrawImage(image, new Rectangle(0, 0, image.Width, image.Height),
                                0, 0, image.Width, image.Height,
                                GraphicsUnit.Pixel, attributes);
                }

                return adjustedImage;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"コントラスト調整エラー: {ex.Message}");
                return new Bitmap(image);
            }
        }

        /// <summary>
        /// 明るさ調整
        /// </summary>
        private static Bitmap AdjustBrightness(Bitmap image, float brightnessLevel)
        {
            if (image == null)
                return null;

            try
            {
                Bitmap adjustedImage = new Bitmap(image.Width, image.Height);

                using (Graphics g = Graphics.FromImage(adjustedImage))
                {
                    float brightness = brightnessLevel - 1.0f;

                    ColorMatrix cm = new ColorMatrix(new float[][]
                    {
                        new float[] {1, 0, 0, 0, 0},
                        new float[] {0, 1, 0, 0, 0},
                        new float[] {0, 0, 1, 0, 0},
                        new float[] {0, 0, 0, 1, 0},
                        new float[] {brightness, brightness, brightness, 0, 1}
                    });

                    ImageAttributes attributes = new ImageAttributes();
                    attributes.SetColorMatrix(cm);

                    g.DrawImage(image, new Rectangle(0, 0, image.Width, image.Height),
                                0, 0, image.Width, image.Height,
                                GraphicsUnit.Pixel, attributes);
                }

                return adjustedImage;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"明るさ調整エラー: {ex.Message}");
                return new Bitmap(image);
            }
        }

        /// <summary>
        /// シャープネス調整
        /// </summary>
        private static Bitmap ApplySharpen(Bitmap image, float sharpnessLevel)
        {
            if (image == null)
                return null;

            try
            {
                Bitmap resultImage = new Bitmap(image.Width, image.Height);

                // シャープネスのマトリックスを作成
                float weight = Math.Min(sharpnessLevel, 1.0f);
                float[,] sharpenMatrix = {
                    { -weight, -weight, -weight },
                    { -weight, 9 + weight, -weight },
                    { -weight, -weight, -weight }
                };

                int width = image.Width;
                int height = image.Height;

                // 元画像のピクセルデータを取得
                BitmapData srcData = image.LockBits(new Rectangle(0, 0, width, height),
                    ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);

                // 結果画像のピクセルデータを取得
                BitmapData dstData = resultImage.LockBits(new Rectangle(0, 0, width, height),
                    ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);

                int stride = srcData.Stride;
                IntPtr srcScan0 = srcData.Scan0;
                IntPtr dstScan0 = dstData.Scan0;

                // ピクセルデータをコピー
                int size = stride * height;
                byte[] pixels = new byte[size];
                byte[] resultPixels = new byte[size];
                Marshal.Copy(srcScan0, pixels, 0, size);

                // 端のピクセルはそのままコピー
                Array.Copy(pixels, resultPixels, size);

                // 内部ピクセルにシャープネスフィルタを適用
                for (int y = 1; y < height - 1; y++)
                {
                    for (int x = 1; x < width - 1; x++)
                    {
                        int offset = y * stride + x * 4;

                        double blue = 0;
                        double green = 0;
                        double red = 0;

                        for (int ky = -1; ky <= 1; ky++)
                        {
                            for (int kx = -1; kx <= 1; kx++)
                            {
                                int pos = (y + ky) * stride + (x + kx) * 4;
                                float matValue = sharpenMatrix[ky + 1, kx + 1];

                                blue += pixels[pos] * matValue;
                                green += pixels[pos + 1] * matValue;
                                red += pixels[pos + 2] * matValue;
                            }
                        }

                        // 値が範囲を超えないように調整
                        blue = Math.Max(0, Math.Min(255, blue));
                        green = Math.Max(0, Math.Min(255, green));
                        red = Math.Max(0, Math.Min(255, red));

                        // 安全に値を設定
                        resultPixels[offset] = (byte)ClampToByte((int)blue);
                        resultPixels[offset + 1] = (byte)ClampToByte((int)green);
                        resultPixels[offset + 2] = (byte)ClampToByte((int)red);
                    }
                }

                // 結果をコピー
                Marshal.Copy(resultPixels, 0, dstScan0, size);

                // リソース解放
                image.UnlockBits(srcData);
                resultImage.UnlockBits(dstData);

                return resultImage;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"シャープネス調整エラー: {ex.Message}");
                return new Bitmap(image);
            }
        }

        /// <summary>
        /// ノイズ軽減
        /// </summary>
        private static Bitmap ReduceNoise(Bitmap image, int level)
        {
            if (image == null)
                return null;

            // レベルが0以下の場合、何もしない
            if (level <= 0)
                return new Bitmap(image);

            try
            {
                Bitmap resultImage = new Bitmap(image.Width, image.Height);

                int width = image.Width;
                int height = image.Height;

                // 簡易的なメディアンフィルタを使用
                int kernelSize = level * 2 + 1;
                kernelSize = Math.Min(kernelSize, 5); // 最大サイズを制限

                // 元画像のピクセルデータを取得
                BitmapData srcData = image.LockBits(new Rectangle(0, 0, width, height),
                    ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);

                // 結果画像のピクセルデータを取得
                BitmapData dstData = resultImage.LockBits(new Rectangle(0, 0, width, height),
                    ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);

                int stride = srcData.Stride;
                IntPtr srcScan0 = srcData.Scan0;
                IntPtr dstScan0 = dstData.Scan0;

                // ピクセルデータをコピー
                int size = stride * height;
                byte[] pixels = new byte[size];
                byte[] resultPixels = new byte[size];
                Marshal.Copy(srcScan0, pixels, 0, size);

                // 端のピクセルはそのままコピー
                Array.Copy(pixels, resultPixels, size);

                int radius = kernelSize / 2;

                // メディアンフィルタを適用
                for (int y = radius; y < height - radius; y++)
                {
                    for (int x = radius; x < width - radius; x++)
                    {
                        int offset = y * stride + x * 4;

                        byte[] blueValues = new byte[kernelSize * kernelSize];
                        byte[] greenValues = new byte[kernelSize * kernelSize];
                        byte[] redValues = new byte[kernelSize * kernelSize];

                        int index = 0;
                        for (int ky = -radius; ky <= radius; ky++)
                        {
                            for (int kx = -radius; kx <= radius; kx++)
                            {
                                int pos = (y + ky) * stride + (x + kx) * 4;
                                blueValues[index] = pixels[pos];
                                greenValues[index] = pixels[pos + 1];
                                redValues[index] = pixels[pos + 2];
                                index++;
                            }
                        }

                        // 各色のメディアン値を計算
                        Array.Sort(blueValues);
                        Array.Sort(greenValues);
                        Array.Sort(redValues);

                        int medianIndex = blueValues.Length / 2;

                        // 安全に値を設定
                        resultPixels[offset] = ClampToByte(blueValues[medianIndex]);
                        resultPixels[offset + 1] = ClampToByte(greenValues[medianIndex]);
                        resultPixels[offset + 2] = ClampToByte(redValues[medianIndex]);
                    }
                }

                // 結果をコピー
                Marshal.Copy(resultPixels, 0, dstScan0, size);

                // リソース解放
                image.UnlockBits(srcData);
                resultImage.UnlockBits(dstData);

                return resultImage;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ノイズ削減エラー: {ex.Message}");
                return new Bitmap(image);
            }
        }

        /// <summary>
        /// 二値化処理
        /// </summary>
        private static Bitmap ApplyThreshold(Bitmap image, int threshold)
        {
            if (image == null)
                return null;

            try
            {
                Bitmap resultImage = new Bitmap(image.Width, image.Height);

                int width = image.Width;
                int height = image.Height;

                // 元画像のピクセルデータを取得
                BitmapData srcData = image.LockBits(new Rectangle(0, 0, width, height),
                    ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);

                // 結果画像のピクセルデータを取得
                BitmapData dstData = resultImage.LockBits(new Rectangle(0, 0, width, height),
                    ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);

                int stride = srcData.Stride;
                IntPtr srcScan0 = srcData.Scan0;
                IntPtr dstScan0 = dstData.Scan0;

                // ピクセルデータをコピー
                int size = stride * height;
                byte[] pixels = new byte[size];
                byte[] resultPixels = new byte[size];
                Marshal.Copy(srcScan0, pixels, 0, size);

                // 二値化処理
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        int offset = y * stride + x * 4;

                        byte blue = pixels[offset];
                        byte green = pixels[offset + 1];
                        byte red = pixels[offset + 2];
                        byte alpha = pixels[offset + 3];

                        // グレースケール変換
                        byte gray = (byte)((red * 0.3) + (green * 0.59) + (blue * 0.11));

                        // 二値化
                        byte value = (gray > threshold) ? (byte)255 : (byte)0;

                        resultPixels[offset] = value;     // Blue
                        resultPixels[offset + 1] = value; // Green
                        resultPixels[offset + 2] = value; // Red
                        resultPixels[offset + 3] = alpha; // Alpha
                    }
                }

                // 結果をコピー
                Marshal.Copy(resultPixels, 0, dstScan0, size);

                // リソース解放
                image.UnlockBits(srcData);
                resultImage.UnlockBits(dstData);

                return resultImage;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"二値化エラー: {ex.Message}");
                return new Bitmap(image);
            }
        }

        /// <summary>
        /// 画像のリサイズ
        /// </summary>
        private static Bitmap Resize(Bitmap image, float scale)
        {
            if (image == null)
                return null;

            try
            {
                int newWidth = (int)(image.Width * scale);
                int newHeight = (int)(image.Height * scale);

                Bitmap resizedImage = new Bitmap(newWidth, newHeight);

                using (Graphics g = Graphics.FromImage(resizedImage))
                {
                    g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                    g.DrawImage(image, 0, 0, newWidth, newHeight);
                }

                return resizedImage;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"リサイズエラー: {ex.Message}");
                return new Bitmap(image);
            }
        }

        /// <summary>
        /// 画像にパディングを追加
        /// </summary>
        private static Bitmap AddPadding(Bitmap image, int padding)
        {
            if (image == null)
                return null;

            try
            {
                int newWidth = image.Width + (padding * 2);
                int newHeight = image.Height + (padding * 2);

                Bitmap paddedImage = new Bitmap(newWidth, newHeight);

                using (Graphics g = Graphics.FromImage(paddedImage))
                {
                    g.Clear(Color.White);
                    g.DrawImage(image, padding, padding);
                }

                return paddedImage;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"パディング追加エラー: {ex.Message}");
                return new Bitmap(image);
            }
        }

        /// <summary>
        /// 値を0-255の範囲に収める
        /// </summary>
        private static byte ClampToByte(int value)
        {
            return (byte)Math.Max(0, Math.Min(255, value));
        }
    }
}