using System;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using GameTranslationOverlay.Properties;
using GameTranslationOverlay.Core.Diagnostics;
using GameTranslationOverlay.Core.Configuration;

namespace GameTranslationOverlay.Core.Security
{
    public class ApiKeyProtector
    {
        private static ApiKeyProtector _instance;
        private static readonly object _lock = new object();

        public static ApiKeyProtector Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                        {
                            _instance = new ApiKeyProtector();
                        }
                    }
                }
                return _instance;
            }
        }

        private ApiKeyProtector()
        {
            // シングルトンパターン
        }

        /// <summary>
        /// リソースファイルから暗号化されたAPIキーを取得し、復号化する
        /// </summary>
        public string GetDecryptedApiKey()
        {
            try
            {
                // リソースファイルから暗号化されたAPIキーを取得
                string encryptedKey = Resources.EncryptedOpenAIApiKey;

                if (string.IsNullOrEmpty(encryptedKey))
                {
                    string message = "Encrypted API key not found in resources";
                    Debug.WriteLine(message);
                    if (Logger.Instance != null)
                        Logger.Instance.LogWarning(message);
                    return string.Empty;
                }

                try
                {
                    // Base64デコード
                    byte[] encryptedBytes = Convert.FromBase64String(encryptedKey);

                    // Windows DPAPIを使用して復号化
                    byte[] decryptedBytes = ProtectedData.Unprotect(
                        encryptedBytes,
                        null,
                        DataProtectionScope.CurrentUser);

                    // バイト配列を文字列に変換
                    string apiKey = Encoding.UTF8.GetString(decryptedBytes);

                    // キーが取得できたことをログに記録
                    Debug.WriteLine("API key successfully decrypted");
                    return apiKey;
                }
                catch (FormatException fex)
                {
                    string message = $"Error decoding Base64 API key: {fex.Message}";
                    Debug.WriteLine(message);
                    if (Logger.Instance != null)
                        Logger.Instance.LogError(message);

                    return GetFallbackApiKey("OpenAI");
                }
                catch (CryptographicException cex)
                {
                    string message = $"Error decrypting API key: {cex.Message}";
                    Debug.WriteLine(message);
                    if (Logger.Instance != null)
                        Logger.Instance.LogError(message);

                    return GetFallbackApiKey("OpenAI");
                }
            }
            catch (Exception ex)
            {
                string message = $"Unexpected error retrieving API key: {ex.Message}";
                Debug.WriteLine(message);
                if (Logger.Instance != null)
                    Logger.Instance.LogError(message);
                return string.Empty;
            }
        }

        /// <summary>
        /// APIキーを暗号化して返す（設定に保存する際に使用）
        /// </summary>
        public string EncryptApiKey(string apiKey)
        {
            try
            {
                if (string.IsNullOrEmpty(apiKey))
                {
                    return string.Empty;
                }

                // 文字列をバイト配列に変換
                byte[] dataToEncrypt = Encoding.UTF8.GetBytes(apiKey);

                // Windows DPAPIを使用して暗号化
                byte[] encryptedData = ProtectedData.Protect(
                    dataToEncrypt,
                    null,
                    DataProtectionScope.CurrentUser);

                // Base64エンコード
                return Convert.ToBase64String(encryptedData);
            }
            catch (Exception ex)
            {
                string message = $"Error encrypting API key: {ex.Message}";
                Debug.WriteLine(message);
                if (Logger.Instance != null)
                    Logger.Instance.LogError(message);
                return string.Empty;
            }
        }

        /// <summary>
        /// フォールバックAPIキーの取得
        /// </summary>
        private string GetFallbackApiKey(string provider)
        {
            // デバッグモードでのみフォールバックキーを使用
            if (AppSettings.Instance != null && AppSettings.Instance.DebugModeEnabled)
            {
                Debug.WriteLine($"Using development fallback key for {provider} in debug mode");
                if (Logger.Instance != null)
                    Logger.Instance.LogWarning($"Using development fallback key for {provider} in debug mode");

                // デバッグモード用のAPIキー
                // 実際のAPIキーはここに記述せず、安全な方法で提供する必要があります
                return "";
            }

            return string.Empty;
        }
    }
}