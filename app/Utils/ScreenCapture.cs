using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Diagnostics;

namespace GameTranslationOverlay.Utils
{
    /// <summary>
    /// 画面キャプチャユーティリティ
    /// </summary>
    public static class ScreenCapture
    {
        // Windows APIインポート
        [DllImport("user32.dll")]
        private static extern IntPtr GetWindowDC(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr ReleaseDC(IntPtr hWnd, IntPtr hDC);

        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateCompatibleDC(IntPtr hdc);

        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateCompatibleBitmap(IntPtr hdc, int nWidth, int nHeight);

        [DllImport("gdi32.dll")]
        private static extern IntPtr SelectObject(IntPtr hdc, IntPtr hgdiobj);

        [DllImport("gdi32.dll")]
        private static extern bool BitBlt(IntPtr hdcDest, int nXDest, int nYDest, int nWidth, int nHeight, IntPtr hdcSrc, int nXSrc, int nYSrc, int dwRop);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteDC(IntPtr hdc);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr hObject);

        // 追加のAPI
        [DllImport("user32.dll")]
        private static extern bool PrintWindow(IntPtr hWnd, IntPtr hdcBlt, int nFlags);

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, ref RECT lpRect);

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        private const int SRCCOPY = 0x00CC0020;
        private const int PW_CLIENTONLY = 0x1;
        private const int PW_RENDERFULLCONTENT = 0x2; // Windows 8.1以降

        /// <summary>
        /// 画面全体をキャプチャ
        /// </summary>
        /// <returns>キャプチャした画像</returns>
        public static Bitmap CaptureScreen()
        {
            return CaptureScreenRect(new Rectangle(0, 0,
                System.Windows.Forms.Screen.PrimaryScreen.Bounds.Width,
                System.Windows.Forms.Screen.PrimaryScreen.Bounds.Height));
        }

        /// <summary>
        /// 画面の特定領域をキャプチャ
        /// </summary>
        /// <param name="rect">キャプチャする領域</param>
        /// <returns>キャプチャした画像</returns>
        public static Bitmap CaptureScreenRect(Rectangle rect)
        {
            Bitmap bitmap = new Bitmap(rect.Width, rect.Height);
            using (Graphics g = Graphics.FromImage(bitmap))
            {
                g.CopyFromScreen(rect.Left, rect.Top, 0, 0, rect.Size);
            }
            return bitmap;
        }

        /// <summary>
        /// ウィンドウをキャプチャ
        /// </summary>
        /// <param name="hWnd">ウィンドウハンドル</param>
        /// <returns>キャプチャした画像</returns>
        public static Bitmap CaptureWindow(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero)
                return null;

            // PrintWindowを試す（より高い互換性）
            try
            {
                Debug.WriteLine("PrintWindowでキャプチャを試行");
                var bitmap = CaptureWindowWithPrintWindow(hWnd);
                if (bitmap != null)
                {
                    SaveDebugImage(bitmap, "print_window");
                    return bitmap;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"PrintWindowエラー: {ex.Message}");
            }

            // 従来のBitBlt方式にフォールバック
            Debug.WriteLine("BitBlt方式にフォールバック");

            Rectangle rect = GetWindowRectangle(hWnd);
            if (rect.Width <= 0 || rect.Height <= 0)
            {
                Debug.WriteLine($"無効なウィンドウサイズ: {rect.Width}x{rect.Height}");
                return null;
            }

            IntPtr hdcWindow = GetWindowDC(hWnd);
            IntPtr hdcMemDC = CreateCompatibleDC(hdcWindow);
            IntPtr hBitmap = CreateCompatibleBitmap(hdcWindow, rect.Width, rect.Height);
            IntPtr hOld = SelectObject(hdcMemDC, hBitmap);

            BitBlt(hdcMemDC, 0, 0, rect.Width, rect.Height, hdcWindow, 0, 0, SRCCOPY);
            SelectObject(hdcMemDC, hOld);

            Bitmap bmp = Image.FromHbitmap(hBitmap);

            DeleteObject(hBitmap);
            DeleteDC(hdcMemDC);
            ReleaseDC(hWnd, hdcWindow);

            SaveDebugImage(bmp, "bitblt");
            return bmp;
        }

        private static Rectangle GetWindowRectangle(IntPtr hWnd)
        {
            RECT rect = new RECT();
            GetWindowRect(hWnd, ref rect);
            return new Rectangle(
                rect.Left,
                rect.Top,
                rect.Right - rect.Left,
                rect.Bottom - rect.Top);
        }

        private static Bitmap CaptureWindowWithPrintWindow(IntPtr hWnd)
        {
            Rectangle rect = GetWindowRectangle(hWnd);
            if (rect.Width <= 0 || rect.Height <= 0)
            {
                Debug.WriteLine($"無効なウィンドウサイズ: {rect.Width}x{rect.Height}");
                return null;
            }

            Debug.WriteLine($"ウィンドウサイズ: {rect.Width}x{rect.Height}");

            IntPtr hdcScreen = GetWindowDC(IntPtr.Zero);
            IntPtr hdcMemDC = CreateCompatibleDC(hdcScreen);
            IntPtr hBitmap = CreateCompatibleBitmap(hdcScreen, rect.Width, rect.Height);
            IntPtr hOld = SelectObject(hdcMemDC, hBitmap);

            // まずPW_RENDERFULLCONTENT（Windows 8.1以降）を試す
            bool success = false;
            try
            {
                success = PrintWindow(hWnd, hdcMemDC, PW_RENDERFULLCONTENT);
                Debug.WriteLine($"PrintWindow (PW_RENDERFULLCONTENT): {success}");
            }
            catch
            {
                // 古いWindowsバージョンではPW_RENDERFULLCONTENTが対応していない可能性がある
                Debug.WriteLine("PW_RENDERFULLCONTENTは対応していません");
            }

            // 失敗した場合はPW_CLIENTONLYを試す
            if (!success)
            {
                success = PrintWindow(hWnd, hdcMemDC, PW_CLIENTONLY);
                Debug.WriteLine($"PrintWindow (PW_CLIENTONLY): {success}");
            }

            // それでも失敗した場合は標準モードを試す
            if (!success)
            {
                success = PrintWindow(hWnd, hdcMemDC, 0);
                Debug.WriteLine($"PrintWindow (標準): {success}");
            }

            SelectObject(hdcMemDC, hOld);

            Bitmap bmp = null;
            if (success)
            {
                bmp = Image.FromHbitmap(hBitmap);
            }

            DeleteObject(hBitmap);
            DeleteDC(hdcMemDC);
            ReleaseDC(IntPtr.Zero, hdcScreen);

            return bmp;
        }

        private static void SaveDebugImage(Bitmap bitmap, string prefix)
        {
            if (bitmap == null) return;

            try
            {
                string appDataFolder = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                string debugFolder = System.IO.Path.Combine(appDataFolder, "GameTranslationOverlay", "Debug");

                if (!System.IO.Directory.Exists(debugFolder))
                {
                    System.IO.Directory.CreateDirectory(debugFolder);
                }

                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string filename = System.IO.Path.Combine(debugFolder, $"{prefix}_{timestamp}.png");

                bitmap.Save(filename, ImageFormat.Png);
                Debug.WriteLine($"デバッグ画像を保存しました: {filename}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"デバッグ画像の保存に失敗: {ex.Message}");
            }
        }
    }
}