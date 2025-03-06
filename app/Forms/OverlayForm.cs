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
using GameTranslationOverlay.Core.Translation.Services;

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

        // テキスト検出設定
        private bool _enableHighlighting = true; // テキスト領域ハイライト表示の有効/無効
        private Color _highlightColor = Color.FromArgb(30, 0, 120, 215); // ハイライト色（薄い青）
        private Color _borderColor = Color.FromArgb(150, 0, 120, 215); // 境界線の色（より濃い青）

        // 翻訳クールダウン制御
        private DateTime _lastTranslationTime = DateTime.MinValue;
        private const int TRANSLATION_COOLDOWN_MS = 300; // 翻訳要求の最小間隔（ミリ秒）

        // UI状態
        private bool _showDebugInfo = false; // デバッグ情報表示の有効/無効

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

            // 追加のイベントハンドラー登録
            this.KeyDown += OverlayForm_KeyDown;

            Debug.WriteLine("OverlayForm: コンストラクタ完了");
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
        /// 翻訳ボックスの初期化
        /// </summary>
        private void InitializeTranslationBox()
        {
            TranslationBox = new TranslationBox();
            TranslationBox.SetTranslationManager(_translationManager);

            // 初期言語設定（日本語をメインターゲットにする）
            TranslationBox.SetTargetLanguage("ja");
            TranslationBox.SetAutoDetect(true);

            Debug.WriteLine("Translation box initialized");
        }

        /// <summary>
        /// テキスト検出サービスの初期化（更新版）
        /// </summary>
        private void InitializeTextDetection()
        {
            try
            {
                // 既存のOcrManagerを使用してTextDetectionServiceを初期化
                _textDetectionService = new TextDetectionService(_ocrManager);
                _textDetectionService.OnRegionsDetected += TextDetectionService_OnRegionsDetected;
                _textDetectionService.OnNoRegionsDetected += TextDetectionService_OnNoRegionsDetected;

                // テキスト検出が機能しやすいように設定を最適化
                _textDetectionService.SetMinimumConfidence(0.5f); // 信頼度の閾値を少し下げる
                _textDetectionService.EnableChangeDetection(true); // テキスト変更検知を有効化
                _textDetectionService.EnableDynamicInterval(true); // 動的間隔調整を有効化
                _textDetectionService.SetDetectionInterval(800); // より頻繁に検出（ミリ秒）

                Debug.WriteLine("テキスト検出サービスを初期化しました");

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
                // エラーが発生した場合は一時的に無効化
                _textDetectionService = null;
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
                Debug.WriteLine($"テキスト検出サービスの対象ウィンドウを設定しました: {windowHandle}");
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
                // TextDetectionServiceが無効の場合は再作成を試みる
                InitializeTextDetection();

                if (_textDetectionService == null)
                {
                    Debug.WriteLine("テキスト検出サービスの作成に失敗しました");
                    return false;
                }
            }

            if (_textDetectionService.IsRunning)
            {
                _textDetectionService.Stop();
                return false;
            }
            else
            {
                if (_targetWindowHandle != IntPtr.Zero)
                {
                    _textDetectionService.SetTargetWindow(_targetWindowHandle);
                    _textDetectionService.Start();
                    return true;
                }
                else
                {
                    Debug.WriteLine("対象ウィンドウが設定されていないため、テキスト検出を開始できません");
                    return false;
                }
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
                if (_textDetectionService == null)
                {
                    Debug.WriteLine("テキスト検出サービスが初期化されていないため、検出を開始できません");
                }
                else
                {
                    Debug.WriteLine("対象ウィンドウが設定されていないため、テキスト検出を開始できません");
                }
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
        /// テキスト領域検出イベントハンドラ（更新版）
        /// </summary>
        private void TextDetectionService_OnRegionsDetected(object sender, List<TextRegion> regions)
        {
            if (regions == null || regions.Count == 0)
                return;

            _currentTextRegions = regions;
            this.Invalidate(); // 再描画して領域を表示

            // クリックスルーを無効化（テキスト領域をクリック可能にする）
            SetClickThrough(false);
            Debug.WriteLine($"{regions.Count}個のテキスト領域が検出されたため、クリックスルーを無効化します");

            // 自動で一番大きな（または信頼度の高い）テキスト領域を翻訳
            TextRegion bestRegion = GetBestTextRegion(regions);
            if (bestRegion != null && (_lastTranslatedRegion == null || !IsSameTextRegion(_lastTranslatedRegion, bestRegion)))
            {
                // 翻訳のクールダウンチェック
                if ((DateTime.Now - _lastTranslationTime).TotalMilliseconds > TRANSLATION_COOLDOWN_MS)
                {
                    Debug.WriteLine("テキスト領域を自動翻訳します");
                    TranslateTextRegion(bestRegion);
                    _lastTranslationTime = DateTime.Now;
                }
                else
                {
                    Debug.WriteLine("翻訳クールダウン中のため、自動翻訳をスキップします");
                }
            }

            // 自動非表示タイマーをリセット
            ResetAutoHideTimer();
        }

        /// <summary>
        /// 2つのテキスト領域が同じかどうかをチェック
        /// </summary>
        private bool IsSameTextRegion(TextRegion a, TextRegion b)
        {
            // テキスト内容が同じであれば、同じ領域とみなす（より単純で堅牢な比較）
            return a != null && b != null && a.Text == b.Text;
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
        /// 最適なテキスト領域を選択する（更新版）
        /// </summary>
        private TextRegion GetBestTextRegion(List<TextRegion> regions)
        {
            if (regions == null || regions.Count == 0)
                return null;

            // 言語検出を使って、対象言語に基づいてテキスト領域を選択
            string targetLang = "ja"; // デフォルト

            // 翻訳ボックスから設定を取得
            if (TranslationBox != null && !TranslationBox.IsDisposed)
            {
                targetLang = TranslationBox.GetSelectedTargetLanguage();
            }

            // 設定されたターゲット言語に基づき、違う言語のテキストを優先的に選択
            var oppositeLanguageRegions = new List<TextRegion>();
            foreach (var region in regions)
            {
                if (!string.IsNullOrWhiteSpace(region.Text))
                {
                    string detectedLang = LanguageManager.DetectLanguage(region.Text);
                    if (detectedLang != targetLang)
                    {
                        oppositeLanguageRegions.Add(region);
                    }
                }
            }

            // 対象言語と異なる言語のテキストがある場合は、そのうち最も良いものを選択
            if (oppositeLanguageRegions.Count > 0)
            {
                return GetHighestQualityRegion(oppositeLanguageRegions);
            }

            // 言語で分けられなかった場合は、全てのテキスト領域から最も良いものを選択
            return GetHighestQualityRegion(regions);
        }

        /// <summary>
        /// 最も品質の高いテキスト領域を選択
        /// </summary>
        private TextRegion GetHighestQualityRegion(List<TextRegion> regions)
        {
            // 最低限の長さチェック
            var nonEmptyRegions = regions.Where(r => !string.IsNullOrWhiteSpace(r.Text) && r.Text.Length > 2).ToList();
            if (nonEmptyRegions.Count == 0)
                return regions.FirstOrDefault(); // 空でも何かしら返す

            // テキスト領域の「品質」を判定（信頼度×面積×テキスト長の組み合わせ）
            return nonEmptyRegions
                .OrderByDescending(r => r.Confidence * r.Bounds.Width * r.Bounds.Height * Math.Min(r.Text.Length, 30))
                .First();
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

                    // テキスト検出サービスに対象言語を伝える（最適化のため）
                    if (_textDetectionService != null)
                    {
                        _textDetectionService.TargetLanguage = targetLang;
                    }

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

                        // 同じ言語への翻訳を避ける
                        if (sourceLang == targetLang)
                        {
                            targetLang = (sourceLang == "en") ? "ja" : "en";
                            Debug.WriteLine($"ソース言語と対象言語が同じため、対象を {targetLang} に変更します");
                        }

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
        /// 描画処理（テキスト領域ハイライト表示用）- 更新版
        /// </summary>
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            // ハイライト表示が無効の場合は何もしない
            if (!_enableHighlighting)
                return;

            // テキスト領域のハイライト描画
            foreach (var region in _currentTextRegions)
            {
                // 半透明の塗りつぶし
                using (SolidBrush brush = new SolidBrush(_highlightColor))
                {
                    e.Graphics.FillRectangle(brush, region.Bounds);
                }

                // 枠線
                using (Pen pen = new Pen(_borderColor, 1))
                {
                    e.Graphics.DrawRectangle(pen, region.Bounds);
                }

                // デバッグ情報の表示（オプション）
                if (_showDebugInfo && !string.IsNullOrEmpty(region.Text))
                {
                    string debugText = $"{region.Confidence:F2}";
                    Font debugFont = new Font("Arial", 7);
                    SizeF textSize = e.Graphics.MeasureString(debugText, debugFont);

                    // 背景付きのテキスト表示
                    Rectangle textRect = new Rectangle(
                        region.Bounds.Left,
                        region.Bounds.Top - (int)textSize.Height,
                        (int)textSize.Width,
                        (int)textSize.Height);

                    e.Graphics.FillRectangle(new SolidBrush(Color.FromArgb(180, 0, 0, 0)), textRect);
                    e.Graphics.DrawString(
                        debugText,
                        debugFont,
                        Brushes.White,
                        region.Bounds.Left,
                        region.Bounds.Top - textSize.Height);
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

                // テキスト検出サービスから直接クリック位置のテキスト領域を取得
                TextRegion clickedRegion = null;
                if (_textDetectionService != null)
                {
                    clickedRegion = _textDetectionService.GetRegionAt(clickPoint);
                }
                else
                {
                    // テキスト検出サービスが利用できない場合は、現在表示中の領域から検索
                    clickedRegion = _currentTextRegions.FirstOrDefault(r => r.Bounds.Contains(clickPoint));
                }

                if (clickedRegion != null)
                {
                    Debug.WriteLine($"テキスト領域がクリックされました: {clickedRegion.Bounds}");
                    TranslateTextRegion(clickedRegion);
                }
            }
        }

        /// <summary>
        /// テキスト領域ハイライト表示の有効/無効を切り替え
        /// </summary>
        public void ToggleHighlighting()
        {
            _enableHighlighting = !_enableHighlighting;
            this.Invalidate();
            Debug.WriteLine($"テキスト領域ハイライト表示を {(_enableHighlighting ? "有効" : "無効")} にしました");
        }

        /// <summary>
        /// デバッグ情報表示の有効/無効を切り替え
        /// </summary>
        public void ToggleDebugInfo()
        {
            _showDebugInfo = !_showDebugInfo;
            this.Invalidate();
            Debug.WriteLine($"デバッグ情報表示を {(_showDebugInfo ? "有効" : "無効")} にしました");
        }

        /// <summary>
        /// キーボードショートカットの処理
        /// </summary>
        private void OverlayForm_KeyDown(object sender, KeyEventArgs e)
        {
            // F1キー: ヘルプ表示
            if (e.KeyCode == Keys.F1)
            {
                ShowHelp();
                e.Handled = true;
            }

            // F2キー: ハイライト表示の切り替え
            else if (e.KeyCode == Keys.F2)
            {
                ToggleHighlighting();
                e.Handled = true;
            }

            // F3キー: デバッグ情報表示の切り替え
            else if (e.KeyCode == Keys.F3)
            {
                ToggleDebugInfo();
                e.Handled = true;
            }

            // Escキー: オーバーレイを閉じる
            else if (e.KeyCode == Keys.Escape)
            {
                CleanupUI();
                e.Handled = true;
            }
        }

        /// <summary>
        /// ヘルプ情報を表示
        /// </summary>
        private void ShowHelp()
        {
            string helpText =
                "ショートカットキー:\n" +
                "F1: このヘルプを表示\n" +
                "F2: テキスト領域ハイライトの切り替え\n" +
                "F3: デバッグ情報表示の切り替え\n" +
                "Esc: オーバーレイを閉じる";

            MessageBox.Show(
                helpText,
                "ゲーム翻訳オーバーレイ - ヘルプ",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
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