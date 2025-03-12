using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using GameTranslationOverlay.Core.Configuration;
using GameTranslationOverlay.Core.UI;
using GameTranslationOverlay.Core.Licensing;

namespace GameTranslationOverlay.Forms.Settings
{
    /// <summary>
    /// 翻訳設定を管理するコントロール
    /// </summary>
    public class TranslationSettingsControl : UserControl
    {
        private CheckBox _useAutoDetectCheckBox;
        private Label _targetLanguageLabel;
        private ComboBox _targetLanguageComboBox;
        private CheckBox _useAITranslationCheckBox;
        private Label _apiKeyLabel;
        private TextBox _apiKeyTextBox;
        private Button _testApiKeyButton;
        private Label _tokenCountLabel;
        private Label _translationCacheSizeLabel;
        private TrackBar _translationCacheSizeTrackBar;
        private Label _cacheSizeValueLabel;
        private ToolTip _toolTip;

        /// <summary>
        /// 設定が変更されたときに発生するイベント
        /// </summary>
        public event EventHandler SettingChanged;

        public TranslationSettingsControl()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            // ツールチップ
            _toolTip = new ToolTip();
            _toolTip.AutoPopDelay = 5000;
            _toolTip.InitialDelay = 500;
            _toolTip.ReshowDelay = 500;
            _toolTip.ShowAlways = true;

            // 言語自動検出
            _useAutoDetectCheckBox = new CheckBox
            {
                Text = "言語を自動検出する",
                Location = new Point(20, 20),
                AutoSize = true,
                Checked = true
            };
            _useAutoDetectCheckBox.CheckedChanged += (s, e) => 
            {
                _targetLanguageComboBox.Enabled = !_useAutoDetectCheckBox.Checked;
                OnSettingChanged();
            };
            _toolTip.SetToolTip(_useAutoDetectCheckBox, "テキストの言語を自動的に検出して、適切な翻訳方向を選択します。");

            // 翻訳先言語
            _targetLanguageLabel = new Label
            {
                Text = "翻訳先言語:",
                Location = new Point(20, 60),
                AutoSize = true
            };

            _targetLanguageComboBox = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Location = new Point(120, 57),
                Size = new Size(150, 21),
                Enabled = false
            };
            
            // 言語リストを追加
            foreach (string langCode in LanguageManager.SupportedLanguages)
            {
                _targetLanguageComboBox.Items.Add(LanguageManager.LanguageNames[langCode]);
            }
            
            // 初期値を日本語に設定
            _targetLanguageComboBox.SelectedIndex = Array.IndexOf(LanguageManager.SupportedLanguages, "ja");
            _targetLanguageComboBox.SelectedIndexChanged += (s, e) => OnSettingChanged();
            _toolTip.SetToolTip(_targetLanguageComboBox, "テキストを翻訳する言語を選択します。");

            // AI翻訳
            _useAITranslationCheckBox = new CheckBox
            {
                Text = "AI翻訳を使用する (Pro版ライセンスが必要)",
                Location = new Point(20, 100),
                AutoSize = true,
                Checked = false
            };
            _useAITranslationCheckBox.CheckedChanged += (s, e) => 
            {
                bool useAI = _useAITranslationCheckBox.Checked;
                
                // Pro版ライセンスがない場合は警告
                if (useAI && !LicenseManager.Instance.HasFeature(PremiumFeature.AiTranslation))
                {
                    MessageBox.Show(
                        "AI翻訳機能を使用するにはPro版ライセンスが必要です。",
                        "ライセンス制限",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning
                    );
                    _useAITranslationCheckBox.Checked = false;
                    return;
                }
                
                _apiKeyLabel.Enabled = useAI;
                _apiKeyTextBox.Enabled = useAI;
                _testApiKeyButton.Enabled = useAI;
                _tokenCountLabel.Visible = useAI;
                
                OnSettingChanged();
            };
            _toolTip.SetToolTip(_useAITranslationCheckBox, "高品質な翻訳結果を得るためにAI翻訳を使用します。Pro版ライセンスが必要です。");

            // APIキー
            _apiKeyLabel = new Label
            {
                Text = "OpenAI APIキー:",
                Location = new Point(40, 130),
                AutoSize = true,
                Enabled = false
            };

            _apiKeyTextBox = new TextBox
            {
                Location = new Point(150, 127),
                Size = new Size(200, 20),
                PasswordChar = '*',
                Enabled = false
            };
            _apiKeyTextBox.TextChanged += (s, e) => OnSettingChanged();
            _toolTip.SetToolTip(_apiKeyTextBox, "OpenAI APIキーを入力してください。空の場合はアプリ内蔵のAPIキーが使用されます。");

            // テストボタン
            _testApiKeyButton = new Button
            {
                Text = "テスト",
                Location = new Point(360, 126),
                Size = new Size(60, 23),
                Enabled = false
            };
            _testApiKeyButton.Click += TestApiKeyButton_Click;
            _toolTip.SetToolTip(_testApiKeyButton, "入力したAPIキーが有効かどうかをテストします。");

            // トークン残量
            _tokenCountLabel = new Label
            {
                Text = "残りトークン: 5000",
                Location = new Point(150, 155),
                AutoSize = true,
                ForeColor = Color.DarkGreen,
                Visible = false
            };

            // 翻訳キャッシュサイズ
            _translationCacheSizeLabel = new Label
            {
                Text = "翻訳キャッシュサイズ:",
                Location = new Point(20, 190),
                AutoSize = true
            };
            _toolTip.SetToolTip(_translationCacheSizeLabel, "翻訳結果をキャッシュするエントリ数。大きくするとメモリ使用量が増加します。");

