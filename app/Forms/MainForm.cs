using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using GameTranslationOverlay.Core.OCR;
using System.Threading.Tasks;

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
        private Button _benchmarkButton;

        public MainForm()
        {
            Debug.WriteLine("MainForm: コンストラクタ開始");
            InitializeComponent();
            InitializeServices();
            InitializeBenchmarkButton();

            // 常に最前面に表示
            SetWindowPos(this.Handle, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE);

            Debug.WriteLine("MainForm: コンストラクタ完了");
        }

        private void InitializeBenchmarkButton()
        {
            _benchmarkButton = new Button
            {
                Text = "Run OCR Benchmark",
                Location = new Point(12, 12),
                Size = new Size(120, 30)
            };
            _benchmarkButton.Click += async (sender, e) => await RunBenchmark();
            this.Controls.Add(_benchmarkButton);
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

        private async Task RunBenchmark()
        {
            try
            {
                _benchmarkButton.Enabled = false;
                _benchmarkButton.Text = "Running...";

                var test = new Core.OCR.Benchmark.OcrBenchmarkTest();
                await Task.Run(async () => await test.RunAllTests());

                MessageBox.Show(
                    "ベンチマーク完了。詳細はログを確認してください。",
                    "Benchmark Complete",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                );
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"ベンチマークエラー: {ex.Message}",
                    "Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
            }
            finally
            {
                _benchmarkButton.Enabled = true;
                _benchmarkButton.Text = "Run OCR Benchmark";
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