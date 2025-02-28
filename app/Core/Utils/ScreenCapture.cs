using System;
using System.Drawing;
using System.Diagnostics;
using System.Runtime.InteropServices;
using GameTranslationOverlay.Core.WindowManagement;

namespace GameTranslationOverlay.Core.Utils
{
    public static class ScreenCapture
    {
        [DllImport("user32.dll")]
        private static extern bool PrintWindow(IntPtr hwnd, IntPtr hdcBlt, int nFlags);

        public static Bitmap CaptureRegion(Rectangle region)
        {
            try
            {
                Bitmap bitmap = new Bitmap(region.Width, region.Height);
                using (Graphics g = Graphics.FromImage(bitmap))
                {
                    g.CopyFromScreen(region.Left, region.Top, 0, 0, region.Size);
                }
                return bitmap;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error capturing screen region: {ex.Message}");
                return null;
            }
        }

        // ウィンドウのキャプチャ
        public static Bitmap CaptureWindow(IntPtr hwnd)
        {
            // WindowSelector.GetWindowRectが利用可能と仮定
            WindowSelector.RECT rect;
            if (!WindowSelector.GetWindowRect(hwnd, out rect))
            {
                Debug.WriteLine("Failed to get window rect");
                return null;
            }

            int width = rect.Right - rect.Left;
            int height = rect.Bottom - rect.Top;

            if (width <= 0 || height <= 0)
            {
                Debug.WriteLine("Invalid window dimensions");
                return null;
            }

            try
            {
                Bitmap bitmap = new Bitmap(width, height);
                using (Graphics g = Graphics.FromImage(bitmap))
                {
                    IntPtr hdc = g.GetHdc();
                    PrintWindow(hwnd, hdc, 0);
                    g.ReleaseHdc(hdc);
                }
                return bitmap;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error capturing window: {ex.Message}");
                return null;
            }
        }
    }
}