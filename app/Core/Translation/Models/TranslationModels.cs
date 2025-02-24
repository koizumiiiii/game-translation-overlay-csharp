namespace GameTranslationOverlay.Core.Translation.Models
{
    public class LanguageInfo
    {
        public string Code { get; set; }
        public string Name { get; set; }
    }

    internal class LibreTranslateRequest
    {
        public string Q { get; set; }
        public string Source { get; set; }
        public string Target { get; set; }
    }

    internal class LibreTranslateResponse
    {
        public string TranslatedText { get; set; }
    }

    public class TranslationResult
    {
        public string OriginalText { get; set; }
        public string TranslatedText { get; set; }
        public string SourceLanguage { get; set; }
        public string TargetLanguage { get; set; }
        public bool IsFromCache { get; set; }
    }
}