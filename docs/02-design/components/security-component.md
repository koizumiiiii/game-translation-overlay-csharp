# ゲーム翻訳オーバーレイアプリケーション - セキュリティ設計

## 1. 概要

セキュリティコンポーネントは、ゲーム翻訳オーバーレイアプリケーションにおいて、APIキーの保護、ユーザーデータのセキュリティ、およびライセンス認証を担当する重要なモジュールです。特に、OpenAI APIやGoogle Gemini APIなどの外部サービスへのアクセスに必要なAPIキーを安全に管理し、適切なタイミングで提供する機能を提供します。

### 1.1 目的と責務

- APIキーの安全な保存と管理
- 機密データの暗号化と保護
- ライセンス認証と機能アクセス制御
- セキュリティ関連のエラー処理と回復機能
- ユーザープライバシーの保護

### 1.2 対象サービス

- **OpenAI API** - 翻訳機能のために使用
- **OpenAI Vision API** - OCR最適化のために使用
- **Google Gemini API** - OCR最適化のために使用
- **ローカルユーザーデータ** - 設定やプロファイル情報
- **ライセンス情報** - ユーザーのライセンス状態データ

### 1.3 基本方針

- APIキーは常に暗号化して保存する
- ユーザーにAPIキーを直接入力させない設計を基本とする
- 複数のAPIサービスに対応した柔軟な設計にする
- エラー時の適切なフォールバック機能を提供する
- メモリ内での機密情報の最小化と適切な破棄を行う

## 2. アーキテクチャ

### 2.1 全体構成

セキュリティコンポーネントは以下のクラスで構成されます：

```
Core/Security/
├── ApiKeyProtector.cs - 単一APIキーの暗号化管理
├── ApiMultiKeyProtector.cs - 複数APIキーの安全な管理
├── EncryptionHelper.cs - 汎用暗号化機能
├── LicenseManager.cs - ライセンス状態の管理
└── SecureStorage.cs - 安全なデータ保存
```

### 2.2 クラス構造

#### 2.2.1 主要クラス

| クラス名 | 名前空間 | 責務 |
|----------|----------|------|
| ApiKeyProtector | GameTranslationOverlay.Core.Security | 単一APIキーの暗号化・復号化 |
| ApiMultiKeyProtector | GameTranslationOverlay.Core.Security | 複数APIキーの安全な管理 |
| EncryptionHelper | GameTranslationOverlay.Core.Security | 汎用暗号化・復号化機能 |
| LicenseManager | GameTranslationOverlay.Core.Licensing | ライセンス状態確認と管理 |
| SecureStorage | GameTranslationOverlay.Core.Security | 設定などの安全な保存 |

#### 2.2.2 主要インターフェース

```csharp
/// <summary>
/// APIキー提供機能のインターフェース
/// </summary>
public interface IApiKeyProvider
{
    /// <summary>
    /// 指定されたプロバイダーのAPIキーを取得する
    /// </summary>
    /// <param name="provider">APIプロバイダー名 (例: "openai", "gemini")</param>
    /// <param name="keyId">キーID (複数キー管理用、デフォルトは "default")</param>
    /// <returns>復号化されたAPIキー、取得できない場合はnull</returns>
    string GetApiKey(string provider, string keyId = "default");
    
    /// <summary>
    /// APIキーのフォーマット検証
    /// </summary>
    /// <param name="provider">APIプロバイダー名</param>
    /// <param name="apiKey">検証するAPIキー</param>
    /// <returns>フォーマットが正しければtrue</returns>
    bool ValidateApiKeyFormat(string provider, string apiKey);
}

/// <summary>
/// ライセンス管理機能のインターフェース
/// </summary>
public interface ILicenseManager
{
    /// <summary>
    /// 指定された機能が現在のライセンスで利用可能かを確認
    /// </summary>
    /// <param name="feature">確認する機能</param>
    /// <returns>利用可能であればtrue</returns>
    bool HasFeature(PremiumFeature feature);
    
    /// <summary>
    /// 現在のライセンスタイプを取得
    /// </summary>
    LicenseType CurrentLicenseType { get; }
    
    /// <summary>
    /// ライセンスキーを検証する
    /// </summary>
    /// <param name="licenseKey">検証するライセンスキー</param>
    /// <returns>検証結果</returns>
    Task<LicenseValidationResult> ValidateLicenseAsync(string licenseKey);
}

/// <summary>
/// 安全なデータ保存機能のインターフェース
/// </summary>
public interface ISecureStorage
{
    /// <summary>
    /// 暗号化してデータを保存
    /// </summary>
    /// <param name="key">データのキー</param>
    /// <param name="value">保存する値</param>
    /// <returns>保存成功すればtrue</returns>
    bool SaveSecureData(string key, string value);
    
    /// <summary>
    /// 暗号化されたデータを読み込み
    /// </summary>
    /// <param name="key">データのキー</param>
    /// <returns>復号化された値、取得できない場合はnull</returns>
    string GetSecureData(string key);
}
```

#### 2.2.3 データモデル

