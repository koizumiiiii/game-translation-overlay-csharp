using System;
using System.Drawing;
using System.Windows.Forms;
using GameTranslationOverlay.Core.Region;

namespace GameTranslationOverlay
{
    public partial class OverlayForm : Form
    {
        private bool _isRegionSelectionMode = false;
        private Point? _selectionStart = null;
        private Panel _selectionBox = null;

        public OverlayForm()
        {
            InitializeComponent();
            InitializeOverlayWindow();
        }

        private void InitializeOverlayWindow()
        {
            // ウィンドウの基本設定
            this.FormBorderStyle = FormBorderStyle.None;
            this.ShowInTaskbar = false;
            this.TopMost = true;
            this.Opacity = 0.5;
            this.TransparencyKey = this.BackColor = Color.Black;

            // 画面全体に表示
            Rectangle bounds = Screen.PrimaryScreen.Bounds;
            this.Bounds = bounds;

            // マウスイベントの登録
            this.MouseDown += OverlayForm_MouseDown;
            this.MouseMove += OverlayForm_MouseMove;
            this.MouseUp += OverlayForm_MouseUp;
        }

        public void SetFullScreenMode()
        {
            _isRegionSelectionMode = false;
            ClearSelectionBox();
            // TODO: 全画面モードの設定を実装
        }

        public void StartRegionSelection()
        {
            _isRegionSelectionMode = true;
            this.Cursor = Cursors.Cross;
        }

        private void OverlayForm_MouseDown(object sender, MouseEventArgs e)
        {
            if (!_isRegionSelectionMode) return;

            _selectionStart = e.Location;
            _selectionBox = new Panel
            {
                BackColor = Color.FromArgb(50, 0, 120, 215),
                BorderStyle = BorderStyle.FixedSingle,
                Location = e.Location,
                Size = new Size(0, 0)
            };

            this.Controls.Add(_selectionBox);
            _selectionBox.BringToFront();
        }

        private void OverlayForm_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_isRegionSelectionMode || _selectionStart == null || _selectionBox == null) return;

            int x = Math.Min(e.X, _selectionStart.Value.X);
            int y = Math.Min(e.Y, _selectionStart.Value.Y);
            int width = Math.Abs(e.X - _selectionStart.Value.X);
            int height = Math.Abs(e.Y - _selectionStart.Value.Y);

            _selectionBox.Location = new Point(x, y);
            _selectionBox.Size = new Size(width, height);
        }

        private void OverlayForm_MouseUp(object sender, MouseEventArgs e)
        {
            if (!_isRegionSelectionMode || _selectionStart == null) return;

            var region = new Rectangle(
                Math.Min(e.X, _selectionStart.Value.X),
                Math.Min(e.Y, _selectionStart.Value.Y),
                Math.Abs(e.X - _selectionStart.Value.X),
                Math.Abs(e.Y - _selectionStart.Value.Y)
            );

            // TODO: 選択された領域を保存/処理

            ClearSelectionBox();
            _selectionStart = null;
            _isRegionSelectionMode = false;
            this.Cursor = Cursors.Default;
        }

        private void ClearSelectionBox()
        {
            if (_selectionBox != null)
            {
                this.Controls.Remove(_selectionBox);
                _selectionBox.Dispose();
                _selectionBox = null;
            }
        }
    }
}