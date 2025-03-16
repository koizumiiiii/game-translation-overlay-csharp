using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using GameTranslationOverlay.Core.Configuration;
using System.IO;
using OCRNamespace = GameTranslationOverlay.Core.OCR;

namespace GameTranslationOverlay.Core.Utils
{
    /// <summary>
    /// 適応型前処理を行うクラス
    /// </summary>
    public class AdaptivePreprocessor
    {
        // 未使用警告を削除するためにフィールドを削除または使用
        // private bool _isInitialized = false;
        // private bool _initialized = false;

        private PreprocessingOptions _currentOptions;
        private List<PreprocessingOptions> _presets;
        private int _currentPresetIndex = 0;

        /// <summary>
        /// コンストラクタ
        /// </summary>
        public AdaptivePreprocessor()
        {
            // プリセットの初期化
            InitializePresets();
            _currentOptions = _presets[_currentPresetIndex];
            Debug.WriteLine("AdaptivePreprocessor: 初期化完了");
        }

        /// <summary>
        /// 現在の前処理オプションを取得または設定
        /// </summary>
        public PreprocessingOptions CurrentPreprocessingOptions
        {
            get => _currentOptions;
            set => _currentOptions = value ?? _presets[0];
        }

        /// <summary>
        /// プリセットを初期化
        /// </summary>
        private void InitializePresets()
        {
            _presets = new List<PreprocessingOptions>
            {
                // 標準プリセット
                new PreprocessingOptions
                {
                    ContrastLevel = 1.2f,
                    BrightnessLevel = 1.0f,
                    SharpnessLevel = 0.3f,
                    NoiseReduction = 0,
                    Threshold = 0,
                    ScaleFactor = 1.0f,
                    Padding = 0
                },
                // 高コントラストプリセット
                new PreprocessingOptions
                {
                    ContrastLevel = 1.4f,
                    BrightnessLevel = 1.1f,
                    SharpnessLevel = 0.5f,
                    NoiseReduction = 1,
                    Threshold = 0,
                    ScaleFactor = 1.0f,
                    Padding = 2
                },
                // シャープネス強化プリセット
                new PreprocessingOptions
                {
                    ContrastLevel = 1.2f,
                    BrightnessLevel = 1.0f,
                    SharpnessLevel = 0.7f,
                    NoiseReduction = 2,
                    Threshold = 0,
                    ScaleFactor = 1.2f,
                    Padding = 4
                }
            };
        }

        /// <summary>
        /// 次のプリセットに切り替える
        /// </summary>
        /// <returns>切り替えに成功したかどうか</returns>
        public bool TryNextPreset()
        {
            if (_presets.Count <= 1)
                return false;

            _currentPresetIndex = (_currentPresetIndex + 1) % _presets.Count;
            _currentOptions = _presets[_currentPresetIndex];
            Debug.WriteLine($"前処理プリセットを変更: {_currentPresetIndex + 1}/{_presets.Count}");
            return true;
        }

        /// <summary>
        /// テキスト領域数に基づいて設定を調整
        /// </summary>
        /// <param name="regionsCount">検出されたテキスト領域の数</param>
        public void AdjustSettings(int regionsCount)
        {
            // テキスト領域が見つからなかった場合は次のプリセットを試す
            if (regionsCount == 0)
            {
                TryNextPreset();
                Debug.WriteLine($"テキスト領域が検出されなかったため、プリセットを切り替えました: {_currentPresetIndex + 1}");
            }
            else
            {
                // テキスト領域が見つかった場合は現在のプリセットを維持
                Debug.WriteLine($"{regionsCount}個のテキスト領域が検出されました（プリセット: {_currentPresetIndex + 1}）");
            }
        }

        /// <summary>
        /// 画像に前処理を適用
        /// </summary>
        /// <param name="sourceBitmap">元の画像</param>
        /// <returns>前処理適用後の画像</returns>
        public Bitmap ProcessImage(Bitmap sourceBitmap)
        {
            if (sourceBitmap == null)
                return null;

            // ImagePreprocessorに現在のオプションを使用して処理を依頼
            Bitmap processedImage = ImagePreprocessor.ProcessImage(sourceBitmap, _currentOptions);

            // デバッグ用に前処理後の画像を保存
            SavePreprocessedImageForDebug(processedImage, _currentPresetIndex);

            return processedImage;
        }

