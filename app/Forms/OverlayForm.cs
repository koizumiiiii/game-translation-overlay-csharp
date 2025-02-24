using System;
using System.Drawing;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.Linq;
using GameTranslationOverlay.Core.OCR;
using GameTranslationOverlay.Core.Translation;
using GameTranslationOverlay.Core.Translation.Services;
using GameTranslationOverlay.Core.Translation.Interfaces;
using GameTranslationOverlay.Core.Translation.Models;
using GameTranslationOverlay.Forms;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using GameTranslationOverlay.Core.Translation.Configuration;

namespace GameTranslationOverlay
{
    public partial class OverlayForm : Form
    {
        // Win32 API
        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, int fsModifiers, int vk);

        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, uint dwNewLong);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr GetWindow(IntPtr hWnd, uint uCmd);

        private const uint GW_HWNDNEXT = 2;


        // Win32 定数
        private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOACTIVATE = 0x0010;
        private const int GWL_EXSTYLE = -20;
        private const uint WS_EX_LAYERED = 0x80000;
        private const uint WS_EX_TRANSPARENT = 0x20;

        // モディファイヤーキーの定義
        private const int MOD_CONTROL = 0x0002;
        private const int MOD_SHIFT = 0x0004;

        // ホットキーのID
        private const int HOTKEY_ID_OVERLAY = 1;
        private const int HOTKEY_ID_REGION_SELECT = 2;
        private const int HOTKEY_ID_CLEAR = 3;

        // メッセージ定数
        private const int WM_HOTKEY = 0x0312;

        // リソース制限
        private const int MAX_AREAS = 30;
        private const long MAX_MEMORY_USAGE = 100_000_000; // 100MB

        // フィールド
        private readonly IOcrEngine _ocrEngine;
        private readonly LibreTranslateEngine _translationEngine;
        private readonly ILogger<OverlayForm> _logger;
        private bool _isRegionSelectMode = false;
        private Point? _selectionStart = null;
        private Panel _selectionBox = null;
        private readonly List<TranslationBox> _translationBoxes = new List<TranslationBox>();
        private readonly List<Form> _translationContainers = new List<Form>();
        private Panel _selectionOverlay = null;
        private long _totalMemoryUsage = 0;
        private readonly Timer _topMostTimer;
        private readonly ITranslationCache _translationCache;

        public OverlayForm(IOcrEngine ocrEngine)
        {
            InitializeComponent();
            _ocrEngine = ocrEngine ?? throw new ArgumentNullException(nameof(ocrEngine));

            _translationCache = new TranslationCache(new TranslationConfig());

            // ロガーの設定
            var loggerFactory = LoggerFactory.Create(builder =>
            {
                builder
                    .AddConsole()
                    .SetMinimumLevel(LogLevel.Debug);
            });
            _logger = loggerFactory.CreateLogger<OverlayForm>();

            // 翻訳エンジンの初期化
            var settings = new LibreTranslateEngine.Settings
            {
                BaseUrl = "http://localhost:5000",
                Timeout = 10000,
                MaxRetries = 3,
                RetryDelay = 1000
            };
            _translationEngine = new LibreTranslateEngine(settings, _translationCache);

            // 最前面維持用のタイマーを初期化
            _topMostTimer = new Timer
            {
                Interval = 500,
                Enabled = true
            };
            _topMostTimer.Tick += (s, e) => EnsureTopMostWithoutFocus();

            InitializeOverlayWindow();
            CreateSelectionOverlay();
        }

        private void EnsureTopMostWithoutFocus()
        {
            try
            {
                // 現在のフォアグラウンドウィンドウを保存
                var currentForeground = GetForegroundWindow();

                // オーバーレイを最前面に
                SetWindowPos(
                    this.Handle,
                    HWND_TOPMOST,
                    0, 0, 0, 0,
                    SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE
                );

                // 翻訳ボックスも最前面に
                foreach (var container in _translationContainers)
                {
                    if (container.Visible)
                    {
                        SetWindowPos(
                            container.Handle,
                            HWND_TOPMOST,
                            0, 0, 0, 0,
                            SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE
                        );
                    }
                }

                // フォアグラウンドウィンドウを元に戻す
                if (currentForeground != IntPtr.Zero)
                {
                    SetForegroundWindow(currentForeground);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in EnsureTopMostWithoutFocus: {ex.Message}");
            }
        }

        private void CreateSelectionOverlay()
        {
            _selectionOverlay = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(1, 255, 255, 255),
                Visible = false
            };

            _selectionOverlay.MouseDown += OverlayForm_MouseDown;
            _selectionOverlay.MouseMove += OverlayForm_MouseMove;
            _selectionOverlay.MouseUp += OverlayForm_MouseUp;

            this.Controls.Add(_selectionOverlay);
            _selectionOverlay.SendToBack();
        }

        private void InitializeOverlayWindow()
        {
            this.FormBorderStyle = FormBorderStyle.None;
            this.ShowInTaskbar = false;
            this.TopMost = true;
            this.BackColor = Color.Black;
            this.TransparencyKey = Color.Black;
            this.Opacity = 0.01;
            this.Bounds = Screen.PrimaryScreen.Bounds;

            // 常に最前面に表示
            SetWindowPos(this.Handle, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);

            // ホットキーの登録
            RegisterHotKey(this.Handle, HOTKEY_ID_OVERLAY, MOD_CONTROL | MOD_SHIFT, (int)Keys.O);
            RegisterHotKey(this.Handle, HOTKEY_ID_REGION_SELECT, MOD_CONTROL | MOD_SHIFT, (int)Keys.R);
            RegisterHotKey(this.Handle, HOTKEY_ID_CLEAR, MOD_CONTROL | MOD_SHIFT, (int)Keys.C);

            SetClickThrough(true);
        }

        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);

            if (m.Msg == WM_HOTKEY)
            {
                switch (m.WParam.ToInt32())
                {
                    case HOTKEY_ID_OVERLAY:
                        this.Visible = !this.Visible;
                        if (this.Visible)
                            EnsureTopMostWithoutFocus();
                        Debug.WriteLine($"オーバーレイ表示切り替え: {this.Visible}");
                        break;

                    case HOTKEY_ID_REGION_SELECT:
                        if (CanAddNewArea())
                            ToggleRegionSelectMode();
                        else
                            MessageBox.Show("これ以上エリアを追加できません。", "制限に達しました", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        break;

                    case HOTKEY_ID_CLEAR:
                        ClearAllAreas();
                        break;
                }
            }
        }

        private bool CanAddNewArea()
        {
            if (_translationBoxes.Count >= MAX_AREAS) return false;
            if (_totalMemoryUsage > MAX_MEMORY_USAGE) return false;
            return true;
        }

        private void ClearAllAreas()
        {
            foreach (var box in _translationBoxes.ToList())
            {
                box.RemoveHighlight();
                box.Dispose();
            }
            _translationBoxes.Clear();

            foreach (var container in _translationContainers.ToList())
            {
                container.Dispose();
            }
            _translationContainers.Clear();

            _totalMemoryUsage = 0;
            Debug.WriteLine("All areas cleared");
        }

        private void ToggleRegionSelectMode()
        {
            if (!this.Visible) return;

            _isRegionSelectMode = !_isRegionSelectMode;
            if (_isRegionSelectMode)
            {
                Debug.WriteLine("領域選択モード開始");
                SetClickThrough(false);
                this.Opacity = 0.3;
                _selectionOverlay.Visible = true;
                _selectionOverlay.Cursor = Cursors.Cross;
                _selectionOverlay.BringToFront();
                EnsureTopMostWithoutFocus();
            }
            else
            {
                Debug.WriteLine("領域選択モード終了");
                ExitRegionSelectMode();
            }
        }

        private void ExitRegionSelectMode()
        {
            _isRegionSelectMode = false;
            this.Opacity = 0.01;
            _selectionOverlay.Visible = false;
            _selectionOverlay.Cursor = Cursors.Default;
            SetClickThrough(true);
            ClearSelectionBox();
        }

        private void OverlayForm_MouseDown(object sender, MouseEventArgs e)
        {
            if (!_isRegionSelectMode) return;

            _selectionStart = e.Location;
            _selectionBox = new Panel
            {
                BackColor = Color.FromArgb(50, 0, 120, 215),
                BorderStyle = BorderStyle.FixedSingle,
                Location = e.Location,
                Size = new Size(0, 0)
            };

            _selectionOverlay.Controls.Add(_selectionBox);
            _selectionBox.BringToFront();
        }

        private void OverlayForm_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_isRegionSelectMode || _selectionStart == null || _selectionBox == null) return;

            int x = Math.Min(e.X, _selectionStart.Value.X);
            int y = Math.Min(e.Y, _selectionStart.Value.Y);
            int width = Math.Abs(e.X - _selectionStart.Value.X);
            int height = Math.Abs(e.Y - _selectionStart.Value.Y);

            _selectionBox.Location = new Point(x, y);
            _selectionBox.Size = new Size(width, height);
        }

        private void OverlayForm_MouseUp(object sender, MouseEventArgs e)
        {
            if (!_isRegionSelectMode || _selectionStart == null) return;

            var screenPoint = _selectionOverlay.PointToScreen(new Point(
                Math.Min(e.X, _selectionStart.Value.X),
                Math.Min(e.Y, _selectionStart.Value.Y)
            ));

            var region = new Rectangle(
                screenPoint.X,
                screenPoint.Y,
                Math.Abs(e.X - _selectionStart.Value.X),
                Math.Abs(e.Y - _selectionStart.Value.Y)
            );

            if (region.Width > 10 && region.Height > 10)
            {
                _ = ProcessSelectedRegionAsync(region);
            }

            ExitRegionSelectMode();
        }

        private async Task ProcessSelectedRegionAsync(Rectangle region)
        {
            Debug.WriteLine($"Processing region: {region}");
            try
            {
                // OCR実行
                var results = await OcrTest.RunTests(region);
                if (!results.Any()) return;

                var detailedResult = results.FirstOrDefault(r => r.Configuration.Contains("Detailed"));
                var bestResult = detailedResult ?? results.OrderByDescending(r => r.Accuracy).First();
                var recognizedText = bestResult.RecognizedText.Trim();

                // 翻訳実行
                await _translationEngine.InitializeAsync();
                string translatedText = await _translationEngine.TranslateAsync(recognizedText, "en", "ja");

                // 表示テキストの作成
                var displayText = $"Original Text:\n{recognizedText}\n\nTranslated Text:\n{translatedText}";

                var translationBox = new TranslationBox(region);
                translationBox.UpdateText(displayText);
                translationBox.TextChangeDetected += async (s, e) => await HandleTextChanged(e.Region);
                _translationBoxes.Add(translationBox);

                var container = new Form
                {
                    FormBorderStyle = FormBorderStyle.None,
                    ShowInTaskbar = false,
                    TopMost = true,
                    StartPosition = FormStartPosition.Manual,
                    Location = translationBox.Location,
                    Size = translationBox.Size,
                    Opacity = 0.8,
                    BackColor = Color.Black
                };

                container.Controls.Add(translationBox);
                translationBox.Location = Point.Empty;
                translationBox.Dock = DockStyle.Fill;

                container.Show();
                _translationContainers.Add(container);

                // 最前面に表示
                SetWindowPos(
                    container.Handle,
                    HWND_TOPMOST,
                    0, 0, 0, 0,
                    SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE
                );

                _totalMemoryUsage += region.Width * region.Height * 4;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error processing region: {ex.Message}");
                Debug.WriteLine(ex.StackTrace);
                MessageBox.Show($"エラーが発生しました: {ex.Message}", "エラー", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async Task HandleTextChanged(Rectangle region)
        {
            try
            {
                var results = await OcrTest.RunTests(region);
                if (!results.Any()) return;

                var detailedResult = results.FirstOrDefault(r => r.Configuration.Contains("Detailed"));
                var bestResult = detailedResult ?? results.OrderByDescending(r => r.Accuracy).First();
                var recognizedText = bestResult.RecognizedText.Trim();

                // 翻訳の実行
                string translatedText = await _translationEngine.TranslateAsync(recognizedText, "en", "ja");

                var box = _translationBoxes.FirstOrDefault(b => b.TargetRegion == region);
                if (box != null)
                {
                    var displayText = $"Original Text:\n{recognizedText}\n\nTranslated Text:\n{translatedText}";
                    box.UpdateText(displayText);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error handling text change: {ex.Message}");
                _logger.LogError($"Text change handling error: {ex.Message}", ex);
            }
        }

        private void SetClickThrough(bool enable)
        {
            uint exStyle = GetWindowLong(this.Handle, GWL_EXSTYLE);
            if (enable)
                exStyle |= WS_EX_TRANSPARENT;
            else
                exStyle &= ~WS_EX_TRANSPARENT;
            SetWindowLong(this.Handle, GWL_EXSTYLE, exStyle);
        }

        private void ClearSelectionBox()
        {
            if (_selectionBox != null)
            {
                _selectionOverlay.Controls.Remove(_selectionBox);
                _selectionBox.Dispose();
                _selectionBox = null;
            }
        }

        protected override void OnVisibleChanged(EventArgs e)
        {
            base.OnVisibleChanged(e);
            if (this.Visible)
            {
                EnsureTopMostWithoutFocus();
                _topMostTimer.Start();
            }
            else
            {
                _topMostTimer.Stop();
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            _topMostTimer.Stop();
            _topMostTimer.Dispose();
            UnregisterHotKey(this.Handle, HOTKEY_ID_OVERLAY);
            UnregisterHotKey(this.Handle, HOTKEY_ID_REGION_SELECT);
            UnregisterHotKey(this.Handle, HOTKEY_ID_CLEAR);

            ClearAllAreas();

            base.OnFormClosing(e);
        }
    }
}