using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Diagnostics;
using System.Runtime.InteropServices;
using GameTranslationOverlay.Core.Utils;

namespace GameTranslationOverlay.Core.OCR
{
    /// <summary>
    /// 連続するスクリーンキャプチャ間の差分を検出するクラス。
    /// 画面に大きな変化がない場合、OCR処理をスキップすることでパフォーマンスを向上させます。
    /// </summary>
    public class DifferenceDetector : IDisposable
    {
        // 前回のキャプチャ画像
        private Bitmap _previousImage;

        // 差分検出のための設定
        private readonly double _differenceThreshold;
        private readonly int _sampleSize;
        private readonly bool _useHighQualityDetection;

        // 統計情報
        private int _totalComparisons = 0;
        private int _significantChanges = 0;
        private long _totalProcessingTime = 0;

        // リソース管理
        private bool _disposed = false;
        private readonly Stopwatch _stopwatch = new Stopwatch();

        // 最大画像次元サイズ（メモリオーバーフロー防止）
        private const int MAX_IMAGE_DIMENSION = 3840; // 4K解像度を上限に

        /// <summary>
        /// コンストラクタ
        /// </summary>
        /// <param name="differenceThreshold">差分検出の閾値（0.0～1.0、デフォルトは0.01、つまり1%の変化で検出）</param>
        /// <param name="sampleSize">サンプリングサイズ（デフォルトは20x20グリッド）</param>
        /// <param name="useHighQualityDetection">高品質検出モード（精度優先、デフォルトはfalse）</param>
        public DifferenceDetector(double differenceThreshold = 0.01, int sampleSize = 20, bool useHighQualityDetection = false)
        {
            _differenceThreshold = differenceThreshold;
            _sampleSize = sampleSize;
            _useHighQualityDetection = useHighQualityDetection;

            Debug.WriteLine($"DifferenceDetector: 初期化 (閾値={_differenceThreshold:F3}, サンプルサイズ={_sampleSize}, 高品質モード={_useHighQualityDetection})");
        }

        /// <summary>
        /// 前回の画像と現在の画像で大きな差分があるかをチェック
        /// </summary>
        /// <param name="currentImage">現在のキャプチャ画像</param>
        /// <returns>大きな変化がある場合はtrue、そうでなければfalse</returns>
        public bool HasSignificantChange(Bitmap currentImage)
        {
            _totalComparisons++;
            _stopwatch.Restart();

            if (currentImage == null)
            {
                Debug.WriteLine("DifferenceDetector: 現在の画像がnullです");
                return true;
            }

            // 初回の比較またはサイズ変更時は変化ありとみなす
            if (_previousImage == null ||
                _previousImage.Width != currentImage.Width ||
                _previousImage.Height != currentImage.Height)
            {
                SaveCurrentImage(currentImage);
                _stopwatch.Stop();
                _totalProcessingTime += _stopwatch.ElapsedMilliseconds;

                Debug.WriteLine("DifferenceDetector: 初回比較または画像サイズが変更されました");
                return true;
            }

            // 画像サイズチェック（メモリオーバーフロー防止）
            if (IsTooLargeImage(currentImage))
            {
                Debug.WriteLine($"DifferenceDetector: 画像が大きすぎます ({currentImage.Width}x{currentImage.Height})");
                return true; // 安全のため変化ありとみなす
            }

            // 画像の差分を計算（オリジナル画像でなくコピーを使用）
            double difference;
            using (Bitmap currentCopy = SafeCloneImage(currentImage))
            {
                if (_useHighQualityDetection)
                {
                    // 高精度検出モード（より詳細な比較、CPU負荷が高い）
                    difference = CalculateDetailedDifference(currentCopy, _previousImage);
                }
                else
                {
                    // 標準検出モード（効率優先）
                    difference = CalculateFastDifference(currentCopy, _previousImage);
                }
            }

            bool hasSignificantChange = difference > _differenceThreshold;

            // 大きな差分がある場合は現在の画像を保存
            if (hasSignificantChange)
            {
                SaveCurrentImage(currentImage);
                _significantChanges++;
                Debug.WriteLine($"DifferenceDetector: 変化を検出しました ({difference:F3} > {_differenceThreshold:F3})");
            }
            else
            {
                Debug.WriteLine($"DifferenceDetector: 変化なし ({difference:F3} <= {_differenceThreshold:F3})");
            }

            _stopwatch.Stop();
            _totalProcessingTime += _stopwatch.ElapsedMilliseconds;

            return hasSignificantChange;
        }

