﻿using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using GameTranslationOverlay.Core.OCR;
using GameTranslationOverlay.Forms;
using System.Threading.Tasks;
using System.Linq;
using System.IO;

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
        private MenuStrip _menuStrip;

        public MainForm()
        {
            Debug.WriteLine("MainForm: コンストラクタ開始");
            InitializeComponent();
            InitializeBenchmarkButton();  // 先にボタンを初期化
            InitializeMenu();            // その後でメニューを初期化
            InitializeServices();

            // 常に最前面に表示
            SetWindowPos(this.Handle, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE);

            Debug.WriteLine("MainForm: コンストラクタ完了");
        }

        private void InitializeMenu()
        {
            _menuStrip = new MenuStrip();
            var toolsMenu = new ToolStripMenuItem("Tools");

            var ocrTestMenuItem = new ToolStripMenuItem("OCR Test");
            var translationTestMenuItem = new ToolStripMenuItem("Translation Test");

            ocrTestMenuItem.Click += (s, e) =>
            {
                using (var testForm = new OcrTestForm())
                {
                    testForm.ShowDialog(this);
                }
            };

            translationTestMenuItem.Click += (s, e) =>
            {
                using (var testForm = new TranslationTestForm())
                {
                    testForm.ShowDialog(this);
                }
            };

            toolsMenu.DropDownItems.Add(ocrTestMenuItem);
            toolsMenu.DropDownItems.Add(translationTestMenuItem);
            _menuStrip.Items.Add(toolsMenu);
            MainMenuStrip = _menuStrip;
            Controls.Add(_menuStrip);

            // メニューの下にボタンを配置するための調整
            _benchmarkButton.Location = new Point(12, _menuStrip.Height + 12);
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

                var testScenarios = new[]
                {
                    "dialog_text",
                    "menu_text",
                    "system_message",
                    "battle_text",
                    "item_description"
                };

                foreach (var scenario in testScenarios)
                {
                    Debug.WriteLine($"\nTesting scenario: {scenario}");
                    var imagePath = Path.Combine("Core", "OCR", "Resources", "TestImages", $"{scenario}.png");

                    try
                    {
                        using (var image = new Bitmap(imagePath))
                        {
                            // 画像全体を対象とするRectangleを作成
                            var region = new Rectangle(0, 0, image.Width, image.Height);
                            var results = await OcrTest.RunTests(region);

                            // PaddleOCRの結果のみを表示
                            var paddleResults = results.Where(r => r.EngineName == "PaddleOCR");
                            foreach (var result in paddleResults)
                            {
                                Debug.WriteLine($"PaddleOCR - Time: {result.ProcessingTime}ms, Accuracy: {result.Accuracy:P2}");
                                Debug.WriteLine($"Recognized Text:\n{result.RecognizedText}\n");

                                if (result.AdditionalInfo?.Any() == true)
                                {
                                    foreach (var info in result.AdditionalInfo)
                                    {
                                        Debug.WriteLine($"{info.Key}: {info.Value}");
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error testing scenario {scenario}: {ex.Message}");
                        continue;
                    }
                }

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