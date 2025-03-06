using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using GameTranslationOverlay.Core.OCR;

namespace GameTranslationOverlay.Core.Utils
{
    /// <summary>
    /// OCR精度向上のための前処理設定を自動調整するクラス
    /// </summary>
    public class AdaptivePreprocessor
    {
        private Dictionary<string, PreprocessingOptions> _gameProfiles = new Dictionary<string, PreprocessingOptions>();
        private PreprocessingOptions _currentSettings;
        private string _currentGameName;
        private int _successfulDetections = 0;
        private int _failedDetections = 0;

        /// <summary>
        /// 現在の前処理設定を取得
        /// </summary>
        public PreprocessingOptions GetCurrentSettings()
        {
            return _currentSettings ?? CreateBalancedProcessingOptions();
        }

        /// <summary>
        /// 処理結果に基づいて設定を自動調整
        /// </summary>
        /// <param name="detectedRegionsCount">検出されたテキスト領域の数</param>
        /// <param name="averageConfidence">検出の平均信頼度</param>
        public void AdjustBasedOnResults(int detectedRegionsCount, float averageConfidence)
        {
            if (_currentSettings == null)
                _currentSettings = CreateBalancedProcessingOptions();

            if (detectedRegionsCount == 0)
            {
                _failedDetections++;
                _successfulDetections = 0;

                // 検出に失敗した場合、より積極的な設定に調整
                if (_failedDetections >= 3)
                {
                    EnhanceSettings();
                    _failedDetections = 0;
                    Debug.WriteLine("検出失敗が続いたため、前処理設定を強化しました");
                }
            }
            else
            {
                _failedDetections = 0;

                if (averageConfidence > 0.7f)
                {
                    _successfulDetections++;

                    // 検出が安定している場合は現在の設定を保存
                    if (_successfulDetections >= 5 && !string.IsNullOrEmpty(_currentGameName))
                    {
                        SaveProfileForGame(_currentGameName, _currentSettings);
                        _successfulDetections = 0;
                        Debug.WriteLine($"検出が安定したため、'{_currentGameName}'のプロファイルを保存しました");
                    }
                }
            }
        }

        /// <summary>
        /// 実行中のゲームに基づいて設定を自動選択
        /// </summary>
        /// <param name="gameExecutableName">ゲーム実行ファイル名</param>
        public PreprocessingOptions SelectSettingsForGame(string gameExecutableName)
        {
            _currentGameName = gameExecutableName;

            // 既存のプロファイルがあればそれを使用
            if (_gameProfiles.TryGetValue(gameExecutableName, out PreprocessingOptions profile))
            {
                _currentSettings = profile;
                Debug.WriteLine($"'{gameExecutableName}'の保存済みプロファイルを適用しました");
                return profile;
            }

            // ない場合はゲームのタイプを推測して設定を選択
            _currentSettings = GuessOptimalSettingsForGame(gameExecutableName);
            Debug.WriteLine($"'{gameExecutableName}'の推測プロファイルを適用しました");
            return _currentSettings;
        }

        /// <summary>
        /// ゲームの特性から最適な設定を推測
        /// </summary>
        private PreprocessingOptions GuessOptimalSettingsForGame(string gameExecutableName)
        {
            string gameName = gameExecutableName.ToLower();

            if (gameName.Contains("rpg") || gameName.Contains("adventure") ||
                gameName.Contains("story") || gameName.Contains("tale") ||
                gameName.Contains("fantasy"))
            {
                return CreateRpgSettings();
            }
            else if (gameName.Contains("fps") || gameName.Contains("shooter") ||
                     gameName.Contains("battle") || gameName.Contains("war") ||
                     gameName.Contains("combat"))
            {
                return CreateFpsSettings();
            }
            else if (gameName.Contains("novel") || gameName.Contains("visual") ||
                     gameName.Contains("drama") || gameName.Contains("dating") ||
                     gameName.Contains("sim"))
            {
                return CreateVisualNovelSettings();
            }

            // デフォルト設定
            return CreateBalancedProcessingOptions();
        }

        /// <summary>
        /// より積極的な前処理設定にする
        /// </summary>
        private void EnhanceSettings()
        {
            if (_currentSettings == null)
                _currentSettings = CreateBalancedProcessingOptions();

            // コントラストを強化
            _currentSettings.ApplyContrast = true;
            _currentSettings.ContrastLevel += 0.1f;
            if (_currentSettings.ContrastLevel > 1.8f)
                _currentSettings.ContrastLevel = 1.8f;

            // シャープネスを強化
            _currentSettings.ApplySharpening = true;
            _currentSettings.SharpeningLevel += 0.1f;
            if (_currentSettings.SharpeningLevel > 0.8f)
                _currentSettings.SharpeningLevel = 0.8f;

            // スケールアップ
            _currentSettings.Resize = true;
            _currentSettings.Scale += 0.1f;
            if (_currentSettings.Scale > 2.0f)
                _currentSettings.Scale = 2.0f;

            // ノイズ除去を有効化
            if (!_currentSettings.RemoveNoise)
            {
                _currentSettings.RemoveNoise = true;
                _currentSettings.NoiseReductionLevel = 1;
            }

            // パディングを追加
            if (!_currentSettings.AddPadding)
            {
                _currentSettings.AddPadding = true;
                _currentSettings.PaddingPixels = 5;
            }
        }

        /// <summary>
        /// 最小限の前処理設定を作成 (検出感度1)
        /// </summary>
        public PreprocessingOptions CreateMinimalProcessingOptions()
        {
            return new PreprocessingOptions
            {
                ApplyContrast = true,
                ContrastLevel = 1.1f,
                ApplyBrightness = false,
                ApplySharpening = false,
                RemoveNoise = false,
                ApplyThreshold = false,
                Resize = false,
                AddPadding = true,
                PaddingPixels = 2
            };
        }

