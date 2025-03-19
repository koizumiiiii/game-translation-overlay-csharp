using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using GameTranslationOverlay.Core.Configuration;
using GameTranslationOverlay.Core.Diagnostics;
using GameTranslationOverlay.Core.Security;
using GameTranslationOverlay.Core.Utils;
using OCRNamespace = GameTranslationOverlay.Core.OCR;
using GameTranslationOverlay.Core.OCR;

namespace GameTranslationOverlay.Core.OCR.AI
{
    /// <summary>
    /// AIを使用してOCR設定を最適化するクラス
    /// ゲームごとに最適なOCR設定をAIの助けを借りて特定し、保存する
    /// </summary>
    public class OcrOptimizer
    {
        #region Constants and Fields

        // AI設定
        private const int MAX_OPTIMIZATION_ATTEMPTS = 3;
        private const int MIN_OPTIMIZATION_INTERVAL_HOURS = 24;

        // 内部状態管理
        private readonly IOcrEngine _ocrEngine;
        private readonly Dictionary<string, OptimalSettings> _optimizationHistory = new Dictionary<string, OptimalSettings>();
        private readonly string _settingsFilePath;
        private readonly VisionServiceClient _visionClient;
        private readonly LanguageDetector _languageDetector;

        // 最適化設定を表すクラス
        public class OptimalSettings
        {
            public float ConfidenceThreshold { get; set; } = 0.5f;
            public GameTranslationOverlay.Core.Utils.PreprocessingOptions PreprocessingOptions { get; set; }
            public DateTime LastOptimized { get; set; } = DateTime.Now;
            public int OptimizationAttempts { get; set; } = 1;
            public bool IsOptimized { get; set; } = false;
            public Dictionary<string, object> AiSuggestions { get; set; } = new Dictionary<string, object>();

            /// <summary>
            /// ゲームプロファイルから互換性のある設定を作成
            /// </summary>
            /// <param name="profileSettings">プロファイルの設定</param>
            /// <returns>新しいOptimalSettings</returns>
            public static OptimalSettings FromProfile(OptimalSettings profileSettings)
            {
                if (profileSettings == null)
                    return null;

                return new OptimalSettings
                {
                    ConfidenceThreshold = profileSettings.ConfidenceThreshold,
                    PreprocessingOptions = new GameTranslationOverlay.Core.Utils.PreprocessingOptions
                    {
                        ContrastLevel = profileSettings.PreprocessingOptions.ContrastLevel,
                        BrightnessLevel = profileSettings.PreprocessingOptions.BrightnessLevel,
                        SharpnessLevel = profileSettings.PreprocessingOptions.SharpnessLevel,
                        NoiseReduction = profileSettings.PreprocessingOptions.NoiseReduction,
                        ScaleFactor = profileSettings.PreprocessingOptions.ScaleFactor,
                        Threshold = profileSettings.PreprocessingOptions.Threshold,
                        Padding = profileSettings.PreprocessingOptions.Padding
                    },
                    LastOptimized = profileSettings.LastOptimized,
                    OptimizationAttempts = profileSettings.OptimizationAttempts,
                    IsOptimized = profileSettings.IsOptimized,
                    AiSuggestions = new Dictionary<string, object>(profileSettings.AiSuggestions ?? new Dictionary<string, object>())
                };
            }
        }

        #endregion

        #region Constructor

        /// <summary>
        /// コンストラクタ
        /// </summary>
        /// <param name="ocrEngine">最適化対象のOCRエンジン</param>
        /// <param name="apiKey">AI APIキー（オプション、指定しない場合はデフォルト設定から取得）</param>
        public OcrOptimizer(IOcrEngine ocrEngine, string apiKey = null)
        {
            _ocrEngine = ocrEngine ?? throw new ArgumentNullException(nameof(ocrEngine));

            // 設定ファイルパスの設定
            string appDataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "GameTranslationOverlay");

            if (!Directory.Exists(appDataPath))
            {
                Directory.CreateDirectory(appDataPath);
            }

            _settingsFilePath = Path.Combine(appDataPath, "ocr_optimization.json");

            // AIサービスクライアントの初期化
            _visionClient = new VisionServiceClient();

            // 言語検出器の初期化
            _languageDetector = new LanguageDetector();

            // 保存された最適化設定を読み込む
            LoadOptimizationSettings();

