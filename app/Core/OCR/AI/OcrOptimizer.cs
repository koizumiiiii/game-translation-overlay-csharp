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
        public async Task<OptimalSettings> OptimizeForGame(string gameTitle, Bitmap sampleScreen)
        {
            if (string.IsNullOrWhiteSpace(gameTitle))
            {
                throw new ArgumentException("Game title cannot be empty", nameof(gameTitle));
            }

            if (sampleScreen == null)
            {
                throw new ArgumentNullException(nameof(sampleScreen), "Sample screen cannot be null");
            }

            // ログ
            Debug.WriteLine($"Starting OCR optimization for: {gameTitle}");

            try
            {
                // 既存の最適化設定をチェック
                if (_optimizationHistory.TryGetValue(gameTitle, out var existingSettings))
                {
                    // 既に十分な最適化がされているか、最適化試行が多すぎる場合はスキップ
                    if (existingSettings.IsOptimized ||
                        (existingSettings.OptimizationAttempts >= MAX_OPTIMIZATION_ATTEMPTS &&
                         (DateTime.Now - existingSettings.LastOptimized).TotalHours < MIN_OPTIMIZATION_INTERVAL_HOURS))
                    {
                        Debug.WriteLine($"Using existing optimization for {gameTitle} (last optimized: {existingSettings.LastOptimized})");
                        return existingSettings;
                    }
                }

                // テキストが十分に表示されているかチェック
                bool hasSufficientText = await HasSufficientText(sampleScreen);
                if (!hasSufficientText)
                {
                    Debug.WriteLine("Insufficient text in sample image for optimization");
                    throw new InvalidOperationException("テキストが十分に表示されていません。会話やメニュー画面など、テキストが多く表示されている画面で実行してください。");
                }

                // 言語を検出（最適なAI選択のため）
                bool isJapaneseText = await DetectJapaneseText(sampleScreen, _ocrEngine);
                Debug.WriteLine($"Detected language: {(isJapaneseText ? "Japanese" : "Non-Japanese")}");

                // AIを使ってテキスト領域を抽出（「正解」データとして使用）
                List<TextRegion> aiTextRegions = new List<TextRegion>();
                try
                {
                    if (isJapaneseText)
                    {
                        // 日本語テキストの場合はGPT-4 Visionを使用
                        aiTextRegions = await _visionClient.ExtractTextWithGpt4Vision(sampleScreen);
                    }
                    else
                    {
                        // 英語などのテキストの場合はGemini Pro Visionを使用
                        aiTextRegions = await _visionClient.ExtractTextWithGeminiVision(sampleScreen);
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"プライマリAIでのテキスト抽出エラー: {ex.Message}");

                    // 代替APIを試行
                    try
                    {
                        Debug.WriteLine($"代替AIサービスを使用して再試行します...");
                        if (isJapaneseText)
                        {
                            // 代替としてGemini Visionを使用
                            aiTextRegions = await _visionClient.ExtractTextWithGeminiVision(sampleScreen);
                        }
                        else
                        {
                            // 代替としてGPT-4 Visionを使用
                            aiTextRegions = await _visionClient.ExtractTextWithGpt4Vision(sampleScreen);
                        }
                    }
                    catch (Exception fallbackEx)
                    {
                        Debug.WriteLine($"代替AIサービスでもエラー: {fallbackEx.Message}");
                        throw new InvalidOperationException($"テキスト抽出中にエラーが発生しました: {ex.Message}", ex);
                    }
                }

                if (aiTextRegions.Count == 0)
                {
                    Debug.WriteLine("AI could not detect any text in the image");
                    throw new InvalidOperationException("AIがテキストを検出できませんでした。別の画面で再試行してください。");
                }

                Debug.WriteLine($"AI detected {aiTextRegions.Count} text regions");

                // 様々なOCR設定を試す
                var testSettings = GenerateTestSettings();
                Dictionary<OptimalSettings, double> settingsScores = new Dictionary<OptimalSettings, double>();

                foreach (var settings in testSettings)
                {
                    // 進捗を記録
                    Debug.WriteLine($"Testing OCR settings: Confidence={settings.ConfidenceThreshold}, " +
                                    $"Contrast={settings.PreprocessingOptions.ContrastLevel}, " +
                                    $"Brightness={settings.PreprocessingOptions.BrightnessLevel}, " +
                                    $"Sharpness={settings.PreprocessingOptions.SharpnessLevel}");

                    // 現在の設定でOCRを実行
                    if (_ocrEngine is OcrManager ocrManager)
                    {
                        ocrManager.SetConfidenceThreshold(settings.ConfidenceThreshold);
                        // Utils名前空間からOCR名前空間に変換
                        ocrManager.SetPreprocessingOptions(settings.PreprocessingOptions);
                        ocrManager.EnablePreprocessing(true);
                    }

                    // テキスト領域を検出
                    List<TextRegion> ocrTextRegions = await _ocrEngine.DetectTextRegionsAsync(sampleScreen);
                    string ocrExtractedText = string.Join(" ", ocrTextRegions.Select(r => r.Text));
                    string aiExtractedText = string.Join(" ", aiTextRegions.Select(r => r.Text));

                    // OCR結果とAI結果の類似度を計算
                    double similarity = CalculateTextSimilarity(ocrExtractedText, aiExtractedText);
                    Debug.WriteLine($"Similarity score: {similarity:F4}");

                    // 設定と評価結果を保存
                    settingsScores[settings] = similarity;
                }

                // 最も類似度が高かった設定を取得
                if (settingsScores.Count == 0)
                {
                    Debug.WriteLine("No valid settings found during optimization");
                    throw new InvalidOperationException("最適化中に有効な設定が見つかりませんでした。");
                }

                var optimalSettings = settingsScores.OrderByDescending(pair => pair.Value).First().Key;
                optimalSettings.IsOptimized = true;
                optimalSettings.LastOptimized = DateTime.Now;

                // 既存の設定があれば更新、なければ新規追加
                if (_optimizationHistory.TryGetValue(gameTitle, out var existing))
                {
                    optimalSettings.OptimizationAttempts = existing.OptimizationAttempts + 1;
                    _optimizationHistory[gameTitle] = optimalSettings;
                }
                else
                {
                    _optimizationHistory[gameTitle] = optimalSettings;
                }

                // 設定を保存
                SaveOptimizationSettings();

                // 設定をOCRエンジンに適用
                if (_ocrEngine is OcrManager manager)
                {
                    manager.SetConfidenceThreshold(optimalSettings.ConfidenceThreshold);
                    // Utils名前空間からOCR名前空間に変換
                    manager.SetPreprocessingOptions(optimalSettings.PreprocessingOptions);
                    manager.EnablePreprocessing(true);
                }

                Debug.WriteLine($"Optimized OCR settings for {gameTitle}: " +
                               $"Confidence={optimalSettings.ConfidenceThreshold}, " +
                               $"Similarity={settingsScores[optimalSettings]:F4}");

                return optimalSettings;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error during OCR optimization: {ex.Message}");
                throw;
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
                    manager.EnablePreprocessing(true);

                    Debug.WriteLine($"Applied optimized settings for {gameTitle}");
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

        /// <summary>
        /// テキストが十分に表示されているかを確認
        /// </summary>
        /// <param name="image">チェックする画像</param>
        /// <returns>十分なテキストがある場合はtrue</returns>
        public async Task<bool> HasSufficientText(Bitmap image)
        {
            try
            {
                // 既存のOCRを使用して簡易チェック
                var regions = await _ocrEngine.DetectTextRegionsAsync(image);

                // テキスト領域数と文字数の両方をチェック
                int minTextRegions = 3; // 最低3つのテキスト領域
                int minTotalChars = 30; // 合計30文字以上

                int totalChars = regions.Sum(r => r.Text?.Length ?? 0);
                int regionCount = regions.Count;

                Debug.WriteLine($"Text sufficiency check: {regionCount} regions, {totalChars} characters");
                return regionCount >= minTextRegions && totalChars >= minTotalChars;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error checking text sufficiency: {ex.Message}");
                // エラーの場合は十分でないと判断
                return false;
            }
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// 画像から日本語テキストを検出
        /// </summary>
        /// <param name="image">検出対象の画像</param>
        /// <param name="ocrEngine">OCRエンジン</param>
        /// <returns>日本語が含まれている場合はtrue</returns>
        private async Task<bool> DetectJapaneseText(Bitmap image, IOcrEngine ocrEngine)
        {
            try
            {
                // OCRを使用してテキスト領域を検出
                var regions = await ocrEngine.DetectTextRegionsAsync(image);

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
        /// OcrNamespaceのPreprocessingOptionsをUtilsNamespaceのPreprocessingOptionsに変換
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

                if (string.IsNullOrWhiteSpace(json))
                {
                    // 復号化に失敗した場合は暗号化されていないとみなして直接読み込み
                    json = encryptedJson;
                }

                // JSONデシリアライズ
                var serializableData = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json);
                _optimizationHistory.Clear();

                foreach (var pair in serializableData)
                {
                    string gameTitle = pair.Key;
                    var element = pair.Value;

                    // 設定を復元
                    var settings = new OptimalSettings
                    {
                        ConfidenceThreshold = element.GetProperty("ConfidenceThreshold").GetSingle(),
                        PreprocessingOptions = new GameTranslationOverlay.Core.Utils.PreprocessingOptions
                        {
                            ContrastLevel = element.GetProperty("PreprocessingOptions").GetProperty("ContrastLevel").GetSingle(),
                            BrightnessLevel = element.GetProperty("PreprocessingOptions").GetProperty("BrightnessLevel").GetSingle(),
                            SharpnessLevel = element.GetProperty("PreprocessingOptions").GetProperty("SharpnessLevel").GetSingle(),
                            NoiseReduction = element.GetProperty("PreprocessingOptions").GetProperty("NoiseReduction").GetInt32(),
                            ScaleFactor = element.GetProperty("PreprocessingOptions").GetProperty("ScaleFactor").GetSingle(),
                            Threshold = element.GetProperty("PreprocessingOptions").GetProperty("Threshold").GetInt32(),
                            Padding = element.GetProperty("PreprocessingOptions").GetProperty("Padding").GetInt32()
                        },
                        LastOptimized = DateTime.Parse(element.GetProperty("LastOptimized").GetString()),
                        OptimizationAttempts = element.GetProperty("OptimizationAttempts").GetInt32(),
                        IsOptimized = element.GetProperty("IsOptimized").GetBoolean()
                    };

                    // AIの提案があれば復元
                    if (element.TryGetProperty("AiSuggestions", out var suggestionsElement))
                    {
                        var suggestions = new Dictionary<string, object>();
                        foreach (var property in suggestionsElement.EnumerateObject())
                        {
                            suggestions[property.Name] = property.Value.GetString();
                        }
                        settings.AiSuggestions = suggestions;
                    }

                    _optimizationHistory[gameTitle] = settings;
                }

                Debug.WriteLine($"Loaded OCR optimization settings for {_optimizationHistory.Count} games");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading OCR optimization settings: {ex.Message}");
                // 読み込みに失敗した場合は空の状態を維持
                _optimizationHistory.Clear();
            }
        }

        #endregion
    }
}