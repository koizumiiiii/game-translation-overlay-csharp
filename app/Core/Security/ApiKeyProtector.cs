using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using static System.Security.Cryptography.ProtectedData;

namespace GameTranslationOverlay.Core.Security
{
    /// <summary>
    /// APIキーなどの機密情報を保護するためのユーティリティクラス
    /// </summary>
    public class ApiKeyProtector
    {
        // 暗号化に使用する固定エントロピー
        // 注: 本番環境では別の安全な方法で管理することを推奨
        private static readonly byte[] _entropy = {
            0x43, 0x87, 0x23, 0x72, 0x45, 0xA7, 0xB2, 0xC1,
            0xD4, 0xE5, 0xF6, 0x19, 0x82, 0x73, 0x64, 0x35
        };

        /// <summary>
        /// 文字列を暗号化する
        /// </summary>
        /// <param name="plainText">暗号化する文字列</param>
        /// <returns>暗号化された文字列（Base64エンコード）</returns>
        public static string Encrypt(string plainText)
        {
            if (string.IsNullOrEmpty(plainText))
                return string.Empty;

            try
            {
                // 文字列をバイト配列に変換
                byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);

                // Windows Data Protection APIを使用して暗号化
                byte[] encryptedBytes = ProtectedData.Protect(
                    plainBytes,
                    _entropy,
                    DataProtectionScope.CurrentUser);

                // 暗号化されたバイト配列をBase64文字列に変換
                return Convert.ToBase64String(encryptedBytes);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Encryption error: {ex.Message}");
                return string.Empty;
            }
        }

        /// <summary>
        /// 暗号化された文字列を復号する
        /// </summary>
        /// <param name="encryptedText">暗号化された文字列（Base64エンコード）</param>
        /// <returns>復号された文字列</returns>
        public static string Decrypt(string encryptedText)
        {
            if (string.IsNullOrEmpty(encryptedText))
                return string.Empty;

            try
            {
                // Base64文字列をバイト配列に変換
                byte[] encryptedBytes = Convert.FromBase64String(encryptedText);

                // Windows Data Protection APIを使用して復号
                byte[] plainBytes = ProtectedData.Unprotect(
                    encryptedBytes,
                    _entropy,
                    DataProtectionScope.CurrentUser);

                // バイト配列を文字列に変換
                return Encoding.UTF8.GetString(plainBytes);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Decryption error: {ex.Message}");
                return string.Empty;
            }
        }

        /// <summary>
        /// APIキーを単純なオブファスケーションで保護
        /// </summary>
        /// <param name="apiKey">元のAPIキー</param>
        /// <returns>オブファスケーションされたAPIキー</returns>
        public static string ObfuscateApiKey(string apiKey)
        {
            if (string.IsNullOrEmpty(apiKey))
                return string.Empty;

            // シンプルなキー派生（実際のアプリケーションではより堅牢な方法を使用）
            byte[] keyBytes = Encoding.UTF8.GetBytes(apiKey);
            byte[] result = new byte[keyBytes.Length];

            for (int i = 0; i < keyBytes.Length; i++)
            {
                // 単純なビット操作（XORとビットシフト）
                result[i] = (byte)(keyBytes[i] ^ (0xA3 + i) ^ ((i * 7) & 0xFF));
            }

            return Convert.ToBase64String(result);
        }

        /// <summary>
        /// オブファスケーションされたAPIキーを復元
        /// </summary>
        /// <param name="obfuscatedKey">オブファスケーションされたAPIキー</param>
        /// <returns>元のAPIキー</returns>
        public static string DeobfuscateApiKey(string obfuscatedKey)
        {
            if (string.IsNullOrEmpty(obfuscatedKey))
                return string.Empty;

            try
            {
                byte[] obfuscatedBytes = Convert.FromBase64String(obfuscatedKey);
                byte[] result = new byte[obfuscatedBytes.Length];

                for (int i = 0; i < obfuscatedBytes.Length; i++)
                {
                    // 逆のビット操作で復元
                    result[i] = (byte)(obfuscatedBytes[i] ^ (0xA3 + i) ^ ((i * 7) & 0xFF));
                }

                return Encoding.UTF8.GetString(result);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Deobfuscation error: {ex.Message}");
                return string.Empty;
            }
        }

        /// <summary>
        /// アプリケーション内蔵のAPIキーを取得
        /// </summary>
        /// <returns>アプリケーション内蔵のAPIキー</returns>
        public static string GetEmbeddedApiKey()
        {
            try
            {
                // リソースファイルから暗号化されたAPIキーを読み込む
                string encryptedKey = Properties.Resources.EncryptedApiKey;
                return Decrypt(encryptedKey);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load API key from resources: {ex.Message}");
                return string.Empty;
            }
        }

        /// <summary>
        /// アプリケーション内蔵のAPIキーが有効かどうかを確認
        /// </summary>
        /// <returns>有効な場合はtrue</returns>
        public static bool IsEmbeddedApiKeyValid()
        {
            string apiKey = GetEmbeddedApiKey();
            return !string.IsNullOrEmpty(apiKey) && apiKey.StartsWith("sk-");
        }
    }
}