using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Tesseract;

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
        private const int HOTKEY_ID_REGION_SELECT = 3;

        // メッセージ定数
        private const int WM_HOTKEY = 0x0312;

        // ウィンドウスタイル定数
        private const int GWL_EXSTYLE = -20;
        private const uint WS_EX_LAYERED = 0x80000;
        private const uint WS_EX_TRANSPARENT = 0x20;

        // 状態管理
        private bool isClickThrough = false;
        private bool isRegionSelectMode = false;

        // 選択範囲関連
        private Point? selectionStart = null;
        private Panel selectionBox = null;

        // OCR関連
        private TesseractEngine tessEngine;

        public Form1()
        {
            InitializeComponent();
            Debug.WriteLine("Form1 初期化開始");

            // 画面全体に表示
            this.WindowState = FormWindowState.Normal;
            this.FormBorderStyle = FormBorderStyle.None;

            // 画面のサイズを取得して設定
            Rectangle bounds = Screen.PrimaryScreen.Bounds;
            this.Bounds = bounds;
            Debug.WriteLine($"画面サイズ設定: Width={bounds.Width}, Height={bounds.Height}");

            // 確認用の半透明設定
            this.BackColor = Color.Red;
            this.Opacity = 0.5;

            // マウスイベントの登録
            this.MouseDown += Form1_MouseDown;
            this.MouseMove += Form1_MouseMove;
            this.MouseUp += Form1_MouseUp;

            // Tesseractエンジンの初期化
            try
            {
                string tessDataPath = Path.Combine(Application.StartupPath, "tessdata");
                Debug.WriteLine($"Tesseract初期化: データパス={tessDataPath}");
                tessEngine = new TesseractEngine(tessDataPath, "jpn", EngineMode.Default);
                Debug.WriteLine("Tesseract初期化成功");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Tesseract初期化エラー: {ex.Message}");
            }
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            Debug.WriteLine("Form1 ロード開始");

            try
            {
                // ホットキーを登録
                bool success = true;
                success &= RegisterHotKey(this.Handle, HOTKEY_ID_CLICK_THROUGH, MOD_CONTROL | MOD_SHIFT, (int)Keys.C);
                success &= RegisterHotKey(this.Handle, HOTKEY_ID_OVERLAY, MOD_CONTROL | MOD_SHIFT, (int)Keys.O);
                success &= RegisterHotKey(this.Handle, HOTKEY_ID_REGION_SELECT, MOD_CONTROL | MOD_SHIFT, (int)Keys.R);
                Debug.WriteLine($"ホットキー登録: {(success ? "成功" : "一部失敗")}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ホットキー登録エラー: {ex.Message}");
            }
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
                        Debug.WriteLine($"クリックスルー状態変更: {isClickThrough}");
                        break;

                    case HOTKEY_ID_OVERLAY:
                        this.Visible = !this.Visible;
                        Debug.WriteLine($"オーバーレイ表示状態変更: {this.Visible}");
                        break;

                    case HOTKEY_ID_REGION_SELECT:
                        isRegionSelectMode = !isRegionSelectMode;
                        this.Cursor = isRegionSelectMode ? Cursors.Cross : Cursors.Default;
                        Debug.WriteLine($"領域選択モード変更: {isRegionSelectMode}");
                        break;
                }
            }
        }

        private void Form1_MouseDown(object sender, MouseEventArgs e)
        {
            if (!isRegionSelectMode || e.Button != MouseButtons.Left) return;

            selectionStart = e.Location;
            Debug.WriteLine($"選択開始: X={e.Location.X}, Y={e.Location.Y}");

            // 選択範囲表示用のPanelを作成
            selectionBox = new Panel
            {
                BackColor = Color.FromArgb(50, 0, 120, 215), // 半透明の青
                BorderStyle = BorderStyle.FixedSingle,
                Location = e.Location,
                Size = new Size(0, 0)
            };

            this.Controls.Add(selectionBox);
            selectionBox.BringToFront();
        }

        private void Form1_MouseMove(object sender, MouseEventArgs e)
        {
            if (!isRegionSelectMode || selectionStart == null || selectionBox == null) return;

            // 選択範囲の更新
            int x = Math.Min(e.X, selectionStart.Value.X);
            int y = Math.Min(e.Y, selectionStart.Value.Y);
            int width = Math.Abs(e.X - selectionStart.Value.X);
            int height = Math.Abs(e.Y - selectionStart.Value.Y);

            selectionBox.Location = new Point(x, y);
            selectionBox.Size = new Size(width, height);
        }

        private void Form1_MouseUp(object sender, MouseEventArgs e)
        {
            if (!isRegionSelectMode || selectionStart == null || e.Button != MouseButtons.Left) return;

            Rectangle selectedRegion = new Rectangle(
                Math.Min(e.X, selectionStart.Value.X),
                Math.Min(e.Y, selectionStart.Value.Y),
                Math.Abs(e.X - selectionStart.Value.X),
                Math.Abs(e.Y - selectionStart.Value.Y)
            );

            Debug.WriteLine($"選択完了: X={selectedRegion.X}, Y={selectedRegion.Y}, Width={selectedRegion.Width}, Height={selectedRegion.Height}");

            if (selectedRegion.Width > 0 && selectedRegion.Height > 0)
            {
                Debug.WriteLine($"OCR処理開始: 領域サイズ = {selectedRegion.Width}x{selectedRegion.Height}");
                try
                {
                    // スクリーンショット取得
                    Debug.WriteLine("スクリーンショット取得開始");
                    using (Bitmap screenshot = new Bitmap(selectedRegion.Width, selectedRegion.Height))
                    {
                        Debug.WriteLine("Bitmap作成成功");
                        using (Graphics g = Graphics.FromImage(screenshot))
                        {
                            Debug.WriteLine("Graphics作成成功");
                            g.CopyFromScreen(
                                selectedRegion.X,
                                selectedRegion.Y,
                                0,
                                0,
                                selectedRegion.Size
                            );
                            Debug.WriteLine("画面のコピー成功");
                        }

                        // OCR処理
                        Debug.WriteLine("Tesseract処理開始");
                        if (tessEngine == null)
                        {
                            Debug.WriteLine("エラー: tessEngineがnullです");
                            return;
                        }

                        using (var page = tessEngine.Process(screenshot))
                        {
                            string text = page.GetText();
                            Debug.WriteLine($"OCR結果: {text}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"OCRエラーの詳細:");
                    Debug.WriteLine($"エラーメッセージ: {ex.Message}");
                    Debug.WriteLine($"スタックトレース: {ex.StackTrace}");
                }
            }

            // 選択範囲表示の後片付け
            if (selectionBox != null)
            {
                this.Controls.Remove(selectionBox);
                selectionBox.Dispose();
                selectionBox = null;
                Debug.WriteLine("選択範囲表示をクリア");
            }
            selectionStart = null;
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            Debug.WriteLine("アプリケーション終了処理開始");
            try
            {
                // ホットキーの登録を解除
                UnregisterHotKey(this.Handle, HOTKEY_ID_CLICK_THROUGH);
                UnregisterHotKey(this.Handle, HOTKEY_ID_OVERLAY);
                UnregisterHotKey(this.Handle, HOTKEY_ID_REGION_SELECT);
                tessEngine?.Dispose();  // Tesseractエンジンの解放
                Debug.WriteLine("終了処理完了");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"終了処理エラー: {ex.Message}");
            }
            base.OnFormClosing(e);
        }

        private void SetClickThrough(bool enabled)
        {
            try
            {
                uint exStyle = GetWindowLong(this.Handle, GWL_EXSTYLE);
                if (enabled)
                    exStyle |= WS_EX_TRANSPARENT;
                else
                    exStyle &= ~WS_EX_TRANSPARENT;
                SetWindowLong(this.Handle, GWL_EXSTYLE, exStyle);
                Debug.WriteLine($"クリックスルー設定変更: {enabled}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"クリックスルー設定エラー: {ex.Message}");
            }
        }
    }
}