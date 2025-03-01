using System;
using System.Drawing;

namespace GameTranslationOverlay.Core.TextDetection
{
    /// <summary>
    /// テキスト領域を表すモデルクラス
    /// </summary>
    public class TextRegion
    {
        /// <summary>
        /// テキスト領域の境界矩形
        /// </summary>
        public Rectangle Bounds { get; set; }

        /// <summary>
        /// 認識されたテキスト
        /// </summary>
        public string Text { get; set; }

        /// <summary>
        /// 認識の信頼度（0.0〜1.0）
        /// </summary>
        public float Confidence { get; set; }

        /// <summary>
        /// テキスト領域の一意性を判断するためのハッシュコード
        /// </summary>
        public override int GetHashCode()
        {
            return Bounds.GetHashCode() ^ (Text?.GetHashCode() ?? 0);
        }

        /// <summary>
        /// 同一テキスト領域の判定
        /// </summary>
        public override bool Equals(object obj)
        {
            if (!(obj is TextRegion other))
                return false;

            return Bounds.Equals(other.Bounds) && Text == other.Text;
        }

        /// <summary>
        /// 文字列表現
        /// </summary>
        public override string ToString()
        {
            return $"TextRegion: '{(Text?.Length > 20 ? Text.Substring(0, 20) + "..." : Text)}' at {Bounds}";
        }
    }
}