using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;

namespace GameTranslationOverlay.Core.Translation.Services
{
    /// <summary>
    /// 言語関連の機能を提供するクラス
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
        /// テキストから言語を検出する
        /// </summary>
        /// <param name="text">検出対象のテキスト</param>
        /// <returns>検出された言語コード（"ja"または"en"）</returns>
        public static string DetectLanguage(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                Debug.WriteLine("DetectLanguage: Empty text provided, returning default 'en'");
                return "en";
            }

            // 日本語文字（ひらがな、カタカナ、漢字）の割合を計算
            int japaneseChars = text.Count(c =>
                (c >= 0x3040 && c <= 0x309F) || // ひらがな
                (c >= 0x30A0 && c <= 0x30FF) || // カタカナ
                (c >= 0x4E00 && c <= 0x9FFF));  // 漢字の一部

            double japaneseRatio = (double)japaneseChars / text.Length;

            string detectedLang = japaneseRatio > 0.2 ? "ja" : "en";
            Debug.WriteLine($"DetectLanguage: '{text.Substring(0, Math.Min(20, text.Length))}...' - Japanese ratio: {japaneseRatio:P2}, Detected as {detectedLang}");

            return detectedLang;
        }

        /// <summary>
        /// 検出結果に基づいて最適な翻訳ペアを提案
        /// </summary>
        /// <param name="text">翻訳対象のテキスト</param>
        /// <param name="preferredTargetLang">優先的な翻訳先言語</param>
        /// <returns>最適な翻訳元と翻訳先の言語ペア</returns>
        public static (string sourceLang, string targetLang) GetOptimalTranslationPair(string text, string preferredTargetLang)
        {
            string detectedLang = DetectLanguage(text);

            // 検出された言語と優先言語が同じ場合は、別の言語に翻訳
            string targetLang = detectedLang == preferredTargetLang ?
                (detectedLang == "ja" ? "en" : "ja") : preferredTargetLang;

            Debug.WriteLine($"GetOptimalTranslationPair: Detected={detectedLang}, Preferred={preferredTargetLang}, Selected target={targetLang}");
            return (detectedLang, targetLang);
        }

        /// <summary>
        /// 優先的な翻訳先言語を取得する
        /// </summary>
        /// <param name="sourceLang">翻訳元の言語</param>
        /// <returns>優先的な翻訳先言語</returns>
        public static string GetDefaultTargetLanguage(string sourceLang)
        {
            return sourceLang == "ja" ? "en" : "ja";
        }
    }
}