```csharp
/// <summary>
/// プレミアム機能の列挙型
/// </summary>
public enum PremiumFeature
{
    AiTranslation,          // AI翻訳機能
    GameProfiles,           // ゲームプロファイル機能
    CustomDictionary,       // カスタム辞書機能
    ExportFeature,          // エクスポート機能
    AdvancedOcrOptimization // 高度なOCR最適化
}

/// <summary>
/// ライセンスタイプの列挙型
/// </summary>
public enum LicenseType
{
    Free,       // 無料版
    Basic,      // ベーシックプラン
    Pro,        // プロフェッショナルプラン
    Developer   // 開発者版
}

/// <summary>
/// ライセンス検証結果クラス
/// </summary>
public class LicenseValidationResult
{
    /// <summary>
    /// 検証に成功したかどうか
    /// </summary>
    public bool IsValid { get; set; }
    
    /// <summary>
    /// ライセンスタイプ
    /// </summary>
    public LicenseType LicenseType { get; set; }
    
    /// <summary>
    /// エラーメッセージ（エラー時のみ）
    /// </summary>
    public string ErrorMessage { get; set; }
    
    /// <summary>
    /// 有効期限（該当する場合）
    /// </summary>
    public DateTime? ExpirationDate { get; set; }
}
```

## 3. APIキー管理

### 3.1 暗号化方式

APIキーなどの機密情報は、以下の暗号化方式を用いて保護しています：

#### 3.1.1 データ保護API (DPAPI)

Windows組み込みの暗号化APIを使用して、ユーザーまたはマシン単位での保護を提供します。

```csharp
// DPAPIを使用した暗号化
public static byte[] EncryptWithDPAPI(string plainText, DataProtectionScope scope)
{
    if (string.IsNullOrEmpty(plainText))
        return null;
        
    try
    {
        byte[] data = Encoding.UTF8.GetBytes(plainText);
        return ProtectedData.Protect(data, null, scope);
    }
    catch (Exception ex)
    {
        Logger.Instance.LogError($"DPAPI暗号化エラー: {ex.Message}");
        throw new CryptographicException($"DPAPI暗号化に失敗しました: {ex.Message}", ex);
    }
}
```

#### 3.1.2 AES暗号化

より詳細な制御が必要な場合に使用する対称鍵暗号方式です。

```csharp
// AES暗号化
public static string EncryptWithAES(string plainText, string key)
{
    if (string.IsNullOrEmpty(plainText) || string.IsNullOrEmpty(key))
        return null;
        
    try
    {
        using (Aes aes = Aes.Create())
        {
            // 鍵とIVの導出
            byte[] keyBytes = new byte[32]; // 256ビット
            byte[] salt = Encoding.UTF8.GetBytes("GameTranslationOverlaySalt");
            
            using (var derivation = new Rfc2898DeriveBytes(key, salt, 10000))
            {
                keyBytes = derivation.GetBytes(32);
                aes.Key = keyBytes;
                aes.IV = derivation.GetBytes(16);
            }
            
            // 暗号化
            using (MemoryStream ms = new MemoryStream())
            {
                using (CryptoStream cs = new CryptoStream(ms, aes.CreateEncryptor(), CryptoStreamMode.Write))
                {
                    byte[] data = Encoding.UTF8.GetBytes(plainText);
                    cs.Write(data, 0, data.Length);
                    cs.FlushFinalBlock();
                }
                
                return Convert.ToBase64String(ms.ToArray());
            }
        }
    }
    catch (Exception ex)
    {
        Logger.Instance.LogError($"AES暗号化エラー: {ex.Message}");
        throw new CryptographicException($"AES暗号化に失敗しました: {ex.Message}", ex);
    }
}
```

### 3.2 APIキーの保存場所

APIキーは以下の方法で安全に保存されます：

#### 3.2.1 埋め込みリソース

アプリケーションに同梱される暗号化されたAPIキーを、リソースとして格納します。

```csharp
// リソースに格納された暗号化APIキー
private static readonly string EncryptedOpenAIApiKey = Properties.Resources.EncryptedOpenAIApiKey;
private static readonly string EncryptedVisionApiKey = Properties.Resources.EncryptedVisionApiKey;
private static readonly string EncryptedGeminiApiKey = Properties.Resources.EncryptedGeminiApiKey;
```

#### 3.2.2 設定ファイル

ユーザーによるカスタムAPIキーは、暗号化して設定ファイルに保存します。

```csharp
// カスタムAPIキーの保存
public bool SaveCustomApiKey(string provider, string apiKey, string keyId = "default")
{
    try
    {
        // APIキーの形式検証
        if (!ValidateApiKeyFormat(provider, apiKey))
        {
            Logger.Instance.LogWarning($"無効な{provider} APIキー形式");
            return false;
        }
        
        // APIキーの暗号化
        byte[] encryptedKey = EncryptionHelper.EncryptWithDPAPI(apiKey, DataProtectionScope.CurrentUser);
        string base64Key = Convert.ToBase64String(encryptedKey);
        
        // 設定へ保存
        string settingKey = $"ApiKey_{provider}_{keyId}";
        AppSettings.Instance.SetSecureValue(settingKey, base64Key);
        
        return true;
    }
    catch (Exception ex)
    {
        Logger.Instance.LogError($"APIキー保存エラー: {ex.Message}");
        return false;
    }
}
```

