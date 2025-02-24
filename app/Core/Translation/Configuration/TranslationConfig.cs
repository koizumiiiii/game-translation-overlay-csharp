using System;

namespace GameTranslationOverlay.Core.Translation.Configuration
{
    public class TranslationConfig
    {
        public string ServerUrl { get; set; } = "http://localhost:5000";
        public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(10);
        public int MaxRetries { get; set; } = 3;
        public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(1);
        public bool AutoDetectLanguage { get; set; } = true;
        public string DefaultSourceLanguage { get; set; } = "en";
        public string DefaultTargetLanguage { get; set; } = "ja";
        public int CacheSize { get; set; } = 1000;
        public TimeSpan CacheExpiration { get; set; } = TimeSpan.FromHours(24);
    }
}