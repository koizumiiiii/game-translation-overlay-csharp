using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;

namespace GameTranslationOverlay.Core.WindowManagement
{
    public class WindowSelector
    {
        // Windows API関連
        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc enumProc, IntPtr lParam);

        [DllImport("user32.dll")]
        public static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll")]
        private static extern int GetWindowTextLength(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

        [DllImport("user32.dll")]
        private static extern bool PrintWindow(IntPtr hwnd, IntPtr hdcBlt, int nFlags);

        [DllImport("user32.dll")]
        private static extern IntPtr GetWindowDC(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateCompatibleDC(IntPtr hdc);

        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateCompatibleBitmap(IntPtr hdc, int nWidth, int nHeight);

        [DllImport("gdi32.dll")]
        private static extern IntPtr SelectObject(IntPtr hdc, IntPtr hgdiobj);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr hObject);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteDC(IntPtr hdc);

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        // ウィンドウ情報クラス
        public class WindowInfo
        {
            public IntPtr Handle { get; set; }
            public string Title { get; set; }
            public string ProcessName { get; set; }
            public Rectangle Bounds { get; set; }
            public Bitmap Thumbnail { get; set; }

            public override string ToString()
            {
                return $"{Title} ({ProcessName})";
            }
        }

        // 現在のアクティブウィンドウの矩形を取得
        public static Rectangle GetActiveWindowRect()
        {
            RECT rect;
            IntPtr handle = GetForegroundWindow();
            if (GetWindowRect(handle, out rect))
            {
                return new Rectangle(
                    rect.Left, rect.Top,
                    rect.Right - rect.Left, rect.Bottom - rect.Top);
            }
            return Rectangle.Empty;
        }

        // 画面上の全ウィンドウを列挙
        public static List<WindowInfo> GetWindows()
        {
            List<WindowInfo> windowList = new List<WindowInfo>();
            EnumWindows(new EnumWindowsProc((hWnd, lParam) =>
            {
                if (!IsWindowVisible(hWnd))
                    return true;

                int length = GetWindowTextLength(hWnd);
                if (length == 0)
                    return true;

                StringBuilder builder = new StringBuilder(length + 1);
                GetWindowText(hWnd, builder, builder.Capacity);
                string title = builder.ToString();

                if (string.IsNullOrWhiteSpace(title))
                    return true;

                RECT rect;
                GetWindowRect(hWnd, out rect);

                uint processId;
                GetWindowThreadProcessId(hWnd, out processId);
                string processName = "";

                try
                {
                    using (Process process = Process.GetProcessById((int)processId))
                    {
                        processName = process.ProcessName;
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error getting process name: {ex.Message}");
                }

                // ウィンドウのサムネイル生成
                Bitmap thumbnail = CaptureWindowThumbnail(hWnd);

                windowList.Add(new WindowInfo
                {
                    Handle = hWnd,
                    Title = title,
                    ProcessName = processName,
                    Bounds = new Rectangle(rect.Left, rect.Top, rect.Right - rect.Left, rect.Bottom - rect.Top),
                    Thumbnail = thumbnail
                });

                return true;
            }), IntPtr.Zero);

            return windowList;
        }

        // ウィンドウのサムネイルを取得
        private static Bitmap CaptureWindowThumbnail(IntPtr hWnd)
        {
            RECT rect;
            if (!GetWindowRect(hWnd, out rect))
                return null;

            int width = rect.Right - rect.Left;
            int height = rect.Bottom - rect.Top;

            if (width <= 0 || height <= 0)
                return null;

            // 大きすぎるウィンドウの場合はサイズを制限
            const int MAX_THUMBNAIL_SIZE = 200;
            int thumbnailWidth = width;
            int thumbnailHeight = height;
            float aspectRatio = (float)width / height;

            if (width > height && width > MAX_THUMBNAIL_SIZE)
            {
                thumbnailWidth = MAX_THUMBNAIL_SIZE;
                thumbnailHeight = (int)(MAX_THUMBNAIL_SIZE / aspectRatio);
            }
            else if (height > MAX_THUMBNAIL_SIZE)
            {
                thumbnailHeight = MAX_THUMBNAIL_SIZE;
                thumbnailWidth = (int)(MAX_THUMBNAIL_SIZE * aspectRatio);
            }

            try
            {
                Bitmap bitmap = new Bitmap(width, height);
                using (Graphics graphics = Graphics.FromImage(bitmap))
                {
                    IntPtr hdc = graphics.GetHdc();
                    PrintWindow(hWnd, hdc, 0);
                    graphics.ReleaseHdc(hdc);
                }

                // サムネイルサイズにリサイズ
                Bitmap thumbnail = new Bitmap(thumbnailWidth, thumbnailHeight);
                using (Graphics graphics = Graphics.FromImage(thumbnail))
                {
                    graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                    graphics.DrawImage(bitmap, 0, 0, thumbnailWidth, thumbnailHeight);
                }

                bitmap.Dispose();
                return thumbnail;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error capturing window thumbnail: {ex.Message}");
                return null;
            }
        }

        // 低レベルAPI版のウィンドウキャプチャ（代替実装）
        private static Bitmap CaptureWindowLowLevel(IntPtr hWnd)
        {
            RECT rect;
            if (!GetWindowRect(hWnd, out rect))
                return null;

            int width = rect.Right - rect.Left;
            int height = rect.Bottom - rect.Top;

            if (width <= 0 || height <= 0)
                return null;

            try
            {
                IntPtr hdcWindow = GetWindowDC(hWnd);
                IntPtr hdcMemDC = CreateCompatibleDC(hdcWindow);
                IntPtr hBitmap = CreateCompatibleBitmap(hdcWindow, width, height);
                IntPtr oldBitmap = SelectObject(hdcMemDC, hBitmap);

                // ウィンドウの内容をコピー
                PrintWindow(hWnd, hdcMemDC, 0);

                // ビットマップをマネージドオブジェクトに変換
                Bitmap bitmap = Image.FromHbitmap(hBitmap);

                // リソースを解放
                SelectObject(hdcMemDC, oldBitmap);
                DeleteObject(hBitmap);
                DeleteDC(hdcMemDC);
                ReleaseDC(hWnd, hdcWindow);

                return bitmap;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error capturing window (low level): {ex.Message}");
                return null;
            }
        }

        // ウィンドウ領域のキャプチャ
        public static Bitmap CaptureWindow(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero)
                return null;

            RECT rect;
            if (!GetWindowRect(hWnd, out rect))
                return null;

            int width = rect.Right - rect.Left;
            int height = rect.Bottom - rect.Top;

            if (width <= 0 || height <= 0)
                return null;

            try
            {
                Bitmap bitmap = new Bitmap(width, height);
                using (Graphics graphics = Graphics.FromImage(bitmap))
                {
                    IntPtr hdc = graphics.GetHdc();
                    PrintWindow(hWnd, hdc, 0);
                    graphics.ReleaseHdc(hdc);
                }
                return bitmap;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error capturing window: {ex.Message}");
                return null;
            }
        }

        // 指定された領域をキャプチャ
        public static Bitmap CaptureRegion(Rectangle region)
        {
            if (region.Width <= 0 || region.Height <= 0)
                return null;

            try
            {
                Bitmap bitmap = new Bitmap(region.Width, region.Height);
                using (Graphics graphics = Graphics.FromImage(bitmap))
                {
                    graphics.CopyFromScreen(region.Left, region.Top, 0, 0, region.Size);
                }
                return bitmap;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error capturing region: {ex.Message}");
                return null;
            }
        }
    }
}