using System;

namespace GameTranslationOverlay.Core.Diagnostics
{
    /// <summary>
    /// アプリケーションのロギング機能を提供するインターフェース
    /// </summary>
    public interface ILogger
    {
        /// <summary>
        /// デバッグレベルのログを記録
        /// </summary>
        /// <param name="message">ログメッセージ</param>
        void LogDebug(string message);

        /// <summary>
        /// デバッグレベルのログをカテゴリ付きで記録
        /// </summary>
        /// <param name="category">ログカテゴリ</param>
        /// <param name="message">ログメッセージ</param>
        void LogDebug(string category, string message);

        /// <summary>
        /// 情報レベルのログを記録
        /// </summary>
        /// <param name="message">ログメッセージ</param>
        void LogInfo(string message);

        /// <summary>
        /// 警告レベルのログを記録
        /// </summary>
        /// <param name="message">ログメッセージ</param>
        void LogWarning(string message);

        /// <summary>
        /// エラーレベルのログを記録
        /// </summary>
        /// <param name="message">ログメッセージ</param>
        void LogError(string message);

        /// <summary>
        /// 例外を含むエラーレベルのログを記録
        /// </summary>
        /// <param name="message">ログメッセージ</param>
        /// <param name="exception">例外オブジェクト</param>
        void LogError(string message, Exception exception);
    }
}