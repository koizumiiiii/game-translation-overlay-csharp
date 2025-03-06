using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using GameTranslationOverlay.Core.OCR;

namespace GameTranslationOverlay.Core.Utils
{
    /// <summary>
    /// OCR精度向上のための適応型前処理と閾値調整を行うユーティリティクラス
    /// </summary>
    public class AdaptivePreprocessor
    {
        // 閾値の初期設定
        private float _baseThreshold = 0.3f;      // 基本閾値
        private float _currentThreshold = 0.3f;   // 現在の閾値
        private float _maxThreshold = 0.7f;       // 上限閾値

        // 動的調整用カウンター
        private int _falsePositiveCount = 0;      // 誤検出カウンター
        private int _noDetectionCount = 0;        // 未検出カウンター
        private int _successCount = 0;            // 成功カウンター

        // 前処理設定
        private PreprocessingOptions _currentPreprocessingOptions;
        private List<PreprocessingOptions> _presetOptions;
        private int _currentPresetIndex = 0;

        /// <summary>
        /// 現在の前処理設定を取得または設定
        /// </summary>
        public PreprocessingOptions CurrentPreprocessingOptions
        {
            get { return _currentPreprocessingOptions; }
            set
            {
                _currentPreprocessingOptions = value;
                Debug.WriteLine("前処理設定を外部から変更しました");
            }
        }

        // 最適化状態追跡
        private bool _isOptimizing = false;       // 最適化プロセス実行中フラグ
        private int _optimizationSteps = 0;       // 最適化ステップ数
        private const int MAX_OPTIMIZATION_STEPS = 5; // 最大最適化ステップ

        // 状態記録
        private DateTime _lastAdjustmentTime = DateTime.MinValue;
        private int _totalDetections = 0;
        private int _successfulDetections = 0;

        /// <summary>
        /// コンストラクタ
        /// </summary>
        public AdaptivePreprocessor()
        {
            // プリセット設定を初期化
            InitializePresets();

            // デフォルトの前処理設定を適用
            _currentPreprocessingOptions = GetDefaultPreprocessingOptions();

            Debug.WriteLine("AdaptivePreprocessor: 初期化完了");
        }

        /// <summary>
        /// プリセット設定を初期化
        /// </summary>
        private void InitializePresets()
        {
            _presetOptions = new List<PreprocessingOptions>
            {
                // プリセット1: 標準設定（コントラスト強調、軽度シャープニング）
                new PreprocessingOptions
                {
                    ApplyContrast = true,
                    ContrastLevel = 20,
                    ApplySharpening = true,
                    SharpeningLevel = 1.2f,
                    Scale = 1.0f,
                    PaddingPixels = 4
                },
                
                // プリセット2: 日本語テキスト向け
                ImagePreprocessor.JapaneseTextPreset,
                
                // プリセット3: 英語テキスト向け
                ImagePreprocessor.EnglishTextPreset,
                
                // プリセット4: ダークテーマゲーム向け（明るさ上げ、コントラスト強調）
                new PreprocessingOptions
                {
                    ApplyContrast = true,
                    ContrastLevel = 30,
                    ApplyBrightness = true,
                    BrightnessLevel = 15,
                    Scale = 1.2f,
                    PaddingPixels = 4
                },
                
                // プリセット5: 小さいフォント向け（拡大、シャープニング）
                new PreprocessingOptions
                {
                    ApplySharpening = true,
                    SharpeningLevel = 1.5f,
                    Scale = 1.5f,
                    PaddingPixels = 6
                }
            };
        }

        /// <summary>
        /// 指定された画像に前処理を適用
        /// </summary>
        /// <param name="image">元画像</param>
        /// <returns>前処理済み画像</returns>
        public Bitmap ApplyPreprocessing(Bitmap image)
        {
            if (image == null)
                return null;

            return ImagePreprocessor.Preprocess(image, _currentPreprocessingOptions);
        }

        /// <summary>
        /// 現在の閾値を取得
        /// </summary>
        public float GetCurrentThreshold()
        {
            return _currentThreshold;
        }

        /// <summary>
        /// 現在の前処理設定を取得
        /// </summary>
        public PreprocessingOptions GetCurrentPreprocessingOptions()
        {
            return CurrentPreprocessingOptions;
        }

        /// <summary>
        /// デフォルトの前処理設定を取得
        /// </summary>
        public static PreprocessingOptions GetDefaultPreprocessingOptions()
        {
            return new PreprocessingOptions
            {
                ApplyContrast = true,
                ContrastLevel = 20,
                ApplySharpening = true,
                SharpeningLevel = 1.2f,
                RemoveNoise = false,
                Scale = 1.0f,
                PaddingPixels = 4
            };
        }

