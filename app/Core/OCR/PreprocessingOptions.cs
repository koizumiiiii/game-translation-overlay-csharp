namespace GameTranslationOverlay.Core.OCR
{
    /// <summary>
    /// 画像前処理のオプションを設定するクラス
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
    }

    /// <summary>
    /// 画像前処理ユーティリティ
    /// </summary>
    public static class ImagePreprocessor
    {
        /// <summary>
        /// 日本語テキスト用プリセット
        /// </summary>
        public static PreprocessingOptions JapaneseTextPreset => new PreprocessingOptions
        {
            ContrastLevel = 1.2f,
            BrightnessLevel = 1.0f,
            SharpnessLevel = 0.3f,
            NoiseReduction = 1,
            ScaleFactor = 1.5f,
            Padding = 10
        };

        /// <summary>
        /// 英語テキスト用プリセット
        /// </summary>
        public static PreprocessingOptions EnglishTextPreset => new PreprocessingOptions
        {
            ContrastLevel = 1.1f,
            BrightnessLevel = 1.0f,
            SharpnessLevel = 0.2f,
            NoiseReduction = 1,
            ScaleFactor = 1.0f,
            Padding = 5
        };

        /// <summary>
        /// ゲーム用軽量プリセット
        /// </summary>
        public static PreprocessingOptions GameTextLightPreset => new PreprocessingOptions
        {
            ContrastLevel = 1.1f,
            BrightnessLevel = 1.0f,
            SharpnessLevel = 0.0f,
            NoiseReduction = 0,
            ScaleFactor = 1.0f,
            Padding = 2
        };
    }
}