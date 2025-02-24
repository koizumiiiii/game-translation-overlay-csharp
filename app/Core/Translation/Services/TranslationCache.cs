using System;
using System.Collections.Concurrent;
using GameTranslationOverlay.Core.Translation.Configuration;
using GameTranslationOverlay.Core.Translation.Interfaces;

namespace GameTranslationOverlay.Core.Translation.Services
{
    public class TranslationCache : ITranslationCache
    {
        private readonly ConcurrentDictionary<string, CacheEntry> _cache;
        private readonly TranslationConfig _config;

        public TranslationCache(TranslationConfig config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _cache = new ConcurrentDictionary<string, CacheEntry>();
        }

        public string GetTranslation(string key)
        {
            if (_cache.TryGetValue(key, out var entry))
            {
                if (DateTime.UtcNow - entry.Timestamp <= _config.CacheExpiration)
                {
                    return entry.Translation;
                }

                _cache.TryRemove(key, out var _);
            }

            return null;
        }

        public void SetTranslation(string key, string translation)
        {
            var entry = new CacheEntry
            {
                Translation = translation,
                Timestamp = DateTime.UtcNow
            };

            _cache.AddOrUpdate(key, entry, (k, v) => entry);

            if (_cache.Count > _config.CacheSize)
            {
                CleanupCache();
            }
        }

        public void Clear()
        {
            _cache.Clear();
        }

        private void CleanupCache()
        {
            var expiredTime = DateTime.UtcNow - _config.CacheExpiration;
            foreach (var key in _cache.Keys)
            {
                if (_cache.TryGetValue(key, out var entry) && entry.Timestamp < expiredTime)
                {
                    _cache.TryRemove(key, out var _);
                }
            }
        }

        private class CacheEntry
        {
            public string Translation { get; set; }
            public DateTime Timestamp { get; set; }
        }
    }
}