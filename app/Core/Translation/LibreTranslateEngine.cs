using System;
using System.Net.Http;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using System.Linq;

namespace GameTranslationOverlay.Core.Translation
{
    public class LibreTranslateEngine : ITranslationEngine, IDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly TranslationConfig _config;
        private readonly ILogger _logger;
        private bool _isAvailable;

        public LibreTranslateEngine(TranslationConfig config, ILogger logger)
        {
            _config = config;
            _logger = logger;
            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(10) // 10秒でタイムアウト
            };
            _isAvailable = false;
        }

        public bool IsAvailable => _isAvailable;

        public IEnumerable<string> SupportedLanguages { get; private set; }

        public async Task InitializeAsync()
        {
            try
            {
                // 初期化時に言語リストを取得
                await LoadSupportedLanguagesAsync();
                _isAvailable = true;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to initialize LibreTranslate: {ex.Message}");
                _isAvailable = false;
                throw new TranslationEngineException("Failed to initialize translation engine", ex);
            }
        }

        public async Task<string> TranslateAsync(string text, string fromLang, string toLang)
        {
            if (!_isAvailable)
            {
                throw new TranslationEngineException("Translation engine is not available");
            }

            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            var retryCount = 0;
            const int maxRetries = 3;
            const int baseDelay = 1000; // 1秒

            while (retryCount < maxRetries)
            {
                try
                {
                    var response = await SendTranslationRequestAsync(text, fromLang, toLang);
                    return response;
                }
                catch (HttpRequestException ex)
                {
                    _logger.LogWarning($"Translation request failed (attempt {retryCount + 1}): {ex.Message}");

                    if (retryCount == maxRetries - 1)
                    {
                        throw new TranslationEngineException("Translation failed after all retries", ex);
                    }

                    // 指数バックオフ
                    var delay = baseDelay * Math.Pow(2, retryCount);
                    await Task.Delay((int)delay);
                    retryCount++;
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Unexpected error during translation: {ex.Message}");
                    throw new TranslationEngineException("Unexpected translation error", ex);
                }
            }

            throw new TranslationEngineException("Translation failed after maximum retries");
        }

        private async Task<string> SendTranslationRequestAsync(string text, string fromLang, string toLang)
        {
            var requestData = new Dictionary<string, string>
            {
                { "q", text },
                { "source", fromLang },
                { "target", toLang }
            };

            var content = new FormUrlEncodedContent(requestData);
            var response = await _httpClient.PostAsync($"{_config.BaseUrl}/translate", content);

            response.EnsureSuccessStatusCode();

            var jsonResponse = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<TranslationResponse>(jsonResponse);

            return result?.TranslatedText ?? throw new TranslationEngineException("Invalid response format");
        }

        private async Task LoadSupportedLanguagesAsync()
        {
            var response = await _httpClient.GetAsync($"{_config.BaseUrl}/languages");
            response.EnsureSuccessStatusCode();

            var jsonResponse = await response.Content.ReadAsStringAsync();
            var languages = JsonSerializer.Deserialize<List<LanguageInfo>>(jsonResponse);

            SupportedLanguages = languages?.Select(l => l.Code) ?? new List<string>();
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }

    public class TranslationConfig
    {
        public string BaseUrl { get; set; }
        public int TimeoutSeconds { get; set; } = 10;
    }

    public class TranslationResponse
    {
        [System.Text.Json.Serialization.JsonPropertyName("translatedText")]
        public string TranslatedText { get; set; }
    }

    public class LanguageInfo
    {
        public string Code { get; set; }
        public string Name { get; set; }
    }

    public class TranslationEngineException : Exception
    {
        public TranslationEngineException(string message) : base(message) { }
        public TranslationEngineException(string message, Exception innerException)
            : base(message, innerException) { }
    }
}