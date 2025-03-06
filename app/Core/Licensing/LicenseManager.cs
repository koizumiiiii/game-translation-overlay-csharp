using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using GameTranslationOverlay.Core.Configuration;
using GameTranslationOverlay.Core.Security;

namespace GameTranslationOverlay.Core.Licensing
{
    /// <summary>
    /// アプリケーションのライセンス状態を管理するクラス
    /// </summary>
    public class LicenseManager
    {
        // シングルトンインスタンス
        private static LicenseManager _instance;

        // ライセンスタイプの定義
        public enum LicenseType
        {
            Free,       // 無料版
            Basic,      // Basicプラン
            Pro         // Proプラン
        }

        // 有料機能の定義
        public enum PremiumFeature
        {
            AiTranslation,          // AI翻訳
            UnlimitedTranslations,  // 無制限翻訳
            AdvancedSettings,       // 詳細設定
            CustomProfiles,         // カスタムプロファイル
            PrioritySupport         // 優先サポート
        }

        // 機能と必要なライセンスのマッピング
        private readonly Dictionary<PremiumFeature, LicenseType> _featureLicenseMap;

        // 現在のライセンスタイプ
        private LicenseType _currentLicenseType = LicenseType.Free;

        // ライセンスキーの有効期限
        private DateTime? _licenseExpiration = null;

        // ライセンス検証状態
        private bool _isLicenseValid = false;

        // ユーザーID（ライセンスに紐づく）
        private string _userId = string.Empty;

        // ライセンスキーの署名に使用するハッシュキー
        private static readonly byte[] _licenseSignatureKey = {
            0x3A, 0x72, 0xF8, 0x4D, 0x9E, 0xC5, 0x7B, 0x2A,
            0x1D, 0x6B, 0x5F, 0x89, 0xA3, 0xE7, 0x10, 0x9C
        };

        // シングルトンアクセサ
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

        /// <summary>
        /// コンストラクタ - プライベートにしてシングルトンパターンを強制
        /// </summary>
        private LicenseManager()
        {
            // 機能と必要なライセンスのマッピングを初期化
            _featureLicenseMap = new Dictionary<PremiumFeature, LicenseType>
            {
                { PremiumFeature.AiTranslation, LicenseType.Pro },
                { PremiumFeature.UnlimitedTranslations, LicenseType.Basic },
                { PremiumFeature.AdvancedSettings, LicenseType.Basic },
                { PremiumFeature.CustomProfiles, LicenseType.Pro },
                { PremiumFeature.PrioritySupport, LicenseType.Pro }
            };

            // 保存されているライセンスキーを検証
            string licenseKey = AppSettings.Instance.LicenseKey;
            if (!string.IsNullOrEmpty(licenseKey))
            {
                VerifyLicense(licenseKey);
            }
        }

        /// <summary>
        /// 現在のライセンスタイプを取得
        /// </summary>
        public LicenseType CurrentLicenseType => _currentLicenseType;

        /// <summary>
        /// ライセンスが有効かどうかを取得
        /// </summary>
        public bool IsLicenseValid => _isLicenseValid;

        /// <summary>
        /// ライセンスの有効期限を取得
        /// </summary>
        public DateTime? LicenseExpiration => _licenseExpiration;

        /// <summary>
        /// ユーザーIDを取得
        /// </summary>
        public string UserId => _userId;

        /// <summary>
        /// ライセンスキーを設定し、検証する
        /// </summary>
        /// <param name="licenseKey">ライセンスキー</param>
        /// <returns>ライセンスが有効な場合はtrue</returns>
        public bool SetLicense(string licenseKey)
        {
            if (string.IsNullOrEmpty(licenseKey))
            {
                ResetLicense();
                return false;
            }

            bool isValid = VerifyLicense(licenseKey);
            if (isValid)
            {
                // 有効なライセンスキーを設定に保存
                AppSettings.Instance.LicenseKey = licenseKey;
                AppSettings.Instance.Save();
                Debug.WriteLine($"ライセンスキーを保存しました: タイプ={_currentLicenseType}, 有効期限={_licenseExpiration}");
            }
            else
            {
                Debug.WriteLine("無効なライセンスキーです。");
            }

            return isValid;
        }