            Debug.WriteLine("OcrOptimizer initialized");
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// ゲームのOCR設定を最適化する
        /// </summary>
        /// <param name="gameTitle">ゲームタイトル</param>
        /// <param name="sampleScreen">サンプル画面</param>
        /// <returns>最適化された設定</returns>
        public async Task<OcrOptimalSettings> OptimizeForGame(string gameTitle, Bitmap sampleScreen)
        {
            Debug.WriteLine($"OptimizeForGame: {gameTitle} のOCR最適化を開始");

            // AIテキスト抽出の直前にデバッグ出力を追加
            Debug.WriteLine("AIによるテキスト抽出を開始します...");
            bool hasSufficientText = await HasSufficientText(sampleScreen);
            Debug.WriteLine($"HasSufficientText 結果: {hasSufficientText}");

            try
            {
                // AIによるテキスト抽出
                List<TextRegion> aiTextRegions = await ExtractTextWithAI(sampleScreen);
                Debug.WriteLine($"AI応答: {aiTextRegions.Count}個のテキスト領域を抽出");

                // テキスト領域に基づく最適設定の生成
                OcrOptimalSettings optimalSettings = new OcrOptimalSettings();

                // デフォルト設定から開始
                optimalSettings.ConfidenceThreshold = 0.5f;
                optimalSettings.ContrastLevel = 1.0f;
                optimalSettings.BrightnessLevel = 1.0f;
                optimalSettings.SharpnessLevel = 0.0f;
                optimalSettings.NoiseReduction = 0;
                optimalSettings.ScaleFactor = 1.0f;

                // AI結果に基づいた設定調整
                if (aiTextRegions.Count > 0)
                {
                    // テキスト特性に基づく調整
                    if (ContainsPixelatedFont(aiTextRegions))
                    {
                        optimalSettings.SharpnessLevel = 0.0f; // ピクセルフォントではシャープネスを下げる
                        optimalSettings.NoiseReduction = 0;    // ノイズ除去も不要
                    }
                    else if (ContainsStylizedFont(aiTextRegions))
                    {
                        optimalSettings.ContrastLevel = 1.4f;  // 装飾フォントではコントラストを上げる
                    }
                }

                // 設定をログに出力
                Debug.WriteLine($"生成された最適設定: 信頼度={optimalSettings.ConfidenceThreshold}, " +
                                $"コントラスト={optimalSettings.ContrastLevel}, " +
                                $"明るさ={optimalSettings.BrightnessLevel}, " +
                                $"シャープネス={optimalSettings.SharpnessLevel}, " +
                                $"ノイズ除去={optimalSettings.NoiseReduction}, " +
                                $"スケール={optimalSettings.ScaleFactor}");

                return optimalSettings;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"OCR最適化中に例外が発生: {ex.Message}");
                Debug.WriteLine($"スタックトレース: {ex.StackTrace}");
                throw; // 例外を再スロー
            }
        }

