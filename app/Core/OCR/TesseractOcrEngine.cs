using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using GameTranslationOverlay.Core.Utils;
using Tesseract;

namespace GameTranslationOverlay.Core.OCR
{
    public class TesseractOcrEngine : IOcrEngine
    {
        private TesseractEngine _tesseractEngine;
        private bool _isDisposed = false;
        private string _language = "jpn";
        private string _dataPath = "tessdata";

        // OCRパラメータ
        private PreprocessingOptions _preprocessingOptions;
        private bool _usePreprocessing = true;
        private PageSegMode _pageSegMode = PageSegMode.Auto;

        // 連続エラー管理
        private int _consecutiveErrors = 0;
        private const int ERROR_THRESHOLD = 5;

        public TesseractOcrEngine(PreprocessingOptions preprocessingOptions = null)
        {
            _preprocessingOptions = preprocessingOptions ?? ImagePreprocessor.JapaneseTextPreset;
        }

        public async Task InitializeAsync()
        {
            await Task.Run(() =>
            {
                try
                {
                    if (!Directory.Exists(_dataPath))
                    {
                        Debug.WriteLine($"Directory does not exist: {_dataPath}");
                        throw new DirectoryNotFoundException($"Tesseract data directory not found: {_dataPath}");
                    }

                    // カスタムオプションを使用した初期化
                    _tesseractEngine = new TesseractEngine(_dataPath, _language, EngineMode.LstmOnly);

                    // 精度向上のための設定
                    _tesseractEngine.SetVariable("tessedit_char_whitelist", ""); // 制限なし
                    _tesseractEngine.SetVariable("tessedit_pageseg_mode", ((int)_pageSegMode).ToString());
                    _tesseractEngine.SetVariable("lstm_use_matrix", "1"); // LSTM行列を使用
                    _tesseractEngine.SetVariable("textord_heavy_nr", "1"); // 強力なノイズリダクション

                    Debug.WriteLine("Tesseract engine initialized successfully with optimized parameters");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Failed to initialize Tesseract: {ex.Message}");
                    throw;
                }
            });
        }

