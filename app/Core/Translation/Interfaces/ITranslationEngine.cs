using System.Collections.Generic;
using System.Threading.Tasks;
using GameTranslationOverlay.Core.Translation.Models;

namespace GameTranslationOverlay.Core.Translation.Interfaces
{
    public interface ITranslationEngine
    {
        Task<string> TranslateAsync(string text, string sourceLang, string targetLang);
        Task<bool> IsAvailableAsync();
        Task<IEnumerable<LanguageInfo>> GetSupportedLanguagesAsync();
    }

    public interface ITranslationCache
    {
        string GetTranslation(string key);
        void SetTranslation(string key, string translation);
        void Clear();
    }
}