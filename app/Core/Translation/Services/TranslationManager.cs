using System;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Collections.Generic;
using GameTranslationOverlay.Core.Translation.Interfaces;
using GameTranslationOverlay.Core.Translation.Exceptions;
using System.Linq;

namespace GameTranslationOverlay.Core.Translation.Services
{
    public enum TranslationEngineType
    {
        Libre,
        AI
    }

    public class TranslationManager
    {
        private ITranslationEngine _translationEngine;
        private TranslationCache _translationCache;
        private bool _isInitialized = false;
        private string _preferredTargetLanguage = "ja"; // デフォルトの翻訳先言語

        public TranslationManager(ITranslationEngine translationEngine)
        {
            _translationEngine = translationEngine;
            _translationCache = new TranslationCache(1000); // 1000エントリまでキャッシュ
        }

        public async Task InitializeAsync()
        {
            try
            {
                await _translationEngine.InitializeAsync();
                _isInitialized = true;
                Debug.WriteLine("TranslationManager: Initialized successfully");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"TranslationManager: Initialization error: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// テキストを翻訳する（言語を自動検出）
        /// </summary>
        /// <param name="text">翻訳するテキスト</param>
        /// <returns>翻訳結果</returns>
        public async Task<string> TranslateWithAutoDetectAsync(string text)
        {
            // 言語を自動検出して翻訳
            var (sourceLang, targetLang) = LanguageManager.GetOptimalTranslationPair(text, _preferredTargetLanguage);
            return await TranslateAsync(text, sourceLang, targetLang);
        }

        /// <summary>
        /// テキストを翻訳する
        /// </summary>
        /// <param name="text">翻訳するテキスト</param>
        /// <param name="fromLang">翻訳元の言語</param>
        /// <param name="toLang">翻訳先の言語</param>
        /// <returns>翻訳結果</returns>
        public async Task<string> TranslateAsync(string text, string fromLang, string toLang)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                Debug.WriteLine("TranslateAsync: Empty text provided");
                return string.Empty;
            }

            if (!_isInitialized)
            {
                try
                {
                    Debug.WriteLine("TranslateAsync: Not initialized, attempting to initialize");
                    await InitializeAsync();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"TranslateAsync: Initialization failed: {ex.Message}");
                    return $"翻訳エンジンの初期化に失敗しました: {ex.Message}";
                }
            }

            try
            {
                // キャッシュから翻訳結果を取得
                string cachedTranslation = _translationCache.GetTranslation(text, fromLang, toLang);
                if (cachedTranslation != null)
                {
                    Debug.WriteLine($"TranslateAsync: Cache hit for '{text.Substring(0, Math.Min(20, text.Length))}...'");
                    return cachedTranslation;
                }

                // キャッシュにない場合は翻訳を実行
                Debug.WriteLine($"TranslateAsync: Cache miss, translating '{text.Substring(0, Math.Min(20, text.Length))}...' from {fromLang} to {toLang}");
                string translatedText = await _translationEngine.TranslateAsync(text, fromLang, toLang);

                // 結果をキャッシュに保存
                if (!string.IsNullOrWhiteSpace(translatedText))
                {
                    _translationCache.AddTranslation(text, translatedText, fromLang, toLang);
                }

                return translatedText;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"TranslateAsync: Translation error: {ex.Message}");

                // ユーザーフレンドリーなエラーメッセージを返す
                if (ex is TranslationException)
                {
                    return $"翻訳エラー: {ex.Message}";
                }
                else if (ex is System.Net.Http.HttpRequestException)
                {
                    return "翻訳サーバーに接続できませんでした。ネットワーク接続を確認してください。";
                }
                else
                {
                    return $"翻訳処理中にエラーが発生しました: {ex.Message}";
                }
            }
        }

        /// <summary>
        /// 優先的な翻訳先言語を設定する
        /// </summary>
        /// <param name="languageCode">言語コード</param>
        public void SetPreferredTargetLanguage(string languageCode)
        {
            if (LanguageManager.SupportedLanguages.Contains(languageCode))
            {
                _preferredTargetLanguage = languageCode;
                Debug.WriteLine($"TranslationManager: Preferred target language set to {languageCode}");
            }
            else
            {
                Debug.WriteLine($"TranslationManager: Unsupported language code {languageCode}");
                throw new ArgumentException($"サポートされていない言語コードです: {languageCode}", nameof(languageCode));
            }
        }

        /// <summary>
        /// 翻訳エンジンを設定する
        /// </summary>
        /// <param name="engine">使用する翻訳エンジン</param>
        public void SetTranslationEngine(ITranslationEngine engine)
        {
            _translationEngine = engine ?? throw new ArgumentNullException(nameof(engine));
            _isInitialized = false; // 新しいエンジンで再初期化が必要
            Debug.WriteLine($"TranslationManager: Translation engine changed to {engine.GetType().Name}");
        }

        /// <summary>
        /// 現在の翻訳エンジンを取得
        /// </summary>
        /// <returns>現在使用中の翻訳エンジン</returns>
        public ITranslationEngine GetCurrentEngine()
        {
            return _translationEngine;
        }

        /// <summary>
        /// キャッシュをクリアする
        /// </summary>
        public void ClearCache()
        {
            _translationCache = new TranslationCache(1000);
            Debug.WriteLine("TranslationManager: Cache cleared");
        }
    }
}