using System;
using System.Drawing;
using System.Windows.Forms;

namespace GameTranslationOverlay.Forms
{
    /// <summary>
    /// 長文テキストを表示するためのシンプルなフォーム
    /// 主に開発者向け診断情報の表示に使用
    /// </summary>
    public class TextDisplayForm : Form
    {
        private TextBox _textBox;
        private Button _closeButton;
        private Button _copyButton;

        /// <summary>
        /// コンストラクタ
        /// </summary>
        /// <param name="title">フォームのタイトル</param>
        /// <param name="text">表示するテキスト内容</param>
        public TextDisplayForm(string title, string text)
        {
            this.Text = title;
            this.Size = new Size(600, 500);
            this.StartPosition = FormStartPosition.CenterParent;

            InitializeComponents();
            _textBox.Text = text ?? "";
        }

        /// <summary>
        /// コンポーネントの初期化
        /// </summary>
        private void InitializeComponents()
        {
            // テキストボックス
            _textBox = new TextBox
            {
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Both,
                WordWrap = false,
                Font = new Font("Consolas", 9),
                Dock = DockStyle.Fill
            };

            // コピーボタン
            _copyButton = new Button
            {
                Text = "内容をコピー",
                Dock = DockStyle.Bottom,
                Height = 30
            };
            _copyButton.Click += (s, e) => 
            {
                if (!string.IsNullOrEmpty(_textBox.Text))
                {
                    Clipboard.SetText(_textBox.Text);
                    MessageBox.Show("テキストをクリップボードにコピーしました。", "コピー完了", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            };

            // 閉じるボタン
            _closeButton = new Button
            {
                Text = "閉じる",
                Dock = DockStyle.Bottom,
                Height = 30
            };
            _closeButton.Click += (s, e) => this.Close();

            // パネルにコントロールを追加
            Panel buttonPanel = new Panel
            {
                Height = 40,
                Dock = DockStyle.Bottom
            };

            _copyButton.Dock = DockStyle.Left;
            _closeButton.Dock = DockStyle.Right;
            _copyButton.Width = 150;
            _closeButton.Width = 100;

            buttonPanel.Controls.Add(_copyButton);
            buttonPanel.Controls.Add(_closeButton);

            // フォームにコントロールを追加
            this.Controls.Add(_textBox);
            this.Controls.Add(buttonPanel);

            // ESCキーでフォームを閉じる
            this.KeyPreview = true;
            this.KeyDown += (s, e) => 
            {
                if (e.KeyCode == Keys.Escape)
                {
                    this.Close();
                }
            };
        }
    }
} 