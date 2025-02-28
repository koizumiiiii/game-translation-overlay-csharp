using System;
using System.Drawing;

namespace GameTranslationOverlay.Core.OCR
{
    public class TextRegion
    {
        public Rectangle Bounds { get; set; }
        public string Text { get; set; }
        public float Confidence { get; set; }
        public DateTime DetectedTime { get; set; }

        public TextRegion()
        {
            DetectedTime = DateTime.Now;
        }

        public TextRegion(Rectangle bounds, string text, float confidence)
        {
            Bounds = bounds;
            Text = text;
            Confidence = confidence;
            DetectedTime = DateTime.Now;
        }

        public override string ToString()
        {
            return $"「{Text}」 ({Confidence:P1})";
        }
    }
}