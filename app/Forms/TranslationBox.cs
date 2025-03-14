using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using GameTranslationOverlay.Core.Translation.Services;
using GameTranslationOverlay.Core.UI;
using TranslationLanguageManager = GameTranslationOverlay.Core.Translation.Services.LanguageManager;
using UILanguageManager = GameTranslationOverlay.Core.UI.LanguageManager;

namespace GameTranslationOverlay.Forms
{
    /// <summary>
    /// 翻訳テキストを表示するウィンドウ
    /// </summary>
    public class TranslationBox : Form
    {
        // Win32 API定義
        [DllImport("user32.dll")]
        public static extern bool ReleaseCapture();

        [DllImport("user32.dll")]
        public static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);

        private const int WM_NCLBUTTONDOWN = 0xA1;
        private const int HT_CAPTION = 0x2;

        // UIコンポーネント
        private RichTextBox _textBox;
        private Panel _controlPanel;
        private ComboBox _targetLanguageComboBox;
        private CheckBox _autoDetectCheckBox;
        private Button _closeButton;
        private ToolTip _toolTip;

        // 翻訳エンジン
        private TranslationManager _translationManager;

        // 自動検出フラグ
        private bool _useAutoDetect = true;

        // チェックボックスイベントハンドラを保持する変数
        private EventHandler _autoDetectCheckBoxHandler;

        // 言語設定変更イベント（MainFormで購読可能）
        public event EventHandler<TranslationSettingsChangedEventArgs> TranslationSettingsChanged;

        /// <summary>
        /// コンストラクタ
        /// </summary>
        public TranslationBox()
        {
            InitializeComponents();
        }

        /// <summary>
        /// 翻訳マネージャーを設定する
        /// </summary>
        /// <param name="translationManager">翻訳マネージャー</param>
        public void SetTranslationManager(TranslationManager translationManager)
        {
            _translationManager = translationManager;
        }

        /// <summary>
        /// UIコンポーネントの初期化
        /// </summary>
        private void InitializeComponents()
        {
            // ウィンドウの基本設定
            this.FormBorderStyle = FormBorderStyle.None;
            this.BackColor = Color.FromArgb(30, 30, 30);
            this.Opacity = 0.95;
            this.Size = new Size(350, 250);
            this.StartPosition = FormStartPosition.Manual;
            this.Location = new Point(Screen.PrimaryScreen.Bounds.Width / 2 - 175, Screen.PrimaryScreen.Bounds.Height / 2 - 125);
            this.ShowInTaskbar = false;
            this.TopMost = true;

            // ツールチップの初期化
            _toolTip = new ToolTip();
            _toolTip.AutoPopDelay = 5000;
            _toolTip.InitialDelay = 500;
            _toolTip.ReshowDelay = 200;
            _toolTip.ShowAlways = true;

            // コントロールパネル（上部）
            _controlPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 35,
                BackColor = Color.FromArgb(45, 45, 48)
            };

            // ドラッグ可能にする
            _controlPanel.MouseDown += (s, e) =>
            {
                if (e.Button == MouseButtons.Left)
                {
                    ReleaseCapture();
                    SendMessage(Handle, WM_NCLBUTTONDOWN, HT_CAPTION, 0);
                }
            };

