using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using GameTranslationOverlay.Core.Diagnostics;

namespace GameTranslationOverlay.Core.Security
{
    /// <summary>
    /// 暗号化と復号化の汎用ユーティリティクラス
    /// </summary>
    public static class EncryptionHelper
    {
        // デフォルトのソルトサイズ
        private const int DefaultSaltSize = 16;

        // デフォルトの初期化ベクトルサイズ
        private const int DefaultIvSize = 16;

        // デフォルトの反復回数（キー生成時）
        private const int DefaultIterationCount = 10000;

        // 暗号化キーのサイズ（ビット）
        private const int KeySize = 256;

        /// <summary>
        /// 文字列をDPAPIを使用して暗号化
        /// </summary>
        /// <param name="plainText">暗号化する文字列</param>
        /// <param name="optionalEntropy">追加のエントロピー（オプション）</param>
        /// <param name="scope">保護スコープ（デフォルトは現在のユーザー）</param>
        /// <returns>Base64エンコードされた暗号化文字列</returns>
        public static string ProtectWithDpapi(string plainText, byte[] optionalEntropy = null, DataProtectionScope scope = DataProtectionScope.CurrentUser)
        {
            try
            {
                if (string.IsNullOrEmpty(plainText))
                {
                    return string.Empty;
                }

                // 文字列をバイト配列に変換
                byte[] dataToEncrypt = Encoding.UTF8.GetBytes(plainText);

                // DPAPIで暗号化
                byte[] encryptedData = ProtectedData.Protect(
                    dataToEncrypt,
                    optionalEntropy,
                    scope);

                // Base64エンコード
                return Convert.ToBase64String(encryptedData);
            }
            catch (Exception ex)
            {
                Logger.Instance.LogError($"Error in ProtectWithDpapi: {ex.Message}", ex);
                return string.Empty;
            }
        }

        /// <summary>
        /// DPAPIで暗号化された文字列を復号化
        /// </summary>
        /// <param name="encryptedText">Base64エンコードされた暗号化文字列</param>
        /// <param name="optionalEntropy">暗号化時に使用したエントロピー</param>
        /// <param name="scope">保護スコープ（暗号化時と同じ値を使用）</param>
        /// <returns>復号化された文字列</returns>
        public static string UnprotectWithDpapi(string encryptedText, byte[] optionalEntropy = null, DataProtectionScope scope = DataProtectionScope.CurrentUser)
        {
            try
            {
                if (string.IsNullOrEmpty(encryptedText))
                {
                    return string.Empty;
                }

                // Base64デコード
                byte[] encryptedData = Convert.FromBase64String(encryptedText);

                // DPAPIで復号化
                byte[] decryptedData = ProtectedData.Unprotect(
                    encryptedData,
                    optionalEntropy,
                    scope);

                // バイト配列を文字列に変換
                return Encoding.UTF8.GetString(decryptedData);
            }
            catch (Exception ex)
            {
                Logger.Instance.LogError($"Error in UnprotectWithDpapi: {ex.Message}", ex);
                return string.Empty;
            }
        }

