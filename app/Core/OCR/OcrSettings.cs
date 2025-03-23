using System;
using System.Diagnostics;

namespace GameTranslationOverlay.Core.OCR
{
    /// <summary>
    /// OCRの設定を管理するクラス
    /// </summary>
    public class OcrSettings
    {
        /// <summary>
        /// デフォルトのOCR設定
        /// </summary>
        public static OcrSettings Default { get; } = new OcrSettings
        {
            EnablePreprocessing = true,
            ConfidenceThreshold = 0.5f,
            PreprocessingOptions = new PreprocessingOptions
            {
                ContrastLevel = 1.0f,
                BrightnessLevel = 1.0f,
                SharpnessLevel = 0.0f,
                NoiseReduction = 0,
                Threshold = 0,
                ScaleFactor = 1.0f,
                Padding = 0
            }
        };

        /// <summary>
        /// 前処理の有効/無効
        /// </summary>
        public bool EnablePreprocessing { get; set; } = true;

        /// <summary>
        /// 信頼度閾値（0.0-1.0）
        /// </summary>
        public float ConfidenceThreshold { get; set; } = 0.5f;

        /// <summary>
        /// 前処理オプション
        /// </summary>
        public PreprocessingOptions PreprocessingOptions { get; set; } = new PreprocessingOptions();

        /// <summary>
        /// 設定をコピーする
        /// </summary>
        public OcrSettings Clone()
        {
            return new OcrSettings
            {
                EnablePreprocessing = this.EnablePreprocessing,
                ConfidenceThreshold = this.ConfidenceThreshold,
                PreprocessingOptions = new PreprocessingOptions
                {
                    ContrastLevel = this.PreprocessingOptions.ContrastLevel,
                    BrightnessLevel = this.PreprocessingOptions.BrightnessLevel,
                    SharpnessLevel = this.PreprocessingOptions.SharpnessLevel,
                    NoiseReduction = this.PreprocessingOptions.NoiseReduction,
                    Threshold = this.PreprocessingOptions.Threshold,
                    ScaleFactor = this.PreprocessingOptions.ScaleFactor,
                    Padding = this.PreprocessingOptions.Padding
                }
            };
        }
    }
}