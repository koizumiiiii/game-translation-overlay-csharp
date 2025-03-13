// GameTranslationOverlay/Core/Licensing/LicenseManager.cs
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.RegularExpressions;
using GameTranslationOverlay.Core.Configuration;

namespace GameTranslationOverlay.Core.Licensing
{
    /// <summary>
    /// ライセンスの種類
    /// </summary>
    public enum LicenseType
    {
        Free = 0,
        Basic = 1,
        Pro = 2
    }

    /// <summary>
    /// プレミアム機能の種類
    /// </summary>
    public enum PremiumFeature
    {
        AiTranslation,          // AI翻訳機能
        UnlimitedTranslations,  // 無制限の翻訳
        AdvancedSettings,       // 高度な設定
        CustomProfiles,         // カスタムプロファイル
        PrioritySupport         // 優先サポート
    }

    /// <summary>
    /// ライセンスを管理するクラス
    /// </summary>
    public class LicenseManager
    {
        private static LicenseManager _instance;

        /// <summary>
        /// シングルトンインスタンスを取得
        /// </summary>
        public static LicenseManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new LicenseManager();
                }
                return _instance;
            }
        }

        // 現在のライセンスタイプ
        private LicenseType _currentLicenseType = LicenseType.Free;

        // ライセンスの有効期限
        private DateTime _licenseExpiry = DateTime.MinValue;

        // ライセンスの有効性
        private bool _isLicenseValid = false;

        // ユーザーID（有料ライセンス用）
        private string _userId = string.Empty;

        // ライセンスキーのパターン（XXXX-XXXX-XXXX-XXXX-XXXX）
        private readonly Regex _licenseKeyPattern = new Regex(@"^[A-Z0-9]{4}-[A-Z0-9]{4}-[A-Z0-9]{4}-[A-Z0-9]{4}-[A-Z0-9]{4}$");

        // 機能と必要なライセンスのマッピング
        private readonly Dictionary<PremiumFeature, LicenseType> _featureLicenseMap = new Dictionary<PremiumFeature, LicenseType>
        {
            { PremiumFeature.AiTranslation, LicenseType.Pro },
            { PremiumFeature.UnlimitedTranslations, LicenseType.Basic },
            { PremiumFeature.AdvancedSettings, LicenseType.Basic },
            { PremiumFeature.CustomProfiles, LicenseType.Pro },
            { PremiumFeature.PrioritySupport, LicenseType.Pro }
        };

        /// <summary>
        /// デバッグモードの有効/無効
        /// </summary>
        public bool DebugModeEnabled => AppSettings.Instance.DebugModeEnabled;

        /// <summary>
        /// 現在のライセンスタイプを取得
        /// </summary>
        public LicenseType CurrentLicenseType => _currentLicenseType;

        /// <summary>
        /// ライセンスが有効かどうかを取得
        /// </summary>
        public bool IsLicenseValid => _isLicenseValid || DebugModeEnabled;

        /// <summary>
        /// ライセンスの有効期限を取得
        /// </summary>
        public DateTime LicenseExpiry => _licenseExpiry;

        /// <summary>
        /// ユーザーIDを取得
        /// </summary>
        public string UserId => _userId;

        /// <summary>
        /// コンストラクタ
        /// </summary>
        private LicenseManager()
        {
            // ライセンスを検証
            VerifyLicense();
        }

        /// <summary>
        /// ライセンスを検証
        /// </summary>
        public void VerifyLicense()
        {
            try
            {
                // 設定からライセンスキーを取得
                string licenseKey = AppSettings.Instance.LicenseKey;

                // ライセンスキーが空の場合は無料版
                if (string.IsNullOrEmpty(licenseKey))
                {
                    SetFreeVersion();
                    return;
                }

                // ライセンスキーの形式を検証
                if (!_licenseKeyPattern.IsMatch(licenseKey))
                {
                    Debug.WriteLine("Invalid license key format");
                    SetFreeVersion();
                    return;
                }

                // ライセンスキーを解析
                ParseLicenseKey(licenseKey);

                // 有効期限を確認
                if (_licenseExpiry < DateTime.Now)
                {
                    Debug.WriteLine("License has expired");
                    SetFreeVersion();
                    return;
                }

                // ライセンス有効
                _isLicenseValid = true;
                Debug.WriteLine($"License verified: Type={_currentLicenseType}, Expiry={_licenseExpiry.ToShortDateString()}, UserId={_userId}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error verifying license: {ex.Message}");
                SetFreeVersion();
            }
        }

        /// <summary>
        /// 無料版に設定
        /// </summary>
        private void SetFreeVersion()
        {
            _currentLicenseType = LicenseType.Free;
            _licenseExpiry = DateTime.MinValue;
            _isLicenseValid = false;
            _userId = string.Empty;
        }

        /// <summary>
        /// ライセンスキーを解析
        /// </summary>
        private void ParseLicenseKey(string licenseKey)
        {
            try
            {
                // 実際のプロダクション環境では、より堅牢な検証ロジックを実装する
                // ここでは簡易版として、キーの一部からライセンスタイプを判定

                // 1番目のブロックに基づいてライセンスタイプを決定
                string typeBlock = licenseKey.Split('-')[0];
                char typeChar = typeBlock[0];

                switch (typeChar)
                {
                    case 'B':
                        _currentLicenseType = LicenseType.Basic;
                        break;
                    case 'P':
                        _currentLicenseType = LicenseType.Pro;
                        break;
                    default:
                        _currentLicenseType = LicenseType.Free;
                        break;
                }

                // 2番目のブロックから有効期限（月数）を取得
                string expiryBlock = licenseKey.Split('-')[1];
                int months;
                if (int.TryParse(expiryBlock.Substring(0, 2), out months))
                {
                    _licenseExpiry = DateTime.Now.AddMonths(months);
                }
                else
                {
                    // デフォルトは1か月
                    _licenseExpiry = DateTime.Now.AddMonths(1);
                }

                // 3番目と4番目のブロックからユーザーIDを生成
                string userIdBlock1 = licenseKey.Split('-')[2];
                string userIdBlock2 = licenseKey.Split('-')[3];
                _userId = $"{userIdBlock1}{userIdBlock2}";
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error parsing license key: {ex.Message}");
                SetFreeVersion();
            }
        }

        /// <summary>
        /// 指定された機能が使用可能かどうかを確認
        /// </summary>
        public bool HasFeature(PremiumFeature feature)
        {
            // デバッグモードの場合は常に機能を有効にする
            if (DebugModeEnabled)
            {
                return true;
            }

            // ライセンスが無効の場合は機能へのアクセスを拒否
            if (!_isLicenseValid && _currentLicenseType != LicenseType.Free)
            {
                return false;
            }

            // 機能に必要なライセンスタイプを取得
            LicenseType requiredLicense;
            if (!_featureLicenseMap.TryGetValue(feature, out requiredLicense))
            {
                return false; // マッピングにない機能は利用不可
            }

            // 現在のライセンスタイプが必要なライセンスタイプ以上かをチェック
            return (int)_currentLicenseType >= (int)requiredLicense;
        }

        /// <summary>
        /// ライセンスキーを設定
        /// </summary>
        public bool SetLicenseKey(string licenseKey)
        {
            try
            {
                // ライセンスキーを保存
                AppSettings.Instance.LicenseKey = licenseKey;
                AppSettings.Instance.SaveSettings();

                // ライセンスを再検証
                VerifyLicense();

                return _isLicenseValid;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error setting license key: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// ライセンスキーを検証する
        /// </summary>
        /// <param name="licenseKey">検証するライセンスキー</param>
        /// <returns>ライセンスが有効な場合はtrue</returns>
        public bool ValidateLicense(string licenseKey)
        {
            try
            {
                // ライセンスキーが空の場合は無効
                if (string.IsNullOrEmpty(licenseKey))
                {
                    Debug.WriteLine("Empty license key");
                    return false;
                }

                // ライセンスキーの形式を検証
                if (!_licenseKeyPattern.IsMatch(licenseKey))
                {
                    Debug.WriteLine("Invalid license key format");
                    return false;
                }

                // 現在のライセンス状態を保存
                LicenseType oldType = _currentLicenseType;
                DateTime oldExpiry = _licenseExpiry;
                bool oldValid = _isLicenseValid;
                string oldUserId = _userId;

                try
                {
                    // ライセンスキーを解析
                    ParseLicenseKey(licenseKey);

                    // 有効期限を確認
                    if (_licenseExpiry < DateTime.Now)
                    {
                        Debug.WriteLine("License has expired");
                        SetFreeVersion();
                        return false;
                    }

                    // ライセンス有効
                    _isLicenseValid = true;
                    Debug.WriteLine($"License validated: Type={_currentLicenseType}, Expiry={_licenseExpiry.ToShortDateString()}, UserId={_userId}");

                    // 設定を保存
                    return SetLicenseKey(licenseKey);
                }
                catch 
                {
                    // エラーが発生した場合、元の状態を復元
                    _currentLicenseType = oldType;
                    _licenseExpiry = oldExpiry;
                    _isLicenseValid = oldValid;
                    _userId = oldUserId;
                    throw;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error validating license key: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// ライセンスキーを生成（開発・テスト用）
        /// </summary>
        public string GenerateLicenseKey(LicenseType type, int months)
        {
            if (!DebugModeEnabled)
            {
                throw new InvalidOperationException("License key generation is only available in debug mode");
            }

            Random random = new Random();

            // ライセンスタイプ部分
            string typePrefix;
            switch (type)
            {
                case LicenseType.Basic:
                    typePrefix = "B";
                    break;
                case LicenseType.Pro:
                    typePrefix = "P";
                    break;
                default:
                    typePrefix = "F";
                    break;
            }

            // ランダムな文字を追加
            string block1 = typePrefix + GetRandomString(random, 3);

            // 有効期限（月数）
            string block2 = months.ToString("00") + GetRandomString(random, 2);

            // ユーザーID部分
            string block3 = GetRandomString(random, 4);
            string block4 = GetRandomString(random, 4);

            // チェックサム（簡易版）
            string block5 = GetRandomString(random, 4);

            return $"{block1}-{block2}-{block3}-{block4}-{block5}";
        }

        /// <summary>
        /// ランダムな文字列を生成
        /// </summary>
        private string GetRandomString(Random random, int length)
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            char[] result = new char[length];

            for (int i = 0; i < length; i++)
            {
                result[i] = chars[random.Next(chars.Length)];
            }

            return new string(result);
        }
    }
}