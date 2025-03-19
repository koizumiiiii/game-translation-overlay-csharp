using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using GameTranslationOverlay.Core.Translation.Interfaces;
using GameTranslationOverlay.Core.Translation.Exceptions;
using GameTranslationOverlay.Core.Translation.Models;
using GameTranslationOverlay.Core.Security;
using GameTranslationOverlay.Core.Configuration;
using GameTranslationOverlay.Core.Licensing;
using static GameTranslationOverlay.Core.Licensing.LicenseManager;

namespace GameTranslationOverlay.Core.Translation.Services
{
    public class AITranslationEngine : ITranslationEngine, IDisposable
    {
        private HttpClient _httpClient;
        private bool _isDisposed = false;
        private readonly int _maxTokensPerRequest = 100;
        private int _remainingTokens;
        private readonly ITranslationEngine _fallbackEngine;
        private readonly string _apiKey;
        private bool _isInitialized = false;

        /// <summary>
        /// 翻訳エンジンが利用可能かどうかを示す
        /// </summary>
        public bool IsAvailable => !string.IsNullOrEmpty(_apiKey) && LicenseManager.Instance.HasFeature(PremiumFeature.AiTranslation);

        /// <summary>
        /// サポートされている言語コードのリスト
        /// </summary>
        public IEnumerable<string> SupportedLanguages => new[] { "en", "ja", "zh", "ko", "fr", "de", "es", "ru" };

