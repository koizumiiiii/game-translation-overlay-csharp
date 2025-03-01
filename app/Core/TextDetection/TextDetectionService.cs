﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using GameTranslationOverlay.Core.OCR;
using GameTranslationOverlay.Utils;

namespace GameTranslationOverlay.Core.TextDetection
{
    /// <summary>
    /// テキスト検出サービス
    /// </summary>
    public class TextDetectionService : IDisposable
    {
        private readonly IOcrEngine ocrEngine;
        private Timer detectionTimer;
        private List<TextRegion> detectedRegions = new List<TextRegion>();
        private IntPtr targetWindowHandle;
        private bool disposed = false;

        // テキスト領域が検出されなかった連続回数
        private int noRegionsDetectedCount = 0;

        // テキスト領域が検出されなくなったと判断するしきい値
        private const int NO_REGIONS_THRESHOLD = 3;

        /// <summary>
        /// コンストラクタ
        /// </summary>
        /// <param name="ocrEngine">OCRエンジン</param>
        public TextDetectionService(IOcrEngine ocrEngine)
        {
            this.ocrEngine = ocrEngine ?? throw new ArgumentNullException(nameof(ocrEngine));

            // 定期的なテキスト検出用タイマー
            detectionTimer = new Timer
            {
                Interval = 1000, // 1秒ごとに検出（適宜調整）
                Enabled = false
            };
            detectionTimer.Tick += DetectionTimer_Tick;

            Debug.WriteLine("テキスト検出サービスが初期化されました");
        }

        /// <summary>
        /// 対象ウィンドウを設定
        /// </summary>
        /// <param name="windowHandle">対象ウィンドウのハンドル</param>
        public void SetTargetWindow(IntPtr windowHandle)
        {
            targetWindowHandle = windowHandle;
            Debug.WriteLine($"テキスト検出対象ウィンドウが設定されました: {windowHandle.ToInt32()}");
        }

        /// <summary>
        /// テキスト検出を開始
        /// </summary>
        public void Start()
        {
            if (targetWindowHandle == IntPtr.Zero)
            {
                Debug.WriteLine("対象ウィンドウが設定されていないため、テキスト検出サービスを開始できません");
                return;
            }

            detectionTimer.Start();
            Debug.WriteLine("テキスト検出サービスを開始しました");
        }

        /// <summary>
        /// テキスト検出を停止
        /// </summary>
        public void Stop()
        {
            detectionTimer.Stop();
            Debug.WriteLine("テキスト検出サービスを停止しました");
        }

        /// <summary>
        /// 検出されたテキスト領域を取得
        /// </summary>
        public List<TextRegion> GetDetectedRegions()
        {
            return new List<TextRegion>(detectedRegions);
        }

        /// <summary>
        /// テキスト検出タイマーのイベントハンドラ
        /// </summary>
        private async void DetectionTimer_Tick(object sender, EventArgs e)
        {
            try
            {
                // タイマーを一時停止
                detectionTimer.Stop();

                if (targetWindowHandle == IntPtr.Zero || !WindowUtils.IsWindowValid(targetWindowHandle))
                {
                    Debug.WriteLine("対象ウィンドウが無効なため、検出をスキップします");
                    return;
                }

                // ターゲットウィンドウの領域を取得
                Rectangle windowRect = WindowUtils.GetWindowRect(targetWindowHandle);

                // ウィンドウが最小化されていないか確認
                if (windowRect.Width <= 0 || windowRect.Height <= 0)
                {
                    Debug.WriteLine("ウィンドウが最小化されているか、サイズが無効です");
                    return;
                }

                // ウィンドウ領域をキャプチャ
                using (Bitmap windowCapture = ScreenCapture.CaptureWindow(targetWindowHandle))
                {
                    if (windowCapture == null)
                    {
                        Debug.WriteLine("ウィンドウキャプチャに失敗しました");
                        return;
                    }

                    // テキスト領域の検出
                    var regions = await ocrEngine.DetectTextRegionsAsync(windowCapture);

                    // 前回と今回の検出結果を比較
                    bool hadRegionsBefore = detectedRegions.Count > 0;
                    bool hasRegionsNow = regions.Count > 0;

                    if (hasRegionsNow)
                    {
                        // テキスト領域が検出された
                        noRegionsDetectedCount = 0;
                        Debug.WriteLine($"{regions.Count}個のテキスト領域を検出しました");

                        // 検出された領域を保存
                        detectedRegions = regions;

                        // テキスト領域が検出されたことを通知
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
                // タイマーを再開（オブジェクトが破棄されていない場合のみ）
                if (!disposed && targetWindowHandle != IntPtr.Zero)
                {
                    detectionTimer.Start();
                }
            }
        }

        /// <summary>
        /// テキスト領域検出イベント
        /// </summary>
        public event EventHandler<List<TextRegion>> OnRegionsDetected;

        /// <summary>
        /// テキスト領域がなくなったことを通知するイベント
        /// </summary>
        public event EventHandler OnNoRegionsDetected;

        /// <summary>
        /// 特定座標のテキスト領域を取得
        /// </summary>
        public TextRegion GetRegionAt(Point point)
        {
            return detectedRegions.FirstOrDefault(r => r.Bounds.Contains(point));
        }

        /// <summary>
        /// リソースの破棄
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// リソースの破棄（内部実装）
        /// </summary>
        protected virtual void Dispose(bool disposing)
        {
            if (!disposed)
            {
                if (disposing)
                {
                    // マネージドリソースの破棄
                    Stop();
                    if (detectionTimer != null)
                    {
                        detectionTimer.Tick -= DetectionTimer_Tick;
                        detectionTimer.Dispose();
                        detectionTimer = null;
                    }
                }

                // アンマネージドリソースの破棄（必要に応じて）

                disposed = true;
            }
        }

        /// <summary>
        /// ファイナライザ
        /// </summary>
        ~TextDetectionService()
        {
            Dispose(false);
        }
    }
}