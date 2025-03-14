using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Text;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using GameTranslationOverlay.Core.Diagnostics;

namespace GameTranslationOverlay.Core.UI
{
    /// <summary>
    /// フォント管理クラス - アプリケーション全体のフォント設定を一元管理
    /// </summary>
    public class FontManager
    {
        #region シングルトンパターン実装

        private static readonly Lazy<FontManager> _instance = new Lazy<FontManager>(() => new FontManager());

        /// <summary>
        /// シングルトンインスタンスを取得
        /// </summary>
        public static FontManager Instance => _instance.Value;

        // 外部からのインスタンス化を防止
        private FontManager()
        {
            Initialize();
        }

        #endregion

        #region フィールドとプロパティ

        // プライベートフォントコレクション
        private readonly PrivateFontCollection _privateFontCollection = new PrivateFontCollection();

        // フォントファイルパス
        private string _jpFontPath = string.Empty;
        private string _enFontPath = string.Empty;

        // 言語別フォントのロード状態
        private bool _isJpFontLoaded = false;
        private bool _isEnFontLoaded = false;

        // フォントファミリー名
        private const string JpFontFamilyName = "LINE Seed JP";
        private const string EnFontFamilyName = "LINE Seed Sans";
        private const string DefaultFontName = "Meiryo UI";
        private const string DefaultFallbackFontName = "MS Gothic";

        // フォントサイズ
        private float _defaultFontSize = 9.0f;
        private float _translationFontSize = 12.0f;
        private float _titleFontSize = 11.0f;
        private float _smallFontSize = 8.0f;

        // 利用可能なフォントファミリー名のリスト
        private readonly List<string> _availableFontFamilies = new List<string>();

        /// <summary>
        /// デフォルトの標準フォントを取得します
        /// </summary>
        public Font DefaultFont => CreateFont(FontSize.Default);

        /// <summary>
        /// 翻訳テキスト用のフォントを取得します（デフォルト言語用）
        /// </summary>
        public Font TranslationFont => CreateTranslationFont(TranslationLanguage.Default);

        /// <summary>
        /// 日本語翻訳用のフォントを取得します
        /// </summary>
        public Font JapaneseTranslationFont => CreateTranslationFont(TranslationLanguage.Japanese);

        /// <summary>
        /// 英語翻訳用のフォントを取得します
        /// </summary>
        public Font EnglishTranslationFont => CreateTranslationFont(TranslationLanguage.English);

        /// <summary>
        /// タイトル用のフォントを取得します
        /// </summary>
        public Font TitleFont => CreateFont(FontSize.Title);

        /// <summary>
        /// 小さいテキスト用のフォントを取得します
        /// </summary>
        public Font SmallFont => CreateFont(FontSize.Small);

        /// <summary>
        /// 日本語フォントがロードされているかどうかを示す値を取得します
        /// </summary>
        public bool IsJpFontAvailable => _isJpFontLoaded;

        /// <summary>
        /// 英語フォントがロードされているかどうかを示す値を取得します
        /// </summary>
        public bool IsEnFontAvailable => _isEnFontLoaded;

        /// <summary>
        /// 利用可能なフォントファミリーのリストを取得します
        /// </summary>
        public IReadOnlyList<string> AvailableFontFamilies => _availableFontFamilies.AsReadOnly();

        #endregion

        #region 公開メソッド

        /// <summary>
        /// 指定したサイズのフォントを作成します
        /// </summary>
        /// <param name="fontSize">フォントサイズ種別</param>
        /// <returns>指定したサイズのフォント</returns>
        public Font CreateFont(FontSize fontSize)
        {
            float size = GetFontSizeValue(fontSize);

            try
            {
                // 利用可能なシステムフォントから最適なものを選択
                if (IsFontAvailable(DefaultFontName))
                {
                    return new Font(DefaultFontName, size);
                }

                // フォールバックフォント
                return new Font(DefaultFallbackFontName, size);
            }
            catch (Exception ex)
            {
                Logger.Instance.Error("FontManager", $"フォント作成中にエラーが発生しました", ex);
                // 最終的なフォールバック
                return new Font(FontFamily.GenericSansSerif, size);
            }
        }

