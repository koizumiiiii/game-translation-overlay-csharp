using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace GameTranslationOverlay.Core.Translation.Services
{
    /// <summary>
    /// 翻訳結果をキャッシュするクラス
    /// </summary>
    public class TranslationCache
    {
        private readonly Dictionary<string, CacheEntry> _cache = new Dictionary<string, CacheEntry>();
        private readonly int _maxEntries;

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
            lock (_cache)
            {
                _cache.Clear();
                Debug.WriteLine("TranslationCache: Cache cleared");
            }
        }

        /// <summary>
        /// キャッシュのエントリ数を取得
        /// </summary>
        public int Count
        {
            get
            {
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