        /// <summary>
        /// 検出結果に基づいて閾値と前処理設定を調整
        /// </summary>
        /// <param name="detectedRegions">検出されたテキスト領域数</param>
        /// <param name="userConfirmedFalsePositive">ユーザーが誤検出と確認したかどうか</param>
        public void AdjustSettings(int detectedRegions, bool userConfirmedFalsePositive = false)
        {
            // 検出統計の更新
            _totalDetections++;
            if (detectedRegions > 0)
                _successfulDetections++;

            // 誤検出のフィードバックがあれば閾値を上げる
            if (userConfirmedFalsePositive)
            {
                _falsePositiveCount++;
                if (_falsePositiveCount >= 3)
                {
                    IncrementThreshold(0.05f);
                    _falsePositiveCount = 0;
                    Debug.WriteLine($"誤検出が多いため閾値を上げました: {_currentThreshold:F2}");
                }
            }

            // 一定期間検出がなければ閾値を下げる
            if (detectedRegions == 0)
            {
                _noDetectionCount++;
                _successCount = 0;

                if (_noDetectionCount >= 5)
                {
                    // 最適化プロセスを開始
                    if (!_isOptimizing)
                    {
                        StartOptimization();
                    }
                    else
                    {
                        // 既に最適化中なら次のプリセットに切り替え
                        TryNextPreset();
                    }

                    _noDetectionCount = 0;
                }
            }
            else
            {
                _noDetectionCount = 0;
                _successCount++;

                // 連続して成功したら最適化を終了
                if (_successCount >= 3 && _isOptimizing)
                {
                    _isOptimizing = false;
                    _optimizationSteps = 0;
                    Debug.WriteLine("検出が安定したため最適化を終了しました");
                }
            }

            // 最適化プロセスの進行
            if (_isOptimizing)
            {
                ContinueOptimization(detectedRegions);
            }

            // 検出率が低い場合は定期的に設定を見直す
            if (_totalDetections > 20)
            {
                float detectionRate = (float)_successfulDetections / _totalDetections;
                if (detectionRate < 0.4f && (DateTime.Now - _lastAdjustmentTime).TotalMinutes > 2)
                {
                    ResetToDefault();
                    _lastAdjustmentTime = DateTime.Now;
                    Debug.WriteLine($"検出率が低いため設定をリセットしました（検出率: {detectionRate:P2}）");
                }
            }
        }

        /// <summary>
        /// 閾値を増加させる
        /// </summary>
        /// <param name="increment">増加量</param>
        private void IncrementThreshold(float increment)
        {
            _currentThreshold = Math.Min(_currentThreshold + increment, _maxThreshold);
        }

        /// <summary>
        /// 閾値を減少させる
        /// </summary>
        /// <param name="decrement">減少量</param>
        private void DecrementThreshold(float decrement)
        {
            _currentThreshold = Math.Max(_currentThreshold - decrement, _baseThreshold);
        }

        /// <summary>
        /// 最適化プロセスを開始
        /// </summary>
        private void StartOptimization()
        {
            _isOptimizing = true;
            _optimizationSteps = 0;
            DecrementThreshold(0.1f); // まず閾値を下げる
            Debug.WriteLine($"最適化プロセスを開始しました: 閾値を下げて {_currentThreshold:F2} になりました");
        }

        /// <summary>
        /// 最適化プロセスを継続
        /// </summary>
        /// <param name="detectedRegions">検出されたテキスト領域数</param>
        private void ContinueOptimization(int detectedRegions)
        {
            _optimizationSteps++;

            if (_optimizationSteps >= MAX_OPTIMIZATION_STEPS)
            {
                // 最大ステップ数に達したら最適化を終了
                _isOptimizing = false;
                _optimizationSteps = 0;
                ResetToDefault();
                Debug.WriteLine("最大最適化ステップに達したため設定をリセットしました");
                return;
            }

            if (detectedRegions == 0)
            {
                // まだ検出されない場合は前処理設定を変更
                TryNextPreset();
            }
        }

        /// <summary>
        /// 次のプリセットに切り替え
        /// </summary>
        public void TryNextPreset()
        {
            _currentPresetIndex = (_currentPresetIndex + 1) % _presetOptions.Count;
            _currentPreprocessingOptions = _presetOptions[_currentPresetIndex];
            Debug.WriteLine($"前処理プリセットを変更しました: プリセット{_currentPresetIndex + 1}");
        }

        /// <summary>
        /// 設定をデフォルトに戻す
        /// </summary>
        public void ResetToDefault()
        {
            _currentThreshold = _baseThreshold;
            _currentPreprocessingOptions = GetDefaultPreprocessingOptions();
            _currentPresetIndex = 0;
            _falsePositiveCount = 0;
            _noDetectionCount = 0;
            _successCount = 0;
            _isOptimizing = false;
            _optimizationSteps = 0;
            Debug.WriteLine("設定をデフォルトに戻しました");
        }

        /// <summary>
        /// 統計情報をクリア
        /// </summary>
        public void ClearStatistics()
        {
            _totalDetections = 0;
            _successfulDetections = 0;
        }

        /// <summary>
        /// 現在の適応状態の概要を文字列で取得
        /// </summary>
        public string GetStatusSummary()
        {
            float detectionRate = _totalDetections > 0 ? (float)_successfulDetections / _totalDetections : 0;

            return $"閾値: {_currentThreshold:F2}, " +
                   $"プリセット: {_currentPresetIndex + 1}/{_presetOptions.Count}, " +
                   $"検出率: {detectionRate:P1}, " +
                   $"最適化中: {(_isOptimizing ? "はい" : "いいえ")}";
        }

        /// <summary>
        /// ゲームプロファイルの設定を適用（将来の拡張用）
        /// </summary>
        /// <param name="profileName">プロファイル名</param>
        public void ApplyGameProfile(string profileName)
        {
            // TODO: ゲームプロファイルデータベースから設定を読み込む実装
            // 現在はデモ用のスタブ実装

            switch (profileName.ToLower())
            {
                case "rpg":
                    _currentPreprocessingOptions = ImagePreprocessor.JapaneseTextPreset;
                    _currentThreshold = 0.4f;
                    break;

                case "fps":
                    _currentPreprocessingOptions = new PreprocessingOptions
                    {
                        ApplyContrast = true,
                        ContrastLevel = 25,
                        ApplySharpening = true,
                        SharpeningLevel = 1.3f,
                        Scale = 1.1f
                    };
                    _currentThreshold = 0.5f;
                    break;

                default:
                    // デフォルト設定を適用
                    ResetToDefault();
                    break;
            }

            Debug.WriteLine($"ゲームプロファイル '{profileName}' を適用しました");
        }
    }
}