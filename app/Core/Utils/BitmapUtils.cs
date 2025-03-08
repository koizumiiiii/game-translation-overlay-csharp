using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Diagnostics;

namespace GameTranslationOverlay.Core.Utils
{
    /// <summary>
    /// Bitmap操作を最適化するためのユーティリティクラス。
    /// メモリ効率の良い画像処理と、リソース管理を改善します。
    /// </summary>
    public static class BitmapUtils
    {
        /// <summary>
        /// 2つのビットマップ間の差分を高速に計算します
        /// </summary>
        /// <param name="bitmap1">比較する1つ目のビットマップ</param>
        /// <param name="bitmap2">比較する2つ目のビットマップ</param>
        /// <param name="sampleSize">サンプリングサイズ（グリッドの分割数）</param>
        /// <returns>差分の割合（0.0～1.0）</returns>
        public static double CalculateFastDifference(Bitmap bitmap1, Bitmap bitmap2, int sampleSize = 20)
        {
            // サイズチェック
            if (bitmap1 == null || bitmap2 == null)
                return 1.0; // 完全に異なる

            if (bitmap1.Width != bitmap2.Width || bitmap1.Height != bitmap2.Height)
                return 1.0; // サイズが違えば完全に異なる

            try
            {
                // LockBitsを使用した高速なピクセルアクセス
                Rectangle rect = new Rectangle(0, 0, bitmap1.Width, bitmap1.Height);
                BitmapData bmpData1 = bitmap1.LockBits(rect, ImageLockMode.ReadOnly, bitmap1.PixelFormat);
                BitmapData bmpData2 = bitmap2.LockBits(rect, ImageLockMode.ReadOnly, bitmap2.PixelFormat);

                int bytesPerPixel = Image.GetPixelFormatSize(bitmap1.PixelFormat) / 8;
                int byteCount = bmpData1.Stride * bitmap1.Height;
                byte[] pixels1 = new byte[byteCount];
                byte[] pixels2 = new byte[byteCount];

                // ピクセルデータをコピー
                Marshal.Copy(bmpData1.Scan0, pixels1, 0, byteCount);
                Marshal.Copy(bmpData2.Scan0, pixels2, 0, byteCount);

                // サンプリングによる差分計算
                int stepX = Math.Max(1, bitmap1.Width / sampleSize);
                int stepY = Math.Max(1, bitmap1.Height / sampleSize);

                int differentPixels = 0;
                int totalSamples = 0;

                for (int y = 0; y < bitmap1.Height; y += stepY)
                {
                    for (int x = 0; x < bitmap1.Width; x += stepX)
                    {
                        int pos = y * bmpData1.Stride + x * bytesPerPixel;

                        // これ以上読み込めない場合はスキップ
                        if (pos + bytesPerPixel > byteCount)
                            continue;

                        // 各色チャネルの差分を計算
                        int bDiff = Math.Abs(pixels1[pos] - pixels2[pos]);
                        int gDiff = Math.Abs(pixels1[pos + 1] - pixels2[pos + 1]);
                        int rDiff = Math.Abs(pixels1[pos + 2] - pixels2[pos + 2]);

                        int colorDiff = rDiff + gDiff + bDiff;

                        if (colorDiff > 30) // 閾値
                        {
                            differentPixels++;
                        }

                        totalSamples++;
                    }
                }

                // 必ずアンロック
                bitmap1.UnlockBits(bmpData1);
                bitmap2.UnlockBits(bmpData2);

                return (double)differentPixels / Math.Max(1, totalSamples);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"CalculateFastDifferenceエラー: {ex.Message}");

                // エラー発生時は安全のため最大値を返す
                return 1.0;
            }
        }

        /// <summary>
        /// ビットマップをより効率的にリサイズします
        /// </summary>
        /// <param name="source">元のビットマップ</param>
        /// <param name="width">新しい幅</param>
        /// <param name="height">新しい高さ</param>
        /// <param name="interpolationMode">補間モード</param>
        /// <returns>リサイズされたビットマップ</returns>
        public static Bitmap ResizeImage(Bitmap source, int width, int height,
                                         System.Drawing.Drawing2D.InterpolationMode interpolationMode =
                                         System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic)
        {
            if (source == null)
                return null;

            // 新しいビットマップを作成
            Bitmap result = new Bitmap(width, height);

            // ResourceManagerで管理
            ResourceManager.TrackResource(result);

            try
            {
                // 高品質なリサイズ
                using (Graphics g = Graphics.FromImage(result))
                {
                    g.InterpolationMode = interpolationMode;
                    g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                    g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                    g.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;

                    // 元の比率を維持するオプション
                    g.DrawImage(source, 0, 0, width, height);
                }

                return result;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ResizeImageエラー: {ex.Message}");

                // エラー時はリソースを解放
                ResourceManager.ReleaseResource(result);
                return null;
            }
        }

