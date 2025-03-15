using System.Collections.Generic;
using System.Diagnostics;

namespace GameTranslationOverlay.Core.Translation.Services
{
    /// <summary>
    /// 言語検出と言語関連のユーティリティを提供するクラス
    /// </summary>
    public static class LanguageManager
    {
        /// <summary>
        /// サポートされている言語コード
        /// </summary>
        public static readonly string[] SupportedLanguages = new[] { "en", "ja" };

        /// <summary>
        /// 言語コードと表示名のマッピング
        /// </summary>
        public static readonly Dictionary<string, string> LanguageNames = new Dictionary<string, string>
        {
            { "en", "English" },
            { "ja", "日本語" }
        };

        /// <summary>
        /// テキストの言語を検出する
        /// </summary>
        /// <param name="text">検出対象のテキスト</param>
        /// <returns>検出された言語コード ("en"または"ja")</returns>
        public static string DetectLanguage(string text)
        {
            // 新しいLanguageDetectorクラスを使用して言語を検出
            return LanguageDetector.DetectLanguage(text);
        }

        /// <summary>
        /// 検出結果に基づいて最適な翻訳言語ペアを提案する
        /// </summary>
        /// <param name="text">翻訳対象のテキスト</param>
        /// <param name="preferredTargetLang">希望する翻訳先言語</param>
        /// <returns>ソース言語と翻訳先言語のペア</returns>
        public static (string sourceLang, string targetLang) GetOptimalTranslationPair(string text, string preferredTargetLang)
        {
            string detectedLang = DetectLanguage(text);

            // 検出言語と希望する翻訳先言語が同じ場合、別の言語へ翻訳
            string targetLang = detectedLang == preferredTargetLang ?
                (detectedLang == "ja" ? "en" : "ja") : preferredTargetLang;

            // デバッグ出力
            Debug.WriteLine($"LanguageManager: Detected language: {detectedLang}, Target language: {targetLang}");

            return (detectedLang, targetLang);
        }

        /// <summary>
        /// テキストが混合言語かどうかを判定する
        /// </summary>
        /// <param name="text">検出対象のテキスト</param>
        /// <returns>混合言語の場合はtrue</returns>
        public static bool IsMixedLanguage(string text)
        {
            bool hasJapanese = LanguageDetector.ContainsJapanese(text);
            bool hasEnglish = LanguageDetector.ContainsEnglish(text);

            return hasJapanese && hasEnglish;
        }

        /// <summary>
        /// 混合言語テキストの場合の主要言語を取得
        /// </summary>
        /// <param name="text">検出対象のテキスト</param>
        /// <returns>主要言語のコード</returns>
        public static string GetPrimaryLanguage(string text)
        {
            if (!IsMixedLanguage(text))
            {
                return DetectLanguage(text);
            }

            // 言語比率を計算
            var ratios = LanguageDetector.CalculateLanguageRatios(text);

            // より高い比率の言語を返す
            return ratios["ja"] > ratios["en"] ? "ja" : "en";
        }

        /// <summary>
        /// テキストが日本語を含むかチェック
        /// </summary>
        /// <param name="text">検出対象のテキスト</param>
        /// <returns>日本語を含む場合はtrue</returns>
        public static bool ContainsJapanese(string text)
        {
            return LanguageDetector.ContainsJapanese(text);
        }

        /// <summary>
        /// テキストが英語を含むかチェック
        /// </summary>
        /// <param name="text">検出対象のテキスト</param>
        /// <returns>英語を含む場合はtrue</returns>
        public static bool ContainsEnglish(string text)
        {
            return LanguageDetector.ContainsEnglish(text);
        }

        /// <summary>
        /// デフォルトの言語ペアを取得
        /// </summary>
        /// <returns>デフォルトのソース言語と翻訳先言語のペア</returns>
        public static (string sourceLang, string targetLang) GetDefaultLanguagePair()
        {
            return ("en", "ja");
        }
    }
}