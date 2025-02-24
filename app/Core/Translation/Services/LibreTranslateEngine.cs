using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Linq;

namespace GameTranslationOverlay.Core.Translation
{
    public class LibreTranslateEngine : ITranslationEngine, IDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly string _baseUrl;
        private readonly JsonSerializerOptions _jsonOptions;
        private readonly Settings _settings;
        private bool _isInitialized;
        private bool _disposed;
        private ISet<string> _supportedLanguages;

        public bool IsAvailable => _isInitialized && !_disposed;

        public IEnumerable<string> SupportedLanguages => _supportedLanguages ?? new HashSet<string>();

        public class Settings
        {
            public string BaseUrl { get; set; } = "http://localhost:5000";
            public int Timeout { get; set; } = 10000;
            public int MaxRetries { get; set; } = 3;
            public int RetryDelay { get; set; } = 1000;
        }

        private class TranslationRequest
        {
            public string q { get; set; }
            public string source { get; set; }
            public string target { get; set; }
        }

        private class TranslationResponse
        {
            public string translatedText { get; set; }
        }

        private class LanguageResponse
        {
            public string code { get; set; }
            public string name { get; set; }
        }

        public LibreTranslateEngine(Settings settings = null)
        {
            _settings = settings ?? new Settings();
            _baseUrl = _settings.BaseUrl.TrimEnd('/');

            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromMilliseconds(_settings.Timeout)
            };

            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            _supportedLanguages = new HashSet<string>();
            Debug.WriteLine($"LibreTranslateEngine initialized with base URL: {_baseUrl}");
        }

        public async Task InitializeAsync()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(LibreTranslateEngine));

            try
            {
                // サーバーの状態確認
                await CheckServerHealthAsync();

                // 対応言語の取得
                await FetchSupportedLanguagesAsync();

                _isInitialized = true;
                Debug.WriteLine("LibreTranslateEngine initialization completed successfully");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"LibreTranslateEngine initialization failed: {ex.Message}");
                _isInitialized = false;
                throw new InvalidOperationException("Failed to initialize translation engine.", ex);
            }
        }

        private async Task CheckServerHealthAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync($"{_baseUrl}/languages");
                response.EnsureSuccessStatusCode();
                Debug.WriteLine("Server health check passed");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Server health check failed: {ex.Message}");
                throw new InvalidOperationException("Translation server is not available.", ex);
            }
        }

        private async Task FetchSupportedLanguagesAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync($"{_baseUrl}/languages");
                response.EnsureSuccessStatusCode();

                var content = await response.Content.ReadAsStringAsync();
                var languages = JsonSerializer.Deserialize<List<LanguageResponse>>(content, _jsonOptions);

                _supportedLanguages = new HashSet<string>(
                    languages.Select(l => l.code),
                    StringComparer.OrdinalIgnoreCase
                );

                Debug.WriteLine($"Fetched supported languages: {string.Join(", ", _supportedLanguages)}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to fetch supported languages: {ex.Message}");
                throw new InvalidOperationException("Failed to get supported languages.", ex);
            }
        }

        public async Task<string> TranslateAsync(string text, string fromLang, string toLang)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(LibreTranslateEngine));

            if (!_isInitialized)
                throw new InvalidOperationException("Translation engine is not initialized.");

            if (string.IsNullOrWhiteSpace(text))
                return string.Empty;

            ValidateLanguageCodes(fromLang, toLang);

            var retryCount = 0;
            while (retryCount < _settings.MaxRetries)
            {
                try
                {
                    return await ExecuteTranslationAsync(text, fromLang, toLang);
                }
                catch (HttpRequestException ex)
                {
                    Debug.WriteLine($"Translation attempt {retryCount + 1} failed: {ex.Message}");

                    if (retryCount == _settings.MaxRetries - 1)
                        throw new InvalidOperationException("Translation failed after maximum retries.", ex);

                    await Task.Delay(_settings.RetryDelay);
                    retryCount++;
                }
            }

            throw new InvalidOperationException("Translation failed unexpectedly.");
        }

        private async Task<string> ExecuteTranslationAsync(string text, string fromLang, string toLang)
        {
            var request = new TranslationRequest
            {
                q = text,
                source = fromLang,
                target = toLang
            };

            var json = JsonSerializer.Serialize(request);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync($"{_baseUrl}/translate", content);
            response.EnsureSuccessStatusCode();

            var responseContent = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<TranslationResponse>(responseContent, _jsonOptions);

            Debug.WriteLine($"Translation successful: {text} -> {result.translatedText}");
            return result.translatedText;
        }

        private void ValidateLanguageCodes(string fromLang, string toLang)
        {
            if (!_supportedLanguages.Contains(fromLang))
                throw new ArgumentException($"Source language '{fromLang}' is not supported.");

            if (!_supportedLanguages.Contains(toLang))
                throw new ArgumentException($"Target language '{toLang}' is not supported.");
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
                return;

            if (disposing)
            {
                _httpClient?.Dispose();
                Debug.WriteLine("LibreTranslateEngine disposed");
            }

            _disposed = true;
        }
    }
}