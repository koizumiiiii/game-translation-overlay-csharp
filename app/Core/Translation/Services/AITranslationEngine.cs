using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Text;
using GameTranslationOverlay.Core.Translation.Interfaces;
using GameTranslationOverlay.Core.Translation.Models;
using GameTranslationOverlay.Core.Translation.Exceptions;
using Newtonsoft.Json;

namespace GameTranslationOverlay.Core.Translation.Services
{
    /// <summary>
    /// OpenAI APIを使用したAI翻訳エンジン
    /// </summary>
    public class AITranslationEngine : ITranslationEngine
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;
        private readonly string _model;
        private readonly int _maxTokensPerRequest;
        private int _remainingTokens;
        private bool _isInitialized = false;
        private List<LanguageInfo> _supportedLanguages = new List<LanguageInfo>();

        // フォールバック用の翻訳エンジン
        private readonly ITranslationEngine _fallbackEngine;

        /// <summary>
        /// AITranslationEngineのコンストラクタ
        /// </summary>
        /// <param name="apiKey">OpenAI APIキー</param>
        /// <param name="model">使用するモデル（デフォルト: gpt-3.5-turbo）</param>
        /// <param name="maxTokensPerRequest">1リクエストあたりの最大トークン数</param>
        /// <param name="totalTokenLimit">デモ版での合計トークン上限</param>
        /// <param name="fallbackEngine">フォールバック用翻訳エンジン（オプション）</param>
        public AITranslationEngine(string apiKey, string model = "gpt-3.5-turbo", int maxTokensPerRequest = 100, int totalTokenLimit = 5000, ITranslationEngine fallbackEngine = null)
        {
            _apiKey = apiKey ?? throw new ArgumentNullException(nameof(apiKey));
            _model = model;
            _maxTokensPerRequest = maxTokensPerRequest;
            _remainingTokens = totalTokenLimit;
            _fallbackEngine = fallbackEngine;

            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
            _httpClient.Timeout = TimeSpan.FromSeconds(10);

            // サポートされている言語の初期化
            InitializeSupportedLanguages();

            Debug.WriteLine($"AITranslationEngine: 初期化 (モデル={model}, 最大トークン={maxTokensPerRequest}, 合計トークン上限={totalTokenLimit})");
        }

        /// <summary>
        /// サポートされている言語を初期化する
        /// </summary>
        private void InitializeSupportedLanguages()
        {
            _supportedLanguages = new List<LanguageInfo>
            {
                new LanguageInfo { Code = "en", Name = "English" },
                new LanguageInfo { Code = "ja", Name = "日本語" },
                new LanguageInfo { Code = "zh", Name = "中文" },
                new LanguageInfo { Code = "ko", Name = "한국어" },
                new LanguageInfo { Code = "fr", Name = "Français" },
                new LanguageInfo { Code = "de", Name = "Deutsch" },
                new LanguageInfo { Code = "es", Name = "Español" },
                new LanguageInfo { Code = "ru", Name = "Русский" }
            };
        }

