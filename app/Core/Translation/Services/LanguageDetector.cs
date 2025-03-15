using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Diagnostics;

namespace GameTranslationOverlay.Core.Translation.Services
{
    /// <summary>
    /// 高度な言語検出機能を提供するクラス
    /// </summary>
    public class LanguageDetector
    {
        // 言語特性のパターン
        private static readonly Dictionary<string, Regex> LanguagePatterns = new Dictionary<string, Regex>
        {
            // 日本語特有のパターン（ひらがな、カタカナ、漢字）
            { "ja", new Regex(@"[\p{IsHiragana}\p{IsKatakana}\p{IsCJKUnifiedIdeographs}ー・、。]", RegexOptions.Compiled) },
            
            // 英語特有のパターン（基本ラテン文字、アポストロフィ、一般的な英語単語のパターン）
            { "en", new Regex(@"[a-zA-Z]('s|'t|'re|'ve|'ll|'d|ing|ed|ly|tion)\b|^[A-Z][a-z]+\s", RegexOptions.Compiled) }
        };

        // 言語特有の記号や句読点
        private static readonly Dictionary<string, char[]> LanguagePunctuation = new Dictionary<string, char[]>
        {
            { "ja", new[] { '。', '、', '「', '」', '・', '：', '！', '？', '～', '…' } },
            { "en", new[] { '.', ',', '!', '?', ':', ';', '"', '\'', '-', '(', ')' } }
        };

        // 各文字セットの文字コード範囲
        private static readonly Dictionary<string, List<(int Start, int End)>> CharacterRanges = new Dictionary<string, List<(int Start, int End)>>
        {
            {
                "ja", new List<(int Start, int End)>
                {
                    (0x3040, 0x309F),  // ひらがな
                    (0x30A0, 0x30FF),  // カタカナ
                    (0x4E00, 0x9FFF),  // 漢字 (CJK統合漢字)
                    (0xFF00, 0xFFEF)   // 全角英数字と記号
                }
            },
            {
                "en", new List<(int Start, int End)>
                {
                    (0x0020, 0x007F)   // ASCII (基本ラテン文字)
                }
            }
        };

        /// <summary>
        /// テキストの言語を高精度で検出する
        /// </summary>
        /// <param name="text">検出対象のテキスト</param>
        /// <returns>検出された言語コード</returns>
        public static string DetectLanguage(string text)
        {
            if (string.IsNullOrEmpty(text))
                return "en"; // デフォルトは英語

            // 短いテキスト（3文字以下）の場合は文字コード範囲のみで判定
            if (text.Length <= 3)
                return DetectLanguageByCharacterRange(text);

            // 言語ごとのスコア
            var scores = new Dictionary<string, double>
            {
                { "ja", 0.0 },
                { "en", 0.0 }
            };

            // 1. 文字コード範囲によるスコア計算
            CalculateCharacterRangeScores(text, scores);

            // 2. 言語パターンによるスコア計算
            CalculatePatternScores(text, scores);

            // 3. 句読点によるスコア計算
            CalculatePunctuationScores(text, scores);

            // 最もスコアが高い言語を返す
            var mostLikelyLanguage = scores.OrderByDescending(pair => pair.Value).First().Key;
            Debug.WriteLine($"Language detection scores: JP={scores["ja"]}, EN={scores["en"]}, Result={mostLikelyLanguage}");

            return mostLikelyLanguage;
        }

        /// <summary>
        /// 文字コード範囲に基づいて言語スコアを計算
        /// </summary>
        private static void CalculateCharacterRangeScores(string text, Dictionary<string, double> scores)
        {
            int totalChars = text.Length;
            var charCounts = new Dictionary<string, int>
            {
                { "ja", 0 },
                { "en", 0 }
            };

            foreach (char c in text)
            {
                foreach (var lang in CharacterRanges.Keys)
                {
                    foreach (var range in CharacterRanges[lang])
                    {
                        if (c >= range.Start && c <= range.End)
                        {
                            charCounts[lang]++;
                            break;
                        }
                    }
                }
            }

            // スコアの計算 (文字数の割合)
            foreach (var lang in scores.Keys.ToList())
            {
                scores[lang] += (double)charCounts[lang] / totalChars * 0.6; // 60%のウェイト
            }
        }

