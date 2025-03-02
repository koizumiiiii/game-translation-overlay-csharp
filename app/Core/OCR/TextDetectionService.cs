using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using GameTranslationOverlay.Utils;
using GameTranslationOverlay.Core.UI;

namespace GameTranslationOverlay.Core.OCR
{
    public class TextDetectionService : IDisposable
    {
        // プロパティと変数の定義をそのまま維持
        private readonly IOcrEngine ocrEngine;
        private Timer detectionTimer;
        private List<TextRegion> detectedRegions = new List<TextRegion>();
        private IntPtr targetWindowHandle;
        private bool disposed = false;
        private bool isRunning = false;
        private int noRegionsDetectedCount = 0;
        private const int NO_REGIONS_THRESHOLD = 3;
        private int detectionInterval = 1000;
        private float minimumConfidence = 0.6f;

        // プロパティの定義
        public bool IsRunning => isRunning;

        // イベント
        public event EventHandler<List<TextRegion>> OnRegionsDetected;
        public event EventHandler OnNoRegionsDetected;

        public TextDetectionService(IOcrEngine ocrEngine)
        {
            this.ocrEngine = ocrEngine ?? throw new ArgumentNullException(nameof(ocrEngine));

            // 検出タイマーの初期化
            detectionTimer = new Timer
            {
                Interval = detectionInterval,
                Enabled = false
            };
            detectionTimer.Tick += DetectionTimer_Tick;
        }

        public void SetTargetWindow(IntPtr windowHandle)
        {
            targetWindowHandle = windowHandle;
            Debug.WriteLine($"テキスト検出対象ウィンドウが設定されました: {windowHandle}");
        }

        public void Start()
        {
            if (targetWindowHandle == IntPtr.Zero)
            {
                Debug.WriteLine("ターゲットウィンドウが設定されていないため、テキスト検出を開始できません");
                return;
            }

            if (!isRunning)
            {
                isRunning = true;
                detectionTimer.Start();
                Debug.WriteLine("テキスト検出サービスを開始しました");
            }
        }

        public void Stop()
        {
            if (isRunning)
            {
                detectionTimer.Stop();
                isRunning = false;
                Debug.WriteLine("テキスト検出サービスを停止しました");
            }
        }

        public void SetDetectionInterval(int milliseconds)
        {
            detectionInterval = milliseconds;
            detectionTimer.Interval = milliseconds;
            Debug.WriteLine($"検出間隔を{milliseconds}ミリ秒に設定しました");
        }

        public List<TextRegion> GetDetectedRegions()
        {
            return new List<TextRegion>(detectedRegions);
        }

        public TextRegion GetRegionAt(Point point)
        {
            return detectedRegions.FirstOrDefault(r => r.Bounds.Contains(point));
        }

        private async void DetectionTimer_Tick(object sender, EventArgs e)
        {
            try
            {
                // タイマーを一時停止
                detectionTimer.Stop();

                if (targetWindowHandle == IntPtr.Zero)
                {
                    return;
                }

                // ウィンドウの矩形情報を取得
                Rectangle rect = WindowUtils.GetWindowRect(targetWindowHandle);
                if (rect.IsEmpty)
                {
                    Debug.WriteLine("ウィンドウの矩形情報を取得できませんでした");
                    return;
                }

                // ウィンドウが最小化されていないか確認
                int width = rect.Width;
                int height = rect.Height;
                if (width <= 0 || height <= 0)
                {
                    Debug.WriteLine("ウィンドウが最小化されているか、サイズが無効です");
                    return;
                }

                // ウィンドウをキャプチャ
                using (Bitmap windowCapture = ScreenCapture.CaptureWindow(targetWindowHandle))
                {
                    if (windowCapture == null)
                    {
                        Debug.WriteLine("ウィンドウのキャプチャに失敗しました");
                        return;
                    }

                    // テキスト領域の検出
                    var regions = await ocrEngine.DetectTextRegionsAsync(windowCapture);

                    // 最低信頼度でフィルタリング
                    regions = regions.Where(r => r.Confidence >= minimumConfidence).ToList();

                    // 前回と今回の検出結果を比較
                    bool hadRegionsBefore = detectedRegions.Count > 0;
                    bool hasRegionsNow = regions.Count > 0;

                    // スクリーン座標に変換
                    if (hasRegionsNow)
                    {
                        foreach (var region in regions)
                        {
                            // キャプチャ内の相対座標からスクリーン座標に変換
                            region.Bounds = new Rectangle(
                                rect.Left + region.Bounds.X,
                                rect.Top + region.Bounds.Y,
                                region.Bounds.Width,
                                region.Bounds.Height);
                        }

                        // 検出結果を保存
                        detectedRegions = regions;
                        noRegionsDetectedCount = 0;

                        // 結果を通知
                        Debug.WriteLine($"{regions.Count}個のテキスト領域を検出しました");
                        OnRegionsDetected?.Invoke(this, regions);
                    }
                    else
                    {
                        // テキスト領域が検出されなかった
                        Debug.WriteLine("テキスト領域は検出されませんでした");
                        noRegionsDetectedCount++;

                        // しきい値を超えて検出されなかった場合
                        if (hadRegionsBefore && noRegionsDetectedCount >= NO_REGIONS_THRESHOLD)
                        {
                            detectedRegions.Clear();
                            OnNoRegionsDetected?.Invoke(this, EventArgs.Empty);
                            Debug.WriteLine($"テキスト領域が{NO_REGIONS_THRESHOLD}回連続で検出されなかったため、クリーンアップイベントを発行します");
                            noRegionsDetectedCount = 0;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"テキスト検出エラー: {ex.Message}");
            }
            finally
            {
                // タイマーを再開（サービスが実行中の場合）
                if (isRunning && targetWindowHandle != IntPtr.Zero)
                {
                    detectionTimer.Start();
                }
            }
        }

        public void Dispose()
        {
            Stop();
            detectionTimer?.Dispose();
        }
    }
}