### 3.3 ApiMultiKeyProtector クラス

複数のAPIサービスとキーIDに対応した柔軟なAPIキー管理クラスです。

```csharp
// ApiMultiKeyProtector クラスの実装
public class ApiMultiKeyProtector : IApiKeyProvider
{
    private static readonly object _lockObject = new object();
    private static ApiMultiKeyProtector _instance;
    private readonly Dictionary<string, Dictionary<string, string>> _keys = new Dictionary<string, Dictionary<string, string>>();
    
    // シングルトンインスタンス取得
    public static ApiMultiKeyProtector Instance
    {
        get
        {
            if (_instance == null)
            {
                lock (_lockObject)
                {
                    if (_instance == null)
                    {
                        _instance = new ApiMultiKeyProtector();
                        _instance.LoadKeys();
                    }
                }
            }
            return _instance;
        }
    }
    
    // APIキー取得
    public string GetApiKey(string provider, string keyId = "default")
    {
        if (string.IsNullOrEmpty(provider))
            return null;
            
        lock (_lockObject)
        {
            if (_keys.TryGetValue(provider, out var providerKeys) &&
                providerKeys.TryGetValue(keyId, out var encryptedKey))
            {
                try
                {
                    // 復号化処理
                    string decryptedKey = EncryptionHelper.DecryptWithDPAPI(
                        Convert.FromBase64String(encryptedKey), 
                        DataProtectionScope.LocalMachine);
                        
                    // APIキー形式検証
                    if (ValidateApiKeyFormat(provider, decryptedKey))
                    {
                        return decryptedKey;
                    }
                    else
                    {
                        // 形式不正の場合はログ記録
                        Logger.Instance.LogWarning($"無効な{provider} APIキー形式");
                    }
                }
                catch (Exception ex)
                {
                    // 復号化エラー記録
                    Logger.Instance.LogError($"{provider} APIキー復号化エラー: {ex.Message}");
                }
            }
            
            // デバッグモードでのフォールバック
            if (AppSettings.Instance.DebugModeEnabled)
            {
                // デバッグモード用の固定キー
                return GetDebugModeKey(provider);
            }
            
            return null;
        }
    }
    
    // APIキー形式検証
    public bool ValidateApiKeyFormat(string provider, string apiKey)
    {
        if (string.IsNullOrEmpty(apiKey))
            return false;
            
        switch (provider.ToLower())
        {
            case "openai":
                return apiKey.StartsWith("sk-") && apiKey.Length >= 20;
                
            case "gemini":
                return apiKey.StartsWith("AIza") && apiKey.Length >= 20;
                
            default:
                return apiKey.Length >= 10;
        }
    }
    
    // 鍵の読み込み
    private void LoadKeys()
    {
        try
        {
            // リソースからの読み込み
            _keys["openai"] = new Dictionary<string, string>
            {
                ["default"] = Properties.Resources.EncryptedOpenAIApiKey
            };
            
            _keys["openai-vision"] = new Dictionary<string, string>
            {
                ["default"] = Properties.Resources.EncryptedVisionApiKey
            };
            
            _keys["gemini"] = new Dictionary<string, string>
            {
                ["default"] = Properties.Resources.EncryptedGeminiApiKey
            };
            
            // ユーザー設定から追加読み込み
            LoadUserKeys();
        }
        catch (Exception ex)
        {
            Logger.Instance.LogError($"APIキー読み込みエラー: {ex.Message}");
        }
    }
    
    // ユーザー設定からのキー読み込み
    private void LoadUserKeys()
    {
        // 設定からカスタムAPIキーを読み込み
        var apiKeySettings = AppSettings.Instance.GetAllSecureValues()
            .Where(k => k.Key.StartsWith("ApiKey_"))
            .ToDictionary(k => k.Key, k => k.Value);
            
        foreach (var setting in apiKeySettings)
        {
            // キー形式: ApiKey_provider_keyId
            string[] parts = setting.Key.Split('_');
            if (parts.Length >= 3)
            {
                string provider = parts[1].ToLower();
                string keyId = parts.Length > 3 ? parts[2] : "default";
                
                if (!_keys.ContainsKey(provider))
                {
                    _keys[provider] = new Dictionary<string, string>();
                }
                
                _keys[provider][keyId] = setting.Value;
            }
        }
    }
}
```

## 4. ライセンス管理

### 4.1 LicenseManager クラス

ライセンス状態を管理し、各機能の利用可否を判断します。

