using System;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using GameTranslationOverlay.Core.Translation;

namespace GameTranslationOverlay.Tests.Translation
{
    [TestClass]
    public class LibreTranslateEngineTests
    {
        private LibreTranslateEngine _engine;

        [TestInitialize]
        public void Setup()
        {
            var settings = new LibreTranslateEngine.Settings
            {
                BaseUrl = "http://localhost:5000",
                Timeout = 10000,
                MaxRetries = 3,
                RetryDelay = 1000
            };
            _engine = new LibreTranslateEngine(settings);
        }

        [TestCleanup]
        public void Cleanup()
        {
            _engine?.Dispose();
        }

        [TestMethod]
        public async Task InitializeAsync_ShouldSucceed()
        {
            await _engine.InitializeAsync();
            Assert.IsTrue(_engine.IsAvailable);
            CollectionAssert.Contains(_engine.SupportedLanguages.ToList(), "en");
            CollectionAssert.Contains(_engine.SupportedLanguages.ToList(), "ja");
        }

        [TestMethod]
        public async Task TranslateAsync_SimpleText_ShouldSucceed()
        {
            await _engine.InitializeAsync();

            // English to Japanese
            var textEn = "Hello, world!";
            var translatedJa = await _engine.TranslateAsync(textEn, "en", "ja");
            Assert.IsFalse(string.IsNullOrEmpty(translatedJa));
            Assert.AreNotEqual(textEn, translatedJa);

            // Japanese to English
            var textJa = "こんにちは、世界！";
            var translatedEn = await _engine.TranslateAsync(textJa, "ja", "en");
            Assert.IsFalse(string.IsNullOrEmpty(translatedEn));
            Assert.AreNotEqual(textJa, translatedEn);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public async Task TranslateAsync_UnsupportedLanguage_ShouldThrowException()
        {
            await _engine.InitializeAsync();
            await _engine.TranslateAsync("Test", "xx", "en"); // Invalid language code
        }

        [TestMethod]
        public async Task TranslateAsync_EmptyText_ShouldReturnEmptyString()
        {
            await _engine.InitializeAsync();
            var result = await _engine.TranslateAsync("", "en", "ja");
            Assert.AreEqual(string.Empty, result);
        }
    }
}