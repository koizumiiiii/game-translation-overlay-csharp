using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Linq;
using GameTranslationOverlay.Core.Translation.Interfaces;
using GameTranslationOverlay.Core.Translation.Models;
using GameTranslationOverlay.Core.Translation.Exceptions;
using Newtonsoft.Json;

namespace GameTranslationOverlay.Core.Translation.Services
{
    public class LibreTranslateEngine : ITranslationEngine
    {
        private readonly string baseUrl;
        private readonly HttpClient httpClient;
        private bool isInitialized = false;
        private List<LanguageInfo> supportedLanguages = new List<LanguageInfo>();

        public LibreTranslateEngine(string baseUrl = "http://localhost:5000")
        {
            this.baseUrl = baseUrl;
            this.httpClient = new HttpClient();
            this.httpClient.Timeout = TimeSpan.FromSeconds(10);
        }

        public async Task InitializeAsync()
        {
            try
            {
                // サーバー接続確認
                var response = await httpClient.GetAsync($"{baseUrl}/languages");
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    supportedLanguages = JsonConvert.DeserializeObject<List<LanguageInfo>>(content);
                    isInitialized = true;
                    Debug.WriteLine("LibreTranslate server connected successfully");
                }
                else
                {
                    throw new Exception($"サーバーに接続できません。ステータスコード: {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"LibreTranslate initialization error: {ex.Message}");
                throw new Exception("翻訳サーバーへの接続に失敗しました。サーバーが起動しているか確認してください。", ex);
            }
        }

        public async Task<string> TranslateAsync(string text, string fromLang, string toLang)
        {
            if (!isInitialized)
            {
                Debug.WriteLine("Warning: Translation engine not initialized");
                throw new Exception("翻訳エンジンが初期化されていません。");
            }

            if (string.IsNullOrWhiteSpace(text))
            {
                Debug.WriteLine("Warning: Empty text provided for translation");
                return string.Empty;
            }

            try
            {
                Debug.WriteLine($"Translating: '{text.Substring(0, Math.Min(30, text.Length))}...' from {fromLang} to {toLang}");

                var content = new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("q", text),
                    new KeyValuePair<string, string>("source", fromLang),
                    new KeyValuePair<string, string>("target", toLang)
                });

                var response = await httpClient.PostAsync($"{baseUrl}/translate", content);
                response.EnsureSuccessStatusCode();

                var responseString = await response.Content.ReadAsStringAsync();
                Debug.WriteLine($"Response received: {responseString}");

                var translationResponse = JsonConvert.DeserializeObject<LibreTranslateResponse>(responseString);

                if (string.IsNullOrEmpty(translationResponse?.TranslatedText))
                {
                    throw new Exception("翻訳結果が無効です。");
                }

                Debug.WriteLine($"Translation result: '{translationResponse.TranslatedText.Substring(0, Math.Min(30, translationResponse.TranslatedText.Length))}...'");
                return translationResponse.TranslatedText;
            }
            catch (HttpRequestException ex)
            {
                Debug.WriteLine($"HTTP Request Error: {ex.Message}");
                throw new Exception($"翻訳サーバーへの接続エラー: {ex.Message}", ex);
            }
            catch (JsonException ex)
            {
                Debug.WriteLine($"JSON Parsing Error: {ex.Message}");
                throw new Exception("翻訳結果の解析に失敗しました", ex);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Translation Error: {ex.Message}");
                throw new Exception($"翻訳処理エラー: {ex.Message}", ex);
            }
        }

        public bool IsAvailable => isInitialized;

        public IEnumerable<string> SupportedLanguages => GetSupportedLanguageCodes();

        private IEnumerable<string> GetSupportedLanguageCodes()
        {
            if (supportedLanguages.Count > 0)
            {
                return supportedLanguages.Select(l => l.Code);
            }

            // 基本的な言語のリスト（フォールバック）
            return new[] { "en", "ja", "zh", "ko", "fr", "de", "es", "ru" };
        }

        // インターフェース要件を満たすために追加
        public async Task<IEnumerable<LanguageInfo>> GetSupportedLanguagesAsync()
        {
            if (!isInitialized)
            {
                try
                {
                    await InitializeAsync();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Failed to initialize: {ex.Message}");
                    throw;
                }
            }

            if (supportedLanguages.Count == 0)
            {
                try
                {
                    var response = await httpClient.GetAsync($"{baseUrl}/languages");
                    response.EnsureSuccessStatusCode();

                    var content = await response.Content.ReadAsStringAsync();
                    supportedLanguages = JsonConvert.DeserializeObject<List<LanguageInfo>>(content);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Failed to get languages: {ex.Message}");
                    throw new Exception("言語リストの取得に失敗しました。", ex);
                }
            }

            return supportedLanguages;
        }
    }
}