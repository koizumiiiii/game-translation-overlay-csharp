﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Threading.Tasks;
using System.Linq;
using GameTranslationOverlay.Core.Utils;
using GameTranslationOverlay.Core.Configuration;
using GameTranslationOverlay.Core.Diagnostics;
using Windows.Media.Ocr;
using GameTranslationOverlay.Core.OCR.AI;
using System.IO;

namespace GameTranslationOverlay.Core.OCR
{
    /// <summary>
    /// OCRエンジンを管理するクラス（複数エンジンから単一エンジンへ簡素化）
    /// </summary>
    public class OcrManager : IDisposable, IOcrEngine
    {
        // OCRエンジン
        private PaddleOcrEngine _paddleOcrEngine;

        // 適応型プリプロセッサ
        private AdaptivePreprocessor _adaptivePreprocessor;

        // 処理設定
        private float _confidenceThreshold = 0.6f;
        private bool _usePreprocessing = true;
        private bool _isDisposed = false;
        private bool _useAdaptiveMode = true; // 適応モードを使用するかどうか

        // 段階的スキャン設定
        private bool _useProgressiveScan = true;
        private const int MAX_SCAN_ATTEMPTS = 3;

        // 並行処理の制御
        private readonly object _ocrLock = new object();
        private const int MAX_CONSECUTIVE_ERRORS = 5;

        // パフォーマンス測定とトラッキング
        private readonly Stopwatch _stopwatch = new Stopwatch();
        private readonly List<double> _processingTimes = new List<double>();
        private const int MAX_TIMING_SAMPLES = 50;

        // OCRキャッシュ（同一画像に対する重複OCR処理を防止）
        private readonly Dictionary<int, List<TextRegion>> _ocrCache = new Dictionary<int, List<TextRegion>>();
        private const int MAX_CACHE_ENTRIES = 20;
        private DateTime _lastCacheCleanupTime = DateTime.MinValue;

        // ゲームプロファイル連携
        private string _currentGameTitle = string.Empty;
        private GameProfiles _gameProfiles = null;

        private bool _isProcessing = false;
        private DateTime _processingStartTime;
        private readonly int _processingTimeoutMs = 10000; // 10秒タイムアウト

        /// <summary>
        /// コンストラクタ
        /// </summary>
        public OcrManager()
        {
            // 適応型プリプロセッサの初期化
            _adaptivePreprocessor = new AdaptivePreprocessor();
            Debug.WriteLine("OcrManager: 初期化されました");
        }