        /// <summary>
        /// 翻訳用のフォントを言語に応じて作成します
        /// </summary>
        /// <param name="language">翻訳言語</param>
        /// <returns>言語に適したフォント</returns>
        public Font CreateTranslationFont(TranslationLanguage language)
        {
            float size = GetFontSizeValue(FontSize.Translation);

            try
            {
                switch (language)
                {
                    case TranslationLanguage.Japanese:
                        // 日本語フォントが利用可能な場合
                        if (_isJpFontLoaded)
                        {
                            foreach (FontFamily family in _privateFontCollection.Families)
                            {
                                if (family.Name.Contains("LINE") && family.Name.Contains("JP"))
                                {
                                    return new Font(family, size);
                                }
                            }
                        }
                        break;

                    case TranslationLanguage.English:
                        // 英語フォントが利用可能な場合
                        if (_isEnFontLoaded)
                        {
                            foreach (FontFamily family in _privateFontCollection.Families)
                            {
                                if (family.Name.Contains("LINE") && !family.Name.Contains("JP"))
                                {
                                    return new Font(family, size);
                                }
                            }
                        }
                        break;
                }

                // 言語別フォントが利用できない場合はシステムフォント
                if (IsFontAvailable(DefaultFontName))
                {
                    return new Font(DefaultFontName, size);
                }

                // 最終フォールバック
                return new Font(DefaultFallbackFontName, size);
            }
            catch (Exception ex)
            {
                Logger.Instance.Error("FontManager", $"翻訳フォント作成中にエラーが発生しました", ex);
                // 最終的なフォールバック
                return new Font(FontFamily.GenericSansSerif, size);
            }
        }

        /// <summary>
        /// コントロールにデフォルトフォントを適用します
        /// </summary>
        /// <param name="control">フォントを適用するコントロール</param>
        public void ApplyDefaultFont(Control control)
        {
            if (control == null) return;
            control.Font = DefaultFont;
        }

        /// <summary>
        /// コントロールとその子コントロールにデフォルトフォントを再帰的に適用します
        /// </summary>
        /// <param name="parentControl">フォントを適用する親コントロール</param>
        public void ApplyDefaultFontRecursively(Control parentControl)
        {
            if (parentControl == null) return;

            // 親コントロールにフォントを適用
            parentControl.Font = DefaultFont;

            // 子コントロールにも再帰的に適用
            foreach (Control control in parentControl.Controls)
            {
                ApplyDefaultFontRecursively(control);
            }
        }

        /// <summary>
        /// コントロールに指定したサイズのフォントを適用します
        /// </summary>
        /// <param name="control">フォントを適用するコントロール</param>
        /// <param name="fontSize">フォントサイズ種別</param>
        public void ApplyFont(Control control, FontSize fontSize)
        {
            if (control == null) return;
            control.Font = CreateFont(fontSize);
        }

        /// <summary>
        /// フォームに適切なフォントを適用します
        /// </summary>
        /// <param name="form">フォントを適用するフォーム</param>
        public void ApplyFontToForm(Form form)
        {
            if (form == null) return;

            form.Font = DefaultFont;

            // フォーム内のコントロールに適切なフォントを適用
            foreach (Control control in form.Controls)
            {
                ApplyDefaultFontRecursively(control);
            }
        }

        /// <summary>
        /// 翻訳結果表示コントロールに言語に応じたフォントを適用します
        /// </summary>
        /// <param name="control">フォントを適用するコントロール</param>
        /// <param name="language">翻訳言語</param>
        public void ApplyTranslationFont(Control control, TranslationLanguage language)
        {
            if (control == null) return;
            control.Font = CreateTranslationFont(language);
        }

