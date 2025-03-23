using System;
using System.Diagnostics;
using System.Drawing;
using System.Threading.Tasks;
using System.Linq;
using GameTranslationOverlay.Core.Diagnostics;

namespace GameTranslationOverlay.Core.OCR
{
    /// <summary>
    /// OCR最適化クラス
    /// </summary>
    public class OcrOptimizer
    {
        private readonly IOcrEngine _ocrEngine;
        private bool _isOptimizing = false;

        public OcrOptimizer(IOcrEngine ocrEngine)
        {
            _ocrEngine = ocrEngine ?? throw new ArgumentNullException(nameof(ocrEngine));
        }

        /// <summary>
        /// 最適なOCR設定を見つけるためのテストを実行します
        /// </summary>
        public async Task<OcrSettings> RunOptimizationAsync(Bitmap sampleScreen)
        {
            if (sampleScreen == null)
            {
                Logger.Instance.Error("OcrOptimizer", "最適化のためのサンプル画像がnullです", null);
                return OcrSettings.Default;
            }

            if (_isOptimizing)
            {
                Debug.WriteLine("[OCR最適化] 既に最適化プロセスが進行中です");
                return OcrSettings.Default;
            }

            _isOptimizing = true;
            OcrSettings bestSettings = OcrSettings.Default;

            try
            {
                Debug.WriteLine("[OCR最適化] 最適化プロセスを開始します...");

                // まず現在の設定で結果を取得
                var defaultSettings = OcrSettings.Default;
                var baselineRegions = await _ocrEngine.DetectTextRegionsAsync(sampleScreen);

                if (baselineRegions == null || baselineRegions.Count == 0)
                {
                    Debug.WriteLine("[OCR最適化] ベースラインでテキスト領域が検出されませんでした。最適化を中止します。");
                    Logger.Instance.Error("OcrOptimizer", "ベースラインでテキスト領域が検出されませんでした", null);
                    return OcrSettings.Default;
                }

                Debug.WriteLine($"[OCR最適化] ベースライン: {baselineRegions.Count}個のテキスト領域が検出されました");

                // 以下、最適化ロジックを実装
                // これは単純な例です。実際には様々なパラメータの組み合わせをテストします

                return bestSettings;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[OCR最適化] エラー: {ex.Message}");
                Logger.Instance.Error("OcrOptimizer", $"OCR最適化プロセス中にエラーが発生しました: {ex.Message}", ex);
                return OcrSettings.Default;
            }
            finally
            {
                _isOptimizing = false;
                Debug.WriteLine("[OCR最適化] 最適化プロセスが完了しました");
            }
        }
    }
} 