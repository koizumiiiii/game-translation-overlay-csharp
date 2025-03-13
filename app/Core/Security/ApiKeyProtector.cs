// GameTranslationOverlay/Core/Security/ApiKeyProtector.cs
using System;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using GameTranslationOverlay.Properties;

namespace GameTranslationOverlay.Core.Security
{
    public class ApiKeyProtector
    {
        private static ApiKeyProtector _instance;

        public static ApiKeyProtector Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new ApiKeyProtector();
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
                    Debug.WriteLine("Encrypted API key not found in resources");
                    return string.Empty;
                }

                // Base64デコード
                byte[] encryptedBytes = Convert.FromBase64String(encryptedKey);

                // Windows DPAPIを使用して復号化
                byte[] decryptedBytes = ProtectedData.Unprotect(
                    encryptedBytes,
                    null,
                    DataProtectionScope.CurrentUser);

                // バイト配列を文字列に変換
                string apiKey = Encoding.UTF8.GetString(decryptedBytes);

                return apiKey;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error decrypting API key: {ex.Message}");
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
                Debug.WriteLine($"Error encrypting API key: {ex.Message}");
                return string.Empty;
            }
        }
    }
}