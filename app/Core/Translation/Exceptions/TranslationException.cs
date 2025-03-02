using System;

namespace GameTranslationOverlay.Core.Translation.Exceptions
{
    /// <summary>
    /// 翻訳処理中に発生するエラーを表す例外
    /// </summary>
    public class TranslationException : Exception
    {
        /// <summary>
        /// TranslationExceptionのコンストラクタ
        /// </summary>
        /// <param name="message">エラーメッセージ</param>
        public TranslationException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// TranslationExceptionのコンストラクタ
        /// </summary>
        /// <param name="message">エラーメッセージ</param>
        /// <param name="innerException">内部例外</param>
        public TranslationException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}