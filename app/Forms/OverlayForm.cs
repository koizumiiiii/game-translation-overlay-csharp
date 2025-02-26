using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using GameTranslationOverlay.Core.OCR;
using GameTranslationOverlay.Core.Region;
using GameTranslationOverlay.Core.Translation;
using GameTranslationOverlay.Core.Translation.Interfaces;
using GameTranslationOverlay.Core.Translation.Services;
using GameTranslationOverlay.Utils;

namespace GameTranslationOverlay.Forms
{
    public partial class OverlayForm : Form
    {
        // 領域選択関連
        private bool isRegionSelectMode = false;
        private Point startPoint;
        private Rectangle selectionRectangle;
        private bool isMouseDown = false;
        private Pen selectionPen = new Pen(Color.Red, 2);
        private Timer textChangeDetectionTimer;
        private string lastRecognizedText = string.Empty;
        private const int TEXT_CHECK_INTERVAL = 1000; // テキスト変更チェック間隔（ミリ秒）

        // OCR関連
        private IOcrEngine ocrEngine;

        // 翻訳関連
        private ITranslationEngine translationEngine;

        // 翻訳表示用ウィンドウ
        private TranslationBox translationBox = null;

        // クリックスルーの状態
        private bool isClickThrough = true;

        // ホットキーID定数
        private const int HOTKEY_TOGGLE_OVERLAY = 9001;    // Ctrl+Shift+O
        private const int HOTKEY_CLEAR_REGION = 9002;      // Ctrl+Shift+C
        private const int HOTKEY_TOGGLE_REGION_SELECT = 9003; // Ctrl+Shift+R

        // ウィンドウ位置変更のための定数とAPI
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOZORDER = 0x0004;
        private const uint SWP_FRAMECHANGED = 0x0020;

        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y,
            int cx, int cy, uint uFlags);

        // マウスフック関連
        private IntPtr mouseHookID = IntPtr.Zero;
        private MouseHookCallback mouseHookCallback;
        private bool hookInstalled = false;

