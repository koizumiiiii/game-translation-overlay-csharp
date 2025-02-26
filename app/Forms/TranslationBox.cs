using System;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace GameTranslationOverlay.Forms
{
    public class TranslationBox : Form
    {
        // Win32 API定数
        private const int WM_NCHITTEST = 0x84;
        private const int WM_NCLBUTTONDOWN = 0xA1;
        private const int HTCLIENT = 0x1;
        private const int HTCAPTION = 0x2;
        private const int HTBOTTOMRIGHT = 0x11;
        private const int HTRIGHT = 0x6;
        private const int HTBOTTOM = 0x15;
        private const int HTLEFT = 0x7;
        private const int HTTOP = 0x8;
        private const int HTTOPLEFT = 0x13;
        private const int HTTOPRIGHT = 0x14;
        private const int HTBOTTOMLEFT = 0x10;
        private const int RESIZE_BORDER = 10; // リサイズ用の境界サイズ

        private RichTextBox contentBox;
        private Panel titleBar;
        private Label titleLabel;
        private Button closeButton;
        private bool isResizing = false;

        // Win32 API インポート
        [DllImport("user32.dll")]
        public static extern bool ReleaseCapture();

        [DllImport("user32.dll")]
        public static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);

        [DllImport("user32.dll")]
        static extern long GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        static extern IntPtr GetCapture();

        private const int GWL_STYLE = -16;

        public TranslationBox()
        {
            InitializeComponent();

            // フォームの初期設定
            this.FormBorderStyle = FormBorderStyle.None;
            this.BackColor = Color.FromArgb(40, 40, 40);
            this.ShowInTaskbar = false;
            this.TopMost = true;
            this.Size = new Size(350, 200);
            this.StartPosition = FormStartPosition.CenterScreen;

            // 透明度設定
            this.Opacity = 0.9;

            // デバッグ用のイベントハンドラ追加
            this.Load += TranslationBox_Load;
        }

        private void TranslationBox_Load(object sender, EventArgs e)
        {
            // ウィンドウスタイルのデバッグ
            DebugWindowStyles();
        }

        private void InitializeComponent()
        {
            // タイトルバー
            titleBar = new Panel
            {
                Dock = DockStyle.Top,
                Height = 30,
                BackColor = Color.FromArgb(60, 60, 60)
            };

            // タイトルラベル
            titleLabel = new Label
            {
                Text = "翻訳",
                ForeColor = Color.White,
                AutoSize = true,
                Location = new Point(10, 8)
            };

            // 閉じるボタン
            closeButton = new Button
            {
                Text = "×",
                FlatStyle = FlatStyle.Flat,
                Size = new Size(30, 30),
                Dock = DockStyle.Right,
                ForeColor = Color.White,
                BackColor = Color.FromArgb(60, 60, 60),
                FlatAppearance = { BorderSize = 0 }
            };

            // コンテンツボックス
            contentBox = new RichTextBox
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(30, 30, 30),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.None,
                Font = new Font("Yu Gothic UI", 12F, FontStyle.Regular),
                ReadOnly = true,
                Margin = new Padding(20),
                Padding = new Padding(10),
                ScrollBars = RichTextBoxScrollBars.Vertical
            };

            // イベントハンドラの設定
            titleBar.MouseDown += TitleBar_MouseDown;
            closeButton.Click += CloseButton_Click;
            this.MouseDown += TranslationBox_MouseDown;
            this.MouseMove += TranslationBox_MouseMove;
            this.MouseUp += TranslationBox_MouseUp;

            // コントロールの追加
            titleBar.Controls.Add(titleLabel);
            titleBar.Controls.Add(closeButton);
            this.Controls.Add(contentBox);
            this.Controls.Add(titleBar);
        }

        // 翻訳テキストを設定
        public void SetTranslationText(string text)
        {
            if (contentBox.InvokeRequired)
            {
                contentBox.BeginInvoke(new Action(() => contentBox.Text = text));
            }
            else
            {
                contentBox.Text = text;
            }
        }

        // タイトルバーのマウスダウンイベント - ウィンドウ移動のトリガー
        private void TitleBar_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                Debug.WriteLine("Title bar mouse down detected, attempting to move window");
                ReleaseCapture();
                SendMessage(this.Handle, WM_NCLBUTTONDOWN, HTCAPTION, 0);
            }
        }

        // 閉じるボタンクリック
        private void CloseButton_Click(object sender, EventArgs e)
        {
            this.Hide();
        }

        // リサイズ用のマウスイベント
        private void TranslationBox_MouseDown(object sender, MouseEventArgs e)
        {
            Debug.WriteLine($"MouseDown: {e.Button}, Location: {e.Location}, Time: {DateTime.Now.ToString("HH:mm:ss.fff")}");

            // マウスが右下のリサイズ領域にあるかチェック
            if (e.X >= this.Width - RESIZE_BORDER && e.Y >= this.Height - RESIZE_BORDER)
            {
                isResizing = true;  // リサイズモードをON
                this.Cursor = Cursors.SizeNWSE;
            }
        }

        private void TranslationBox_MouseMove(object sender, MouseEventArgs e)
        {
            // リサイズ中のカーソルを設定
            if (isResizing)
            {
                // リサイズ処理
                this.Width = Math.Max(100, e.X);
                this.Height = Math.Max(50, e.Y);
            }
            else if (e.X >= this.Width - RESIZE_BORDER && e.Y >= this.Height - RESIZE_BORDER)
            {
                this.Cursor = Cursors.SizeNWSE; // 右下
            }
            else if (e.X >= this.Width - RESIZE_BORDER)
            {
                this.Cursor = Cursors.SizeWE; // 右
            }
            else if (e.Y >= this.Height - RESIZE_BORDER)
            {
                this.Cursor = Cursors.SizeNS; // 下
            }
            else
            {
                this.Cursor = Cursors.Default;
            }
        }

        private void TranslationBox_MouseUp(object sender, MouseEventArgs e)
        {
            Debug.WriteLine("Mouse up detected");
            isResizing = false;  // リサイズモードをOFF
            this.Cursor = Cursors.Default;
            CheckMouseCapture();
        }

        // WndProcのオーバーライドでウィンドウメッセージを処理
        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);

            // NCHITTEST メッセージの処理
            if (m.Msg == WM_NCHITTEST && (int)m.Result == HTCLIENT)
            {
                Point screenPoint = new Point(m.LParam.ToInt32() & 0xFFFF, m.LParam.ToInt32() >> 16);
                Point clientPoint = this.PointToClient(screenPoint);

                // デバッグ出力
                // Debug.WriteLine($"Mouse position: {clientPoint.X}, {clientPoint.Y}");

                // リサイズ用のヒットテスト
                if (clientPoint.X <= RESIZE_BORDER)
                {
                    if (clientPoint.Y <= RESIZE_BORDER)
                        m.Result = (IntPtr)HTTOPLEFT;
                    else if (clientPoint.Y >= this.ClientSize.Height - RESIZE_BORDER)
                        m.Result = (IntPtr)HTBOTTOMLEFT;
                    else
                        m.Result = (IntPtr)HTLEFT;
                }
                else if (clientPoint.X >= this.ClientSize.Width - RESIZE_BORDER)
                {
                    if (clientPoint.Y <= RESIZE_BORDER)
                        m.Result = (IntPtr)HTTOPRIGHT;
                    else if (clientPoint.Y >= this.ClientSize.Height - RESIZE_BORDER)
                        m.Result = (IntPtr)HTBOTTOMRIGHT;
                    else
                        m.Result = (IntPtr)HTRIGHT;
                }
                else if (clientPoint.Y <= RESIZE_BORDER)
                {
                    m.Result = (IntPtr)HTTOP;
                }
                else if (clientPoint.Y >= this.ClientSize.Height - RESIZE_BORDER)
                {
                    m.Result = (IntPtr)HTBOTTOM;
                }
            }
        }

        // CreateParamsのオーバーライド - リサイズ可能なウィンドウスタイルを設定
        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.Style |= 0x00040000; // WS_SIZEBOX
                Debug.WriteLine($"CreateParams: Style={cp.Style.ToString("X8")}");
                return cp;
            }
        }

        // デバッグ用メソッド - ウィンドウスタイルの確認
        private void DebugWindowStyles()
        {
            long style = GetWindowLong(this.Handle, GWL_STYLE);
            Debug.WriteLine($"Window style: 0x{style.ToString("X8")}");
            // WS_SIZEBOX (0x00040000) が含まれているか確認
            Debug.WriteLine($"Has WS_SIZEBOX: {(style & 0x00040000) != 0}");
        }

        // デバッグ用メソッド - マウスキャプチャ状態の確認
        private void CheckMouseCapture()
        {
            IntPtr captureHandle = GetCapture();
            Debug.WriteLine($"Current capture: {captureHandle}, Form handle: {this.Handle}");
        }
    }
}