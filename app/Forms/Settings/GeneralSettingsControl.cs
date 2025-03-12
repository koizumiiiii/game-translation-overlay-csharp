using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using GameTranslationOverlay.Core.Configuration;
using GameTranslationOverlay.Core.Licensing;

namespace GameTranslationOverlay.Forms.Settings
{
    /// <summary>
    /// 一般設定を管理するコントロール
    /// </summary>
    public class GeneralSettingsControl : UserControl
    {
        private CheckBox _debugModeCheckBox;
        private Label _licenseKeyLabel;
        private TextBox _licenseKeyTextBox;
        private Button _activateButton;
        private Label _statusLabel;
        private Label _licenseStatusLabel;
        private Button _resetSettingsButton;
        private ToolTip _toolTip;

        /// <summary>
        /// 設定が変更されたときに発生するイベント
        /// </summary>
        public event EventHandler SettingChanged;

        public GeneralSettingsControl()
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

            // デバッグモード
            _debugModeCheckBox = new CheckBox
            {
                Text = "デバッグモードを有効にする",
                Location = new Point(20, 20),
                AutoSize = true
            };
            _debugModeCheckBox.CheckedChanged += (s, e) => OnSettingChanged();
            _toolTip.SetToolTip(_debugModeCheckBox, "詳細なデバッグ情報を表示します。問題が発生した場合に役立ちます。");

            // ライセンスキー
            _licenseKeyLabel = new Label
            {
                Text = "ライセンスキー:",
                Location = new Point(20, 60),
                AutoSize = true
            };

            _licenseKeyTextBox = new TextBox
            {
                Location = new Point(120, 58),
                Size = new Size(250, 20),
                PasswordChar = '*'
            };
            _licenseKeyTextBox.TextChanged += (s, e) => OnSettingChanged();
            _toolTip.SetToolTip(_licenseKeyTextBox, "ライセンスキーを入力してください。プロ版機能を利用するために必要です。");

            // 有効化ボタン
            _activateButton = new Button
            {
                Text = "有効化",
                Location = new Point(380, 56),
                Size = new Size(80, 25)
            };
            _activateButton.Click += ActivateButton_Click;
            _toolTip.SetToolTip(_activateButton, "ライセンスキーを確認して有効化します。");

            // ライセンス状態
            _statusLabel = new Label
            {
                Text = "ライセンス状態:",
                Location = new Point(20, 100),
                AutoSize = true
            };

            _licenseStatusLabel = new Label
            {
                Text = "確認中...",
                Location = new Point(120, 100),
                AutoSize = true,
                ForeColor = Color.DarkBlue
            };

            // 設定リセットボタン
            _resetSettingsButton = new Button
            {
                Text = "設定をリセット",
                Location = new Point(20, 150),
                Size = new Size(120, 30)
            };
            _resetSettingsButton.Click += ResetSettingsButton_Click;
            _toolTip.SetToolTip(_resetSettingsButton, "すべての設定をデフォルト値に戻します。ライセンスキーは保持されます。");

            // コントロールをフォームに追加
            this.Controls.Add(_debugModeCheckBox);
            this.Controls.Add(_licenseKeyLabel);
            this.Controls.Add(_licenseKeyTextBox);
            this.Controls.Add(_activateButton);
            this.Controls.Add(_statusLabel);
            this.Controls.Add(_licenseStatusLabel);
            this.Controls.Add(_resetSettingsButton);

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
                _debugModeCheckBox.Checked = settings.DebugModeEnabled;
                _licenseKeyTextBox.Text = settings.LicenseKey;
                
                UpdateLicenseStatus();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"一般設定の読み込みに失敗しました: {ex.Message}");
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
                settings.DebugModeEnabled = _debugModeCheckBox.Checked;
                settings.LicenseKey = _licenseKeyTextBox.Text;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"一般設定の保存に失敗しました: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// ライセンス状態を更新する
        /// </summary>
        private void UpdateLicenseStatus()
        {
            // ライセンスマネージャーからライセンス状態を取得
            var licenseManager = LicenseManager.Instance;
            bool isValid = licenseManager.IsLicenseValid;
            var licenseType = licenseManager.CurrentLicenseType;

            // 状態に応じてラベルを設定
            if (isValid)
            {
                _licenseStatusLabel.Text = $"{licenseType} (有効)";
                _licenseStatusLabel.ForeColor = Color.DarkGreen;
            }
            else if (licenseType == LicenseType.Free)
            {
                _licenseStatusLabel.Text = "無料版";
                _licenseStatusLabel.ForeColor = Color.DarkBlue;
            }
            else
            {
                _licenseStatusLabel.Text = $"{licenseType} (無効)";
                _licenseStatusLabel.ForeColor = Color.Red;
            }
        }

        /// <summary>
        /// ライセンスキーを有効化する
        /// </summary>
        private void ActivateButton_Click(object sender, EventArgs e)
        {
            try
            {
                // 入力されたライセンスキーを保存
                AppSettings.Instance.LicenseKey = _licenseKeyTextBox.Text;
                AppSettings.Instance.SaveSettings();

                // ライセンスマネージャーを再初期化
                bool result = LicenseManager.Instance.ValidateLicense(_licenseKeyTextBox.Text);
                
                // 結果に応じてメッセージを表示
                if (result)
                {
                    MessageBox.Show(
                        "ライセンスキーが有効化されました。",
                        "成功",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information
                    );
                }
                else
                {
                    MessageBox.Show(
                        "ライセンスキーが無効です。正しいキーを入力してください。",
                        "エラー",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error
                    );
                }
                
                // ライセンス状態を更新
                UpdateLicenseStatus();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ライセンスキー有効化エラー: {ex.Message}");
                MessageBox.Show(
                    $"ライセンスキーの有効化に失敗しました: {ex.Message}",
                    "エラー",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
            }
        }

        /// <summary>
        /// 設定をリセットする
        /// </summary>
        private void ResetSettingsButton_Click(object sender, EventArgs e)
        {
            DialogResult result = MessageBox.Show(
                "すべての設定をデフォルト値にリセットしますか？\nライセンスキーは保持されます。",
                "確認",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question
            );
            
            if (result == DialogResult.Yes)
            {
                try
                {
                    // 設定をリセット
                    AppSettings.Instance.ResetToDefaults();
                    
                    // 設定を読み込み直す
                    LoadSettings();
                    
                    MessageBox.Show(
                        "設定がリセットされました。",
                        "成功",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information
                    );
                    
                    // 設定変更イベントを発火
                    OnSettingChanged();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"設定リセットエラー: {ex.Message}");
                    MessageBox.Show(
                        $"設定のリセットに失敗しました: {ex.Message}",
                        "エラー",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error
                    );
                }
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