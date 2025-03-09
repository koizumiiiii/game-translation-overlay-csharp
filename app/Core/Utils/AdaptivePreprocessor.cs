using System;
using System.Collections.Generic;
using System.Drawing;
using System.Diagnostics;
using System.Text;
using OCRNamespace = GameTranslationOverlay.Core.OCR;

namespace GameTranslationOverlay.Core.Utils
{
    /// <summary>
    /// 状況に応じて画像前処理設定を自動調整するクラス
    /// </summary>
    public class AdaptivePreprocessor
    {
        // 適応型前処理オプション
        private AdaptivePreprocessorOptions _currentOptions;
        private readonly List<AdaptivePreprocessorOptions> _presets = new List<AdaptivePreprocessorOptions>();
        private int _currentPresetIndex = 0;

        // 初期化状態を追跡
        private bool _isInitialized = false;

        // OcrManagerで使用されるフィールド
        private bool _initialized = false;

        // MainForm.csで使用されるクラス変数
        public static bool AdaptivePreprocessor_initialized = false;

        /// <summary>
        /// 現在の前処理オプションを取得または設定
        /// </summary>
        public AdaptivePreprocessorOptions CurrentPreprocessingOptions
        {
            get { return _currentOptions; }
            set { _currentOptions = value ?? new AdaptivePreprocessorOptions(); }
        }

        /// <summary>
        /// コンストラクタ
        /// </summary>
        public AdaptivePreprocessor()
        {
            // 初期化
            _currentOptions = new AdaptivePreprocessorOptions();
            InitializePresets();
            _isInitialized = true;
            _initialized = true;
            AdaptivePreprocessor_initialized = true;

            Debug.WriteLine($"AdaptivePreprocessor initialized with {_presets.Count} presets");
        }

        /// <summary>
        /// 画像前処理プリセットを初期化
        /// </summary>
        private void InitializePresets()
        {
            // プリセット1: 標準（軽度のシャープニングと明るさ調整）
            var preset1 = new AdaptivePreprocessorOptions();
            preset1.ContrastLevel = 1.2f;
            preset1.BrightnessLevel = 1.1f;
            preset1.SharpnessLevel = 0.5f;
            preset1.NoiseReduction = 0;
            preset1.Threshold = 0;
            preset1.ScaleFactor = 1.0f;
            preset1.Padding = 0;
            _presets.Add(preset1);

            // プリセット2: コントラスト強調
            var preset2 = new AdaptivePreprocessorOptions();
            preset2.ContrastLevel = 1.5f;
            preset2.BrightnessLevel = 1.0f;
            preset2.SharpnessLevel = 0.3f;
            preset2.NoiseReduction = 1;
            preset2.Threshold = 0;
            preset2.ScaleFactor = 1.0f;
            preset2.Padding = 0;
            _presets.Add(preset2);

            // プリセット3: ノイズ除去強化
            var preset3 = new AdaptivePreprocessorOptions();
            preset3.ContrastLevel = 1.2f;
            preset3.BrightnessLevel = 1.0f;
            preset3.SharpnessLevel = 0.0f;
            preset3.NoiseReduction = 2;
            preset3.Threshold = 0;
            preset3.ScaleFactor = 1.0f;
            preset3.Padding = 2;
            _presets.Add(preset3);

            // プリセット4: 二値化
            var preset4 = new AdaptivePreprocessorOptions();
            preset4.ContrastLevel = 1.3f;
            preset4.BrightnessLevel = 1.1f;
            preset4.SharpnessLevel = 0.0f;
            preset4.NoiseReduction = 1;
            preset4.Threshold = 128;
            preset4.ScaleFactor = 1.0f;
            preset4.Padding = 0;
            _presets.Add(preset4);

            // プリセット5: 拡大
            var preset5 = new AdaptivePreprocessorOptions();
            preset5.ContrastLevel = 1.2f;
            preset5.BrightnessLevel = 1.0f;
            preset5.SharpnessLevel = 0.3f;
            preset5.NoiseReduction = 0;
            preset5.Threshold = 0;
            preset5.ScaleFactor = 1.5f;
            preset5.Padding = 4;
            _presets.Add(preset5);

            // 初期プリセットの設定
            if (_presets.Count > 0)
            {
                _currentOptions = _presets[0];
            }
        }

        /// <summary>
        /// 次のプリセットを試す
        /// </summary>
        /// <returns>新しいプリセットが適用されたかどうか</returns>
        public bool TryNextPreset()
        {
            if (_presets.Count <= 1)
                return false;

            // 次のプリセットインデックスを計算
            _currentPresetIndex = (_currentPresetIndex + 1) % _presets.Count;
            _currentOptions = _presets[_currentPresetIndex];

            Debug.WriteLine($"Switched to preset {_currentPresetIndex + 1}/{_presets.Count}");
            return true;
        }