```csharp
// LicenseManager クラスの実装
public class LicenseManager : ILicenseManager
{
    private static readonly object _lockObject = new object();
    private static LicenseManager _instance;
    
    // ライセンス情報
    private LicenseType _currentLicenseType = LicenseType.Free;
    private DateTime? _expirationDate = null;
    private string _licenseKey = null;
    private bool _isValidated = false;
    
    // シングルトンインスタンス取得
    public static LicenseManager Instance
    {
        get
        {
            if (_instance == null)
            {
                lock (_lockObject)
                {
                    if (_instance == null)
                    {
                        _instance = new LicenseManager();
                        _instance.LoadLicenseState();
                    }
                }
            }
            return _instance;
        }
    }
    
    // 現在のライセンスタイプ
    public LicenseType CurrentLicenseType
    {
        get { return _currentLicenseType; }
    }
    
    // 初期化処理
    private LicenseManager()
    {
        // 定期的なライセンス検証タイマーの設定
        Timer validationTimer = new Timer(ValidateLicensePeriodically, null, 
            TimeSpan.FromHours(24), TimeSpan.FromHours(24));
    }
    
    // 機能アクセス確認
    public bool HasFeature(PremiumFeature feature)
    {
        // ライセンスが未検証の場合はロード
        if (!_isValidated)
        {
            LoadLicenseState();
        }
        
        // ライセンスが期限切れの場合は無料版として扱う
        if (_expirationDate.HasValue && _expirationDate.Value < DateTime.Now)
        {
            _currentLicenseType = LicenseType.Free;
        }
        
        // ライセンスタイプに基づく機能アクセス制御
        switch (feature)
        {
            case PremiumFeature.AiTranslation:
                return _currentLicenseType == LicenseType.Pro || 
                       _currentLicenseType == LicenseType.Developer;
                
            case PremiumFeature.GameProfiles:
                return _currentLicenseType >= LicenseType.Basic;
                
            case PremiumFeature.CustomDictionary:
                return _currentLicenseType >= LicenseType.Basic;
                
            case PremiumFeature.ExportFeature:
                return _currentLicenseType == LicenseType.Pro || 
                       _currentLicenseType == LicenseType.Developer;
                
            case PremiumFeature.AdvancedOcrOptimization:
                return _currentLicenseType == LicenseType.Pro || 
                       _currentLicenseType == LicenseType.Developer;
                
            default:
                return false;
        }
    }
    
    // ライセンス検証
    public async Task<LicenseValidationResult> ValidateLicenseAsync(string licenseKey)
    {
        if (string.IsNullOrEmpty(licenseKey))
        {
            return new LicenseValidationResult
            {
                IsValid = false,
                LicenseType = LicenseType.Free,
                ErrorMessage = "ライセンスキーが指定されていません"
            };
        }
        
        try
        {
            // ローカル検証（オフライン時）
            if (!NetworkHelper.IsInternetAvailable())
            {
                return ValidateLicenseOffline(licenseKey);
            }
            
            // オンライン検証
            var result = await ValidateLicenseOnlineAsync(licenseKey);
            
            // 検証結果が有効な場合、状態を保存
            if (result.IsValid)
            {
                SaveLicenseState(licenseKey, result.LicenseType, result.ExpirationDate);
            }
            
            return result;
        }
        catch (Exception ex)
        {
            Logger.Instance.LogError($"ライセンス検証エラー: {ex.Message}");
            
            // エラー時はローカル検証を試みる
            return ValidateLicenseOffline(licenseKey);
        }
    }
    
    // オンライン検証（実際の実装では適切なサーバー通信が必要）
    private async Task<LicenseValidationResult> ValidateLicenseOnlineAsync(string licenseKey)
    {
        // 実装省略（実際はライセンスサーバーへの通信が必要）
        
        // ダミー実装
        await Task.Delay(500); // サーバー通信の代わり
        
        if (licenseKey.StartsWith("PRO-"))
        {
            return new LicenseValidationResult
            {
                IsValid = true,
                LicenseType = LicenseType.Pro,
                ExpirationDate = DateTime.Now.AddYears(1)
            };
        }
        else if (licenseKey.StartsWith("BASIC-"))
        {
            return new LicenseValidationResult
            {
                IsValid = true,
                LicenseType = LicenseType.Basic,
                ExpirationDate = DateTime.Now.AddYears(1)
            };
        }
        
        return new LicenseValidationResult
        {
            IsValid = false,
            LicenseType = LicenseType.Free,
            ErrorMessage = "無効なライセンスキー"
        };
    }
    
    // ライセンス状態の保存/読み込み処理は省略
    
    // オフライン検証
    private LicenseValidationResult ValidateLicenseOffline(string licenseKey)
    {
        // 簡易的なオフライン検証（実際はより高度な検証が必要）
        // 保存されたハッシュとの比較など
        
        // 実装省略
        return new LicenseValidationResult
        {
            IsValid = false,
            LicenseType = LicenseType.Free,
            ErrorMessage = "オフラインモードではライセンス検証できません"
        };
    }
}
```

### 4.2 ライセンス状態の保存

ライセンス情報はセキュアに保存し、改ざんを防止します。

