using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using GameTranslationOverlay.Core.Configuration;
using GameTranslationOverlay.Core.OCR;

namespace GameTranslationOverlay.Forms.Settings
{
    /// <summary>
    /// ゲームプロファイル設定を管理するコントロール
    /// </summary>
    public class ProfileSettingsControl : UserControl
    {
        private ListView _profileListView;
        private Button _addButton;
        private Button _editButton;
        private Button _deleteButton;
        private Button _importButton;
        private Button _exportButton;
        private Label _descriptionLabel;
        private ToolTip _toolTip;

        // プロファイルデータクラス（将来的にはCore.Configuration内に移動すべき）
        private class GameProfile
        {
            public string GameName { get; set; }
            public string ExecutableName { get; set; }
            public float ConfidenceThreshold { get; set; }
            public bool EnablePreprocessing { get; set; }
            public int PreprocessingPreset { get; set; }
            public DateTime LastModified { get; set; }

            public GameProfile()
            {
                ConfidenceThreshold = 0.6f;
                EnablePreprocessing = true;
                PreprocessingPreset = 0;
                LastModified = DateTime.Now;
            }
        }

        // 現在のプロファイルリスト
        private List<GameProfile> _profiles = new List<GameProfile>();

        /// <summary>
        /// 設定が変更されたときに発生するイベント
        /// </summary>
        public event EventHandler SettingChanged;

        public ProfileSettingsControl()
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

            // 説明ラベル
            _descriptionLabel = new Label
            {
                Text = "ゲームごとに最適化された設定プロファイルを管理できます。",
                Location = new Point(20, 20),
                Size = new Size(460, 30)
            };

            // プロファイルリストビュー
            _profileListView = new ListView
            {
                Location = new Point(20, 60),
                Size = new Size(460, 200),
                View = View.Details,
                FullRowSelect = true,
                GridLines = true,
                MultiSelect = false
            };
            _profileListView.Columns.Add("ゲーム名", 150);
            _profileListView.Columns.Add("実行ファイル", 150);
            _profileListView.Columns.Add("信頼度閾値", 80);
            _profileListView.Columns.Add("最終更新", 80);
            _profileListView.SelectedIndexChanged += (s, e) => UpdateButtonStates();
            _profileListView.DoubleClick += (s, e) => EditSelectedProfile();
            _toolTip.SetToolTip(_profileListView, "プロファイルをダブルクリックして編集します。");

            // 追加ボタン
            _addButton = new Button
            {
                Text = "追加",
                Location = new Point(20, 270),
                Size = new Size(80, 30)
            };
            _addButton.Click += (s, e) => AddNewProfile();
            _toolTip.SetToolTip(_addButton, "新しいゲームプロファイルを追加します。");

            // 編集ボタン
            _editButton = new Button
            {
                Text = "編集",
                Location = new Point(110, 270),
                Size = new Size(80, 30),
                Enabled = false
            };
            _editButton.Click += (s, e) => EditSelectedProfile();
            _toolTip.SetToolTip(_editButton, "選択したプロファイルを編集します。");

            // 削除ボタン
            _deleteButton = new Button
            {
                Text = "削除",
                Location = new Point(200, 270),
                Size = new Size(80, 30),
                Enabled = false
            };
            _deleteButton.Click += (s, e) => DeleteSelectedProfile();
            _toolTip.SetToolTip(_deleteButton, "選択したプロファイルを削除します。");

            // インポートボタン
            _importButton = new Button
            {
                Text = "インポート",
                Location = new Point(300, 270),
                Size = new Size(80, 30)
            };
            _importButton.Click += (s, e) => ImportProfiles();
            _toolTip.SetToolTip(_importButton, "プロファイルをJSONファイルからインポートします。");

            // エクスポートボタン
            _exportButton = new Button
            {
                Text = "エクスポート",
                Location = new Point(390, 270),
                Size = new Size(80, 30),
                Enabled = false
            };
            _exportButton.Click += (s, e) => ExportSelectedProfile();
            _toolTip.SetToolTip(_exportButton, "選択したプロファイルをJSONファイルにエクスポートします。");

