using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Linq;
using System.IO;
using GameTranslationOverlay.Core.OCR;
using GameTranslationOverlay.Core.UI;
using GameTranslationOverlay.Forms;
using GameTranslationOverlay.Utils;
using GameTranslationOverlay.Core.Models;
using GameTranslationOverlay.Core.Translation.Services;
using GameTranslationOverlay.Core.Translation.Interfaces;
using GameTranslationOverlay.Core.Utils;

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
        private ComboBox _targetLanguageComboBox;
        private CheckBox _useAITranslationCheckBox;
        private CheckBox _useAutoDetectCheckBox;
        private Label _tokenCountLabel;
        private MenuStrip _menuStrip;
        private Label _statusLabel;
        private GroupBox _translationSettingsGroup;
        private OverlayForm _overlayForm;

        // OCR関連
        private OcrManager _ocrManager;

        // OCR設定UI要素
        private GroupBox _ocrSettingsGroup;
        private ComboBox _ocrEngineComboBox;
        private CheckBox _useFallbackCheckBox;
        private CheckBox _usePreprocessingCheckBox;
        private TrackBar _confidenceTrackBar;
        private Label _confidenceLabel;

        private GameTranslationOverlay.Core.Models.WindowInfo _selectedWindow;
        private Timer _checkWindowTimer;

        // 翻訳関連
        private TranslationManager _translationManager;
        private LibreTranslateEngine _libreTranslateEngine;
        private AITranslationEngine _aiTranslationEngine;
        private string _openAiApiKey = ""; // 実際のAPIキーは環境変数や設定ファイルから読み込むべき

        private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOSIZE = 0x0001;

        public MainForm()
        {
            InitializeComponent();
            Debug.WriteLine("MainForm: コンストラクタ開始");

            // 状態表示ラベル
            _statusLabel = new Label
            {
                AutoSize = true,
                ForeColor = Color.DarkBlue,
                Font = new Font("Yu Gothic UI", 9),
                Text = "状態: 準備完了",
                Location = new Point(12, 12)
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

            // OCR設定グループを初期化
            InitializeOcrSettings();

            // 翻訳設定グループを初期化
            InitializeTranslationSettings();

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
                Math.Max(_ocrSettingsGroup.Right + 12, _translationSettingsGroup.Right + 12),
                Math.Max(_benchmarkButton.Bottom + 12, _translationSettingsGroup.Bottom + 12)
            );

            // 常に最前面に表示
            SetWindowPos(this.Handle, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE);

            Debug.WriteLine("MainForm: コンストラクタ完了");
        }

        /// <summary>
        /// OCR設定UIの初期化
        /// </summary>
        private void InitializeOcrSettings()
        {
            // OCR設定グループ
            _ocrSettingsGroup = new GroupBox
            {
                Text = "OCR設定",
                Location = new Point(200, _benchmarkButton.Bottom + 20),
                Size = new Size(200, 160)
            };

            // OCRエンジン選択
            Label engineLabel = new Label
            {
                Text = "OCRエンジン:",
                Location = new Point(10, 25),
                AutoSize = true
            };

            _ocrEngineComboBox = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Location = new Point(120, 22),
                Size = new Size(70, 21)
            };
            _ocrEngineComboBox.Items.AddRange(new object[] { "Auto", "PaddleOCR", "Tesseract" });
            _ocrEngineComboBox.SelectedIndex = 0;
            _ocrEngineComboBox.SelectedIndexChanged += (s, e) =>
            {
                if (_ocrManager != null)
                {
                    _ocrManager.SetOcrEngine(_ocrEngineComboBox.SelectedItem.ToString());
                }
            };

            // フォールバックチェックボックス
            _useFallbackCheckBox = new CheckBox
            {
                Text = "フォールバック使用",
                Location = new Point(10, 50),
                AutoSize = true,
                Checked = true
            };
            _useFallbackCheckBox.CheckedChanged += (s, e) =>
            {
                if (_ocrManager != null)
                {
                    _ocrManager.SetUseFallback(_useFallbackCheckBox.Checked);
                }
            };

            // 前処理チェックボックス
            _usePreprocessingCheckBox = new CheckBox
            {
                Text = "画像前処理を適用",
                Location = new Point(10, 75),
                AutoSize = true,
                Checked = true
            };
            _usePreprocessingCheckBox.CheckedChanged += (s, e) =>
            {
                if (_ocrManager != null)
                {
                    _ocrManager.EnablePreprocessing(_usePreprocessingCheckBox.Checked);
                }
            };

            // 信頼度スライダー
            Label thresholdLabel = new Label
            {
                Text = "信頼度閾値:",
                Location = new Point(10, 100),
                AutoSize = true
            };

            _confidenceTrackBar = new TrackBar
            {
                Location = new Point(10, 120),
                Size = new Size(140, 45),
                Minimum = 0,
                Maximum = 100,
                Value = 60, // 0.6の信頼度
                TickFrequency = 10
            };

            _confidenceLabel = new Label
            {
                Text = "0.60",
                Location = new Point(160, 120),
                AutoSize = true
            };

            _confidenceTrackBar.ValueChanged += (s, e) =>
            {
                float value = _confidenceTrackBar.Value / 100.0f;
                _confidenceLabel.Text = value.ToString("F2");

                if (_ocrManager != null)
                {
                    _ocrManager.SetConfidenceThreshold(value);
                }
            };

            // コントロールをグループに追加
            _ocrSettingsGroup.Controls.Add(engineLabel);
            _ocrSettingsGroup.Controls.Add(_ocrEngineComboBox);
            _ocrSettingsGroup.Controls.Add(_useFallbackCheckBox);
            _ocrSettingsGroup.Controls.Add(_usePreprocessingCheckBox);
            _ocrSettingsGroup.Controls.Add(thresholdLabel);
            _ocrSettingsGroup.Controls.Add(_confidenceTrackBar);
            _ocrSettingsGroup.Controls.Add(_confidenceLabel);

            this.Controls.Add(_ocrSettingsGroup);
        }

        /// <summary>
        /// 翻訳設定グループの初期化
        /// </summary>
        private void InitializeTranslationSettings()
        {
            // 翻訳設定グループ
            _translationSettingsGroup = new GroupBox
            {
                Text = "翻訳設定",
                Location = new Point(450, _benchmarkButton.Bottom + 20),
                Size = new Size(220, 160)
            };

            // 言語選択ラベル
            Label targetLanguageLabel = new Label
            {
                Text = "翻訳先言語:",
                Location = new Point(10, 25),
                AutoSize = true
            };

            // 言語選択コンボボックス
            _targetLanguageComboBox = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Location = new Point(100, 22),
                Size = new Size(100, 21)
            };

            foreach (string langCode in LanguageManager.SupportedLanguages)
            {
                _targetLanguageComboBox.Items.Add(LanguageManager.LanguageNames[langCode]);
            }

            // 初期値を日本語に設定
            _targetLanguageComboBox.SelectedIndex = Array.IndexOf(LanguageManager.SupportedLanguages, "ja");
            _targetLanguageComboBox.SelectedIndexChanged += TargetLanguageComboBox_SelectedIndexChanged;

            // 自動検出チェックボックス
            _useAutoDetectCheckBox = new CheckBox
            {
                Text = "言語自動検出",
                Location = new Point(10, 55),
                AutoSize = true,
                Checked = true
            };
            _useAutoDetectCheckBox.CheckedChanged += UseAutoDetectCheckBox_CheckedChanged;

            // AI翻訳切り替えチェックボックス
            _useAITranslationCheckBox = new CheckBox
            {
                Text = "AI翻訳を使用",
                Location = new Point(10, 85),
                AutoSize = true
            };
            _useAITranslationCheckBox.CheckedChanged += UseAITranslationCheckBox_CheckedChanged;

            // トークン残量表示ラベル
            _tokenCountLabel = new Label
            {
                AutoSize = true,
                ForeColor = Color.DarkGreen,
                Text = "残りトークン: 5000",
                Location = new Point(30, 110),
                Visible = false
            };

            // コントロールをグループに追加
            _translationSettingsGroup.Controls.Add(targetLanguageLabel);
            _translationSettingsGroup.Controls.Add(_targetLanguageComboBox);
            _translationSettingsGroup.Controls.Add(_useAutoDetectCheckBox);
            _translationSettingsGroup.Controls.Add(_useAITranslationCheckBox);
            _translationSettingsGroup.Controls.Add(_tokenCountLabel);

            this.Controls.Add(_translationSettingsGroup);
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();
            // 
            // MainForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(800, 450);
            this.Name = "MainForm";
            this.Text = "Game Translation Overlay";
            this.ResumeLayout(false);
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

        private void UseAutoDetectCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            // 言語自動検出の設定を更新
            bool useAutoDetect = _useAutoDetectCheckBox.Checked;

            // TargetLanguageComboBoxの有効/無効を切り替え
            _targetLanguageComboBox.Enabled = !useAutoDetect;

            // 翻訳ボックスにも設定を反映（もし表示されていれば）
            if (_overlayForm != null && _overlayForm.TranslationBox != null && !_overlayForm.TranslationBox.IsDisposed)
            {
                try
                {
                    _overlayForm.TranslationBox.SetAutoDetect(useAutoDetect);
                    Debug.WriteLine($"言語自動検出を{(useAutoDetect ? "有効" : "無効")}に設定しました");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error setting auto detect: {ex.Message}");
                }
            }
        }

        private void TargetLanguageComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            try
            {
                int selectedIndex = _targetLanguageComboBox.SelectedIndex;
                if (selectedIndex >= 0 && selectedIndex < LanguageManager.SupportedLanguages.Length)
                {
                    string selectedLangCode = LanguageManager.SupportedLanguages[selectedIndex];
                    _translationManager?.SetPreferredTargetLanguage(selectedLangCode);
                    Debug.WriteLine($"Target language changed to: {selectedLangCode}");

                    // 翻訳ボックスにも設定を反映（もし表示されていれば）
                    if (_overlayForm != null && _overlayForm.TranslationBox != null && !_overlayForm.TranslationBox.IsDisposed)
                    {
                        _overlayForm.TranslationBox.SetTargetLanguage(selectedLangCode);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error setting target language: {ex.Message}");
                MessageBox.Show(
                    $"言語設定エラー: {ex.Message}",
                    "エラー",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
            }
        }

        private void UseAITranslationCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            try
            {
                bool useAI = _useAITranslationCheckBox.Checked;

                if (useAI)
                {
                    if (_aiTranslationEngine == null)
                    {
                        // APIキーが設定されていない場合
                        if (string.IsNullOrEmpty(_openAiApiKey))
                        {
                            MessageBox.Show(
                                "AI翻訳を使用するにはAPIキーを設定する必要があります。",
                                "設定エラー",
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Warning
                            );
                            _useAITranslationCheckBox.Checked = false;
                            return;
                        }

                        // AIエンジンの初期化
                        _aiTranslationEngine = new AITranslationEngine(_openAiApiKey);
                    }

                    // AI翻訳エンジンに切り替え
                    _translationManager.SetTranslationEngine(_aiTranslationEngine);
                    _tokenCountLabel.Visible = true;
                    _tokenCountLabel.Text = $"残りトークン: {_aiTranslationEngine.GetRemainingTokens()}";

                    // 初期化
                    Task.Run(async () =>
                    {
                        try
                        {
                            await _translationManager.InitializeAsync();
                            this.BeginInvoke(new Action(() =>
                            {
                                UpdateStatus("AI翻訳エンジン初期化完了");
                            }));
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"AI Translation initialization error: {ex.Message}");
                            this.BeginInvoke(new Action(() =>
                            {
                                UpdateStatus($"AI翻訳エンジン初期化エラー: {ex.Message}", true);
                                _useAITranslationCheckBox.Checked = false;
                                MessageBox.Show(
                                    $"AI翻訳エンジン初期化エラー: {ex.Message}",
                                    "エラー",
                                    MessageBoxButtons.OK,
                                    MessageBoxIcon.Error
                                );
                            }));
                        }
                    });
                }
                else
                {
                    // LibreTranslate エンジンに戻す
                    _translationManager.SetTranslationEngine(_libreTranslateEngine);
                    _tokenCountLabel.Visible = false;

                    // 初期化
                    Task.Run(async () =>
                    {
                        try
                        {
                            await _translationManager.InitializeAsync();
                            this.BeginInvoke(new Action(() =>
                            {
                                UpdateStatus("標準翻訳エンジン初期化完了");
                            }));
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"Libre Translation initialization error: {ex.Message}");
                            this.BeginInvoke(new Action(() =>
                            {
                                UpdateStatus($"標準翻訳エンジン初期化エラー: {ex.Message}", true);
                            }));
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error changing translation engine: {ex.Message}");
                MessageBox.Show(
                    $"翻訳エンジン切替エラー: {ex.Message}",
                    "エラー",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
            }
        }

        private void InitializeTranslationServices()
        {
            try
            {
                // Libre Translate エンジンの初期化
                _libreTranslateEngine = new LibreTranslateEngine("http://localhost:5000");

                // AI翻訳エンジンの初期化（APIキーが設定されている場合のみ）
                if (!string.IsNullOrEmpty(_openAiApiKey))
                {
                    _aiTranslationEngine = new AITranslationEngine(_openAiApiKey);
                }

                // 翻訳マネージャーの初期化
                _translationManager = new TranslationManager(_libreTranslateEngine);
                Task.Run(async () =>
                {
                    try
                    {
                        await _translationManager.InitializeAsync();
                        this.BeginInvoke(new Action(() =>
                        {
                            UpdateStatus("翻訳エンジン初期化完了");
                        }));
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Translation initialization error: {ex.Message}");
                        this.BeginInvoke(new Action(() =>
                        {
                            UpdateStatus($"翻訳エンジン初期化エラー: {ex.Message}", true);
                            MessageBox.Show(
                                $"翻訳エンジン初期化エラー: {ex.Message}\n\nLibreTranslate サーバーが起動しているか確認してください。",
                                "エラー",
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Error
                            );
                        }));
                    }
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Translation services initialization error: {ex.Message}");
                UpdateStatus($"翻訳サービス初期化エラー: {ex.Message}", true);
            }
        }

        private async void InitializeServices()
        {
            Debug.WriteLine("InitializeServices: 開始");
            try
            {
                UpdateStatus("初期化中...");

                // OCRマネージャーの初期化
                _ocrManager = new OcrManager();
                await _ocrManager.InitializeAsync();
                Debug.WriteLine("InitializeServices: OCRマネージャー初期化完了");

                // OCR設定UIの初期状態を設定
                UpdateOcrSettings();

                // 翻訳サービスの初期化
                InitializeTranslationServices();
                Debug.WriteLine("InitializeServices: 翻訳サービス初期化開始");

                // オーバーレイフォームの作成時にOCRマネージャーとTranslationManagerを渡す
                _overlayForm = new OverlayForm(_ocrManager, _translationManager);

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


        /// <summary>
        /// OCR設定UIを現在の状態に更新
        /// </summary>
        private void UpdateOcrSettings()
        {
            if (_ocrManager == null)
                return;

            // デフォルトでAutoを選択
            int index = 0;

            // GetPrimaryEngineNameメソッドは現在問題があるため使用しない
            _ocrEngineComboBox.SelectedIndex = index;
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

                        // オーバーレイフォームに対象ウィンドウを設定して自動検出を開始
                        if (_overlayForm != null)
                        {
                            _overlayForm.SetTargetWindow(_selectedWindow.Handle);
                            _checkWindowTimer.Start();
                            _toggleTextDetectionButton.Enabled = true;
                            _overlayForm.StartTextDetection();

                            UpdateStatus($"翻訳実行中: {_selectedWindow.Title}");
                        }
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
                    Rectangle rect = WindowUtils.GetWindowRect(_selectedWindow.Handle);
                    if (rect.IsEmpty)
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
                        if (rect != _selectedWindow.Bounds)
                        {
                            _selectedWindow.Bounds = rect;
                            _overlayForm.UpdateOverlayPosition(rect);
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

                // 各前処理オプションのテスト
                var preprocessingOptions = new PreprocessingOptions[]
                {
                    null, // デフォルト（前処理なし）
                    ImagePreprocessor.JapaneseTextPreset,
                    ImagePreprocessor.EnglishTextPreset,
                    ImagePreprocessor.GameTextLightPreset
                };

                string[] optionNames = { "Default", "Japanese Preset", "English Preset", "Light Preset" };

                // 各プリセットとシナリオの組み合わせをテスト
                for (int i = 0; i < preprocessingOptions.Length; i++)
                {
                    Debug.WriteLine($"\n--- Testing with {optionNames[i]} ---");

                    if (_ocrManager != null)
                    {
                        // 前処理設定を適用
                        _ocrManager.SetPreprocessingOptions(preprocessingOptions[i]);
                        _ocrManager.EnablePreprocessing(preprocessingOptions[i] != null);
                    }

                    foreach (var scenario in testScenarios)
                    {
                        Debug.WriteLine($"\nTesting scenario: {scenario}");
                        var imagePath = Path.Combine("Core", "OCR", "Resources", "TestImages", $"{scenario}.png");

                        try
                        {
                            using (var image = new Bitmap(imagePath))
                            {
                                // OCRマネージャーを使用してテキスト領域を検出
                                var regions = await _ocrManager.DetectTextRegionsAsync(image);

                                Debug.WriteLine($"Detected {regions.Count} text regions");
                                foreach (var region in regions)
                                {
                                    Debug.WriteLine($"Region: {region.Bounds}, Confidence: {region.Confidence:F2}");
                                    Debug.WriteLine($"Text: {region.Text}");
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"Error testing scenario {scenario}: {ex.Message}");
                            continue;
                        }
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

                if (_ocrManager != null)
                {
                    _ocrManager.Dispose();
                    Debug.WriteLine("OnFormClosing: OCRマネージャーを破棄");
                }

                if (_overlayForm != null)
                {
                    _overlayForm.Dispose();
                    Debug.WriteLine("OnFormClosing: オーバーレイフォームを破棄");
                }

                // 翻訳エンジンのクリーンアップ（IDisposableが実装されていない場合にはこの行を削除）
                _aiTranslationEngine = null;
                _libreTranslateEngine = null;
                _translationManager = null;
                Debug.WriteLine("OnFormClosing: 翻訳関連リソースをクリーンアップ");
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