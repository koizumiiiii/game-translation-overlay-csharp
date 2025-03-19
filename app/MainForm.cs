using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Linq;
using System.IO;
// 名前空間の衝突を解決するためのエイリアス
using OCRNamespace = GameTranslationOverlay.Core.OCR;
using UtilsNamespace = GameTranslationOverlay.Core.Utils;
// PreprocessingOptionsの名前空間を正しく指定
using GameTranslationOverlay.Core.Utils;
// 他の名前空間は通常どおりインポート
using GameTranslationOverlay.Core.UI;
using GameTranslationOverlay.Forms;
using GameTranslationOverlay.Utils;
using GameTranslationOverlay.Core.Models;
using GameTranslationOverlay.Core.Translation.Services;
using GameTranslationOverlay.Core.Translation.Interfaces;
using GameTranslationOverlay.Core.Licensing;
using GameTranslationOverlay.Core.OCR.AI;
using GameTranslationOverlay.Core.Configuration;
using TranslationLanguageManager = GameTranslationOverlay.Core.Translation.Services.LanguageManager;
using UILanguageManager = GameTranslationOverlay.Core.UI.LanguageManager;
using System.Collections.Generic;

namespace GameTranslationOverlay
{
    public partial class MainForm : Form
    {
        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

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
        private OCRNamespace.OcrManager _ocrManager;

        private GameTranslationOverlay.Core.Models.WindowInfo _selectedWindow;
        private Timer _checkWindowTimer;

        // 翻訳関連
        private TranslationManager _translationManager;
        private LibreTranslateEngine _libreTranslateEngine;
        private AITranslationEngine _aiTranslationEngine;

        private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOSIZE = 0x0001;

        private Button _optimizeOcrButton;
        private OcrOptimizer _ocrOptimizer;
        private GameProfiles _gameProfiles;

        private Button _benchmarkButton; // 参照があるので宣言だけ残す

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

            // ベンチマークボタンは削除するが、変数だけ残しておく（参照があるため）
            _benchmarkButton = new Button(); // 表示しないダミーボタン

            // 最適化コントロールを初期化
            InitializeOptimizationControls();

            // OCR設定メソッドは呼び出さない
            // InitializeOcrSettings();

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

            // フォント管理の初期化と適用
            InitializeFontManagement();

            // フォームのサイズを調整
            this.ClientSize = new Size(
                Math.Max(
                    _translationSettingsGroup.Right + 12,
                    _optimizeOcrButton.Right + 12
                ),
                Math.Max(
                    _translationSettingsGroup.Bottom + 12,
                    _optimizeOcrButton.Bottom + 12
                )
            );

            // 常に最前面に表示
            SetWindowPos(this.Handle, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE);

            Debug.WriteLine("MainForm: コンストラクタ完了");
        }