        /// <summary>
        /// コンストラクタ
        /// </summary>
        /// <param name="apiKey">OpenAI APIキー (null の場合は AppSettings から取得)</param>
        /// <param name="initialTokens">初期トークン数</param>
        /// <param name="fallbackEngine">フォールバックとして使用する翻訳エンジン</param>
        public AITranslationEngine(string apiKey = null, int initialTokens = 5000, ITranslationEngine fallbackEngine = null)
        {
            try
            {
                // APIキーが指定されていない場合は AppSettings から取得
                if (string.IsNullOrEmpty(apiKey))
                {
                    _apiKey = GetApiKeyFromSettings();
                }
                else
                {
                    _apiKey = apiKey;
                }

                _remainingTokens = initialTokens;
                _httpClient = new HttpClient();
                _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_apiKey}");
                _httpClient.Timeout = TimeSpan.FromSeconds(30); // タイムアウトを30秒に設定

                _fallbackEngine = fallbackEngine;

                Debug.WriteLine("AITranslationEngine: Constructed with API key");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"AITranslationEngine construction error: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 設定から API キーを取得
        /// </summary>
        private string GetApiKeyFromSettings()
        {
            try
            {
                // AppSettingsからカスタムAPIキーを優先で取得
                var settings = AppSettings.Instance;
                if (!string.IsNullOrEmpty(settings.CustomApiKey))
                {
                    return settings.CustomApiKey;
                }

                // 組み込みのAPIキーはリソースからあらかじめ取得した値を使用
                // エラー回避のため、リソースからの直接取得は行わない
                string embeddedApiKey = "sk-..."; // 安全な方法で組み込みAPIキーを設定

                return embeddedApiKey;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting API key from settings: {ex.Message}");
                return string.Empty;
            }
        }

        /// <summary>
        /// 翻訳エンジンを初期化する
        /// </summary>
        public async Task InitializeAsync()
        {
            try
            {
                if (!LicenseManager.Instance.HasFeature(PremiumFeature.AiTranslation))
                {
                    throw new TranslationException("AI翻訳機能は有料プラン専用です");
                }

                if (string.IsNullOrEmpty(_apiKey))
                {
                    throw new TranslationException("APIキーが設定されていません");
                }

                // APIの可用性テスト
                var testResponse = await TestApiConnection();
                if (!testResponse)
                {
                    throw new TranslationException("OpenAI APIへの接続に失敗しました");
                }

                _isInitialized = true;
                Debug.WriteLine("AITranslationEngine: Initialized successfully");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"AITranslationEngine initialization error: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// APIの接続テスト
        /// </summary>
        private async Task<bool> TestApiConnection()
        {
            try
            {
                // 軽量なモデル情報リクエスト
                var response = await _httpClient.GetAsync("https://api.openai.com/v1/models");
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"API connection test failed: {ex.Message}");
                return false;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_isDisposed)
            {
                if (disposing)
                {
                    try
                    {
                        _httpClient?.Dispose();
                        _httpClient = null;
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"LibreTranslateEngine: リソース解放中にエラーが発生しました: {ex.Message}");
                    }
                }

                _isDisposed = true;
            }
        }

        // 各メソッドの先頭に破棄済みチェックを追加
        private void CheckDisposed()
        {
            if (_isDisposed)
            {
                throw new ObjectDisposedException(nameof(LibreTranslateEngine));
            }
        }

        /// <summary>
        /// テキストを翻訳する
        /// </summary>
        public async Task<string> TranslateAsync(string text, string fromLang, string toLang)
        {
            CheckDisposed();
            if (!_isInitialized)
            {
                try
                {
                    await InitializeAsync();
                }
                catch (Exception ex)
                {
                    if (_fallbackEngine != null)
                    {
                        Debug.WriteLine("Using fallback translation engine due to initialization error");
                        return await _fallbackEngine.TranslateAsync(text, fromLang, toLang);
                    }

                    throw new TranslationException($"AI翻訳エンジンの初期化に失敗しました: {ex.Message}", ex);
                }
            }

            // ライセンスチェック
            if (!LicenseManager.Instance.HasFeature(PremiumFeature.AiTranslation))
            {
                if (_fallbackEngine != null)
                {
                    Debug.WriteLine("Using fallback translation engine due to license restriction");
                    return await _fallbackEngine.TranslateAsync(text, fromLang, toLang);
                }

                throw new TranslationException("AI翻訳機能は有料プラン専用です");
            }

            // トークン数のチェック
            int estimatedTokens = EstimateTokens(text);
            if (_remainingTokens < estimatedTokens)
            {
                if (_fallbackEngine != null)
                {
                    Debug.WriteLine("Using fallback translation engine due to token limit");
                    return await _fallbackEngine.TranslateAsync(text, fromLang, toLang);
                }

                throw new TranslationException("AI翻訳のトークン上限に達しました");
            }

            try
            {
                // システムメッセージに翻訳指示を含める
                string systemPrompt = $"Translate the following text from {fromLang} to {toLang}. " +
                                     "Provide only the translated text without explanations.";

                var requestBody = new
                {
                    model = "gpt-3.5-turbo",
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
                    var responseObject = JsonConvert.DeserializeObject<dynamic>(responseBody);

                    // 使用トークン数を更新
                    int tokensUsed = (int)responseObject.usage.total_tokens;
                    _remainingTokens -= tokensUsed;

                    // 翻訳結果を取得
                    string translation = responseObject.choices[0].message.content.ToString().Trim();
                    return translation;
                }
                else
                {
                    string errorContent = await response.Content.ReadAsStringAsync();

                    // フォールバックエンジンが設定されている場合は使用
                    if (_fallbackEngine != null)
                    {
                        Debug.WriteLine($"Using fallback translation engine due to API error: {response.StatusCode}");
                        return await _fallbackEngine.TranslateAsync(text, fromLang, toLang);
                    }

                    throw new TranslationException($"AI翻訳エラー: {response.StatusCode} - {errorContent}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"AI translation error: {ex.Message}");

                // フォールバックエンジンが設定されている場合は使用
                if (_fallbackEngine != null && !(ex is TranslationException))
                {
                    Debug.WriteLine("Using fallback translation engine due to exception");
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
            // 非同期操作を疑似的に待機して警告を回避
            await Task.Delay(1);

            var languageInfos = new List<LanguageInfo>();

            foreach (var lang in SupportedLanguages)
            {
                string name;
                switch (lang)
                {
                    case "en":
                        name = "English";
                        break;
                    case "ja":
                        name = "日本語";
                        break;
                    case "zh":
                        name = "中文";
                        break;
                    case "ko":
                        name = "한국어";
                        break;
                    case "fr":
                        name = "Français";
                        break;
                    case "de":
                        name = "Deutsch";
                        break;
                    case "es":
                        name = "Español";
                        break;
                    case "ru":
                        name = "Русский";
                        break;
                    default:
                        name = lang;
                        break;
                }

                languageInfos.Add(new LanguageInfo { Code = lang, Name = name });
            }

            return languageInfos;
        }

        /// <summary>
        /// テキストからおおよそのトークン数を見積もる
        /// </summary>
        private int EstimateTokens(string text)
        {
            // 単純な見積り: 英語では平均4文字で1トークン
            // 日本語・中国語などでは1文字あたり約1〜2トークン
            // ここでは保守的に1文字あたり1トークンと見積もる
            return Math.Max(1, text.Length);
        }

        /// <summary>
        /// 残りのトークン数を取得
        /// </summary>
        public int GetRemainingTokens()
        {
            return _remainingTokens;
        }

        /// <summary>
        /// トークン数をリセット
        /// </summary>
        public void ResetTokens(int tokens = 5000)
        {
            _remainingTokens = tokens;
        }
    }
}