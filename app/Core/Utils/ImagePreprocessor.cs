using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Diagnostics;

namespace GameTranslationOverlay.Core.Utils
{
    /// <summary>
    /// OCR精度向上のための画像前処理ユーティリティクラス
    /// </summary>
    public static class ImagePreprocessor
    {
        /// <summary>
        /// 画像の前処理を行い、OCR精度を向上させる
        /// </summary>
        /// <param name="source">元画像</param>
        /// <param name="options">前処理オプション</param>
        /// <returns>処理された画像</returns>
        public static Bitmap Preprocess(Bitmap source, PreprocessingOptions options = null)
        {
            if (source == null)
                return null;

            options = options ?? new PreprocessingOptions();

            try
            {
                // 元画像のコピーを作成
                Bitmap processedImage = new Bitmap(source.Width, source.Height, PixelFormat.Format24bppRgb);

                try
                {
                    // 画像処理のためのグラフィックスオブジェクトを作成
                    using (Graphics g = Graphics.FromImage(processedImage))
                    {
                        g.SmoothingMode = SmoothingMode.AntiAlias;
                        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                        g.PixelOffsetMode = PixelOffsetMode.HighQuality;

                        // 基本的な描画
                        g.DrawImage(source, new Rectangle(0, 0, source.Width, source.Height));
                    }

                    // オプションに応じて各処理を適用
                    if (options.ApplyContrast)
                    {
                        processedImage = AdjustContrast(processedImage, options.ContrastLevel);
                    }

                    if (options.ApplyBrightness)
                    {
                        processedImage = AdjustBrightness(processedImage, options.BrightnessLevel);
                    }

                    if (options.ApplySharpening)
                    {
                        processedImage = Sharpen(processedImage, options.SharpeningLevel);
                    }

                    if (options.RemoveNoise)
                    {
                        processedImage = RemoveNoise(processedImage, options.NoiseReductionLevel);
                    }

                    if (options.ApplyThreshold)
                    {
                        processedImage = ApplyThreshold(processedImage, options.ThresholdLevel);
                    }

                    if (options.Scale != 1.0f)
                    {
                        processedImage = Resize(processedImage, options.Scale);
                    }

                    if (options.PaddingPixels > 0)
                    {
                        processedImage = AddPadding(processedImage, options.PaddingPixels);
                    }

                    return processedImage;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error in image preprocessing: {ex.Message}");
                    processedImage?.Dispose();
                    return new Bitmap(source); // エラー時は元画像のコピーを返す
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Critical error in image preprocessing: {ex.Message}");
                return new Bitmap(source); // エラー時は元画像のコピーを返す
            }
        }

        /// <summary>
        /// コントラストを調整する
        /// </summary>
        private static Bitmap AdjustContrast(Bitmap image, float level)
        {
            try
            {
                // コントラスト値の調整（-100〜100の範囲）
                float contrast = (100.0f + level) / 100.0f;
                contrast *= contrast; // 二乗して効果を強調

                float[][] colorMatrixElements = {
                    new float[] {contrast, 0, 0, 0, 0},
                    new float[] {0, contrast, 0, 0, 0},
                    new float[] {0, 0, contrast, 0, 0},
                    new float[] {0, 0, 0, 1, 0},
                    new float[] {0.5f * (1 - contrast), 0.5f * (1 - contrast), 0.5f * (1 - contrast), 0, 1}
                };

                ColorMatrix colorMatrix = new ColorMatrix(colorMatrixElements);
                ImageAttributes imageAttributes = new ImageAttributes();
                imageAttributes.SetColorMatrix(colorMatrix);

                Bitmap result = new Bitmap(image.Width, image.Height);
                using (Graphics g = Graphics.FromImage(result))
                {
                    g.DrawImage(image,
                        new Rectangle(0, 0, image.Width, image.Height),
                        0, 0, image.Width, image.Height,
                        GraphicsUnit.Pixel, imageAttributes);
                }

                return result;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error adjusting contrast: {ex.Message}");
                return image;
            }
        }

        /// <summary>
        /// 明るさを調整する
        /// </summary>
        private static Bitmap AdjustBrightness(Bitmap image, float level)
        {
            try
            {
                // 明るさレベルを調整（-100〜100の範囲）
                float brightness = level / 100.0f;

                float[][] colorMatrixElements = {
                    new float[] {1, 0, 0, 0, 0},
                    new float[] {0, 1, 0, 0, 0},
                    new float[] {0, 0, 1, 0, 0},
                    new float[] {0, 0, 0, 1, 0},
                    new float[] {brightness, brightness, brightness, 0, 1}
                };

                ColorMatrix colorMatrix = new ColorMatrix(colorMatrixElements);
                ImageAttributes imageAttributes = new ImageAttributes();
                imageAttributes.SetColorMatrix(colorMatrix);

                Bitmap result = new Bitmap(image.Width, image.Height);
                using (Graphics g = Graphics.FromImage(result))
                {
                    g.DrawImage(image,
                        new Rectangle(0, 0, image.Width, image.Height),
                        0, 0, image.Width, image.Height,
                        GraphicsUnit.Pixel, imageAttributes);
                }

                return result;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error adjusting brightness: {ex.Message}");
                return image;
            }
        }

        /// <summary>
        /// 画像をシャープにする
        /// </summary>
        private static Bitmap Sharpen(Bitmap image, float strength)
        {
            try
            {
                // 5x5シャープニング行列
                float[,] sharpenMatrix = {
                    { -1, -1, -1, -1, -1 },
                    { -1,  2,  2,  2, -1 },
                    { -1,  2,  16, 2, -1 },
                    { -1,  2,  2,  2, -1 },
                    { -1, -1, -1, -1, -1 }
                };

                // シャープニング強度の適用
                float matrixSum = 16.0f;
                for (int x = 0; x < 5; x++)
                {
                    for (int y = 0; y < 5; y++)
                    {
                        sharpenMatrix[x, y] *= strength;
                    }
                }

                // カーネルの合計が1になるように調整
                float scale = 1.0f / (matrixSum * strength);

                Bitmap result = new Bitmap(image.Width, image.Height);
                BitmapData srcData = image.LockBits(
                    new Rectangle(0, 0, image.Width, image.Height),
                    ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
                BitmapData destData = result.LockBits(
                    new Rectangle(0, 0, result.Width, result.Height),
                    ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);

                int stride = srcData.Stride;
                IntPtr srcScan0 = srcData.Scan0;
                IntPtr destScan0 = destData.Scan0;

                unsafe
                {
                    byte* src = (byte*)srcScan0;
                    byte* dest = (byte*)destScan0;

                    // 境界を除いて各ピクセルを処理
                    for (int y = 2; y < image.Height - 2; y++)
                    {
                        for (int x = 2; x < image.Width - 2; x++)
                        {
                            int offset = y * stride + x * 4;
                            double blue = 0, green = 0, red = 0;

                            // カーネルを適用
                            for (int ky = -2; ky <= 2; ky++)
                            {
                                for (int kx = -2; kx <= 2; kx++)
                                {
                                    int pos = offset + ky * stride + kx * 4;
                                    blue += src[pos] * sharpenMatrix[ky + 2, kx + 2];
                                    green += src[pos + 1] * sharpenMatrix[ky + 2, kx + 2];
                                    red += src[pos + 2] * sharpenMatrix[ky + 2, kx + 2];
                                }
                            }

                            // 値をスケールして範囲内に収める
                            byte b = Math.Min(Math.Max((int)(blue * scale), 0), 255);
                            byte g = Math.Min(Math.Max((int)(green * scale), 0), 255);
                            byte r = Math.Min(Math.Max((int)(red * scale), 0), 255);

                            dest[offset] = b;
                            dest[offset + 1] = g;
                            dest[offset + 2] = r;
                            dest[offset + 3] = src[offset + 3]; // アルファ値はそのまま
                        }
                    }
                }

                image.UnlockBits(srcData);
                result.UnlockBits(destData);

                return result;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error sharpening image: {ex.Message}");
                return image;
            }
        }

        /// <summary>
        /// ノイズを除去する
        /// </summary>
        private static Bitmap RemoveNoise(Bitmap image, int level)
        {
            try
            {
                // メディアンフィルタによるノイズ除去
                int filterSize = level; // フィルタサイズ（3、5、7など。奇数であるべき）
                if (filterSize % 2 == 0)
                    filterSize++; // 偶数の場合は奇数に調整

                if (filterSize < 3)
                    filterSize = 3; // 最小値は3

                Bitmap result = new Bitmap(image.Width, image.Height);
                BitmapData srcData = image.LockBits(
                    new Rectangle(0, 0, image.Width, image.Height),
                    ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
                BitmapData destData = result.LockBits(
                    new Rectangle(0, 0, result.Width, result.Height),
                    ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);

                int stride = srcData.Stride;
                IntPtr srcScan0 = srcData.Scan0;
                IntPtr destScan0 = destData.Scan0;

                int halfFilter = filterSize / 2;

                unsafe
                {
                    byte* src = (byte*)srcScan0;
                    byte* dest = (byte*)destScan0;

                    // 境界を除いて各ピクセルを処理
                    for (int y = halfFilter; y < image.Height - halfFilter; y++)
                    {
                        for (int x = halfFilter; x < image.Width - halfFilter; x++)
                        {
                            int offset = y * stride + x * 4;

                            byte[] blueValues = new byte[filterSize * filterSize];
                            byte[] greenValues = new byte[filterSize * filterSize];
                            byte[] redValues = new byte[filterSize * filterSize];

                            // 周辺ピクセルの値を収集
                            int index = 0;
                            for (int ky = -halfFilter; ky <= halfFilter; ky++)
                            {
                                for (int kx = -halfFilter; kx <= halfFilter; kx++)
                                {
                                    int pos = offset + ky * stride + kx * 4;
                                    blueValues[index] = src[pos];
                                    greenValues[index] = src[pos + 1];
                                    redValues[index] = src[pos + 2];
                                    index++;
                                }
                            }

                            // メディアン値を取得（簡易ソート）
                            Array.Sort(blueValues);
                            Array.Sort(greenValues);
                            Array.Sort(redValues);

                            // メディアン値を設定
                            int medianIndex = blueValues.Length / 2;
                            dest[offset] = blueValues[medianIndex];
                            dest[offset + 1] = greenValues[medianIndex];
                            dest[offset + 2] = redValues[medianIndex];
                            dest[offset + 3] = src[offset + 3]; // アルファ値はそのまま
                        }
                    }
                }

                image.UnlockBits(srcData);
                result.UnlockBits(destData);

                return result;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error removing noise: {ex.Message}");
                return image;
            }
        }

        /// <summary>
        /// 閾値処理（二値化）を適用する
        /// </summary>
        private static Bitmap ApplyThreshold(Bitmap image, int threshold)
        {
            try
            {
                Bitmap result = new Bitmap(image.Width, image.Height);

                for (int y = 0; y < image.Height; y++)
                {
                    for (int x = 0; x < image.Width; x++)
                    {
                        Color pixel = image.GetPixel(x, y);

                        // グレースケール値を計算（輝度）
                        int grayScale = (int)(0.299 * pixel.R + 0.587 * pixel.G + 0.114 * pixel.B);

                        // 閾値と比較してピクセル値を設定
                        Color newColor = grayScale > threshold ? Color.White : Color.Black;
                        result.SetPixel(x, y, newColor);
                    }
                }

                return result;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error applying threshold: {ex.Message}");
                return image;
            }
        }

        /// <summary>
        /// 画像のサイズを変更する
        /// </summary>
        private static Bitmap Resize(Bitmap image, float scale)
        {
            try
            {
                int newWidth = (int)(image.Width * scale);
                int newHeight = (int)(image.Height * scale);

                if (newWidth <= 0 || newHeight <= 0)
                {
                    Debug.WriteLine("Invalid scaling factor results in zero or negative dimensions");
                    return image;
                }

                Bitmap result = new Bitmap(newWidth, newHeight);

                using (Graphics g = Graphics.FromImage(result))
                {
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.DrawImage(image, new Rectangle(0, 0, newWidth, newHeight));
                }

                return result;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error resizing image: {ex.Message}");
                return image;
            }
        }

        /// <summary>
        /// 画像に余白（パディング）を追加する
        /// </summary>
        private static Bitmap AddPadding(Bitmap image, int padding)
        {
            try
            {
                int newWidth = image.Width + (padding * 2);
                int newHeight = image.Height + (padding * 2);

                Bitmap result = new Bitmap(newWidth, newHeight);

                using (Graphics g = Graphics.FromImage(result))
                {
                    g.Clear(Color.White); // 背景を白に
                    g.DrawImage(image, padding, padding, image.Width, image.Height);
                }

                return result;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error adding padding: {ex.Message}");
                return image;
            }
        }

        /// <summary>
        /// 日本語テキスト向けの最適化プリセット
        /// </summary>
        public static PreprocessingOptions JapaneseTextPreset => new PreprocessingOptions
        {
            ApplyContrast = true,
            ContrastLevel = 30,
            ApplyBrightness = true,
            BrightnessLevel = 10,
            ApplySharpening = true,
            SharpeningLevel = 1.5f,
            RemoveNoise = true,
            NoiseReductionLevel = 3,
            ApplyThreshold = false,
            Scale = 1.5f,
            PaddingPixels = 5
        };

        /// <summary>
        /// 英語テキスト向けの最適化プリセット
        /// </summary>
        public static PreprocessingOptions EnglishTextPreset => new PreprocessingOptions
        {
            ApplyContrast = true,
            ContrastLevel = 25,
            ApplyBrightness = true,
            BrightnessLevel = 5,
            ApplySharpening = true,
            SharpeningLevel = 1.2f,
            RemoveNoise = true,
            NoiseReductionLevel = 3,
            ApplyThreshold = false,
            Scale = 1.2f,
            PaddingPixels = 4
        };

        /// <summary>
        /// ゲーム向け簡易プリセット（最小の処理でパフォーマンスを優先）
        /// </summary>
        public static PreprocessingOptions GameTextLightPreset => new PreprocessingOptions
        {
            ApplyContrast = true,
            ContrastLevel = 20,
            ApplyBrightness = false,
            ApplySharpening = false,
            RemoveNoise = false,
            ApplyThreshold = false,
            Scale = 1.0f,
            PaddingPixels = 0
        };
    }

    /// <summary>
    /// 画像前処理オプション
    /// </summary>
    public class PreprocessingOptions
    {
        /// <summary>
        /// コントラスト調整を適用するかどうか
        /// </summary>
        public bool ApplyContrast { get; set; } = false;

        /// <summary>
        /// コントラストレベル（-100〜100）
        /// </summary>
        public float ContrastLevel { get; set; } = 0;

        /// <summary>
        /// 明るさ調整を適用するかどうか
        /// </summary>
        public bool ApplyBrightness { get; set; } = false;

        /// <summary>
        /// 明るさレベル（-100〜100）
        /// </summary>
        public float BrightnessLevel { get; set; } = 0;

        /// <summary>
        /// シャープニングを適用するかどうか
        /// </summary>
        public bool ApplySharpening { get; set; } = false;

        /// <summary>
        /// シャープニング強度（0.0〜3.0）
        /// </summary>
        public float SharpeningLevel { get; set; } = 1.0f;

        /// <summary>
        /// ノイズ除去を適用するかどうか
        /// </summary>
        public bool RemoveNoise { get; set; } = false;

        /// <summary>
        /// ノイズ除去レベル（フィルタサイズ、3、5、7など）
        /// </summary>
        public int NoiseReductionLevel { get; set; } = 3;

        /// <summary>
        /// 閾値処理（二値化）を適用するかどうか
        /// </summary>
        public bool ApplyThreshold { get; set; } = false;

        /// <summary>
        /// 閾値レベル（0〜255）
        /// </summary>
        public int ThresholdLevel { get; set; } = 128;

        /// <summary>
        /// リサイズ倍率（1.0は元のサイズ）
        /// </summary>
        public float Scale { get; set; } = 1.0f;

        /// <summary>
        /// 追加するパディング（余白）のピクセル数
        /// </summary>
        public int PaddingPixels { get; set; } = 0;
    }
}