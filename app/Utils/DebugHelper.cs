using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using GameTranslationOverlay.Utils;

namespace GameTranslationOverlay.Core.Utils
{
    /// <summary>
    /// デバッグ支援用のユーティリティクラス
    /// </summary>
    public static class DebugHelper
    {
        /// <summary>
        /// ウィンドウスタイルのデバッグ出力
        /// </summary>
        public static void LogWindowStyles(IntPtr handle)
        {
            long style = WindowsAPI.GetWindowLong(handle, WindowsAPI.GWL_STYLE);
            Debug.WriteLine($"Window style: 0x{style.ToString("X8")}");

            // 特定のスタイルフラグをチェック
            Debug.WriteLine($"Has WS_SIZEBOX: {(style & WindowsAPI.WS_SIZEBOX) != 0}");
        }

        /// <summary>
        /// マウスキャプチャ状態のデバッグ出力
        /// </summary>
        public static void LogMouseCapture(IntPtr ownerHandle)
        {
            IntPtr captureHandle = WindowsAPI.GetCapture();
            Debug.WriteLine($"Current capture: {captureHandle}, Owner handle: {ownerHandle}");
        }

        /// <summary>
        /// マウスイベントのデバッグ出力
        /// </summary>
        public static void LogMouseEvent(string eventName, MouseEventArgs e, Point location)
        {
            Debug.WriteLine($"{eventName}: Button={e.Button}, Location={location}, Time={DateTime.Now.ToString("HH:mm:ss.fff")}");
        }

        /// <summary>
        /// ホットキー登録のデバッグ出力
        /// </summary>
        public static void LogHotkeyRegistration(int id, bool success)
        {
            Debug.WriteLine($"Hotkey registration (ID={id}): {(success ? "Success" : "Failed")}");
        }

        /// <summary>
        /// ウィンドウメッセージのデバッグ出力
        /// </summary>
        public static void LogWindowMessage(int msg, IntPtr wParam, IntPtr lParam)
        {
            // 特定のメッセージのみデバッグ出力
            switch (msg)
            {
                case WindowsAPI.WM_HOTKEY:
                    Debug.WriteLine($"WM_HOTKEY: ID={wParam.ToInt32()}");
                    break;

                case WindowsAPI.WM_NCHITTEST:
                    // NICHITTESTメッセージは頻繁に発生するので、詳細デバッグ時のみ有効化
                    // Point screenPoint = new Point(lParam.ToInt32() & 0xFFFF, lParam.ToInt32() >> 16);
                    // Debug.WriteLine($"WM_NCHITTEST: Screen={screenPoint}");
                    break;
            }
        }
    }
}