        /// <summary>
        /// OCRエンジンの初期化
        /// </summary>
        public async Task InitializeAsync()
        {
            try
            {
                Debug.WriteLine("OCRエンジンの初期化を開始...");

                // PaddleOCRエンジンの初期化
                _paddleOcrEngine = new PaddleOcrEngine();
                await _paddleOcrEngine.InitializeAsync();

                // PaddleOCRエンジンはリソースマネージャーに登録しない
                // PaddleOCR自体がリソース管理を行うため、直接Disposeメソッドで解放する

                Debug.WriteLine("OCRエンジンの初期化が完了しました");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"OCRエンジン初期化エラー: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// テキスト領域を検出する（段階的スキャンを使用）
        /// </summary>
        /// <param name="image">検出対象の画像</param>
        /// <returns>検出されたテキスト領域のリスト</returns>
        public async Task<List<TextRegion>> DetectTextRegionsAsync(Bitmap image)
        {
            // タイムアウト判定
            if (_isProcessing && (DateTime.Now - _processingStartTime).TotalMilliseconds > _processingTimeoutMs)
            {
                Logger.Instance.LogWarning("OCR処理タイムアウトのためリセットします");
                _isProcessing = false;
            }

            if (_isProcessing)
            {
                Logger.Instance.LogDebug("OcrManager", "前回の処理が完了していないため、処理をスキップします");
                return new List<TextRegion>();
            }

            try
            {
                _isProcessing = true;
                _processingStartTime = DateTime.Now;

                // 処理前に現在の設定をログに記録（デバッグレベル）
                if (AppSettings.Instance.DebugModeEnabled)
                {
                    LogCurrentSettings();
                }

                Logger.Instance.LogDebug("OcrManager", "OCR処理開始");

                // 実際のOCR処理
                var result = await _paddleOcrEngine.DetectTextRegionsAsync(image);

                // 処理結果のログ出力強化
                if (result.Count == 0)
                {
                    Logger.Instance.LogWarning("OCR処理完了しましたが、テキスト領域が検出されませんでした");

                    // デバッグモード時のみ画像を保存
                    if (AppSettings.Instance.DebugModeEnabled)
                    {
                        try
                        {
                            string debugDir = Path.Combine(
                                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                                "GameTranslationOverlay", "Debug");
                            Directory.CreateDirectory(debugDir);
                            string filename = Path.Combine(debugDir, $"ocr_failed_{DateTime.Now:yyyyMMdd_HHmmss}.png");
                            image.Save(filename, System.Drawing.Imaging.ImageFormat.Png);
                            Logger.Instance.LogDebug("OcrManager", $"OCR失敗時の画像を保存: {filename}");
                        }
                        catch (Exception ex)
                        {
                            Logger.Instance.LogError($"デバッグ画像保存エラー: {ex.Message}", ex);
                        }
                    }
                }
                else
                {
                    Logger.Instance.LogInfo($"OCR処理完了: {result.Count}個のテキスト領域を検出");
                }

                return result;
            }
            catch (Exception ex)
            {
                Logger.Instance.LogError("OCR処理中にエラーが発生しました: " + ex.Message, ex);
                return new List<TextRegion>();
            }
            finally
            {
                // 必ずフラグをリセット
                _isProcessing = false;
            }
        }

        /// <summary>
        /// 画像のハッシュ値を計算（簡易的な方法）
        /// </summary>
        private int CalculateImageHash(Bitmap image)
        {
            try
            {
                // 単純なハッシュ計算（完全なマッチングではなく近似）
                int hash = 17;
                hash = hash * 31 + image.Width;
                hash = hash * 31 + image.Height;

                // 数ポイントをサンプリングして特徴を抽出
                int samplingSize = 5;
                int stepX = Math.Max(1, image.Width / samplingSize);
                int stepY = Math.Max(1, image.Height / samplingSize);

                for (int y = 0; y < image.Height; y += stepY)
                {
                    for (int x = 0; x < image.Width; x += stepX)
                    {
                        if (x < image.Width && y < image.Height)
                        {
                            Color pixel = image.GetPixel(x, y);
                            // 色の大まかな特徴だけを使用
                            int colorValue = (pixel.R / 32) * 1000000 + (pixel.G / 32) * 1000 + (pixel.B / 32);
                            hash = hash * 31 + colorValue;
                        }
                    }
                }

                return hash;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"画像ハッシュ計算エラー: {ex.Message}");
                // エラー時は固有のハッシュを返す（キャッシュヒットしないよう）
                return image.GetHashCode();
            }
        }

        /// <summary>
        /// キャッシュにテキスト領域を追加
        /// </summary>
        private void AddToCache(int imageHash, List<TextRegion> regions)
        {
            try
            {
                // キャッシュが大きすぎる場合、古いエントリを削除
                if (_ocrCache.Count >= MAX_CACHE_ENTRIES)
                {
                    int oldestKey = _ocrCache.Keys.First();
                    _ocrCache.Remove(oldestKey);
                }

                // 結果のディープコピーを保存（参照の問題を避けるため）
                var regionsCopy = regions.Select(r => new TextRegion
                {
                    Text = r.Text,
                    Confidence = r.Confidence,
                    Bounds = r.Bounds
                }).ToList();

                _ocrCache[imageHash] = regionsCopy;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"キャッシュ追加エラー: {ex.Message}");
            }
        }

        /// <summary>
        /// キャッシュのクリーンアップ
        /// </summary>
        private void CleanupCache()
        {
            try
            {
                // キャッシュが一定サイズを超えたらクリアする
                if (_ocrCache.Count > MAX_CACHE_ENTRIES / 2)
                {
                    Debug.WriteLine($"OCRキャッシュをクリーンアップしています（現在のエントリ数: {_ocrCache.Count}）");
                    _ocrCache.Clear();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"キャッシュクリーンアップエラー: {ex.Message}");
            }
        }

        /// <summary>
        /// リソースのクリーンアップ
        /// </summary>
        private void CleanupResources()
        {
            try
            {
                // キャッシュのクリア
                _ocrCache.Clear();

                // リソースマネージャーのクリーンアップ促進
                ResourceManager.CleanupDeadReferences();

                // GCの実行を促進
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"リソースクリーンアップエラー: {ex.Message}");
            }
        }

