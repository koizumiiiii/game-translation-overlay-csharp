using System.Collections.Generic;

namespace GameTranslationOverlay.Core.UI
{
    /// <summary>
    /// 言語関連の情報を管理するクラス
    /// </summary>
    public static class LanguageManager
    {
        /// <summary>
        /// サポートされている言語コードの配列
        /// </summary>
        public static readonly string[] SupportedLanguages = new[] { "en", "ja", "zh", "ko", "fr", "de", "es", "ru" };

        /// <summary>
        /// 言語コードと言語名のマッピング
        /// </summary>
        public static readonly Dictionary<string, string> LanguageNames = new Dictionary<string, string>
        {
            { "en", "英語" },
            { "ja", "日本語" },
            { "zh", "中国語" },
            { "ko", "韓国語" },
            { "fr", "フランス語" },
            { "de", "ドイツ語" },
            { "es", "スペイン語" },
            { "ru", "ロシア語" }
        };

        /// <summary>
        /// 言語コードからその言語で表示される言語名のマッピング（将来的な多言語UI用）
        /// </summary>
        private static readonly Dictionary<string, Dictionary<string, string>> LocalizedLanguageNames = new Dictionary<string, Dictionary<string, string>>
        {
            {
                "en", new Dictionary<string, string>
                {
                    { "en", "English" },
                    { "ja", "Japanese" },
                    { "zh", "Chinese" },
                    { "ko", "Korean" },
                    { "fr", "French" },
                    { "de", "German" },
                    { "es", "Spanish" },
                    { "ru", "Russian" }
                }
            },
            {
                "ja", new Dictionary<string, string>
                {
                    { "en", "英語" },
                    { "ja", "日本語" },
                    { "zh", "中国語" },
                    { "ko", "韓国語" },
                    { "fr", "フランス語" },
                    { "de", "ドイツ語" },
                    { "es", "スペイン語" },
                    { "ru", "ロシア語" }
                }
            }
        };

        /// <summary>
        /// 言語コードからその言語での表記を取得する
        /// </summary>
        /// <param name="langCode">言語コード</param>
        /// <param name="displayLangCode">表示言語コード（デフォルトは日本語）</param>
        /// <returns>言語名</returns>
        public static string GetLanguageNameLocalized(string langCode, string displayLangCode = "ja")
        {
            // 表示言語が存在しない場合はデフォルトを使用
            if (!LocalizedLanguageNames.ContainsKey(displayLangCode))
            {
                displayLangCode = "ja";
            }

            // その言語コードが存在しない場合は言語コードそのものを返す
            if (!LocalizedLanguageNames[displayLangCode].ContainsKey(langCode))
            {
                return langCode;
            }

            return LocalizedLanguageNames[displayLangCode][langCode];
        }

        /// <summary>
        /// デフォルトの翻訳ソース言語コード（自動検出時）
        /// </summary>
        public static string GetDefaultSourceLanguage()
        {
            return "auto";
        }

        /// <summary>
        /// デフォルトの翻訳先言語コード
        /// </summary>
        public static string GetDefaultTargetLanguage()
        {
            return "ja";
        }

        /// <summary>
        /// 言語コードが有効かどうかを確認する
        /// </summary>
        /// <param name="langCode">言語コード</param>
        /// <returns>有効な場合はtrue</returns>
        public static bool IsValidLanguageCode(string langCode)
        {
            foreach (var code in SupportedLanguages)
            {
                if (code == langCode)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 言語コードから言語名を取得する
        /// </summary>
        /// <param name="langCode">言語コード</param>
        /// <returns>言語名（見つからない場合は言語コードそのもの）</returns>
        public static string GetLanguageName(string langCode)
        {
            if (LanguageNames.ContainsKey(langCode))
            {
                return LanguageNames[langCode];
            }
            return langCode;
        }
    }
}