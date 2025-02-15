using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using GameTranslationOverlay.Core.OCR;

namespace GameTranslationOverlay
{
    public partial class MainForm : Form
    {
        // Win32 API
        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOSIZE = 0x0001;

        private OverlayForm _overlayForm;
        private TesseractOcrEngine _ocrEngine;

        public MainForm()
        {
            Debug.WriteLine("MainForm: コンストラクタ開始");
            InitializeComponent();
            InitializeServices();

            // 常に最前面に表示
            SetWindowPos(this.Handle, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE);

            Debug.WriteLine("MainForm: コンストラクタ完了");
        }

        private async void InitializeServices()
        {
            Debug.WriteLine("InitializeServices: 開始");
            try
            {
                _ocrEngine = new TesseractOcrEngine();
                Debug.WriteLine("InitializeServices: OCRエンジン作成");

                await _ocrEngine.InitializeAsync();
                Debug.WriteLine("InitializeServices: OCRエンジン初期化完了");

                _overlayForm = new OverlayForm(_ocrEngine);
                _overlayForm.Show();
                Debug.WriteLine("InitializeServices: オーバーレイフォーム作成完了");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"InitializeServices エラー: {ex.Message}");
                MessageBox.Show(
                    $"初期化エラー: {ex.Message}\nアプリケーションを終了します。",
                    "エラー",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
                Application.Exit();
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            Debug.WriteLine("OnFormClosing: 開始");
            try
            {
                if (_ocrEngine != null)
                {
                    _ocrEngine.Dispose();
                    Debug.WriteLine("OnFormClosing: OCRエンジンを破棄");
                }

                if (_overlayForm != null)
                {
                    _overlayForm.Dispose();
                    Debug.WriteLine("OnFormClosing: オーバーレイフォームを破棄");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"OnFormClosing エラー: {ex.Message}");
            }

            Debug.WriteLine("OnFormClosing: 終了");
            base.OnFormClosing(e);
        }
    }
}