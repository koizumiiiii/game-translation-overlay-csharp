using System;
using System.Windows.Forms;
using GameTranslationOverlay.Core.OCR;
using GameTranslationOverlay.Core.Region;
using GameTranslationOverlay.Forms;

namespace GameTranslationOverlay
{
    public partial class MainForm : Form
    {
        private OverlayForm _overlayForm;
        private TesseractOcrEngine _ocrEngine;
        private RegionManager _regionManager;
        private bool _isEnabled = false;

        public MainForm()
        {
            InitializeComponent();
            InitializeServices();
            btnSelect.Enabled = rbSelectedRegion.Checked;
        }

        private async void InitializeServices()
        {
            try
            {
                _ocrEngine = new TesseractOcrEngine();
                await _ocrEngine.InitializeAsync();

                _regionManager = new RegionManager();
                _overlayForm = new OverlayForm();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"初期化エラー: {ex.Message}", "エラー", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void btnEnable_Click(object sender, EventArgs e)
        {
            _isEnabled = !_isEnabled;
            btnEnable.Text = _isEnabled ? "Disable" : "Enable";
            _overlayForm.Visible = _isEnabled;
        }

        private void rbFullScreen_CheckedChanged(object sender, EventArgs e)
        {
            btnSelect.Enabled = !rbFullScreen.Checked;
            if (rbFullScreen.Checked)
            {
                // 全画面モードの設定
                _overlayForm.SetFullScreenMode();
            }
        }

        private void btnSelect_Click(object sender, EventArgs e)
        {
            if (_overlayForm != null)
            {
                _overlayForm.StartRegionSelection();
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            _ocrEngine?.Dispose();
            _overlayForm?.Dispose();
            base.OnFormClosing(e);
        }
    }
}