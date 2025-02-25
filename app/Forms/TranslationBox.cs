using System;
using System.Drawing;
using System.Windows.Forms;
using System.Threading.Tasks;
using System.Diagnostics;
using GameTranslationOverlay.Core.Utils;

namespace GameTranslationOverlay.Forms
{
    public class TranslationBox : RichTextBox
    {
        private const int MONITORING_INTERVAL = 500; // ミリ秒
        private const int MIN_INTERVAL_BETWEEN_CHANGES = 1000; // ミリ秒
        private const int MAX_CONSECUTIVE_ERRORS = 3;

        private readonly Rectangle _targetRegion;
        private Timer _monitoringTimer;
        private Bitmap _lastImage;
        private string _lastText;
        private bool _isMonitoring;
        private DateTime _lastChangeTime;
        private int _consecutiveErrors;
        private bool _isDisposed;

        public event EventHandler<TextChangeEventArgs> TextChangeDetected;

        public TranslationBox(Rectangle region) : base()
        {
            _targetRegion = region;
            InitializeBox();
            InitializeMonitoring();
        }

        private void InitializeBox()
        {
            SetStyle(ControlStyles.SupportsTransparentBackColor, true);

            // 背景をより暗く、透明度を調整
            BackColor = Color.FromArgb(220, 10, 10, 10); // より暗い背景
            ForeColor = Color.White; // 白いテキスト

            // フォントを大きくして可読性を向上
            Font = new Font("Yu Gothic UI", 12F, FontStyle.Regular);

            BorderStyle = BorderStyle.None;

            // ウィンドウサイズを大きくする
            // 位置はコンテナで決定されるので、右側に表示
            Location = new Point(_targetRegion.Right + 20, _targetRegion.Top);
            Size = new Size(350, 200); // より大きなサイズ

            ReadOnly = true;
            Multiline = true;
            WordWrap = true;
            DetectUrls = false;

            // スクロールバーの表示設定
            ScrollBars = RichTextBoxScrollBars.Vertical;

            // 内部の余白を大きくする
            Margin = new Padding(20);

            Debug.WriteLine($"TranslationBox created: Location={Location}, Size={Size}");
        }

        private void InitializeMonitoring()
        {
            _lastChangeTime = DateTime.Now;
            _consecutiveErrors = 0;

            _monitoringTimer = new Timer();
            _monitoringTimer.Interval = MONITORING_INTERVAL;
            _monitoringTimer.Tick += OnMonitoringTick;
            _monitoringTimer.Enabled = false;

            try
            {
                _lastImage = CaptureRegion();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to capture initial region: {ex.Message}");
            }
        }

        public Rectangle TargetRegion => _targetRegion;

        private async void OnMonitoringTick(object sender, EventArgs e)
        {
            await Task.Run(async () => await CheckForChanges());
        }

        private async Task CheckForChanges()
        {
            if (!_isMonitoring || _isDisposed) return;

            try
            {
                if ((DateTime.Now - _lastChangeTime).TotalMilliseconds < MIN_INTERVAL_BETWEEN_CHANGES)
                    return;

                using (var currentImage = CaptureRegion())
                {
                    if (currentImage == null) return;

                    await Task.Run(() =>
                    {
                        if (TextDetectionUtil.HasSignificantChange(_lastImage, currentImage, _lastText, Text))
                        {
                            _lastChangeTime = DateTime.Now;
                            var oldImage = _lastImage;
                            _lastImage = (Bitmap)currentImage.Clone();
                            oldImage?.Dispose();

                            BeginInvoke(new Action(() => OnPossibleTextChange()));
                            _consecutiveErrors = 0;
                            Debug.WriteLine($"Text change detected in region: {_targetRegion}");
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in CheckForChanges: {ex.Message}");
                _consecutiveErrors++;

                if (_consecutiveErrors >= MAX_CONSECUTIVE_ERRORS)
                {
                    StopMonitoring();
                    Debug.WriteLine("Monitoring stopped due to consecutive errors");
                }
            }
        }

        private Bitmap CaptureRegion()
        {
            if (_isDisposed) return null;

            try
            {
                var bitmap = new Bitmap(_targetRegion.Width, _targetRegion.Height);
                using (var g = Graphics.FromImage(bitmap))
                {
                    g.CopyFromScreen(_targetRegion.Location, Point.Empty, _targetRegion.Size);
                }
                return bitmap;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error capturing region: {ex.Message}");
                return null;
            }
        }

        private void OnPossibleTextChange()
        {
            if (_isDisposed) return;

            try
            {
                TextChangeDetected?.Invoke(this, new TextChangeEventArgs(_targetRegion));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in OnPossibleTextChange: {ex.Message}");
            }
        }

        public void StartMonitoring()
        {
            if (_isDisposed) return;

            _isMonitoring = true;
            _monitoringTimer.Start();
            Debug.WriteLine("Monitoring started");
        }

        public void StopMonitoring()
        {
            if (_isDisposed) return;

            _isMonitoring = false;
            _monitoringTimer.Stop();
            Debug.WriteLine("Monitoring stopped");
        }

        public void UpdateText(string newText)
        {
            if (_isDisposed) return;

            if (_lastText != newText)
            {
                if (InvokeRequired)
                {
                    BeginInvoke(new Action(() => UpdateText(newText)));
                    return;
                }

                Clear(); // 既存のテキストをクリア

                // 書式設定を適用
                SelectionFont = new Font(Font.FontFamily, Font.Size, FontStyle.Bold);
                SelectionColor = Color.White;

                // テキストの追加 (20ピクセルの余白を維持)
                SelectionIndent = 20;
                SelectionRightIndent = 20;
                SelectionHangingIndent = 0;

                AppendText(newText);

                _lastText = newText;
                Debug.WriteLine($"Text updated: {newText}");
            }
        }

        public void RemoveHighlight()
        {
            if (_isDisposed) return;
            StopMonitoring();
        }

        protected override void Dispose(bool disposing)
        {
            if (!_isDisposed)
            {
                if (disposing)
                {
                    StopMonitoring();
                    _monitoringTimer?.Dispose();
                    _lastImage?.Dispose();
                }
                _isDisposed = true;
            }
            base.Dispose(disposing);
        }
    }

    public class TextChangeEventArgs : EventArgs
    {
        public Rectangle Region { get; }

        public TextChangeEventArgs(Rectangle region)
        {
            Region = region;
        }
    }
}