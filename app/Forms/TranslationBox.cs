using System;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace GameTranslationOverlay.Forms
{
    public class TranslationBox : Form
    {
        private const int WS_EX_LAYERED = 0x80000;
        private const int WS_EX_TRANSPARENT = 0x20;
        private const int HTCAPTION = 2;
        private const int WM_NCHITTEST = 0x84;

        private TextBox translationTextBox;
        private bool isResizing = false;
        private Point lastMousePos;

        public TranslationBox()
        {
            InitializeComponents();
            SetWindowProperties();
        }

        private void InitializeComponents()
        {
            this.FormBorderStyle = FormBorderStyle.None;
            this.BackColor = Color.Black;
            this.Opacity = 0.85; // 透明度を調整
            this.ShowInTaskbar = false;
            this.TopMost = true;
            this.Size = new Size(350, 200);
            this.StartPosition = FormStartPosition.Manual;

            // テキストボックスの設定
            translationTextBox = new TextBox
            {
                Multiline = true,
                ReadOnly = true,
                Dock = DockStyle.Fill,
                BorderStyle = BorderStyle.None,
                BackColor = Color.Black,
                ForeColor = Color.White,
                Font = new Font("Meiryo UI", 12),
                Padding = new Padding(20),
                ScrollBars = ScrollBars.Vertical
            };

            this.Controls.Add(translationTextBox);

            // イベントハンドラの設定
            this.MouseDown += TranslationBox_MouseDown;
            this.MouseMove += TranslationBox_MouseMove;
            this.MouseUp += TranslationBox_MouseUp;

            // 閉じるボタンの追加
            Button closeButton = new Button
            {
                Text = "×",
                Size = new Size(25, 25),
                Location = new Point(this.Width - 30, 5),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.Transparent,
                ForeColor = Color.White,
                Cursor = Cursors.Hand,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };

            closeButton.FlatAppearance.BorderSize = 0;
            closeButton.Click += (s, e) => this.Hide();

            this.Controls.Add(closeButton);
        }

        private void SetWindowProperties()
        {
            // 常に最前面に表示
            this.TopMost = true;
        }

        public void DisplayTranslation(string text)
        {
            try
            {
                // UIスレッドでの実行を保証
                if (this.InvokeRequired)
                {
                    this.BeginInvoke(new Action(() => DisplayTranslation(text)));
                    return;
                }

                // 表示テキストの設定
                if (string.IsNullOrWhiteSpace(text))
                {
                    translationTextBox.Text = "翻訳結果がありません。";
                    Debug.WriteLine("Warning: Empty translation text");
                }
                else
                {
                    translationTextBox.Text = text;
                    Debug.WriteLine($"Translation displayed: {text.Substring(0, Math.Min(30, text.Length))}...");
                }

                // 表示状態と位置の調整
                if (!this.Visible)
                {
                    this.Show();
                    this.BringToFront();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error displaying translation: {ex.Message}");
            }
        }

        public void SetTranslationText(string text)
        {
            try
            {
                // UIスレッドでの実行を保証
                if (this.InvokeRequired)
                {
                    this.BeginInvoke(new Action(() => SetTranslationText(text)));
                    return;
                }

                // 表示テキストの設定
                if (string.IsNullOrWhiteSpace(text))
                {
                    translationTextBox.Text = "翻訳結果がありません。";
                    Debug.WriteLine("Warning: Empty translation text");
                }
                else
                {
                    translationTextBox.Text = text;
                    Debug.WriteLine($"Translation displayed: {text.Substring(0, Math.Min(30, text.Length))}...");
                }

                // 表示状態と位置の調整
                if (!this.Visible)
                {
                    this.Show();
                    this.BringToFront();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error displaying translation: {ex.Message}");
            }
        }

        // ウィンドウのドラッグ移動を可能にする
        private void TranslationBox_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                lastMousePos = e.Location;

                if (IsInResizeZone(e.Location))
                {
                    isResizing = true;
                    this.Cursor = Cursors.SizeNWSE;
                }
                else
                {
                    // ウィンドウのドラッグを開始
                    NativeMethods.ReleaseCapture();
                    NativeMethods.SendMessage(this.Handle, 0xA1, new IntPtr(2), IntPtr.Zero);
                }
            }
        }

        private void TranslationBox_MouseMove(object sender, MouseEventArgs e)
        {
            if (isResizing && e.Button == MouseButtons.Left)
            {
                // リサイズ処理
                int newWidth = this.Width + (e.X - lastMousePos.X);
                int newHeight = this.Height + (e.Y - lastMousePos.Y);

                if (newWidth > 150 && newHeight > 100)
                {
                    this.Size = new Size(newWidth, newHeight);
                    lastMousePos = e.Location;
                }
            }
            else if (IsInResizeZone(e.Location))
            {
                this.Cursor = Cursors.SizeNWSE;
            }
            else
            {
                this.Cursor = Cursors.Default;
            }
        }

        private void TranslationBox_MouseUp(object sender, MouseEventArgs e)
        {
            isResizing = false;
            this.Cursor = Cursors.Default;
        }

        private bool IsInResizeZone(Point p)
        {
            int resizeHandleSize = 10;
            return p.X > this.Width - resizeHandleSize && p.Y > this.Height - resizeHandleSize;
        }

        // ウィンドウメッセージの処理
        protected override void WndProc(ref Message m)
        {
            switch (m.Msg)
            {
                case WM_NCHITTEST:
                    base.WndProc(ref m);

                    if ((int)m.Result == 0x1)  // HTCLIENT
                    {
                        Point screenPoint = new Point(m.LParam.ToInt32());
                        Point clientPoint = this.PointToClient(screenPoint);

                        if (IsInResizeZone(clientPoint))
                        {
                            m.Result = new IntPtr(0x11); // HTBOTTOMRIGHT (右下隅リサイズ)
                            return;
                        }
                    }
                    return;
            }

            base.WndProc(ref m);
        }
    }

    public static class NativeMethods
    {
        [DllImport("user32.dll")]
        public static extern bool ReleaseCapture();

        [DllImport("user32.dll")]
        public static extern IntPtr SendMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);
    }
}