using System.Collections.Generic;
using System.Threading.Tasks;

namespace GameTranslationOverlay.Core.Translation
{
    public interface ITranslationEngine
    {
        /// <summary>
        /// 翻訳エンジンが利用可能かどうかを示す
        /// </summary>
        bool IsAvailable { get; }

        /// <summary>
        /// サポートされている言語コードのリスト
        /// </summary>
        IEnumerable<string> SupportedLanguages { get; }

        /// <summary>
        /// テキストを翻訳する
        /// </summary>
        /// <param name="text">翻訳元のテキスト</param>
        /// <param name="fromLang">翻訳元の言語コード</param>
        /// <param name="toLang">翻訳先の言語コード</param>
        /// <returns>翻訳されたテキスト</returns>
        Task<string> TranslateAsync(string text, string fromLang, string toLang);

        /// <summary>
        /// 翻訳エンジンを初期化する
        /// </summary>
        Task InitializeAsync();
    }
}