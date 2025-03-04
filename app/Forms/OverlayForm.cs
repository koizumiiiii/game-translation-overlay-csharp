using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Linq;
using System.Windows.Forms;
using GameTranslationOverlay.Core.OCR;
using GameTranslationOverlay.Core.Translation.Interfaces;
using GameTranslationOverlay.Utils;
using GameTranslationOverlay.Core.UI;
using GameTranslationOverlay.Core.Translation.Services;
using GameTranslationOverlay.Core.Models;

namespace GameTranslationOverlay.Forms
{
    public partial class OverlayForm : Form
    {
        // OCR関連
        private OcrManager _ocrManager; // OcrManagerを保持するためのフィールド

        // 翻訳関連
        private TranslationManager _translationManager;

        // 翻訳表示用ウィンドウ - MainFormからアクセスできるようにpublicにする
        public TranslationBox TranslationBox { get; private set; }

        // クリックスルーの状態
        private bool _isClickThrough = true;

        // ウィンドウ位置変更のための定数とAPI
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOZORDER = 0x0004;
        private const uint SWP_FRAMECHANGED = 0x0020;
        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_TRANSPARENT = 0x00000020;

        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y,
            int cx, int cy, uint uFlags);

        [DllImport("user32.dll")]
        private static extern long GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        private static extern long SetWindowLong(IntPtr hWnd, int nIndex, long dwNewLong);

        // 自動テキスト検出関連のフィールド
        private TextDetectionService _textDetectionService;
        private List<TextRegion> _currentTextRegions = new List<TextRegion>();
        private Timer _autoHideTimer;
        private const int AUTO_HIDE_TIMEOUT = 5000; // 5秒

        // 最後に翻訳したテキスト領域
        private TextRegion _lastTranslatedRegion;
        private string _lastTranslatedText;

        // 対象ウィンドウのハンドル
        private IntPtr _targetWindowHandle = IntPtr.Zero;

        /// <summary>
        /// ターゲットウィンドウのハンドルを取得
        /// </summary>
        public IntPtr TargetWindowHandle => _targetWindowHandle;

        /// <summary>
        /// コンストラクタ
        /// </summary>
        public OverlayForm(OcrManager ocrManager, TranslationManager translationManager)
        {
            try
            {
                InitializeComponent();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"InitializeComponent failed: {ex.Message}");
                InitComponentsManually(); // フォールバック初期化
            }

            // OcrManagerの検証
            if (ocrManager == null)
                throw new ArgumentNullException(nameof(ocrManager));

            // OcrManagerを設定
            this._ocrManager = ocrManager;

            // 翻訳マネージャーを設定
            this._translationManager = translationManager ?? throw new ArgumentNullException(nameof(translationManager));

            // フォームの初期設定
            this.FormBorderStyle = FormBorderStyle.None;
            this.WindowState = FormWindowState.Maximized;
            this.TopMost = true;
            this.BackColor = Color.White;
            this.TransparencyKey = Color.White;
            this.ShowInTaskbar = false;

            // クリックスルーを有効化（デフォルト）
            SetClickThrough(true);

            // テキスト検出機能の初期化
            InitializeTextDetection();

            // 翻訳ボックスの初期化
            InitializeTranslationBox();

