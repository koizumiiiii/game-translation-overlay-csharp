using System;
using System.Drawing;
using System.Runtime.InteropServices;

namespace GameTranslationOverlay.Utils
{
    /// <summary>
    /// ウィンドウ操作ユーティリティ
    /// </summary>
    public static class WindowUtils
    {
        // Windows APIインポート
        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern int GetWindowText(IntPtr hWnd, System.Text.StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        /// <summary>
        /// ウィンドウの矩形情報を取得
        /// </summary>
        /// <param name="hWnd">ウィンドウハンドル</param>
        /// <returns>ウィンドウの矩形</returns>
        public static Rectangle GetWindowRect(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero)
                return Rectangle.Empty;

            RECT rect;
            if (GetWindowRect(hWnd, out rect))
            {
                return new Rectangle(
                    rect.Left,
                    rect.Top,
                    rect.Right - rect.Left,
                    rect.Bottom - rect.Top);
            }

            return Rectangle.Empty;
        }

        /// <summary>
        /// ウィンドウが有効かどうか確認
        /// </summary>
        /// <param name="hWnd">ウィンドウハンドル</param>
        /// <returns>有効な場合はtrue</returns>
        public static bool IsWindowValid(IntPtr hWnd)
        {
            return hWnd != IntPtr.Zero && IsWindow(hWnd) && IsWindowVisible(hWnd);
        }

        /// <summary>
        /// ウィンドウのタイトルを取得
        /// </summary>
        /// <param name="hWnd">ウィンドウハンドル</param>
        /// <returns>ウィンドウタイトル</returns>
        public static string GetWindowTitle(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero)
                return string.Empty;

            const int nChars = 256;
            System.Text.StringBuilder buff = new System.Text.StringBuilder(nChars);

            if (GetWindowText(hWnd, buff, nChars) > 0)
            {
                return buff.ToString();
            }

            return string.Empty;
        }

        /// <summary>
        /// 最前面のウィンドウハンドルを取得
        /// </summary>
        /// <returns>最前面ウィンドウのハンドル</returns>
        public static IntPtr GetForegroundWindowHandle()
        {
            return GetForegroundWindow();
        }
    }
}