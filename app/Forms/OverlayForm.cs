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
        // 領域選択関連
        private bool isRegionSelectMode = false;
        private Pen selectionPen = new Pen(Color.Red, 2);
        private string lastRecognizedText = string.Empty;
        private const int TEXT_CHECK_INTERVAL = 1000; // テキスト変更チェック間隔（ミリ秒）

        // OCR関連
        private OcrManager _ocrManager; // OcrManagerを保持するための新しいフィールド
        private IOcrEngine _ocrEngine; // 既存のフィールドはそのまま

        // 翻訳関連
        private TranslationManager _translationManager;

        // 翻訳表示用ウィンドウ
        private TranslationBox _translationBox = null;

        // クリックスルーの状態
        private bool _isClickThrough = true;

        // ホットキーID定数
        private const int HOTKEY_TOGGLE_OVERLAY = 9001;    // Ctrl+Shift+O
        private const int HOTKEY_CLEAR_REGION = 9002;      // Ctrl+Shift+C
        private const int HOTKEY_TOGGLE_REGION_SELECT = 9003; // Ctrl+Shift+R

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

            // OcrManagerから主要OCRエンジンを取得（GetPrimaryEngineName関数をOcrManagerクラスから提供されたものと仮定）
            this._ocrManager = ocrManager ?? throw new ArgumentNullException(nameof(ocrManager));

            // OcrManagerからプライマリエンジンを取得
            string primaryEngineName = ocrManager.GetPrimaryEngineName();
            // ここでプライマリエンジンの取得方法が必要（実際のOcrManagerの実装に依存）

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

            // ホットキーの登録
            RegisterHotkeys();

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
            _translationBox = new TranslationBox();
            _translationBox.SetTranslationManager(_translationManager);
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
        /// ホットキーの登録
        /// </summary>
        private void RegisterHotkeys()
        {
            try
            {
                // まず既存のホットキーを解除
                UnregisterHotkeys();

                // オーバーレイの表示/非表示 (Ctrl+Shift+O)
                bool success1 = WindowsAPI.RegisterHotKey(
                    this.Handle,
                    HOTKEY_TOGGLE_OVERLAY,
                    WindowsAPI.MOD_CONTROL | WindowsAPI.MOD_SHIFT,
                    (int)Keys.O);
                Debug.WriteLine($"Registering Ctrl+Shift+O hotkey: {(success1 ? "Success" : "Failed")}");

                // 選択領域のクリア (Ctrl+Shift+C)
                bool success2 = WindowsAPI.RegisterHotKey(
                    this.Handle,
                    HOTKEY_CLEAR_REGION,
                    WindowsAPI.MOD_CONTROL | WindowsAPI.MOD_SHIFT,
                    (int)Keys.C);
                Debug.WriteLine($"Registering Ctrl+Shift+C hotkey: {(success2 ? "Success" : "Failed")}");

                // 領域選択モードの切り替え (Ctrl+Shift+R)
                bool success3 = WindowsAPI.RegisterHotKey(
                    this.Handle,
                    HOTKEY_TOGGLE_REGION_SELECT,
                    WindowsAPI.MOD_CONTROL | WindowsAPI.MOD_SHIFT,
                    (int)Keys.R);
                Debug.WriteLine($"Registering Ctrl+Shift+R hotkey: {(success3 ? "Success" : "Failed")}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error registering hotkeys: {ex.Message}");
            }
        }

        /// <summary>
        /// ホットキーの登録解除
        /// </summary>
        private void UnregisterHotkeys()
        {
            try
            {
                WindowsAPI.UnregisterHotKey(this.Handle, HOTKEY_TOGGLE_OVERLAY);
                WindowsAPI.UnregisterHotKey(this.Handle, HOTKEY_CLEAR_REGION);
                WindowsAPI.UnregisterHotKey(this.Handle, HOTKEY_TOGGLE_REGION_SELECT);
                Debug.WriteLine("All hotkeys unregistered");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error unregistering hotkeys: {ex.Message}");
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
            if (_translationBox == null || _translationBox.IsDisposed)
            {
                _translationBox = new TranslationBox();
                _translationBox.SetTranslationManager(_translationManager);
                Debug.WriteLine("New translation box created");
            }

            _translationBox.SetTranslationText(translatedText);

            if (!_translationBox.Visible)
            {
                _translationBox.Show();
                Debug.WriteLine("Translation box shown");
            }
        }

        /// <summary>
        /// テキスト検出サービスの初期化
        /// </summary>
        private void InitializeTextDetection()
        {
            // テキスト検出サービスの初期化（_ocrEngineを使用）
            _textDetectionService = new TextDetectionService(_ocrEngine);
            _textDetectionService.OnRegionsDetected += TextDetectionService_OnRegionsDetected;
            _textDetectionService.OnNoRegionsDetected += TextDetectionService_OnNoRegionsDetected;

            // 自動非表示タイマーの初期化
            _autoHideTimer = new Timer
            {
                Interval = AUTO_HIDE_TIMEOUT,
                Enabled = false
            };
            _autoHideTimer.Tick += AutoHideTimer_Tick;

            Debug.WriteLine("テキスト検出サービスが初期化されました");
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

                // オーバーレイをウィンドウに合わせる
                AdjustOverlayToWindow(windowHandle);
            }
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
                return false;

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
            if (_translationBox != null && !_translationBox.IsDisposed && _translationBox.Visible)
            {
                _translationBox.Hide();
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

                if (_translationBox != null && !_translationBox.IsDisposed)
                {
                    bool useAutoDetect = _translationBox.IsUsingAutoDetect();
                    string targetLang = _translationBox.GetSelectedTargetLanguage();

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
                    // _translationBoxが利用できない場合
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

            if (e.Button == MouseButtons.Left && !_isClickThrough)
            {
                Point clickPoint = e.Location;
                TextRegion clickedRegion = _textDetectionService?.GetRegionAt(clickPoint);

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

            // ホットキーメッセージの処理
            if (m.Msg == 0x0312) // WM_HOTKEY
            {
                int id = m.WParam.ToInt32();
                Debug.WriteLine($"Hotkey message received: ID={id}");

                switch (id)
                {
                    case HOTKEY_TOGGLE_OVERLAY: // Ctrl+Shift+O
                        Debug.WriteLine("Processing HOTKEY_TOGGLE_OVERLAY");
                        // 翻訳ボックスの表示/非表示を切り替え
                        if (_translationBox != null && !_translationBox.IsDisposed)
                        {
                            _translationBox.Visible = !_translationBox.Visible;
                        }
                        break;

                    case HOTKEY_CLEAR_REGION: // Ctrl+Shift+C
                        Debug.WriteLine("Processing HOTKEY_CLEAR_REGION");
                        // 選択領域のクリア
                        _currentTextRegions.Clear();
                        this.Invalidate();
                        break;

                    case HOTKEY_TOGGLE_REGION_SELECT: // Ctrl+Shift+R
                        Debug.WriteLine("Processing HOTKEY_TOGGLE_REGION_SELECT");
                        // 領域選択モードの切り替え
                        isRegionSelectMode = !isRegionSelectMode;
                        SetClickThrough(!isRegionSelectMode);
                        break;

                    default:
                        Debug.WriteLine($"Unknown hotkey ID: {id}");
                        break;
                }
            }
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
            if (_translationBox != null && !_translationBox.IsDisposed)
            {
                _translationBox.Close();
                _translationBox.Dispose();
                _translationBox = null;
                Debug.WriteLine("翻訳ボックスを破棄しました");
            }

            // ホットキーの解除
            UnregisterHotkeys();

            base.OnFormClosed(e);
        }
    }
}