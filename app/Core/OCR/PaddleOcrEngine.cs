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

        public async Task InitializeAsync()
        {
            await Task.Run(() =>
            {
                try
                {
                    _modelConfig = new OCRModelConfig();
                    _paddleOcr = new PaddleOCREngine(_modelConfig);
                    Debug.WriteLine("PaddleOCR engine initialized successfully");
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

                        OCRResult result = _paddleOcr.DetectText(screenshot);
                        if (result != null && result.Text != null)
                        {
                            return result.Text.Trim();
                        }
                        return string.Empty;
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error in PaddleOCR recognition: {ex.Message}");
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
                    // PaddleOCRを使用してテキスト領域を検出
                    OCRResult result = _paddleOcr.DetectText(image);

                    // APIの変更に対応するためにリフレクションを使用
                    if (result != null && result.TextBlocks != null && result.TextBlocks.Count > 0)
                    {
                        // 最初のアイテムでプロパティを確認（デバッグ用）
                        var firstBlock = result.TextBlocks[0];
                        var props = firstBlock.GetType().GetProperties();

                        // デバッグ出力
                        Debug.WriteLine("TextBlock properties:");
                        foreach (var prop in props)
                        {
                            Debug.WriteLine($"Property: {prop.Name}, Type: {prop.PropertyType.Name}");
                            try
                            {
                                var value = prop.GetValue(firstBlock);
                                if (value != null)
                                {
                                    Debug.WriteLine($"  Value: {value}");
                                }
                            }
                            catch { }
                        }

                        // 各テキストブロックをTextRegionオブジェクトに変換
                        foreach (var textBlock in result.TextBlocks)
                        {
                            try
                            {
                                // 座標情報を取得するための変数
                                int x = 0, y = 0, width = 0, height = 0;
                                float confidence = 0.0f;
                                string text = "";

                                // リフレクションを使用して様々なプロパティパターンを試す
                                // BoxやRect、直接座標プロパティなど

                                // テキスト
                                var textProp = textBlock.GetType().GetProperty("Text");
                                if (textProp != null)
                                {
                                    text = (string)textProp.GetValue(textBlock) ?? "";
                                }

                                // 信頼度
                                var scoreProp = textBlock.GetType().GetProperty("Score") ??
                                               textBlock.GetType().GetProperty("Confidence");
                                if (scoreProp != null)
                                {
                                    confidence = Convert.ToSingle(scoreProp.GetValue(textBlock));
                                }

                                // Box/Rectオブジェクトとしてのプロパティ
                                var boxProp = textBlock.GetType().GetProperty("Box") ??
                                             textBlock.GetType().GetProperty("Rect") ??
                                             textBlock.GetType().GetProperty("Rectangle");

                                if (boxProp != null)
                                {
                                    var box = boxProp.GetValue(textBlock);
                                    if (box != null)
                                    {
                                        var boxType = box.GetType();

                                        // Boxの各プロパティを取得
                                        var xProp = boxType.GetProperty("X");
                                        var yProp = boxType.GetProperty("Y");
                                        var wProp = boxType.GetProperty("Width");
                                        var hProp = boxType.GetProperty("Height");

                                        if (xProp != null && yProp != null && wProp != null && hProp != null)
                                        {
                                            x = Convert.ToInt32(xProp.GetValue(box));
                                            y = Convert.ToInt32(yProp.GetValue(box));
                                            width = Convert.ToInt32(wProp.GetValue(box));
                                            height = Convert.ToInt32(hProp.GetValue(box));
                                        }
                                    }
                                }
                                else
                                {
                                    // 直接座標プロパティを取得
                                    var xProp = textBlock.GetType().GetProperty("X");
                                    var yProp = textBlock.GetType().GetProperty("Y");
                                    var wProp = textBlock.GetType().GetProperty("Width");
                                    var hProp = textBlock.GetType().GetProperty("Height");

                                    if (xProp != null && yProp != null && wProp != null && hProp != null)
                                    {
                                        x = Convert.ToInt32(xProp.GetValue(textBlock));
                                        y = Convert.ToInt32(yProp.GetValue(textBlock));
                                        width = Convert.ToInt32(wProp.GetValue(textBlock));
                                        height = Convert.ToInt32(hProp.GetValue(textBlock));
                                    }
                                }

                                // 有効な座標が取得できた場合のみTextRegionを作成
                                if (width > 0 && height > 0)
                                {
                                    regions.Add(new TextRegion(
                                        new Rectangle(x, y, width, height),
                                        text,
                                        confidence
                                    ));
                                }
                                else
                                {
                                    Debug.WriteLine("Warning: Failed to extract valid rectangle coordinates from TextBlock");
                                }
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine($"Error processing text block: {ex.Message}");
                            }
                        }
                    }
                    // TextBlocksがない場合やAPIが変わっている場合の代替手段
                    else if (!string.IsNullOrEmpty(result?.Text))
                    {
                        // 全体を1つの領域として扱う
                        regions.Add(new TextRegion(
                            new Rectangle(0, 0, image.Width, image.Height),
                            result.Text,
                            1.0f
                        ));
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error detecting text regions: {ex.Message}");
                }

                return regions;
            });
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