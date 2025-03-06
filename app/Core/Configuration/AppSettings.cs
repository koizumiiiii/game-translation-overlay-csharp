using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using GameTranslationOverlay.Core.Security;
using Newtonsoft.Json;
using System.Diagnostics;

namespace GameTranslationOverlay.Core.Configuration
{
    /// <summary>
    /// アプリケーション設定を管理するクラス
    /// </summary>
    public class AppSettings
    {
        // 設定ファイルのデフォルトパス
        private static readonly string DefaultSettingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "GameTranslationOverlay",
            "settings.json");

        // シングルトンインスタンス
        private static AppSettings _instance;

        // シングルトンアクセサ
        public static AppSettings Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = Load();
                }
                return _instance;
            }
        }

        #region 一般設定

        /// <summary>
        /// 現在のアプリケーションバージョン
        /// </summary>
        public string AppVersion { get; set; } = "1.0.0";

        /// <summary>
        /// デバッグモードが有効かどうか
        /// </summary>
        public bool DebugModeEnabled { get; set; } = false;

        /// <summary>
        /// 起動時にアップデートを確認するかどうか
        /// </summary>
        public bool CheckForUpdatesOnStartup { get; set; } = true;

        /// <summary>
        /// システムトレイに最小化するかどうか
        /// </summary>
        public bool MinimizeToTray { get; set; } = true;

        /// <summary>
        /// Windowsスタートアップ時に自動起動するかどうか
        /// </summary>
        public bool StartWithWindows { get; set; } = false;

        #endregion

        #region OCR設定

        /// <summary>
        /// OCR処理の信頼度閾値
        /// </summary>
        public float OcrConfidenceThreshold { get; set; } = 0.6f;

        /// <summary>
        /// 画像前処理を有効にするかどうか
        /// </summary>
        public bool EnablePreprocessing { get; set; } = true;

        /// <summary>
        /// OCR処理のインターバル（ミリ秒）
        /// </summary>
        public int OcrProcessingInterval { get; set; } = 1000;

        #endregion

        #region 翻訳設定

        /// <summary>
        /// 翻訳元言語の自動検出を使用するかどうか
        /// </summary>
        public bool UseAutoDetectLanguage { get; set; } = true;

        /// <summary>
        /// デフォルトの翻訳元言語
        /// </summary>
        public string DefaultSourceLanguage { get; set; } = "en";

        /// <summary>
        /// デフォルトの翻訳先言語
        /// </summary>
        public string DefaultTargetLanguage { get; set; } = "ja";

        /// <summary>
        /// 翻訳エンジンの種類
        /// </summary>
        public string TranslationEngineType { get; set; } = "LibreTranslate";

        /// <summary>
        /// AI翻訳を使用するかどうか
        /// </summary>
        public bool UseAITranslation { get; set; } = false;

        /// <summary>
        /// AI翻訳の残りトークン数
        /// </summary>
        public int RemainingAITokens { get; set; } = 5000;

        #endregion

        #region APIキー設定

        /// <summary>
        /// カスタムAPIキーを使用するかどうか
        /// </summary>
        public bool UseCustomApiKey { get; set; } = false;

        /// <summary>
        /// 暗号化されたカスタムAPIキー
        /// </summary>
        public string EncryptedCustomApiKey { get; set; } = string.Empty;

        /// <summary>
        /// ライセンスキー
        /// </summary>
        public string LicenseKey { get; set; } = string.Empty;

        /// <summary>
        /// カスタムAPIキーを取得する
        /// </summary>
        public string GetCustomApiKey()
        {
            if (string.IsNullOrEmpty(EncryptedCustomApiKey))
            {
                return string.Empty;
            }

            try
            {
                // 簡易なオブファスケーション方式を使用
                // 注: API Keyのみの暗号化ではなく、オブファスケーションも使います
                // これはDPAPIが使えない場合のフォールバックとしても機能します
                return ApiKeyProtector.DeobfuscateApiKey(EncryptedCustomApiKey);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"カスタムAPIキーの復号化中にエラーが発生しました: {ex.Message}");
                return string.Empty;
            }
        }

        /// <summary>
        /// カスタムAPIキーを設定する
        /// </summary>
        public void SetCustomApiKey(string apiKey)
        {
            if (string.IsNullOrEmpty(apiKey))
            {
                EncryptedCustomApiKey = string.Empty;
            }
            else
            {
                try
                {
                    // 簡易なオブファスケーション方式を使用
                    EncryptedCustomApiKey = ApiKeyProtector.ObfuscateApiKey(apiKey);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"カスタムAPIキーの暗号化中にエラーが発生しました: {ex.Message}");
                    EncryptedCustomApiKey = string.Empty;
                }
            }
        }

        /// <summary>
        /// 利用可能なAPIキーを取得する
        /// </summary>
        public string GetActiveApiKey()
        {
            // カスタムAPIキーを使用する場合
            if (UseCustomApiKey)
            {
                string customKey = GetCustomApiKey();
                if (!string.IsNullOrEmpty(customKey))
                {
                    return customKey;
                }
            }

            // 組み込みAPIキーを使用する場合
            try
            {
                return ApiKeyProtector.GetEmbeddedApiKey();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"組み込みAPIキーの取得中にエラーが発生しました: {ex.Message}");
                return string.Empty;
            }
        }

        /// <summary>
        /// 有効なAPIキーがあるかどうかを確認する
        /// </summary>
        public bool HasValidApiKey()
        {
            string apiKey = GetActiveApiKey();
            return !string.IsNullOrEmpty(apiKey) && apiKey.StartsWith("sk-");
        }

        #endregion

        #region UI設定

        /// <summary>
        /// テキスト検出の視覚的表示を有効にするかどうか
        /// </summary>
        public bool ShowDetectionRectangles { get; set; } = true;

        /// <summary>
        /// 翻訳ボックスの背景色（ARGB形式の16進数文字列）
        /// </summary>
        public string TranslationBoxBackgroundColor { get; set; } = "#DD303030";

        /// <summary>
        /// 翻訳ボックスのテキスト色（ARGB形式の16進数文字列）
        /// </summary>
        public string TranslationBoxTextColor { get; set; } = "#FFFFFFFF";

        /// <summary>
        /// 翻訳ボックスのフォントサイズ
        /// </summary>
        public float TranslationBoxFontSize { get; set; } = 12.0f;

        #endregion

        #region 設定の読み込みと保存

        /// <summary>
        /// 設定をJSONファイルから読み込む
        /// </summary>
        /// <param name="filePath">設定ファイルのパス（省略時はデフォルトパス）</param>
        public static AppSettings Load(string filePath = null)
        {
            string path = filePath ?? DefaultSettingsPath;
            AppSettings settings = null;

            try
            {
                if (File.Exists(path))
                {
                    string json = File.ReadAllText(path);
                    settings = JsonConvert.DeserializeObject<AppSettings>(json);
                    Debug.WriteLine($"設定を読み込みました: {path}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"設定の読み込み中にエラーが発生しました: {ex.Message}");
            }

            // 設定ファイルが存在しない、または読み込みに失敗した場合はデフォルト設定を使用
            if (settings == null)
            {
                settings = new AppSettings();
                Debug.WriteLine("デフォルト設定を使用します");
            }

            return settings;
        }

        /// <summary>
        /// 現在の設定をJSONファイルに保存する
        /// </summary>
        /// <param name="filePath">設定ファイルのパス（省略時はデフォルトパス）</param>
        public void Save(string filePath = null)
        {
            string path = filePath ?? DefaultSettingsPath;

            try
            {
                // ディレクトリが存在しない場合は作成
                string directory = Path.GetDirectoryName(path);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                // 設定をJSON形式で保存
                string json = JsonConvert.SerializeObject(this, Formatting.Indented);
                File.WriteAllText(path, json);
                Debug.WriteLine($"設定を保存しました: {path}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"設定の保存中にエラーが発生しました: {ex.Message}");
            }
        }

        /// <summary>
        /// 設定をデフォルト値にリセットする
        /// </summary>
        public void ResetToDefaults()
        {
            _instance = new AppSettings();
            Debug.WriteLine("設定をデフォルト値にリセットしました");
        }

        #endregion
    }
}