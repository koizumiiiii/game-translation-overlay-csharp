using System;
using System.Threading.Tasks;
using System.Diagnostics;
using GameTranslationOverlay.Core.Translation.Interfaces;

namespace GameTranslationOverlay.Core.Translation.Services
{
    public class TranslationManager
    {
        private readonly ITranslationEngine translationEngine;
        private bool isInitialized = false;

        public TranslationManager(ITranslationEngine translationEngine)
        {
            this.translationEngine = translationEngine;
        }

        public async Task InitializeAsync()
        {
            try
            {
                await translationEngine.InitializeAsync();
                isInitialized = true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Translation initialization error: {ex.Message}");
                throw;
            }
        }

        public async Task<string> TranslateAsync(string text, string fromLang, string toLang)
        {
            if (!isInitialized)
            {
                await InitializeAsync();
            }

            try
            {
                return await translationEngine.TranslateAsync(text, fromLang, toLang);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Translation error: {ex.Message}");
                return $"翻訳エラー: {ex.Message}";
            }
        }
    }
}