```csharp
// ライセンス状態の保存
private void SaveLicenseState(string licenseKey, LicenseType licenseType, DateTime? expirationDate)
{
    try
    {
        // 機密情報の暗号化
        string encryptedKey = EncryptionHelper.EncryptWithAES(
            licenseKey, 
            GetMachineSpecificKey());
            
        // ライセンス情報の保存
        SecureStorage.Instance.SaveSecureData("LicenseKey", encryptedKey);
        SecureStorage.Instance.SaveSecureData("LicenseType", ((int)licenseType).ToString());
        
        if (expirationDate.HasValue)
        {
            SecureStorage.Instance.SaveSecureData("ExpirationDate", expirationDate.Value.ToString("o"));
        }
        
        // メモリ内の状態を更新
        _licenseKey = licenseKey;
        _currentLicenseType = licenseType;
        _expirationDate = expirationDate;
        _isValidated = true;
    }
    catch (Exception ex)
    {
        Logger.Instance.LogError($"ライセンス状態保存エラー: {ex.Message}");
    }
}
```

## 5. 安全なデータ保存

### 5.1 SecureStorage クラス

ユーザー設定などのデータを安全に保存・読み込みするためのクラスです。

```csharp
// SecureStorage クラスの実装
public class SecureStorage : ISecureStorage
{
    private static readonly object _lockObject = new object();
    private static SecureStorage _instance;
    
    // シングルトンインスタンス取得
    public static SecureStorage Instance
    {
        get
        {
            if (_instance == null)
            {
                lock (_lockObject)
                {
                    if (_instance == null)
                    {
                        _instance = new SecureStorage();
                    }
                }
            }
            return _instance;
        }
    }
    
    // 安全なデータ保存
    public bool SaveSecureData(string key, string value)
    {
        try
        {
            // キー検証
            if (string.IsNullOrEmpty(key))
            {
                throw new ArgumentException("キーが指定されていません");
            }
            
            // 値の暗号化
            string encryptedValue = value;
            if (!string.IsNullOrEmpty(value))
            {
                encryptedValue = EncryptionHelper.EncryptWithAES(value, GetStorageKey());
            }
            
            // レジストリに保存（代替としてファイルやなどの保存も可能）
            string registryPath = $"SOFTWARE\\GameTranslationOverlay\\SecureData";
            using (var regKey = Registry.CurrentUser.CreateSubKey(registryPath))
            {
                regKey.SetValue(key, encryptedValue ?? string.Empty);
            }
            
            return true;
        }
        catch (Exception ex)
        {
            Logger.Instance.LogError($"セキュアデータ保存エラー: {ex.Message}");
            return false;
        }
    }
    
    // 安全なデータ読み込み
    public string GetSecureData(string key)
    {
        try
        {
            // キー検証
            if (string.IsNullOrEmpty(key))
            {
                throw new ArgumentException("キーが指定されていません");
            }
            
            // レジストリから読み込み
            string registryPath = $"SOFTWARE\\GameTranslationOverlay\\SecureData";
            using (var regKey = Registry.CurrentUser.OpenSubKey(registryPath))
            {
                if (regKey == null)
                    return null;
                    
                string encryptedValue = regKey.GetValue(key) as string;
                
                // 値が空ならnullを返す
                if (string.IsNullOrEmpty(encryptedValue))
                    return null;
                    
                // 復号化して返す
                return EncryptionHelper.DecryptWithAES(encryptedValue, GetStorageKey());
            }
        }
        catch (Exception ex)
        {
            Logger.Instance.LogError($"セキュアデータ読み込みエラー: {ex.Message}");
            return null;
        }
    }
    
    // ストレージキーの取得（実装は省略、マシン固有の情報からキーを生成）
    private string GetStorageKey()
    {
        // 実装省略
        return "SecureStorageKey";
    }
}
```

### 5.2 安全なファイル保存

設定やデータをファイルに保存する際の安全な方法です。

```csharp
// 安全なファイル保存
public static bool SaveToSecureFile(string filePath, string content, string password = null)
{
    try
    {
        // パスワードの取得（指定がなければマシン固有情報から生成）
        string encryptionKey = string.IsNullOrEmpty(password)
            ? GetMachineSpecificKey()
            : password;
            
        // 内容の暗号化
        string encryptedContent = EncryptionHelper.EncryptWithAES(content, encryptionKey);
        
        // 安全な書き込み（一時ファイル経由）
        string tempFile = Path.GetTempFileName();
        File.WriteAllText(tempFile, encryptedContent);
        
        // ディレクトリの確認
        string directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
        
        // 既存ファイルのバックアップと置き換え
        if (File.Exists(filePath))
        {
            string backupFile = filePath + ".bak";
            if (File.Exists(backupFile))
                File.Delete(backupFile);
                
            File.Move(filePath, backupFile);
        }
        
        // 一時ファイルを移動
        File.Move(tempFile, filePath);
        
        return true;
    }
    catch (Exception ex)
    {
        Logger.Instance.LogError($"安全なファイル保存エラー: {ex.Message}");
        return false;
    }
}
```

## 6. メモリ内保護

### 6.1 機密データの安全な取り扱い

メモリ内の機密データを安全に管理する方法です。