        /// <summary>
        /// AES暗号化を使用して文字列を暗号化
        /// </summary>
        /// <param name="plainText">暗号化する文字列</param>
        /// <param name="password">パスワード</param>
        /// <param name="salt">ソルト（指定しない場合はランダム生成）</param>
        /// <param name="iterations">反復回数</param>
        /// <returns>暗号化された文字列（ソルトと初期化ベクトルを含む）</returns>
        public static string EncryptWithAes(string plainText, string password, byte[] salt = null, int iterations = DefaultIterationCount)
        {
            try
            {
                if (string.IsNullOrEmpty(plainText) || string.IsNullOrEmpty(password))
                {
                    return string.Empty;
                }

                // ソルトがない場合はランダム生成
                if (salt == null || salt.Length == 0)
                {
                    salt = GenerateRandomBytes(DefaultSaltSize);
                }

                // パスワードとソルトからキーと初期化ベクトルを生成
                using (var deriveBytes = new Rfc2898DeriveBytes(password, salt, iterations))
                {
                    byte[] key = deriveBytes.GetBytes(KeySize / 8);
                    byte[] iv = deriveBytes.GetBytes(DefaultIvSize);

                    // 暗号化
                    using (var aes = Aes.Create())
                    {
                        aes.Key = key;
                        aes.IV = iv;

                        using (var ms = new MemoryStream())
                        {
                            // ソルトを保存
                            ms.Write(BitConverter.GetBytes(salt.Length), 0, sizeof(int));
                            ms.Write(salt, 0, salt.Length);

                            // 反復回数を保存
                            ms.Write(BitConverter.GetBytes(iterations), 0, sizeof(int));

                            // 暗号化テキストを書き込む
                            using (var cs = new CryptoStream(ms, aes.CreateEncryptor(), CryptoStreamMode.Write))
                            {
                                byte[] plainTextBytes = Encoding.UTF8.GetBytes(plainText);
                                cs.Write(plainTextBytes, 0, plainTextBytes.Length);
                                cs.FlushFinalBlock();
                            }

                            // Base64エンコード
                            return Convert.ToBase64String(ms.ToArray());
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.LogError($"Error in EncryptWithAes: {ex.Message}", ex);
                return string.Empty;
            }
        }

        /// <summary>
        /// AES暗号化された文字列を復号化
        /// </summary>
        /// <param name="encryptedText">暗号化された文字列</param>
        /// <param name="password">暗号化に使用したパスワード</param>
        /// <returns>復号化された文字列</returns>
        public static string DecryptWithAes(string encryptedText, string password)
        {
            try
            {
                if (string.IsNullOrEmpty(encryptedText) || string.IsNullOrEmpty(password))
                {
                    return string.Empty;
                }

                // Base64デコード
                byte[] encryptedBytes = Convert.FromBase64String(encryptedText);

                using (var ms = new MemoryStream(encryptedBytes))
                {
                    // ソルトサイズを読み込む
                    byte[] saltSizeBytes = new byte[sizeof(int)];
                    ms.Read(saltSizeBytes, 0, saltSizeBytes.Length);
                    int saltSize = BitConverter.ToInt32(saltSizeBytes, 0);

                    // ソルトを読み込む
                    byte[] salt = new byte[saltSize];
                    ms.Read(salt, 0, salt.Length);

                    // 反復回数を読み込む
                    byte[] iterationsBytes = new byte[sizeof(int)];
                    ms.Read(iterationsBytes, 0, iterationsBytes.Length);
                    int iterations = BitConverter.ToInt32(iterationsBytes, 0);

                    // パスワードとソルトからキーと初期化ベクトルを生成
                    using (var deriveBytes = new Rfc2898DeriveBytes(password, salt, iterations))
                    {
                        byte[] key = deriveBytes.GetBytes(KeySize / 8);
                        byte[] iv = deriveBytes.GetBytes(DefaultIvSize);

                        // 復号化
                        using (var aes = Aes.Create())
                        {
                            aes.Key = key;
                            aes.IV = iv;

                            using (var cs = new CryptoStream(ms, aes.CreateDecryptor(), CryptoStreamMode.Read))
                            {
                                using (var sr = new StreamReader(cs, Encoding.UTF8))
                                {
                                    return sr.ReadToEnd();
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.LogError($"Error in DecryptWithAes: {ex.Message}", ex);
                return string.Empty;
            }
        }

        /// <summary>
        /// ランダムなソルトを生成
        /// </summary>
        /// <param name="size">生成するバイト数</param>
        /// <returns>ランダムなバイト配列</returns>
        public static byte[] GenerateRandomSalt(int size = DefaultSaltSize)
        {
            return GenerateRandomBytes(size);
        }

        /// <summary>
        /// ランダムなバイト配列を生成
        /// </summary>
        /// <param name="size">生成するバイト数</param>
        /// <returns>ランダムなバイト配列</returns>
        public static byte[] GenerateRandomBytes(int size)
        {
            try
            {
                byte[] randomBytes = new byte[size];
                using (var rng = RandomNumberGenerator.Create())
                {
                    rng.GetBytes(randomBytes);
                }
                return randomBytes;
            }
            catch (Exception ex)
            {
                Logger.Instance.LogError($"Error in GenerateRandomBytes: {ex.Message}", ex);
                return new byte[0];
            }
        }

        /// <summary>
        /// 文字列をハッシュ化（SHA-256）
        /// </summary>
        /// <param name="input">ハッシュ化する文字列</param>
        /// <param name="salt">ソルト（オプション）</param>
        /// <returns>ハッシュ値（16進数文字列）</returns>
        public static string ComputeHash(string input, byte[] salt = null)
        {
            try
            {
                if (string.IsNullOrEmpty(input))
                {
                    return string.Empty;
                }

                byte[] inputBytes = Encoding.UTF8.GetBytes(input);
                byte[] dataToHash;

                // ソルトがある場合は連結
                if (salt != null && salt.Length > 0)
                {
                    dataToHash = new byte[inputBytes.Length + salt.Length];
                    Buffer.BlockCopy(inputBytes, 0, dataToHash, 0, inputBytes.Length);
                    Buffer.BlockCopy(salt, 0, dataToHash, inputBytes.Length, salt.Length);
                }
                else
                {
                    dataToHash = inputBytes;
                }

                // SHA-256ハッシュを計算
                using (var sha256 = SHA256.Create())
                {
                    byte[] hashBytes = sha256.ComputeHash(dataToHash);

                    // 16進数文字列に変換
                    StringBuilder sb = new StringBuilder();
                    foreach (byte b in hashBytes)
                    {
                        sb.Append(b.ToString("x2"));
                    }
                    return sb.ToString();
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.LogError($"Error in ComputeHash: {ex.Message}", ex);
                return string.Empty;
            }
        }

        /// <summary>
        /// バイト配列を16進数文字列に変換
        /// </summary>
        /// <param name="bytes">変換するバイト配列</param>
        /// <returns>16進数文字列</returns>
        public static string BytesToHex(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0)
            {
                return string.Empty;
            }

            StringBuilder sb = new StringBuilder(bytes.Length * 2);
            foreach (byte b in bytes)
            {
                sb.Append(b.ToString("x2"));
            }
            return sb.ToString();
        }

        /// <summary>
        /// 16進数文字列をバイト配列に変換
        /// </summary>
        /// <param name="hex">16進数文字列</param>
        /// <returns>バイト配列</returns>
        public static byte[] HexToBytes(string hex)
        {
            try
            {
                if (string.IsNullOrEmpty(hex))
                {
                    return new byte[0];
                }

                // 奇数の場合は先頭に0を追加
                if (hex.Length % 2 != 0)
                {
                    hex = "0" + hex;
                }

                byte[] bytes = new byte[hex.Length / 2];
                for (int i = 0; i < bytes.Length; i++)
                {
                    bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
                }
                return bytes;
            }
            catch (Exception ex)
            {
                Logger.Instance.LogError($"Error in HexToBytes: {ex.Message}", ex);
                return new byte[0];
            }
        }
    }
}