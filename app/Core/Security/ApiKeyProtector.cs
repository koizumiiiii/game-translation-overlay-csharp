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

                // デバッグ情報の追加
                Debug.WriteLine($"OpenAI暗号化キーサイズ: {encryptedKey.Length}バイト");
                Debug.WriteLine($"現在のユーザー: {Environment.UserName}");

                try
                {
                    // Base64デコード
                    byte[] encryptedBytes = Convert.FromBase64String(encryptedKey);

                    // Windows DPAPIを使用して復号化
                    byte[] decryptedBytes = ProtectedData.Unprotect(
                    encryptedBytes,
                    null,
                    DataProtectionScope.LocalMachine);

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
                DataProtectionScope.LocalMachine);

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

        public string GetDecryptedGeminiApiKey()
        {
            try
            {
                // リソースファイルから暗号化されたAPIキーを取得
                string encryptedKey = Resources.EncryptedGeminiApiKey;

                if (string.IsNullOrEmpty(encryptedKey))
                {
                    string message = "Encrypted Gemini API key not found in resources";
                    Debug.WriteLine(message);
                    if (Logger.Instance != null)
                        Logger.Instance.LogWarning(message);
                    return string.Empty;
                }

                // デバッグ情報の追加
                Debug.WriteLine($"Gemini暗号化キーサイズ: {encryptedKey.Length}バイト");
                Debug.WriteLine($"現在のユーザー: {Environment.UserName}");

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

                    // キー形式の検証（Gemini APIキーはAIzaで始まるはず）
                    if (!apiKey.StartsWith("AIza"))
                    {
                        string message = "Decrypted Gemini API key has invalid format (should start with 'AIza')";
                        Debug.WriteLine(message);
                        if (Logger.Instance != null)
                            Logger.Instance.LogWarning(message);

                        // 形式が正しくない場合でも一応返す（フォールバックに任せる）
                    }
                    else
                    {
                        // 正しい形式のキーが取得できたことをログに記録
                        Debug.WriteLine("Gemini API key successfully decrypted and validated");
                    }
                    return apiKey;
                }
                catch (FormatException fex)
                {
                    string message = $"Error decoding Base64 Gemini API key: {fex.Message}";
                    Debug.WriteLine(message);
                    if (Logger.Instance != null)
                        Logger.Instance.LogError(message);

                    return GetFallbackApiKey("Gemini");
                }
                catch (CryptographicException cex)
                {
                    string message = $"Error decrypting Gemini API key: {cex.Message}";
                    Debug.WriteLine(message);
                    if (Logger.Instance != null)
                        Logger.Instance.LogError(message);

                    return GetFallbackApiKey("Gemini");
                }
            }
            catch (Exception ex)
            {
                string message = $"Unexpected error retrieving Gemini API key: {ex.Message}";
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
            // デバッグモードのみフォールバックを提供
            if (AppSettings.Instance.DebugModeEnabled)
            {
                Debug.WriteLine($"Using development fallback key for {provider} in debug mode");

                // ここでより現実的なAPIキーを返す
                if (provider.Equals("OpenAI", StringComparison.OrdinalIgnoreCase))
                {
                    return "sk-test-openai-key-for-debugging";
                }
                else if (provider.Equals("Gemini", StringComparison.OrdinalIgnoreCase))
                {
                    // 実際のGemini APIキーの形式に合わせて修正
                    // 注意：これは実際のキーではなく、形式のみ模倣したもの
                    return "AIzaSyD1234567890abcdefghijklmnopqrstuvwxyz";
                }
            }

            return string.Empty;
        }
    }
}