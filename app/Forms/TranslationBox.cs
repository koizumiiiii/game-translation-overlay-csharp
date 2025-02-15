using System;
using System.Drawing;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.Diagnostics;

namespace GameTranslationOverlay.Forms
{
    public class TranslationBox : Panel
    {
        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOSIZE = 0x0001;

        private readonly Label _textLabel;
        private const int PADDING = 10;

        public TranslationBox(Rectangle targetBounds, string text)
        {
            // パネルの基本設定
            this.BackColor = Color.FromArgb(200, 0, 0, 0);  // 半透明の黒
            this.Padding = new Padding(PADDING);
            this.BorderStyle = BorderStyle.None;

            // テキストラベルの作成
            _textLabel = new Label
            {
                Text = text,
                ForeColor = Color.White,
                BackColor = Color.Transparent,
                Font = new Font("Yu Gothic UI", 9),
                AutoSize = true,
                MaximumSize = new Size(400, 0), // 最大幅を制限
                MinimumSize = new Size(100, 0)  // 最小幅を設定
            };

            // パネルにラベルを追加
            this.Controls.Add(_textLabel);

            // ラベルを中央に配置
            _textLabel.Location = new Point(PADDING, PADDING);

            // パネルのサイズをラベルに合わせて設定
            this.Size = new Size(
                _textLabel.Width + (PADDING * 2),
                _textLabel.Height + (PADDING * 2)
            );

            // 位置を設定（選択領域の下に表示）
            this.Location = new Point(
                targetBounds.X,
                targetBounds.Y + targetBounds.Height + 5
            );

            Debug.WriteLine($"TranslationBox created: Text={text}, Size={this.Size}, Location={this.Location}");
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            // 最前面に表示
            SetWindowPos(this.Handle, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            // 必要に応じて追加の描画処理
        }
    }
}