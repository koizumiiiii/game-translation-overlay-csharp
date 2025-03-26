using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Drawing.Drawing2D;

namespace GameTranslationOverlay.Forms
{
    /// <summary>
    /// 最適化の進行状態を表示するオーバーレイフォーム
    /// </summary>
    public class OptimizationProgressOverlay : Form
    {
        private ProgressBar _progressBar;
        private Label _statusLabel;
        private Label _detailLabel;
        private Button _cancelButton;
        private Label _titleLabel;
        private TableLayoutPanel _stepsPanel;
        private Panel _animationPanel;
        private Timer _animationTimer;

        private Dictionary<OptimizationStep, StepControl> _stepControls;
        private OptimizationStep _currentStep = OptimizationStep.Initializing;
        private int _animationFrame = 0;

        /// <summary>
        /// 最適化ステップの列挙型
        /// </summary>
        public enum OptimizationStep
        {
            Initializing,       // 初期化中
            CapturingScreen,    // 画面キャプチャ中
            AnalyzingText,      // テキスト分析中
            AIProcessing,       // AI分析中
            GeneratingSettings, // 設定生成中
            TestingSettings,    // 設定テスト中
            VerifyingResults,   // 結果検証中
            ApplyingOptimization, // 最適化適用中
            SavingProfile,      // プロファイル保存中
            Completed,          // 完了
            Failed              // 失敗
        }

        /// <summary>
        /// コンストラクタ
        /// </summary>
        public OptimizationProgressOverlay()
        {
            InitializeComponent();
            InitializeStepControls();
            InitializeAnimationTimer();
        }

        /// <summary>
        /// コンポーネントの初期化
        /// </summary>
        private void InitializeComponent()
        {
            this.FormBorderStyle = FormBorderStyle.None;
            this.BackColor = Color.FromArgb(30, 30, 30);
            this.Opacity = 0.95; // 95%の不透明度
            this.ShowInTaskbar = false;
            this.TopMost = true;
            this.StartPosition = FormStartPosition.Manual;

            // タイトルラベル
            _titleLabel = new Label
            {
                Location = new Point(50, 30),
                Size = new Size(400, 40),
                Text = "OCR最適化",
                ForeColor = Color.White,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI", 16, FontStyle.Bold)
            };

            // ステップ表示パネル
            _stepsPanel = new TableLayoutPanel
            {
                Location = new Point(50, 80),
                Size = new Size(400, 60),
                ColumnCount = 5,
                RowCount = 1,
                BackColor = Color.Transparent
            };

            // プログレスバー
            _progressBar = new ProgressBar
            {
                Location = new Point(50, 160),
                Size = new Size(400, 24),
                Style = ProgressBarStyle.Marquee,
                MarqueeAnimationSpeed = 50
            };

            // アニメーション用パネル
            _animationPanel = new Panel
            {
                Location = new Point(50, 195),
                Size = new Size(400, 5),
                BackColor = Color.FromArgb(0, 120, 215),
                Visible = false
            };

            // ステータスラベル
            _statusLabel = new Label
            {
                Location = new Point(50, 210),
                Size = new Size(400, 30),
                Text = "OCR最適化の準備中...",
                ForeColor = Color.White,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI", 12, FontStyle.Regular)
            };

            // 詳細ラベル
            _detailLabel = new Label
            {
                Location = new Point(50, 240),
                Size = new Size(400, 25),
                Text = "",
                ForeColor = Color.Silver,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI", 9, FontStyle.Regular)
            };

            // キャンセルボタン
            _cancelButton = new Button
            {
                Location = new Point(200, 280),
                Size = new Size(100, 32),
                Text = "キャンセル",
                BackColor = Color.FromArgb(60, 60, 60),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };

            _cancelButton.FlatAppearance.BorderSize = 1;
            _cancelButton.FlatAppearance.BorderColor = Color.FromArgb(100, 100, 100);
            _cancelButton.Click += (s, e) => this.DialogResult = DialogResult.Cancel;

            this.Controls.Add(_titleLabel);
            this.Controls.Add(_stepsPanel);
            this.Controls.Add(_progressBar);
            this.Controls.Add(_animationPanel);
            this.Controls.Add(_statusLabel);
            this.Controls.Add(_detailLabel);
            this.Controls.Add(_cancelButton);

            this.Size = new Size(500, 350);
            this.ClientSize = new Size(500, 350);
        }

