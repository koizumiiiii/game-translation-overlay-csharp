using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using GameTranslationOverlay.Core.Diagnostics;
using GameTranslationOverlay.Core.OCR;
using GameTranslationOverlay.Core.OCR.AI;
using GameTranslationOverlay.Core.Security;
using GameTranslationOverlay.Core.WindowManagement;
using OCRNamespace = GameTranslationOverlay.Core.OCR;
using UtilsNamespace = GameTranslationOverlay.Core.Utils;
using System.Runtime.InteropServices;
using System.Text;

namespace GameTranslationOverlay.Core.Configuration
{
    /// <summary>
    /// ゲームごとのOCR最適化プロファイルを管理するクラス
    /// </summary>
    public class GameProfiles
    {
        #region Win32 API

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        private string GetWindowTitleText(IntPtr hWnd)
        {
            StringBuilder sb = new StringBuilder(256);
            if (GetWindowText(hWnd, sb, sb.Capacity) > 0)
            {
                return sb.ToString();
            }
            return string.Empty;
        }

        #endregion

        #region シングルトンパターン

        private static readonly Lazy<GameProfiles> _instance = new Lazy<GameProfiles>(() => new GameProfiles());

        /// <summary>
        /// GameProfilesのインスタンスを取得します
        /// </summary>
        public static GameProfiles Instance => _instance.Value;

        #endregion

        #region フィールドとプロパティ

        private readonly object _lockObject = new object();
        private readonly Dictionary<string, GameProfile> _profiles = new Dictionary<string, GameProfile>();
        private readonly Dictionary<string, string> _processNameToGame = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private readonly string _profilesFilePath;
        private bool _initialized = false;

        // 現在アクティブなゲームプロファイル
        private GameProfile _activeProfile;
        private string _activeGameTitle;

        /// <summary>
        /// 登録されているプロファイル数
        /// </summary>
        public int ProfileCount => _profiles.Count;

        /// <summary>
        /// 現在選択中のゲームタイトル
        /// </summary>
        public string ActiveGameTitle => _activeGameTitle;

        /// <summary>
        /// 初期化済みかどうか
        /// </summary>
        public bool IsInitialized => _initialized;

        #endregion

        #region コンストラクタ

        /// <summary>
        /// GameProfilesのコンストラクタ。デフォルトのプロファイル格納場所を設定します。
        /// </summary>
        private GameProfiles()
        {
            // プロファイル格納ディレクトリの設定
            string appDataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "GameTranslationOverlay");

            // ディレクトリが存在しない場合は作成
            if (!Directory.Exists(appDataPath))
            {
                Directory.CreateDirectory(appDataPath);
            }

            _profilesFilePath = Path.Combine(appDataPath, "game_profiles.json");
        }

        #endregion

        #region 公開メソッド

        /// <summary>
        /// GameProfilesを初期化します
        /// </summary>
        public void Initialize()
        {
            lock (_lockObject)
            {
                if (_initialized)
                    return;

                try
                {
                    // プロファイルを読み込む
                    LoadProfiles();
                    _initialized = true;
                    Logger.Instance.LogInfo("GameProfiles initialized successfully");
                }
                catch (Exception ex)
                {
                    Logger.Instance.LogError("Failed to initialize GameProfiles", ex);
                    _initialized = false;
                }
            }
        }