        /// <summary>
        /// 現在の設定でスキャンを実行
        /// </summary>
        private async Task<List<TextRegion>> ScanWithCurrentSettingsAsync(Bitmap image)
        {
            Bitmap processedImage = null;

            try
            {
                // 前処理を適用
                if (_usePreprocessing)
                {
                    processedImage = _adaptivePreprocessor.ApplyPreprocessing(image);
                    if (processedImage == null)
                    {
                        Debug.WriteLine("前処理に失敗したため、元の画像を使用します");
                        processedImage = new Bitmap(image);
                    }
                }
                else
                {
                    // 元の画像のコピーを使用（安全のため）
                    processedImage = new Bitmap(image);
                }

                // ビットマップリソースはResourceManagerに登録して追跡
                ResourceManager.TrackResource(processedImage);

                // OCRエンジンでテキスト領域を検出
                var regions = await _paddleOcrEngine.DetectTextRegionsAsync(processedImage);

                return regions;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"OCRスキャンエラー: {ex.Message}");
                throw;
            }
            finally
            {
                // 前処理で作成した新しい画像を解放
                if (processedImage != null && processedImage != image)
                {
                    ResourceManager.ReleaseResource(processedImage);
                    processedImage = null;
                }
            }
        }

        /// <summary>
        /// 段階的に複数の設定でスキャンを試行
        /// </summary>
        private async Task<List<TextRegion>> ScanWithMultipleSettingsAsync(Bitmap image)
        {
            // 最初に現在の設定で試行
            var regions = await ScanWithCurrentSettingsAsync(image);

            // 結果があれば終了
            if (regions.Count > 0)
                return regions;

            // 異なる設定でさらに試行
            for (int attempt = 1; attempt < MAX_SCAN_ATTEMPTS; attempt++)
            {
                Debug.WriteLine($"テキスト検出再試行 ({attempt}/{MAX_SCAN_ATTEMPTS - 1})...");

                // 閾値を一時的に下げる
                float originalThreshold = _confidenceThreshold;
                _confidenceThreshold = Math.Max(_confidenceThreshold - (0.1f * attempt), 0.1f);

                // 次のプリセットを試す（一時的に）
                if (_usePreprocessing && _useAdaptiveMode)
                {
                    _adaptivePreprocessor.TryNextPreset();
                }

                try
                {
                    // 再試行
                    regions = await ScanWithCurrentSettingsAsync(image);
                }
                finally
                {
                    // 設定を元に戻す
                    _confidenceThreshold = originalThreshold;
                }

                // 結果があれば終了
                if (regions.Count > 0)
                {
                    Debug.WriteLine($"再試行で{regions.Count}個のテキスト領域を検出");
                    return regions;
                }
            }

            // 成功した結果がある場合、自動的にプロファイルを更新を検討
            if (regions.Count > 0 && !string.IsNullOrWhiteSpace(_currentGameTitle) && _gameProfiles != null)
            {
                // 10個以上の領域が見つかった場合は有用な設定と判断
                if (regions.Count >= 10 && _processingTimes.Count > 0)
                {
                    // 直近の処理時間が良好（500ms以下）かつ検出結果が多い場合、自動保存
                    double lastProcessingTime = _processingTimes.Last();
                    if (lastProcessingTime <= 500)
                    {
                        Debug.WriteLine($"検出結果が良好 ({regions.Count}領域, {lastProcessingTime:F1}ms)なため、プロファイル自動更新を検討");

                        // 既存プロファイルと現在の設定を比較して更新するか決定
                        var existingProfile = _gameProfiles.GetProfile(_currentGameTitle);
                        bool shouldUpdate = false;

                        if (existingProfile == null || !existingProfile.IsOptimized)
                        {
                            // プロファイルがないか最適化されていない場合は保存
                            shouldUpdate = true;
                        }
                        else
                        {
                            // 最後の最適化から1週間以上経過している場合は更新を検討
                            TimeSpan timeSinceLastUpdate = DateTime.Now - existingProfile.LastOptimized;
                            if (timeSinceLastUpdate.TotalDays > 7)
                            {
                                // 現在の結果が既存より良いか（領域数で判断）
                                shouldUpdate = true;
                            }
                        }

                        // 更新条件を満たす場合はプロファイル保存
                        if (shouldUpdate)
                        {
                            SaveProfileForCurrentGame();
                        }
                    }
                }
            }

            // すべての試行が失敗した場合は空のリストを返す
            return new List<TextRegion>();
        }

        /// <summary>
        /// 信頼度閾値に基づいてテキスト領域をフィルタリング
        /// </summary>
        private List<TextRegion> FilterByConfidence(List<TextRegion> regions)
        {
            return regions.FindAll(r => r.Confidence >= _confidenceThreshold);
        }

        /// <summary>
        /// 処理時間を記録する
        /// </summary>
        private void RecordProcessingTime(long milliseconds)
        {
            try
            {
                _processingTimes.Add(milliseconds);

                // サンプル数を制限
                if (_processingTimes.Count > MAX_TIMING_SAMPLES)
                {
                    _processingTimes.RemoveAt(0);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"処理時間記録エラー: {ex.Message}");
            }
        }

        /// <summary>
        /// スペシフィック領域のテキストを認識する
        /// </summary>
        public async Task<string> RecognizeTextAsync(Rectangle region)
        {
            // 破棄済みのチェック
            if (_isDisposed)
            {
                Debug.WriteLine("OCR: マネージャーは既に破棄されています");
                return string.Empty;
            }

            try
            {
                // スクリーンキャプチャは呼び出し元で行われることを想定
                // 非同期操作を模擬するために小さな遅延を挿入
                await Task.Delay(1);
                return "OCR Manager: RecognizeTextAsync not directly implemented. Use PaddleOcrEngine instead.";
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"テキスト認識エラー: {ex.Message}");
                return string.Empty;
            }
        }

        /// <summary>
        /// 使用するOCRエンジンを設定（互換性のために維持）
        /// </summary>
        public void SetOcrEngine(string engineName)
        {
            // PaddleOCRのみをサポートするため、実質的には何もしない
            Debug.WriteLine($"使用できるOCRエンジンはPaddleOCRのみです。指定されたエンジン: {engineName}");
        }

        /// <summary>
        /// フォールバックの有効・無効を設定（互換性のために維持）
        /// </summary>
        public void SetUseFallback(bool enable)
        {
            // 単一エンジンのため、フォールバックは使用しない
            Debug.WriteLine($"フォールバック機能は使用されません。");
        }

        /// <summary>
        /// 信頼度閾値を設定
        /// </summary>
        public void SetConfidenceThreshold(float threshold)
        {
            _confidenceThreshold = Math.Max(0.0f, Math.Min(1.0f, threshold));
            Debug.WriteLine($"信頼度閾値を {_confidenceThreshold:F2} に設定しました");
        }

        /// <summary>
        /// 前処理オプションを設定
        /// </summary>
        public void SetPreprocessingOptions(GameTranslationOverlay.Core.Utils.PreprocessingOptions options)
        {
            if (options != null && _adaptivePreprocessor != null)
            {
                // プロパティを通じて設定を適用
                _adaptivePreprocessor.CurrentPreprocessingOptions = options;
                Debug.WriteLine("前処理オプションを設定しました");
            }
        }

        /// <summary>
        /// 前処理の有効・無効を設定
        /// </summary>
        public void EnablePreprocessing(bool enable)
        {
            _usePreprocessing = enable;
            Debug.WriteLine($"前処理を {(_usePreprocessing ? "有効" : "無効")} にしました");
        }

        /// <summary>
        /// 適応モードの有効・無効を設定
        /// </summary>
        public void EnableAdaptiveMode(bool enable)
        {
            _useAdaptiveMode = enable;
            Debug.WriteLine($"適応モードを {(_useAdaptiveMode ? "有効" : "無効")} にしました");
        }

        /// <summary>
        /// 段階的スキャンの有効・無効を設定
        /// </summary>
        public void EnableProgressiveScan(bool enable)
        {
            _useProgressiveScan = enable;
            Debug.WriteLine($"段階的スキャンを {(_useProgressiveScan ? "有効" : "無効")} にしました");
        }

        /// <summary>
        /// 現在の設定状態を取得
        /// </summary>
        public string GetStatusSummary()
        {
            double avgTime = 0;
            if (_processingTimes.Count > 0)
            {
                avgTime = _processingTimes.Sum() / _processingTimes.Count;
            }

            return $"信頼度閾値: {_confidenceThreshold:F2}, " +
                   $"前処理: {(_usePreprocessing ? "有効" : "無効")}, " +
                   $"適応モード: {(_useAdaptiveMode ? "有効" : "無効")}, " +
                   $"段階的スキャン: {(_useProgressiveScan ? "有効" : "無効")}, " +
                   $"平均処理時間: {avgTime:F1}ms, " +
                   $"キャッシュエントリ数: {_ocrCache.Count}";
        }

        /// <summary>
        /// OCRキャッシュをクリア
        /// </summary>
        public void ClearCache()
        {
            _ocrCache.Clear();
            Debug.WriteLine("OCRキャッシュをクリアしました");
        }

        /// <summary>
        /// 適応型プリプロセッサの状態サマリーを取得
        /// </summary>
        public string GetPreprocessorStatusSummary()
        {
            return _adaptivePreprocessor?.GetStatusSummary() ?? "プリプロセッサが初期化されていません";
        }

        /// <summary>
        /// 設定をデフォルトに戻す
        /// </summary>
        public void ResetToDefault()
        {
            _confidenceThreshold = 0.6f;
            _usePreprocessing = true;
            _useAdaptiveMode = true;
            _useProgressiveScan = true;

            if (_adaptivePreprocessor != null)
            {
                _adaptivePreprocessor.ResetToDefault();
            }

            ClearCache();
            Debug.WriteLine("すべての設定をデフォルトに戻しました");
        }

        /// <summary>
        /// ゲームプロファイルを適用
        /// </summary>
        public void ApplyGameProfile(string profileName)
        {
            if (_adaptivePreprocessor != null)
            {
                _adaptivePreprocessor.ApplyGameProfile(profileName);
            }
        }

        /// <summary>
        /// 使用しているプライマリエンジンの名前を取得（互換性用）
        /// </summary>
        public string GetPrimaryEngineName()
        {
            return "PaddleOCR";
        }

        /// <summary>
        /// メモリ使用量を監視・管理する内部クラス
        /// </summary>
        private class MemoryManagement
        {
            private static readonly long HighMemoryThresholdMB = 300; // 高メモリ使用と判断する閾値（MB）
            private static readonly TimeSpan MonitorInterval = TimeSpan.FromMinutes(5); // 監視間隔
            private static DateTime _lastCheck = DateTime.MinValue;

            /// <summary>
            /// メモリ使用量を確認し、必要に応じてクリーンアップを実行
            /// </summary>
            public static void CheckMemoryUsage()
            {
                // 前回のチェックから一定時間が経過していない場合はスキップ
                if (DateTime.Now - _lastCheck < MonitorInterval)
                {
                    return;
                }

                _lastCheck = DateTime.Now;

                try
                {
                    using (Process currentProcess = Process.GetCurrentProcess())
                    {
                        // 現在のメモリ使用量を取得（MB単位）
                        long memoryUsageMB = currentProcess.PrivateMemorySize64 / (1024 * 1024);

                        Debug.WriteLine($"メモリ使用量: {memoryUsageMB}MB");

                        // 高メモリ使用の場合、クリーンアップを促進
                        if (memoryUsageMB > HighMemoryThresholdMB)
                        {
                            Debug.WriteLine($"高メモリ使用を検出（{memoryUsageMB}MB）: クリーンアップを実行します");

                            // リソースマネージャーのクリーンアップを促進
                            ResourceManager.CleanupDeadReferences();

                            // GCの実行を促進
                            GC.Collect();
                            GC.WaitForPendingFinalizers();

                            // 2回目のGC（断片化対策）
                            GC.Collect();

                            // クリーンアップ後のメモリ使用量を確認
                            long afterCleanupMB = currentProcess.PrivateMemorySize64 / (1024 * 1024);
                            Debug.WriteLine($"クリーンアップ後のメモリ使用量: {afterCleanupMB}MB（{memoryUsageMB - afterCleanupMB}MB削減）");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"メモリ使用量確認エラー: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// リソースの解放
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed)
                return;

            try
            {
                Dispose(true);
                GC.SuppressFinalize(this);

                // フラグのリセット
                _isProcessing = false;

                Logger.Instance.LogDebug("OcrManager", "OcrManagerが正常に破棄されました");
            }
            catch (Exception ex)
            {
                Logger.Instance.LogError("OcrManager破棄中にエラーが発生: " + ex.Message, ex);
            }
            finally
            {
                _isDisposed = true;
            }
        }

        // LogCurrentSettings()メソッドの追加 - 現在のOCR設定を詳細にログ出力
        public void LogCurrentSettings()
        {
            Logger.Instance.LogInfo("===== 現在のOCR設定 =====");
            Logger.Instance.LogInfo($"信頼度閾値: {_confidenceThreshold:F2}");
            Logger.Instance.LogInfo($"前処理: {(_usePreprocessing ? "有効" : "無効")}");
            Logger.Instance.LogInfo($"適応モード: {(_useAdaptiveMode ? "有効" : "無効")}");

            if (_adaptivePreprocessor != null && _adaptivePreprocessor.CurrentPreprocessingOptions != null)
            {
                var options = _adaptivePreprocessor.CurrentPreprocessingOptions;
                Logger.Instance.LogInfo($"前処理設定: コントラスト={options.ContrastLevel:F2}, 明るさ={options.BrightnessLevel:F2}, " +
                                        $"シャープネス={options.SharpnessLevel:F2}, ノイズ除去={options.NoiseReduction}, " +
                                        $"スケール={options.ScaleFactor:F2}, パディング={options.Padding}");
            }
            else
            {
                Logger.Instance.LogWarning("前処理設定が未設定または取得できません");
            }
            Logger.Instance.LogInfo("=========================");
        }

        // UpdatePreprocessingSettings - PaddleOcrEngineへの設定伝達を確実にするメソッド
        public void UpdatePreprocessingSettings(GameTranslationOverlay.Core.Utils.PreprocessingOptions options)
        {
            if (options == null)
            {
                Logger.Instance.LogWarning("null前処理設定が更新されようとしました");
                return;
            }

            // 前処理設定を適応型プリプロセッサに設定
            if (_adaptivePreprocessor != null)
            {
                _adaptivePreprocessor.CurrentPreprocessingOptions = options;
                Logger.Instance.LogInfo("適応型プリプロセッサに前処理設定を適用しました");
            }

            // PaddleOCRエンジンに直接設定を適用
            if (_paddleOcrEngine != null)
            {
                // PreprocessingOptionsの変換（Utils -> OCR名前空間）
                var ocrOptions = PreprocessingOptions.FromUtilsOptions(options);
                _paddleOcrEngine.SetPreprocessingOptions(ocrOptions);
                // 前処理を有効化
                _paddleOcrEngine.EnablePreprocessing(_usePreprocessing);

                Logger.Instance.LogInfo("PaddleOCRエンジンに前処理設定を直接適用しました");
            }
            else
            {
                Logger.Instance.LogWarning("PaddleOCRエンジンがnullのため、設定を適用できません");
            }
        }

        // AIで最適化された設定適用のログの強化
        public void ApplySettings(OCR.AI.OcrOptimalSettings settings)
        {
            if (settings == null)
            {
                Logger.Instance.LogWarning("null設定が適用されようとしました");
                return;
            }

            Logger.Instance.LogInfo($"OCR最適化設定の適用を開始: 信頼度={settings.ConfidenceThreshold:F2}");

            // 信頼度閾値の設定
            this.SetConfidenceThreshold(settings.ConfidenceThreshold);

            // 前処理設定の適用
            var preprocessingOptions = settings.ToPreprocessingOptions();

            // 前処理の有効化
            this.EnablePreprocessing(true);

            // 設定を適応型プリプロセッサとPaddleOCRエンジン両方に確実に伝達
            this.UpdatePreprocessingSettings(preprocessingOptions);

            // 設定適用の確認ログ
            Logger.Instance.LogInfo($"OCR設定を適用しました: 信頼度={_confidenceThreshold:F2}, " +
                $"コントラスト={preprocessingOptions.ContrastLevel:F2}, " +
                $"明るさ={preprocessingOptions.BrightnessLevel:F2}, " +
                $"シャープネス={preprocessingOptions.SharpnessLevel:F2}");

            // 現在の設定を詳細にログ出力
            LogCurrentSettings();
        }

        /// <summary>
        /// リソースの解放（内部実装）
        /// </summary>
        protected virtual void Dispose(bool disposing)
        {
            if (!_isDisposed)
            {
                if (disposing)
                {
                    try
                    {
                        // キャッシュのクリア
                        _ocrCache.Clear();
                        _processingTimes.Clear();

                        // PaddleOCRエンジンは最後に破棄する
                        // (他のリソースがPaddleOCRに依存している可能性があるため)
                        if (_paddleOcrEngine != null)
                        {
                            Debug.WriteLine("PaddleOCRエンジンを解放しています...");
                            _paddleOcrEngine.Dispose();
                            _paddleOcrEngine = null;
                        }

                        // 明示的なメモリクリーンアップ
                        GC.Collect();
                        GC.WaitForPendingFinalizers();
                        GC.Collect(); // 2回目のGCで断片化を減らす
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"OcrManager破棄中のエラー: {ex.Message}");
                    }
                }

                _isDisposed = true;
            }
        }

        /// <summary>
        /// デストラクタ
        /// </summary>
        ~OcrManager()
        {
            Dispose(false);
        }

        /// <summary>
        /// ゲームプロファイル管理クラスを設定
        /// </summary>
        /// <param name="gameProfiles">ゲームプロファイル管理クラス</param>
        public void SetGameProfiles(GameProfiles gameProfiles)
        {
            _gameProfiles = gameProfiles;
            Debug.WriteLine("ゲームプロファイル管理クラスを設定しました");
        }

        /// <summary>
        /// 現在のゲームタイトルを設定し、プロファイルがあれば適用
        /// </summary>
        /// <param name="gameTitle">ゲームタイトル</param>
        /// <returns>プロファイルが適用された場合はtrue</returns>
        public bool SetCurrentGame(string gameTitle)
        {
            if (string.IsNullOrWhiteSpace(gameTitle))
            {
                Debug.WriteLine("ゲームタイトルが空のため、プロファイル適用をスキップします");
                return false;
            }

            _currentGameTitle = gameTitle;
            Debug.WriteLine($"現在のゲーム: {_currentGameTitle}");

            // プロファイルがあれば適用
            return ApplyProfileForCurrentGame();
        }

        /// <summary>
        /// 現在のゲームのプロファイルを適用
        /// </summary>
        /// <returns>プロファイルが適用された場合はtrue</returns>
        public bool ApplyProfileForCurrentGame()
        {
            if (_gameProfiles == null || string.IsNullOrWhiteSpace(_currentGameTitle))
            {
                return false;
            }

            try
            {
                // プロファイル取得
                var settings = _gameProfiles.GetProfile(_currentGameTitle);
                if (settings == null || !settings.IsOptimized)
                {
                    Debug.WriteLine($"ゲーム '{_currentGameTitle}' の有効なプロファイルがありません");
                    return false;
                }

                // 設定適用
                SetConfidenceThreshold(settings.ConfidenceThreshold);
                SetPreprocessingOptions(settings.PreprocessingOptions);
                EnablePreprocessing(true);

                Debug.WriteLine($"ゲーム '{_currentGameTitle}' のプロファイルを適用しました（信頼度: {settings.ConfidenceThreshold:F2}）");
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"プロファイル適用エラー: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 現在のゲームのプロファイルを保存/更新
        /// </summary>
        /// <returns>保存に成功した場合はtrue</returns>
        public bool SaveProfileForCurrentGame()
        {
            if (_gameProfiles == null || string.IsNullOrWhiteSpace(_currentGameTitle))
            {
                Debug.WriteLine("ゲームプロファイル保存: ゲームプロファイル管理またはゲームタイトルが未設定");
                return false;
            }

            try
            {
                // 既存のプロファイルがあれば取得
                var existingSettings = _gameProfiles.GetProfile(_currentGameTitle);
                int optimizationAttempts = existingSettings?.OptimizationAttempts ?? 0;

                // 現在の設定でプロファイルを作成/更新
                var settings = new OCR.AI.OcrOptimizer.OptimalSettings
                {
                    ConfidenceThreshold = _confidenceThreshold,
                    PreprocessingOptions = _adaptivePreprocessor.CurrentPreprocessingOptions,
                    LastOptimized = DateTime.Now,
                    OptimizationAttempts = optimizationAttempts + 1,
                    IsOptimized = true
                };

                // プロファイル保存
                _gameProfiles.SaveProfile(_currentGameTitle, settings);
                Debug.WriteLine($"ゲーム '{_currentGameTitle}' のプロファイルを保存しました");
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"プロファイル保存エラー: {ex.Message}");
                return false;
            }
        }
    }
}