        /// <summary>
        /// 軽めの前処理設定を作成 (検出感度2)
        /// </summary>
        public PreprocessingOptions CreateLightProcessingOptions()
        {
            return new PreprocessingOptions
            {
                ApplyContrast = true,
                ContrastLevel = 1.2f,
                ApplyBrightness = false,
                ApplySharpening = true,
                SharpeningLevel = 0.2f,
                RemoveNoise = false,
                ApplyThreshold = false,
                Resize = false,
                AddPadding = true,
                PaddingPixels = 3
            };
        }

        /// <summary>
        /// バランスの取れた前処理設定を作成 (検出感度3)
        /// </summary>
        public PreprocessingOptions CreateBalancedProcessingOptions()
        {
            return new PreprocessingOptions
            {
                ApplyContrast = true,
                ContrastLevel = 1.3f,
                ApplyBrightness = true,
                BrightnessLevel = 1.05f,
                ApplySharpening = true,
                SharpeningLevel = 0.3f,
                RemoveNoise = true,
                NoiseReductionLevel = 1,
                ApplyThreshold = false,
                Resize = true,
                Scale = 1.2f,
                AddPadding = true,
                PaddingPixels = 5
            };
        }

        /// <summary>
        /// 積極的な前処理設定を作成 (検出感度4)
        /// </summary>
        public PreprocessingOptions CreateAggressiveProcessingOptions()
        {
            return new PreprocessingOptions
            {
                ApplyContrast = true,
                ContrastLevel = 1.4f,
                ApplyBrightness = true,
                BrightnessLevel = 1.1f,
                ApplySharpening = true,
                SharpeningLevel = 0.5f,
                RemoveNoise = true,
                NoiseReductionLevel = 2,
                ApplyThreshold = false,
                Resize = true,
                Scale = 1.5f,
                AddPadding = true,
                PaddingPixels = 8
            };
        }

        /// <summary>
        /// 非常に積極的な前処理設定を作成 (検出感度5)
        /// </summary>
        public PreprocessingOptions CreateVeryAggressiveProcessingOptions()
        {
            return new PreprocessingOptions
            {
                ApplyContrast = true,
                ContrastLevel = 1.6f,
                ApplyBrightness = true,
                BrightnessLevel = 1.15f,
                ApplySharpening = true,
                SharpeningLevel = 0.7f,
                RemoveNoise = true,
                NoiseReductionLevel = 2,
                ApplyThreshold = false,
                Resize = true,
                Scale = 1.8f,
                AddPadding = true,
                PaddingPixels = 10
            };
        }

        /// <summary>
        /// RPGゲーム向けの設定を作成
        /// </summary>
        private PreprocessingOptions CreateRpgSettings()
        {
            return new PreprocessingOptions
            {
                ApplyContrast = true,
                ContrastLevel = 1.3f,
                ApplyBrightness = true,
                BrightnessLevel = 1.05f,
                ApplySharpening = true,
                SharpeningLevel = 0.4f,
                RemoveNoise = true,
                NoiseReductionLevel = 1,
                ApplyThreshold = false,
                Resize = true,
                Scale = 1.3f,
                AddPadding = true,
                PaddingPixels = 6
            };
        }

        /// <summary>
        /// FPSゲーム向けの設定を作成
        /// </summary>
        private PreprocessingOptions CreateFpsSettings()
        {
            return new PreprocessingOptions
            {
                ApplyContrast = true,
                ContrastLevel = 1.5f,
                ApplyBrightness = true,
                BrightnessLevel = 1.1f,
                ApplySharpening = true,
                SharpeningLevel = 0.6f,
                RemoveNoise = false,
                ApplyThreshold = false,
                Resize = true,
                Scale = 1.2f,
                AddPadding = true,
                PaddingPixels = 4
            };
        }

        /// <summary>
        /// ビジュアルノベル向けの設定を作成
        /// </summary>
        private PreprocessingOptions CreateVisualNovelSettings()
        {
            return new PreprocessingOptions
            {
                ApplyContrast = true,
                ContrastLevel = 1.2f,
                ApplyBrightness = false,
                ApplySharpening = true,
                SharpeningLevel = 0.3f,
                RemoveNoise = true,
                NoiseReductionLevel = 1,
                ApplyThreshold = false,
                Resize = true,
                Scale = 1.4f,
                AddPadding = true,
                PaddingPixels = 8
            };
        }

        /// <summary>
        /// ゲーム向けのプロファイルを保存
        /// </summary>
        private void SaveProfileForGame(string gameName, PreprocessingOptions options)
        {
            // ディープコピーを作成して保存
            var copy = new PreprocessingOptions
            {
                ApplyContrast = options.ApplyContrast,
                ContrastLevel = options.ContrastLevel,
                ApplyBrightness = options.ApplyBrightness,
                BrightnessLevel = options.BrightnessLevel,
                ApplySharpening = options.ApplySharpening,
                SharpeningLevel = options.SharpeningLevel,
                RemoveNoise = options.RemoveNoise,
                NoiseReductionLevel = options.NoiseReductionLevel,
                ApplyThreshold = options.ApplyThreshold,
                ThresholdLevel = options.ThresholdLevel,
                Resize = options.Resize,
                Scale = options.Scale,
                AddPadding = options.AddPadding,
                PaddingPixels = options.PaddingPixels
            };

            _gameProfiles[gameName] = copy;
        }
    }
}