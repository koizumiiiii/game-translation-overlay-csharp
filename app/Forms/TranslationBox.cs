using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using GameTranslationOverlay.Core.Translation.Services;

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

        // 翻訳エンジン
        private TranslationManager _translationManager;

        // 自動検出フラグ
        private bool _useAutoDetect = true;

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
            foreach (string langCode in LanguageManager.SupportedLanguages)
            {
                _targetLanguageComboBox.Items.Add(LanguageManager.LanguageNames[langCode]);
            }

            // 初期言語を日本語に設定
            _targetLanguageComboBox.SelectedIndex = Array.IndexOf(LanguageManager.SupportedLanguages, "ja");

            // 言語選択変更イベント
            _targetLanguageComboBox.SelectedIndexChanged += (s, e) =>
            {
                if (_translationManager != null && _targetLanguageComboBox.SelectedIndex >= 0)
                {
                    string selectedLang = LanguageManager.SupportedLanguages[_targetLanguageComboBox.SelectedIndex];
                    _translationManager.SetPreferredTargetLanguage(selectedLang);
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

            _autoDetectCheckBox.CheckedChanged += (s, e) =>
            {
                _useAutoDetect = _autoDetectCheckBox.Checked;
                Debug.WriteLine($"翻訳ボックス: 言語自動検出を {(_useAutoDetect ? "有効" : "無効")} にしました");
            };

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
                Font = new Font("Yu Gothic UI", 12F, FontStyle.Regular),
                Padding = new Padding(10),
                ScrollBars = RichTextBoxScrollBars.Vertical
            };

            // コンポーネントをフォームに追加
            this.Controls.Add(_textBox);
            this.Controls.Add(_controlPanel);

            // リサイズ可能にする
            this.ResizeRedraw = true;
            this.SetStyle(ControlStyles.ResizeRedraw, true);
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
                _targetLanguageComboBox.SelectedIndex < LanguageManager.SupportedLanguages.Length)
            {
                return LanguageManager.SupportedLanguages[_targetLanguageComboBox.SelectedIndex];
            }
            return "en"; // デフォルト
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
}