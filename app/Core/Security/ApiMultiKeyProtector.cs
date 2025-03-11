// GameTranslationOverlay/Core/Security/ApiMultiKeyProtector.cs
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Linq;
using GameTranslationOverlay.Properties;
using GameTranslationOverlay.Core.Diagnostics;

namespace GameTranslationOverlay.Core.Security
{
    /// <summary>
    /// 複数のAPIキーを安全に管理するためのシングルトンクラス
    /// </summary>
    public class ApiMultiKeyProtector
    {
        #region Singleton Implementation

        private static ApiMultiKeyProtector _instance;
        private static readonly object _lock = new object();

        public static ApiMultiKeyProtector Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                        {
                            _instance = new ApiMultiKeyProtector();
                        }
                    }
                }
                return _instance;
            }
        }

        #endregion

        #region Constants and Fields

        // サポートするAPIプロバイダーの列挙型
        public enum ApiProvider
        {
            OpenAI,
            GoogleGemini,
            Custom
        }

        // キー情報を格納する内部クラス
        private class ApiKeyInfo
        {
            public string EncryptedKey { get; set; }
            public DateTime Created { get; set; }
            public DateTime? Expiration { get; set; }
            public bool IsActive { get; set; }
            public string KeyId { get; set; }
        }

        // プロバイダー別のキー管理
        private readonly Dictionary<ApiProvider, List<ApiKeyInfo>> _apiKeys = new Dictionary<ApiProvider, List<ApiKeyInfo>>();
        
        // 設定ファイルパス
        private readonly string _keysFilePath;
        
        // 最後に発生したエラー
        private string _lastError = string.Empty;

        #endregion

        #region Constructor

        private ApiMultiKeyProtector()
        {
            // アプリケーションデータフォルダにキー情報を保存
            string appDataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "GameTranslationOverlay");
                
            // フォルダが存在しない場合は作成
            if (!Directory.Exists(appDataPath))
            {
                Directory.CreateDirectory(appDataPath);
            }
            
            _keysFilePath = Path.Combine(appDataPath, "api_keys.dat");
            
            // 初期化
            InitializeDefaultKeys();
            LoadKeys();
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// 指定されたプロバイダーの現在アクティブなAPIキーを取得
        /// </summary>
        /// <param name="provider">APIプロバイダー</param>
        /// <returns>復号化されたAPIキー、取得できない場合は空文字列</returns>
        public string GetApiKey(ApiProvider provider)
        {
            try
            {
                if (!_apiKeys.ContainsKey(provider) || _apiKeys[provider].Count == 0)
                {
                    _lastError = $"No API keys available for provider: {provider}";
                    Logger.Instance.LogWarning(_lastError);
                    return string.Empty;
                }

                // アクティブで期限内のキーを取得
                var activeKeys = _apiKeys[provider]
                    .Where(k => k.IsActive && (!k.Expiration.HasValue || k.Expiration.Value > DateTime.Now))
                    .OrderByDescending(k => k.Created)
                    .ToList();

                if (activeKeys.Count == 0)
                {
                    _lastError = $"No active API keys available for provider: {provider}";
                    Logger.Instance.LogWarning(_lastError);
                    return string.Empty;
                }

                // 最新のキーを使用
                var keyInfo = activeKeys.First();
                
                try
                {
                    // Base64デコード
                    byte[] encryptedBytes = Convert.FromBase64String(keyInfo.EncryptedKey);

                    // Windows DPAPIを使用して復号化
                    byte[] decryptedBytes = ProtectedData.Unprotect(
                        encryptedBytes,
                        null,
                        DataProtectionScope.CurrentUser);

                    // バイト配列を文字列に変換
                    return Encoding.UTF8.GetString(decryptedBytes);
                }
                catch (Exception ex)
                {
                    _lastError = $"Error decrypting API key: {ex.Message}";
                    Logger.Instance.LogError(_lastError, ex);
                    return string.Empty;
                }
            }
            catch (Exception ex)
            {
                _lastError = $"Unexpected error in GetApiKey: {ex.Message}";
                Logger.Instance.LogError(_lastError, ex);
                return string.Empty;
            }
        }

        /// <summary>
        /// 新しいAPIキーを追加
        /// </summary>
        /// <param name="provider">APIプロバイダー</param>
        /// <param name="apiKey">平文のAPIキー</param>
        /// <param name="expirationDate">有効期限（null = 無期限）</param>
        /// <param name="keyId">キーを識別するためのID（任意）</param>
        /// <returns>追加に成功した場合はtrue</returns>
        public bool AddApiKey(ApiProvider provider, string apiKey, DateTime? expirationDate = null, string keyId = null)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(apiKey))
                {
                    _lastError = "API key cannot be empty";
                    Logger.Instance.LogWarning(_lastError);
                    return false;
                }

                // 最初のエントリの場合はリストを初期化
                if (!_apiKeys.ContainsKey(provider))
                {
                    _apiKeys[provider] = new List<ApiKeyInfo>();
                }

                // キーIDの生成（指定がない場合）
                if (string.IsNullOrWhiteSpace(keyId))
                {
                    keyId = Guid.NewGuid().ToString().Substring(0, 8);
                }

                // 暗号化したキーを追加
                _apiKeys[provider].Add(new ApiKeyInfo
                {
                    EncryptedKey = EncryptApiKey(apiKey),
                    Created = DateTime.Now,
                    Expiration = expirationDate,
                    IsActive = true,
                    KeyId = keyId
                });

                // 変更を保存
                SaveKeys();

                Logger.Instance.LogInfo($"Added new API key for provider: {provider}, KeyID: {keyId}");
                return true;
            }
            catch (Exception ex)
            {
                _lastError = $"Error adding API key: {ex.Message}";
                Logger.Instance.LogError(_lastError, ex);
                return false;
            }
        }

        /// <summary>
        /// 指定されたプロバイダーのAPIキーを更新
        /// </summary>
        /// <param name="provider">APIプロバイダー</param>
        /// <param name="keyId">更新するキーのID</param>
        /// <param name="newApiKey">新しいAPIキー（nullの場合は更新しない）</param>
        /// <param name="isActive">アクティブ状態</param>
        /// <param name="newExpiration">新しい有効期限（nullの場合は更新しない）</param>
        /// <returns>更新に成功した場合はtrue</returns>
        public bool UpdateApiKey(ApiProvider provider, string keyId, string newApiKey = null, 
            bool? isActive = null, DateTime? newExpiration = null)
        {
            try
            {
                if (!_apiKeys.ContainsKey(provider))
                {
                    _lastError = $"No API keys available for provider: {provider}";
                    Logger.Instance.LogWarning(_lastError);
                    return false;
                }

                // 指定されたIDのキーを検索
                var keyInfo = _apiKeys[provider].FirstOrDefault(k => k.KeyId == keyId);
                if (keyInfo == null)
                {
                    _lastError = $"API key with ID {keyId} not found for provider: {provider}";
                    Logger.Instance.LogWarning(_lastError);
                    return false;
                }

                // キーを更新（指定された場合）
                if (!string.IsNullOrWhiteSpace(newApiKey))
                {
                    keyInfo.EncryptedKey = EncryptApiKey(newApiKey);
                    keyInfo.Created = DateTime.Now; // 更新日時を記録
                }

                // アクティブ状態を更新（指定された場合）
                if (isActive.HasValue)
                {
                    keyInfo.IsActive = isActive.Value;
                }

                // 有効期限を更新（指定された場合）
                if (newExpiration.HasValue)
                {
                    keyInfo.Expiration = newExpiration;
                }

                // 変更を保存
                SaveKeys();

                Logger.Instance.LogInfo($"Updated API key for provider: {provider}, KeyID: {keyId}");
                return true;
            }
            catch (Exception ex)
            {
                _lastError = $"Error updating API key: {ex.Message}";
                Logger.Instance.LogError(_lastError, ex);
                return false;
            }
        }

        /// <summary>
        /// APIキーを削除
        /// </summary>
        /// <param name="provider">APIプロバイダー</param>
        /// <param name="keyId">削除するキーのID</param>
        /// <returns>削除に成功した場合はtrue</returns>
        public bool RemoveApiKey(ApiProvider provider, string keyId)
        {
            try
            {
                if (!_apiKeys.ContainsKey(provider))
                {
                    _lastError = $"No API keys available for provider: {provider}";
                    Logger.Instance.LogWarning(_lastError);
                    return false;
                }

                // 削除する前のカウント
                int beforeCount = _apiKeys[provider].Count;
                
                // 指定されたIDのキーを削除
                _apiKeys[provider].RemoveAll(k => k.KeyId == keyId);
                
                // 削除後のカウントを確認
                if (_apiKeys[provider].Count == beforeCount)
                {
                    _lastError = $"API key with ID {keyId} not found for provider: {provider}";
                    Logger.Instance.LogWarning(_lastError);
                    return false;
                }

                // 変更を保存
                SaveKeys();

                Logger.Instance.LogInfo($"Removed API key for provider: {provider}, KeyID: {keyId}");
                return true;
            }
            catch (Exception ex)
            {
                _lastError = $"Error removing API key: {ex.Message}";
                Logger.Instance.LogError(_lastError, ex);
                return false;
            }
        }

        /// <summary>
        /// 指定されたプロバイダーの全てのキー情報を取得
        /// </summary>
        /// <param name="provider">APIプロバイダー</param>
        /// <returns>キー情報の一覧</returns>
        public List<Dictionary<string, object>> GetKeyInfoList(ApiProvider provider)
        {
            try
            {
                if (!_apiKeys.ContainsKey(provider) || _apiKeys[provider].Count == 0)
                {
                    return new List<Dictionary<string, object>>();
                }

                // 公開可能な情報のみを含むリストを作成
                return _apiKeys[provider].Select(k => new Dictionary<string, object>
                {
                    ["KeyId"] = k.KeyId,
                    ["Created"] = k.Created,
                    ["Expiration"] = k.Expiration,
                    ["IsActive"] = k.IsActive,
                    ["IsExpired"] = k.Expiration.HasValue && k.Expiration.Value < DateTime.Now
                }).ToList();
            }
            catch (Exception ex)
            {
                _lastError = $"Error getting key info list: {ex.Message}";
                Logger.Instance.LogError(_lastError, ex);
                return new List<Dictionary<string, object>>();
            }
        }

        /// <summary>
        /// 最後に発生したエラーメッセージを取得
        /// </summary>
        public string GetLastError()
        {
            return _lastError;
        }

        /// <summary>
        /// 有効期限切れのキーをチェックして無効化
        /// </summary>
        public void CleanupExpiredKeys()
        {
            try
            {
                bool changedAny = false;
                
                foreach (var provider in _apiKeys.Keys.ToList())
                {
                    int expiredCount = 0;
                    
                    foreach (var key in _apiKeys[provider].Where(k => k.IsActive && k.Expiration.HasValue && k.Expiration.Value < DateTime.Now))
                    {
                        key.IsActive = false;
                        expiredCount++;
                        changedAny = true;
                    }
                    
                    if (expiredCount > 0)
                    {
                        Logger.Instance.LogInfo($"Deactivated {expiredCount} expired keys for provider: {provider}");
                    }
                }
                
                // 変更があった場合のみ保存
                if (changedAny)
                {
                    SaveKeys();
                }
            }
            catch (Exception ex)
            {
                _lastError = $"Error cleaning up expired keys: {ex.Message}";
                Logger.Instance.LogError(_lastError, ex);
            }
        }

        /// <summary>
        /// 指定されたプロバイダーのキーがあるかどうかを確認
        /// </summary>
        public bool HasValidKey(ApiProvider provider)
        {
            try
            {
                if (!_apiKeys.ContainsKey(provider) || _apiKeys[provider].Count == 0)
                {
                    return false;
                }

                // アクティブで期限内のキーが1つでもあるか確認
                return _apiKeys[provider].Any(k => k.IsActive && (!k.Expiration.HasValue || k.Expiration.Value > DateTime.Now));
            }
            catch (Exception ex)
            {
                _lastError = $"Error checking valid keys: {ex.Message}";
                Logger.Instance.LogError(_lastError, ex);
                return false;
            }
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// デフォルトのAPIキーを初期化
        /// </summary>
        private void InitializeDefaultKeys()
        {
            try
            {
                // リソースからデフォルトのOpenAI APIキーを取得
                string openAiKey = Resources.EncryptedApiKey;
                if (!string.IsNullOrEmpty(openAiKey))
                {
                    if (!_apiKeys.ContainsKey(ApiProvider.OpenAI))
                    {
                        _apiKeys[ApiProvider.OpenAI] = new List<ApiKeyInfo>();
                    }

                    // デフォルトキーがまだ追加されていないか確認
                    if (!_apiKeys[ApiProvider.OpenAI].Any(k => k.KeyId == "default"))
                    {
                        _apiKeys[ApiProvider.OpenAI].Add(new ApiKeyInfo
                        {
                            EncryptedKey = openAiKey,
                            Created = DateTime.Now,
                            Expiration = null, // デフォルトキーは期限なし
                            IsActive = true,
                            KeyId = "default"
                        });

                        Logger.Instance.LogInfo("Initialized default OpenAI API key");
                    }
                }

                // 必要に応じて他のデフォルトキーも初期化
                // 例: GoogleGemini のデフォルトキーも同様に初期化
            }
            catch (Exception ex)
            {
                _lastError = $"Error initializing default keys: {ex.Message}";
                Logger.Instance.LogError(_lastError, ex);
            }
        }

        /// <summary>
        /// APIキーを暗号化
        /// </summary>
        private string EncryptApiKey(string apiKey)
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
                _lastError = $"Error encrypting API key: {ex.Message}";
                Logger.Instance.LogError(_lastError, ex);
                return string.Empty;
            }
        }

        /// <summary>
        /// キー情報を保存
        /// </summary>
        private void SaveKeys()
        {
            try
            {
                // APIキー情報をシリアライズ
                var serializedData = new Dictionary<string, List<Dictionary<string, object>>>();

                foreach (var providerPair in _apiKeys)
                {
                    string providerName = providerPair.Key.ToString();
                    var keyInfoList = new List<Dictionary<string, object>>();

                    foreach (var keyInfo in providerPair.Value)
                    {
                        keyInfoList.Add(new Dictionary<string, object>
                        {
                            ["EncryptedKey"] = keyInfo.EncryptedKey,
                            ["Created"] = keyInfo.Created.ToString("o"),
                            ["Expiration"] = keyInfo.Expiration?.ToString("o"),
                            ["IsActive"] = keyInfo.IsActive,
                            ["KeyId"] = keyInfo.KeyId
                        });
                    }

                    serializedData[providerName] = keyInfoList;
                }

                // 文字列に変換
                string json = System.Text.Json.JsonSerializer.Serialize(serializedData);

                // さらに保護するために暗号化
                byte[] dataToEncrypt = Encoding.UTF8.GetBytes(json);
                byte[] encryptedData = ProtectedData.Protect(
                    dataToEncrypt,
                    null,
                    DataProtectionScope.CurrentUser);

                // ファイルに保存
                File.WriteAllBytes(_keysFilePath, encryptedData);

                Logger.Instance.LogInfo("Saved API keys to file");
            }
            catch (Exception ex)
            {
                _lastError = $"Error saving API keys: {ex.Message}";
                Logger.Instance.LogError(_lastError, ex);
            }
        }

        /// <summary>
        /// キー情報を読み込み
        /// </summary>
        private void LoadKeys()
        {
            try
            {
                if (!File.Exists(_keysFilePath))
                {
                    Logger.Instance.LogInfo("API keys file does not exist, using defaults only");
                    return;
                }

                // ファイルから暗号化されたデータを読み込み
                byte[] encryptedData = File.ReadAllBytes(_keysFilePath);

                // 復号化
                byte[] decryptedData = ProtectedData.Unprotect(
                    encryptedData,
                    null,
                    DataProtectionScope.CurrentUser);

                // JSON文字列に変換
                string json = Encoding.UTF8.GetString(decryptedData);

                // デシリアライズ
                var serializedData = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, System.Text.Json.JsonElement>>(json);

                foreach (var providerPair in serializedData)
                {
                    // プロバイダー名を列挙型に変換
                    if (Enum.TryParse<ApiProvider>(providerPair.Key, out var provider))
                    {
                        var keyInfoList = new List<ApiKeyInfo>();

                        foreach (var keyInfoElement in providerPair.Value.EnumerateArray())
                        {
                            var keyInfo = new ApiKeyInfo
                            {
                                EncryptedKey = keyInfoElement.GetProperty("EncryptedKey").GetString(),
                                Created = DateTime.Parse(keyInfoElement.GetProperty("Created").GetString()),
                                IsActive = keyInfoElement.GetProperty("IsActive").GetBoolean(),
                                KeyId = keyInfoElement.GetProperty("KeyId").GetString()
                            };

                            // 有効期限（オプション）
                            if (keyInfoElement.TryGetProperty("Expiration", out var expirationElement) && 
                                !expirationElement.ValueKind.HasFlag(System.Text.Json.JsonValueKind.Null))
                            {
                                keyInfo.Expiration = DateTime.Parse(expirationElement.GetString());
                            }

                            keyInfoList.Add(keyInfo);
                        }

                        _apiKeys[provider] = keyInfoList;
                    }
                }

                Logger.Instance.LogInfo("Loaded API keys from file");
            }
            catch (Exception ex)
            {
                _lastError = $"Error loading API keys: {ex.Message}";
                Logger.Instance.LogError(_lastError, ex);
                
                // 読み込みに失敗した場合はデフォルト値のみを使用
                Logger.Instance.LogWarning("Using default API keys only due to load error");
            }
        }

        #endregion
    }
}
