using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;
using GameTranslationOverlay.Core.Models;
using GameTranslationOverlay.Utils;

namespace GameTranslationOverlay.Core.UI
{
    /// <summary>
    /// ウィンドウ選択ユーティリティ
    /// </summary>
    public static class WindowSelector
    {
        // Windows APIインポート
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

        [DllImport("user32.dll")]
        private static extern IntPtr GetShellWindow();

        // ウィンドウ列挙のコールバック
        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        /// <summary>
        /// すべての実行中ウィンドウを取得
        /// </summary>
        /// <returns>ウィンドウ情報のリスト</returns>
        public static List<WindowInfo> GetAllWindows()
        {
            List<WindowInfo> windows = new List<WindowInfo>();
            IntPtr shellWindow = GetShellWindow();

            EnumWindows((hWnd, lParam) => {
                // 可視ウィンドウのみ対象
                if (!IsWindowVisible(hWnd) || hWnd == shellWindow)
                    return true;

                // ウィンドウタイトル取得
                StringBuilder title = new StringBuilder(256);
                GetWindowText(hWnd, title, title.Capacity);

                if (string.IsNullOrWhiteSpace(title.ToString()))
                    return true;

                // プロセス情報取得
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
                catch
                {
                    return true; // プロセス情報が取得できない場合はスキップ
                }

                // ウィンドウの位置とサイズを取得
                Rectangle bounds = WindowUtils.GetWindowRect(hWnd);

                // ウィンドウ情報を追加
                WindowInfo info = new WindowInfo
                {
                    Handle = hWnd,
                    Title = title.ToString(),
                    ProcessName = processName,
                    Bounds = bounds
                };

                // サムネイルを取得
                try
                {
                    // サイズの小さいサムネイルを生成
                    info.Thumbnail = GenerateThumbnail(hWnd, 160, 120);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"サムネイル取得エラー: {ex.Message}");
                }

                if (info.Thumbnail != null)
                {
                    windows.Add(info);
                }

                return true;
            }, IntPtr.Zero);

            return windows;
        }

        /// <summary>
        /// ウィンドウのサムネイルを生成
        /// </summary>
        /// <param name="hWnd">ウィンドウハンドル</param>
        /// <param name="width">サムネイル幅</param>
        /// <param name="height">サムネイル高さ</param>
        /// <returns>サムネイル画像</returns>
        private static Bitmap GenerateThumbnail(IntPtr hWnd, int width, int height)
        {
            // ウィンドウをキャプチャ
            using (Bitmap windowCapture = ScreenCapture.CaptureWindow(hWnd))
            {
                if (windowCapture == null)
                    return null;

                // サムネイルを生成
                Bitmap thumbnail = new Bitmap(width, height);
                using (Graphics g = Graphics.FromImage(thumbnail))
                {
                    g.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
                    g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                    g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                    g.DrawImage(windowCapture, 0, 0, width, height);
                }

                return thumbnail;
            }
        }
    }
}