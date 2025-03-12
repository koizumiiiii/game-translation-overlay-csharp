using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using GameTranslationOverlay.Core.Configuration;

namespace GameTranslationOverlay.Forms.Settings
{
    /// <summary>
    /// OCR設定を管理するコントロール
    /// </summary>
    public class OcrSettingsControl : UserControl
    {
        private CheckBox _enablePreprocessingCheckBox;
        private TrackBar _confidenceThresholdTrackBar;
        private Label _confidenceThresholdLabel;
        private Label _confidenceValueLabel;
        private GroupBox _preprocessingGroupBox;
        private RadioButton _defaultPresetRadioButton;
        private RadioButton _jpPresetRadioButton;
        private RadioButton _enPresetRadioButton;
        private RadioButton _lightPresetRadioButton;
        private ToolTip _toolTip;

        /// <summary>
        /// 設定が変更されたときに発生するイベント
        /// </summary>
        public event EventHandler SettingChanged;

        public OcrSettingsControl()
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

            // 前処理有効化チェックボックス
            _enablePreprocessingCheckBox = new CheckBox
            {
                Text = "OCR前処理を有効にする",
                Location = new Point(20, 20),
                AutoSize = true,
                Checked = true
            };
            _enablePreprocessingCheckBox.CheckedChanged += (s, e) => 
            {
                _preprocessingGroupBox.Enabled = _enablePreprocessingCheckBox.Checked;
                OnSettingChanged();
            };
            _toolTip.SetToolTip(_enablePreprocessingCheckBox, "OCR精度向上のために画像前処理を適用します。無効にするとパフォーマンスが向上する場合があります。");

            // 信頼度閾値ラベルとトラックバー
            _confidenceThresholdLabel = new Label
            {
                Text = "信頼度閾値:",
                Location = new Point(20, 60),
                AutoSize = true
            };
            _toolTip.SetToolTip(_confidenceThresholdLabel, "OCR結果の信頼度が、この値以上の場合のみ結果として採用します。");

            _confidenceThresholdTrackBar = new TrackBar
            {
                Location = new Point(120, 50),
                Size = new Size(300, 45),
                Minimum = 1,
                Maximum = 100,
                Value = 60,
                TickFrequency = 10,
                LargeChange = 10,
                SmallChange = 1
            };
            _confidenceThresholdTrackBar.ValueChanged += (s, e) =>
            {
                float value = _confidenceThresholdTrackBar.Value / 100.0f;
                _confidenceValueLabel.Text = value.ToString("F2");
                OnSettingChanged();
            };
            _toolTip.SetToolTip(_confidenceThresholdTrackBar, "値を大きくすると精度が向上しますが、検出されるテキストが減少します。");

            _confidenceValueLabel = new Label
            {
                Text = "0.60",
                Location = new Point(430, 60),
                AutoSize = true
            };

            // 前処理プリセットグループ
            _preprocessingGroupBox = new GroupBox
            {
                Text = "前処理プリセット",
                Location = new Point(20, 100),
                Size = new Size(440, 150),
                Enabled = true
            };

            // デフォルトプリセット
            _defaultPresetRadioButton = new RadioButton
            {
                Text = "デフォルト（前処理なし）",
                Location = new Point(20, 30),
                AutoSize = true,
                Checked = true
            };
            _defaultPresetRadioButton.CheckedChanged += (s, e) => { if (_defaultPresetRadioButton.Checked) OnSettingChanged(); };
            _toolTip.SetToolTip(_defaultPresetRadioButton, "前処理を適用しません。パフォーマンスが最も高くなります。");

            // 日本語プリセット
            _jpPresetRadioButton = new RadioButton
            {
                Text = "日本語テキスト用最適化",
                Location = new Point(20, 60),
                AutoSize = true
            };
            _jpPresetRadioButton.CheckedChanged += (s, e) => { if (_jpPresetRadioButton.Checked) OnSettingChanged(); };
            _toolTip.SetToolTip(_jpPresetRadioButton, "日本語のテキスト認識精度を向上させるための設定です。");

            // 英語プリセット
            _enPresetRadioButton = new RadioButton
            {
                Text = "英語テキスト用最適化",
                Location = new Point(20, 90),
                AutoSize = true
            };
            _enPresetRadioButton.CheckedChanged += (s, e) => { if (_enPresetRadioButton.Checked) OnSettingChanged(); };
            _toolTip.SetToolTip(_enPresetRadioButton, "英語のテキスト認識精度を向上させるための設定です。");

            // 軽量プリセット
            _lightPresetRadioButton = new RadioButton
            {
                Text = "軽量プリセット（低スペックPC向け）",
                Location = new Point(20, 120),
                AutoSize = true
            };
            _lightPresetRadioButton.CheckedChanged += (s, e) => { if (_lightPresetRadioButton.Checked) OnSettingChanged(); };
            _toolTip.SetToolTip(_lightPresetRadioButton, "パフォーマンスを優先した軽量な設定です。精度が低下する場合があります。");

            // プリセットグループにコントロールを追加
            _preprocessingGroupBox.Controls.Add(_defaultPresetRadioButton);
            _preprocessingGroupBox.Controls.Add(_jpPresetRadioButton);
            _preprocessingGroupBox.Controls.Add(_enPresetRadioButton);
            _preprocessingGroupBox.Controls.Add(_lightPresetRadioButton);

            // コントロールをフォームに追加
            this.Controls.Add(_enablePreprocessingCheckBox);
            this.Controls.Add(_confidenceThresholdLabel);
            this.Controls.Add(_confidenceThresholdTrackBar);
            this.Controls.Add(_confidenceValueLabel);
            this.Controls.Add(_preprocessingGroupBox);

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
                _enablePreprocessingCheckBox.Checked = settings.EnableOcrPreprocessing;
                _confidenceThresholdTrackBar.Value = (int)(settings.DefaultOcrThreshold * 100);
                _confidenceValueLabel.Text = settings.DefaultOcrThreshold.ToString("F2");
                
                // プリセット設定を反映
                // 注: AppSettingsにはまだプリセット設定がないため、デフォルトまたは将来的な拡張での実装を想定
                _preprocessingGroupBox.Enabled = settings.EnableOcrPreprocessing;
                
                // 現在はデフォルトを選択
                _defaultPresetRadioButton.Checked = true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"OCR設定の読み込みに失敗しました: {ex.Message}");
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
                settings.EnableOcrPreprocessing = _enablePreprocessingCheckBox.Checked;
                settings.DefaultOcrThreshold = _confidenceThresholdTrackBar.Value / 100.0f;
                
                // プリセット設定の保存
                // 注: AppSettingsにはまだプリセット設定がないため、将来的な拡張での実装を想定
                // 現在は何もしない
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"OCR設定の保存に失敗しました: {ex.Message}");
                throw;
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