using System.Collections.Generic;

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
            if (string.IsNullOrEmpty(text)) return "en";

            // 日本語文字（ひらがな、カタカナ、漢字）の出現率に基づく簡易判定
            int japaneseChars = 0;

            foreach (char c in text)
            {
                if ((c >= 0x3040 && c <= 0x309F) || // ひらがな
                    (c >= 0x30A0 && c <= 0x30FF) || // カタカナ
                    (c >= 0x4E00 && c <= 0x9FFF))   // 漢字
                {
                    japaneseChars++;
                }
            }

            double japaneseRatio = (double)japaneseChars / text.Length;
            return japaneseRatio > 0.2 ? "ja" : "en";
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
            string targetLang = detectedLang == preferredTargetLang ?
                (detectedLang == "ja" ? "en" : "ja") : preferredTargetLang;

            return (detectedLang, targetLang);
        }
    }
}