        /// <summary>
        /// 安全に画像をクローンする
        /// </summary>
        private Bitmap SafeCloneImage(Bitmap source)
        {
            if (source == null) return null;

            try
            {
                // 新しいビットマップを作成してコピー
                Bitmap clone = new Bitmap(source.Width, source.Height, source.PixelFormat);

                using (Graphics g = Graphics.FromImage(clone))
                {
                    g.DrawImage(source, 0, 0, source.Width, source.Height);
                }

                return clone;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"画像クローン作成エラー: {ex.Message}");
                // 失敗した場合は元の参照を返す（理想的ではないが、エラーを防ぐ）
                return new Bitmap(source);
            }
        }

        /// <summary>
        /// 画像が処理に適したサイズか確認
        /// </summary>
        private bool IsTooLargeImage(Bitmap image)
        {
            return image.Width > MAX_IMAGE_DIMENSION || image.Height > MAX_IMAGE_DIMENSION;
        }

        /// <summary>
        /// 高速な差分計算（サンプリングポイントによる比較）
        /// </summary>
        private double CalculateFastDifference(Bitmap current, Bitmap previous)
        {
            // パフォーマンスのため、サンプリングポイントで差分を計算
            int differentPixels = 0;
            int totalSamples = 0;

            // より安全なサンプリングステップの計算（ゼロ除算防止）
            int stepX = Math.Max(1, current.Width / Math.Max(1, _sampleSize));
            int stepY = Math.Max(1, current.Height / Math.Max(1, _sampleSize));

            BitmapData currentData = null;
            BitmapData previousData = null;

            try
            {
                // LockBitsを使用した高速なピクセルアクセス
                Rectangle rect = new Rectangle(0, 0, current.Width, current.Height);
                currentData = current.LockBits(rect, ImageLockMode.ReadOnly, current.PixelFormat);
                previousData = previous.LockBits(rect, ImageLockMode.ReadOnly, previous.PixelFormat);

                int bytesPerPixel = Image.GetPixelFormatSize(current.PixelFormat) / 8;
                int byteCount = currentData.Stride * current.Height;

                // バッファサイズの検証（安全対策）
                if (byteCount <= 0 || byteCount > 100 * 1024 * 1024) // 100MB上限
                {
                    Debug.WriteLine($"バッファサイズ異常: {byteCount} bytes");
                    return 1.0; // 変化ありとみなす
                }

                byte[] currentPixels = new byte[byteCount];
                byte[] previousPixels = new byte[byteCount];

                // ピクセルデータをコピー
                Marshal.Copy(currentData.Scan0, currentPixels, 0, byteCount);
                Marshal.Copy(previousData.Scan0, previousPixels, 0, byteCount);

                // サンプリングによる差分計算
                for (int y = 0; y < current.Height; y += stepY)
                {
                    for (int x = 0; x < current.Width; x += stepX)
                    {
                        int pos = y * currentData.Stride + x * bytesPerPixel;

                        // 配列の境界チェック
                        if (pos + 2 < byteCount)
                        {
                            // 各色チャネルの差分を計算
                            int bDiff = Math.Abs(currentPixels[pos] - previousPixels[pos]);
                            int gDiff = Math.Abs(currentPixels[pos + 1] - previousPixels[pos + 1]);
                            int rDiff = Math.Abs(currentPixels[pos + 2] - previousPixels[pos + 2]);

                            int colorDiff = rDiff + gDiff + bDiff;

                            if (colorDiff > 30) // 閾値
                            {
                                differentPixels++;
                            }

                            totalSamples++;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"差分計算エラー: {ex.Message}");
                return 1.0; // エラー時は変化ありとみなす
            }
            finally
            {
                // 確実にビットマップデータをアンロック
                if (currentData != null)
                {
                    try { current.UnlockBits(currentData); }
                    catch (Exception ex) { Debug.WriteLine($"UnlockBits エラー: {ex.Message}"); }
                }

                if (previousData != null)
                {
                    try { previous.UnlockBits(previousData); }
                    catch (Exception ex) { Debug.WriteLine($"UnlockBits エラー: {ex.Message}"); }
                }
            }

            return totalSamples == 0 ? 1.0 : (double)differentPixels / totalSamples;
        }

        /// <summary>
        /// より詳細な差分計算（全ピクセルを確認する高品質モード）
        /// </summary>
        private double CalculateDetailedDifference(Bitmap current, Bitmap previous)
        {
            int width = current.Width;
            int height = current.Height;
            int differentPixels = 0;
            int totalPixels = 0;

            // 全ピクセルではなく、グリッド状にサンプリング（パフォーマンスのため）
            int stepX = Math.Max(1, width / Math.Max(1, _sampleSize * 2)); // 高品質モードはより密なサンプリング
            int stepY = Math.Max(1, height / Math.Max(1, _sampleSize * 2));

            BitmapData currentData = null;
            BitmapData previousData = null;

            try
            {
                // LockBitsを使用した高速なピクセルアクセス
                Rectangle rect = new Rectangle(0, 0, width, height);
                currentData = current.LockBits(rect, ImageLockMode.ReadOnly, current.PixelFormat);
                previousData = previous.LockBits(rect, ImageLockMode.ReadOnly, previous.PixelFormat);

                int bytesPerPixel = Image.GetPixelFormatSize(current.PixelFormat) / 8;
                int byteCount = currentData.Stride * height;

                // バッファサイズの検証（安全対策）
                if (byteCount <= 0 || byteCount > 100 * 1024 * 1024) // 100MB上限
                {
                    Debug.WriteLine($"バッファサイズ異常: {byteCount} bytes");
                    return 1.0; // 変化ありとみなす
                }

                byte[] currentPixels = new byte[byteCount];
                byte[] previousPixels = new byte[byteCount];

                // ピクセルデータをコピー
                Marshal.Copy(currentData.Scan0, currentPixels, 0, byteCount);
                Marshal.Copy(previousData.Scan0, previousPixels, 0, byteCount);

                // 詳細な差分計算
                for (int y = 0; y < height; y += stepY)
                {
                    for (int x = 0; x < width; x += stepX)
                    {
                        int pos = y * currentData.Stride + x * bytesPerPixel;

                        // 配列の境界チェック
                        if (pos + 2 < byteCount)
                        {
                            // 各色チャネルの差分を計算（二乗誤差を使用して小さな差を強調）
                            int bDiff = currentPixels[pos] - previousPixels[pos];
                            int gDiff = currentPixels[pos + 1] - previousPixels[pos + 1];
                            int rDiff = currentPixels[pos + 2] - previousPixels[pos + 2];

                            // 二乗誤差の合計を計算
                            double squaredError = (bDiff * bDiff + gDiff * gDiff + rDiff * rDiff) / 3.0;

                            if (squaredError > 100) // 閾値
                            {
                                differentPixels++;
                            }

                            totalPixels++;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"詳細差分計算エラー: {ex.Message}");
                return 1.0; // エラー時は変化ありとみなす
            }
            finally
            {
                // 確実にビットマップデータをアンロック
                if (currentData != null)
                {
                    try { current.UnlockBits(currentData); }
                    catch (Exception ex) { Debug.WriteLine($"UnlockBits エラー: {ex.Message}"); }
                }

                if (previousData != null)
                {
                    try { previous.UnlockBits(previousData); }
                    catch (Exception ex) { Debug.WriteLine($"UnlockBits エラー: {ex.Message}"); }
                }
            }

            return totalPixels == 0 ? 1.0 : (double)differentPixels / totalPixels;
        }

        /// <summary>
        /// 現在の画像を保存し、前回の画像を破棄
        /// </summary>
        private void SaveCurrentImage(Bitmap current)
        {
            // 前の画像を破棄
            DisposePreviousImage();

            try
            {
                // 新しい画像をコピーして保存（元の画像に依存しない）
                _previousImage = SafeCloneImage(current);

                // ResourceManagerに追跡させる（メモリ管理の最適化）
                ResourceManager.TrackResource(_previousImage);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"画像保存エラー: {ex.Message}");
                _previousImage = null;
            }
        }

        /// <summary>
        /// 前回の画像を破棄
        /// </summary>
        private void DisposePreviousImage()
        {
            if (_previousImage != null)
            {
                try
                {
                    _previousImage.Dispose();
                    _previousImage = null;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"画像破棄エラー: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// 全てのリソースをリセット
        /// </summary>
        public void Reset()
        {
            DisposePreviousImage();
            _totalComparisons = 0;
            _significantChanges = 0;
            _totalProcessingTime = 0;
            Debug.WriteLine("DifferenceDetector: リセットしました");
        }

        /// <summary>
        /// 統計情報を取得
        /// </summary>
        public string GetStatistics()
        {
            double avgProcessingTime = _totalComparisons == 0 ? 0 : (double)_totalProcessingTime / _totalComparisons;
            double changeRate = _totalComparisons == 0 ? 0 : (double)_significantChanges / _totalComparisons;

            return string.Format(
                "DifferenceDetector 統計:\n" +
                "総比較回数: {0}\n" +
                "変化検出回数: {1}\n" +
                "変化検出率: {2:P2}\n" +
                "平均処理時間: {3:F2}ms\n" +
                "閾値: {4:F3}",
                _totalComparisons,
                _significantChanges,
                changeRate,
                avgProcessingTime,
                _differenceThreshold
            );
        }

        /// <summary>
        /// リソースの破棄
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// リソースの破棄（内部実装）
        /// </summary>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    // マネージドリソースの破棄
                    DisposePreviousImage();
                }

                _disposed = true;
            }
        }

        /// <summary>
        /// デストラクタ
        /// </summary>
        ~DifferenceDetector()
        {
            Dispose(false);
        }
    }
}