        /// <summary>
        /// 指定したフォントサイズ値を設定します
        /// </summary>
        /// <param name="fontSize">フォントサイズの種類</param>
        /// <param name="value">設定する値</param>
        public void SetFontSize(FontSize fontSize, float value)
        {
            if (value <= 0) return;

            switch (fontSize)
            {
                case FontSize.Default:
                    _defaultFontSize = value;
                    break;
                case FontSize.Translation:
                    _translationFontSize = value;
                    break;
                case FontSize.Title:
                    _titleFontSize = value;
                    break;
                case FontSize.Small:
                    _smallFontSize = value;
                    break;
            }
        }

        /// <summary>
        /// 日本語翻訳用フォントファイルを読み込みます
        /// </summary>
        /// <param name="fontFilePath">日本語フォントファイルのパス</param>
        /// <returns>読み込みに成功した場合はtrue、それ以外はfalse</returns>
        public bool LoadJapaneseFontFile(string fontFilePath)
        {
            if (string.IsNullOrEmpty(fontFilePath) || !File.Exists(fontFilePath))
            {
                Logger.Instance.Warning("FontManager", $"指定された日本語フォントファイルが見つかりません: {fontFilePath}");
                return false;
            }

            try
            {
                // フォントを追加
                _privateFontCollection.AddFontFile(fontFilePath);
                _jpFontPath = fontFilePath;
                _isJpFontLoaded = true;

                Logger.Instance.Info("FontManager", $"日本語フォントを読み込みました: {fontFilePath}");
                return true;
            }
            catch (Exception ex)
            {
                Logger.Instance.Error("FontManager", $"日本語フォント読み込み中にエラーが発生しました", ex);
                _isJpFontLoaded = false;
                return false;
            }
        }

        /// <summary>
        /// 英語翻訳用フォントファイルを読み込みます
        /// </summary>
        /// <param name="fontFilePath">英語フォントファイルのパス</param>
        /// <returns>読み込みに成功した場合はtrue、それ以外はfalse</returns>
        public bool LoadEnglishFontFile(string fontFilePath)
        {
            if (string.IsNullOrEmpty(fontFilePath) || !File.Exists(fontFilePath))
            {
                Logger.Instance.Warning("FontManager", $"指定された英語フォントファイルが見つかりません: {fontFilePath}");
                return false;
            }

            try
            {
                // フォントを追加
                _privateFontCollection.AddFontFile(fontFilePath);
                _enFontPath = fontFilePath;
                _isEnFontLoaded = true;

                Logger.Instance.Info("FontManager", $"英語フォントを読み込みました: {fontFilePath}");
                return true;
            }
            catch (Exception ex)
            {
                Logger.Instance.Error("FontManager", $"英語フォント読み込み中にエラーが発生しました", ex);
                _isEnFontLoaded = false;
                return false;
            }
        }

        #endregion

        #region プライベートメソッド

        /// <summary>
        /// フォントマネージャーを初期化します
        /// </summary>
        private void Initialize()
        {
            try
            {
                // システムフォントのリストを作成
                UpdateAvailableFontsList();

                // 言語別フォントを読み込む
                LoadTranslationFonts();

                // フォントの利用可能状態をログに記録
                LogFontAvailability();
            }
            catch (Exception ex)
            {
                Logger.Instance.Error("FontManager", $"FontManager初期化中にエラーが発生しました", ex);
            }
        }

        /// <summary>
        /// 翻訳用の言語別フォントを読み込みます
        /// </summary>
        private void LoadTranslationFonts()
        {
            // アプリケーションのベースディレクトリ
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;

            // フォントディレクトリ
            string fontDir = Path.Combine(baseDir, "Fonts");

            // 日本語フォントの読み込み
            string jpFontPath = Path.Combine(fontDir, "LINESeedJP_A_TTF_Rg.ttf");
            if (File.Exists(jpFontPath))
            {
                LoadJapaneseFontFile(jpFontPath);
            }

            // 英語フォントの読み込み
            string enFontPath = Path.Combine(fontDir, "LINESeedSans_A_Rg.ttf");
            if (File.Exists(enFontPath))
            {
                LoadEnglishFontFile(enFontPath);
            }
        }