        // 追加するメソッド
        public void SavePreprocessedImageForDebug(Bitmap processedImage, int presetIndex)
        {
            Debug.WriteLine("SavePreprocessedImageForDebug: 開始");

            // デバッグモードに関わらず、前処理設定の詳細をログに出力
            Debug.WriteLine($"前処理設定詳細 (プリセット {presetIndex + 1}/{_presets.Count}):");
            Debug.WriteLine($"  - コントラスト: {_currentOptions.ContrastLevel:F2}");
            Debug.WriteLine($"  - 明るさ: {_currentOptions.BrightnessLevel:F2}");
            Debug.WriteLine($"  - シャープネス: {_currentOptions.SharpnessLevel:F2}");
            Debug.WriteLine($"  - ノイズ除去: {_currentOptions.NoiseReduction}");
            Debug.WriteLine($"  - 二値化閾値: {_currentOptions.Threshold}");
            Debug.WriteLine($"  - スケール係数: {_currentOptions.ScaleFactor:F2}");
            Debug.WriteLine($"  - パディング: {_currentOptions.Padding}px");

            if (AppSettings.Instance.DebugModeEnabled && processedImage != null)
            {
                try
                {
                    // 一時ディレクトリの代わりにデスクトップを使用（確認しやすいよう）
                    string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                    string debugDir = Path.Combine(desktopPath, "GameTranslationOverlay_Debug");

                    // ディレクトリが存在しない場合は作成
                    if (!Directory.Exists(debugDir))
                    {
                        Directory.CreateDirectory(debugDir);
                    }

                    string fileName = $"debug_preprocess_{presetIndex + 1}_{DateTime.Now.ToString("HHmmss")}.png";
                    string fullPath = Path.Combine(debugDir, fileName);

                    // 画像を保存
                    processedImage.Save(fullPath, System.Drawing.Imaging.ImageFormat.Png);
                    Debug.WriteLine($"前処理画像を保存しました: {fullPath}");

                    // 画像のプロパティもログに出力
                    Debug.WriteLine($"  - 画像サイズ: {processedImage.Width}x{processedImage.Height}");
                    Debug.WriteLine($"  - ピクセル形式: {processedImage.PixelFormat}");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"前処理画像の保存に失敗: {ex.Message}");
                    Debug.WriteLine($"例外詳細: {ex}");
                }
            }
            else if (processedImage == null)
            {
                Debug.WriteLine("前処理画像の保存失敗: 画像がnullです");
            }
            else if (!AppSettings.Instance.DebugModeEnabled)
            {
                Debug.WriteLine("前処理画像の保存をスキップ: デバッグモードが無効です");
            }

            Debug.WriteLine("SavePreprocessedImageForDebug: 終了");
        }

        /// <summary>
        /// 画像に前処理を適用（OcrManagerとの互換性用）
        /// </summary>
        /// <param name="sourceBitmap">元の画像</param>
        /// <returns>前処理適用後の画像</returns>
        public Bitmap ApplyPreprocessing(Bitmap sourceBitmap)
        {
            return ProcessImage(sourceBitmap);
        }

        /// <summary>
        /// 設定をデフォルトに戻す
        /// </summary>
        public void ResetToDefault()
        {
            _currentPresetIndex = 0;
            _currentOptions = _presets[_currentPresetIndex];
            Debug.WriteLine("前処理設定をデフォルトに戻しました");
        }

        /// <summary>
        /// ゲームプロファイルを適用
        /// </summary>
        /// <param name="profileName">プロファイル名</param>
        public void ApplyGameProfile(string profileName)
        {
            // ゲームプロファイルに基づいて設定を変更（将来実装）
            Debug.WriteLine($"ゲームプロファイル '{profileName}' の適用: 未実装");
        }

        /// <summary>
        /// 現在の状態のサマリーを取得
        /// </summary>
        /// <returns>状態サマリー文字列</returns>
        public string GetStatusSummary()
        {
            return $"現在のプリセット: {_currentPresetIndex + 1}/{_presets.Count}, " +
                   $"コントラスト: {_currentOptions.ContrastLevel:F1}, " +
                   $"明るさ: {_currentOptions.BrightnessLevel:F1}, " +
                   $"シャープネス: {_currentOptions.SharpnessLevel:F1}";
        }
    }
}