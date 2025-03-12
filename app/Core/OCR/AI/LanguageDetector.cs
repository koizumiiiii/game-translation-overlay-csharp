using System;
using System.Linq;
using System.Drawing;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Diagnostics;
using GameTranslationOverlay.Core.Diagnostics;

namespace GameTranslationOverlay.Core.OCR.AI
{
    /// <summary>
    /// 画像やテキストの言語を検出するためのユーティリティクラス
    /// </summary>
    public class LanguageDetector
    {
        // 最小テキスト長（短すぎるテキストは信頼性が低いため）
        private const int MIN_TEXT_LENGTH = 3;

        // 日本語判定の基準となる割合
        private const double JAPANESE_THRESHOLD = 0.2;

        /// <summary>
        /// テキストの言語を検出（現在は日本語と英語のみ）
        /// </summary>
        /// <param name="text">検出するテキスト</param>
        /// <returns>言語コード（"ja" = 日本語, "en" = 英語・その他）</returns>
        public static string DetectLanguage(string text)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(text) || text.Length < MIN_TEXT_LENGTH)
                {
                    Debug.WriteLine("テキストが短すぎるか空のため、デフォルトの英語を返します");
                    return "en";
                }

                // 日本語文字（ひらがな、カタカナ、漢字）のカウント
                int japaneseChars = text.Count(c => IsJapaneseCharacter(c));

                // 日本語文字の割合を計算
                double japaneseRatio = (double)japaneseChars / text.Length;

                Debug.WriteLine($"言語検出: 文字数={text.Length}, 日本語文字数={japaneseChars}, 割合={japaneseRatio:P2}");

