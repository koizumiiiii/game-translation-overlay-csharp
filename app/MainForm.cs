using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using GameTranslationOverlay.Core.OCR;
using GameTranslationOverlay.Core.WindowManagement;
using GameTranslationOverlay.Forms;
using System.Threading.Tasks;
using System.Linq;
using System.IO;

namespace GameTranslationOverlay
{
    public partial class MainForm : Form
    {
        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        private Button _benchmarkButton;
        private Button _selectWindowButton;
        private Button _startTranslationButton;
        private Button _toggleTextDetectionButton;
        private MenuStrip _menuStrip;
        private Label _hotkeyInfoLabel;
        private Label _statusLabel;
        private OverlayForm _overlayForm;
        private TesseractOcrEngine _ocrEngine;
        private WindowSelector.WindowInfo _selectedWindow;
        private Timer _checkWindowTimer;

        private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOSIZE = 0x0001;

        public MainForm()
        {
            Debug.WriteLine("MainForm: コンストラクタ開始");
            InitializeComponent();

            // ホットキー情報ラベル
            _hotkeyInfoLabel = new Label
            {
                AutoSize = true,
                ForeColor = Color.Black,
                Font = new Font("Yu Gothic UI", 9),
                Text = "ホットキー一覧:\n" +
                       "Ctrl+Shift+O : オーバーレイ表示切替\n" +
                       "Ctrl+Shift+R : エリア選択・翻訳開始\n" +
                       "Ctrl+Shift+C : すべてのエリアを削除",
                Location = new Point(12, 12)
            };
            this.Controls.Add(_hotkeyInfoLabel);

            // 状態表示ラベル
            _statusLabel = new Label
            {
                AutoSize = true,
                ForeColor = Color.DarkBlue,
                Font = new Font("Yu Gothic UI", 9),
                Text = "状態: 準備完了",
                Location = new Point(12, _hotkeyInfoLabel.Bottom + 12)
            };
            this.Controls.Add(_statusLabel);

            // ウィンドウ選択ボタン
            _selectWindowButton = new Button
            {
                Text = "翻訳対象ウィンドウを選択",
                Location = new Point(12, _statusLabel.Bottom + 12),
                Size = new Size(180, 30)
            };
            _selectWindowButton.Click += SelectWindowButton_Click;
            this.Controls.Add(_selectWindowButton);

            // 翻訳開始ボタン
            _startTranslationButton = new Button
            {
                Text = "翻訳開始",
                Location = new Point(12, _selectWindowButton.Bottom + 12),
                Size = new Size(120, 30),
                Enabled = false
            };
            _startTranslationButton.Click += StartTranslationButton_Click;
            this.Controls.Add(_startTranslationButton);

            // テキスト検出切替ボタン
            _toggleTextDetectionButton = new Button
            {
                Text = "テキスト検出切替",
                Location = new Point(12, _startTranslationButton.Bottom + 12),
                Size = new Size(120, 30),
                Enabled = false
            };
            _toggleTextDetectionButton.Click += ToggleTextDetectionButton_Click;
            this.Controls.Add(_toggleTextDetectionButton);

            // ベンチマークボタン
            _benchmarkButton = new Button
            {
                Text = "Run OCR Benchmark",
                Location = new Point(12, _toggleTextDetectionButton.Bottom + 12),
                Size = new Size(120, 30)
            };
            _benchmarkButton.Click += async (sender, e) => await RunBenchmark();
            this.Controls.Add(_benchmarkButton);

            // メニューストリップの初期化
            InitializeMenu();

            // ウィンドウ監視タイマー
            _checkWindowTimer = new Timer
            {
                Interval = 1000, // 1秒ごとにチェック
                Enabled = false
            };
            _checkWindowTimer.Tick += CheckWindowTimer_Tick;

            // サービスの初期化
            InitializeServices();

            // フォームのサイズを調整
            this.ClientSize = new Size(
                Math.Max(_benchmarkButton.Right + 12, _hotkeyInfoLabel.Right + 12),
                _benchmarkButton.Bottom + 12
            );

            // 常に最前面に表示
            SetWindowPos(this.Handle, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE);

            Debug.WriteLine("MainForm: コンストラクタ完了");
        }

        private void InitializeMenu()
        {
            _menuStrip = new MenuStrip();
            var toolsMenu = new ToolStripMenuItem("Tools");
            var ocrTestMenuItem = new ToolStripMenuItem("OCR Test");
            var settingsMenuItem = new ToolStripMenuItem("設定");

            ocrTestMenuItem.Click += (s, e) =>
            {
                using (var testForm = new OcrTestForm())
                {
                    testForm.ShowDialog(this);
                }
            };

            settingsMenuItem.Click += (s, e) =>
            {
                // TODO: 設定画面の実装
                MessageBox.Show("設定機能は近日実装予定です。", "Information", MessageBoxButtons.OK, MessageBoxIcon.Information);
            };

            toolsMenu.DropDownItems.Add(ocrTestMenuItem);
            toolsMenu.DropDownItems.Add(settingsMenuItem);
            _menuStrip.Items.Add(toolsMenu);
            MainMenuStrip = _menuStrip;
            Controls.Add(_menuStrip);
        }

        private async void InitializeServices()
        {
            Debug.WriteLine("InitializeServices: 開始");
            try
            {
                UpdateStatus("初期化中...");

                _ocrEngine = new TesseractOcrEngine();
                Debug.WriteLine("InitializeServices: OCRエンジン作成");

                await _ocrEngine.InitializeAsync();
                Debug.WriteLine("InitializeServices: OCRエンジン初期化完了");

                _overlayForm = new OverlayForm(_ocrEngine);
                _overlayForm.Show();
                Debug.WriteLine("InitializeServices: オーバーレイフォーム作成完了");

                UpdateStatus("準備完了");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"InitializeServices エラー: {ex.Message}");
                UpdateStatus($"初期化エラー: {ex.Message}", true);
                MessageBox.Show(
                    $"初期化エラー: {ex.Message}\nアプリケーションを終了します。",
                    "エラー",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
                Application.Exit();
            }
        }