        /// <summary>
        /// フォントの利用可能状態をログに記録します
        /// </summary>
        private void LogFontAvailability()
        {
            if (_isJpFontLoaded)
            {
                Logger.Instance.Info("FontManager", "日本語翻訳用フォント(LINESeedJP)が利用可能です。");
            }
            else
            {
                Logger.Instance.Info("FontManager", $"日本語翻訳用フォント(LINESeedJP)が利用できないため、デフォルトフォントを使用します。");
            }

            if (_isEnFontLoaded)
            {
                Logger.Instance.Info("FontManager", "英語翻訳用フォント(LINESeedSans)が利用可能です。");
            }
            else
            {
                Logger.Instance.Info("FontManager", $"英語翻訳用フォント(LINESeedSans)が利用できないため、デフォルトフォントを使用します。");
            }
        }

        /// <summary>
        /// プライベートフォントコレクションをクリアします
        /// </summary>
        private void ClearPrivateFontCollection()
        {
            try
            {
                if (_privateFontCollection != null)
                {
                    // privateフォントコレクションをクリアする（.NETでは直接的な方法がない）
                    _isJpFontLoaded = false;
                    _isEnFontLoaded = false;
                    _jpFontPath = string.Empty;
                    _enFontPath = string.Empty;
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.Error("FontManager", $"フォントコレクションのクリア中にエラーが発生しました", ex);
            }
        }

        /// <summary>
        /// システムに利用可能なフォントのリストを更新します
        /// </summary>
        private void UpdateAvailableFontsList()
        {
            _availableFontFamilies.Clear();

            try
            {
                InstalledFontCollection installedFontCollection = new InstalledFontCollection();
                foreach (FontFamily family in installedFontCollection.Families)
                {
                    _availableFontFamilies.Add(family.Name);
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.Error("FontManager", $"利用可能フォントリスト更新中にエラーが発生しました", ex);
            }
        }

        /// <summary>
        /// 指定したフォント名がシステムに存在するかを確認します
        /// </summary>
        /// <param name="fontName">確認するフォント名</param>
        /// <returns>フォントが存在する場合はtrue、それ以外はfalse</returns>
        private bool IsFontAvailable(string fontName)
        {
            return _availableFontFamilies.Contains(fontName);
        }

        /// <summary>
        /// 指定したフォントサイズ種別に対応する実際のサイズ値を取得します
        /// </summary>
        /// <param name="fontSize">フォントサイズ種別</param>
        /// <returns>フォントサイズ値</returns>
        private float GetFontSizeValue(FontSize fontSize)
        {
            switch (fontSize)
            {
                case FontSize.Default:
                    return _defaultFontSize;
                case FontSize.Translation:
                    return _translationFontSize;
                case FontSize.Title:
                    return _titleFontSize;
                case FontSize.Small:
                    return _smallFontSize;
                default:
                    return _defaultFontSize;
            }
        }

        #endregion
    }

    /// <summary>
    /// フォントサイズの種類を定義する列挙型
    /// </summary>
    public enum FontSize
    {
        /// <summary>
        /// デフォルトのフォントサイズ（標準的なUI要素用）
        /// </summary>
        Default,

        /// <summary>
        /// 翻訳テキスト表示用の大きめフォントサイズ
        /// </summary>
        Translation,

        /// <summary>
        /// タイトルやヘッダー用のやや大きめフォントサイズ
        /// </summary>
        Title,

        /// <summary>
        /// 注釈や小さいテキスト用の小さめフォントサイズ
        /// </summary>
        Small
    }

    /// <summary>
    /// 翻訳言語を定義する列挙型
    /// </summary>
    public enum TranslationLanguage
    {
        /// <summary>
        /// デフォルト言語（システム設定に従う）
        /// </summary>
        Default,

        /// <summary>
        /// 日本語
        /// </summary>
        Japanese,

        /// <summary>
        /// 英語
        /// </summary>
        English
    }
}