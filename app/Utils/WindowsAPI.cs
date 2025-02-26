using System;
using System.Runtime.InteropServices;

namespace GameTranslationOverlay.Utils
{
    public static class WindowsAPI
    {
        // 定数
        public const int GWL_EXSTYLE = -20;
        public const int MOD_ALT = 0x0001;
        public const int MOD_CONTROL = 0x0002;
        public const int MOD_SHIFT = 0x0004;
        public const int MOD_WIN = 0x0008;

        // ウィンドウスタイル関連の定数
        public const int WS_EX_TRANSPARENT = 0x00000020;
        public const int WS_EX_LAYERED = 0x00080000;
        public const int WS_EX_TOPMOST = 0x00000008;

        // メッセージ定数
        public const int WM_HOTKEY = 0x0312;

        // GetWindowLong および SetWindowLong メソッド
        [DllImport("user32.dll")]
        public static extern long GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern long SetWindowLong(IntPtr hWnd, int nIndex, long dwNewLong);

        // ホットキー関連メソッド
        [DllImport("user32.dll")]
        public static extern bool RegisterHotKey(IntPtr hWnd, int id, int fsModifiers, int vk);

        [DllImport("user32.dll")]
        public static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        // キー状態を取得するメソッド
        [DllImport("user32.dll")]
        public static extern short GetAsyncKeyState(int vKey);

        // ウィンドウの移動とサイズ変更
        [DllImport("user32.dll")]
        public static extern bool MoveWindow(IntPtr hWnd, int X, int Y, int nWidth, int nHeight, bool bRepaint);

        // ウィンドウの親子関係
        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr SetParent(IntPtr hWndChild, IntPtr hWndNewParent);

        // ウィンドウの可視性
        [DllImport("user32.dll")]
        public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        // ウィンドウのアクティブ化
        [DllImport("user32.dll")]
        public static extern bool SetForegroundWindow(IntPtr hWnd);

        // ウィンドウの位置を取得
        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        // マウスキャプチャの解放
        [DllImport("user32.dll")]
        public static extern bool ReleaseCapture();

        // ウィンドウメッセージの送信
        [DllImport("user32.dll")]
        public static extern IntPtr SendMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);

        // RECTの構造体
        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;

            public int Width { get { return Right - Left; } }
            public int Height { get { return Bottom - Top; } }
        }

        // ShowWindow用の定数
        public const int SW_HIDE = 0;
        public const int SW_SHOW = 5;

        // SendMessage用のメッセージ定数
        public const int WM_NCLBUTTONDOWN = 0xA1;
        public const int HT_CAPTION = 0x2;

        // WindowsAPI.cs に追加する定数
        public const int GWL_STYLE = -16;  // ウィンドウスタイル
        public const int WS_SIZEBOX = 0x00040000;  // サイズ変更可能なボーダー
        public const int WM_NCHITTEST = 0x0084;  // ヒットテスト

        // マウスキャプチャを取得するメソッド
        [DllImport("user32.dll")]
        public static extern IntPtr GetCapture();
    }
}