                // 閾値に基づいて判定
                return japaneseRatio > JAPANESE_THRESHOLD ? "ja" : "en";
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"言語検出エラー: {ex.Message}");
                return "en"; // エラー時はデフォルトで英語
            }
        }

        /// <summary>
        /// 画像内のテキストから言語を検出
        /// </summary>
        /// <param name="image">検出対象の画像</param>
        /// <param name="ocrEngine">OCRエンジン</param>
        /// <returns>言語コード（"ja" = 日本語, "en" = 英語・その他）と検出信頼度</returns>
        public static async Task<(string language, double confidence)> DetectLanguageFromImageAsync(Bitmap image, IOcrEngine ocrEngine)
        {
            try
            {
                if (image == null || ocrEngine == null)
                {
                    Debug.WriteLine("画像またはOCRエンジンがnullのため、言語検出に失敗しました");
                    return ("en", 0.0);
                }

                // OCRを使用してテキスト領域を検出
                List<TextRegion> regions = await ocrEngine.DetectTextRegionsAsync(image);

                // テキスト領域がない場合
                if (regions == null || regions.Count == 0)
                {
                    Debug.WriteLine("テキスト領域が検出されなかったためデフォルトの英語を返します");
                    return ("en", 0.0);
                }

                // すべてのテキストを連結（小さすぎるテキストは除外）
                string allText = string.Join(" ", regions
                    .Where(r => !string.IsNullOrWhiteSpace(r.Text) && r.Text.Length >= MIN_TEXT_LENGTH)
                    .Select(r => r.Text));

                // テキストがない場合
                if (string.IsNullOrWhiteSpace(allText))
                {
                    Debug.WriteLine("有効なテキストが検出されなかったためデフォルトの英語を返します");
                    return ("en", 0.0);
                }

                // 全体から言語を検出
                string language = DetectLanguage(allText);

                // 信頼度計算（検出されたテキスト量に基づく）
                double confidence = Math.Min(1.0, allText.Length / 100.0);

                Debug.WriteLine($"画像から言語を検出: {language}, 信頼度: {confidence:P2}, テキスト長: {allText.Length}文字");

                return (language, confidence);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"画像からの言語検出エラー: {ex.Message}");
                return ("en", 0.0); // エラー時はデフォルトで英語
            }
        }

        /// <summary>
        /// 与えられた文字が日本語文字かどうかを判定
        /// </summary>
        private static bool IsJapaneseCharacter(char c)
        {
            // ひらがな: U+3040-U+309F
            // カタカナ: U+30A0-U+30FF
            // 漢字: U+4E00-U+9FFF（CJK統合漢字）
            // 半角カタカナ: U+FF61-U+FF9F
            return (c >= 0x3040 && c <= 0x309F) ||  // ひらがな
                   (c >= 0x30A0 && c <= 0x30FF) ||  // カタカナ
                   (c >= 0x4E00 && c <= 0x9FFF) ||  // 漢字
                   (c >= 0xFF61 && c <= 0xFF9F);    // 半角カタカナ
        }

        /// <summary>
        /// 最適な翻訳言語ペアを取得
        /// </summary>
        /// <param name="text">原文テキスト</param>
        /// <param name="preferredTargetLang">希望する翻訳先言語（ユーザー設定）</param>
        /// <returns>ソース言語とターゲット言語のペア</returns>
        public static (string sourceLang, string targetLang) GetOptimalTranslationPair(string text, string preferredTargetLang)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(text))
                {
                    return ("en", preferredTargetLang ?? "ja");
                }

                // テキストの言語を検出
                string detectedLang = DetectLanguage(text);

                // 翻訳先言語の決定
                // 検出した言語と希望する翻訳先言語が同じ場合は翻訳方向を反転
                string targetLang = detectedLang == preferredTargetLang ?
                    (detectedLang == "ja" ? "en" : "ja") :
                    (preferredTargetLang ?? (detectedLang == "ja" ? "en" : "ja"));

                Debug.WriteLine($"最適な翻訳ペア: {detectedLang} -> {targetLang}, テキスト: {(text.Length > 20 ? text.Substring(0, 20) + "..." : text)}");

                return (detectedLang, targetLang);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"翻訳ペア決定エラー: {ex.Message}");
                return ("en", preferredTargetLang ?? "ja");
            }
        }

        /// <summary>
        /// 画像から日本語が含まれているかを検出
        /// </summary>
        /// <param name="image">検出対象の画像</param>
        /// <param name="ocrEngine">OCRエンジン</param>
        /// <returns>日本語が含まれている場合はtrue</returns>
        public async Task<bool> DetectJapaneseFromImage(Bitmap image, IOcrEngine ocrEngine)
        {
            try
            {
                var result = await DetectLanguageFromImageAsync(image, ocrEngine);
                string language = result.language;
                Debug.WriteLine($"画像から言語検出: {language}");
                return language == "ja";
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"日本語検出エラー: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// AI APIプロバイダー選択のための最適な言語を判断
        /// </summary>
        /// <param name="text">判断するテキスト</param>
        /// <returns>言語の複雑性評価（JapaneseComplex=日本語で複雑, JapaneseSimple=日本語で単純, Other=その他言語）</returns>
        public static LanguageComplexity EvaluateLanguageComplexity(string text)
        {
            try
            {
                // テキストが空または短すぎる場合
                if (string.IsNullOrWhiteSpace(text) || text.Length < MIN_TEXT_LENGTH)
                {
                    return LanguageComplexity.Other;
                }

                // 言語を検出
                string language = DetectLanguage(text);

                // 日本語以外の場合はOther
                if (language != "ja")
                {
                    return LanguageComplexity.Other;
                }

                // 日本語の場合は複雑さを評価
                // 漢字の割合が高い場合は複雑と判断
                int kanjiCount = text.Count(c => c >= 0x4E00 && c <= 0x9FFF);
                double kanjiRatio = (double)kanjiCount / text.Length;

                // テキスト長も考慮（長いテキストは複雑である可能性が高い）
                bool isLongText = text.Length > 50;

                // 漢字比率が15%以上、または長いテキストで漢字が10%以上の場合は複雑と判断
                if (kanjiRatio > 0.15 || (isLongText && kanjiRatio > 0.1))
                {
                    Debug.WriteLine($"複雑な日本語テキストと判断: 漢字比率={kanjiRatio:P2}, 長さ={text.Length}");
                    return LanguageComplexity.JapaneseComplex;
                }
                else
                {
                    Debug.WriteLine($"シンプルな日本語テキストと判断: 漢字比率={kanjiRatio:P2}, 長さ={text.Length}");
                    return LanguageComplexity.JapaneseSimple;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"言語複雑性評価エラー: {ex.Message}");
                return LanguageComplexity.Other;
            }
        }
    }

    /// <summary>
    /// 言語の複雑性評価（AIプロバイダー選択のため）
    /// </summary>
    public enum LanguageComplexity
    {
        /// <summary>
        /// 複雑な日本語（漢字が多いなど）- GPT-4 Visionが適している
        /// </summary>
        JapaneseComplex,

        /// <summary>
        /// シンプルな日本語（ひらがな/カタカナ中心）- Geminiでも対応可能
        /// </summary>
        JapaneseSimple,

        /// <summary>
        /// その他の言語（英語など）- Geminiが適している
        /// </summary>
        Other
    }
}