```csharp
// 安全な文字列クラス
public sealed class SecureString : IDisposable
{
    private byte[] _encryptedData;
    private bool _isDisposed = false;
    
    // 機密文字列の設定
    public void SetValue(string value)
    {
        if (_isDisposed)
            throw new ObjectDisposedException(nameof(SecureString));
            
        if (string.IsNullOrEmpty(value))
        {
            _encryptedData = null;
            return;
        }
        
        // メモリ内暗号化
        _encryptedData = ProtectInMemory(Encoding.UTF8.GetBytes(value));
    }
    
    // 機密文字列の取得
    public string GetValue()
    {
        if (_isDisposed)
            throw new ObjectDisposedException(nameof(SecureString));
            
        if (_encryptedData == null)
            return null;
            
        // メモリ内復号化
        byte[] decryptedData = UnprotectInMemory(_encryptedData);
        
        try
        {
            return Encoding.UTF8.GetString(decryptedData);
        }
        finally
        {
            // 復号後データのゼロクリア
            Array.Clear(decryptedData, 0, decryptedData.Length);
        }
    }
    
    // リソース解放
    public void Dispose()
    {
        if (!_isDisposed)
        {
            if (_encryptedData != null)
            {
                Array.Clear(_encryptedData, 0, _encryptedData.Length);
                _encryptedData = null;
            }
            
            _isDisposed = true;
        }
    }
    
    // メモリ内保護（DPAPI使用）
    private byte[] ProtectInMemory(byte[] data)
    {
        return ProtectedMemory.Protect(data, MemoryProtectionScope.SameProcess);
    }
    
    // メモリ内復号（DPAPI使用）
    private byte[] UnprotectInMemory(byte[] data)
    {
        return ProtectedMemory.Unprotect(data, MemoryProtectionScope.SameProcess);
    }
}
```

### 6.2 APIキーのメモリ内保護

APIキーをメモリ内で安全に管理するための拡張機能です。

```csharp
// APIキーのメモリ保護拡張
public class ProtectedApiKeyCache
{
    private readonly Dictionary<string, SecureString> _cachedKeys = new Dictionary<string, SecureString>();
    private readonly TimeSpan _cacheExpiration = TimeSpan.FromMinutes(30);
    private readonly Timer _cleanupTimer;
    private readonly object _lockObject = new object();
    
    // コンストラクタ
    public ProtectedApiKeyCache()
    {
        // 定期的なクリーンアップタイマー
        _cleanupTimer = new Timer(CleanupExpiredCache, null, 
            TimeSpan.FromMinutes(15), TimeSpan.FromMinutes(15));
    }
    
    // APIキーのキャッシュ
    public void CacheApiKey(string cacheKey, string apiKey)
    {
        lock (_lockObject)
        {
            if (!_cachedKeys.TryGetValue(cacheKey, out var secureString))
            {
                secureString = new SecureString();
                _cachedKeys[cacheKey] = secureString;
            }
            
            secureString.SetValue(apiKey);
        }
    }
    
    // キャッシュから取得
    public string GetApiKey(string cacheKey)
    {
        lock (_lockObject)
        {
            if (_cachedKeys.TryGetValue(cacheKey, out var secureString))
            {
                return secureString.GetValue();
            }
            
            return null;
        }
    }
    
    // 期限切れキャッシュのクリーンアップ
    private void CleanupExpiredCache(object state)
    {
        lock (_lockObject)
        {
            foreach (var key in _cachedKeys.Keys.ToList())
            {
                // 実際の実装では有効期限の管理が必要
                var secureString = _cachedKeys[key];
                secureString.Dispose();
                _cachedKeys.Remove(key);
            }
        }
    }
    
    // リソース解放
    public void Dispose()
    {
        _cleanupTimer?.Dispose();
        
        lock (_lockObject)
        {
            foreach (var secureString in _cachedKeys.Values)
            {
                secureString.Dispose();
            }
            
            _cachedKeys.Clear();
        }
    }
}
```

## 7. エラー処理と回復

### 7.1 セキュリティエラーの種類

セキュリティ関連の主なエラータイプです。

```csharp
// 暗号化例外
public class EncryptionException : Exception
{
    public EncryptionException(string message) 
        : base(message) { }
        
    public EncryptionException(string message, Exception innerException) 
        : base(message, innerException) { }
}

// ライセンス例外
public class LicenseException : Exception
{
    public LicenseException(string message) 
        : base(message) { }
        
    public LicenseException(string message, Exception innerException) 
        : base(message, innerException) { }
}

// APIキー例外
public class ApiKeyException : Exception
{
    public ApiKeyException(string message) 
        : base(message) { }
        
    public ApiKeyException(string message, Exception innerException) 
        : base(message, innerException) { }
}
```

### 7.2 回復メカニズム

エラー発生時の回復戦略です。