        /// <summary>
        /// フォント管理の初期化と適用
        /// </summary>
        private void InitializeFontManagement()
        {
            try
            {
                // このフォームにデフォルトフォントを適用
                FontManager.Instance.ApplyFontToForm(this);

                // 特定のコントロールに適切なフォントを適用
                if (_statusLabel != null)
                {
                    FontManager.Instance.ApplyFont(_statusLabel, FontSize.Default);
                }

                // タイトルフォントをメニュー項目に適用
                if (_menuStrip != null)
                {
                    FontManager.Instance.ApplyFont(_menuStrip, FontSize.Title);
                }

                // グループボックスのタイトルフォント
                // _ocrSettingsGroupへの参照を削除

                if (_translationSettingsGroup != null)
                {
                    FontManager.Instance.ApplyFont(_translationSettingsGroup, FontSize.Title);
                }

                // フォント情報をログに出力
                Debug.WriteLine("フォント管理を初期化しました");
                Debug.WriteLine($"日本語フォント利用可能: {FontManager.Instance.IsJpFontAvailable}");
                Debug.WriteLine($"英語フォント利用可能: {FontManager.Instance.IsEnFontAvailable}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"フォント管理の初期化中にエラーが発生しました: {ex.Message}");
            }
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

            foreach (string langCode in UILanguageManager.SupportedLanguages)
            {
                _targetLanguageComboBox.Items.Add(UILanguageManager.LanguageNames[langCode]);
            }

            // 初期値を日本語に設定
            _targetLanguageComboBox.SelectedIndex = Array.IndexOf(UILanguageManager.SupportedLanguages, "ja");
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
            this.FormClosing += MainForm_FormClosing;
        }

        private void InitializeOptimizationControls()
        {
            // OCR最適化ボタン
            _optimizeOcrButton = new Button
            {
                Text = "現在のゲームのOCR設定を最適化",
                Location = new Point(12, _toggleTextDetectionButton.Bottom + 12),
                Size = new Size(180, 30)
            };
            _optimizeOcrButton.Click += OptimizeOcrButton_Click;
            this.Controls.Add(_optimizeOcrButton);

            // ゲームプロファイル管理の初期化
            _gameProfiles = new GameProfiles();
        }

        private void InitializeOcrSettings()
        {
            // ダミー実装（空）
        }

        private async void OptimizeOcrButton_Click(object sender, EventArgs e)
        {
            string gameTitle = _selectedWindow?.Title;

            if (string.IsNullOrEmpty(gameTitle))
            {
                MessageBox.Show("まず翻訳対象のウィンドウを選択してください。");
                return;
            }

            // API使用制限のチェック
            if (!ApiUsageManager.Instance.CanCallApi(gameTitle))
            {
                int remainingCalls = ApiUsageManager.Instance.GetRemainingCalls(gameTitle);
                MessageBox.Show(
                    $"このゲームのAPI呼び出し制限に達しました。24時間後に再試行してください。\n\n" +
                    $"残りの呼び出し回数: {remainingCalls}",
                    "API制限",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning
                );
                return;
            }

            // テキスト表示のガイダンス
            DialogResult result = MessageBox.Show(
                "OCR設定の最適化を行います。\n\n" +
                "会話シーン、メニュー画面、説明画面など、\n" +
                "テキストが多く表示されている画面で実行してください。\n\n" +
                "準備ができたら「OK」を押してください。",
                "OCR最適化ガイド",
                MessageBoxButtons.OKCancel,
                MessageBoxIcon.Information
            );

            if (result != DialogResult.OK)
                return;

            // 進行状態表示オーバーレイを作成
            using (var progressOverlay = new OptimizationProgressOverlay())
            {
                // オーバーレイの表示
                progressOverlay.Size = this.Size;
                progressOverlay.Location = new Point(0, 0);
                progressOverlay.Show(this);
                progressOverlay.UpdateStatus(OptimizationProgressOverlay.OptimizationStep.Initializing);

                try
                {
                    // 現在のゲーム画面をキャプチャ
                    progressOverlay.UpdateStatus(OptimizationProgressOverlay.OptimizationStep.CapturingScreen);
                    using (Bitmap screenshot = ScreenCapture.CaptureWindow(_selectedWindow.Handle))
                    {
                        // 初回実行時にOCROptimizerを初期化
                        if (_ocrOptimizer == null)
                        {
                            _ocrOptimizer = new OcrOptimizer(_ocrManager);
                        }

                        // テキスト分析
                        progressOverlay.UpdateStatus(OptimizationProgressOverlay.OptimizationStep.AnalyzingText);

                        // 最適化を実行
                        progressOverlay.UpdateStatus(OptimizationProgressOverlay.OptimizationStep.GeneratingSettings);
                        var optimalSettings = await _ocrOptimizer.OptimizeForGame(gameTitle, screenshot);

                        // 最適化設定を適用
                        progressOverlay.UpdateStatus(OptimizationProgressOverlay.OptimizationStep.ApplyingOptimization);

                        // ゲームプロファイルに保存
                        var profileSettings = new OcrOptimizer.OptimalSettings
                        {
                            ConfidenceThreshold = optimalSettings.ConfidenceThreshold,
                            PreprocessingOptions = optimalSettings.ToPreprocessingOptions(),
                            LastOptimized = DateTime.Now,
                            OptimizationAttempts = 1,
                            IsOptimized = true,
                            AiSuggestions = new Dictionary<string, object>() // 必要に応じて値を追加
                        };
                        _gameProfiles.SaveProfile(gameTitle, profileSettings);

                        // 成功を記録
                        ApiUsageManager.Instance.RecordApiCall(gameTitle, true);

                        progressOverlay.UpdateStatus(OptimizationProgressOverlay.OptimizationStep.Completed, 100);
                        await Task.Delay(1500); // 完了メッセージを表示する時間

                        UpdateStatus($"{gameTitle} のOCR設定を最適化しました");
                    }
                }
                catch (Exception ex)
                {
                    // 失敗を記録
                    ApiUsageManager.Instance.RecordApiCall(gameTitle, false);

                    progressOverlay.UpdateStatus(OptimizationProgressOverlay.OptimizationStep.Failed);
                    await Task.Delay(1500); // エラーメッセージを表示する時間

                    if (ex.Message.Contains("テキストが十分に表示されていません"))
                    {
                        MessageBox.Show(
                            "テキストが十分に表示されていない画面です。\n\n" +
                            "会話シーン、メニュー画面、説明画面など、\n" +
                            "より多くのテキストが表示されている画面で再試行してください。",
                            "テキスト不足",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Warning
                        );
                    }
                    else
                    {
                        MessageBox.Show($"最適化中にエラーが発生しました: {ex.Message}", "エラー", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }

                    UpdateStatus("OCR最適化エラー", true);
                }
            }
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
                using (var settingsForm = new Forms.Settings.SettingsForm())
                {
                    // 設定変更イベントを処理
                    settingsForm.SettingsChanged += (sender, args) =>
                    {
                        // 設定が変更された場合の処理
                        // 例: OCR設定を更新、翻訳設定を更新など
                        UpdateSettingsFromConfig();
                    };

                    settingsForm.ShowDialog(this);
                }
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
                if (selectedIndex >= 0 && selectedIndex < UILanguageManager.SupportedLanguages.Length)
                {
                    string selectedLangCode = UILanguageManager.SupportedLanguages[selectedIndex];
                    _translationManager?.SetPreferredTargetLanguage(selectedLangCode);
                    Debug.WriteLine($"Target language changed to: {selectedLangCode}");

                    // 翻訳ボックスにも設定を反映（もし表示されていれば）
                    if (_overlayForm != null && _overlayForm.TranslationBox != null && !_overlayForm.TranslationBox.IsDisposed)
                    {
                        _overlayForm.TranslationBox.SetTargetLanguage(selectedLangCode);

                        // 言語に応じたフォントも適用
                        UpdateTranslationBoxFont(selectedLangCode);
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
                    if (!LicenseManager.Instance.HasFeature(PremiumFeature.AiTranslation))
                    {
                        MessageBox.Show(
                            "AI翻訳機能はPro版ライセンスが必要です。",
                            "ライセンス制限",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Warning
                        );
                        _useAITranslationCheckBox.Checked = false;
                        return;
                    }

                    // AI翻訳エンジンがない場合は初期化
                    if (_aiTranslationEngine == null)
                    {
                        _aiTranslationEngine = new AITranslationEngine();
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
                                UpdateStatus("翻訳エンジン初期化完了");

                                // ライセンスに基づいてAI翻訳チェックボックスの状態を設定
                                UpdateAITranslationCheckboxState();

                                // OverlayFormのTranslationBoxに適切なフォントを設定（もし表示されていれば）
                                if (_overlayForm != null && _overlayForm.TranslationBox != null && !_overlayForm.TranslationBox.IsDisposed)
                                {
                                    string selectedLang = GetSelectedTargetLanguage();
                                    if (selectedLang == "ja")
                                    {
                                        FontManager.Instance.ApplyTranslationFont(_overlayForm.TranslationBox, TranslationLanguage.Japanese);
                                    }
                                    else
                                    {
                                        FontManager.Instance.ApplyTranslationFont(_overlayForm.TranslationBox, TranslationLanguage.English);
                                    }
                                }
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

                            // ライセンスに基づいてAI翻訳チェックボックスの状態を設定
                            UpdateAITranslationCheckboxState();
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

        private void UpdateAITranslationCheckboxState()
        {
            bool hasAiFeature = LicenseManager.Instance.HasFeature(PremiumFeature.AiTranslation);

            // ライセンスに基づいてチェックボックスの有効/無効を設定
            _useAITranslationCheckBox.Enabled = hasAiFeature;

            // ツールチップを設定
            string tooltipText = hasAiFeature ?
                "AI翻訳を使用します（Pro版のみ）" :
                "AI翻訳機能はPro版ライセンスが必要です";

            // ツールチップの設定
            var toolTip = new ToolTip();
            toolTip.SetToolTip(_useAITranslationCheckBox, tooltipText);

            // ライセンス情報をステータスに表示
            UpdateStatus($"ライセンス: {LicenseManager.Instance.CurrentLicenseType}");
        }

        private async void InitializeServices()
        {
            Debug.WriteLine("InitializeServices: 開始");
            try
            {
                UpdateStatus("初期化中...");

                // OCRマネージャーの初期化
                _ocrManager = new OCRNamespace.OcrManager();
                await _ocrManager.InitializeAsync();
                Debug.WriteLine("InitializeServices: OCRマネージャー初期化完了");

                // OCR設定UIの初期状態を設定
                UpdateOcrSettings();

                // オプティマイザーの初期化
                if (_ocrManager != null)
                {
                    _ocrOptimizer = new OcrOptimizer(_ocrManager);
                    Debug.WriteLine("InitializeServices: OCRオプティマイザー初期化完了");
                }

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

        // リソース解放の順序を制御
        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            Debug.WriteLine("アプリケーションを終了しています...");

            try
            {
                // 1. オーバーレイフォームを先に閉じる
                if (_overlayForm != null && !_overlayForm.IsDisposed)
                {
                    Debug.WriteLine("オーバーレイフォームを閉じています...");
                    _overlayForm.Close();
                    _overlayForm = null;
                }

                // 2. 翻訳マネージャーの破棄 - IDisposable 実装後に追加
                if (_translationManager != null)
                {
                    Debug.WriteLine("翻訳マネージャーを破棄しています...");
                    _translationManager.Dispose();
                    _translationManager = null;
                }

                // 3. OCRマネージャーを破棄
                if (_ocrManager != null)
                {
                    Debug.WriteLine("OCRマネージャーを破棄しています...");
                    _ocrManager.Dispose();
                    _ocrManager = null;
                }

                // 4. 最後にResourceManagerのDisposeAllを呼び出し
                Debug.WriteLine("残りのリソースをクリーンアップしています...");
                int disposedResources = ResourceManager.DisposeAll();
                Debug.WriteLine($"{disposedResources}個の残りリソースを解放しました");

                // 5. GCを明示的に実行
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect(); // 2回目のGCで断片化を減らす

                Debug.WriteLine("アプリケーションのクリーンアップが完了しました");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"アプリケーション終了時のエラー: {ex.Message}");
            }
        }

        private void UpdateSettingsFromConfig()
        {
            // OCR設定の更新
            if (_ocrManager != null)
            {
                _ocrManager.SetConfidenceThreshold(AppSettings.Instance.OcrConfidenceThreshold);
                _ocrManager.EnablePreprocessing(AppSettings.Instance.EnablePreprocessing);
                // スライダーやチェックボックスの更新コードは削除
            }

            // 翻訳設定の更新
            if (_translationManager != null)
            {
                _translationManager.SetPreferredTargetLanguage(AppSettings.Instance.TargetLanguage);
                _useAutoDetectCheckBox.Checked = AppSettings.Instance.UseAutoDetect;
                _targetLanguageComboBox.SelectedIndex = Array.IndexOf(
                    UILanguageManager.SupportedLanguages,
                    AppSettings.Instance.TargetLanguage
                );
            }

            // 状態表示の更新
            UpdateStatus("設定を更新しました");
        }

        /// <summary>
        /// OCR設定UIを現在の状態に更新
        /// </summary>
        private void UpdateOcrSettings()
        {
            // ダミー実装（空）
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

        /// <summary>
        /// 現在選択されている翻訳先言語コードを取得します
        /// </summary>
        /// <returns>言語コード</returns>
        private string GetSelectedTargetLanguage()
        {
            if (_targetLanguageComboBox.SelectedIndex >= 0 &&
                _targetLanguageComboBox.SelectedIndex < UILanguageManager.SupportedLanguages.Length)
            {
                return UILanguageManager.SupportedLanguages[_targetLanguageComboBox.SelectedIndex];
            }
            return "ja"; // デフォルトは日本語
        }

        /// <summary>
        /// 翻訳ボックスのフォントを更新します
        /// </summary>
        /// <param name="languageCode">言語コード</param>
        private void UpdateTranslationBoxFont(string languageCode)
        {
            if (_overlayForm != null && _overlayForm.TranslationBox != null && !_overlayForm.TranslationBox.IsDisposed)
            {
                try
                {
                    TranslationLanguage language = (languageCode == "ja")
                        ? TranslationLanguage.Japanese
                        : TranslationLanguage.English;

                    FontManager.Instance.ApplyTranslationFont(_overlayForm.TranslationBox, language);
                    Debug.WriteLine($"翻訳ボックスのフォントを{language}用に更新しました");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"翻訳ボックスのフォント更新時にエラーが発生しました: {ex.Message}");
                }
            }
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
            // プロファイルの適用を追加
            if (_selectedWindow != null)
            {
                // 選択したゲームのプロファイルを適用
                ApplyGameProfile(_selectedWindow.Title);
            }
        }

        // プロファイル適用処理
        private void ApplyGameProfile(string gameTitle)
        {
            try
            {
                // プロファイルが存在するか確認
                if (_gameProfiles != null && _gameProfiles.HasProfile(gameTitle))
                {
                    // プロファイルを適用
                    var profile = _gameProfiles.GetProfile(gameTitle);

                    if (_ocrManager != null)
                    {
                        // OCR設定を適用
                        _ocrManager.SetConfidenceThreshold(profile.ConfidenceThreshold);
                        _ocrManager.SetPreprocessingOptions(profile.PreprocessingOptions);
                        _ocrManager.EnablePreprocessing(true);

                        // UI更新部分を削除（スライダーなどが無くなったため）

                        UpdateStatus($"{gameTitle} の最適化プロファイルを適用しました");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"プロファイル適用エラー: {ex.Message}");
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

        private void RunBenchmark()
        {
            // 削除または空実装
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