using System;
using System.Drawing;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace GameTranslationOverlay.Forms
{
    public partial class OcrTestForm : Form
    {
        private Rectangle _selectedRegion;
        private readonly ListBox _resultsListBox;
        private readonly Button _startTestButton;
        private readonly Button _selectRegionButton;
        private readonly Label _statusLabel;

        public OcrTestForm()
        {
            Text = "OCR Test";
            Size = new Size(800, 600);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;

            // 領域選択ボタン
            _selectRegionButton = new Button
            {
                Text = "Select Region",
                Location = new Point(10, 10),
                Size = new Size(120, 30)
            };
            _selectRegionButton.Click += OnSelectRegionClick;

            // テスト開始ボタン
            _startTestButton = new Button
            {
                Text = "Start OCR Test",
                Location = new Point(140, 10),
                Size = new Size(120, 30),
                Enabled = false
            };
            _startTestButton.Click += OnStartTestClick;

            // ステータスラベル
            _statusLabel = new Label
            {
                Text = "Select a region to test",
                Location = new Point(270, 15),
                AutoSize = true
            };

            // 結果表示用リストボックス
            _resultsListBox = new ListBox
            {
                Location = new Point(10, 50),
                Size = new Size(765, 500),
                Font = new Font("Consolas", 9F),
                HorizontalScrollbar = true
            };

            Controls.AddRange(new Control[] {
                _selectRegionButton,
                _startTestButton,
                _statusLabel,
                _resultsListBox
            });
        }

        private void OnSelectRegionClick(object sender, EventArgs e)
        {
            this.WindowState = FormWindowState.Minimized;

            using (var selector = new RegionSelectorForm())
            {
                if (selector.ShowDialog() == DialogResult.OK)
                {
                    _selectedRegion = selector.SelectedRegion;
                    _startTestButton.Enabled = true;
                    _statusLabel.Text = $"Selected Region: {_selectedRegion.Width}x{_selectedRegion.Height}";
                }
                else
                {
                    _startTestButton.Enabled = false;
                    _statusLabel.Text = "Region selection cancelled";
                }
            }

            this.WindowState = FormWindowState.Normal;
            this.Activate();
        }

        private async void OnStartTestClick(object sender, EventArgs e)
        {
            if (_selectedRegion.IsEmpty)
            {
                MessageBox.Show("Please select a region first.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            _startTestButton.Enabled = false;
            _selectRegionButton.Enabled = false;
            _statusLabel.Text = "Running OCR tests...";
            _resultsListBox.Items.Clear();

            try
            {
                var results = await Core.OCR.OcrTest.RunTests(_selectedRegion);
                DisplayResults(results);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error during OCR test: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                _startTestButton.Enabled = true;
                _selectRegionButton.Enabled = true;
                _statusLabel.Text = "Test completed";
            }
        }

        private void DisplayResults(List<Core.OCR.OcrTest.TestResult> results)
        {
            _resultsListBox.Items.Clear();
            _resultsListBox.Items.Add("OCR Test Results:");
            _resultsListBox.Items.Add("================");

            foreach (var result in results)
            {
                _resultsListBox.Items.Add($"Engine: {result.EngineName}");
                _resultsListBox.Items.Add($"Config: {result.Configuration}");
                _resultsListBox.Items.Add($"Time: {result.ProcessingTime}ms");
                _resultsListBox.Items.Add($"Accuracy: {result.Accuracy:P2}");
                _resultsListBox.Items.Add($"Text: {result.RecognizedText}");

                if (result.AdditionalInfo?.Any() == true)
                {
                    _resultsListBox.Items.Add("Additional Info:");
                    foreach (var info in result.AdditionalInfo)
                    {
                        _resultsListBox.Items.Add($"  {info.Key}: {info.Value}");
                    }
                }

                _resultsListBox.Items.Add("----------------");
            }
        }
    }

    public class RegionSelectorForm : Form
    {
        private Point _startPoint;
        private bool _isSelecting;
        public Rectangle SelectedRegion { get; private set; }

        public RegionSelectorForm()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer, true);
            FormBorderStyle = FormBorderStyle.None;
            WindowState = FormWindowState.Maximized;
            Cursor = Cursors.Cross;
            BackColor = Color.FromArgb(128, 0, 0, 0);
            TopMost = true;

            MouseDown += OnMouseDown;
            MouseMove += OnMouseMove;
            MouseUp += OnMouseUp;
            KeyDown += OnKeyDown;
        }

        private void OnMouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                _startPoint = e.Location;
                _isSelecting = true;
            }
        }

        private void OnMouseMove(object sender, MouseEventArgs e)
        {
            if (_isSelecting)
            {
                SelectedRegion = GetRectangle(_startPoint, e.Location);
                Invalidate();
            }
        }

        private void OnMouseUp(object sender, MouseEventArgs e)
        {
            if (_isSelecting)
            {
                _isSelecting = false;
                SelectedRegion = GetRectangle(_startPoint, e.Location);
                if (!SelectedRegion.IsEmpty)
                {
                    DialogResult = DialogResult.OK;
                }
                Close();
            }
        }

        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
            {
                DialogResult = DialogResult.Cancel;
                Close();
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (!SelectedRegion.IsEmpty)
            {
                using (var pen = new Pen(Color.Red, 2))
                {
                    e.Graphics.DrawRectangle(pen, SelectedRegion);
                }
            }
        }

        private Rectangle GetRectangle(Point start, Point end)
        {
            return new Rectangle(
                Math.Min(start.X, end.X),
                Math.Min(start.Y, end.Y),
                Math.Abs(end.X - start.X),
                Math.Abs(end.Y - start.Y)
            );
        }
    }
}