            // 言語選択コンボボックス
            _targetLanguageComboBox = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Location = new Point(10, 7),
                Size = new Size(100, 25),
                BackColor = Color.FromArgb(60, 60, 65),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };

            // 言語選択肢を追加
            foreach (string langCode in UILanguageManager.SupportedLanguages)
            {
                _targetLanguageComboBox.Items.Add(UILanguageManager.LanguageNames[langCode]);
            }

            // 初期言語を日本語に設定
            _targetLanguageComboBox.SelectedIndex = Array.IndexOf(UILanguageManager.SupportedLanguages, "ja");

            // ツールチップを設定
            _toolTip.SetToolTip(_targetLanguageComboBox, "翻訳先の言語を選択します");

            // 言語選択変更イベント
            _targetLanguageComboBox.SelectedIndexChanged += (s, e) =>
            {
                if (_targetLanguageComboBox.SelectedIndex >= 0)
                {
                    string selectedLang = UILanguageManager.SupportedLanguages[_targetLanguageComboBox.SelectedIndex];

                    // 翻訳マネージャーに設定
                    if (_translationManager != null)
                    {
                        _translationManager.SetPreferredTargetLanguage(selectedLang);
                    }

                    // イベント発火して外部（MainForm）に通知
                    OnTranslationSettingsChanged(selectedLang, _useAutoDetect);

                    Debug.WriteLine($"翻訳ボックス: 翻訳先言語を {selectedLang} に設定しました");
                }
            };

            // 自動検出チェックボックス
            _autoDetectCheckBox = new CheckBox
            {
                Text = "言語自動検出",
                Location = new Point(120, 8),
                Size = new Size(110, 20),
                Checked = true,
                ForeColor = Color.White
            };

            // ツールチップを設定
            _toolTip.SetToolTip(_autoDetectCheckBox, "テキストの言語を自動的に検出します");

            // チェックボックスのイベントハンドラを変数に保存
            _autoDetectCheckBoxHandler = new EventHandler((s, e) =>
            {
                _useAutoDetect = _autoDetectCheckBox.Checked;
                _targetLanguageComboBox.Enabled = !_useAutoDetect;

                // イベント発火して外部（MainForm）に通知
                if (_targetLanguageComboBox.SelectedIndex >= 0)
                {
                    string selectedLang = UILanguageManager.SupportedLanguages[_targetLanguageComboBox.SelectedIndex];
                    OnTranslationSettingsChanged(selectedLang, _useAutoDetect);
                }

                Debug.WriteLine($"翻訳ボックス: 言語自動検出を {(_useAutoDetect ? "有効" : "無効")} にしました");
            });

            // イベントハンドラを登録
            _autoDetectCheckBox.CheckedChanged += _autoDetectCheckBoxHandler;

            // 閉じるボタン
            _closeButton = new Button
            {
                Text = "×",
                Size = new Size(25, 25),
                Location = new Point(_controlPanel.Width - 30, 5),
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.White,
                BackColor = Color.FromArgb(45, 45, 48),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Cursor = Cursors.Hand
            };

            _closeButton.FlatAppearance.BorderSize = 0;
            _closeButton.Click += (s, e) => this.Hide();

            // ツールチップを設定
            _toolTip.SetToolTip(_closeButton, "翻訳ウィンドウを閉じます");

            // コントロールパネルにコンポーネントを追加
            _controlPanel.Controls.Add(_targetLanguageComboBox);
            _controlPanel.Controls.Add(_autoDetectCheckBox);
            _controlPanel.Controls.Add(_closeButton);

            // テキストボックス
            _textBox = new RichTextBox
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                BackColor = Color.FromArgb(30, 30, 30),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.None,
                Padding = new Padding(10),
                ScrollBars = RichTextBoxScrollBars.Vertical
            };

            // LINESeedフォントが利用可能な場合は適用、そうでなければデフォルトフォント
            if (FontManager.Instance.IsJpFontAvailable || FontManager.Instance.IsEnFontAvailable)
            {
                _textBox.Font = FontManager.Instance.TranslationFont;
            }
            else
            {
                _textBox.Font = new Font("Yu Gothic UI", 12F, FontStyle.Regular);
            }

            // コンポーネントをフォームに追加
            this.Controls.Add(_textBox);
            this.Controls.Add(_controlPanel);

            // リサイズ可能にする
            this.ResizeRedraw = true;
            this.SetStyle(ControlStyles.ResizeRedraw, true);
        }

        /// <summary>
        /// 翻訳設定変更イベント発火メソッド
        /// </summary>
        protected virtual void OnTranslationSettingsChanged(string targetLanguage, bool autoDetect)
        {
            TranslationSettingsChanged?.Invoke(this, new TranslationSettingsChangedEventArgs(targetLanguage, autoDetect));
        }

        /// <summary>
        /// 言語自動検出モードを設定する
        /// </summary>
        /// <param name="useAutoDetect">自動検出を使用するかどうか</param>
        public void SetAutoDetect(bool useAutoDetect)
        {
            if (this.InvokeRequired)
            {
                this.BeginInvoke(new Action(() => SetAutoDetect(useAutoDetect)));
                return;
            }

            // イベントハンドラを一時的に解除
            _autoDetectCheckBox.CheckedChanged -= _autoDetectCheckBoxHandler;

            _autoDetectCheckBox.Checked = useAutoDetect;
            _useAutoDetect = useAutoDetect;
            _targetLanguageComboBox.Enabled = !useAutoDetect;

            // イベントハンドラを再登録
            _autoDetectCheckBox.CheckedChanged += _autoDetectCheckBoxHandler;

            Debug.WriteLine($"翻訳ボックス: 外部から言語自動検出を {(useAutoDetect ? "有効" : "無効")} に設定しました");
        }

        /// <summary>
        /// 翻訳先言語を設定する
        /// </summary>
        /// <param name="languageCode">言語コード</param>
        public void SetTargetLanguage(string languageCode)
        {
            if (this.InvokeRequired)
            {
                this.BeginInvoke(new Action(() => SetTargetLanguage(languageCode)));
                return;
            }

            int index = Array.IndexOf(UILanguageManager.SupportedLanguages, languageCode);
            if (index >= 0)
            {
                _targetLanguageComboBox.SelectedIndex = index;

                // 翻訳マネージャーに設定
                if (_translationManager != null)
                {
                    _translationManager.SetPreferredTargetLanguage(languageCode);
                }

                Debug.WriteLine($"翻訳ボックス: 外部から翻訳先言語を {languageCode} に設定しました");
            }
            else
            {
                Debug.WriteLine($"翻訳ボックス: 無効な言語コード {languageCode}");
            }
        }

        /// <summary>
        /// 翻訳テキストを設定する
        /// </summary>
        /// <param name="text">翻訳テキスト</param>
        public void SetTranslationText(string text)
        {
            if (this.InvokeRequired)
            {
                this.BeginInvoke(new Action(() => SetTranslationText(text)));
                return;
            }

            _textBox.Text = text;

            // テキストが日本語かどうかを判断し、言語設定を自動調整（必要に応じて）
            if (_useAutoDetect && !string.IsNullOrEmpty(text) && text.Length > 5)
            {
                string detectedLang = TranslationLanguageManager.DetectLanguage(text);

                // 検出された言語が現在選択されている言語と同じ場合は、別の言語に切り替える
                if (detectedLang == GetSelectedTargetLanguage())
                {
                    // 例: 検出された言語が英語で、選択されている言語も英語の場合、日本語に切り替える
                    string newTargetLang = (detectedLang == "en") ? "ja" : "en";
                    SetTargetLanguage(newTargetLang);
                    Debug.WriteLine($"翻訳テキストから言語を検出: {detectedLang}、選択言語と同じため表示言語を{newTargetLang}に変更しました");
                }

                // 言語に応じたフォントを適用
                if (detectedLang == "ja")
                {
                    // 日本語テキストには日本語フォントを適用
                    FontManager.Instance.ApplyTranslationFont(_textBox, TranslationLanguage.Japanese);
                }
                else
                {
                    // その他の言語（英語など）には英語フォントを適用
                    FontManager.Instance.ApplyTranslationFont(_textBox, TranslationLanguage.English);
                }
            }
        }

        /// <summary>
        /// 自動検出モードを使用しているかどうか
        /// </summary>
        /// <returns>自動検出モードの状態</returns>
        public bool IsUsingAutoDetect()
        {
            return _useAutoDetect;
        }

        /// <summary>
        /// 選択された翻訳先言語コードを取得する
        /// </summary>
        /// <returns>言語コード</returns>
        public string GetSelectedTargetLanguage()
        {
            if (_targetLanguageComboBox.SelectedIndex >= 0 &&
                _targetLanguageComboBox.SelectedIndex < UILanguageManager.SupportedLanguages.Length)
            {
                return UILanguageManager.SupportedLanguages[_targetLanguageComboBox.SelectedIndex];
            }
            return "ja"; // デフォルト
        }

        /// <summary>
        /// リサイズのためのWndProcオーバーライド
        /// </summary>
        protected override void WndProc(ref Message m)
        {
            // マウスがフォームの端にある場合、リサイズカーソルに変更
            const int WM_NCHITTEST = 0x84;
            const int HTLEFT = 10;
            const int HTRIGHT = 11;
            const int HTTOP = 12;
            const int HTTOPLEFT = 13;
            const int HTTOPRIGHT = 14;
            const int HTBOTTOM = 15;
            const int HTBOTTOMLEFT = 16;
            const int HTBOTTOMRIGHT = 17;

            if (m.Msg == WM_NCHITTEST)
            {
                Point cursor = this.PointToClient(Cursor.Position);
                int x = cursor.X;
                int y = cursor.Y;
                int w = this.Width;
                int h = this.Height;

                // 境界サイズの定義
                const int borderSize = 10;

                // コーナー検出
                if (x < borderSize && y < borderSize)
                {
                    m.Result = (IntPtr)HTTOPLEFT;
                    return;
                }
                else if (x >= w - borderSize && y < borderSize)
                {
                    m.Result = (IntPtr)HTTOPRIGHT;
                    return;
                }
                else if (x < borderSize && y >= h - borderSize)
                {
                    m.Result = (IntPtr)HTBOTTOMLEFT;
                    return;
                }
                else if (x >= w - borderSize && y >= h - borderSize)
                {
                    m.Result = (IntPtr)HTBOTTOMRIGHT;
                    return;
                }
                // 辺検出
                else if (x < borderSize)
                {
                    m.Result = (IntPtr)HTLEFT;
                    return;
                }
                else if (x >= w - borderSize)
                {
                    m.Result = (IntPtr)HTRIGHT;
                    return;
                }
                else if (y < borderSize)
                {
                    m.Result = (IntPtr)HTTOP;
                    return;
                }
                else if (y >= h - borderSize)
                {
                    m.Result = (IntPtr)HTBOTTOM;
                    return;
                }
            }

            base.WndProc(ref m);
        }

        /// <summary>
        /// キー入力の処理
        /// </summary>
        protected override void OnKeyDown(KeyEventArgs e)
        {
            // ESCキーで閉じる
            if (e.KeyCode == Keys.Escape)
            {
                this.Hide();
                e.Handled = true;
            }

            base.OnKeyDown(e);
        }

        /// <summary>
        /// フォームのサイズ変更時のイベント
        /// </summary>
        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);

            // コントロールパネルのサイズ調整
            if (_controlPanel != null)
            {
                _controlPanel.Width = this.Width;
                _closeButton.Location = new Point(_controlPanel.Width - 30, 5);
            }
        }
    }

    /// <summary>
    /// 翻訳設定変更イベント引数
    /// </summary>
    public class TranslationSettingsChangedEventArgs : EventArgs
    {
        public string TargetLanguage { get; private set; }
        public bool AutoDetect { get; private set; }

        public TranslationSettingsChangedEventArgs(string targetLanguage, bool autoDetect)
        {
            TargetLanguage = targetLanguage;
            AutoDetect = autoDetect;
        }
    }
}