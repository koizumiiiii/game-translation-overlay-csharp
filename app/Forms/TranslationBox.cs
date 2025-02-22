using System;
using System.Drawing;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.IO;
using System.Drawing.Imaging;
using GameTranslationOverlay.Core.Utils;

namespace GameTranslationOverlay.Forms
{
    public class TranslationBox : Panel, IDisposable
    {
        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOSIZE = 0x0001;

        private readonly Label _textLabel;
        private readonly Panel _highlightBox;  // 選択領域の可視化用
        private const int PADDING = 10;
        private const int MAX_WIDTH = 400;
        private readonly Timer _monitorTimer;
        private Bitmap _lastScreenshot;
        private string _lastRecognizedText;
        private readonly Rectangle _targetBounds;
        private bool _isMonitoring = false;
        private int _errorCount = 0;
        private const int MAX_ERROR_COUNT = 5;
        private DateTime _lastOcrTime = DateTime.MinValue;
        private const int MIN_OCR_INTERVAL_MS = 2000;

        public new event EventHandler<TextChangedEventArgs> TextChanged;
        public Rectangle TargetRegion => _targetBounds;
        public bool IsActive => _isMonitoring && !IsDisposed;

        public TranslationBox(Rectangle targetBounds, string text)
        {
            _targetBounds = targetBounds;

            // パネルの基本設定
            this.BackColor = Color.FromArgb(200, 0, 0, 0);
            this.Padding = new Padding(PADDING);
            this.BorderStyle = BorderStyle.None;

            // 選択領域のハイライトを作成
            _highlightBox = new Panel
            {
                BackColor = Color.FromArgb(30, 0, 120, 215),
                BorderStyle = BorderStyle.FixedSingle,
                Bounds = targetBounds
            };

            // テキストラベルの作成
            _textLabel = new Label
            {
                Text = text,
                ForeColor = Color.White,
                BackColor = Color.Transparent,
                Font = new Font("Yu Gothic UI", 9),
                AutoSize = true,
                MaximumSize = new Size(MAX_WIDTH - (PADDING * 2), 0),
                MinimumSize = new Size(100, 0)
            };

            // パネルにラベルを追加
            this.Controls.Add(_textLabel);
            _textLabel.Location = new Point(PADDING, PADDING);

            // パネルのサイズをラベルに合わせて設定
            this.Size = new Size(
                Math.Min(_textLabel.Width + (PADDING * 2), MAX_WIDTH),
                _textLabel.Height + (PADDING * 2)
            );

            // 位置を設定（選択領域の下に表示）
            this.Location = new Point(
                targetBounds.X,
                targetBounds.Y + targetBounds.Height + 5
            );

            // 監視タイマーの設定
            _monitorTimer = new Timer
            {
                Interval = 1000 // 初期間隔は1秒
            };
            _monitorTimer.Tick += MonitorTimer_Tick;
            _lastRecognizedText = text;

            // 作成時に自動的に監視を開始
            StartMonitoring();

            Debug.WriteLine($"TranslationBox created: Location={this.Location}, Size={this.Size}");
        }

        public void StartMonitoring()
        {
            if (!_isMonitoring)
            {
                _lastScreenshot = CaptureRegion();
                if (_lastScreenshot != null)
                {
                    _monitorTimer.Start();
                    _isMonitoring = true;
                    _errorCount = 0;
                    Debug.WriteLine("Monitoring started");
                }
                else
                {
                    Debug.WriteLine("Failed to start monitoring - initial capture failed");
                }
            }
        }

        public void StopMonitoring()
        {
            if (_isMonitoring)
            {
                _monitorTimer.Stop();
                _isMonitoring = false;
                _lastScreenshot?.Dispose();
                _lastScreenshot = null;
                _errorCount = 0;
                Debug.WriteLine("Monitoring stopped");
            }
        }

        private void MonitorTimer_Tick(object sender, EventArgs e)
        {
            if (!_isMonitoring || !IsHandleCreated || IsDisposed)
            {
                return;
            }

            try
            {
                var currentScreenshot = CaptureRegion();
                if (currentScreenshot == null)
                {
                    HandleError("Capture failed");
                    return;
                }

                using (currentScreenshot)
                {
                    var hasChange = TextDetectionUtil.HasSignificantChange(
                        _lastScreenshot,
                        currentScreenshot,
                        _lastRecognizedText,
                        _lastRecognizedText
                    );

                    if (hasChange)
                    {
                        Debug.WriteLine("Text change detected");
                        _lastScreenshot?.Dispose();
                        _lastScreenshot = (Bitmap)currentScreenshot.Clone();

                        if ((DateTime.Now - _lastOcrTime).TotalMilliseconds >= MIN_OCR_INTERVAL_MS)
                        {
                            TextChanged?.Invoke(this, new TextChangedEventArgs(_targetBounds));
                            _lastOcrTime = DateTime.Now;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                HandleError($"MonitorTimer_Tick error: {ex.Message}");
                if (ex is AccessViolationException)
                {
                    Debug.WriteLine("Access violation detected - stopping monitoring");
                    StopMonitoring();
                }
            }
        }

        private void HandleError(string message)
        {
            Debug.WriteLine(message);
            _errorCount++;

            if (_errorCount >= MAX_ERROR_COUNT)
            {
                Debug.WriteLine($"Too many errors ({_errorCount}), stopping monitoring");
                StopMonitoring();
            }
            else
            {
                // エラー発生時は監視間隔を一時的に延長
                _monitorTimer.Interval = 2000;
            }
        }

        private Bitmap CaptureRegion()
        {
            try
            {
                if (_targetBounds.Width <= 0 || _targetBounds.Height <= 0)
                {
                    Debug.WriteLine("Invalid region dimensions");
                    return null;
                }

                var screenBounds = Screen.PrimaryScreen.Bounds;
                if (!screenBounds.Contains(_targetBounds))
                {
                    Debug.WriteLine("Region is outside screen bounds");
                    return null;
                }

                var bitmap = new Bitmap(_targetBounds.Width, _targetBounds.Height);
                using (var g = Graphics.FromImage(bitmap))
                {
                    g.CopyFromScreen(_targetBounds.Location, Point.Empty, _targetBounds.Size);
                }
                return bitmap;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Screen capture error: {ex.Message}");
                return null;
            }
        }

        public void UpdateText(string newText)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action(() => UpdateText(newText)));
                return;
            }

            _lastRecognizedText = newText;
            _textLabel.Text = newText;
            this.Size = new Size(
                Math.Min(_textLabel.Width + (PADDING * 2), MAX_WIDTH),
                _textLabel.Height + (PADDING * 2)
            );
        }

        public void RemoveHighlight()
        {
            if (_highlightBox != null && !_highlightBox.IsDisposed)
            {
                _highlightBox.Dispose();
            }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            SetWindowPos(this.Handle, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE);
            _highlightBox?.BringToFront();
        }

        protected override void OnVisibleChanged(EventArgs e)
        {
            base.OnVisibleChanged(e);
            if (_highlightBox != null)
            {
                _highlightBox.Visible = this.Visible;
            }
            if (!Visible && _isMonitoring)
            {
                StopMonitoring();
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                StopMonitoring();
                _monitorTimer?.Dispose();
                _textLabel?.Dispose();
                _highlightBox?.Dispose();
                _lastScreenshot?.Dispose();
            }
            base.Dispose(disposing);
        }
    }

    public class TextChangedEventArgs : EventArgs
    {
        public Rectangle Region { get; }

        public TextChangedEventArgs(Rectangle region)
        {
            Region = region;
        }
    }
}