        /// <summary>
        /// 翻訳エンジンを初期化する
        /// </summary>
        public async Task InitializeAsync()
        {
            try
            {
                // APIが有効かどうかを確認するための最小限のリクエスト
                var requestBody = new
                {
                    model = _model,
                    messages = new[]
                    {
                        new { role = "system", content = "This is a test request to verify API connectivity." },
                        new { role = "user", content = "Hello" }
                    },
                    max_tokens = 1
                };

                var content = new StringContent(
                    JsonConvert.SerializeObject(requestBody),
                    Encoding.UTF8,
                    "application/json");

                var response = await _httpClient.PostAsync("https://api.openai.com/v1/chat/completions", content);

                if (response.IsSuccessStatusCode)
                {
                    _isInitialized = true;
                    Debug.WriteLine("AITranslationEngine: 初期化に成功しました");
                }
                else
                {
                    string errorContent = await response.Content.ReadAsStringAsync();
                    throw new TranslationException($"APIサーバーエラー: {response.StatusCode} - {errorContent}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"AITranslationEngine 初期化エラー: {ex.Message}");
                throw new TranslationException("AI翻訳エンジンの初期化に失敗しました。APIキーを確認してください。", ex);
            }
        }

        /// <summary>
        /// テキストを翻訳する
        /// </summary>
        /// <param name="text">翻訳元のテキスト</param>
        /// <param name="fromLang">翻訳元の言語コード</param>
        /// <param name="toLang">翻訳先の言語コード</param>
        /// <returns>翻訳されたテキスト</returns>
        public async Task<string> TranslateAsync(string text, string fromLang, string toLang)
        {
            if (!_isInitialized)
            {
                throw new TranslationException("AI翻訳エンジンが初期化されていません。");
            }

            if (string.IsNullOrWhiteSpace(text))
            {
                Debug.WriteLine("Warning: 翻訳のために空のテキストが提供されました");
                return string.Empty;
            }

            // トークン数の簡易計算（実際にはもっと複雑）
            int estimatedTokens = (int)(text.Length / 3.5);

            if (_remainingTokens < estimatedTokens)
            {
                Debug.WriteLine($"トークン上限に達しました: 残り{_remainingTokens}, 必要約{estimatedTokens}");

                // フォールバックエンジンが利用可能な場合
                if (_fallbackEngine != null)
                {
                    Debug.WriteLine("フォールバック翻訳エンジンを使用します");
                    return await _fallbackEngine.TranslateAsync(text, fromLang, toLang);
                }

                throw new TranslationException($"トークン上限に達しました。残りトークン: {_remainingTokens}、必要トークン: 約{estimatedTokens}。デモ版では{_remainingTokens}トークンまで利用可能です。");
            }

            try
            {
                // システムメッセージに翻訳指示を含める
                string fromLangName = GetLanguageName(fromLang);
                string toLangName = GetLanguageName(toLang);

                string systemPrompt = $"Translate the following text from {fromLangName} to {toLangName}. " +
                                     "Provide only the translated text without explanations or additional content.";

                var requestBody = new
                {
                    model = _model,
                    messages = new[]
                    {
                        new { role = "system", content = systemPrompt },
                        new { role = "user", content = text }
                    },
                    max_tokens = _maxTokensPerRequest
                };

                var content = new StringContent(
                    JsonConvert.SerializeObject(requestBody),
                    Encoding.UTF8,
                    "application/json");

                HttpResponseMessage response = await _httpClient.PostAsync(
                    "https://api.openai.com/v1/chat/completions", content);

                if (response.IsSuccessStatusCode)
                {
                    string responseBody = await response.Content.ReadAsStringAsync();
                    Debug.WriteLine($"API レスポンス: {responseBody}");

                    dynamic responseObject = JsonConvert.DeserializeObject<dynamic>(responseBody);

                    // 使用トークン数を更新
                    int tokensUsed = (int)responseObject.usage.total_tokens;
                    _remainingTokens -= tokensUsed;
                    Debug.WriteLine($"使用トークン: {tokensUsed}, 残り: {_remainingTokens}");

                    // 翻訳結果を取得
                    string translation = responseObject.choices[0].message.content.ToString().Trim();
                    return translation;
                }
                else
                {
                    string errorContent = await response.Content.ReadAsStringAsync();
                    Debug.WriteLine($"API エラー: {response.StatusCode} - {errorContent}");

                    // フォールバックエンジンが利用可能な場合
                    if (_fallbackEngine != null)
                    {
                        Debug.WriteLine("APIエラーが発生したため、フォールバック翻訳エンジンを使用します");
                        return await _fallbackEngine.TranslateAsync(text, fromLang, toLang);
                    }

                    throw new TranslationException($"AI翻訳エラー: {response.StatusCode} - {errorContent}");
                }
            }
            catch (TranslationException)
            {
                throw; // 既に処理済みの例外は再スロー
            }
            catch (HttpRequestException ex)
            {
                Debug.WriteLine($"HTTP リクエストエラー: {ex.Message}");

                // フォールバックエンジンが利用可能な場合
                if (_fallbackEngine != null)
                {
                    Debug.WriteLine("HTTP接続エラーが発生したため、フォールバック翻訳エンジンを使用します");
                    return await _fallbackEngine.TranslateAsync(text, fromLang, toLang);
                }

                throw new TranslationException("AI翻訳サーバーへの接続に失敗しました。ネットワーク接続を確認してください。", ex);
            }
            catch (JsonException ex)
            {
                Debug.WriteLine($"JSON パースエラー: {ex.Message}");

                // フォールバックエンジンが利用可能な場合
                if (_fallbackEngine != null)
                {
                    Debug.WriteLine("JSONパースエラーが発生したため、フォールバック翻訳エンジンを使用します");
                    return await _fallbackEngine.TranslateAsync(text, fromLang, toLang);
                }

                throw new TranslationException("翻訳結果の解析に失敗しました", ex);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"翻訳エラー: {ex.Message}");

                // フォールバックエンジンが利用可能な場合
                if (_fallbackEngine != null)
                {
                    Debug.WriteLine("エラーが発生したため、フォールバック翻訳エンジンを使用します");
                    return await _fallbackEngine.TranslateAsync(text, fromLang, toLang);
                }

                throw new TranslationException($"AI翻訳処理中にエラーが発生しました: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 詳細な言語情報を取得する
        /// </summary>
        public async Task<IEnumerable<LanguageInfo>> GetSupportedLanguagesAsync()
        {
            // 非同期メソッドとするためにTask.FromResultを使用
            return await Task.FromResult(_supportedLanguages);
        }

        /// <summary>
        /// 言語コードから言語名を取得する
        /// </summary>
        private string GetLanguageName(string langCode)
        {
            var langInfo = _supportedLanguages.Find(l => l.Code == langCode);
            return langInfo?.Name ?? langCode;
        }

        /// <summary>
        /// 残りのトークン数を取得する
        /// </summary>
        public int GetRemainingTokens()
        {
            return _remainingTokens;
        }

        /// <summary>
        /// トークン数を再設定する
        /// </summary>
        public void ResetTokens(int tokenCount)
        {
            _remainingTokens = tokenCount;
            Debug.WriteLine($"トークン数を再設定しました: {_remainingTokens}");
        }

        /// <summary>
        /// 翻訳エンジンが利用可能かどうかを示す
        /// </summary>
        public bool IsAvailable => _isInitialized;

        /// <summary>
        /// サポートされている言語コードのリスト
        /// </summary>
        public IEnumerable<string> SupportedLanguages => GetSupportedLanguageCodes();

        private IEnumerable<string> GetSupportedLanguageCodes()
        {
            return _supportedLanguages.ConvertAll(l => l.Code);
        }
    }
}