            Debug.WriteLine("OverlayForm: コンストラクタ完了");
        }

        /// <summary>
        /// 翻訳ボックスの初期化
        /// </summary>
        private void InitializeTranslationBox()
        {
            TranslationBox = new TranslationBox();
            TranslationBox.SetTranslationManager(_translationManager);
            Debug.WriteLine("Translation box initialized");
        }

        // フォールバック初期化（デザイナーファイルが機能しない場合用）
        private void InitComponentsManually()
        {
            this.SuspendLayout();
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(800, 600);
            this.Name = "OverlayForm";
            this.Text = "Game Translation Overlay";
            this.ResumeLayout(false);
        }

        /// <summary>
        /// テキスト検出サービスの初期化
        /// </summary>
        private void InitializeTextDetection()
        {
            try
            {
                // 一時的にテキスト検出機能を無効化
                _textDetectionService = null;
                Debug.WriteLine("テキスト検出サービスは一時的に無効化されています");

                // 自動非表示タイマーの初期化
                _autoHideTimer = new Timer
                {
                    Interval = AUTO_HIDE_TIMEOUT,
                    Enabled = false
                };
                _autoHideTimer.Tick += AutoHideTimer_Tick;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"テキスト検出サービスの初期化中にエラーが発生しました: {ex.Message}");
            }
        }

        /// <summary>
        /// クリックスルーの設定
        /// </summary>
        private void SetClickThrough(bool enable)
        {
            if (enable)
            {
                // クリックスルーを有効化
                long exStyle = GetWindowLong(this.Handle, GWL_EXSTYLE);
                SetWindowLong(this.Handle, GWL_EXSTYLE, exStyle | WS_EX_TRANSPARENT);
                _isClickThrough = true;
                Debug.WriteLine("Click-through enabled");
            }
            else
            {
                // クリックスルーを無効化
                long exStyle = GetWindowLong(this.Handle, GWL_EXSTYLE);
                SetWindowLong(this.Handle, GWL_EXSTYLE, exStyle & ~WS_EX_TRANSPARENT);
                _isClickThrough = false;
                Debug.WriteLine("Click-through disabled");
            }

            // 変更を強制的に適用
            SetWindowPos(this.Handle, IntPtr.Zero, 0, 0, 0, 0,
                SWP_NOMOVE | SWP_NOSIZE | SWP_NOZORDER | SWP_FRAMECHANGED);
        }

        /// <summary>
        /// 翻訳テキストの表示
        /// </summary>
        public void ShowTranslation(string translatedText)
        {
            if (TranslationBox == null || TranslationBox.IsDisposed)
            {
                TranslationBox = new TranslationBox();
                TranslationBox.SetTranslationManager(_translationManager);
                Debug.WriteLine("New translation box created");
            }

            TranslationBox.SetTranslationText(translatedText);

            if (!TranslationBox.Visible)
            {
                TranslationBox.Show();
                Debug.WriteLine("Translation box shown");
            }
        }

        /// <summary>
        /// 対象ウィンドウを設定
        /// </summary>
        public void SetTargetWindow(IntPtr windowHandle)
        {
            _targetWindowHandle = windowHandle;

            if (_textDetectionService != null)
            {
                _textDetectionService.SetTargetWindow(windowHandle);
            }

            // オーバーレイをウィンドウに合わせる
            AdjustOverlayToWindow(windowHandle);
        }

        /// <summary>
        /// ウィンドウの位置変更時にオーバーレイを調整
        /// </summary>
        public void AdjustToTargetWindow()
        {
            if (_targetWindowHandle != IntPtr.Zero)
            {
                AdjustOverlayToWindow(_targetWindowHandle);
            }
        }

        /// <summary>
        /// オーバーレイをウィンドウに合わせる
        /// </summary>
        private void AdjustOverlayToWindow(IntPtr windowHandle)
        {
            if (windowHandle == IntPtr.Zero)
                return;

            Rectangle rect = WindowUtils.GetWindowRect(windowHandle);
            if (!rect.IsEmpty)
            {
                int width = rect.Width;
                int height = rect.Height;

                if (width > 0 && height > 0)
                {
                    this.Location = new Point(rect.X, rect.Y);
                    this.Size = new Size(width, height);
                    Debug.WriteLine($"オーバーレイの位置を更新しました: {this.Bounds}");
                }
            }
        }

        /// <summary>
        /// オーバーレイの位置を更新する（外部から呼び出し用）
        /// </summary>
        public void UpdateOverlayPosition(Rectangle bounds)
        {
            this.Location = new Point(bounds.Left, bounds.Top);
            this.Size = new Size(bounds.Width, bounds.Height);
            this.Invalidate();
        }

        /// <summary>
        /// テキスト検出の開始/停止を切り替え
        /// </summary>
        /// <returns>新しい状態（true=有効、false=無効）</returns>
        public bool ToggleTextDetection()
        {
            if (_textDetectionService == null)
            {
                Debug.WriteLine("テキスト検出サービスが無効化されています");
                return false;
            }

            if (_textDetectionService.IsRunning)
            {
                _textDetectionService.Stop();
                return false;
            }
            else
            {
                _textDetectionService.Start();
                return true;
            }
        }

        /// <summary>
        /// テキスト検出サービスを開始
        /// </summary>
        public void StartTextDetection()
        {
            if (_textDetectionService != null && _targetWindowHandle != IntPtr.Zero)
            {
                _textDetectionService.Start();
                Debug.WriteLine("テキスト検出サービスを開始しました");
            }
            else
            {
                Debug.WriteLine("テキスト検出サービスが無効化されているか、対象ウィンドウが設定されていません");
            }
        }

        /// <summary>
        /// テキスト検出サービスを停止
        /// </summary>
        public void StopTextDetection()
        {
            if (_textDetectionService != null)
            {
                _textDetectionService.Stop();
                Debug.WriteLine("テキスト検出サービスを停止しました");
            }
        }

        /// <summary>
        /// テキスト領域検出イベントハンドラ
        /// </summary>
        private void TextDetectionService_OnRegionsDetected(object sender, List<TextRegion> regions)
        {
            _currentTextRegions = regions;
            this.Invalidate(); // 再描画して領域を表示

            // 検出されたテキスト領域が存在する場合
            if (regions.Count > 0)
            {
                // クリックスルーを無効化（テキスト領域をクリック可能にする）
                SetClickThrough(false);
                Debug.WriteLine("テキスト領域が検出されたため、クリックスルーを無効化します");

                // 自動で一番大きな（または信頼度の高い）テキスト領域を翻訳
                TextRegion bestRegion = GetBestTextRegion(regions);
                if (bestRegion != null && (_lastTranslatedRegion == null || !_lastTranslatedRegion.Equals(bestRegion)))
                {
                    Debug.WriteLine("テキスト領域を自動翻訳します");
                    TranslateTextRegion(bestRegion);
                }

                // 自動非表示タイマーをリセット
                ResetAutoHideTimer();
            }
        }

        /// <summary>
        /// テキスト領域がなくなったイベントハンドラ
        /// </summary>
        private void TextDetectionService_OnNoRegionsDetected(object sender, EventArgs e)
        {
            Debug.WriteLine("テキスト領域がなくなりました");
            CleanupUI();
        }

        /// <summary>
        /// UI要素のクリーンアップ
        /// </summary>
        private void CleanupUI()
        {
            // 翻訳ボックスを非表示にする
            HideTranslationBox();

            // ハイライト表示をクリアする
            _currentTextRegions.Clear();
            this.Invalidate();

            // クリックスルーを有効化する
            SetClickThrough(true);
            Debug.WriteLine("テキスト領域がなくなったため、クリックスルーを有効化します");

            // 自動非表示タイマーを停止する
            if (_autoHideTimer != null)
            {
                _autoHideTimer.Stop();
            }

            Debug.WriteLine("UI要素をクリーンアップしました");
        }

        /// <summary>
        /// 自動非表示タイマーのイベントハンドラ
        /// </summary>
        private void AutoHideTimer_Tick(object sender, EventArgs e)
        {
            _autoHideTimer.Stop();
            CleanupUI();
            Debug.WriteLine("自動非表示タイマーによりUIをクリーンアップしました");
        }

        /// <summary>
        /// 自動非表示タイマーのリセット
        /// </summary>
        private void ResetAutoHideTimer()
        {
            if (_autoHideTimer != null)
            {
                _autoHideTimer.Stop();
                _autoHideTimer.Start();
            }
        }

        /// <summary>
        /// 翻訳ボックスを非表示にする
        /// </summary>
        private void HideTranslationBox()
        {
            if (TranslationBox != null && !TranslationBox.IsDisposed && TranslationBox.Visible)
            {
                TranslationBox.Hide();
                Debug.WriteLine("翻訳ボックスを非表示にしました");
            }
        }

        /// <summary>
        /// 最適なテキスト領域を選択する
        /// </summary>
        private TextRegion GetBestTextRegion(List<TextRegion> regions)
        {
            if (regions == null || regions.Count == 0)
                return null;

            // 最も大きな領域または信頼度の高い領域を選択
            // ここでは単純に面積で選択していますが、アプリケーションの要件に応じて調整可能
            return regions.OrderByDescending(r => r.Bounds.Width * r.Bounds.Height).First();
        }

        /// <summary>
        /// テキスト領域を翻訳
        /// </summary>
        private async void TranslateTextRegion(TextRegion region)
        {
            try
            {
                Debug.WriteLine($"翻訳を開始: テキスト領域={region.Bounds}");
                Debug.WriteLine($"テキスト領域を翻訳開始: '{region.Text}'");

                if (string.IsNullOrWhiteSpace(region.Text))
                {
                    Debug.WriteLine("テキストが空のため翻訳をスキップします");
                    return;
                }

                // 最後に翻訳した領域を保存
                _lastTranslatedRegion = region;

                // 翻訳処理
                string translatedText = string.Empty;

                if (TranslationBox != null && !TranslationBox.IsDisposed)
                {
                    bool useAutoDetect = TranslationBox.IsUsingAutoDetect();
                    string targetLang = TranslationBox.GetSelectedTargetLanguage();

                    if (useAutoDetect)
                    {
                        // 言語自動検出を使用
                        translatedText = await _translationManager.TranslateWithAutoDetectAsync(region.Text);
                        Debug.WriteLine("言語自動検出を使用して翻訳しました");
                    }
                    else
                    {
                        // 言語を明示的に指定
                        string sourceLang = LanguageManager.DetectLanguage(region.Text);
                        translatedText = await _translationManager.TranslateAsync(region.Text, sourceLang, targetLang);
                        Debug.WriteLine($"{sourceLang}から{targetLang}へ翻訳しました");
                    }
                }
                else
                {
                    // TranslationBoxが利用できない場合
                    Debug.WriteLine("翻訳ボックスが利用できないため、デフォルトの翻訳設定を使用します");
                    translatedText = await _translationManager.TranslateWithAutoDetectAsync(region.Text);
                }

                // 最後に翻訳したテキストを保存
                _lastTranslatedText = translatedText;

                // 翻訳結果を表示
                Debug.WriteLine($"翻訳結果を表示: '{translatedText}'");
                ShowTranslation(translatedText);

                // 自動非表示タイマーをリセット
                ResetAutoHideTimer();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"翻訳処理中にエラーが発生しました: {ex.Message}");
                // ユーザーフレンドリーなエラーメッセージを表示
                ShowTranslation($"翻訳エラー: {ex.Message}");
            }
        }

        /// <summary>
        /// 描画処理（テキスト領域ハイライト表示用）
        /// </summary>
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            // テキスト領域のハイライト描画
            foreach (var region in _currentTextRegions)
            {
                // 非常に薄い半透明の塗りつぶし（アルファ値10）
                using (SolidBrush brush = new SolidBrush(Color.FromArgb(10, 0, 120, 215)))
                {
                    e.Graphics.FillRectangle(brush, region.Bounds);
                }

                // はっきりした枠線
                using (Pen pen = new Pen(Color.FromArgb(150, 0, 120, 215), 1))
                {
                    e.Graphics.DrawRectangle(pen, region.Bounds);
                }
            }
        }

        /// <summary>
        /// マウスクリック処理（テキスト領域クリック検出用）
        /// </summary>
        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);

            if (e.Button == MouseButtons.Left && !_isClickThrough && _textDetectionService != null)
            {
                Point clickPoint = e.Location;
                TextRegion clickedRegion = _textDetectionService.GetRegionAt(clickPoint);

                if (clickedRegion != null)
                {
                    Debug.WriteLine($"テキスト領域がクリックされました: {clickedRegion.Bounds}");
                    TranslateTextRegion(clickedRegion);
                }
            }
        }

        /// <summary>
        /// ウィンドウメッセージの処理
        /// </summary>
        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);
        }

        /// <summary>
        /// フォーム終了時の処理
        /// </summary>
        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            // テキスト検出サービスの破棄
            if (_textDetectionService != null)
            {
                _textDetectionService.Dispose();
                _textDetectionService = null;
                Debug.WriteLine("テキスト検出サービスを破棄しました");
            }

            // 自動非表示タイマーの破棄
            if (_autoHideTimer != null)
            {
                _autoHideTimer.Stop();
                _autoHideTimer.Dispose();
                _autoHideTimer = null;
            }

            // 翻訳ボックスの破棄
            if (TranslationBox != null && !TranslationBox.IsDisposed)
            {
                TranslationBox.Close();
                TranslationBox.Dispose();
                TranslationBox = null;
                Debug.WriteLine("翻訳ボックスを破棄しました");
            }

            base.OnFormClosed(e);
        }
    }
}