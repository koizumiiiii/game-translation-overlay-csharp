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
        // OcrManagerを使用するように変更
        private readonly OcrManager _ocrManager;
        private Timer _detectionTimer;
        private List<TextRegion> _detectedRegions = new List<TextRegion>();
        private IntPtr _targetWindowHandle;
        private bool _isRunning = false;
        private int _noRegionsDetectedCount = 0;
        private const int NO_REGIONS_THRESHOLD = 3;
        private int _detectionInterval = 1000;
        private float _minimumConfidence = 0.6f;

        // プロパティの定義
        public bool IsRunning => _isRunning;

        // イベント
        public event EventHandler<List<TextRegion>> OnRegionsDetected;
        public event EventHandler OnNoRegionsDetected;

        /// <summary>
        /// OcrManagerを使用するTextDetectionServiceのコンストラクタ
        /// </summary>
        /// <param name="ocrManager">使用するOCRマネージャ</param>
        public TextDetectionService(OcrManager ocrManager)
        {
            _ocrManager = ocrManager ?? throw new ArgumentNullException(nameof(ocrManager));

            // 検出タイマーの初期化
            _detectionTimer = new Timer
            {
                Interval = _detectionInterval,
                Enabled = false
            };
            _detectionTimer.Tick += DetectionTimer_Tick;
        }

        public void SetTargetWindow(IntPtr windowHandle)
        {
            _targetWindowHandle = windowHandle;
            Debug.WriteLine($"テキスト検出対象ウィンドウが設定されました: {windowHandle}");
        }

        public void Start()
        {
            if (_targetWindowHandle == IntPtr.Zero)
            {
                Debug.WriteLine("ターゲットウィンドウが設定されていないため、テキスト検出を開始できません");
                return;
            }

            if (!_isRunning)
            {
                _isRunning = true;
                _detectionTimer.Start();
                Debug.WriteLine("テキスト検出サービスを開始しました");
            }
        }

        public void Stop()
        {
            if (_isRunning)
            {
                _detectionTimer.Stop();
                _isRunning = false;
                Debug.WriteLine("テキスト検出サービスを停止しました");
            }
        }

        public void SetDetectionInterval(int milliseconds)
        {
            _detectionInterval = milliseconds;
            _detectionTimer.Interval = milliseconds;
            Debug.WriteLine($"検出間隔を{milliseconds}ミリ秒に設定しました");
        }

        public void SetMinimumConfidence(float confidence)
        {
            _minimumConfidence = Math.Max(0.0f, Math.Min(1.0f, confidence));
            Debug.WriteLine($"最小信頼度を{_minimumConfidence:P0}に設定しました");
        }

        public List<TextRegion> GetDetectedRegions()
        {
            return new List<TextRegion>(_detectedRegions);
        }

        public TextRegion GetRegionAt(Point point)
        {
            return _detectedRegions.FirstOrDefault(r => r.Bounds.Contains(point));
        }

        private async void DetectionTimer_Tick(object sender, EventArgs e)
        {
            try
            {
                // タイマーを一時停止
                _detectionTimer.Stop();

                if (_targetWindowHandle == IntPtr.Zero)
                {
                    return;
                }

                // ウィンドウの矩形情報を取得
                Rectangle rect = WindowUtils.GetWindowRect(_targetWindowHandle);
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
                using (Bitmap windowCapture = ScreenCapture.CaptureWindow(_targetWindowHandle))
                {
                    if (windowCapture == null)
                    {
                        Debug.WriteLine("ウィンドウのキャプチャに失敗しました");
                        return;
                    }

                    // OcrManagerを使用してテキスト領域を検出
                    var regions = await _ocrManager.DetectTextRegionsAsync(windowCapture);

                    // 最低信頼度でフィルタリング（OcrManagerですでにフィルタリングされている可能性がある）
                    regions = regions.Where(r => r.Confidence >= _minimumConfidence).ToList();

                    // 前回と今回の検出結果を比較
                    bool hadRegionsBefore = _detectedRegions.Count > 0;
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
                        _detectedRegions = regions;
                        _noRegionsDetectedCount = 0;

                        // 結果を通知
                        Debug.WriteLine($"{regions.Count}個のテキスト領域を検出しました");
                        OnRegionsDetected?.Invoke(this, regions);
                    }
                    else
                    {
                        // テキスト領域が検出されなかった
                        Debug.WriteLine("テキスト領域は検出されませんでした");
                        _noRegionsDetectedCount++;

                        // しきい値を超えて検出されなかった場合
                        if (hadRegionsBefore && _noRegionsDetectedCount >= NO_REGIONS_THRESHOLD)
                        {
                            _detectedRegions.Clear();
                            OnNoRegionsDetected?.Invoke(this, EventArgs.Empty);
                            Debug.WriteLine($"テキスト領域が{NO_REGIONS_THRESHOLD}回連続で検出されなかったため、クリーンアップイベントを発行します");
                            _noRegionsDetectedCount = 0;
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
                if (_isRunning && _targetWindowHandle != IntPtr.Zero)
                {
                    _detectionTimer.Start();
                }
            }
        }

        public void Dispose()
        {
            Stop();
            _detectionTimer?.Dispose();
        }
    }
}