        public async Task<string> RecognizeTextAsync(Rectangle region)
        {
            if (_tesseractEngine == null)
            {
                throw new InvalidOperationException("Tesseract engine not initialized");
            }

            return await Task.Run(() =>
            {
                try
                {
                    using (Bitmap screenshot = ScreenCapture.CaptureRegion(region))
                    {
                        if (screenshot == null)
                        {
                            Debug.WriteLine("Screenshot is null");
                            return string.Empty;
                        }

                        // 画像前処理を適用（オプション）
                        Bitmap processedImage = _usePreprocessing
                            ? ImagePreprocessor.Preprocess(screenshot, _preprocessingOptions)
                            : screenshot;

                        try
                        {
                            using (var page = _tesseractEngine.Process(processedImage))
                            {
                                // ページセグメンテーションモード設定 - リフレクションを使用して安全に呼び出し
                                TrySetPageSegMode(page, _pageSegMode);

                                string result = page.GetText();

                                // 連続エラーカウンタをリセット
                                _consecutiveErrors = 0;

                                return result?.Trim() ?? string.Empty;
                            }
                        }
                        catch (Exception ex)
                        {
                            _consecutiveErrors++;

                            // 特定のエラーパターンに対する処理
                            if (ex.Message.Contains("boxClipToRectangle") ||
                                ex.Message.Contains("pixScanForForeground"))
                            {
                                Debug.WriteLine($"Boundary error detected: {ex.Message}, retrying with padding");

                                try
                                {
                                    // パディングを追加して再処理
                                    using (var paddedImage = AddSafePadding(processedImage))
                                    {
                                        using (var page = _tesseractEngine.Process(paddedImage))
                                        {
                                            TrySetPageSegMode(page, _pageSegMode);
                                            string retryResult = page.GetText();
                                            return retryResult?.Trim() ?? string.Empty;
                                        }
                                    }
                                }
                                catch (Exception retryEx)
                                {
                                    Debug.WriteLine($"Retry also failed: {retryEx.Message}");
                                }
                            }

                            // 連続エラーが閾値を超えたらページセグメンテーションモードを変更
                            if (_consecutiveErrors >= ERROR_THRESHOLD)
                            {
                                _usePreprocessing = !_usePreprocessing;

                                // セグメンテーションモードをローテーション
                                switch (_pageSegMode)
                                {
                                    case PageSegMode.Auto:
                                        _pageSegMode = PageSegMode.SingleBlock;
                                        break;
                                    case PageSegMode.SingleBlock:
                                        _pageSegMode = PageSegMode.SingleLine;
                                        break;
                                    case PageSegMode.SingleLine:
                                        _pageSegMode = PageSegMode.SingleWord;
                                        break;
                                    default:
                                        _pageSegMode = PageSegMode.Auto;
                                        break;
                                }

                                Debug.WriteLine($"Too many consecutive errors, changed page segmentation mode to {_pageSegMode} and preprocessing to {_usePreprocessing}");
                                _consecutiveErrors = 0;
                            }

                            Debug.WriteLine($"Error in Tesseract recognition: {ex.Message}");
                            return $"OCR Error: {ex.Message}";
                        }
                        finally
                        {
                            // 前処理画像が元画像と異なる場合のみ破棄
                            if (_usePreprocessing && processedImage != screenshot)
                            {
                                processedImage.Dispose();
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error capturing screenshot: {ex.Message}");
                    return $"OCR Error: {ex.Message}";
                }
            });
        }

        // 新規追加メソッド - テキスト領域の検出
        public async Task<List<TextRegion>> DetectTextRegionsAsync(Bitmap image)
        {
            if (_tesseractEngine == null)
            {
                throw new InvalidOperationException("Tesseract engine not initialized");
            }

            return await Task.Run(() =>
            {
                List<TextRegion> regions = new List<TextRegion>();

                try
                {
                    // 前処理適用（オプション）
                    Bitmap processedImage = _usePreprocessing
                        ? ImagePreprocessor.Preprocess(image, _preprocessingOptions)
                        : image;

                    try
                    {
                        using (var page = _tesseractEngine.Process(processedImage))
                        {
                            // ページセグメンテーションモード設定
                            TrySetPageSegMode(page, _pageSegMode);

                            using (var iterator = page.GetIterator())
                            {
                                // 開始点に移動
                                iterator.Begin();

                                do
                                {
                                    if (iterator.IsAtBeginningOf(PageIteratorLevel.TextLine))
                                    {
                                        Rect bounds;
                                        if (iterator.TryGetBoundingBox(PageIteratorLevel.TextLine, out bounds))
                                        {
                                            string lineText = iterator.GetText(PageIteratorLevel.TextLine);
                                            float confidence = iterator.GetConfidence(PageIteratorLevel.TextLine) / 100.0f;

                                            // 有効なテキストと領域のみ追加
                                            if (!string.IsNullOrWhiteSpace(lineText) && IsValidBounds(bounds))
                                            {
                                                regions.Add(new TextRegion(
                                                    new Rectangle(bounds.X1, bounds.Y1, bounds.Width, bounds.Height),
                                                    lineText.Trim(),
                                                    confidence
                                                ));
                                            }
                                        }
                                    }
                                } while (iterator.Next(PageIteratorLevel.TextLine));
                            }

                            // 連続エラーカウンタをリセット
                            _consecutiveErrors = 0;
                        }
                    }
                    catch (Exception ex)
                    {
                        _consecutiveErrors++;

                        // 特定のエラーパターンに対する処理
                        if (ex.Message.Contains("boxClipToRectangle") ||
                            ex.Message.Contains("pixScanForForeground"))
                        {
                            Debug.WriteLine($"Boundary error detected: {ex.Message}, retrying with padding");

                            try
                            {
                                // パディングを追加して再処理
                                using (var paddedImage = AddSafePadding(processedImage))
                                {
                                    using (var page = _tesseractEngine.Process(paddedImage))
                                    {
                                        TrySetPageSegMode(page, _pageSegMode);

                                        using (var iterator = page.GetIterator())
                                        {
                                            iterator.Begin();

                                            do
                                            {
                                                if (iterator.IsAtBeginningOf(PageIteratorLevel.TextLine))
                                                {
                                                    Rect bounds;
                                                    if (iterator.TryGetBoundingBox(PageIteratorLevel.TextLine, out bounds))
                                                    {
                                                        string lineText = iterator.GetText(PageIteratorLevel.TextLine);
                                                        float confidence = iterator.GetConfidence(PageIteratorLevel.TextLine) / 100.0f;

                                                        // パディング分のオフセット補正
                                                        if (IsValidBounds(bounds) && !string.IsNullOrWhiteSpace(lineText))
                                                        {
                                                            // パディング分の調整（10px）
                                                            int x = Math.Max(0, bounds.X1 - 10);
                                                            int y = Math.Max(0, bounds.Y1 - 10);

                                                            regions.Add(new TextRegion(
                                                                new Rectangle(x, y, bounds.Width, bounds.Height),
                                                                lineText.Trim(),
                                                                confidence
                                                            ));
                                                        }
                                                    }
                                                }
                                            } while (iterator.Next(PageIteratorLevel.TextLine));
                                        }
                                    }
                                }
                            }
                            catch (Exception retryEx)
                            {
                                Debug.WriteLine($"Retry also failed: {retryEx.Message}");
                            }
                        }

                        // 連続エラーが閾値を超えたらページセグメンテーションモードを変更
                        if (_consecutiveErrors >= ERROR_THRESHOLD)
                        {
                            _usePreprocessing = !_usePreprocessing;

                            // セグメンテーションモードをローテーション
                            switch (_pageSegMode)
                            {
                                case PageSegMode.Auto:
                                    _pageSegMode = PageSegMode.SingleBlock;
                                    break;
                                case PageSegMode.SingleBlock:
                                    _pageSegMode = PageSegMode.SingleLine;
                                    break;
                                case PageSegMode.SingleLine:
                                    _pageSegMode = PageSegMode.SingleWord;
                                    break;
                                default:
                                    _pageSegMode = PageSegMode.Auto;
                                    break;
                            }

                            Debug.WriteLine($"Too many consecutive errors, changed page segmentation mode to {_pageSegMode} and preprocessing to {_usePreprocessing}");
                            _consecutiveErrors = 0;
                        }

                        Debug.WriteLine($"Error detecting text regions with Tesseract: {ex.Message}");
                    }
                    finally
                    {
                        // 前処理画像が元画像と異なる場合のみ破棄
                        if (_usePreprocessing && processedImage != image)
                        {
                            processedImage.Dispose();
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error in pre-processing: {ex.Message}");
                }

                return regions;
            });
        }

        /// <summary>
        /// 安全なパディングを追加した画像を作成
        /// </summary>
        private Bitmap AddSafePadding(Bitmap image)
        {
            // 境界エラーを避けるため、画像の周囲に余白を追加
            int padding = 10;
            Bitmap paddedImage = new Bitmap(image.Width + (padding * 2), image.Height + (padding * 2));

            using (Graphics g = Graphics.FromImage(paddedImage))
            {
                g.Clear(Color.White); // 背景を白に
                g.DrawImage(image, padding, padding, image.Width, image.Height);
            }

            return paddedImage;
        }

        /// <summary>
        /// 有効な境界かどうかをチェック
        /// </summary>
        private bool IsValidBounds(Rect bounds)
        {
            // 負の座標や無効なサイズをチェック
            if (bounds.X1 < 0 || bounds.Y1 < 0 || bounds.Width <= 0 || bounds.Height <= 0)
                return false;

            // 極端に大きい値をチェック（誤検出の可能性）
            if (bounds.Width > 2000 || bounds.Height > 2000)
                return false;

            return true;
        }

        /// <summary>
        /// ページセグメンテーションモードを安全に設定
        /// </summary>
        private bool TrySetPageSegMode(Page page, PageSegMode mode)
        {
            try
            {
                // SetPageSegModeメソッドの存在を確認
                var method = page.GetType().GetMethod("SetPageSegMode");
                if (method != null)
                {
                    method.Invoke(page, new object[] { mode });
                    return true;
                }
                else
                {
                    // 代替方法：エンジンレベルで設定済みの場合は問題なし
                    Debug.WriteLine("SetPageSegMode not found, mode was set at engine level");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to set page segmentation mode: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 前処理オプションの設定
        /// </summary>
        public void SetPreprocessingOptions(PreprocessingOptions options)
        {
            _preprocessingOptions = options ?? ImagePreprocessor.JapaneseTextPreset;
        }

        /// <summary>
        /// 前処理の有効/無効を切り替え
        /// </summary>
        public void EnablePreprocessing(bool enable)
        {
            _usePreprocessing = enable;
            _consecutiveErrors = 0; // エラーカウンタもリセット
        }

        /// <summary>
        /// ページセグメンテーションモードを設定
        /// </summary>
        public void SetPageSegmentationMode(PageSegMode mode)
        {
            _pageSegMode = mode;
        }

        public void Dispose()
        {
            if (!_isDisposed)
            {
                _tesseractEngine?.Dispose();
                _isDisposed = true;
            }
        }
    }
}