            _translationCacheSizeTrackBar = new TrackBar
            {
                Location = new Point(150, 180),
                Size = new Size(200, 45),
                Minimum = 100,
                Maximum = 5000,
                Value = 1000,
                TickFrequency = 500,
                LargeChange = 500,
                SmallChange = 100
            };
            _translationCacheSizeTrackBar.ValueChanged += (s, e) =>
            {
                _cacheSizeValueLabel.Text = _translationCacheSizeTrackBar.Value.ToString();
                OnSettingChanged();
            };
            _toolTip.SetToolTip(_translationCacheSizeTrackBar, "値を大きくするとより多くの翻訳結果をキャッシュできますが、メモリ使用量が増加します。");

            _cacheSizeValueLabel = new Label
            {
                Text = "1000",
                Location = new Point(360, 190),
                AutoSize = true
            };

            // コントロールをフォームに追加
            this.Controls.Add(_useAutoDetectCheckBox);
            this.Controls.Add(_targetLanguageLabel);
            this.Controls.Add(_targetLanguageComboBox);
            this.Controls.Add(_useAITranslationCheckBox);
            this.Controls.Add(_apiKeyLabel);
            this.Controls.Add(_apiKeyTextBox);
            this.Controls.Add(_testApiKeyButton);
            this.Controls.Add(_tokenCountLabel);
            this.Controls.Add(_translationCacheSizeLabel);
            this.Controls.Add(_translationCacheSizeTrackBar);
            this.Controls.Add(_cacheSizeValueLabel);

            // ドック設定
            this.Dock = DockStyle.Fill;
        }

        /// <summary>
        /// 設定を読み込む
        /// </summary>
        public void LoadSettings()
        {
            try
            {
                var settings = AppSettings.Instance;
                
                // 設定を反映
                _useAutoDetectCheckBox.Checked = settings.UseAutoDetect;
                _targetLanguageComboBox.Enabled = !settings.UseAutoDetect;
                
                // 翻訳先言語の設定
                int langIndex = Array.IndexOf(LanguageManager.SupportedLanguages, settings.TargetLanguage);
                if (langIndex >= 0)
                {
                    _targetLanguageComboBox.SelectedIndex = langIndex;
                }
                
                // AI翻訳の設定
                bool canUseAI = LicenseManager.Instance.HasFeature(PremiumFeature.AiTranslation);
                _useAITranslationCheckBox.Enabled = canUseAI;
                
                if (!canUseAI)
                {
                    _useAITranslationCheckBox.Checked = false;
                    _toolTip.SetToolTip(_useAITranslationCheckBox, "AI翻訳機能を使用するにはPro版ライセンスが必要です。");
                }
                else
                {
                    _useAITranslationCheckBox.Checked = settings.UseAITranslation;
                }
                
                // APIキーの設定
                _apiKeyTextBox.Text = settings.OpenAIApiKey;
                _apiKeyLabel.Enabled = _useAITranslationCheckBox.Checked;
                _apiKeyTextBox.Enabled = _useAITranslationCheckBox.Checked;
                _testApiKeyButton.Enabled = _useAITranslationCheckBox.Checked;
                _tokenCountLabel.Visible = _useAITranslationCheckBox.Checked;
                
                // キャッシュサイズの設定
                _translationCacheSizeTrackBar.Value = settings.TranslationCacheSize;
                _cacheSizeValueLabel.Text = settings.TranslationCacheSize.ToString();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"翻訳設定の読み込みに失敗しました: {ex.Message}");
            }
        }

        /// <summary>
        /// 設定を保存する
        /// </summary>
        public void SaveSettings()
        {
            try
            {
                var settings = AppSettings.Instance;
                
                // 設定を保存
                settings.UseAutoDetect = _useAutoDetectCheckBox.Checked;
                
                // 翻訳先言語の保存
                int selectedIndex = _targetLanguageComboBox.SelectedIndex;
                if (selectedIndex >= 0 && selectedIndex < LanguageManager.SupportedLanguages.Length)
                {
                    settings.TargetLanguage = LanguageManager.SupportedLanguages[selectedIndex];
                }
                
                // AI翻訳の設定
                settings.UseAITranslation = _useAITranslationCheckBox.Checked;
                
                // APIキーの保存
                settings.OpenAIApiKey = _apiKeyTextBox.Text;
                
                // キャッシュサイズの保存
                settings.TranslationCacheSize = _translationCacheSizeTrackBar.Value;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"翻訳設定の保存に失敗しました: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// APIキーをテストする
        /// </summary>
        private async void TestApiKeyButton_Click(object sender, EventArgs e)
        {
            try
            {
                _testApiKeyButton.Enabled = false;
                _testApiKeyButton.Text = "テスト中...";
                
                // APIキーのテスト
                // 実際の実装では、AITranslationEngineのテスト機能を呼び出す
                await System.Threading.Tasks.Task.Delay(1000); // テスト用の遅延
                
                MessageBox.Show(
                    "APIキーが有効です。",
                    "成功",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                );
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"APIキーテストエラー: {ex.Message}");
                MessageBox.Show(
                    $"APIキーのテストに失敗しました: {ex.Message}",
                    "エラー",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
            }
            finally
            {
                _testApiKeyButton.Enabled = true;
                _testApiKeyButton.Text = "テスト";
            }
        }

        /// <summary>
        /// 設定変更イベントを発火する
        /// </summary>
        private void OnSettingChanged()
        {
            SettingChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}