        private void UpdateStatus(string message, bool isError = false)
        {
            if (_statusLabel.InvokeRequired)
            {
                _statusLabel.BeginInvoke(new Action(() => UpdateStatus(message, isError)));
                return;
            }

            _statusLabel.Text = $"状態: {message}";
            _statusLabel.ForeColor = isError ? Color.Red : Color.DarkBlue;
        }

        private void SelectWindowButton_Click(object sender, EventArgs e)
        {
            using (var selectorForm = new WindowSelectorForm())
            {
                if (selectorForm.ShowDialog(this) == DialogResult.OK)
                {
                    _selectedWindow = selectorForm.SelectedWindow;
                    if (_selectedWindow != null)
                    {
                        _startTranslationButton.Enabled = true;
                        UpdateStatus($"選択: {_selectedWindow.Title}");
                    }
                }
            }
        }

        private void StartTranslationButton_Click(object sender, EventArgs e)
        {
            if (_selectedWindow != null && _overlayForm != null)
            {
                _overlayForm.SetTargetWindow(_selectedWindow.Handle);
                _checkWindowTimer.Start();
                _toggleTextDetectionButton.Enabled = true;
                UpdateStatus($"翻訳実行中: {_selectedWindow.Title}");
            }
        }

        private void ToggleTextDetectionButton_Click(object sender, EventArgs e)
        {
            if (_overlayForm != null)
            {
                bool newState = _overlayForm.ToggleTextDetection();
                _toggleTextDetectionButton.Text = newState ? "テキスト検出 OFF" : "テキスト検出 ON";
                UpdateStatus($"テキスト検出: {(newState ? "有効" : "無効")}");
            }
        }

        private void CheckWindowTimer_Tick(object sender, EventArgs e)
        {
            // 選択したウィンドウが有効かどうかチェック
            if (_selectedWindow != null)
            {
                try
                {
                    WindowSelector.RECT rect;
                    if (!WindowSelector.GetWindowRect(_selectedWindow.Handle, out rect))
                    {
                        // ウィンドウが見つからない場合
                        _checkWindowTimer.Stop();
                        UpdateStatus("選択したウィンドウが見つかりません", true);
                        _startTranslationButton.Enabled = false;
                        _selectedWindow = null;
                    }
                    else
                    {
                        // ウィンドウの位置やサイズが変わっていたら調整
                        Rectangle newBounds = new Rectangle(
                            rect.Left, rect.Top,
                            rect.Right - rect.Left, rect.Bottom - rect.Top);

                        if (newBounds != _selectedWindow.Bounds)
                        {
                            _selectedWindow.Bounds = newBounds;
                            _overlayForm.UpdateOverlayPosition(newBounds);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Window check error: {ex.Message}");
                    _checkWindowTimer.Stop();
                    UpdateStatus("ウィンドウ監視エラー", true);
                }
            }
        }

        private async Task RunBenchmark()
        {
            try
            {
                _benchmarkButton.Enabled = false;
                _benchmarkButton.Text = "Running...";

                var testScenarios = new[]
                {
                    "dialog_text",
                    "menu_text",
                    "system_message",
                    "battle_text",
                    "item_description"
                };

                foreach (var scenario in testScenarios)
                {
                    Debug.WriteLine($"\nTesting scenario: {scenario}");
                    var imagePath = Path.Combine("Core", "OCR", "Resources", "TestImages", $"{scenario}.png");

                    try
                    {
                        using (var image = new Bitmap(imagePath))
                        {
                            var region = new Rectangle(0, 0, image.Width, image.Height);
                            var results = await OcrTest.RunTests(region);

                            var paddleResults = results.Where(r => r.EngineName == "PaddleOCR");
                            foreach (var result in paddleResults)
                            {
                                Debug.WriteLine($"PaddleOCR - Time: {result.ProcessingTime}ms, Accuracy: {result.Accuracy:P2}");
                                Debug.WriteLine($"Recognized Text:\n{result.RecognizedText}\n");

                                if (result.AdditionalInfo?.Any() == true)
                                {
                                    foreach (var info in result.AdditionalInfo)
                                    {
                                        Debug.WriteLine($"{info.Key}: {info.Value}");
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error testing scenario {scenario}: {ex.Message}");
                        continue;
                    }
                }

                MessageBox.Show(
                    "ベンチマーク完了。詳細はログを確認してください。",
                    "Benchmark Complete",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                );
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"ベンチマークエラー: {ex.Message}",
                    "Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
            }
            finally
            {
                _benchmarkButton.Enabled = true;
                _benchmarkButton.Text = "Run OCR Benchmark";
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            Debug.WriteLine("OnFormClosing: 開始");
            try
            {
                if (_checkWindowTimer != null)
                {
                    _checkWindowTimer.Stop();
                    _checkWindowTimer.Dispose();
                }

                if (_ocrEngine != null)
                {
                    _ocrEngine.Dispose();
                    Debug.WriteLine("OnFormClosing: OCRエンジンを破棄");
                }

                if (_overlayForm != null)
                {
                    _overlayForm.Dispose();
                    Debug.WriteLine("OnFormClosing: オーバーレイフォームを破棄");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"OnFormClosing エラー: {ex.Message}");
            }

            Debug.WriteLine("OnFormClosing: 終了");
            base.OnFormClosing(e);
        }
    }
}