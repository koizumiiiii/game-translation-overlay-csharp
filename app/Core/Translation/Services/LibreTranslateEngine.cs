using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using GameTranslationOverlay.Core.Translation.Interfaces;
using GameTranslationOverlay.Core.Translation.Models;
using GameTranslationOverlay.Core.Translation.Exceptions;

namespace GameTranslationOverlay.Core.Translation.Services
{
    public class LibreTranslateEngine : ITranslationEngine, IDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly string _baseUrl;
        private readonly JsonSerializerOptions _jsonOptions;
        private readonly Settings _settings;
        private readonly ITranslationCache _cache;
        private bool _isInitialized;
        private bool _disposed;
        private ISet<string> _supportedLanguages;
        private List<LanguageInfo> _languageInfos;

        public bool IsAvailable => _isInitialized && !_disposed;

        public IEnumerable<string> SupportedLanguages => _supportedLanguages ?? new HashSet<string>();

        public class Settings
        {
            public string BaseUrl { get; set; } = "http://localhost:5000";
            public int Timeout { get; set; } = 10000;
            public int MaxRetries { get; set; } = 3;
            public int RetryDelay { get; set; } = 1000;
        }

        public LibreTranslateEngine(Settings settings = null, ITranslationCache cache = null)
        {
            _settings = settings ?? new Settings();
            _cache = cache;
            _baseUrl = _settings.BaseUrl.TrimEnd('/');

            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromMilliseconds(_settings.Timeout)
            };

            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            _supportedLanguages = new HashSet<string>();
            _languageInfos = new List<LanguageInfo>();
            Debug.WriteLine($"LibreTranslateEngine initialized with base URL: {_baseUrl}");
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

            // テキストの前処理
            text = PreprocessText(text);
            Debug.WriteLine($"Preprocessed text for translation: {text}");

            // キャッシュのチェック
            if (_cache != null)
            {
                var cacheKey = $"{fromLang}:{toLang}:{text}";
                var cachedTranslation = _cache.GetTranslation(cacheKey);
                if (cachedTranslation != null)
                {
                    Debug.WriteLine("Translation found in cache");
                    return cachedTranslation;
                }
            }

            var retryCount = 0;
            while (retryCount < _settings.MaxRetries)
            {
                try
                {
                    var translation = await ExecuteTranslationAsync(text, fromLang, toLang);

                    // キャッシュに保存
                    if (_cache != null)
                    {
                        var cacheKey = $"{fromLang}:{toLang}:{text}";
                        _cache.SetTranslation(cacheKey, translation);
                    }

                    return translation;
                }
                catch (HttpRequestException ex)
                {
                    Debug.WriteLine($"Translation attempt {retryCount + 1} failed: {ex.Message}");

                    if (retryCount == _settings.MaxRetries - 1)
                        throw new ConnectionException("Translation failed after maximum retries.", ex);

                    await Task.Delay(_settings.RetryDelay);
                    retryCount++;
                }
            }

            throw new TranslationServerException("Translation failed unexpectedly.");
        }

        private string PreprocessText(string text)
        {
            try
            {
                // 改行を空白に置換
                text = text.Replace("\n", " ").Replace("\r", " ");

                // 連続する空白を単一の空白に
                text = Regex.Replace(text, @"\s+", " ");

                // キャメルケースやパスカルケースの単語間にスペースを挿入
                text = Regex.Replace(text, @"(?<!^)(?=[A-Z][a-z])|(?<!^)(?=[0-9])|(?<=[a-z])(?=[0-9])", " ");

                // URLは処理から除外（スペースを削除）
                text = Regex.Replace(text, @"https?://\S+", match => match.Value.Replace(" ", ""));

                // 最終的な整形
                text = text.Trim();

                Debug.WriteLine($"Text preprocessing result: {text}");
                return text;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Text preprocessing error: {ex.Message}");
                return text; // エラー時は元のテキストを返す
            }
        }

        private async Task<string> ExecuteTranslationAsync(string text, string fromLang, string toLang)
        {
            var request = new LibreTranslateRequest
            {
                Q = text,
                Source = fromLang,
                Target = toLang
            };

            var json = JsonSerializer.Serialize(request, _jsonOptions);
            Debug.WriteLine($"Translation request payload: {json}");

            var content = new StringContent(json, Encoding.UTF8, "application/json");

            try
            {
                var response = await _httpClient.PostAsync($"{_baseUrl}/translate", content);
                var responseContent = await response.Content.ReadAsStringAsync();
                Debug.WriteLine($"Translation response: {responseContent}");

                response.EnsureSuccessStatusCode();

                var result = JsonSerializer.Deserialize<LibreTranslateResponse>(responseContent, _jsonOptions);
                return result?.TranslatedText ?? text;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Translation execution error: {ex}");
                throw;
            }
        }

        public async Task InitializeAsync()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(LibreTranslateEngine));

            try
            {
                await CheckServerHealthAsync();
                await FetchSupportedLanguagesAsync();

                _isInitialized = true;
                Debug.WriteLine("LibreTranslateEngine initialization completed successfully");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"LibreTranslateEngine initialization failed: {ex.Message}");
                _isInitialized = false;
                throw new ConnectionException("Failed to initialize translation engine.", ex);
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
                throw new ConnectionException("Translation server is not available.", ex);
            }
        }

        private async Task FetchSupportedLanguagesAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync($"{_baseUrl}/languages");
                response.EnsureSuccessStatusCode();

                var content = await response.Content.ReadAsStringAsync();
                Debug.WriteLine($"Supported languages response: {content}");

                _languageInfos = JsonSerializer.Deserialize<List<LanguageInfo>>(content, _jsonOptions);
                _supportedLanguages = new HashSet<string>(
                    _languageInfos.Select(l => l.Code),
                    StringComparer.OrdinalIgnoreCase
                );

                Debug.WriteLine($"Fetched supported languages: {string.Join(", ", _supportedLanguages)}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to fetch supported languages: {ex.Message}");
                throw new TranslationServerException("Failed to get supported languages.", ex);
            }
        }

        private void ValidateLanguageCodes(string fromLang, string toLang)
        {
            if (!_supportedLanguages.Contains(fromLang))
                throw new UnsupportedLanguageException(fromLang);

            if (!_supportedLanguages.Contains(toLang))
                throw new UnsupportedLanguageException(toLang);
        }

        public Task<IEnumerable<LanguageInfo>> GetSupportedLanguagesAsync()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(LibreTranslateEngine));

            if (!_isInitialized)
                throw new InvalidOperationException("Translation engine is not initialized.");

            return Task.FromResult<IEnumerable<LanguageInfo>>(_languageInfos);
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