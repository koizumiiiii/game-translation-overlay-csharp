using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;

namespace GameTranslationOverlay.Core.Translation.Services
{
    /// <summary>
    /// 翻訳結果をキャッシュするクラス
    /// </summary>
    public class TranslationCache : IDisposable
    {
        // 重複定義を修正 - 1つのみに統合
        private Dictionary<string, CacheEntry> _cache = new Dictionary<string, CacheEntry>();
        private readonly int _maxEntries;
        private bool _isDisposed = false;

        /// <summary>
        /// コンストラクタ
        /// </summary>
        /// <param name="maxEntries">キャッシュの最大エントリ数</param>
        public TranslationCache(int maxEntries = 1000)
        {
            _maxEntries = maxEntries;
            Debug.WriteLine($"TranslationCache: Initialized with max entries = {maxEntries}");
        }

        /// <summary>
        /// キャッシュされた翻訳を取得
        /// </summary>
        /// <param name="sourceText">翻訳元のテキスト</param>
        /// <param name="sourceLang">翻訳元の言語</param>
        /// <param name="targetLang">翻訳先の言語</param>
        /// <returns>キャッシュされた翻訳（存在しない場合はnull）</returns>
        public string GetTranslation(string sourceText, string sourceLang, string targetLang)
        {
            // 破棄状態チェックを追加
            CheckDisposed();

            string key = GenerateKey(sourceText, sourceLang, targetLang);

            lock (_cache)
            {
                if (_cache.TryGetValue(key, out CacheEntry entry))
                {
                    // 最終アクセス時間を更新（LRU用）
                    entry.LastAccessed = DateTime.Now;
                    return entry.TranslatedText;
                }
            }

            return null;
        }

        /// <summary>
        /// 翻訳をキャッシュに追加
        /// </summary>
        /// <param name="sourceText">翻訳元のテキスト</param>
        /// <param name="translatedText">翻訳されたテキスト</param>
        /// <param name="sourceLang">翻訳元の言語</param>
        /// <param name="targetLang">翻訳先の言語</param>
        public void AddTranslation(string sourceText, string translatedText, string sourceLang, string targetLang)
        {
            // 破棄状態チェックを追加
            CheckDisposed();

            if (string.IsNullOrWhiteSpace(sourceText) || string.IsNullOrWhiteSpace(translatedText))
            {
                return;
            }

            string key = GenerateKey(sourceText, sourceLang, targetLang);

            lock (_cache)
            {
                // キャッシュがいっぱいの場合は、最も古いエントリを削除
                if (_cache.Count >= _maxEntries && !_cache.ContainsKey(key))
                {
                    RemoveOldestEntry();
                }

                // 新しいエントリを追加または更新
                _cache[key] = new CacheEntry
                {
                    SourceText = sourceText,
                    TranslatedText = translatedText,
                    SourceLang = sourceLang,
                    TargetLang = targetLang,
                    LastAccessed = DateTime.Now
                };
            }
        }

        /// <summary>
        /// キャッシュをクリア
        /// </summary>
        public void Clear()
        {
            CheckDisposed();

            lock (_cache)
            {
                _cache.Clear();
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
                    // キャッシュのクリア
                    try
                    {
                        lock (_cache)
                        {
                            _cache.Clear();
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error during TranslationCache disposal: {ex.Message}");
                    }
                }

                _isDisposed = true;
            }
        }

        private void CheckDisposed()
        {
            if (_isDisposed)
            {
                throw new ObjectDisposedException(nameof(TranslationCache));
            }
        }

        /// <summary>
        /// キャッシュのエントリ数を取得
        /// </summary>
        public int Count
        {
            get
            {
                CheckDisposed();

                lock (_cache)
                {
                    return _cache.Count;
                }
            }
        }

        /// <summary>
        /// 最も古いエントリを削除（LRU）
        /// </summary>
        private void RemoveOldestEntry()
        {
            if (_cache.Count == 0)
            {
                return;
            }

            // 最終アクセス時間が最も古いエントリを見つける
            var oldestEntry = _cache.OrderBy(kv => kv.Value.LastAccessed).First();
            _cache.Remove(oldestEntry.Key);
            Debug.WriteLine($"TranslationCache: Removed oldest entry '{oldestEntry.Value.SourceText?.Substring(0, Math.Min(20, oldestEntry.Value.SourceText?.Length ?? 0))}...'");
        }

        /// <summary>
        /// キャッシュキーを生成
        /// </summary>
        private string GenerateKey(string sourceText, string sourceLang, string targetLang)
        {
            // null チェックを追加して NullReferenceException を防止
            if (string.IsNullOrEmpty(sourceText))
            {
                return $"{sourceLang}|{targetLang}|";
            }

            return $"{sourceLang}|{targetLang}|{sourceText.Trim()}";
        }

        /// <summary>
        /// キャッシュエントリクラス
        /// </summary>
        private class CacheEntry
        {
            public string SourceText { get; set; }
            public string TranslatedText { get; set; }
            public string SourceLang { get; set; }
            public string TargetLang { get; set; }
            public DateTime LastAccessed { get; set; }
        }
    }
}