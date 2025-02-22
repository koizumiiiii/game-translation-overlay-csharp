using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GameTranslationOverlay.Core.Translation.Tests
{
    [TestClass]
    public class LibreTranslateEngineTest
    {
        private ILogger<LibreTranslateEngine> _logger;
        private TranslationConfig _config;
        private LibreTranslateEngine _engine;

        [TestInitialize]
        public void Setup()
        {
            // ロガーの設定
            var loggerFactory = LoggerFactory.Create(builder =>
            {
                builder
                    .AddConsole()
                    .SetMinimumLevel(LogLevel.Debug);
            });
            _logger = loggerFactory.CreateLogger<LibreTranslateEngine>();

            // 設定の初期化
            _config = new TranslationConfig
            {
                BaseUrl = "http://localhost:5000",
                TimeoutSeconds = 10
            };

            // エンジンの初期化
            _engine = new LibreTranslateEngine(_config, _logger);
        }

        [TestMethod]
        public async Task Initialize_ShouldSucceed()
        {
            // Act
            await _engine.InitializeAsync();

            // Assert
            Assert.IsTrue(_engine.IsAvailable);
            Assert.IsNotNull(_engine.SupportedLanguages);
        }

        [TestMethod]
        public async Task Translate_ShouldSucceed()
        {
            // Arrange
            await _engine.InitializeAsync();
            var text = "Hello, world!";

            // Act
            var result = await _engine.TranslateAsync(text, "en", "ja");

            // Assert
            Assert.IsNotNull(result);
            Assert.AreNotEqual(text, result);
            _logger.LogInformation($"Translated text: {result}");
        }

        [TestMethod]
        public async Task Translate_EmptyText_ShouldReturnEmpty()
        {
            // Arrange
            await _engine.InitializeAsync();

            // Act
            var result = await _engine.TranslateAsync("", "en", "ja");

            // Assert
            Assert.AreEqual(string.Empty, result);
        }

        [TestMethod]
        [ExpectedException(typeof(TranslationEngineException))]
        public async Task Translate_BeforeInitialize_ShouldThrowException()
        {
            // Act
            await _engine.TranslateAsync("test", "en", "ja");
        }

        [TestCleanup]
        public void Cleanup()
        {
            _engine?.Dispose();
        }
    }
}