        /// <summary>
        /// 指定されたPreprocessingOptionsを適用
        /// </summary>
        /// <param name="options">適用するPreprocessingOptions</param>
        public void ApplySettings(OCRNamespace.PreprocessingOptions options)
        {
            if (options == null)
                return;

            // 新しいAdaptivePreprocessorOptionsを作成して設定
            _currentOptions = new AdaptivePreprocessorOptions();
            _currentOptions.ApplySettings(options);
        }

        /// <summary>
        /// 前処理を適用する
        /// </summary>
        /// <param name="sourceBitmap">元の画像</param>
        /// <returns>前処理後の画像</returns>
        public Bitmap ProcessImage(Bitmap sourceBitmap)
        {
            if (sourceBitmap == null)
                return null;

            try
            {
                // ImagePreprocessorを使用して処理
                return ImagePreprocessor.ProcessImage(sourceBitmap, _currentOptions.ToPreprocessingOptions());
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in ProcessImage: {ex.Message}");
                return (Bitmap)sourceBitmap.Clone();
            }
        }

        /// <summary>
        /// 前処理を適用する（OcrManagerで使用される形式）
        /// </summary>
        /// <param name="sourceBitmap">元の画像</param>
        /// <returns>前処理後の画像</returns>
        public Bitmap ApplyPreprocessing(Bitmap sourceBitmap)
        {
            return ProcessImage(sourceBitmap);
        }

        /// <summary>
        /// 検出されたテキスト領域の数に基づいて設定を調整
        /// </summary>
        /// <param name="regionsCount">検出されたテキスト領域の数</param>
        public void AdjustSettings(int regionsCount)
        {
            // テキスト領域が見つからなかった場合は次のプリセットを試す
            if (regionsCount == 0)
            {
                TryNextPreset();
                Debug.WriteLine($"No text regions detected, switching to preset {_currentPresetIndex + 1}");
            }
            else
            {
                // テキスト領域が見つかった場合は現在のプリセットを維持
                Debug.WriteLine($"Found {regionsCount} text regions with preset {_currentPresetIndex + 1}");
            }
        }

        /// <summary>
        /// OCR結果の成功/失敗に基づいてプリセットを調整
        /// </summary>
        /// <param name="success">OCR処理が成功したかどうか</param>
        public void AdjustPresetBasedOnResult(bool success)
        {
            if (success)
            {
                // 成功した場合は現在のプリセットを維持
                Debug.WriteLine($"OCR successful with preset {_currentPresetIndex + 1}");
            }
            else
            {
                // 失敗した場合は次のプリセットを試す
                TryNextPreset();
                Debug.WriteLine($"OCR failed, switching to next preset {_currentPresetIndex + 1}");
            }
        }

        /// <summary>
        /// 特定のゲームプロファイルに基づいて設定を適用
        /// </summary>
        /// <param name="gameProfileName">ゲームプロファイル名</param>
        public void ApplyGameProfile(string gameProfileName)
        {
            // ゲームプロファイルに基づいて最適なプリセットを選択（実装例）
            switch (gameProfileName?.ToLower())
            {
                case "high_contrast":
                    _currentPresetIndex = 1; // コントラスト強調プリセット
                    break;
                case "noise_reduction":
                    _currentPresetIndex = 2; // ノイズ除去プリセット
                    break;
                case "binary":
                    _currentPresetIndex = 3; // 二値化プリセット
                    break;
                case "enlarged":
                    _currentPresetIndex = 4; // 拡大プリセット
                    break;
                default:
                    _currentPresetIndex = 0; // 標準プリセット
                    break;
            }

            if (_presets.Count > _currentPresetIndex)
            {
                _currentOptions = _presets[_currentPresetIndex];
                Debug.WriteLine($"Applied game profile: {gameProfileName}, using preset {_currentPresetIndex + 1}");
            }
        }

        /// <summary>
        /// デフォルト設定にリセット
        /// </summary>
        public void ResetToDefault()
        {
            _currentPresetIndex = 0;
            if (_presets.Count > 0)
            {
                _currentOptions = _presets[0];
                Debug.WriteLine("Reset to default preset");
            }
        }

        /// <summary>
        /// 状態の概要を取得
        /// </summary>
        /// <returns>現在の状態の文字列表現</returns>
        public string GetStatusSummary()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"Current Preset: {_currentPresetIndex + 1}/{_presets.Count}");
            sb.AppendLine("Settings:");
            sb.AppendLine($"  Contrast: {_currentOptions.ContrastLevel}");
            sb.AppendLine($"  Brightness: {_currentOptions.BrightnessLevel}");
            sb.AppendLine($"  Sharpness: {_currentOptions.SharpnessLevel}");
            sb.AppendLine($"  Noise Reduction: {_currentOptions.NoiseReduction}");
            sb.AppendLine($"  Threshold: {_currentOptions.Threshold}");
            sb.AppendLine($"  Scale Factor: {_currentOptions.ScaleFactor}");
            sb.AppendLine($"  Padding: {_currentOptions.Padding}");
            return sb.ToString();
        }
    }
}