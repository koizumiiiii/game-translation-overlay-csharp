using System;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Collections.Generic;
using GameTranslationOverlay.Core.Translation.Interfaces;
using GameTranslationOverlay.Core.Translation.Exceptions;
using System.Linq;
using GameTranslationOverlay.Core.Licensing;

namespace GameTranslationOverlay.Core.Translation.Services
{
    public enum TranslationEngineType
    {
        Libre,
        AI
    }

    public class TranslationManager : IDisposable
    {
        private ITranslationEngine _translationEngine;
        private TranslationCache _translationCache;
        private bool _isInitialized = false;
        private string _preferredTargetLanguage = "ja"; // デフォルトの翻訳先言語
        private bool _useAutoDetect = true; // 言語自動検出の有効/無効
        private bool _isDisposed = false;

        public TranslationManager(ITranslationEngine translationEngine)
        {
            _translationEngine = translationEngine;
            _translationCache = new TranslationCache(1000); // 1000エントリまでキャッシュ
        }

        public async Task InitializeAsync()
        {
            CheckDisposed();

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
            CheckDisposed();

            if (!_useAutoDetect)
            {
                // 自動検出が無効の場合はデフォルト言語ペアを使用
                var defaultPair = LanguageManager.GetDefaultLanguagePair();
                return await TranslateAsync(text, defaultPair.sourceLang, defaultPair.targetLang);
            }

            // 混合言語テキストかどうかをチェック
            bool isMixed = LanguageManager.IsMixedLanguage(text);
            string detectedLang;

            if (isMixed)
            {
                // 混合言語の場合は主要言語を使用
                detectedLang = LanguageManager.GetPrimaryLanguage(text);
                Debug.WriteLine($"TranslateWithAutoDetect: Mixed language detected, using primary language: {detectedLang}");
            }
            else
            {
                // 通常の検出
                detectedLang = LanguageManager.DetectLanguage(text);
                Debug.WriteLine($"TranslateWithAutoDetect: Detected language: {detectedLang}");
            }

            // 翻訳先言語の決定
            string targetLang = detectedLang == _preferredTargetLanguage ?
                (detectedLang == "ja" ? "en" : "ja") : _preferredTargetLanguage;

            Debug.WriteLine($"TranslateWithAutoDetect: Translating from {detectedLang} to {targetLang}");
            return await TranslateAsync(text, detectedLang, targetLang);
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
            CheckDisposed();

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
                // 言語の再検証（言語コードが正しいことを確認）
                fromLang = ValidateLanguageCode(fromLang);
                toLang = ValidateLanguageCode(toLang);

                // 同じ言語の場合はそのまま返す
                if (fromLang == toLang)
                {
                    Debug.WriteLine($"TranslateAsync: Source and target languages are the same ({fromLang}), skipping translation");
                    return text;
                }

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
        /// 言語コードの検証と正規化
        /// </summary>
        /// <param name="langCode">検証する言語コード</param>
        /// <returns>正規化された言語コード</returns>
        private string ValidateLanguageCode(string langCode)
        {
            // サポートされている言語コードか確認
            if (LanguageManager.SupportedLanguages.Contains(langCode))
            {
                return langCode;
            }

            // サポートされていない場合はデフォルト言語を返す
            Debug.WriteLine($"ValidateLanguageCode: Unsupported language code '{langCode}', defaulting to 'en'");
            return "en";
        }

        /// <summary>
        /// 優先的な翻訳先言語を設定する
        /// </summary>
        /// <param name="languageCode">言語コード</param>
        public void SetPreferredTargetLanguage(string languageCode)
        {
            CheckDisposed();

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
        /// 現在の優先翻訳先言語を取得
        /// </summary>
        /// <returns>言語コード</returns>
        public string GetPreferredTargetLanguage()
        {
            CheckDisposed();
            return _preferredTargetLanguage;
        }

        /// <summary>
        /// 言語自動検出の有効/無効を設定
        /// </summary>
        /// <param name="enable">有効にする場合はtrue</param>
        public void SetAutoDetect(bool enable)
        {
            CheckDisposed();
            _useAutoDetect = enable;
            Debug.WriteLine($"TranslationManager: Auto-detect set to {enable}");
        }

        /// <summary>
        /// 言語自動検出が有効かどうかを取得
        /// </summary>
        /// <returns>有効の場合はtrue</returns>
        public bool IsAutoDetectEnabled()
        {
            CheckDisposed();
            return _useAutoDetect;
        }

        /// <summary>
        /// 翻訳エンジンを設定する
        /// </summary>
        /// <param name="engine">使用する翻訳エンジン</param>
        public void SetTranslationEngine(ITranslationEngine engine)
        {
            CheckDisposed();

            // 既存のエンジンが IDisposable を実装している場合は解放
            if (_translationEngine is IDisposable disposableEngine)
            {
                try
                {
                    Debug.WriteLine("TranslationManager: 既存の翻訳エンジンを解放します");
                    disposableEngine.Dispose();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"TranslationManager: 翻訳エンジン解放中にエラー: {ex.Message}");
                }
            }

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
            CheckDisposed();
            return _translationEngine;
        }

        /// <summary>
        /// 翻訳エンジンをタイプに基づいて取得または作成
        /// </summary>
        public ITranslationEngine GetOrCreateEngine(TranslationEngineType engineType)
        {
            CheckDisposed();

            switch (engineType)
            {
                case TranslationEngineType.AI:
                    // AI翻訳が使用可能か確認
                    if (!LicenseManager.Instance.HasFeature(PremiumFeature.AiTranslation))
                    {
                        Debug.WriteLine("AI translation feature is not available with current license");
                        return null;
                    }

                    // AIエンジンを作成して返す
                    return new AITranslationEngine();

                case TranslationEngineType.Libre:
                default:
                    // 標準の翻訳エンジンを返す
                    return new LibreTranslateEngine("http://localhost:5000");
            }
        }

        /// <summary>
        /// キャッシュをクリアする
        /// </summary>
        public void ClearCache()
        {
            CheckDisposed();
            _translationCache = new TranslationCache(1000);
            Debug.WriteLine("TranslationManager: Cache cleared");
        }

        /// <summary>
        /// キャッシュの統計情報を取得
        /// </summary>
        /// <returns>キャッシュサイズ</returns>
        public int GetCacheSize()
        {
            CheckDisposed();
            return _translationCache?.Count ?? 0;
        }

        #region IDisposable の実装

        /// <summary>
        /// リソースを解放します
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// リソースを解放します
        /// </summary>
        /// <param name="disposing">マネージドリソースも解放する場合は true</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!_isDisposed)
            {
                if (disposing)
                {
                    try
                    {
                        Debug.WriteLine("TranslationManager: リソースを解放しています...");

                        // キャッシュのクリア
                        if (_translationCache != null)
                        {
                            _translationCache.Clear();

                            // TranslationCacheがIDisposableを実装している場合は解放
                            if (_translationCache is IDisposable disposableCache)
                            {
                                disposableCache.Dispose();
                            }

                            _translationCache = null;
                        }

                        // 翻訳エンジンの解放
                        if (_translationEngine is IDisposable disposableEngine)
                        {
                            Debug.WriteLine("TranslationManager: 翻訳エンジンを解放しています...");
                            disposableEngine.Dispose();
                        }

                        _translationEngine = null;

                        Debug.WriteLine("TranslationManager: すべてのリソースを解放しました");
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"TranslationManager: リソース解放中にエラーが発生しました: {ex.Message}");
                    }
                }

                _isDisposed = true;
            }
        }

        /// <summary>
        /// オブジェクトが破棄されていないことを確認します
        /// </summary>
        private void CheckDisposed()
        {
            if (_isDisposed)
            {
                throw new ObjectDisposedException(nameof(TranslationManager), "翻訳マネージャーは既に破棄されています");
            }
        }

        #endregion
    }
}