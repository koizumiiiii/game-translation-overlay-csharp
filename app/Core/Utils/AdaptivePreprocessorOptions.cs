using System;
using OCRNamespace = GameTranslationOverlay.Core.OCR;

namespace GameTranslationOverlay.Core.Utils
{
    /// <summary>
    /// 適応型前処理オプションを管理するクラス
    /// </summary>
    public class AdaptivePreprocessorOptions
    {
        private PreprocessingOptions _options;

        /// <summary>
        /// コンストラクタ
        /// </summary>
        public AdaptivePreprocessorOptions()
        {
            _options = new PreprocessingOptions();
        }

        /// <summary>
        /// OCR名前空間のPreprocessingOptionsから作成
        /// </summary>
        public AdaptivePreprocessorOptions(OCRNamespace.PreprocessingOptions ocrOptions)
        {
            _options = PreprocessingOptions.FromOcrOptions(ocrOptions);
        }

        /// <summary>
        /// Utils名前空間のPreprocessingOptionsからの作成
        /// </summary>
        public AdaptivePreprocessorOptions(PreprocessingOptions options)
        {
            _options = options ?? new PreprocessingOptions();
        }

        /// <summary>
        /// 現在のオプションを取得または設定
        /// </summary>
        public PreprocessingOptions Options
        {
            get => _options;
            set => _options = value ?? new PreprocessingOptions();
        }

        /// <summary>
        /// OCR名前空間のPreprocessingOptionsへの明示的変換演算子
        /// </summary>
        public static explicit operator OCRNamespace.PreprocessingOptions(AdaptivePreprocessorOptions options)
        {
            return options?._options.ToOcrOptions() ?? new OCRNamespace.PreprocessingOptions();
        }

        /// <summary>
        /// Utils名前空間のPreprocessingOptionsへの明示的変換演算子
        /// </summary>
        public static explicit operator PreprocessingOptions(AdaptivePreprocessorOptions options)
        {
            return options?._options ?? new PreprocessingOptions();
        }

        /// <summary>
        /// Utils名前空間のPreprocessingOptionsからの明示的変換演算子
        /// </summary>
        public static explicit operator AdaptivePreprocessorOptions(PreprocessingOptions options)
        {
            return new AdaptivePreprocessorOptions(options);
        }
    }
}