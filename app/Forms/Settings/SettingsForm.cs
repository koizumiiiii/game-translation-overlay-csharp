using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using GameTranslationOverlay.Core.Configuration;

namespace GameTranslationOverlay.Forms.Settings
{
    /// <summary>
    /// アプリケーション設定画面
    /// </summary>
    public partial class SettingsForm : Form
    {
        private TabControl _tabControl;
        private TabPage _generalTab;
        private TabPage _ocrTab;
        private TabPage _translationTab;
        private TabPage _profileTab;

        private Button _okButton;
        private Button _cancelButton;
        private Button _applyButton;

        private GeneralSettingsControl _generalSettings;
        private OcrSettingsControl _ocrSettings;
        private TranslationSettingsControl _translationSettings;
        private ProfileSettingsControl _profileSettings;

        private bool _settingsChanged = false;

        /// <summary>
        /// 設定が変更されたときに発生するイベント
        /// </summary>
        public event EventHandler SettingsChanged;

        public SettingsForm()
        {
            InitializeComponent();

            // フォームのプロパティを設定
            this.Text = "設定";
            this.MinimumSize = new Size(600, 500);
            this.Size = new Size(600, 500);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = true;
            this.StartPosition = FormStartPosition.CenterParent;

            // 設定を読み込む
            LoadSettings();
        }

        private void InitializeComponent()
        {
            // タブコントロール
            _tabControl = new TabControl
            {
                Dock = DockStyle.Fill,
                Padding = new Point(10, 3)
            };

            // タブページ
            _generalTab = new TabPage("一般");
            _ocrTab = new TabPage("OCR設定");
            _translationTab = new TabPage("翻訳設定");
            _profileTab = new TabPage("ゲームプロファイル");

            // 各タブのコントロールを初期化
            _generalSettings = new GeneralSettingsControl();
            _generalSettings.SettingChanged += OnSettingChanged;
            _generalTab.Controls.Add(_generalSettings);

            _ocrSettings = new OcrSettingsControl();
            _ocrSettings.SettingChanged += OnSettingChanged;
            _ocrTab.Controls.Add(_ocrSettings);

            _translationSettings = new TranslationSettingsControl();
            _translationSettings.SettingChanged += OnSettingChanged;
            _translationTab.Controls.Add(_translationSettings);

            _profileSettings = new ProfileSettingsControl();
            _profileSettings.SettingChanged += OnSettingChanged;
            _profileTab.Controls.Add(_profileSettings);

            // タブをタブコントロールに追加
            _tabControl.Controls.Add(_generalTab);
            _tabControl.Controls.Add(_ocrTab);
            _tabControl.Controls.Add(_translationTab);
            _tabControl.Controls.Add(_profileTab);

            // ボタンパネル
            var buttonPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 50
            };

            // OKボタン
            _okButton = new Button
            {
                Text = "OK",
                Size = new Size(80, 30),
                Location = new Point(buttonPanel.Width - 275, 10),
                Anchor = AnchorStyles.Right | AnchorStyles.Bottom
            };
            _okButton.Click += OkButton_Click;

            // キャンセルボタン
            _cancelButton = new Button
            {
                Text = "キャンセル",
                Size = new Size(80, 30),
                Location = new Point(buttonPanel.Width - 180, 10),
                Anchor = AnchorStyles.Right | AnchorStyles.Bottom
            };
            _cancelButton.Click += CancelButton_Click;

            // 適用ボタン
            _applyButton = new Button
            {
                Text = "適用",
                Size = new Size(80, 30),
                Location = new Point(buttonPanel.Width - 85, 10),
                Anchor = AnchorStyles.Right | AnchorStyles.Bottom,
                Enabled = false
            };
            _applyButton.Click += ApplyButton_Click;

            // ボタンをパネルに追加
            buttonPanel.Controls.Add(_okButton);
            buttonPanel.Controls.Add(_cancelButton);
            buttonPanel.Controls.Add(_applyButton);

            // コントロールをフォームに追加
            this.Controls.Add(_tabControl);
            this.Controls.Add(buttonPanel);

            // イベントハンドラの追加
            this.FormClosing += SettingsForm_FormClosing;
        }

        /// <summary>
        /// 設定を読み込む
        /// </summary>
        private void LoadSettings()
        {
            try
            {
                // 各タブのコントロールに設定を読み込ませる
                _generalSettings.LoadSettings();
                _ocrSettings.LoadSettings();
                _translationSettings.LoadSettings();
                _profileSettings.LoadSettings();

                // 設定変更フラグをリセット
                _settingsChanged = false;
                _applyButton.Enabled = false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"設定の読み込みに失敗しました: {ex.Message}");
                MessageBox.Show(
                    $"設定の読み込みに失敗しました: {ex.Message}",
                    "エラー",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
            }
        }

        /// <summary>
        /// 設定を保存する
        /// </summary>
        private void SaveSettings()
        {
            try
            {
                // 各タブのコントロールから設定を保存
                _generalSettings.SaveSettings();
                _ocrSettings.SaveSettings();
                _translationSettings.SaveSettings();
                _profileSettings.SaveSettings();

                // 設定をファイルに保存
                AppSettings.Instance.SaveSettings();

                // 設定変更フラグをリセット
                _settingsChanged = false;
                _applyButton.Enabled = false;

                // 設定変更イベントを発火
                SettingsChanged?.Invoke(this, EventArgs.Empty);

                Debug.WriteLine("設定を保存しました");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"設定の保存に失敗しました: {ex.Message}");
                MessageBox.Show(
                    $"設定の保存に失敗しました: {ex.Message}",
                    "エラー",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
            }
        }

        private void OnSettingChanged(object sender, EventArgs e)
        {
            _settingsChanged = true;
            _applyButton.Enabled = true;
        }

        private void OkButton_Click(object sender, EventArgs e)
        {
            if (_settingsChanged)
            {
                SaveSettings();
            }

            this.DialogResult = DialogResult.OK;
            this.Close();
        }

        private void CancelButton_Click(object sender, EventArgs e)
        {
            this.DialogResult = DialogResult.Cancel;
            this.Close();
        }

        private void ApplyButton_Click(object sender, EventArgs e)
        {
            SaveSettings();
        }

        private void SettingsForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (_settingsChanged)
            {
                DialogResult result = MessageBox.Show(
                    "設定が変更されています。保存しますか？",
                    "確認",
                    MessageBoxButtons.YesNoCancel,
                    MessageBoxIcon.Question
                );

                if (result == DialogResult.Yes)
                {
                    SaveSettings();
                }
                else if (result == DialogResult.Cancel)
                {
                    e.Cancel = true;
                }
            }
        }
    }
}