using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Threading.Tasks;
using PaddleOCRSharp;
using GameTranslationOverlay.Core.Utils;

namespace GameTranslationOverlay.Core.OCR
{
    public class PaddleOcrEngine : IOcrEngine
    {
        private PaddleOCREngine _paddleOcr;
        private OCRModelConfig _modelConfig;
        private bool _isDisposed = false;

        // OCRパラメータ
        private PreprocessingOptions _preprocessingOptions;
        private bool _usePreprocessing = true;

        // エラー状態の追跡
        private int _consecutiveErrors = 0;
        private const int ERROR_THRESHOLD = 5;

        public PaddleOcrEngine(PreprocessingOptions preprocessingOptions = null)
        {
            _preprocessingOptions = preprocessingOptions ?? ImagePreprocessor.JapaneseTextPreset;
        }

        public async Task InitializeAsync()
        {
            await Task.Run(() =>
            {
                try
                {
                    // OCRモデル設定を最適化
                    _modelConfig = new OCRModelConfig();

                    // リフレクションを使用して安全にプロパティを設定
                    // paddleEnableLiteFP16プロパティが存在するか確認し設定
                    TrySetProperty(_modelConfig, "paddleEnableLiteFP16", true);

                    // clsプロパティが存在するか確認し設定
                    TrySetProperty(_modelConfig, "cls", true);

                    // recプロパティが存在するか確認し設定
                    TrySetProperty(_modelConfig, "rec", true);

                    // detプロパティが存在するか確認し設定
                    TrySetProperty(_modelConfig, "det", true);

                    // エンジン初期化
                    _paddleOcr = new PaddleOCREngine(_modelConfig);

                    Debug.WriteLine("PaddleOCR engine initialized successfully with optimized parameters");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Failed to initialize PaddleOCR: {ex.Message}");
                    throw;
                }
            });
        }