        /// <summary>
        /// ステップコントロールの初期化
        /// </summary>
        private void InitializeStepControls()
        {
            _stepControls = new Dictionary<OptimizationStep, StepControl>();

            // 主要なステップだけをUI上に表示
            var displaySteps = new OptimizationStep[]
            {
                OptimizationStep.AnalyzingText,
                OptimizationStep.AIProcessing,
                OptimizationStep.GeneratingSettings,
                OptimizationStep.VerifyingResults,
                OptimizationStep.SavingProfile
            };

            var stepLabels = new string[]
            {
                "テキスト分析",
                "AI処理",
                "設定生成",
                "検証",
                "保存"
            };

            _stepsPanel.ColumnStyles.Clear();
            for (int i = 0; i < displaySteps.Length; i++)
            {
                _stepsPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20F));
                var stepControl = new StepControl(i + 1, stepLabels[i]);
                _stepControls.Add(displaySteps[i], stepControl);
                _stepsPanel.Controls.Add(stepControl, i, 0);
            }
        }

        /// <summary>
        /// アニメーションタイマーの初期化
        /// </summary>
        private void InitializeAnimationTimer()
        {
            _animationTimer = new Timer
            {
                Interval = 50,
                Enabled = false
            };
            
            _animationTimer.Tick += (s, e) =>
            {
                _animationFrame = (_animationFrame + 1) % 10;
                int width = _animationPanel.Width;
                int position = (_animationFrame * width / 10) - width / 2;
                
                using (Graphics g = _animationPanel.CreateGraphics())
                {
                    g.Clear(_animationPanel.BackColor);
                    using (LinearGradientBrush brush = new LinearGradientBrush(
                        new Rectangle(position, 0, width, _animationPanel.Height),
                        Color.FromArgb(0, 120, 215),
                        Color.FromArgb(100, 180, 255),
                        LinearGradientMode.Horizontal))
                    {
                        g.FillRectangle(brush, position, 0, width, _animationPanel.Height);
                    }
                }
            };
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

            _currentStep = step;
            
            // ステップコントロールの状態を更新
            foreach (var kvp in _stepControls)
            {
                if (kvp.Key < step)
                {
                    kvp.Value.SetState(StepState.Completed);
                }
                else if (kvp.Key == step)
                {
                    kvp.Value.SetState(StepState.Current);
                }
                else
                {
                    kvp.Value.SetState(StepState.Pending);
                }
            }

            // 進行度の表示
            if (progressPercent >= 0)
            {
                _progressBar.Style = ProgressBarStyle.Continuous;
                _progressBar.Value = Math.Min(100, Math.Max(0, progressPercent));
                _animationPanel.Visible = false;
                _animationTimer.Stop();
            }
            else
            {
                _progressBar.Style = ProgressBarStyle.Marquee;
                _animationPanel.Visible = true;
                _animationTimer.Start();
            }

            // ステータステキストの更新
            UpdateStepStatusText(step);

            this.Update();
        }

        /// <summary>
        /// ステップごとのステータステキストを更新
        /// </summary>
        private void UpdateStepStatusText(OptimizationStep step)
        {
            switch (step)
            {
                case OptimizationStep.Initializing:
                    _statusLabel.Text = "OCR最適化の準備中...";
                    _detailLabel.Text = "初期化しています";
                    break;
                case OptimizationStep.CapturingScreen:
                    _statusLabel.Text = "画面キャプチャ中...";
                    _detailLabel.Text = "ゲーム画面を取得しています";
                    break;
                case OptimizationStep.AnalyzingText:
                    _statusLabel.Text = "テキスト分析中...";
                    _detailLabel.Text = "画面上のテキストを検出しています";
                    break;
                case OptimizationStep.AIProcessing:
                    _statusLabel.Text = "AI処理中...";
                    _detailLabel.Text = "AIによるテキスト特性の分析を行っています";
                    break;
                case OptimizationStep.GeneratingSettings:
                    _statusLabel.Text = "最適なOCR設定を生成中...";
                    _detailLabel.Text = "このゲームに最適なパラメータを計算しています";
                    break;
                case OptimizationStep.TestingSettings:
                    _statusLabel.Text = "設定のテスト中...";
                    _detailLabel.Text = "生成した設定をテストしています";
                    break;
                case OptimizationStep.VerifyingResults:
                    _statusLabel.Text = "結果の検証中...";
                    _detailLabel.Text = "設定の有効性を検証しています";
                    break;
                case OptimizationStep.ApplyingOptimization:
                    _statusLabel.Text = "最適化設定を適用中...";
                    _detailLabel.Text = "OCRエンジンに設定を適用しています";
                    break;
                case OptimizationStep.SavingProfile:
                    _statusLabel.Text = "プロファイルを保存中...";
                    _detailLabel.Text = "設定をゲームプロファイルに保存しています";
                    break;
                case OptimizationStep.Completed:
                    _statusLabel.Text = "最適化が完了しました！";
                    _detailLabel.Text = "OCR設定が正常に最適化されました";
                    _statusLabel.ForeColor = Color.LightGreen;
                    _cancelButton.Text = "閉じる";
                    _cancelButton.BackColor = Color.FromArgb(40, 120, 40);
                    _animationTimer.Stop();
                    _animationPanel.Visible = false;
                    
                    // 全てのステップを完了状態に
                    foreach (var control in _stepControls.Values)
                    {
                        control.SetState(StepState.Completed);
                    }
                    break;
                case OptimizationStep.Failed:
                    _statusLabel.Text = "最適化に失敗しました";
                    _detailLabel.Text = "詳細はエラーメッセージをご確認ください";
                    _statusLabel.ForeColor = Color.OrangeRed;
                    _cancelButton.Text = "閉じる";
                    _cancelButton.BackColor = Color.FromArgb(120, 40, 40);
                    _animationTimer.Stop();
                    _animationPanel.Visible = false;
                    
                    // 現在のステップを失敗状態に
                    if (_stepControls.ContainsKey(_currentStep))
                    {
                        _stepControls[_currentStep].SetState(StepState.Failed);
                    }
                    break;
            }
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

            _detailLabel.Text = statusMessage;
            this.Update();
        }

        /// <summary>
        /// メインステータスのメッセージを直接設定
        /// </summary>
        /// <param name="mainStatus">メインステータスメッセージ</param>
        /// <param name="detailStatus">詳細ステータスメッセージ</param>
        public void SetStatus(string mainStatus, string detailStatus = null)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action(() => SetStatus(mainStatus, detailStatus)));
                return;
            }

            _statusLabel.Text = mainStatus;
            
            if (detailStatus != null)
            {
                _detailLabel.Text = detailStatus;
            }
            
            this.Update();
        }

        /// <summary>
        /// 最適化結果の成功メッセージを表示
        /// </summary>
        /// <param name="detectedRegions">検出されたテキスト領域数</param>
        /// <param name="confidence">平均信頼度</param>
        public void ShowSuccessResult(int detectedRegions, double confidence)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action(() => ShowSuccessResult(detectedRegions, confidence)));
                return;
            }

            UpdateStatus(OptimizationStep.Completed, 100);
            
            _statusLabel.Text = "最適化が完了しました！";
            _detailLabel.Text = $"テキスト領域: {detectedRegions}個 / 平均信頼度: {confidence:F2}";
            _progressBar.Value = 100;
            
            // 成功エフェクト - グリーンフラッシュ
            Task.Run(async () => 
            {
                await FlashBackgroundAsync(Color.FromArgb(0, 100, 0), 800);
            });
        }

        /// <summary>
        /// エラーメッセージを表示
        /// </summary>
        /// <param name="errorMessage">エラーメッセージ</param>
        /// <param name="detailMessage">詳細メッセージ</param>
        public void ShowError(string errorMessage, string detailMessage = null)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action(() => ShowError(errorMessage, detailMessage)));
                return;
            }

            UpdateStatus(OptimizationStep.Failed, 0);
            
            _statusLabel.Text = errorMessage;
            if (detailMessage != null)
            {
                _detailLabel.Text = detailMessage;
            }
            
            // 失敗エフェクト - レッドフラッシュ
            Task.Run(async () => 
            {
                await FlashBackgroundAsync(Color.FromArgb(100, 0, 0), 800);
            });
        }

        /// <summary>
        /// 背景を一時的にフラッシュさせる
        /// </summary>
        private async Task FlashBackgroundAsync(Color flashColor, int duration)
        {
            if (this.InvokeRequired)
            {
                // ラムダ式を明示的にFunc<Task>型に変換
                Func<Task> asyncAction = async () => await FlashBackgroundAsync(flashColor, duration);
                this.Invoke(asyncAction);
                return;
            }

            Color originalColor = this.BackColor;
            this.BackColor = flashColor;
            await Task.Delay(duration);
            
            // 元の色に徐々に戻す
            for (int i = 10; i > 0; i--)
            {
                this.BackColor = Color.FromArgb(
                    (flashColor.R * i + originalColor.R * (10 - i)) / 10,
                    (flashColor.G * i + originalColor.G * (10 - i)) / 10,
                    (flashColor.B * i + originalColor.B * (10 - i)) / 10
                );
                await Task.Delay(20);
            }
            
            this.BackColor = originalColor;
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
            
            if (style == ProgressBarStyle.Marquee)
            {
                _animationPanel.Visible = true;
                _animationTimer.Start();
            }
            else
            {
                _animationPanel.Visible = false;
                _animationTimer.Stop();
            }
        }

        /// <summary>
        /// フォーム終了時にアニメーションタイマーを停止
        /// </summary>
        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            _animationTimer?.Stop();
            base.OnFormClosed(e);
        }
    }

    /// <summary>
    /// ステップの状態を表す列挙型
    /// </summary>
    public enum StepState
    {
        Pending,    // 保留中
        Current,    // 現在実行中
        Completed,  // 完了
        Failed      // 失敗
    }

    /// <summary>
    /// ステップを表示するコントロール
    /// </summary>
    public class StepControl : Panel
    {
        private Label _numberLabel;
        private Label _textLabel;
        private StepState _state;

        public StepControl(int number, string text)
        {
            this.Size = new Size(70, 60);
            this.BackColor = Color.Transparent;
            this.Dock = DockStyle.Fill;
            this.Margin = new Padding(2);

            _numberLabel = new Label
            {
                Size = new Size(24, 24),
                Text = number.ToString(),
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = Color.FromArgb(60, 60, 60),
                Location = new Point((this.Width - 24) / 2, 5)
            };

            _textLabel = new Label
            {
                Size = new Size(70, 20),
                Text = text,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI", 8, FontStyle.Regular),
                ForeColor = Color.Silver,
                BackColor = Color.Transparent,
                Location = new Point(0, 34),
                Dock = DockStyle.Bottom
            };

            this.Controls.Add(_numberLabel);
            this.Controls.Add(_textLabel);

            SetState(StepState.Pending);
        }

        public void SetState(StepState state)
        {
            _state = state;
            
            switch (state)
            {
                case StepState.Pending:
                    _numberLabel.BackColor = Color.FromArgb(60, 60, 60);
                    _numberLabel.ForeColor = Color.Silver;
                    _textLabel.ForeColor = Color.Silver;
                    break;
                case StepState.Current:
                    _numberLabel.BackColor = Color.FromArgb(0, 120, 215);
                    _numberLabel.ForeColor = Color.White;
                    _textLabel.ForeColor = Color.White;
                    break;
                case StepState.Completed:
                    _numberLabel.BackColor = Color.FromArgb(40, 160, 40);
                    _numberLabel.ForeColor = Color.White;
                    _textLabel.ForeColor = Color.LightGreen;
                    break;
                case StepState.Failed:
                    _numberLabel.BackColor = Color.FromArgb(180, 40, 40);
                    _numberLabel.ForeColor = Color.White;
                    _textLabel.ForeColor = Color.OrangeRed;
                    break;
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            
            // コントロールが親コンテナに存在し、最後のコントロールでない場合、ラインを描画
            if (this.Parent != null && this.Parent.Controls.IndexOf(this) < this.Parent.Controls.Count - 1)
            {
                Color lineColor = _state == StepState.Completed ? Color.FromArgb(40, 160, 40) : Color.FromArgb(80, 80, 80);
                
                using (Pen pen = new Pen(lineColor, 1))
                {
                    // ステップ間の接続線を描画
                    int startX = this.Width - 5;
                    int startY = 17;
                    int endX = this.Width + 5;
                    int endY = 17;
                    
                    e.Graphics.DrawLine(pen, startX, startY, endX, endY);
                }
            }
        }
    }
}