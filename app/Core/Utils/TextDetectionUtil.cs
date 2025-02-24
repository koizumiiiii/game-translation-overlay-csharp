using System;
using System.Drawing;
using System.Drawing.Imaging;

namespace GameTranslationOverlay.Core.Utils
{
    public static class TextDetectionUtil
    {
        private const int IMAGE_DIFF_THRESHOLD = 30;  // 画像差分の閾値
        private const double MIN_CHANGE_RATIO = 0.01; // 最小変化率（1%）
        private const double LEVENSHTEIN_THRESHOLD = 0.8; // テキスト類似度の閾値（80%）

        /// <summary>
        /// 画像とテキストの両方の変化を検出
        /// </summary>
        public static bool HasSignificantChange(Bitmap previous, Bitmap current, string previousText, string currentText)
        {
            // 画像の差分チェック（軽量な前処理）
            if (!HasImageChange(previous, current))
                return false;

            // テキストの類似度チェック（より詳細な判定）
            return !AreTextsSimilar(previousText, currentText);
        }

        /// <summary>
        /// 画像の差分を検出
        /// </summary>
        private static bool HasImageChange(Bitmap img1, Bitmap img2)
        {
            if (img1 == null || img2 == null) return true;
            if (img1.Width != img2.Width || img1.Height != img2.Height) return true;

            try
            {
                var rect = new Rectangle(0, 0, img1.Width, img1.Height);
                var bd1 = img1.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
                var bd2 = img2.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);

                int totalPixels = img1.Width * img1.Height;
                int changedPixels = 0;
                unsafe
                {
                    byte* ptr1 = (byte*)bd1.Scan0;
                    byte* ptr2 = (byte*)bd2.Scan0;

                    for (int i = 0; i < totalPixels * 4; i += 4)
                    {
                        // RGBの差分を計算
                        int diff = Math.Abs(ptr1[i] - ptr2[i]) +
                                 Math.Abs(ptr1[i + 1] - ptr2[i + 1]) +
                                 Math.Abs(ptr1[i + 2] - ptr2[i + 2]);

                        if (diff > IMAGE_DIFF_THRESHOLD)
                        {
                            changedPixels++;
                            if ((double)changedPixels / totalPixels > MIN_CHANGE_RATIO)
                            {
                                img1.UnlockBits(bd1);
                                img2.UnlockBits(bd2);
                                return true;
                            }
                        }
                    }
                }

                img1.UnlockBits(bd1);
                img2.UnlockBits(bd2);
                return false;
            }
            catch (Exception)
            {
                return true; // エラーの場合は変更ありとして扱う
            }
        }

        /// <summary>
        /// テキストの類似度を計算（レーベンシュタイン距離を使用）
        /// </summary>
        private static bool AreTextsSimilar(string text1, string text2)
        {
            if (string.IsNullOrEmpty(text1) || string.IsNullOrEmpty(text2)) return false;
            if (text1 == text2) return true;

            // 前処理：空白を正規化
            text1 = NormalizeText(text1);
            text2 = NormalizeText(text2);

            int distance = ComputeLevenshteinDistance(text1, text2);
            int maxLength = Math.Max(text1.Length, text2.Length);
            double similarity = 1 - ((double)distance / maxLength);

            return similarity >= LEVENSHTEIN_THRESHOLD;
        }

        private static string NormalizeText(string text)
        {
            return text.Trim().Replace("  ", " ");
        }

        private static int ComputeLevenshteinDistance(string s, string t)
        {
            int n = s.Length;
            int m = t.Length;
            int[,] d = new int[n + 1, m + 1];

            if (n == 0) return m;
            if (m == 0) return n;

            for (int i = 0; i <= n; i++) d[i, 0] = i;
            for (int j = 0; j <= m; j++) d[0, j] = j;

            for (int i = 1; i <= n; i++)
            {
                for (int j = 1; j <= m; j++)
                {
                    int cost = (t[j - 1] == s[i - 1]) ? 0 : 1;
                    d[i, j] = Math.Min(Math.Min(
                        d[i - 1, j] + 1,      // 削除
                        d[i, j - 1] + 1),     // 挿入
                        d[i - 1, j - 1] + cost); // 置換
                }
            }

            return d[n, m];
        }
    }
}