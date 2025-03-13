using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using Newtonsoft.Json;

namespace GameTranslationOverlay.Core.Configuration
{
    /// <summary>
    /// アプリケーションの設定を管理するクラス
    /// </summary>
    public class AppSettings
    {
        // シングルトンインスタンス
        private static AppSettings _instance;
        public static AppSettings Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = LoadSettings();
                }
                return _instance;
            }
        }

        // 設定ファイルのデフォルトパス
        private static readonly string DefaultSettingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "GameTranslationOverlay",
            "settings.json");

        // インスタンスを作成済みかどうか
        private static bool _hasCreatedDirectory = false;

        #region 共通設定

        /// <summary>
        /// アプリケーションのバージョン
        /// </summary>
        public string AppVersion { get; set; } = "1.0.0";

        /// <summary>
        /// デバッグモードの有効/無効
        /// </summary>
        public bool DebugModeEnabled { get; set; } = false;

        /// <summary>
        /// ライセンスキー
        /// </summary>
        public string LicenseKey { get; set; } = string.Empty;

        #endregion

        #region OCR設定

        /// <summary>
        /// OCR信頼度閾値
        /// </summary>
        public float OcrConfidenceThreshold { get; set; } = 0.6f;

        /// <summary>
        /// デフォルトのOCR信頼度閾値
        /// </summary>
        public float DefaultOcrThreshold { get; set; } = 0.6f;

        /// <summary>
        /// OCR前処理の有効/無効
        /// </summary>
        public bool EnablePreprocessing { get; set; } = true;

        /// <summary>
        /// OCR前処理の有効/無効
        /// </summary>
        public bool EnableOcrPreprocessing { get; set; } = true;

        #endregion

        #region 翻訳設定

        /// <summary>
        /// 翻訳元の言語コード (自動検出時はnullまたは空文字列)
        /// </summary>
        public string SourceLanguage { get; set; } = string.Empty;

        /// <summary>
        /// 翻訳先の言語コード
        /// </summary>
        public string TargetLanguage { get; set; } = "ja";

        /// <summary>
        /// 言語自動検出の有効/無効
        /// </summary>
        public bool UseAutoDetect { get; set; } = true;

        /// <summary>
        /// AI翻訳の使用
        /// </summary>
        public bool UseAITranslation { get; set; } = false;

        /// <summary>
        /// 翻訳結果のキャッシュサイズ
        /// </summary>
        public int TranslationCacheSize { get; set; } = 1000;

        /// <summary>
        /// カスタムAPIキー
        /// </summary>
        public string CustomApiKey { get; set; } = string.Empty;

        #endregion

        #region UI設定

        /// <summary>
        /// テキスト領域ハイライト表示の有効/無効
        /// </summary>
        public bool ShowTextRegions { get; set; } = true;

        /// <summary>
        /// デバッグ情報表示の有効/無効
        /// </summary>
        public bool ShowDebugInfo { get; set; } = false;

        /// <summary>
        /// 翻訳ボックスの位置 (X座標)
        /// </summary>
        public int TranslationBoxX { get; set; } = 100;

        /// <summary>
        /// 翻訳ボックスの位置 (Y座標)
        /// </summary>
        public int TranslationBoxY { get; set; } = 100;

        /// <summary>
        /// 翻訳ボックスの幅
        /// </summary>
        public int TranslationBoxWidth { get; set; } = 350;

        /// <summary>
        /// 翻訳ボックスの高さ
        /// </summary>
        public int TranslationBoxHeight { get; set; } = 200;

        #endregion

        /// <summary>
        /// 設定をファイルから読み込む
        /// </summary>
        private static AppSettings LoadSettings()
        {
            try
            {
                // 設定ディレクトリを作成
                EnsureDirectoryExists();

                // 設定ファイルが存在しない場合はデフォルトを返す
                if (!File.Exists(DefaultSettingsPath))
                {
                    var defaultSettings = new AppSettings();
                    defaultSettings.SaveSettings();
                    return defaultSettings;
                }

                // 設定ファイルを読み込む
                string json = File.ReadAllText(DefaultSettingsPath);
                var settings = JsonConvert.DeserializeObject<AppSettings>(json);

                // nullの場合はデフォルトを返す
                if (settings == null)
                {
                    return new AppSettings();
                }

                Debug.WriteLine("Settings loaded successfully from: " + DefaultSettingsPath);
                return settings;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading settings: {ex.Message}");
                return new AppSettings();
            }
        }

        /// <summary>
        /// 設定をファイルに保存する
        /// </summary>
        public void SaveSettings()
        {
            try
            {
                // 設定ディレクトリを作成
                EnsureDirectoryExists();

                // JSON形式で保存
                string json = JsonConvert.SerializeObject(this, Formatting.Indented);
                File.WriteAllText(DefaultSettingsPath, json);

                Debug.WriteLine("Settings saved successfully to: " + DefaultSettingsPath);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error saving settings: {ex.Message}");
            }
        }

        /// <summary>
        /// 設定ディレクトリの存在を確認し、必要に応じて作成する
        /// </summary>
        private static void EnsureDirectoryExists()
        {
            if (!_hasCreatedDirectory)
            {
                string directoryPath = Path.GetDirectoryName(DefaultSettingsPath);
                if (!Directory.Exists(directoryPath))
                {
                    Directory.CreateDirectory(directoryPath);
                }
                _hasCreatedDirectory = true;
            }
        }

        /// <summary>
        /// 設定をデフォルトにリセットする
        /// </summary>
        public void ResetToDefaults()
        {
            // ライセンスキーのみ保持
            string licenseKey = LicenseKey;

            // デフォルト設定を適用
            var defaultSettings = new AppSettings();

            // プロパティをコピー
            foreach (var property in typeof(AppSettings).GetProperties())
            {
                // ライセンスキー以外をコピー
                if (property.Name != nameof(LicenseKey))
                {
                    property.SetValue(this, property.GetValue(defaultSettings));
                }
            }

            // 保持していた値を復元
            LicenseKey = licenseKey;

            // 設定を保存
            SaveSettings();
            Debug.WriteLine("Settings reset to defaults (keeping license key)");
        }
    }
}