```csharp
// 回復メカニズムの実装例
public static class RecoveryManager
{
    // APIキー回復
    public static string RecoverApiKey(string provider, string keyId = "default")
    {
        try
        {
            // 1. まずキャッシュから試行
            var cachedKey = ApiKeyCache.Instance.GetApiKey($"{provider}_{keyId}");
            if (!string.IsNullOrEmpty(cachedKey))
                return cachedKey;
                
            // 2. リソースから試行（LocalMachineスコープ）
            var resourceKey = ApiMultiKeyProtector.Instance.GetApiKey(provider, keyId);
            if (!string.IsNullOrEmpty(resourceKey))
                return resourceKey;
                
            // 3. 代替リソースから試行（CurrentUserスコープ）
            var alternativeKey = TryGetAlternativeApiKey(provider, keyId);
            if (!string.IsNullOrEmpty(alternativeKey))
                return alternativeKey;
                
            // 4. デバッグモードならフォールバックキー
            if (AppSettings.Instance.DebugModeEnabled)
            {
                return GetDebugModeKey(provider);
            }
            
            return null;
        }
        catch (Exception ex)
        {
            Logger.Instance.LogError($"APIキー回復エラー: {ex.Message}");
            
            // 最終手段としてハードコードされたテスト用キー（開発環境のみ）
            #if DEBUG
            return GetEmergencyFallbackKey(provider);
            #else
            return null;
            #endif
        }
    }
    
    // 暗号化回復
    public static bool RecoverEncryptedData(string filePath, out string decryptedContent)
    {
        decryptedContent = null;
        
        try
        {
            // 1. 通常の復号化を試行
            if (TryDecryptFile(filePath, GetMachineSpecificKey(), out decryptedContent))
                return true;
                
            // 2. バックアップファイルからの復号化を試行
            string backupFile = filePath + ".bak";
            if (File.Exists(backupFile) && 
                TryDecryptFile(backupFile, GetMachineSpecificKey(), out decryptedContent))
                return true;
                
            // 3. 代替キーでの復号化を試行
            if (TryDecryptFile(filePath, GetAlternativeKey(), out decryptedContent))
                return true;
                
            return false;
        }
        catch (Exception ex)
        {
            Logger.Instance.LogError($"暗号化データ回復エラー: {ex.Message}");
            return false;
        }
    }
    
    // ライセンス回復
    public static bool RecoverLicenseState()
    {
        try
        {
            // 1. 通常のライセンスロードを試行
            if (LicenseManager.Instance.LoadLicenseState())
                return true;
                
            // 2. バックアップからのロードを試行
            if (LicenseManager.Instance.LoadLicenseStateFromBackup())
                return true;
                
            // 3. 以前のバージョンの形式からのロードを試行
            if (LicenseManager.Instance.TryMigrateLegacyLicense())
                return true;
                
            // 回復失敗時は無料ライセンスとして扱う
            Logger.Instance.LogWarning("ライセンス状態の回復に失敗しました。無料ライセンスとして実行します。");
            return false;
        }
        catch (Exception ex)
        {
            Logger.Instance.LogError($"ライセンス回復エラー: {ex.Message}");
            return false;
        }
    }
}
```

## 8. セキュリティ検証とテスト

### 8.1 セキュリティテスト

セキュリティ機能の検証方法です。

```csharp
// 暗号化検証テスト
public static bool VerifyEncryption()
{
    try
    {
        // テストデータ
        string testData = "Test encryption data " + Guid.NewGuid().ToString();
        
        // DPAPI暗号化テスト
        byte[] encrypted = EncryptionHelper.EncryptWithDPAPI(testData, DataProtectionScope.CurrentUser);
        string decrypted = EncryptionHelper.DecryptWithDPAPI(encrypted, DataProtectionScope.CurrentUser);
        
        if (testData != decrypted)
            return false;
            
        // AES暗号化テスト
        string encryptedAes = EncryptionHelper.EncryptWithAES(testData, "TestKey");
        string decryptedAes = EncryptionHelper.DecryptWithAES(encryptedAes, "TestKey");
        
        if (testData != decryptedAes)
            return false;
            
        return true;
    }
    catch (Exception ex)
    {
        Logger.Instance.LogError($"暗号化検証エラー: {ex.Message}");
        return false;
    }
}

// APIキー管理検証
public static bool VerifyApiKeyManagement()
{
    try
    {
        // テストキー
        string testProvider = "test";
        string testKeyId = "verification";
        string testApiKey = "test-api-key-" + Guid.NewGuid().ToString();
        
        // テストキーの保存
        bool saveResult = ApiMultiKeyProtector.Instance.SaveCustomApiKey(
            testProvider, testApiKey, testKeyId);
            
        if (!saveResult)
            return false;
            
        // テストキーの取得
        string retrievedKey = ApiMultiKeyProtector.Instance.GetApiKey(
            testProvider, testKeyId);
            
        // 検証
        bool isValid = testApiKey == retrievedKey;
        
        // テストキーの削除
        ApiMultiKeyProtector.Instance.RemoveCustomApiKey(
            testProvider, testKeyId);
            
        return isValid;
    }
    catch (Exception ex)
    {
        Logger.Instance.LogError($"APIキー管理検証エラー: {ex.Message}");
        return false;
    }
}
```

### 8.2 暗号化アルゴリズムの強度検証

使用する暗号化アルゴリズムの強度を検証します。