        public delegate IntPtr MouseHookCallback(int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, MouseHookCallback lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        private const int WH_MOUSE_LL = 14;
        private const int WM_LBUTTONDOWN = 0x0201;
        private const int WM_LBUTTONUP = 0x0202;
        private const int WM_MOUSEMOVE = 0x0200;

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int x;
            public int y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MSLLHOOKSTRUCT
        {
            public POINT pt;
            public uint mouseData;
            public uint flags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        // MainFormから渡されるOCRエンジンを受け取るコンストラクタ
        public OverlayForm(IOcrEngine ocrEngine)
        {
            InitializeComponent();

            // 受け取ったOCRエンジンをフィールドに保存
            this.ocrEngine = ocrEngine;

            // フォームの初期設定
            this.FormBorderStyle = FormBorderStyle.None;
            this.WindowState = FormWindowState.Maximized;
            this.TopMost = true;
            this.BackColor = Color.White;
            this.TransparencyKey = Color.White;
            this.ShowInTaskbar = false;

            // マウスフックコールバックの初期化
            mouseHookCallback = new MouseHookCallback(MouseHookProc);

            // クリックスルーを有効化（デフォルト）
            SetClickThrough(true);

            // テキスト変更検出タイマーの初期化
            textChangeDetectionTimer = new Timer
            {
                Interval = TEXT_CHECK_INTERVAL,
                Enabled = false
            };
            textChangeDetectionTimer.Tick += TextChangeDetectionTimer_Tick;

            // 翻訳エンジンの初期化
            try
            {
                translationEngine = new LibreTranslateEngine();
                Debug.WriteLine("Translation engine created successfully");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error creating translation engine: {ex.Message}");
            }

            // ホットキーの登録
            RegisterHotkeys();
        }

        // マウスフック処理
        private IntPtr MouseHookProc(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && isRegionSelectMode)
            {
                MSLLHOOKSTRUCT hookStruct = (MSLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(MSLLHOOKSTRUCT));
                Point screenPoint = new Point(hookStruct.pt.x, hookStruct.pt.y);
                Point clientPoint = this.PointToClient(screenPoint);

                if (wParam.ToInt32() == WM_LBUTTONDOWN)
                {
                    Debug.WriteLine($"Mouse hook: left button down at screen:{screenPoint}, client:{clientPoint}");
                    this.BeginInvoke(new Action(() =>
                    {
                        startPoint = clientPoint;
                        isMouseDown = true;
                        selectionRectangle = Rectangle.Empty;
                        this.Invalidate();
                    }));
                }
                else if (wParam.ToInt32() == WM_MOUSEMOVE && isMouseDown)
                {
                    // マウス移動イベントは多すぎるのでデバッグ出力は控える
                    this.BeginInvoke(new Action(() =>
                    {
                        // 選択範囲の更新
                        int x = Math.Min(startPoint.X, clientPoint.X);
                        int y = Math.Min(startPoint.Y, clientPoint.Y);
                        int width = Math.Abs(clientPoint.X - startPoint.X);
                        int height = Math.Abs(clientPoint.Y - startPoint.Y);

                        selectionRectangle = new Rectangle(x, y, width, height);
                        this.Invalidate();
                    }));
                }
                else if (wParam.ToInt32() == WM_LBUTTONUP && isMouseDown)
                {
                    Debug.WriteLine($"Mouse hook: left button up at screen:{screenPoint}, client:{clientPoint}");
                    this.BeginInvoke(new Action(() =>
                    {
                        isMouseDown = false;

                        if (selectionRectangle.Width > 10 && selectionRectangle.Height > 10)
                        {
                            Debug.WriteLine($"Selection completed via hook: {selectionRectangle}");
                            ProcessSelectedRegion();
                            ToggleRegionSelectMode();
                        }
                        else
                        {
                            selectionRectangle = Rectangle.Empty;
                            this.Invalidate();
                            Debug.WriteLine("Selection too small, cleared");
                        }
                    }));
                }
            }

            return CallNextHookEx(mouseHookID, nCode, wParam, lParam);
        }

        // マウスフックのインストール
        private void InstallMouseHook()
        {
            if (!hookInstalled)
            {
                using (Process curProcess = Process.GetCurrentProcess())
                using (ProcessModule curModule = curProcess.MainModule)
                {
                    mouseHookID = SetWindowsHookEx(WH_MOUSE_LL, mouseHookCallback,
                        GetModuleHandle(curModule.ModuleName), 0);
                }

                if (mouseHookID != IntPtr.Zero)
                {
                    hookInstalled = true;
                    Debug.WriteLine("Mouse hook installed successfully");
                }
                else
                {
                    int errorCode = Marshal.GetLastWin32Error();
                    Debug.WriteLine($"Failed to install mouse hook. Error code: {errorCode}");
                }
            }
        }

        // マウスフックのアンインストール
        private void UninstallMouseHook()
        {
            if (hookInstalled && mouseHookID != IntPtr.Zero)
            {
                UnhookWindowsHookEx(mouseHookID);
                mouseHookID = IntPtr.Zero;
                hookInstalled = false;
                Debug.WriteLine("Mouse hook uninstalled");
            }
        }

        // テキスト変更検出タイマーのイベントハンドラ
        private async void TextChangeDetectionTimer_Tick(object sender, EventArgs e)
        {
            if (selectionRectangle.IsEmpty)
            {
                textChangeDetectionTimer.Stop();
                return;
            }

            try
            {
                // OCR処理
                string currentText = string.Empty;
                if (ocrEngine != null)
                {
                    currentText = await ocrEngine.RecognizeTextAsync(selectionRectangle);
                }
                else
                {
                    return;
                }

                // テキストに変更がある場合のみ翻訳を実行
                if (currentText != lastRecognizedText && !string.IsNullOrWhiteSpace(currentText))
                {
                    Debug.WriteLine($"Text change detected: {currentText}");
                    lastRecognizedText = currentText;

                    // 翻訳処理
                    string translatedText = string.Empty;
                    if (translationEngine != null)
                    {
                        try
                        {
                            translatedText = await translationEngine.TranslateAsync(currentText, "ja", "en");
                        }
                        catch (Exception ex)
                        {
                            translatedText = $"Translation error: {ex.Message}";
                        }
                    }
                    else
                    {
                        translatedText = $"No translation engine available. Text detected: {currentText}";
                    }

                    ShowTranslation(translatedText);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in text detection: {ex.Message}");
            }
        }

        // ホットキーの登録
        private void RegisterHotkeys()
        {
            try
            {
                // まず既存のホットキーを解除
                UnregisterHotkeys();

                // オーバーレイの表示/非表示 (Ctrl+Shift+O)
                bool success1 = WindowsAPI.RegisterHotKey(
                    this.Handle,
                    HOTKEY_TOGGLE_OVERLAY,
                    WindowsAPI.MOD_CONTROL | WindowsAPI.MOD_SHIFT,
                    (int)Keys.O);
                Debug.WriteLine($"Registering Ctrl+Shift+O hotkey: {(success1 ? "Success" : "Failed")}");

                // 選択領域のクリア (Ctrl+Shift+C)
                bool success2 = WindowsAPI.RegisterHotKey(
                    this.Handle,
                    HOTKEY_CLEAR_REGION,
                    WindowsAPI.MOD_CONTROL | WindowsAPI.MOD_SHIFT,
                    (int)Keys.C);
                Debug.WriteLine($"Registering Ctrl+Shift+C hotkey: {(success2 ? "Success" : "Failed")}");

                // 領域選択モードの切り替え (Ctrl+Shift+R)
                bool success3 = WindowsAPI.RegisterHotKey(
                    this.Handle,
                    HOTKEY_TOGGLE_REGION_SELECT,
                    WindowsAPI.MOD_CONTROL | WindowsAPI.MOD_SHIFT,
                    (int)Keys.R);
                Debug.WriteLine($"Registering Ctrl+Shift+R hotkey: {(success3 ? "Success" : "Failed")}");

                // ホットキー登録に失敗した場合のエラーを確認
                if (!success1 || !success2 || !success3)
                {
                    int error = Marshal.GetLastWin32Error();
                    Debug.WriteLine($"Hotkey registration failed with error code: {error}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error registering hotkeys: {ex.Message}");
            }
        }

        // ホットキーの登録解除
        private void UnregisterHotkeys()
        {
            try
            {
                WindowsAPI.UnregisterHotKey(this.Handle, HOTKEY_TOGGLE_OVERLAY);
                WindowsAPI.UnregisterHotKey(this.Handle, HOTKEY_CLEAR_REGION);
                WindowsAPI.UnregisterHotKey(this.Handle, HOTKEY_TOGGLE_REGION_SELECT);
                Debug.WriteLine("All hotkeys unregistered");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error unregistering hotkeys: {ex.Message}");
            }
        }

        // クリックスルーの設定
        private void SetClickThrough(bool enable)
        {
            if (enable)
            {
                // クリックスルーを有効化（WS_EX_TRANSPARENT）
                long exStyle = WindowsAPI.GetWindowLong(this.Handle, WindowsAPI.GWL_EXSTYLE);
                WindowsAPI.SetWindowLong(this.Handle, WindowsAPI.GWL_EXSTYLE, exStyle | 0x00000020);

                // スタイル変更を強制適用
                SetWindowPos(this.Handle, IntPtr.Zero, 0, 0, 0, 0,
                    SWP_NOMOVE | SWP_NOSIZE | SWP_NOZORDER | SWP_FRAMECHANGED);

                isClickThrough = true;
                Debug.WriteLine("Click-through enabled");
            }
            else
            {
                // クリックスルーを無効化
                long exStyle = WindowsAPI.GetWindowLong(this.Handle, WindowsAPI.GWL_EXSTYLE);
                WindowsAPI.SetWindowLong(this.Handle, WindowsAPI.GWL_EXSTYLE, exStyle & ~0x00000020);

                // スタイル変更を強制適用
                SetWindowPos(this.Handle, IntPtr.Zero, 0, 0, 0, 0,
                    SWP_NOMOVE | SWP_NOSIZE | SWP_NOZORDER | SWP_FRAMECHANGED);

                isClickThrough = false;
                Debug.WriteLine("Click-through disabled");
            }
        }

        // 領域選択モードの切り替え
        private void ToggleRegionSelectMode()
        {
            isRegionSelectMode = !isRegionSelectMode;

            if (isRegionSelectMode)
            {
                // 領域選択モードを有効にする
                Debug.WriteLine("Enabling region select mode");

                // クリックスルーを無効化
                SetClickThrough(false);

                // マウスフックをインストール
                InstallMouseHook();

                // フォームを確実にアクティブ化
                this.Activate();
                this.BringToFront();
                Application.DoEvents();

                // カーソル設定
                Cursor.Current = Cursors.Cross;

                Debug.WriteLine("Region select mode enabled");
            }
            else
            {
                // 領域選択モードを無効にする
                Debug.WriteLine("Disabling region select mode");

                // マウスフックをアンインストール
                UninstallMouseHook();

                // クリックスルーを有効化
                SetClickThrough(true);

                // カーソル設定
                Cursor.Current = Cursors.Default;

                Debug.WriteLine("Region select mode disabled");
            }
        }

        // 選択領域のクリア
        private void ClearSelectedRegion()
        {
            Debug.WriteLine("ClearSelectedRegion called");
            textChangeDetectionTimer.Stop();
            selectionRectangle = Rectangle.Empty;
            lastRecognizedText = string.Empty;
            this.Invalidate();
            Debug.WriteLine("Selected region cleared");
        }

        // 翻訳ウィンドウの表示/非表示
        private void ToggleTranslationBox()
        {
            Debug.WriteLine("ToggleTranslationBox called");
            if (translationBox != null && !translationBox.IsDisposed)
            {
                translationBox.Visible = !translationBox.Visible;
                Debug.WriteLine($"Translation box visibility toggled: {translationBox.Visible}");
            }
            else
            {
                Debug.WriteLine("Translation box not available");
            }
        }

        // 翻訳テキストの表示
        public void ShowTranslation(string translatedText)
        {
            if (translationBox == null || translationBox.IsDisposed)
            {
                translationBox = new TranslationBox();
                Debug.WriteLine("New translation box created");
            }

            translationBox.SetTranslationText(translatedText);

            if (!translationBox.Visible)
            {
                translationBox.Show();
                Debug.WriteLine("Translation box shown");
            }
        }

        // 選択領域からOCRを実行
        private async void ProcessSelectedRegion()
        {
            if (!selectionRectangle.IsEmpty)
            {
                Debug.WriteLine($"Processing region: {selectionRectangle}");
                try
                {
                    // OCR処理
                    string recognizedText = string.Empty;
                    if (ocrEngine != null)
                    {
                        recognizedText = await ocrEngine.RecognizeTextAsync(selectionRectangle);
                        lastRecognizedText = recognizedText;
                        Debug.WriteLine($"OCR result: {recognizedText}");
                    }
                    else
                    {
                        recognizedText = "OCRエンジンが利用できません。";
                        Debug.WriteLine("OCR engine not available");
                    }

                    // 翻訳処理
                    string translatedText = string.Empty;
                    if (translationEngine != null)
                    {
                        try
                        {
                            translatedText = await translationEngine.TranslateAsync(recognizedText, "ja", "en");
                            Debug.WriteLine($"Translation result: {translatedText}");
                        }
                        catch (Exception ex)
                        {
                            translatedText = $"Translation error: {ex.Message}";
                            Debug.WriteLine($"Translation error: {ex.Message}");
                        }
                    }
                    else
                    {
                        translatedText = $"Translation engine not available. Text detected: {recognizedText}";
                        Debug.WriteLine("Translation engine not available");
                    }

                    // 翻訳結果の表示
                    ShowTranslation(translatedText);

                    // テキスト変更検出を開始
                    textChangeDetectionTimer.Start();
                    Debug.WriteLine("Text change detection timer started");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error processing region: {ex.Message}");
                }
            }
            else
            {
                Debug.WriteLine("Region is empty, not processing");
            }
        }

        // ウィンドウメッセージの処理
        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);

            // ホットキーメッセージの処理
            if (m.Msg == 0x0312) // WM_HOTKEY
            {
                int id = m.WParam.ToInt32();
                Debug.WriteLine($"Hotkey message received: ID={id}");

                switch (id)
                {
                    case HOTKEY_TOGGLE_OVERLAY: // Ctrl+Shift+O
                        Debug.WriteLine("Processing HOTKEY_TOGGLE_OVERLAY");
                        ToggleTranslationBox();
                        break;

                    case HOTKEY_CLEAR_REGION: // Ctrl+Shift+C
                        Debug.WriteLine("Processing HOTKEY_CLEAR_REGION");
                        ClearSelectedRegion();
                        break;

                    case HOTKEY_TOGGLE_REGION_SELECT: // Ctrl+Shift+R
                        Debug.WriteLine("Processing HOTKEY_TOGGLE_REGION_SELECT");
                        ToggleRegionSelectMode();
                        break;

                    default:
                        Debug.WriteLine($"Unknown hotkey ID: {id}");
                        break;
                }
            }
        }

        // 描画処理
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            if (!selectionRectangle.IsEmpty)
            {
                Debug.WriteLine($"Painting selection rectangle: {selectionRectangle}");
                e.Graphics.DrawRectangle(selectionPen, selectionRectangle);
                using (SolidBrush brush = new SolidBrush(Color.FromArgb(50, 255, 0, 0)))
                {
                    e.Graphics.FillRectangle(brush, selectionRectangle);
                }
            }
        }

        // フォーム表示時の処理
        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            Debug.WriteLine("Form shown, ensuring full screen display");

            // フォームが実際に表示されたら全画面表示を確実にする
            this.WindowState = FormWindowState.Maximized;
            this.TopMost = true;

            // 再描画を強制
            this.Invalidate();
        }

        // フォーム終了時の処理
        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            base.OnFormClosed(e);

            // マウスフックをアンインストール
            UninstallMouseHook();

            if (textChangeDetectionTimer != null)
            {
                textChangeDetectionTimer.Stop();
                textChangeDetectionTimer.Dispose();
            }

            selectionPen.Dispose();

            if (translationBox != null && !translationBox.IsDisposed)
            {
                translationBox.Dispose();
            }

            try
            {
                UnregisterHotkeys();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error unregistering hotkeys on form close: {ex.Message}");
            }
        }
    }
}