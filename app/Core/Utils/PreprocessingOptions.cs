using System;
using OCRNamespace = GameTranslationOverlay.Core.OCR;

namespace GameTranslationOverlay.Core.Utils
{
    /// <summary>
    /// Utils名前空間用の画像前処理オプションを設定するクラス
    /// </summary>
    public class PreprocessingOptions
    {
        /// <summary>
        /// コントラストレベル（1.0=変更なし）
        /// </summary>
        public float ContrastLevel { get; set; } = 1.0f;

        /// <summary>
        /// 明るさレベル（1.0=変更なし）
        /// </summary>
        public float BrightnessLevel { get; set; } = 1.0f;

        /// <summary>
        /// シャープネスレベル（値が大きいほど強く）
        /// </summary>
        public float SharpnessLevel { get; set; } = 0.0f;

        /// <summary>
        /// ノイズ軽減レベル（値が大きいほど強く）
        /// </summary>
        public int NoiseReduction { get; set; } = 0;

        /// <summary>
        /// 二値化の閾値（0=無効）
        /// </summary>
        public int Threshold { get; set; } = 0;

        /// <summary>
        /// スケーリング係数（1.0=変更なし）
        /// </summary>
        public float ScaleFactor { get; set; } = 1.0f;

        /// <summary>
        /// パディング（画像の周囲に追加するピクセル数）
        /// </summary>
        public int Padding { get; set; } = 0;

        /// <summary>
        /// OCR名前空間のPreprocessingOptionsに変換
        /// </summary>
        /// <returns>OCR名前空間のPreprocessingOptionsインスタンス</returns>
        public OCRNamespace.PreprocessingOptions ToOcrOptions()
        {
            return new OCRNamespace.PreprocessingOptions
            {
                ContrastLevel = this.ContrastLevel,
                BrightnessLevel = this.BrightnessLevel,
                SharpnessLevel = this.SharpnessLevel,
                NoiseReduction = this.NoiseReduction,
                Threshold = this.Threshold,
                ScaleFactor = this.ScaleFactor,
                Padding = this.Padding
            };
        }

        /// <summary>
        /// OCR名前空間のPreprocessingOptionsからコピーして作成
        /// </summary>
        /// <param name="ocrOptions">コピー元のオプション</param>
        /// <returns>新しいPreprocessingOptionsインスタンス</returns>
        public static PreprocessingOptions FromOcrOptions(OCRNamespace.PreprocessingOptions ocrOptions)
        {
            if (ocrOptions == null)
                return new PreprocessingOptions();

            return new PreprocessingOptions
            {
                ContrastLevel = ocrOptions.ContrastLevel,
                BrightnessLevel = ocrOptions.BrightnessLevel,
                SharpnessLevel = ocrOptions.SharpnessLevel,
                NoiseReduction = ocrOptions.NoiseReduction,
                Threshold = ocrOptions.Threshold,
                ScaleFactor = ocrOptions.ScaleFactor,
                Padding = ocrOptions.Padding
            };
        }

        /// <summary>
        /// OCR名前空間のPreprocessingOptionsからの明示的変換演算子
        /// </summary>
        public static explicit operator PreprocessingOptions(OCRNamespace.PreprocessingOptions ocrOptions)
        {
            return FromOcrOptions(ocrOptions);
        }

        /// <summary>
        /// OCR名前空間のPreprocessingOptionsへの明示的変換演算子
        /// </summary>
        public static explicit operator OCRNamespace.PreprocessingOptions(PreprocessingOptions options)
        {
            return options?.ToOcrOptions();
        }
    }
}