using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace GameTranslationOverlay.Forms
{
    /// <summary>
    /// 最適化の進行状態を表示するオーバーレイフォーム
    /// </summary>
    public class OptimizationProgressOverlay : Form
    {
        private ProgressBar _progressBar;
        private Label _statusLabel;
        private Button _cancelButton;
        private Label _titleLabel;

        /// <summary>
        /// 最適化ステップの列挙型
        /// </summary>
        public enum OptimizationStep
        {
            Initializing,       // 初期化中
            CapturingScreen,    // 画面キャプチャ中
            AnalyzingText,      // テキスト分析中
            GeneratingSettings, // 設定生成中
            ApplyingOptimization, // 最適化適用中
            Completed,          // 完了
            Failed              // 失敗
        }

        /// <summary>
        /// コンストラクタ
        /// </summary>
        public OptimizationProgressOverlay()
        {
            InitializeComponent();
        }

        /// <summary>
        /// コンポーネントの初期化
        /// </summary>
        private void InitializeComponent()
        {
            this.FormBorderStyle = FormBorderStyle.None;
            this.BackColor = Color.FromArgb(128, 0, 0, 0); // 半透明の背景
            this.ShowInTaskbar = false;
            this.TopMost = true;
            this.StartPosition = FormStartPosition.Manual;

            // タイトルラベル
            _titleLabel = new Label
            {
                Location = new Point(50, 50),
                Size = new Size(300, 30),
                Text = "OCR最適化",
                ForeColor = Color.White,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI", 14, FontStyle.Bold)
            };

            // プログレスバー
            _progressBar = new ProgressBar
            {
                Location = new Point(50, 150),
                Size = new Size(300, 30),
                Style = ProgressBarStyle.Marquee, // アニメーション表示
                MarqueeAnimationSpeed = 50
            };

            // ステータスラベル
            _statusLabel = new Label
            {
                Location = new Point(50, 100),
                Size = new Size(300, 30),
                Text = "OCR最適化の準備中...",
                ForeColor = Color.White,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI", 12, FontStyle.Regular)
            };

            // キャンセルボタン
            _cancelButton = new Button
            {
                Location = new Point(150, 200),
                Size = new Size(100, 30),
                Text = "キャンセル",
                BackColor = Color.FromArgb(200, 60, 60),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };

            _cancelButton.Click += (s, e) => this.DialogResult = DialogResult.Cancel;

            this.Controls.Add(_titleLabel);
            this.Controls.Add(_progressBar);
            this.Controls.Add(_statusLabel);
            this.Controls.Add(_cancelButton);

            this.Size = new Size(400, 250);
            this.ClientSize = new Size(400, 250);
        }

        /// <summary>
        /// 進行状態の更新
        /// </summary>
        /// <param name="step">最適化ステップ</param>
        /// <param name="progressPercent">進行度（0-100）</param>
        public void UpdateStatus(OptimizationStep step, int progressPercent = -1)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action(() => UpdateStatus(step, progressPercent)));
                return;
            }

            if (progressPercent >= 0)
            {
                _progressBar.Style = ProgressBarStyle.Continuous;
                _progressBar.Value = Math.Min(100, Math.Max(0, progressPercent));
            }

            switch (step)
            {
                case OptimizationStep.Initializing:
                    _statusLabel.Text = "OCR最適化の準備中...";
                    break;
                case OptimizationStep.CapturingScreen:
                    _statusLabel.Text = "画面キャプチャ中...";
                    break;
                case OptimizationStep.AnalyzingText:
                    _statusLabel.Text = "テキスト分析中...";
                    break;
                case OptimizationStep.GeneratingSettings:
                    _statusLabel.Text = "最適なOCR設定を生成中...";
                    break;
                case OptimizationStep.ApplyingOptimization:
                    _statusLabel.Text = "最適化設定を適用中...";
                    break;
                case OptimizationStep.Completed:
                    _statusLabel.Text = "最適化が完了しました！";
                    _statusLabel.ForeColor = Color.LightGreen;
                    _cancelButton.Text = "閉じる";
                    _cancelButton.BackColor = Color.FromArgb(60, 180, 60);
                    break;
                case OptimizationStep.Failed:
                    _statusLabel.Text = "最適化に失敗しました";
                    _statusLabel.ForeColor = Color.OrangeRed;
                    _cancelButton.Text = "閉じる";
                    break;
            }

            this.Update();
        }

        /// <summary>
        /// 詳細なステータスメッセージを設定
        /// </summary>
        /// <param name="statusMessage">ステータスメッセージ</param>
        public void SetDetailedStatus(string statusMessage)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action(() => SetDetailedStatus(statusMessage)));
                return;
            }

            _statusLabel.Text = statusMessage;
        }

        /// <summary>
        /// 親フォームの中央に位置するよう設定
        /// </summary>
        /// <param name="parentForm">親フォーム</param>
        public void CenterToParent(Form parentForm)
        {
            if (parentForm != null)
            {
                int x = parentForm.Left + (parentForm.Width - this.Width) / 2;
                int y = parentForm.Top + (parentForm.Height - this.Height) / 2;
                this.Location = new Point(x, y);
            }
        }

        /// <summary>
        /// 非同期でオーバーレイを表示し、完了を待機
        /// </summary>
        /// <param name="parentForm">親フォーム</param>
        /// <returns>非同期タスク</returns>
        public async Task<DialogResult> ShowDialogAsync(Form parentForm)
        {
            var tcs = new TaskCompletionSource<DialogResult>();

            this.FormClosed += (s, e) => tcs.TrySetResult(this.DialogResult);

            if (parentForm != null && !parentForm.IsDisposed)
            {
                this.Show(parentForm);
            }
            else
            {
                this.Show();
            }

            return await tcs.Task;
        }

        /// <summary>
        /// 最適化プロセスをキャンセル可能にするためのプロパティ
        /// </summary>
        public bool IsCancellationRequested { get; private set; }

        /// <summary>
        /// キャンセルボタンのテキストを設定
        /// </summary>
        /// <param name="text">ボタンのテキスト</param>
        public void SetCancelButtonText(string text)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action(() => SetCancelButtonText(text)));
                return;
            }

            _cancelButton.Text = text;
        }

        /// <summary>
        /// キャンセルボタンのクリックイベントハンドラを設定
        /// </summary>
        /// <param name="handler">クリックイベントハンドラ</param>
        public void SetCancelButtonClickHandler(EventHandler handler)
        {
            _cancelButton.Click -= (s, e) => this.DialogResult = DialogResult.Cancel;
            _cancelButton.Click += handler;
        }

        /// <summary>
        /// キャンセルボタンを有効/無効
        /// </summary>
        /// <param name="enabled">有効にする場合はtrue</param>
        public void SetCancelButtonEnabled(bool enabled)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action(() => SetCancelButtonEnabled(enabled)));
                return;
            }

            _cancelButton.Enabled = enabled;
        }

        /// <summary>
        /// エラーメッセージを表示
        /// </summary>
        /// <param name="errorMessage">エラーメッセージ</param>
        public void ShowError(string errorMessage)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action(() => ShowError(errorMessage)));
                return;
            }

            _statusLabel.Text = errorMessage;
            _statusLabel.ForeColor = Color.OrangeRed;
            _cancelButton.Text = "閉じる";
            _progressBar.Style = ProgressBarStyle.Continuous;
            _progressBar.Value = 0;

            this.Update();
        }

        /// <summary>
        /// 進捗バーのスタイルを設定
        /// </summary>
        /// <param name="style">進捗バーのスタイル</param>
        public void SetProgressBarStyle(ProgressBarStyle style)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action(() => SetProgressBarStyle(style)));
                return;
            }

            _progressBar.Style = style;
        }
    }
}