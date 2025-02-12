using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace GameTranslationOverlay
{
    public partial class Form1 : Form
    {
        // Win32 API宣言
        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, int fsModifiers, int vk);

        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        [DllImport("user32.dll", SetLastError = true)]
        static extern uint GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", SetLastError = true)]
        static extern int SetWindowLong(IntPtr hWnd, int nIndex, uint dwNewLong);

        // モディファイヤーキーの定義
        private const int MOD_CONTROL = 0x0002;
        private const int MOD_SHIFT = 0x0004;

        // ホットキーのID
        private const int HOTKEY_ID_CLICK_THROUGH = 1;
        private const int HOTKEY_ID_OVERLAY = 2;

        // メッセージ定数
        private const int WM_HOTKEY = 0x0312;

        // ウィンドウスタイル定数
        private const int GWL_EXSTYLE = -20;
        private const uint WS_EX_LAYERED = 0x80000;
        private const uint WS_EX_TRANSPARENT = 0x20;

        // クリックスルーの状態管理
        private bool isClickThrough = false;

        public Form1()
        {
            InitializeComponent();

            // 画面全体に表示
            this.WindowState = FormWindowState.Normal;
            this.FormBorderStyle = FormBorderStyle.None;

            // 画面のサイズを取得して設定
            Rectangle bounds = Screen.PrimaryScreen.Bounds;
            this.Bounds = bounds;

            // 確認用の半透明設定
            this.BackColor = Color.Red;
            this.Opacity = 0.5;
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            // ホットキーを登録 (Ctrl + Shift + C と Ctrl + Shift + O)
            RegisterHotKey(this.Handle, HOTKEY_ID_CLICK_THROUGH, MOD_CONTROL | MOD_SHIFT, (int)Keys.C);
            RegisterHotKey(this.Handle, HOTKEY_ID_OVERLAY, MOD_CONTROL | MOD_SHIFT, (int)Keys.O);
        }

        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);

            if (m.Msg == WM_HOTKEY)
            {
                switch (m.WParam.ToInt32())
                {
                    case HOTKEY_ID_CLICK_THROUGH:
                        isClickThrough = !isClickThrough;
                        this.Opacity = isClickThrough ? 0.1 : 0.5;
                        SetClickThrough(isClickThrough);
                        break;

                    case HOTKEY_ID_OVERLAY:
                        this.Visible = !this.Visible;
                        break;
                }
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            // ホットキーの登録を解除
            UnregisterHotKey(this.Handle, HOTKEY_ID_CLICK_THROUGH);
            UnregisterHotKey(this.Handle, HOTKEY_ID_OVERLAY);
            base.OnFormClosing(e);
        }

        private void SetClickThrough(bool enabled)
        {
            uint exStyle = GetWindowLong(this.Handle, GWL_EXSTYLE);
            if (enabled)
                exStyle |= WS_EX_TRANSPARENT;
            else
                exStyle &= ~WS_EX_TRANSPARENT;
            SetWindowLong(this.Handle, GWL_EXSTYLE, exStyle);
        }
    }
}