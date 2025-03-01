using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using GameTranslationOverlay.Core.Translation.Configuration;
using GameTranslationOverlay.Core.Translation.Interfaces;

namespace GameTranslationOverlay.Core.Translation.Services
{
    public class TranslationCache
    {
        private readonly Dictionary<string, CacheEntry> cache = new Dictionary<string, CacheEntry>();
        private readonly int maxEntries;

        public TranslationCache(int maxEntries = 100)
        {
            this.maxEntries = maxEntries;
        }

        public string GetTranslation(string text, string sourceLang, string targetLang)
        {
            string key = GenerateKey(text, sourceLang, targetLang);

            if (cache.TryGetValue(key, out CacheEntry entry))
            {
                entry.LastAccessed = DateTime.Now;
                return entry.TranslatedText;
            }

            return null;
        }

        public void AddTranslation(string text, string translatedText, string sourceLang, string targetLang)
        {
            if (string.IsNullOrWhiteSpace(text) || string.IsNullOrWhiteSpace(translatedText))
            {
                return;
            }

            string key = GenerateKey(text, sourceLang, targetLang);

            // キャッシュがいっぱいなら古いエントリを削除
            if (cache.Count >= maxEntries && !cache.ContainsKey(key))
            {
                RemoveOldestEntry();
            }

            cache[key] = new CacheEntry
            {
                SourceText = text,
                TranslatedText = translatedText,
                SourceLang = sourceLang,
                TargetLang = targetLang,
                LastAccessed = DateTime.Now
            };
        }

        private string GenerateKey(string text, string sourceLang, string targetLang)
        {
            return $"{sourceLang}|{targetLang}|{text}";
        }

        private void RemoveOldestEntry()
        {
            DateTime oldestTime = DateTime.MaxValue;
            string oldestKey = null;

            foreach (var pair in cache)
            {
                if (pair.Value.LastAccessed < oldestTime)
                {
                    oldestTime = pair.Value.LastAccessed;
                    oldestKey = pair.Key;
                }
            }

            if (oldestKey != null)
            {
                cache.Remove(oldestKey);
            }
        }

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