using System;

namespace GameTranslationOverlay.Core.OCR
{
    /// <summary>
    /// OCR処理用の画像前処理のオプションを設定するクラス
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
        /// UtilsのPreprocessingOptionsからコピーして作成
        /// </summary>
        /// <param name="utilsOptions">コピー元のオプション</param>
        /// <returns>新しいPreprocessingOptionsインスタンス</returns>
        public static PreprocessingOptions FromUtilsOptions(GameTranslationOverlay.Core.Utils.PreprocessingOptions utilsOptions)
        {
            if (utilsOptions == null)
                return new PreprocessingOptions();

            return new PreprocessingOptions
            {
                ContrastLevel = utilsOptions.ContrastLevel,
                BrightnessLevel = utilsOptions.BrightnessLevel,
                SharpnessLevel = utilsOptions.SharpnessLevel,
                NoiseReduction = utilsOptions.NoiseReduction,
                Threshold = utilsOptions.Threshold,
                ScaleFactor = utilsOptions.ScaleFactor,
                Padding = utilsOptions.Padding
            };
        }

        /// <summary>
        /// Utils名前空間のPreprocessingOptionsに変換
        /// </summary>
        /// <returns>Utils名前空間のPreprocessingOptionsインスタンス</returns>
        public GameTranslationOverlay.Core.Utils.PreprocessingOptions ToUtilsOptions()
        {
            return new GameTranslationOverlay.Core.Utils.PreprocessingOptions
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
    }
}