        public async Task<string> RecognizeTextAsync(Rectangle region)
        {
            if (_paddleOcr == null)
            {
                throw new InvalidOperationException("PaddleOCR engine not initialized");
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
                            // 検出と認識を実行
                            OCRResult result = _paddleOcr.DetectText(processedImage);

                            // 連続エラーカウンタをリセット
                            _consecutiveErrors = 0;

                            // 認識されたテキストを返す
                            if (result != null && !string.IsNullOrEmpty(result.Text))
                            {
                                return result.Text.Trim();
                            }
                            return string.Empty;
                        }
                        catch (Exception ex)
                        {
                            _consecutiveErrors++;

                            // 特定のエラーパターンに対する処理
                            if (ex.Message.Contains("boxClipToRectangle") ||
                                ex.Message.Contains("pixScanForForeground"))
                            {
                                // 境界エラーの場合は、画像をパディングして再試行
                                Debug.WriteLine($"Boundary error detected: {ex.Message}, retrying with padding");

                                try
                                {
                                    // パディングを追加して再処理
                                    using (var paddedImage = AddSafePadding(processedImage))
                                    {
                                        OCRResult retryResult = _paddleOcr.DetectText(paddedImage);
                                        if (retryResult != null && !string.IsNullOrEmpty(retryResult.Text))
                                        {
                                            return retryResult.Text.Trim();
                                        }
                                    }
                                }
                                catch (Exception retryEx)
                                {
                                    Debug.WriteLine($"Retry also failed: {retryEx.Message}");
                                }
                            }

                            // 連続エラーが閾値を超えたら前処理を無効化（一時的な対応策）
                            if (_consecutiveErrors >= ERROR_THRESHOLD && _usePreprocessing)
                            {
                                _usePreprocessing = false;
                                Debug.WriteLine("Too many consecutive errors, disabling preprocessing temporarily");
                            }

                            Debug.WriteLine($"Error in PaddleOCR recognition: {ex.Message}");
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

        // PaddleOCRSharpのAPIに合わせて追加
        public async Task<string> RecognizeDetailedAsync(Rectangle region)
        {
            // 既存のRecognizeTextAsyncメソッドの内容を再利用
            return await RecognizeTextAsync(region);
        }

        // 新規追加メソッド - テキスト領域の検出
        public async Task<List<TextRegion>> DetectTextRegionsAsync(Bitmap image)
        {
            if (_paddleOcr == null)
            {
                throw new InvalidOperationException("PaddleOCR engine not initialized");
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
                        // PaddleOCRを使用してテキスト領域を検出
                        OCRResult result = _paddleOcr.DetectText(processedImage);

                        // 連続エラーカウンタをリセット
                        _consecutiveErrors = 0;

                        // 結果の処理
                        ProcessOcrResult(result, regions);
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
                                    OCRResult retryResult = _paddleOcr.DetectText(paddedImage);
                                    ProcessOcrResult(retryResult, regions, 10); // パディング分のオフセット補正
                                }
                            }
                            catch (Exception retryEx)
                            {
                                Debug.WriteLine($"Retry also failed: {retryEx.Message}");
                            }
                        }

                        // 連続エラーが閾値を超えたら前処理を無効化
                        if (_consecutiveErrors >= ERROR_THRESHOLD && _usePreprocessing)
                        {
                            _usePreprocessing = false;
                            Debug.WriteLine("Too many consecutive errors, disabling preprocessing temporarily");
                        }

                        Debug.WriteLine($"Error detecting text regions: {ex.Message}");
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
        /// OCR結果を処理してTextRegionリストを作成
        /// </summary>
        private void ProcessOcrResult(OCRResult result, List<TextRegion> regions, int offsetCorrection = 0)
        {
            if (result == null || result.TextBlocks == null)
                return;

            try
            {
                foreach (var textBlock in result.TextBlocks)
                {
                    try
                    {
                        // リフレクションを使用せずに直接プロパティにアクセス
                        // ※APIバージョンによって異なる場合があります
                        Rectangle rect;
                        float confidence;
                        string text;

                        // 最新のPaddleOCRSharpのプロパティ構造に合わせて調整
                        // APIがさらに変更されている場合は適宜修正が必要
                        try
                        {
                            // Direct property access attempt
                            text = textBlock.Text;
                            confidence = textBlock.Score;

                            // さまざまなAPI形式に対応（プロパティ名の違いに備える）
                            int x, y, width, height;

                            // Attempt 1: Try to get Rect property
                            if (textBlock.GetType().GetProperty("Rect") != null)
                            {
                                var rectObject = textBlock.GetType().GetProperty("Rect").GetValue(textBlock);
                                x = (int)rectObject.GetType().GetProperty("X").GetValue(rectObject);
                                y = (int)rectObject.GetType().GetProperty("Y").GetValue(rectObject);
                                width = (int)rectObject.GetType().GetProperty("Width").GetValue(rectObject);
                                height = (int)rectObject.GetType().GetProperty("Height").GetValue(rectObject);
                            }
                            // Attempt 2: Try to get Box property
                            else if (textBlock.GetType().GetProperty("Box") != null)
                            {
                                var boxObject = textBlock.GetType().GetProperty("Box").GetValue(textBlock);
                                x = (int)boxObject.GetType().GetProperty("X").GetValue(boxObject);
                                y = (int)boxObject.GetType().GetProperty("Y").GetValue(boxObject);
                                width = (int)boxObject.GetType().GetProperty("Width").GetValue(boxObject);
                                height = (int)boxObject.GetType().GetProperty("Height").GetValue(boxObject);
                            }
                            // Attempt 3: Try to use direct coordinates
                            else if (textBlock.GetType().GetProperty("X") != null)
                            {
                                x = (int)textBlock.GetType().GetProperty("X").GetValue(textBlock);
                                y = (int)textBlock.GetType().GetProperty("Y").GetValue(textBlock);
                                width = (int)textBlock.GetType().GetProperty("Width").GetValue(textBlock);
                                height = (int)textBlock.GetType().GetProperty("Height").GetValue(textBlock);
                            }
                            else
                            {
                                // Fallback for unknown API format
                                throw new InvalidOperationException("Unknown TextBlock property structure");
                            }

                            // パディングオフセットの修正
                            if (offsetCorrection > 0)
                            {
                                x -= offsetCorrection;
                                y -= offsetCorrection;

                                // 境界チェック
                                if (x < 0) x = 0;
                                if (y < 0) y = 0;
                            }

                            rect = new Rectangle(x, y, width, height);
                        }
                        catch (Exception propEx)
                        {
                            Debug.WriteLine($"Property access error: {propEx.Message}, falling back to reflection");

                            // リフレクションを使用して情報を取得（下位互換性）
                            text = GetPropertyValueSafely<string>(textBlock, "Text", "");
                            confidence = GetPropertyValueSafely<float>(textBlock, "Score", 0.0f);

                            var rectObj = GetPropertyValueSafely<object>(textBlock, "Rect", null) ??
                                         GetPropertyValueSafely<object>(textBlock, "Box", null);

                            if (rectObj != null)
                            {
                                int x = GetPropertyValueSafely<int>(rectObj, "X", 0);
                                int y = GetPropertyValueSafely<int>(rectObj, "Y", 0);
                                int width = GetPropertyValueSafely<int>(rectObj, "Width", 0);
                                int height = GetPropertyValueSafely<int>(rectObj, "Height", 0);

                                // パディングオフセットの修正
                                if (offsetCorrection > 0)
                                {
                                    x -= offsetCorrection;
                                    y -= offsetCorrection;

                                    // 境界チェック
                                    if (x < 0) x = 0;
                                    if (y < 0) y = 0;
                                }

                                rect = new Rectangle(x, y, width, height);
                            }
                            else
                            {
                                // Fallback for unknown structure
                                rect = new Rectangle(0, 0, 100, 20);
                            }
                        }

                        // 有効な範囲と認識テキストを確認
                        if (IsValidRegion(rect) && !string.IsNullOrWhiteSpace(text))
                        {
                            regions.Add(new TextRegion(rect, text, confidence));
                        }
                    }
                    catch (Exception textBlockEx)
                    {
                        Debug.WriteLine($"Error processing text block: {textBlockEx.Message}");
                        continue;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error processing OCR result: {ex.Message}");
            }
        }

        /// <summary>
        /// 安全にプロパティ値を取得する
        /// </summary>
        private T GetPropertyValueSafely<T>(object obj, string propertyName, T defaultValue)
        {
            try
            {
                var prop = obj.GetType().GetProperty(propertyName);
                if (prop != null)
                {
                    var value = prop.GetValue(obj);
                    if (value != null)
                    {
                        return (T)Convert.ChangeType(value, typeof(T));
                    }
                }
            }
            catch
            {
                // エラーを無視して既定値を返す
            }

            return defaultValue;
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
        /// プロパティを安全に設定するヘルパーメソッド
        /// </summary>
        private bool TrySetProperty(object obj, string propertyName, object value)
        {
            try
            {
                var property = obj.GetType().GetProperty(propertyName);
                if (property != null && property.CanWrite)
                {
                    property.SetValue(obj, value);
                    return true;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to set property {propertyName}: {ex.Message}");
            }
            return false;
        }

        /// <summary>
        /// 有効な領域かどうかをチェック
        /// </summary>
        private bool IsValidRegion(Rectangle rect)
        {
            // 負の座標や無効なサイズをチェック
            if (rect.X < 0 || rect.Y < 0 || rect.Width <= 0 || rect.Height <= 0)
                return false;

            // 極端に大きい値をチェック（誤検出の可能性）
            if (rect.Width > 2000 || rect.Height > 2000)
                return false;

            return true;
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

        public void Dispose()
        {
            if (!_isDisposed)
            {
                _paddleOcr?.Dispose();
                _isDisposed = true;
            }
        }
    }
}