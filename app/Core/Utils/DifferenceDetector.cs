﻿// /app/Core/Utils/DifferenceDetector.cs
using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace GameTranslationOverlay.Core.Utils
{
    /// <summary>
    /// 画像間の差分を検出するためのクラス
    /// 画面内容に変化がない場合は、OCR処理をスキップするために使用
    /// </summary>
    public class DifferenceDetector : IDisposable
    {
        // 前回のキャプチャ画像
        private Bitmap _previousImage;

        // 差分検出の閾値（パーセンテージ）
        private double _differenceThreshold;

        // サンプリングサイズ（処理効率のため）
        private readonly int _sampleSize;

        // 処理パフォーマンスの計測
        private readonly Stopwatch _stopwatch = new Stopwatch();

        // リソース解放フラグ
        private bool _disposed = false;

        /// <summary>
        /// コンストラクタ
        /// </summary>
        /// <param name="differenceThreshold">差分検出閾値（0.0〜1.0）</param>
        /// <param name="sampleSize">サンプリングサイズ（大きいほど正確だが遅い）</param>
        public DifferenceDetector(double differenceThreshold = 0.01, int sampleSize = 20)
        {
            _differenceThreshold = Math.Max(0.001, Math.Min(1.0, differenceThreshold));
            _sampleSize = Math.Max(5, Math.Min(50, sampleSize));

            Debug.WriteLine($"DifferenceDetector: 閾値={_differenceThreshold:F3}, サンプリングサイズ={_sampleSize}");
        }

        /// <summary>
        /// 現在の画像と前回の画像との間に有意な差分があるかを検出
        /// </summary>
        /// <param name="currentImage">現在のキャプチャ画像</param>
        /// <returns>有意な差分がある場合はtrue、ない場合はfalse</returns>
        public bool HasSignificantChange(Bitmap currentImage)
        {
            if (currentImage == null)
            {
                return true; // 画像がnullの場合は変更ありとみなす
            }

            // 初回実行時は前回画像がないため、変更ありとみなす
            if (_previousImage == null)
            {
                SaveCurrentImage(currentImage);
                return true;
            }

            // サイズが異なる場合は変更ありとみなす
            if (_previousImage.Width != currentImage.Width ||
                _previousImage.Height != currentImage.Height)
            {
                SaveCurrentImage(currentImage);
                return true;
            }

            try
            {
                _stopwatch.Restart();

                // 画像の差分を計算
                double difference = CalculateFastDifference(currentImage, _previousImage);

                _stopwatch.Stop();

                // 差分が閾値を超えていれば変更ありと判断
                bool hasChanged = difference > _differenceThreshold;

                if (hasChanged)
                {
                    SaveCurrentImage(currentImage);
                    Debug.WriteLine($"画面差分を検出: {difference:F4} (閾値={_differenceThreshold:F3}, 計算時間={_stopwatch.ElapsedMilliseconds}ms)");
                }
                else
                {
                    Debug.WriteLine($"画面に有意な変化なし: {difference:F4} (閾値={_differenceThreshold:F3}, 計算時間={_stopwatch.ElapsedMilliseconds}ms)");
                }

                return hasChanged;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"差分検出エラー: {ex.Message}");
                // エラー時は安全のため変更ありとみなす
                return true;
            }
        }

        /// <summary>
        /// 高速な差分計算（LockBitsを使用）
        /// </summary>
        private double CalculateFastDifference(Bitmap current, Bitmap previous)
        {
            // Rectangle構造体でビットマップ全体を指定
            Rectangle rect = new Rectangle(0, 0, current.Width, current.Height);

            // ピクセルデータへのアクセスをロック
            BitmapData currentData = current.LockBits(rect, ImageLockMode.ReadOnly, current.PixelFormat);
            BitmapData previousData = previous.LockBits(rect, ImageLockMode.ReadOnly, previous.PixelFormat);

            try
            {
                // ビットマップのピクセルフォーマットに基づいてバイト数を計算
                int bytesPerPixel = Image.GetPixelFormatSize(current.PixelFormat) / 8;
                int stride = currentData.Stride;

                // サンプリングのステップサイズを計算
                int stepX = Math.Max(1, current.Width / _sampleSize);
                int stepY = Math.Max(1, current.Height / _sampleSize);

                // 差分計算用の変数
                int differentPixels = 0;
                int totalSamples = 0;

                unsafe
                {
                    // アンセーフコードでポインタを使用して高速アクセス
                    byte* currentPtr = (byte*)currentData.Scan0;
                    byte* previousPtr = (byte*)previousData.Scan0;

                    // サンプリングポイントで差分チェック
                    for (int y = 0; y < current.Height; y += stepY)
                    {
                        for (int x = 0; x < current.Width; x += stepX)
                        {
                            // 現在のピクセル位置を計算
                            int position = y * stride + x * bytesPerPixel;

                            // 各色チャネルの差分を計算
                            int bDiff = Math.Abs(currentPtr[position] - previousPtr[position]);
                            int gDiff = Math.Abs(currentPtr[position + 1] - previousPtr[position + 1]);
                            int rDiff = Math.Abs(currentPtr[position + 2] - previousPtr[position + 2]);

                            // 色差の合計が閾値を超えていれば異なるピクセルとみなす
                            if (bDiff + gDiff + rDiff > 30)
                            {
                                differentPixels++;
                            }

                            totalSamples++;
                        }
                    }
                }

                // 異なるピクセルの割合を計算して返す
                return (double)differentPixels / totalSamples;
            }
            finally
            {
                // アンロックを確実に行う
                current.UnlockBits(currentData);
                previous.UnlockBits(previousData);
            }
        }

        /// <summary>
        /// 現在の画像を保存（前回画像として）
        /// </summary>
        private void SaveCurrentImage(Bitmap current)
        {
            // 前の画像を破棄
            _previousImage?.Dispose();

            // 新しい画像をコピーして保存
            _previousImage = new Bitmap(current);
        }

        /// <summary>
        /// 状態をリセット（前回画像をクリア）
        /// </summary>
        public void Reset()
        {
            _previousImage?.Dispose();
            _previousImage = null;
            Debug.WriteLine("DifferenceDetector: 状態をリセットしました");
        }

        /// <summary>
        /// 閾値を変更
        /// </summary>
        public void SetThreshold(double threshold)
        {
            _differenceThreshold = Math.Max(0.001, Math.Min(1.0, threshold));
            Debug.WriteLine($"DifferenceDetector: 閾値を {_differenceThreshold:F3} に変更しました");
        }

        /// <summary>
        /// リソースの解放
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// リソースの解放（内部実装）
        /// </summary>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    // マネージドリソースの破棄
                    _previousImage?.Dispose();
                    _previousImage = null;
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