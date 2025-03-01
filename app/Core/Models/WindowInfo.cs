using System;
using System.Drawing;

namespace GameTranslationOverlay.Core.Models
{
    /// <summary>
    /// ウィンドウ情報を格納するモデルクラス
    /// </summary>
    public class WindowInfo
    {
        /// <summary>
        /// ウィンドウハンドル
        /// </summary>
        public IntPtr Handle { get; set; }

        /// <summary>
        /// ウィンドウタイトル
        /// </summary>
        public string Title { get; set; }

        /// <summary>
        /// プロセス名
        /// </summary>
        public string ProcessName { get; set; }

        /// <summary>
        /// ウィンドウの矩形
        /// </summary>
        public Rectangle Bounds { get; set; }

        /// <summary>
        /// ウィンドウのサムネイル画像
        /// </summary>
        public Bitmap Thumbnail { get; set; }

        /// <summary>
        /// 文字列表現
        /// </summary>
        public override string ToString()
        {
            return $"{Title} ({ProcessName}) - {Handle}";
        }
    }
}