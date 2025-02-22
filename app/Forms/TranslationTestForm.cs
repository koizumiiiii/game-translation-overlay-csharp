using System;
using System.Windows.Forms;
using Microsoft.Extensions.Logging;
using GameTranslationOverlay.Core.Translation;

namespace GameTranslationOverlay.Forms
{
    public partial class TranslationTestForm : Form
    {
        private readonly ILogger<TranslationTestForm> _logger;
        private LibreTranslateEngine _translationEngine;

        public TranslationTestForm()
        {
            InitializeComponent();

            // ロガーの設定
            var loggerFactory = LoggerFactory.Create(builder =>
            {
                builder
                    .AddConsole()
                    .SetMinimumLevel(LogLevel.Debug);
            });
            _logger = loggerFactory.CreateLogger<TranslationTestForm>();

            InitializeTranslationEngine();
        }

        private async void InitializeTranslationEngine()
        {
            try
            {
                var config = new TranslationConfig
                {
                    BaseUrl = "http://localhost:5000",
                    TimeoutSeconds = 10
                };

                _translationEngine = new LibreTranslateEngine(config, _logger);
                await _translationEngine.InitializeAsync();

                // 言語選択コンボボックスの設定
                comboBoxSourceLang.Items.AddRange(new[] { "en", "ja" });
                comboBoxTargetLang.Items.AddRange(new[] { "en", "ja" });

                comboBoxSourceLang.SelectedItem = "en";
                comboBoxTargetLang.SelectedItem = "ja";

                _logger.LogInformation("Translation engine initialized successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to initialize translation engine: {ex.Message}");
                MessageBox.Show("翻訳エンジンの初期化に失敗しました。", "エラー", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async void buttonTranslate_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(textBoxInput.Text))
            {
                MessageBox.Show("翻訳するテキストを入力してください。", "警告", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                buttonTranslate.Enabled = false;
                textBoxOutput.Text = "翻訳中...";

                var result = await _translationEngine.TranslateAsync(
                    textBoxInput.Text,
                    comboBoxSourceLang.SelectedItem.ToString(),
                    comboBoxTargetLang.SelectedItem.ToString()
                );

                textBoxOutput.Text = result;
                _logger.LogInformation($"Translation completed: {result}");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Translation failed: {ex.Message}");
                MessageBox.Show($"翻訳に失敗しました: {ex.Message}", "エラー", MessageBoxButtons.OK, MessageBoxIcon.Error);
                textBoxOutput.Text = string.Empty;
            }
            finally
            {
                buttonTranslate.Enabled = true;
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            base.OnFormClosing(e);
            _translationEngine?.Dispose();
        }
    }
}