using System;

namespace GameTranslationOverlay.Core.Translation.Exceptions
{
    // 既存の例外階層と競合しないための単純な例外クラス
    public class TranslationException : Exception
    {
        public TranslationException(string message) : base(message)
        {
        }

        public TranslationException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}