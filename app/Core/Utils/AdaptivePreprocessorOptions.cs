using System;
using System.Diagnostics;
using OCRNamespace = GameTranslationOverlay.Core.OCR;

namespace GameTranslationOverlay.Core.Utils
{
    /// <summary>
    /// 適応型前処理のオプションを管理するラッパークラス
    /// </summary>
    public class AdaptivePreprocessorOptions
    {
        // 内部で実際のPreprocessingOptionsインスタンスを保持
        private OCRNamespace.PreprocessingOptions _options;

        /// <summary>
        /// コンストラクタ
        /// </summary>
        public AdaptivePreprocessorOptions()
        {
            _options = new OCRNamespace.PreprocessingOptions();
        }

        /// <summary>
        /// PreprocessingOptionsのプロパティへの変換
        /// </summary>
        /// <returns>OCR用のPreprocessingOptionsインスタンス</returns>
        public OCRNamespace.PreprocessingOptions ToPreprocessingOptions()
        {
            return _options;
        }

        /// <summary>
        /// 設定を適用する
        /// </summary>
        /// <param name="options">適用する設定</param>
        public void ApplySettings(OCRNamespace.PreprocessingOptions options)
        {
            if (options == null)
                return;

            _options = options ?? new OCRNamespace.PreprocessingOptions();
        }

        /// <summary>
        /// SharpnessLevelプロパティ
        /// </summary>
        public float SharpnessLevel
        {
            get { return _options.SharpnessLevel; }
            set { _options.SharpnessLevel = value; }
        }

        /// <summary>
        /// NoiseReductionプロパティ
        /// </summary>
        public int NoiseReduction
        {
            get { return _options.NoiseReduction; }
            set { _options.NoiseReduction = value; }
        }

        /// <summary>
        /// Thresholdプロパティ
        /// </summary>
        public int Threshold
        {
            get { return _options.Threshold; }
            set { _options.Threshold = value; }
        }

        /// <summary>
        /// ScaleFactorプロパティ
        /// </summary>
        public float ScaleFactor
        {
            get { return _options.ScaleFactor; }
            set { _options.ScaleFactor = value; }
        }

        /// <summary>
        /// Paddingプロパティ
        /// </summary>
        public int Padding
        {
            get { return _options.Padding; }
            set { _options.Padding = value; }
        }

        /// <summary>
        /// ContrastLevelプロパティ
        /// </summary>
        public float ContrastLevel
        {
            get { return _options.ContrastLevel; }
            set { _options.ContrastLevel = value; }
        }

        /// <summary>
        /// BrightnessLevelプロパティ
        /// </summary>
        public float BrightnessLevel
        {
            get { return _options.BrightnessLevel; }
            set { _options.BrightnessLevel = value; }
        }

        /// <summary>
        /// 実際のPreprocessingOptionsを取得
        /// </summary>
        public OCRNamespace.PreprocessingOptions GetOptions()
        {
            return _options;
        }

        /// <summary>
        /// 明示的な変換演算子
        /// </summary>
        public static explicit operator OCRNamespace.PreprocessingOptions(AdaptivePreprocessorOptions options)
        {
            return options?._options ?? new OCRNamespace.PreprocessingOptions();
        }

        /// <summary>
        /// 明示的な変換演算子（Utils名前空間のPreprocessingOptions対応）
        /// </summary>
        public static explicit operator PreprocessingOptions(AdaptivePreprocessorOptions options)
        {
            if (options == null)
                return new PreprocessingOptions();

            // Utils名前空間のPreprocessingOptionsに変換する
            var utilsOptions = new PreprocessingOptions();

            // プロパティをコピー
            utilsOptions.ContrastLevel = options.ContrastLevel;
            utilsOptions.BrightnessLevel = options.BrightnessLevel;
            utilsOptions.SharpnessLevel = options.SharpnessLevel;
            utilsOptions.NoiseReduction = options.NoiseReduction;
            utilsOptions.Threshold = options.Threshold;
            utilsOptions.ScaleFactor = options.ScaleFactor;
            utilsOptions.Padding = options.Padding;

            return utilsOptions;
        }
    }
}