```csharp
// 暗号化強度の検証
public static void VerifyCryptographicStrength()
{
    // AES暗号化強度テスト
    using (Aes aes = Aes.Create())
    {
        // キーサイズ確認（256ビットが望ましい）
        if (aes.LegalKeySizes.Any(size => size.MaxSize >= 256))
        {
            aes.KeySize = 256;
        }
        else
        {
            Logger.Instance.LogWarning($"256ビットAES暗号化がサポートされていません。最大サイズ: {aes.LegalKeySizes.Max(s => s.MaxSize)}ビット");
        }
        
        // ブロックサイズ確認
        if (aes.LegalBlockSizes.Any(size => size.MaxSize >= 128))
        {
            aes.BlockSize = 128;
        }
        
        // モード確認（CBCまたはGCMが望ましい）
        aes.Mode = CipherMode.CBC;
        
        // パディング確認
        aes.Padding = PaddingMode.PKCS7;
        
        Logger.Instance.LogInfo($"AES設定 - キーサイズ: {aes.KeySize}ビット, ブロックサイズ: {aes.BlockSize}ビット, モード: {aes.Mode}, パディング: {aes.Padding}");
    }
    
    // DPAPI強度テスト（環境依存）
    try
    {
        byte[] testData = Encoding.UTF8.GetBytes("DPAPI test data");
        
        // LocalMachineスコープでのDPAPI暗号化
        byte[] machineProtected = ProtectedData.Protect(testData, null, DataProtectionScope.LocalMachine);
        
        // CurrentUserスコープでのDPAPI暗号化
        byte[] userProtected = ProtectedData.Protect(testData, null, DataProtectionScope.CurrentUser);
        
        // 暗号化データの比較（異なるべき）
        bool isDifferent = !machineProtected.SequenceEqual(userProtected);
        
        Logger.Instance.LogInfo($"DPAPI検証 - マシンスコープとユーザースコープで異なる暗号化結果: {isDifferent}");
    }
    catch (Exception ex)
    {
        Logger.Instance.LogWarning($"DPAPI検証エラー: {ex.Message}");
    }
}
```

## 9. APIキー発行・更新ツール

開発者向けのAPIキー管理ツールです。

```csharp
// APIキー暗号化ツール（開発者向け）
public class KeyEncryptionTool
{
    // APIキーの暗号化
    public static string EncryptApiKey(string apiKey, DataProtectionScope scope = DataProtectionScope.LocalMachine)
    {
        try
        {
            // APIキーの暗号化
            byte[] encryptedData = EncryptionHelper.EncryptWithDPAPI(apiKey, scope);
            
            // Base64エンコード
            return Convert.ToBase64String(encryptedData);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"暗号化エラー: {ex.Message}");
            return null;
        }
    }
    
    // リソースファイル用にAPIキーを暗号化
    public static void EncryptApiKeysForResources()
    {
        Console.WriteLine("リソースファイル用APIキー暗号化ツール");
        Console.WriteLine("===============================");
        
        // OpenAI API
        Console.Write("OpenAI APIキーを入力: ");
        string openaiKey = Console.ReadLine();
        
        // OpenAI Vision API
        Console.Write("OpenAI Vision APIキーを入力: ");
        string visionKey = Console.ReadLine();
        
        // Gemini API
        Console.Write("Google Gemini APIキーを入力: ");
        string geminiKey = Console.ReadLine();
        
        // 暗号化
        string encryptedOpenAI = EncryptApiKey(openaiKey);
        string encryptedVision = EncryptApiKey(visionKey);
        string encryptedGemini = EncryptApiKey(geminiKey);
        
        // 結果表示
        Console.WriteLine("\n暗号化結果（リソースファイルに追加してください）:");
        Console.WriteLine($"EncryptedOpenAIApiKey: {encryptedOpenAI}");
        Console.WriteLine($"EncryptedVisionApiKey: {encryptedVision}");
        Console.WriteLine($"EncryptedGeminiApiKey: {encryptedGemini}");
    }
}
```

## 10. 現在の実装状況

### 10.1 実装済み機能

- [x] APIキーの暗号化・復号化基本機能
- [x] ApiKeyProtectorクラスの基本実装
- [x] ApiMultiKeyProtectorの実装
- [x] EncryptionHelperの基本機能
- [x] 設定ファイルの暗号化保存

### 10.2 開発中機能

- [ ] LicenseManagerの詳細実装
- [ ] オンラインライセンス認証機能
- [ ] より高度なAPIキー管理と共有
- [ ] メモリ内保護の強化
- [ ] セキュリティ検証ツール

### 10.3 将来の拡張計画

- [ ] 多要素認証（開発者向け）
- [ ] クラウドベースのキー管理
- [ ] ハードウェア保護の統合（TPM活用）
- [ ] キーローテーション機能
- [ ] 詳細な監査ログ機能

## 11. 関連ドキュメント

- [アーキテクチャ概要](../01-overview/architecture-summary.md) - セキュリティコンポーネントの位置づけ
- [システムアーキテクチャ](../02-design/system-architecture.md) - システム内での連携
- [OCRコンポーネント](../02-design/components/ocr-component.md) - OCR機能でのAPIキー使用
- [翻訳コンポーネント](../02-design/components/translation-component.md) - 翻訳機能でのAPIキー使用
- [ビジネスモデル](../05-deployment/business-model.md) - ライセンスプランとの連携