        /// <summary>
        /// OCR処理に最適化したリサイズを行います
        /// </summary>
        /// <param name="source">元のビットマップ</param>
        /// <param name="scale">スケール係数</param>
        /// <returns>OCR用に最適化されたビットマップ</returns>
        public static Bitmap ResizeForOcr(Bitmap source, float scale)
        {
            if (source == null)
                return null;

            // スケールが1.0の場合はコピーを返す
            if (Math.Abs(scale - 1.0f) < 0.001f)
            {
                Bitmap copy = new Bitmap(source);
                ResourceManager.TrackResource(copy);
                return copy;
            }

            int width = (int)(source.Width * scale);
            int height = (int)(source.Height * scale);

            // 有効なサイズかチェック
            if (width <= 0 || height <= 0)
                return null;

            return ResizeImage(source, width, height,
                               System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic);
        }

        /// <summary>
        /// OCR精度を向上させるための画像前処理
        /// </summary>
        /// <param name="source">元の画像</param>
        /// <param name="enhanceContrast">コントラスト強調するか</param>
        /// <param name="removeNoise">ノイズ除去するか</param>
        /// <returns>前処理された画像</returns>
        public static Bitmap PreprocessForOcr(Bitmap source, bool enhanceContrast = true, bool removeNoise = true)
        {
            if (source == null)
                return null;

            try
            {
                // 新しいBitmapを作成（元のサイズ維持）
                Bitmap result = new Bitmap(source.Width, source.Height);
                ResourceManager.TrackResource(result);

                // 画像サイズの検証
                if (source.Width <= 0 || source.Height <= 0 ||
                    source.Width > 4000 || source.Height > 4000) // 極端なサイズは除外
                {
                    return ResizeForOcr(source, 1.0f); // 単純なコピーを返す
                }

                // 画像データの取得
                Rectangle rect = new Rectangle(0, 0, source.Width, source.Height);
                BitmapData srcData = source.LockBits(rect, ImageLockMode.ReadOnly, source.PixelFormat);
                BitmapData destData = result.LockBits(rect, ImageLockMode.WriteOnly, source.PixelFormat);

                int bytesPerPixel = Image.GetPixelFormatSize(source.PixelFormat) / 8;
                int byteCount = srcData.Stride * source.Height;

                byte[] pixels = new byte[byteCount];
                byte[] resultPixels = new byte[byteCount];

                // ソースデータをコピー
                Marshal.Copy(srcData.Scan0, pixels, 0, byteCount);

                // コントラスト強調
                if (enhanceContrast)
                {
                    // ヒストグラム分析のためのデータ収集
                    int[] histogram = new int[256];
                    Array.Clear(histogram, 0, histogram.Length);

                    for (int y = 0; y < source.Height; y++)
                    {
                        for (int x = 0; x < source.Width; x++)
                        {
                            int pos = y * srcData.Stride + x * bytesPerPixel;

                            if (pos + 2 < byteCount)
                            {
                                // 輝度を計算（グレースケール相当）
                                byte b = pixels[pos];
                                byte g = pixels[pos + 1];
                                byte r = pixels[pos + 2];
                                int brightness = (int)(0.299 * r + 0.587 * g + 0.114 * b);

                                // ヒストグラムを更新
                                histogram[brightness]++;
                            }
                        }
                    }

                    // コントラストストレッチングのためのしきい値を計算
                    int minThreshold = 0;
                    int maxThreshold = 255;
                    int pixelCount = source.Width * source.Height;
                    int threshold = (int)(pixelCount * 0.01); // 下位1%

                    // 下限を求める
                    int sum = 0;
                    for (int i = 0; i < 256; i++)
                    {
                        sum += histogram[i];
                        if (sum >= threshold)
                        {
                            minThreshold = i;
                            break;
                        }
                    }

                    // 上限を求める
                    sum = 0;
                    for (int i = 255; i >= 0; i--)
                    {
                        sum += histogram[i];
                        if (sum >= threshold)
                        {
                            maxThreshold = i;
                            break;
                        }
                    }

                    // 値の範囲が狭すぎる場合はデフォルトに戻す
                    if (maxThreshold - minThreshold < 50)
                    {
                        minThreshold = 0;
                        maxThreshold = 255;
                    }

                    // コントラストストレッチングの適用
                    float scale = 255.0f / Math.Max(1, maxThreshold - minThreshold);

                    for (int i = 0; i < byteCount; i += bytesPerPixel)
                    {
                        if (i + 2 < byteCount)
                        {
                            byte b = pixels[i];
                            byte g = pixels[i + 1];
                            byte r = pixels[i + 2];

                            // コントラスト調整
                            int newB = (int)((b - minThreshold) * scale);
                            int newG = (int)((g - minThreshold) * scale);
                            int newR = (int)((r - minThreshold) * scale);

                            // 範囲を0-255に制限
                            resultPixels[i] = (byte)Math.Max(0, Math.Min(255, newB));
                            resultPixels[i + 1] = (byte)Math.Max(0, Math.Min(255, newG));
                            resultPixels[i + 2] = (byte)Math.Max(0, Math.Min(255, newR));

                            // アルファチャンネルがある場合はそのままコピー
                            if (bytesPerPixel > 3 && i + 3 < byteCount)
                            {
                                resultPixels[i + 3] = pixels[i + 3];
                            }
                        }
                    }
                }
                else
                {
                    // 前処理なしの場合はそのままコピー
                    Array.Copy(pixels, resultPixels, byteCount);
                }

                // ノイズ除去（単純な中央値フィルタ）
                if (removeNoise)
                {
                    // 元の画像をバックアップ
                    byte[] tempPixels = new byte[byteCount];
                    Array.Copy(resultPixels, tempPixels, byteCount);

                    // 3x3のカーネルで処理
                    int kernelSize = 1; // 半径

                    for (int y = kernelSize; y < source.Height - kernelSize; y++)
                    {
                        for (int x = kernelSize; x < source.Width - kernelSize; x++)
                        {
                            int pos = y * srcData.Stride + x * bytesPerPixel;

                            if (pos + 2 < byteCount)
                            {
                                // 各色チャンネルごとに中央値を計算
                                for (int c = 0; c < Math.Min(3, bytesPerPixel); c++)
                                {
                                    byte[] values = new byte[9]; // 3x3カーネル
                                    int idx = 0;

                                    // 周囲のピクセルを収集
                                    for (int ky = -kernelSize; ky <= kernelSize; ky++)
                                    {
                                        for (int kx = -kernelSize; kx <= kernelSize; kx++)
                                        {
                                            int offset = pos + ky * srcData.Stride + kx * bytesPerPixel + c;
                                            if (offset >= 0 && offset < byteCount)
                                            {
                                                values[idx++] = tempPixels[offset];
                                            }
                                        }
                                    }

                                    // 中央値を計算（単純化のため配列をソート）
                                    Array.Sort(values, 0, idx);
                                    resultPixels[pos + c] = values[idx / 2];
                                }

                                // アルファチャンネルは維持
                                if (bytesPerPixel > 3 && pos + 3 < byteCount)
                                {
                                    resultPixels[pos + 3] = tempPixels[pos + 3];
                                }
                            }
                        }
                    }
                }

                // 結果を書き込み
                Marshal.Copy(resultPixels, 0, destData.Scan0, byteCount);

                // リソースの解放
                source.UnlockBits(srcData);
                result.UnlockBits(destData);

                return result;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"PreprocessForOcrエラー: {ex.Message}");
                return ResizeForOcr(source, 1.0f); // エラー時は単純なコピーを返す
            }
        }

        /// <summary>
        /// ビットマップに安全なパディングを追加します
        /// （境界エラー防止用）
        /// </summary>
        /// <param name="source">元の画像</param>
        /// <param name="padding">パディングサイズ（ピクセル）</param>
        /// <returns>パディングが追加された画像</returns>
        public static Bitmap AddSafePadding(Bitmap source, int padding = 10)
        {
            if (source == null)
                return null;

            try
            {
                int newWidth = source.Width + padding * 2;
                int newHeight = source.Height + padding * 2;

                Bitmap result = new Bitmap(newWidth, newHeight);
                ResourceManager.TrackResource(result);

                using (Graphics g = Graphics.FromImage(result))
                {
                    g.Clear(Color.White); // パディングは白色
                    g.DrawImage(source, padding, padding, source.Width, source.Height);
                }

                return result;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"AddSafePaddingエラー: {ex.Message}");
                return ResizeForOcr(source, 1.0f); // エラー時は単純なコピーを返す
            }
        }

        /// <summary>
        /// 安全にビットマップをクローンします。
        /// リソース追跡も行います。
        /// </summary>
        /// <param name="source">元の画像</param>
        /// <returns>クローンされた画像</returns>
        public static Bitmap SafeClone(Bitmap source)
        {
            if (source == null)
                return null;

            try
            {
                Bitmap clone = new Bitmap(source);
                ResourceManager.TrackResource(clone);
                return clone;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"SafeCloneエラー: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 安全にビットマップを破棄します
        /// </summary>
        /// <param name="bitmap">破棄するビットマップ</param>
        public static void SafeDispose(ref Bitmap bitmap)
        {
            if (bitmap != null)
            {
                try
                {
                    ResourceManager.ReleaseResource(bitmap);
                    bitmap.Dispose();
                    bitmap = null;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"SafeDisposeエラー: {ex.Message}");
                }
            }
        }
    }
}