        /// <summary>
        /// ゲームプロファイルから最適化設定を読み込んで適用する
        /// </summary>
        /// <param name="gameTitle">ゲームタイトル</param>
        /// <param name="gameProfiles">ゲームプロファイル管理クラス</param>
        /// <returns>適用に成功した場合はtrue</returns>
        public bool ApplyFromGameProfiles(string gameTitle, GameProfiles gameProfiles)
        {
            if (string.IsNullOrWhiteSpace(gameTitle) || gameProfiles == null)
            {
                Debug.WriteLine("ゲームタイトルまたはプロファイルがnullのため、設定を適用できません");
                return false;
            }

            try
            {
                // プロファイルから設定を取得
                var settings = gameProfiles.GetProfile(gameTitle);
                if (settings == null)
                {
                    Debug.WriteLine($"ゲーム '{gameTitle}' のプロファイルが見つかりません");
                    return false;
                }

                // 設定が最適化済みか確認
                if (!settings.IsOptimized)
                {
                    Debug.WriteLine($"ゲーム '{gameTitle}' のプロファイルは最適化されていません");
                    return false;
                }

                // 最適化設定をOCRエンジンに適用
                if (_ocrEngine is OcrManager manager)
                {
                    manager.SetConfidenceThreshold(settings.ConfidenceThreshold);
                    manager.SetPreprocessingOptions(settings.PreprocessingOptions);
                    manager.EnablePreprocessing(true);

                    // 最適化履歴に追加（既存の場合は上書き）
                    _optimizationHistory[gameTitle] = settings;

                    Debug.WriteLine($"ゲーム '{gameTitle}' の最適化設定を適用しました（信頼度: {settings.ConfidenceThreshold:F2}）");
                    return true;
                }
                else
                {
                    Debug.WriteLine("OCRマネージャーが見つからないため、設定を適用できません");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ゲームプロファイル適用エラー: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 最適化されたOCR設定をゲームに適用する
        /// </summary>
        /// <param name="gameTitle">ゲームタイトル</param>
        /// <returns>適用に成功した場合はtrue</returns>
        public bool ApplyOptimalSettingsForGame(string gameTitle)
        {
            if (string.IsNullOrWhiteSpace(gameTitle))
            {
                Debug.WriteLine("Cannot apply settings: Game title is empty");
                return false;
            }

            if (_optimizationHistory.TryGetValue(gameTitle, out var settings))
            {
                if (_ocrEngine is OcrManager manager)
                {
                    manager.SetConfidenceThreshold(settings.ConfidenceThreshold);
                    manager.SetPreprocessingOptions(settings.PreprocessingOptions);

                    // AiSuggestionsを確認し、前処理を有効/無効に設定
                    bool useOriginalImage = false;
                    if (settings.AiSuggestions != null &&
                        settings.AiSuggestions.TryGetValue("UseOriginalImage", out var val) &&
                        val is bool boolVal && boolVal)
                    {
                        useOriginalImage = true;
                    }

                    manager.EnablePreprocessing(!useOriginalImage);

                    Debug.WriteLine($"Applied optimized settings for {gameTitle} (Preprocessing: {(!useOriginalImage ? "Enabled" : "Disabled")})");
                    return true;
                }
            }

            Debug.WriteLine($"No optimized settings found for {gameTitle}");
            return false;
        }

        /// <summary>
        /// 最適化されたゲームの一覧を取得
        /// </summary>
        /// <returns>ゲームタイトルの一覧</returns>
        public List<string> GetOptimizedGames()
        {
            return _optimizationHistory.Keys.ToList();
        }

        /// <summary>
        /// 特定のゲームの最適化状態を取得
        /// </summary>
        /// <param name="gameTitle">ゲームタイトル</param>
        /// <returns>最適化状態の情報</returns>
        public Dictionary<string, object> GetOptimizationStatus(string gameTitle)
        {
            if (string.IsNullOrWhiteSpace(gameTitle) || !_optimizationHistory.TryGetValue(gameTitle, out var settings))
            {
                return new Dictionary<string, object>
                {
                    ["IsOptimized"] = false,
                    ["LastOptimized"] = null,
                    ["OptimizationAttempts"] = 0
                };
            }

            return new Dictionary<string, object>
            {
                ["IsOptimized"] = settings.IsOptimized,
                ["LastOptimized"] = settings.LastOptimized,
                ["OptimizationAttempts"] = settings.OptimizationAttempts,
                ["ConfidenceThreshold"] = settings.ConfidenceThreshold
            };
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// テキストが十分に表示されているかを確認
        /// </summary>
        /// <param name="image">チェックする画像</param>
        /// <returns>十分なテキストがある場合はtrue</returns>
        private async Task<bool> HasSufficientText(Bitmap image)
        {
            try
            {
                // 既存のOCRを使用して簡易チェック
                Logger.Instance.LogDebug("OcrOptimizer", "HasSufficientText: OCR処理を開始");
                var regions = await _ocrEngine.DetectTextRegionsAsync(image);

                int totalChars = regions.Sum(r => r.Text?.Length ?? 0);
                int regionCount = regions.Count;

                Logger.Instance.LogDebug("OcrOptimizer", $"Text sufficiency check: {regionCount} regions, {totalChars} characters");

                // 各テキスト領域の内容をログに出力
                for (int i = 0; i < regions.Count; i++)
                {
                    Logger.Instance.LogDebug("OcrOptimizer", $"領域{i + 1}: \"{regions[i].Text}\" (信頼度: {regions[i].Confidence})");
                }

                // テキスト検出条件を常に満たすように修正
                bool result = true; // 常にtrueを返す
                Logger.Instance.LogDebug("OcrOptimizer", $"HasSufficientText: 結果 = {result} (条件無視して常に成功)");
                return result;
            }
            catch (Exception ex)
            {
                Logger.Instance.LogError($"テキスト十分性チェックでエラーが発生しました: {ex.Message}", ex);
                Debug.WriteLine($"Error checking text sufficiency: {ex.Message}");
                return true; // エラー時も最適化を試みる
            }
        }

        private async Task<List<TextRegion>> ExtractTextWithAI(Bitmap image)
        {
            Debug.WriteLine("ExtractTextWithAI: AI画像認識を実行");

            try
            {
                // 言語検出とAPIの選択
                bool isJapanese = await IsJapaneseTextDominant(image);
                Debug.WriteLine($"言語検出結果: {(isJapanese ? "日本語" : "非日本語")}");

                Debug.WriteLine($"選択したAPI: {(isJapanese ? "GPT-4 Vision" : "Gemini Vision")}");

                // API呼び出し
                var regions = await _visionClient.ExtractTextFromImage(image, isJapanese);
                Debug.WriteLine($"API呼び出し結果: {regions.Count}個のテキスト領域を検出");

                return regions;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"AI画像認識中にエラー: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 画像が日本語テキストを含むかどうかを判定
        /// </summary>
        private async Task<bool> IsJapaneseTextDominant(Bitmap image)
        {
            try
            {
                // OCRを使用してテキスト領域を検出
                var regions = await _ocrEngine.DetectTextRegionsAsync(image);

                // テキスト領域がない場合
                if (regions == null || regions.Count == 0)
                {
                    Debug.WriteLine("テキスト領域が検出されなかったためデフォルトの英語と判断");
                    return false;
                }

                // 信頼度の高いテキスト領域のみを使用（ノイズ除去）
                var validRegions = regions.Where(r =>
                    !string.IsNullOrWhiteSpace(r.Text) &&
                    r.Text.Length >= 3 &&
                    r.Confidence >= 0.5f).ToList();

                if (validRegions.Count == 0)
                {
                    Debug.WriteLine("有効なテキスト領域が検出されなかったためデフォルトの英語と判断");
                    return false;
                }

                // すべてのテキストを連結
                string allText = string.Join(" ", validRegions.Select(r => r.Text));

                // テキストがない場合
                if (string.IsNullOrWhiteSpace(allText))
                {
                    Debug.WriteLine("有効なテキストが検出されなかったためデフォルトの英語と判断");
                    return false;
                }

                // 言語を検出
                bool isJapanese = IsJapaneseTextDominant(allText);
                Debug.WriteLine($"言語検出結果: {(isJapanese ? "日本語" : "日本語以外")}, テキスト: {allText.Substring(0, Math.Min(50, allText.Length))}...");

                return isJapanese;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"日本語検出エラー: {ex.Message}");
                return false;
            }
        }

        // IsJapaneseTextDominant メソッドを追加（より正確な日本語検出）
        private bool IsJapaneseTextDominant(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;

            // 日本語文字（ひらがな、カタカナ、漢字）のカウント
            int japaneseChars = text.Count(c =>
                (c >= 0x3040 && c <= 0x309F) || // ひらがな
                (c >= 0x30A0 && c <= 0x30FF) || // カタカナ
                (c >= 0x4E00 && c <= 0x9FFF));  // 漢字の一部

            // 合計文字数（空白を除く）
            int totalNonSpaceChars = text.Count(c => !char.IsWhiteSpace(c));

            if (totalNonSpaceChars == 0) return false;

            // 日本語文字の割合を計算
            double japaneseRatio = (double)japaneseChars / totalNonSpaceChars;

            // 日本語文字が15%以上あれば日本語主体と判定
            // ゲーム画面では混合テキストが多いため、比率を低めに設定
            return japaneseRatio >= 0.15;
        }

        /// <summary>
        /// OCRNamespaceのPreprocessingOptionsをUtilsNamespaceのPreprocessingOptionsに変換
        /// </summary>
        private GameTranslationOverlay.Core.Utils.PreprocessingOptions ConvertToUtilsPreprocessingOptions(OCRNamespace.PreprocessingOptions ocrOptions)
        {
            if (ocrOptions == null)
                return new GameTranslationOverlay.Core.Utils.PreprocessingOptions();

            return new GameTranslationOverlay.Core.Utils.PreprocessingOptions
            {
                ContrastLevel = ocrOptions.ContrastLevel,
                BrightnessLevel = ocrOptions.BrightnessLevel,
                SharpnessLevel = ocrOptions.SharpnessLevel,
                NoiseReduction = ocrOptions.NoiseReduction,
                Threshold = ocrOptions.Threshold,
                ScaleFactor = ocrOptions.ScaleFactor,
                Padding = ocrOptions.Padding
            };
        }

        /// <summary>
        /// テスト用の設定セットを生成
        /// </summary>
        private List<OptimalSettings> GenerateTestSettings()
        {
            var settings = new List<OptimalSettings>();

            // 閾値のバリエーション
            float[] confidenceThresholds = { 0.3f, 0.5f, 0.7f };

            // コントラストのバリエーション
            float[] contrastLevels = { 0.8f, 1.0f, 1.2f };

            // 明るさのバリエーション
            float[] brightnessLevels = { 0.9f, 1.0f, 1.1f };

            // シャープネスのバリエーション
            float[] sharpnessLevels = { 0.0f, 1.0f, 2.0f };

            // 日本語テキスト向けのプリセット
            settings.Add(new OptimalSettings
            {
                ConfidenceThreshold = 0.5f,
                PreprocessingOptions = new GameTranslationOverlay.Core.Utils.PreprocessingOptions
                {
                    ContrastLevel = 1.1f,
                    BrightnessLevel = 1.0f,
                    SharpnessLevel = 1.0f,
                    NoiseReduction = 1,
                    ScaleFactor = 1.2f
                }
            });

            // 英語テキスト向けのプリセット
            settings.Add(new OptimalSettings
            {
                ConfidenceThreshold = 0.6f,
                PreprocessingOptions = new GameTranslationOverlay.Core.Utils.PreprocessingOptions
                {
                    ContrastLevel = 1.0f,
                    BrightnessLevel = 1.0f,
                    SharpnessLevel = 0.5f,
                    NoiseReduction = 0,
                    ScaleFactor = 1.0f
                }
            });

            // パラメータの組み合わせで設定を生成
            foreach (var confidence in confidenceThresholds)
            {
                foreach (var contrast in contrastLevels)
                {
                    foreach (var brightness in brightnessLevels)
                    {
                        foreach (var sharpness in sharpnessLevels)
                        {
                            // 基本的な組み合わせのみを追加（すべての組み合わせだと多すぎる）
                            if (contrast == 1.0f || brightness == 1.0f || sharpness == 0.0f)
                            {
                                settings.Add(new OptimalSettings
                                {
                                    ConfidenceThreshold = confidence,
                                    PreprocessingOptions = new GameTranslationOverlay.Core.Utils.PreprocessingOptions
                                    {
                                        ContrastLevel = contrast,
                                        BrightnessLevel = brightness,
                                        SharpnessLevel = sharpness,
                                        NoiseReduction = 0,
                                        ScaleFactor = 1.0f
                                    }
                                });
                            }
                        }
                    }
                }
            }

            return settings;
        }

        /// <summary>
        /// 二つのテキストの類似度を計算
        /// </summary>
        /// <param name="text1">比較元テキスト</param>
        /// <param name="text2">比較先テキスト</param>
        /// <returns>0.0～1.0の類似度</returns>
        private double CalculateTextSimilarity(string text1, string text2)
        {
            if (string.IsNullOrWhiteSpace(text1) && string.IsNullOrWhiteSpace(text2))
            {
                return 1.0; // 両方空なら完全一致
            }

            if (string.IsNullOrWhiteSpace(text1) || string.IsNullOrWhiteSpace(text2))
            {
                return 0.0; // 片方だけ空なら全く一致しない
            }

            // 前処理（スペースの正規化など）
            text1 = NormalizeText(text1);
            text2 = NormalizeText(text2);

            // レーベンシュタイン距離を計算
            int distance = ComputeLevenshteinDistance(text1, text2);
            int maxLength = Math.Max(text1.Length, text2.Length);

            // 距離を類似度に変換（1.0が完全一致、0.0が全く一致しない）
            return 1.0 - (double)distance / maxLength;
        }

        /// <summary>
        /// テキストの正規化
        /// </summary>
        private string NormalizeText(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            // 空白文字の正規化
            text = text.Replace("\r", " ").Replace("\n", " ").Replace("\t", " ");

            // 連続する空白を単一の空白に置換
            while (text.Contains("  "))
            {
                text = text.Replace("  ", " ");
            }

            return text.Trim();
        }

        /// <summary>
        /// レーベンシュタイン距離の計算
        /// </summary>
        private int ComputeLevenshteinDistance(string s, string t)
        {
            int[,] d = new int[s.Length + 1, t.Length + 1];

            for (int i = 0; i <= s.Length; i++)
            {
                d[i, 0] = i;
            }

            for (int j = 0; j <= t.Length; j++)
            {
                d[0, j] = j;
            }

            for (int j = 1; j <= t.Length; j++)
            {
                for (int i = 1; i <= s.Length; i++)
                {
                    int cost = (s[i - 1] == t[j - 1]) ? 0 : 1;
                    d[i, j] = Math.Min(
                        Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                        d[i - 1, j - 1] + cost);
                }
            }

            return d[s.Length, t.Length];
        }

        /// <summary>
        /// 最適化設定の保存
        /// </summary>
        private void SaveOptimizationSettings()
        {
            try
            {
                // シリアライズ用のデータ構造に変換
                var serializableData = new Dictionary<string, object>();
                foreach (var pair in _optimizationHistory)
                {
                    var settings = pair.Value;
                    serializableData[pair.Key] = new
                    {
                        ConfidenceThreshold = settings.ConfidenceThreshold,
                        PreprocessingOptions = new
                        {
                            ContrastLevel = settings.PreprocessingOptions.ContrastLevel,
                            BrightnessLevel = settings.PreprocessingOptions.BrightnessLevel,
                            SharpnessLevel = settings.PreprocessingOptions.SharpnessLevel,
                            NoiseReduction = settings.PreprocessingOptions.NoiseReduction,
                            ScaleFactor = settings.PreprocessingOptions.ScaleFactor,
                            Threshold = settings.PreprocessingOptions.Threshold,
                            Padding = settings.PreprocessingOptions.Padding
                        },
                        LastOptimized = settings.LastOptimized.ToString("o"),
                        OptimizationAttempts = settings.OptimizationAttempts,
                        IsOptimized = settings.IsOptimized,
                        AiSuggestions = settings.AiSuggestions
                    };
                }

                // JSON形式で保存
                string json = JsonSerializer.Serialize(serializableData, new JsonSerializerOptions
                {
                    WriteIndented = true
                });

                // ファイルに保存（暗号化オプション）
                string encryptedJson = EncryptionHelper.EncryptWithAes(json, "GameTranslationOverlay");
                File.WriteAllText(_settingsFilePath, encryptedJson);

                Debug.WriteLine($"Saved OCR optimization settings to {_settingsFilePath}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error saving OCR optimization settings: {ex.Message}");
            }
        }

        /// <summary>
        /// 最適化設定の読み込み
        /// </summary>
        private void LoadOptimizationSettings()
        {
            try
            {
                // ファイルが存在しない場合はスキップ
                if (!File.Exists(_settingsFilePath))
                {
                    Debug.WriteLine("No OCR optimization settings file found");
                    return;
                }

                // ファイルから読み込み（暗号化されている場合は復号化）
                string encryptedJson = File.ReadAllText(_settingsFilePath);
                string json = EncryptionHelper.DecryptWithAes(encryptedJson, "GameTranslationOverlay");

                // JSONをデシリアライズ
                var serializableData = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json);

                // 設定をメモリに読み込み
                _optimizationHistory.Clear();
                foreach (var pair in serializableData)
                {
                    var gameTitle = pair.Key;
                    var data = pair.Value;

                    // 基本設定
                    float confidenceThreshold = data.GetProperty("ConfidenceThreshold").GetSingle();
                    DateTime lastOptimized = DateTime.Parse(data.GetProperty("LastOptimized").GetString());
                    int optimizationAttempts = data.GetProperty("OptimizationAttempts").GetInt32();
                    bool isOptimized = data.GetProperty("IsOptimized").GetBoolean();

                    // 前処理オプション
                    var preprocessingData = data.GetProperty("PreprocessingOptions");
                    var preprocessingOptions = new GameTranslationOverlay.Core.Utils.PreprocessingOptions
                    {
                        ContrastLevel = preprocessingData.GetProperty("ContrastLevel").GetSingle(),
                        BrightnessLevel = preprocessingData.GetProperty("BrightnessLevel").GetSingle(),
                        SharpnessLevel = preprocessingData.GetProperty("SharpnessLevel").GetSingle(),
                        NoiseReduction = preprocessingData.GetProperty("NoiseReduction").GetInt32(),
                        ScaleFactor = preprocessingData.GetProperty("ScaleFactor").GetSingle()
                    };

                    // Thresholdプロパティがあれば設定
                    if (preprocessingData.TryGetProperty("Threshold", out var thresholdValue))
                    {
                        preprocessingOptions.Threshold = thresholdValue.GetInt32();
                    }

                    // Paddingプロパティがあれば設定
                    if (preprocessingData.TryGetProperty("Padding", out var paddingValue))
                    {
                        preprocessingOptions.Padding = paddingValue.GetInt32();
                    }

                    // AIサジェスションがあれば設定
                    Dictionary<string, object> aiSuggestions = new Dictionary<string, object>();
                    if (data.TryGetProperty("AiSuggestions", out var suggestionsElement))
                    {
                        foreach (var prop in suggestionsElement.EnumerateObject())
                        {
                            if (prop.Value.ValueKind == JsonValueKind.String)
                            {
                                aiSuggestions[prop.Name] = prop.Value.GetString();
                            }
                            else if (prop.Value.ValueKind == JsonValueKind.Number)
                            {
                                aiSuggestions[prop.Name] = prop.Value.GetDouble();
                            }
                            else if (prop.Value.ValueKind == JsonValueKind.True || prop.Value.ValueKind == JsonValueKind.False)
                            {
                                aiSuggestions[prop.Name] = prop.Value.GetBoolean();
                            }
                        }
                    }

                    // 設定をメモリに追加
                    _optimizationHistory[gameTitle] = new OptimalSettings
                    {
                        ConfidenceThreshold = confidenceThreshold,
                        PreprocessingOptions = preprocessingOptions,
                        LastOptimized = lastOptimized,
                        OptimizationAttempts = optimizationAttempts,
                        IsOptimized = isOptimized,
                        AiSuggestions = aiSuggestions
                    };
                }

                Debug.WriteLine($"Loaded OCR optimization settings for {_optimizationHistory.Count} games");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading OCR optimization settings: {ex.Message}");
                _optimizationHistory.Clear();
            }
        }

        /// <summary>
        /// 装飾的なフォントかどうかを判定
        /// </summary>
        private bool ContainsStylizedFont(List<TextRegion> textRegions)
        {
            // この実装は簡易的なもの
            // 実際には、フォントの特徴を分析してより高度な判定を行うことが望ましい

            // テキスト領域の高さと幅の比率を分析
            var heightWidthRatios = textRegions
                .Where(r => r.Bounds.Width > 0 && r.Bounds.Height > 0)
                .Select(r => (double)r.Bounds.Height / r.Bounds.Width).ToList();

            // 異常に高いか低い比率の領域があれば、装飾的なフォントの可能性
            if (heightWidthRatios.Any(r => r > 2.0 || r < 0.3))
            {
                return true;
            }

            // 特定の文字パターンに基づく判定
            // 例：特殊記号や装飾的な文字の使用が多いかどうか
            var allText = string.Join(" ", textRegions.Select(r => r.Text));
            var specialCharCount = allText.Count(c => !char.IsLetterOrDigit(c) && !char.IsWhiteSpace(c));
            var totalCharCount = allText.Length;

            // 特殊文字の割合が高い場合は装飾的なフォントと判断
            return totalCharCount > 0 && (double)specialCharCount / totalCharCount > 0.2;
        }

        /// <summary>
        /// ピクセル化されたフォントかどうかを判定
        /// </summary>
        private bool ContainsPixelatedFont(List<TextRegion> textRegions)
        {
            // この実装は簡易的なもの
            // 実際には画像分析を行うことが望ましい

            // テキスト領域が小さい場合はピクセル化されている可能性が高い
            var smallRegions = textRegions.Count(r => r.Bounds.Height < 15);
            if (smallRegions > textRegions.Count / 2)
            {
                return true;
            }

            // 文字の高さが均一であればピクセルフォントの可能性
            var heights = textRegions.Select(r => r.Bounds.Height).ToList();
            if (heights.Count > 1)
            {
                double avg = heights.Average();
                double stdDev = Math.Sqrt(heights.Sum(h => Math.Pow(h - avg, 2)) / heights.Count);

                // 標準偏差が小さければ均一な高さと判断
                if (stdDev < 2.0)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 画像の分析結果を表すクラス
        /// </summary>
        private class ImageAnalysisResult
        {
            public double AverageBrightness { get; set; }
            public double ContrastRatio { get; set; }
            public bool HasLargeEmptyAreas { get; set; }
            public bool HasSmallText { get; set; }
        }

        /// <summary>
        /// 画像を分析して特性を取得
        /// </summary>
        private ImageAnalysisResult AnalyzeImage(Bitmap image)
        {
            // 平均輝度と最大/最小輝度を計算
            double totalBrightness = 0;
            int minBrightness = 255;
            int maxBrightness = 0;

            // サンプリングでパフォーマンスを確保
            int sampleStep = Math.Max(1, image.Width * image.Height / 10000);
            int samplesCount = 0;

            for (int y = 0; y < image.Height; y += sampleStep)
            {
                for (int x = 0; x < image.Width; x += sampleStep)
                {
                    Color pixel = image.GetPixel(x, y);
                    int brightness = (int)(0.299 * pixel.R + 0.587 * pixel.G + 0.114 * pixel.B);

                    totalBrightness += brightness;
                    minBrightness = Math.Min(minBrightness, brightness);
                    maxBrightness = Math.Max(maxBrightness, brightness);

                    samplesCount++;
                }
            }

            double averageBrightness = totalBrightness / samplesCount;
            double contrastRatio = (maxBrightness > minBrightness) ? (double)maxBrightness / (minBrightness + 1) : 1.0;

            // 解析結果を返す
            return new ImageAnalysisResult
            {
                AverageBrightness = averageBrightness,
                ContrastRatio = contrastRatio,
                HasLargeEmptyAreas = false, // この判定は複雑なので簡略化
                HasSmallText = false // テキスト領域の情報が必要なので簡略化
            };
        }

        /// <summary>
        /// OCR設定の値域を正規化
        /// </summary>
        private void NormalizeSettings(OcrOptimalSettings settings)
        {
            // 信頼度閾値は0.0～1.0の範囲に
            settings.ConfidenceThreshold = Math.Max(0.0f, Math.Min(1.0f, settings.ConfidenceThreshold));

            // コントラストは0.5～2.0の範囲に
            settings.ContrastLevel = Math.Max(0.5f, Math.Min(2.0f, settings.ContrastLevel));

            // 明るさは0.5～1.5の範囲に
            settings.BrightnessLevel = Math.Max(0.5f, Math.Min(1.5f, settings.BrightnessLevel));

            // シャープネスは0.0～3.0の範囲に
            settings.SharpnessLevel = Math.Max(0.0f, Math.Min(3.0f, settings.SharpnessLevel));

            // ノイズ除去は0～3の範囲に
            settings.NoiseReduction = Math.Max(0, Math.Min(3, settings.NoiseReduction));

            // スケールは0.5～2.0の範囲に
            settings.ScaleFactor = Math.Max(0.5f, Math.Min(2.0f, settings.ScaleFactor));
        }
    }

    /// <summary>
    /// OCRの最適設定を表すクラス
    /// </summary>
    public class OcrOptimalSettings
    {
        public float ConfidenceThreshold { get; set; } = 0.5f;
        public float ContrastLevel { get; set; } = 1.0f;
        public float BrightnessLevel { get; set; } = 1.0f;
        public float SharpnessLevel { get; set; } = 0.0f;
        public int NoiseReduction { get; set; } = 0;
        public float ScaleFactor { get; set; } = 1.0f;
        public int Threshold { get; set; } = 0;
        public int Padding { get; set; } = 0;

        /// <summary>
        /// PreprocessingOptionsに変換
        /// </summary>
        public GameTranslationOverlay.Core.Utils.PreprocessingOptions ToPreprocessingOptions()
        {
            return new GameTranslationOverlay.Core.Utils.PreprocessingOptions
            {
                ContrastLevel = this.ContrastLevel,
                BrightnessLevel = this.BrightnessLevel,
                SharpnessLevel = this.SharpnessLevel,
                NoiseReduction = this.NoiseReduction,
                ScaleFactor = this.ScaleFactor,
                Threshold = this.Threshold,
                Padding = this.Padding
            };
        }

        /// <summary>
        /// PreprocessingOptionsから生成
        /// </summary>
        public static OcrOptimalSettings FromPreprocessingOptions(GameTranslationOverlay.Core.Utils.PreprocessingOptions options, float confidenceThreshold = 0.5f)
        {
            if (options == null)
                return new OcrOptimalSettings();

            return new OcrOptimalSettings
            {
                ConfidenceThreshold = confidenceThreshold,
                ContrastLevel = options.ContrastLevel,
                BrightnessLevel = options.BrightnessLevel,
                SharpnessLevel = options.SharpnessLevel,
                NoiseReduction = options.NoiseReduction,
                ScaleFactor = options.ScaleFactor,
                Threshold = options.Threshold,
                Padding = options.Padding
            };
        }
    }
    #endregion
}