        /// <summary>
        /// 現在実行中のフォアグラウンドウィンドウからゲームを検出し、
        /// 該当するプロファイルがあれば適用します
        /// </summary>
        /// <returns>検出されたゲームタイトル（見つからない場合はnull）</returns>
        public string DetectAndApplyGameProfile()
        {
            try
            {
                // 現在アクティブなウィンドウを取得
                IntPtr activeWindowHandle = WindowSelector.GetForegroundWindow();
                if (activeWindowHandle == IntPtr.Zero)
                    return null;

                // ウィンドウのプロセスIDを取得
                uint processId;
                GetWindowThreadProcessId(activeWindowHandle, out processId);
                if (processId == 0)
                    return null;

                // プロセス名を取得
                string processName = null;
                try
                {
                    using (Process process = Process.GetProcessById((int)processId))
                    {
                        processName = process.ProcessName;
                    }
                }
                catch (Exception ex)
                {
                    Logger.Instance.LogError($"Failed to get process information: {ex.Message}", ex);
                    return null;
                }

                if (string.IsNullOrEmpty(processName))
                    return null;

                // プロセス名からゲームタイトルを検索
                string gameTitle = GetGameTitleByProcessName(processName);
                if (string.IsNullOrEmpty(gameTitle))
                {
                    // ウィンドウのタイトルも確認
                    string windowTitle = GetWindowTitleText(activeWindowHandle);
                    if (!string.IsNullOrEmpty(windowTitle))
                    {
                        gameTitle = FindGameByWindowTitle(windowTitle);
                    }
                }

                // ゲームが見つかった場合はプロファイルを適用
                if (!string.IsNullOrEmpty(gameTitle))
                {
                    if (ApplyProfile(gameTitle))
                    {
                        Logger.Instance.LogInfo($"Applied profile for game: {gameTitle}");
                        return gameTitle;
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                Logger.Instance.LogError("Error in DetectAndApplyGameProfile", ex);
                return null;
            }
        }

        /// <summary>
        /// ウィンドウタイトルからゲームを検索します
        /// </summary>
        /// <param name="windowTitle">ウィンドウタイトル</param>
        /// <returns>見つかったゲームタイトル（見つからない場合はnull）</returns>
        private string FindGameByWindowTitle(string windowTitle)
        {
            if (string.IsNullOrEmpty(windowTitle))
                return null;

            // 登録済みのゲームタイトルとウィンドウタイトルを照合
            foreach (var profile in _profiles.Values)
            {
                if (!string.IsNullOrEmpty(profile.GameTitle) &&
                    (windowTitle.IndexOf(profile.GameTitle, StringComparison.OrdinalIgnoreCase) >= 0 ||
                     profile.AlternativeTitles.Any(alt => windowTitle.IndexOf(alt, StringComparison.OrdinalIgnoreCase) >= 0)))
                {
                    return profile.GameTitle;
                }
            }

            return null;
        }

        /// <summary>
        /// 指定されたゲームのプロファイルを適用します
        /// </summary>
        /// <param name="gameTitle">ゲームタイトル</param>
        /// <returns>適用に成功した場合はtrue</returns>
        public bool ApplyProfile(string gameTitle)
        {
            if (string.IsNullOrEmpty(gameTitle))
                return false;

            lock (_lockObject)
            {
                if (!_profiles.TryGetValue(gameTitle, out GameProfile profile))
                {
                    Logger.Instance.LogWarning($"Profile not found for game: {gameTitle}");
                    return false;
                }

                try
                {
                    // OCRManagerを取得（AppSettingsから取得するか、別途受け取る方法を実装）
                    var ocrManager = GetOcrManager();
                    if (ocrManager == null)
                    {
                        Logger.Instance.LogWarning("OCR Manager is not available");
                        return false;
                    }

                    // プロファイルの設定を適用
                    ocrManager.SetConfidenceThreshold(profile.ConfidenceThreshold);
                    
                    // 前処理オプションの設定
                    var preprocessingOptions = new UtilsNamespace.PreprocessingOptions
                    {
                        ContrastLevel = profile.ContrastLevel,
                        BrightnessLevel = profile.BrightnessLevel,
                        SharpnessLevel = profile.SharpnessLevel,
                        NoiseReduction = profile.NoiseReduction,
                        ScaleFactor = profile.ScaleFactor,
                        Threshold = profile.Threshold,
                        Padding = profile.Padding
                    };
                    
                    ocrManager.SetPreprocessingOptions(preprocessingOptions);
                    ocrManager.EnablePreprocessing(profile.EnablePreprocessing);
                    
                    // 適応モードの設定
                    if (ocrManager is OcrManager manager)
                    {
                        manager.EnableAdaptiveMode(profile.EnableAdaptiveMode);
                        manager.EnableProgressiveScan(profile.EnableProgressiveScan);
                    }

                    // アクティブなプロファイルを更新
                    _activeProfile = profile;
                    _activeGameTitle = gameTitle;

                    // 最終使用日時を更新
                    profile.LastUsed = DateTime.Now;
                    SaveProfiles();

                    Logger.Instance.LogInfo($"Applied profile for '{gameTitle}' successfully");
                    return true;
                }
                catch (Exception ex)
                {
                    Logger.Instance.LogError($"Error applying profile for '{gameTitle}'", ex);
                    return false;
                }
            }
        }

        /// <summary>
        /// OCRManagerを取得します。
        /// この実装はアプリケーション構造に応じて修正が必要です。
        /// </summary>
        private OcrManager GetOcrManager()
        {
            // 実際のアプリケーションではDIコンテナから取得するなどの方法が考えられます
            // ここでは仮の実装としています
            
            // この部分は実際のアプリケーション構造に合わせて修正してください
            // 例: MainForm.Instance.OcrManager を返すなど
            
            // 現在はnullを返しますが、実際には適切なOcrManagerインスタンスを返すように実装してください
            return null;
        }

        /// <summary>
        /// 新しいゲームプロファイルを追加または更新します
        /// </summary>
        /// <param name="gameTitle">ゲームタイトル</param>
        /// <param name="processName">ゲームのプロセス名</param>
        /// <param name="ocrSettings">OCR最適化設定</param>
        /// <returns>成功した場合はtrue</returns>
        public bool AddOrUpdateProfile(string gameTitle, string processName, OcrOptimizer.OptimalSettings ocrSettings)
        {
            if (string.IsNullOrEmpty(gameTitle))
                return false;

            lock (_lockObject)
            {
                try
                {
                    // 既存のプロファイルを探す
                    if (!_profiles.TryGetValue(gameTitle, out GameProfile profile))
                    {
                        // 新しいプロファイルを作成
                        profile = new GameProfile
                        {
                            GameTitle = gameTitle,
                            ProcessName = processName,
                            Created = DateTime.Now
                        };
                        _profiles[gameTitle] = profile;
                    }

                    // プロセス名からゲームタイトルへのマッピングを更新
                    if (!string.IsNullOrEmpty(processName))
                    {
                        _processNameToGame[processName] = gameTitle;
                    }

                    // OCR設定を更新
                    if (ocrSettings != null)
                    {
                        profile.ConfidenceThreshold = ocrSettings.ConfidenceThreshold;
                        profile.ContrastLevel = ocrSettings.PreprocessingOptions.ContrastLevel;
                        profile.BrightnessLevel = ocrSettings.PreprocessingOptions.BrightnessLevel;
                        profile.SharpnessLevel = ocrSettings.PreprocessingOptions.SharpnessLevel;
                        profile.NoiseReduction = ocrSettings.PreprocessingOptions.NoiseReduction;
                        profile.ScaleFactor = ocrSettings.PreprocessingOptions.ScaleFactor;
                        profile.Threshold = ocrSettings.PreprocessingOptions.Threshold;
                        profile.Padding = ocrSettings.PreprocessingOptions.Padding;
                        profile.IsOptimized = true;
                    }

                    // 最終更新日時を更新
                    profile.LastModified = DateTime.Now;

                    // プロファイルを保存
                    SaveProfiles();

                    Logger.Instance.LogInfo($"Added/updated profile for '{gameTitle}'");
                    return true;
                }
                catch (Exception ex)
                {
                    Logger.Instance.LogError($"Failed to add/update profile for '{gameTitle}'", ex);
                    return false;
                }
            }
        }

        /// <summary>
        /// 既存のゲームプロファイルを削除します
        /// </summary>
        /// <param name="gameTitle">ゲームタイトル</param>
        /// <returns>成功した場合はtrue</returns>
        public bool DeleteProfile(string gameTitle)
        {
            if (string.IsNullOrEmpty(gameTitle))
                return false;

            lock (_lockObject)
            {
                try
                {
                    // プロファイルが存在しない場合
                    if (!_profiles.TryGetValue(gameTitle, out GameProfile profile))
                    {
                        return false;
                    }

                    // プロセス名からゲームタイトルへのマッピングを削除
                    string processName = profile.ProcessName;
                    if (!string.IsNullOrEmpty(processName) && _processNameToGame.ContainsKey(processName))
                    {
                        _processNameToGame.Remove(processName);
                    }

                    // プロファイルを削除
                    _profiles.Remove(gameTitle);

                    // アクティブプロファイルが削除対象だった場合
                    if (_activeGameTitle == gameTitle)
                    {
                        _activeGameTitle = null;
                        _activeProfile = null;
                    }

                    // プロファイルを保存
                    SaveProfiles();

                    Logger.Instance.LogInfo($"Deleted profile for '{gameTitle}'");
                    return true;
                }
                catch (Exception ex)
                {
                    Logger.Instance.LogError($"Failed to delete profile for '{gameTitle}'", ex);
                    return false;
                }
            }
        }

        /// <summary>
        /// 指定されたゲームタイトルのプロファイルを取得します
        /// </summary>
        /// <param name="gameTitle">ゲームタイトル</param>
        /// <returns>ゲームプロファイル（見つからない場合はnull）</returns>
        public GameProfile GetProfile(string gameTitle)
        {
            if (string.IsNullOrEmpty(gameTitle))
                return null;

            lock (_lockObject)
            {
                if (_profiles.TryGetValue(gameTitle, out GameProfile profile))
                {
                    return profile;
                }
                return null;
            }
        }

        /// <summary>
        /// 最適化されたゲームプロファイルの一覧を取得します
        /// </summary>
        /// <returns>最適化済みゲームタイトルのリスト</returns>
        public List<string> GetOptimizedGames()
        {
            lock (_lockObject)
            {
                return _profiles.Values
                    .Where(p => p.IsOptimized)
                    .Select(p => p.GameTitle)
                    .ToList();
            }
        }

        /// <summary>
        /// すべてのゲームプロファイルを取得します
        /// </summary>
        /// <returns>すべてのゲームプロファイルのリスト</returns>
        public List<GameProfile> GetAllProfiles()
        {
            lock (_lockObject)
            {
                return _profiles.Values.ToList();
            }
        }

        /// <summary>
        /// ゲームプロセス名からゲームタイトルを取得します
        /// </summary>
        /// <param name="processName">プロセス名</param>
        /// <returns>ゲームタイトル（見つからない場合はnull）</returns>
        public string GetGameTitleByProcessName(string processName)
        {
            if (string.IsNullOrEmpty(processName))
                return null;

            lock (_lockObject)
            {
                if (_processNameToGame.TryGetValue(processName, out string gameTitle))
                {
                    return gameTitle;
                }
                return null;
            }
        }

        /// <summary>
        /// 代替ゲームタイトル（別名）を追加します
        /// </summary>
        /// <param name="gameTitle">メインのゲームタイトル</param>
        /// <param name="alternativeTitle">代替タイトル</param>
        /// <returns>成功した場合はtrue</returns>
        public bool AddAlternativeTitle(string gameTitle, string alternativeTitle)
        {
            if (string.IsNullOrEmpty(gameTitle) || string.IsNullOrEmpty(alternativeTitle))
                return false;

            lock (_lockObject)
            {
                try
                {
                    if (!_profiles.TryGetValue(gameTitle, out GameProfile profile))
                    {
                        return false;
                    }

                    // 既に登録済みの場合はスキップ
                    if (profile.AlternativeTitles.Any(title => string.Equals(title, alternativeTitle, StringComparison.OrdinalIgnoreCase)))
                    {
                        return true;
                    }

                    // 代替タイトルを追加
                    profile.AlternativeTitles.Add(alternativeTitle);
                    profile.LastModified = DateTime.Now;

                    // プロファイルを保存
                    SaveProfiles();

                    Logger.Instance.LogInfo($"Added alternative title '{alternativeTitle}' for '{gameTitle}'");
                    return true;
                }
                catch (Exception ex)
                {
                    Logger.Instance.LogError($"Failed to add alternative title '{alternativeTitle}' for '{gameTitle}'", ex);
                    return false;
                }
            }
        }

        #endregion

        #region プロファイルの読み込みと保存

        /// <summary>
        /// プロファイルをファイルに保存します
        /// </summary>
        private void SaveProfiles()
        {
            try
            {
                lock (_lockObject)
                {
                    // シリアライズ用のデータ構造に変換
                    var serializableData = new Dictionary<string, object>();
                    foreach (var profile in _profiles.Values)
                    {
                        serializableData[profile.GameTitle] = new
                        {
                            ProcessName = profile.ProcessName,
                            AlternativeTitles = profile.AlternativeTitles,
                            ConfidenceThreshold = profile.ConfidenceThreshold,
                            ContrastLevel = profile.ContrastLevel,
                            BrightnessLevel = profile.BrightnessLevel,
                            SharpnessLevel = profile.SharpnessLevel,
                            NoiseReduction = profile.NoiseReduction,
                            ScaleFactor = profile.ScaleFactor,
                            Threshold = profile.Threshold,
                            Padding = profile.Padding,
                            EnablePreprocessing = profile.EnablePreprocessing,
                            EnableAdaptiveMode = profile.EnableAdaptiveMode,
                            EnableProgressiveScan = profile.EnableProgressiveScan,
                            IsOptimized = profile.IsOptimized,
                            Created = profile.Created.ToString("o"),
                            LastModified = profile.LastModified.ToString("o"),
                            LastUsed = profile.LastUsed.ToString("o")
                        };
                    }

                    // JSON形式でシリアライズ
                    var options = new JsonSerializerOptions { WriteIndented = true };
                    string json = JsonSerializer.Serialize(serializableData, options);

                    // 暗号化して保存
                    string encryptedJson = EncryptionHelper.EncryptWithAes(json, "GameTranslationOverlay");
                    File.WriteAllText(_profilesFilePath, encryptedJson);

                    Logger.Instance.LogInfo($"Saved {_profiles.Count} game profiles to {_profilesFilePath}");
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.LogError("Failed to save game profiles", ex);
            }
        }

        /// <summary>
        /// プロファイルをファイルから読み込みます
        /// </summary>
        private void LoadProfiles()
        {
            try
            {
                lock (_lockObject)
                {
                    // ファイルが存在しない場合はスキップ
                    if (!File.Exists(_profilesFilePath))
                    {
                        Logger.Instance.LogInfo("No game profiles file found");
                        return;
                    }

                    // ファイルを読み込み（暗号化されている場合は復号化）
                    string encryptedJson = File.ReadAllText(_profilesFilePath);
                    string json = EncryptionHelper.DecryptWithAes(encryptedJson, "GameTranslationOverlay");

                    if (string.IsNullOrWhiteSpace(json))
                    {
                        // 復号化に失敗した場合は暗号化されていないとみなして直接読み込み
                        json = encryptedJson;
                    }

                    // JSONデシリアライズ
                    var serializableData = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json);
                    _profiles.Clear();
                    _processNameToGame.Clear();

                    foreach (var pair in serializableData)
                    {
                        string gameTitle = pair.Key;
                        var element = pair.Value;

                        try
                        {
                            // プロファイルを復元
                            var profile = new GameProfile
                            {
                                GameTitle = gameTitle,
                                ProcessName = element.GetProperty("ProcessName").GetString(),
                                ConfidenceThreshold = element.GetProperty("ConfidenceThreshold").GetSingle(),
                                ContrastLevel = element.GetProperty("ContrastLevel").GetSingle(),
                                BrightnessLevel = element.GetProperty("BrightnessLevel").GetSingle(),
                                SharpnessLevel = element.GetProperty("SharpnessLevel").GetSingle(),
                                NoiseReduction = element.GetProperty("NoiseReduction").GetInt32(),
                                ScaleFactor = element.GetProperty("ScaleFactor").GetSingle(),
                                Threshold = element.GetProperty("Threshold").GetInt32(),
                                Padding = element.GetProperty("Padding").GetInt32(),
                                EnablePreprocessing = element.GetProperty("EnablePreprocessing").GetBoolean(),
                                EnableAdaptiveMode = element.TryGetProperty("EnableAdaptiveMode", out var adaptiveMode) ? adaptiveMode.GetBoolean() : true,
                                EnableProgressiveScan = element.TryGetProperty("EnableProgressiveScan", out var progressiveScan) ? progressiveScan.GetBoolean() : true,
                                IsOptimized = element.GetProperty("IsOptimized").GetBoolean(),
                                Created = DateTime.Parse(element.GetProperty("Created").GetString()),
                                LastModified = DateTime.Parse(element.GetProperty("LastModified").GetString()),
                                LastUsed = DateTime.Parse(element.GetProperty("LastUsed").GetString())
                            };

                            // 代替タイトルの読み込み
                            if (element.TryGetProperty("AlternativeTitles", out var altTitlesElement))
                            {
                                foreach (var altTitle in altTitlesElement.EnumerateArray())
                                {
                                    profile.AlternativeTitles.Add(altTitle.GetString());
                                }
                            }

                            // プロファイルを追加
                            _profiles[gameTitle] = profile;

                            // プロセス名の登録
                            if (!string.IsNullOrEmpty(profile.ProcessName))
                            {
                                _processNameToGame[profile.ProcessName] = gameTitle;
                            }
                        }
                        catch (Exception ex)
                        {
                            Logger.Instance.LogError($"Failed to parse profile for '{gameTitle}': {ex.Message}", ex);
                        }
                    }

                    Logger.Instance.LogInfo($"Loaded {_profiles.Count} game profiles from {_profilesFilePath}");
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.LogError("Failed to load game profiles", ex);
                // 読み込みに失敗した場合は空の状態を維持
                _profiles.Clear();
                _processNameToGame.Clear();
            }
        }

        #endregion

        #region ゲームプロファイルクラス

        /// <summary>
        /// ゲームごとの設定プロファイルを表すクラス
        /// </summary>
        public class GameProfile
        {
            #region 基本情報

            /// <summary>
            /// ゲームタイトル
            /// </summary>
            public string GameTitle { get; set; }

            /// <summary>
            /// ゲームの実行ファイル名（プロセス名）
            /// </summary>
            public string ProcessName { get; set; }

            /// <summary>
            /// 代替タイトル（別名）のリスト
            /// </summary>
            public List<string> AlternativeTitles { get; set; } = new List<string>();

            #endregion

            #region OCR設定

            /// <summary>
            /// OCR信頼度閾値
            /// </summary>
            public float ConfidenceThreshold { get; set; } = 0.5f;

            /// <summary>
            /// コントラストレベル
            /// </summary>
            public float ContrastLevel { get; set; } = 1.0f;

            /// <summary>
            /// 明るさレベル
            /// </summary>
            public float BrightnessLevel { get; set; } = 1.0f;

            /// <summary>
            /// シャープネスレベル
            /// </summary>
            public float SharpnessLevel { get; set; } = 0.0f;

            /// <summary>
            /// ノイズ軽減レベル
            /// </summary>
            public int NoiseReduction { get; set; } = 0;

            /// <summary>
            /// 閾値（二値化）
            /// </summary>
            public int Threshold { get; set; } = 0;

            /// <summary>
            /// スケール係数
            /// </summary>
            public float ScaleFactor { get; set; } = 1.0f;

            /// <summary>
            /// パディング
            /// </summary>
            public int Padding { get; set; } = 0;

            /// <summary>
            /// 前処理の有効/無効
            /// </summary>
            public bool EnablePreprocessing { get; set; } = true;

            /// <summary>
            /// 適応モードの有効/無効
            /// </summary>
            public bool EnableAdaptiveMode { get; set; } = true;

            /// <summary>
            /// 段階的スキャンの有効/無効
            /// </summary>
            public bool EnableProgressiveScan { get; set; } = true;

            /// <summary>
            /// 最適化済みかどうか
            /// </summary>
            public bool IsOptimized { get; set; } = false;

            #endregion

            #region メタデータ

            /// <summary>
            /// プロファイル作成日時
            /// </summary>
            public DateTime Created { get; set; } = DateTime.Now;

            /// <summary>
            /// 最終更新日時
            /// </summary>
            public DateTime LastModified { get; set; } = DateTime.Now;

            /// <summary>
            /// 最終使用日時
            /// </summary>
            public DateTime LastUsed { get; set; } = DateTime.Now;

            #endregion

            /// <summary>
            /// このプロファイルをOcrOptimizerのOptimalSettingsに変換します
            /// </summary>
            /// <returns>OCR最適化設定</returns>
            public OcrOptimizer.OptimalSettings ToOptimalSettings()
            {
                var preprocessingOptions = new UtilsNamespace.PreprocessingOptions
                {
                    ContrastLevel = ContrastLevel,
                    BrightnessLevel = BrightnessLevel,
                    SharpnessLevel = SharpnessLevel,
                    NoiseReduction = NoiseReduction,
                    ScaleFactor = ScaleFactor,
                    Threshold = Threshold,
                    Padding = Padding
                };

                return new OcrOptimizer.OptimalSettings
                {
                    ConfidenceThreshold = ConfidenceThreshold,
                    PreprocessingOptions = preprocessingOptions,
                    IsOptimized = IsOptimized,
                    LastOptimized = LastModified
                };
            }
        }

        #endregion
    }
}