            // コントロールをフォームに追加
            this.Controls.Add(_descriptionLabel);
            this.Controls.Add(_profileListView);
            this.Controls.Add(_addButton);
            this.Controls.Add(_editButton);
            this.Controls.Add(_deleteButton);
            this.Controls.Add(_importButton);
            this.Controls.Add(_exportButton);

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
                // ダミーデータを作成 (実際の実装ではファイルやデータベースから読み込む)
                _profiles = CreateDummyProfiles();
                
                // リストビューを更新
                UpdateProfileListView();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"プロファイル設定の読み込みに失敗しました: {ex.Message}");
            }
        }

        /// <summary>
        /// 設定を保存する
        /// </summary>
        public void SaveSettings()
        {
            try
            {
                // プロファイル設定を保存 (実際の実装ではファイルやデータベースに保存)
                Debug.WriteLine($"{_profiles.Count}個のプロファイルを保存します");
                
                // 将来的には、Core.Configuration内にGameProfilesクラスを作成し、
                // そこでJSONシリアライズして保存する実装を行う
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"プロファイル設定の保存に失敗しました: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// リストビューを更新する
        /// </summary>
        private void UpdateProfileListView()
        {
            _profileListView.Items.Clear();
            
            foreach (var profile in _profiles)
            {
                var item = new ListViewItem(profile.GameName);
                item.SubItems.Add(profile.ExecutableName);
                item.SubItems.Add(profile.ConfidenceThreshold.ToString("F2"));
                item.SubItems.Add(profile.LastModified.ToString("yyyy/MM/dd"));
                item.Tag = profile;
                
                _profileListView.Items.Add(item);
            }
            
            UpdateButtonStates();
        }

        /// <summary>
        /// ボタンの状態を更新する
        /// </summary>
        private void UpdateButtonStates()
        {
            bool hasSelection = _profileListView.SelectedItems.Count > 0;
            _editButton.Enabled = hasSelection;
            _deleteButton.Enabled = hasSelection;
            _exportButton.Enabled = hasSelection;
        }

        /// <summary>
        /// 新しいプロファイルを追加する
        /// </summary>
        private void AddNewProfile()
        {
            try
            {
                // 新しいプロファイルの作成
                var profile = new GameProfile
                {
                    GameName = "新しいゲーム",
                    ExecutableName = "game.exe",
                    ConfidenceThreshold = 0.6f,
                    EnablePreprocessing = true,
                    PreprocessingPreset = 0,
                    LastModified = DateTime.Now
                };
                
                // プロファイル編集ダイアログを表示
                if (EditProfile(profile))
                {
                    // プロファイルを追加
                    _profiles.Add(profile);
                    
                    // リストビューを更新
                    UpdateProfileListView();
                    
                    // 設定変更イベントを発火
                    OnSettingChanged();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"プロファイル追加エラー: {ex.Message}");
                MessageBox.Show(
                    $"プロファイルの追加に失敗しました: {ex.Message}",
                    "エラー",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
            }
        }

        /// <summary>
        /// 選択したプロファイルを編集する
        /// </summary>
        private void EditSelectedProfile()
        {
            try
            {
                if (_profileListView.SelectedItems.Count == 0)
                    return;
                
                // 選択したプロファイルを取得
                var selectedItem = _profileListView.SelectedItems[0];
                var profile = selectedItem.Tag as GameProfile;
                
                if (profile == null)
                    return;
                
                // プロファイル編集ダイアログを表示
                if (EditProfile(profile))
                {
                    // 最終更新日時を更新
                    profile.LastModified = DateTime.Now;
                    
                    // リストビューを更新
                    UpdateProfileListView();
                    
                    // 設定変更イベントを発火
                    OnSettingChanged();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"プロファイル編集エラー: {ex.Message}");
                MessageBox.Show(
                    $"プロファイルの編集に失敗しました: {ex.Message}",
                    "エラー",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
            }
        }

        /// <summary>
        /// 選択したプロファイルを削除する
        /// </summary>
        private void DeleteSelectedProfile()
        {
            try
            {
                if (_profileListView.SelectedItems.Count == 0)
                    return;
                
                // 確認ダイアログを表示
                var result = MessageBox.Show(
                    "選択したプロファイルを削除しますか？",
                    "確認",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question
                );
                
                if (result != DialogResult.Yes)
                    return;
                
                // 選択したプロファイルを取得
                var selectedItem = _profileListView.SelectedItems[0];
                var profile = selectedItem.Tag as GameProfile;
                
                if (profile == null)
                    return;
                
                // プロファイルを削除
                _profiles.Remove(profile);
                
                // リストビューを更新
                UpdateProfileListView();
                
                // 設定変更イベントを発火
                OnSettingChanged();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"プロファイル削除エラー: {ex.Message}");
                MessageBox.Show(
                    $"プロファイルの削除に失敗しました: {ex.Message}",
                    "エラー",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
            }
        }

        /// <summary>
        /// プロファイルをインポートする
        /// </summary>
        private void ImportProfiles()
        {
            try
            {
                // ファイル選択ダイアログを表示
                using (var dialog = new OpenFileDialog())
                {
                    dialog.Filter = "JSONファイル (*.json)|*.json|すべてのファイル (*.*)|*.*";
                    dialog.Title = "プロファイルをインポート";
                    
                    if (dialog.ShowDialog() == DialogResult.OK)
                    {
                        // ファイルからプロファイルを読み込む
                        // 実際の実装ではJSONデシリアライズを行う
                        
                        MessageBox.Show(
                            "プロファイルがインポートされました。",
                            "成功",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Information
                        );
                        
                        // リストビューを更新
                        UpdateProfileListView();
                        
                        // 設定変更イベントを発火
                        OnSettingChanged();
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"プロファイルインポートエラー: {ex.Message}");
                MessageBox.Show(
                    $"プロファイルのインポートに失敗しました: {ex.Message}",
                    "エラー",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
            }
        }

        /// <summary>
        /// 選択したプロファイルをエクスポートする
        /// </summary>
        private void ExportSelectedProfile()
        {
            try
            {
                if (_profileListView.SelectedItems.Count == 0)
                    return;
                
                // 選択したプロファイルを取得
                var selectedItem = _profileListView.SelectedItems[0];
                var profile = selectedItem.Tag as GameProfile;
                
                if (profile == null)
                    return;
                
                // ファイル保存ダイアログを表示
                using (var dialog = new SaveFileDialog())
                {
                    dialog.Filter = "JSONファイル (*.json)|*.json|すべてのファイル (*.*)|*.*";
                    dialog.Title = "プロファイルをエクスポート";
                    dialog.FileName = $"{profile.GameName}.json";
                    
                    if (dialog.ShowDialog() == DialogResult.OK)
                    {
                        // プロファイルをファイルに保存
                        // 実際の実装ではJSONシリアライズを行う
                        
                        MessageBox.Show(
                            "プロファイルがエクスポートされました。",
                            "成功",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Information
                        );
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"プロファイルエクスポートエラー: {ex.Message}");
                MessageBox.Show(
                    $"プロファイルのエクスポートに失敗しました: {ex.Message}",
                    "エラー",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
            }
        }

        /// <summary>
        /// プロファイルを編集する
        /// </summary>
        private bool EditProfile(GameProfile profile)
        {
            // ここではダイアログ実装は省略し、基本的なダイアログで代用
            using (var form = new Form())
            {
                form.Text = "プロファイル編集";
                form.Size = new Size(400, 250);
                form.FormBorderStyle = FormBorderStyle.FixedDialog;
                form.MaximizeBox = false;
                form.MinimizeBox = false;
                form.StartPosition = FormStartPosition.CenterParent;
                
                // ゲーム名
                var gameNameLabel = new Label { Text = "ゲーム名:", Location = new Point(20, 20), AutoSize = true };
                var gameNameTextBox = new TextBox { Text = profile.GameName, Location = new Point(120, 17), Size = new Size(200, 20) };
                
                // 実行ファイル名
                var exeNameLabel = new Label { Text = "実行ファイル:", Location = new Point(20, 50), AutoSize = true };
                var exeNameTextBox = new TextBox { Text = profile.ExecutableName, Location = new Point(120, 47), Size = new Size(200, 20) };
                
                // 信頼度閾値
                var thresholdLabel = new Label { Text = "信頼度閾値:", Location = new Point(20, 80), AutoSize = true };
                var thresholdTrackBar = new TrackBar { Location = new Point(120, 70), Size = new Size(200, 45), Minimum = 1, Maximum = 100, Value = (int)(profile.ConfidenceThreshold * 100) };
                var thresholdValueLabel = new Label { Text = profile.ConfidenceThreshold.ToString("F2"), Location = new Point(330, 80), AutoSize = true };
                thresholdTrackBar.ValueChanged += (s, e) => thresholdValueLabel.Text = (thresholdTrackBar.Value / 100.0f).ToString("F2");
                
                // 前処理の有効/無効
                var preprocessingCheckBox = new CheckBox { Text = "前処理を有効にする", Location = new Point(20, 120), AutoSize = true, Checked = profile.EnablePreprocessing };
                
                // プリセット選択
                var presetLabel = new Label { Text = "前処理プリセット:", Location = new Point(20, 150), AutoSize = true };
                var presetComboBox = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Location = new Point(120, 147), Size = new Size(200, 21) };
                presetComboBox.Items.AddRange(new object[] { "デフォルト", "日本語テキスト用", "英語テキスト用", "軽量プリセット" });
                presetComboBox.SelectedIndex = profile.PreprocessingPreset;
                
                // OKボタン
                var okButton = new Button { Text = "OK", DialogResult = DialogResult.OK, Location = new Point(120, 180), Size = new Size(80, 30) };
                
                // キャンセルボタン
                var cancelButton = new Button { Text = "キャンセル", DialogResult = DialogResult.Cancel, Location = new Point(210, 180), Size = new Size(80, 30) };
                
                // コントロールをフォームに追加
                form.Controls.Add(gameNameLabel);
                form.Controls.Add(gameNameTextBox);
                form.Controls.Add(exeNameLabel);
                form.Controls.Add(exeNameTextBox);
                form.Controls.Add(thresholdLabel);
                form.Controls.Add(thresholdTrackBar);
                form.Controls.Add(thresholdValueLabel);
                form.Controls.Add(preprocessingCheckBox);
                form.Controls.Add(presetLabel);
                form.Controls.Add(presetComboBox);
                form.Controls.Add(okButton);
                form.Controls.Add(cancelButton);
                
                // フォームを表示
                var result = form.ShowDialog();
                
                if (result == DialogResult.OK)
                {
                    // 入力値を反映
                    profile.GameName = gameNameTextBox.Text;
                    profile.ExecutableName = exeNameTextBox.Text;
                    profile.ConfidenceThreshold = thresholdTrackBar.Value / 100.0f;
                    profile.EnablePreprocessing = preprocessingCheckBox.Checked;
                    profile.PreprocessingPreset = presetComboBox.SelectedIndex;
                    
                    return true;
                }
                
                return false;
            }
        }

        /// <summary>
        /// ダミープロファイルを作成する
        /// </summary>
        private List<GameProfile> CreateDummyProfiles()
        {
            var profiles = new List<GameProfile>();
            
            // ダミーデータの作成
            profiles.Add(new GameProfile
            {
                GameName = "サンプルゲーム1",
                ExecutableName = "game1.exe",
                ConfidenceThreshold = 0.7f,
                EnablePreprocessing = true,
                PreprocessingPreset = 1,
                LastModified = DateTime.Now.AddDays(-5)
            });
            
            profiles.Add(new GameProfile
            {
                GameName = "サンプルゲーム2",
                ExecutableName = "game2.exe",
                ConfidenceThreshold = 0.6f,
                EnablePreprocessing = true,
                PreprocessingPreset = 2,
                LastModified = DateTime.Now.AddDays(-2)
            });
            
            profiles.Add(new GameProfile
            {
                GameName = "サンプルゲーム3",
                ExecutableName = "game3.exe",
                ConfidenceThreshold = 0.5f,
                EnablePreprocessing = false,
                PreprocessingPreset = 0,
                LastModified = DateTime.Now.AddDays(-1)
            });
            
            return profiles;
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