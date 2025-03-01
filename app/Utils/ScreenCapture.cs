using System;
using System.Drawing;
using System.Runtime.InteropServices;

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

        private const int SRCCOPY = 0x00CC0020;

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

            Rectangle rect = WindowUtils.GetWindowRect(hWnd);
            if (rect.Width <= 0 || rect.Height <= 0)
                return null;

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

            return bmp;
        }
    }
}