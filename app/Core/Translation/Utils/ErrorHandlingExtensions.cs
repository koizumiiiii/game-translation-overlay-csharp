using System;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Net.Http;
using GameTranslationOverlay.Core.Translation.Exceptions;

namespace GameTranslationOverlay.Core.Translation.Utils
{
    /// <summary>
    /// エラーハンドリングのための拡張メソッドを提供するクラス
    /// </summary>
    public static class ErrorHandlingExtensions
    {
        /// <summary>
        /// 再試行処理を簡単に実装するための拡張メソッド
        /// </summary>
        /// <typeparam name="T">非同期操作の戻り値の型</typeparam>
        /// <param name="operation">実行する非同期操作</param>
        /// <param name="maxRetries">最大再試行回数</param>
        /// <param name="onRetry">再試行時のコールバック</param>
        /// <returns>操作の結果</returns>
        public static async Task<T> WithRetry<T>(
            this Func<Task<T>> operation,
            int maxRetries = 3,
            Func<Exception, int, Task> onRetry = null)
        {
            Exception lastException = null;

            for (int attempt = 0; attempt <= maxRetries; attempt++)
            {
                try
                {
                    // 実際の処理を実行
                    return await operation();
                }
                catch (Exception ex)
                {
                    lastException = ex;

                    // 最大再試行回数に達したら例外をスロー
                    if (attempt == maxRetries)
                        break;

                    // 再試行前のコールバックを実行
                    if (onRetry != null)
                        await onRetry(ex, attempt);
                    else
                    {
                        // 指数バックオフによる待機
                        TimeSpan delay = TimeSpan.FromSeconds(Math.Pow(2, attempt));
                        Debug.WriteLine($"Retry {attempt + 1}/{maxRetries} after {delay.TotalSeconds}s due to: {ex.Message}");
                        await Task.Delay(delay);
                    }
                }
            }

            // すべての再試行が失敗した場合
            Debug.WriteLine($"All {maxRetries} retries failed with error: {lastException.Message}");
            throw new RetryFailedException($"操作を {maxRetries} 回試みましたが失敗しました。", lastException);
        }

        /// <summary>
        /// ユーザーフレンドリーなエラーメッセージを生成
        /// </summary>
        /// <param name="ex">発生した例外</param>
        /// <returns>ユーザーフレンドリーなエラーメッセージ</returns>
        public static string GetUserFriendlyErrorMessage(this Exception ex)
        {
            if (ex is TranslationException || ex is RetryFailedException)
                return ex.Message;

            if (ex is HttpRequestException)
                return "翻訳サーバーに接続できませんでした。ネットワーク接続を確認してください。";

            if (ex is TimeoutException)
                return "翻訳処理がタイムアウトしました。しばらく時間をおいて再試行してください。";

            // OCR関連のエラー
            if (ex.Message.Contains("Error in boxClipToRectangle") ||
                ex.Message.Contains("Error in pixScanForForeground"))
                return "テキスト認識エラー: 選択した領域内にテキストが見つかりませんでした。";

            // その他のエラー
            return "処理中にエラーが発生しました。" +
                   (Debug.Listeners.Count > 0 ? "詳細はログを確認してください。" : string.Empty);
        }
    }

    /// <summary>
    /// 再試行処理が失敗した場合にスローされる例外
    /// </summary>
    public class RetryFailedException : Exception
    {
        public RetryFailedException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}