        /// <summary>
        /// ライセンスをリセットして無料版に戻す
        /// </summary>
        public void ResetLicense()
        {
            _currentLicenseType = LicenseType.Free;
            _licenseExpiration = null;
            _isLicenseValid = false;
            _userId = string.Empty;

            // 設定からライセンスキーを削除
            AppSettings.Instance.LicenseKey = string.Empty;
            AppSettings.Instance.Save();
            Debug.WriteLine("ライセンスをリセットしました。無料版に戻ります。");
        }

        /// <summary>
        /// 特定の機能が現在のライセンスで利用可能かどうかをチェック
        /// </summary>
        /// <param name="feature">チェックする機能</param>
        /// <returns>機能が利用可能な場合はtrue</returns>
        public bool HasFeature(PremiumFeature feature)
        {
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
        /// AI翻訳機能が利用可能かどうかをチェック
        /// </summary>
        /// <returns>AI翻訳が利用可能な場合はtrue</returns>
        public bool CanUseAiTranslation()
        {
            return HasFeature(PremiumFeature.AiTranslation);
        }

        /// <summary>
        /// デバッグモード中はAI翻訳機能をオーバーライドして有効にするかどうか
        /// </summary>
        /// <returns>デバッグモードでオーバーライドされる場合はtrue</returns>
        public bool IsAiTranslationOverridden()
        {
            return AppSettings.Instance.DebugModeEnabled && AppSettings.Instance.UseAITranslation;
        }

        /// <summary>
        /// 無制限翻訳機能が利用可能かどうかをチェック
        /// </summary>
        /// <returns>無制限翻訳が利用可能な場合はtrue</returns>
        public bool HasUnlimitedTranslations()
        {
            return HasFeature(PremiumFeature.UnlimitedTranslations);
        }

        /// <summary>
        /// ライセンスキーを検証する
        /// </summary>
        /// <param name="licenseKey">検証するライセンスキー</param>
        /// <returns>ライセンスが有効な場合はtrue</returns>
        private bool VerifyLicense(string licenseKey)
        {
            try
            {
                // ライセンスキーのフォーマットをチェック
                if (!IsValidLicenseFormat(licenseKey))
                {
                    Debug.WriteLine("ライセンスキーのフォーマットが無効です。");
                    _isLicenseValid = false;
                    return false;
                }

                // ライセンスキーを解析
                var licenseData = ParseLicenseKey(licenseKey);
                if (licenseData == null)
                {
                    Debug.WriteLine("ライセンスキーの解析に失敗しました。");
                    _isLicenseValid = false;
                    return false;
                }

                // ライセンスデータを展開
                _currentLicenseType = licenseData.Item1;
                _licenseExpiration = licenseData.Item2;
                _userId = licenseData.Item3;

                // 有効期限をチェック
                if (_licenseExpiration.HasValue && _licenseExpiration.Value < DateTime.Now)
                {
                    Debug.WriteLine($"ライセンスの有効期限が切れています: {_licenseExpiration.Value}");
                    _isLicenseValid = false;
                    return false;
                }

                _isLicenseValid = true;
                Debug.WriteLine($"ライセンスキーの検証に成功しました: タイプ={_currentLicenseType}, 有効期限={_licenseExpiration}, ユーザーID={_userId}");
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ライセンスキーの検証中にエラーが発生しました: {ex.Message}");
                _isLicenseValid = false;
                return false;
            }
        }

        /// <summary>
        /// ライセンスキーのフォーマットが有効かどうかをチェック
        /// </summary>
        /// <param name="licenseKey">チェックするライセンスキー</param>
        /// <returns>フォーマットが有効な場合はtrue</returns>
        private bool IsValidLicenseFormat(string licenseKey)
        {
            // フォーマット: XXXX-XXXX-XXXX-XXXX-XXXX (Xは英数字)
            // 実際のアプリケーションではより複雑なフォーマットを使用することを推奨
            return !string.IsNullOrEmpty(licenseKey) &&
                   Regex.IsMatch(licenseKey, @"^[A-Z0-9]{4}-[A-Z0-9]{4}-[A-Z0-9]{4}-[A-Z0-9]{4}-[A-Z0-9]{4}$");
        }

        /// <summary>
        /// ライセンスキーを解析し、ライセンス情報を取得
        /// </summary>
        /// <param name="licenseKey">解析するライセンスキー</param>
        /// <returns>ライセンスタイプ、有効期限、ユーザーIDのタプル。解析失敗時はnull</returns>
        private Tuple<LicenseType, DateTime?, string> ParseLicenseKey(string licenseKey)
        {
            try
            {
                // ハイフンを除去
                string keyWithoutHyphens = licenseKey.Replace("-", "");

                // Base64エンコードされた部分を取得（最初の16文字はハッシュ用に予約）
                string encodedData = keyWithoutHyphens.Substring(0, 16);

                // シンプルな例として、最初の文字からライセンスタイプを判定
                // 実際のアプリケーションではより堅牢な方法を使用することを推奨
                char typeChar = encodedData[0];
                LicenseType licenseType = LicenseType.Free;

                switch (typeChar)
                {
                    case 'B':
                        licenseType = LicenseType.Basic;
                        break;
                    case 'P':
                        licenseType = LicenseType.Pro;
                        break;
                }

                // 有効期限（例として現在から1年後）
                DateTime? expiration = DateTime.Now.AddYears(1);

                // ユーザーID（例として簡易的な実装）
                string userId = $"USER-{encodedData.Substring(1, 8)}";

                return new Tuple<LicenseType, DateTime?, string>(licenseType, expiration, userId);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ライセンスキーの解析中にエラーが発生しました: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// ライセンスキーを生成する（開発用・テスト用）
        /// </summary>
        /// <param name="licenseType">ライセンスタイプ</param>
        /// <param name="validityDays">有効期間（日数）</param>
        /// <returns>生成されたライセンスキー</returns>
        public static string GenerateLicenseKey(LicenseType licenseType, int validityDays = 365)
        {
            try
            {
                // タイプに基づく接頭辞
                char typePrefix;
                switch (licenseType)
                {
                    case LicenseType.Basic:
                        typePrefix = 'B';
                        break;
                    case LicenseType.Pro:
                        typePrefix = 'P';
                        break;
                    default:
                        typePrefix = 'F';
                        break;
                }

                // ランダムなユーザーID部分
                byte[] randomBytes = new byte[8];
                using (var rng = RandomNumberGenerator.Create())
                {
                    rng.GetBytes(randomBytes);
                }
                string randomPart = BitConverter.ToString(randomBytes).Replace("-", "");

                // 有効期限
                DateTime expiration = DateTime.Now.AddDays(validityDays);
                string expirationString = expiration.ToString("yyyyMMdd");

                // キーの構成部分
                string keyData = $"{typePrefix}{randomPart.Substring(0, 8)}{expirationString}";

                // キーを5つのグループに分割してフォーマット
                string formattedKey = string.Empty;
                for (int i = 0; i < keyData.Length; i += 4)
                {
                    if (i + 4 <= keyData.Length)
                    {
                        formattedKey += keyData.Substring(i, 4) + "-";
                    }
                    else
                    {
                        formattedKey += keyData.Substring(i);
                    }
                }

                // 末尾のハイフンを削除
                formattedKey = formattedKey.TrimEnd('-');

                return formattedKey;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ライセンスキー生成中にエラーが発生しました: {ex.Message}");
                return string.Empty;
            }
        }
    }
}