        /// <summary>
        /// 言語パターンに基づいてスコアを計算
        /// </summary>
        private static void CalculatePatternScores(string text, Dictionary<string, double> scores)
        {
            foreach (var lang in LanguagePatterns.Keys)
            {
                int matches = LanguagePatterns[lang].Matches(text).Count;
                double normalizedScore = Math.Min(1.0, (double)matches / (text.Length / 5)); // 5文字ごとに1パターンを期待
                scores[lang] += normalizedScore * 0.3; // 30%のウェイト
            }
        }

        /// <summary>
        /// 句読点に基づいてスコアを計算
        /// </summary>
        private static void CalculatePunctuationScores(string text, Dictionary<string, double> scores)
        {
            foreach (var lang in LanguagePunctuation.Keys)
            {
                int punctCount = text.Count(c => LanguagePunctuation[lang].Contains(c));
                double normalizedScore = Math.Min(1.0, (double)punctCount / (text.Length / 10)); // 10文字ごとに1記号を期待
                scores[lang] += normalizedScore * 0.1; // 10%のウェイト
            }
        }

        /// <summary>
        /// 文字コード範囲のみで言語を検出（短いテキスト用）
        /// </summary>
        private static string DetectLanguageByCharacterRange(string text)
        {
            int jaChars = 0;
            int enChars = 0;

            foreach (char c in text)
            {
                bool matched = false;

                // 日本語文字チェック
                foreach (var range in CharacterRanges["ja"])
                {
                    if (c >= range.Start && c <= range.End)
                    {
                        jaChars++;
                        matched = true;
                        break;
                    }
                }

                if (!matched)
                {
                    // 英語文字チェック
                    foreach (var range in CharacterRanges["en"])
                    {
                        if (c >= range.Start && c <= range.End)
                        {
                            enChars++;
                            break;
                        }
                    }
                }
            }

            // 日本語文字が1つでもあれば日本語、そうでなければ英語
            return jaChars > 0 ? "ja" : "en";
        }

        /// <summary>
        /// テキストに日本語が含まれているかをチェック
        /// </summary>
        /// <param name="text">検査するテキスト</param>
        /// <returns>日本語が含まれているか</returns>
        public static bool ContainsJapanese(string text)
        {
            if (string.IsNullOrEmpty(text))
                return false;

            foreach (char c in text)
            {
                foreach (var range in CharacterRanges["ja"])
                {
                    if (c >= range.Start && c <= range.End)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// テキストに英語が含まれているかをチェック
        /// </summary>
        /// <param name="text">検査するテキスト</param>
        /// <returns>英語が含まれているか</returns>
        public static bool ContainsEnglish(string text)
        {
            if (string.IsNullOrEmpty(text))
                return false;

            foreach (char c in text)
            {
                foreach (var range in CharacterRanges["en"])
                {
                    if (c >= range.Start && c <= range.End)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// 混合言語テキストの場合、言語の割合を計算
        /// </summary>
        /// <param name="text">分析するテキスト</param>
        /// <returns>言語コードと割合のディクショナリ</returns>
        public static Dictionary<string, double> CalculateLanguageRatios(string text)
        {
            if (string.IsNullOrEmpty(text))
                return new Dictionary<string, double> { { "en", 1.0 }, { "ja", 0.0 } };

            int totalChars = text.Length;
            var charCounts = new Dictionary<string, int>
            {
                { "ja", 0 },
                { "en", 0 },
                { "other", 0 }
            };

            foreach (char c in text)
            {
                bool matched = false;

                // 言語ごとに文字をカウント
                foreach (var lang in CharacterRanges.Keys)
                {
                    foreach (var range in CharacterRanges[lang])
                    {
                        if (c >= range.Start && c <= range.End)
                        {
                            charCounts[lang]++;
                            matched = true;
                            break;
                        }
                    }

                    if (matched)
                        break;
                }

                // どの言語にも該当しない場合
                if (!matched)
                {
                    charCounts["other"]++;
                }
            }

            // 割合の計算
            return new Dictionary<string, double>
            {
                { "ja", (double)charCounts["ja"] / totalChars },
                { "en", (double)charCounts["en"] / totalChars },
                { "other", (double)charCounts["other"] / totalChars }
            };
        }
    }
}