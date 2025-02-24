using System;

namespace GameTranslationOverlay.Core.Translation.Exceptions
{
    public class TranslationEngineException : Exception
    {
        public TranslationEngineException(string message) : base(message)
        {
        }

        public TranslationEngineException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }

    public class TranslationServerException : TranslationEngineException
    {
        public TranslationServerException(string message) : base(message)
        {
        }

        public TranslationServerException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }

    public class UnsupportedLanguageException : TranslationEngineException
    {
        public string LanguageCode { get; }

        public UnsupportedLanguageException(string languageCode)
            : base($"Language '{languageCode}' is not supported.")
        {
            LanguageCode = languageCode;
        }
    }

    public class ConnectionException : TranslationEngineException
    {
        public ConnectionException(string message) : base(message)
        {
        }

        public ConnectionException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}