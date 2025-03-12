// E:\dev\game-translation-overlay-csharp\app\Core\OCR\AI\VisionServiceClient.cs
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Net.Http.Headers;
using GameTranslationOverlay.Core.OCR;
using GameTranslationOverlay.Core.Security;
using GameTranslationOverlay.Core.Diagnostics;
using GameTranslationOverlay.Core.Configuration;

namespace GameTranslationOverlay.Core.OCR.AI
{
    /// <summary>
    /// AIビジョンサービス（GPT-4 VisionとGemini Pro Vision）と通信するクライアント
    /// </summary>
    public class VisionServiceClient
    {
        private readonly HttpClient _httpClient;
        private readonly ApiMultiKeyProtector _keyProtector;
        // Loggerをprivateフィールドではなくstaticメソッド呼び出しで使用する

        private const string OPENAI_API_URL = "https://api.openai.com/v1/chat/completions";
        private const string GEMINI_API_URL = "https://generativelanguage.googleapis.com/v1beta/models/gemini-pro-vision:generateContent";

        /// <summary>
        /// VisionServiceClientの新しいインスタンスを初期化します
        /// </summary>
        public VisionServiceClient()
        {
            _httpClient = new HttpClient();
            _httpClient.Timeout = TimeSpan.FromSeconds(30);
            _keyProtector = ApiMultiKeyProtector.Instance;
            // Loggerのインスタンス化は行わない
        }

        /// <summary>
        /// 画像からOpenAI GPT-4 Visionを使用してテキスト領域を抽出します
        /// </summary>
        /// <param name="image">テキスト抽出対象の画像</param>
        /// <returns>検出されたテキスト領域のリスト</returns>
        public async Task<List<TextRegion>> ExtractTextWithGpt4Vision(Bitmap image)
        {
            try
            {
                // 実際のAPIキー取得方法を使用
                string apiKey = GetOpenAIApiKey();
                if (string.IsNullOrEmpty(apiKey))
                {
                    // Loggerインスタンスを使用せずにDebug.WriteLineを使用
                    Debug.WriteLine("GPT-4 Vision APIキーが設定されていません");
                    throw new InvalidOperationException("GPT-4 Vision APIキーが設定されていません");
                }

                string base64Image = BitmapToBase64(image);
                string response = await SendRequestToGpt4Vision(apiKey, base64Image);
                return ParseGpt4VisionResponse(response, image.Width, image.Height);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"GPT-4 Visionによるテキスト抽出エラー: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 画像からGoogle Gemini Pro Visionを使用してテキスト領域を抽出します
        /// </summary>
        /// <param name="image">テキスト抽出対象の画像</param>
        /// <returns>検出されたテキスト領域のリスト</returns>
        public async Task<List<TextRegion>> ExtractTextWithGeminiVision(Bitmap image)
        {
            try
            {
                // 実際のAPIキー取得方法を使用
                string apiKey = GetGeminiApiKey();
                if (string.IsNullOrEmpty(apiKey))
                {
                    Debug.WriteLine("Gemini Pro Vision APIキーが設定されていません");
                    throw new InvalidOperationException("Gemini Pro Vision APIキーが設定されていません");
                }

                string base64Image = BitmapToBase64(image);
                string response = await SendRequestToGeminiVision(apiKey, base64Image);
                return ParseGeminiVisionResponse(response, image.Width, image.Height);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Gemini Pro Visionによるテキスト抽出エラー: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// OpenAI APIキーを取得します
        /// </summary>
        private string GetOpenAIApiKey()
        {
            // 実際のプロジェクトでのAPIキー取得方法に合わせて実装
            // 例: AppSettings.Instance.OpenAIApiKey を使用
            // または ApiKeyProtector.Instance.GetKey("OpenAI") など

            // 仮実装
            return AppSettings.Instance.OpenAIApiKey;
        }

        /// <summary>
        /// Gemini APIキーを取得します
        /// </summary>
        private string GetGeminiApiKey()
        {
            // 実際のプロジェクトでのAPIキー取得方法に合わせて実装
            // 例: AppSettings.Instance.GeminiApiKey を使用

            // 仮実装
            return AppSettings.Instance.GeminiApiKey;
        }

        /// <summary>
        /// ビットマップをBase64エンコードされた文字列に変換します
        /// </summary>
        private string BitmapToBase64(Bitmap image)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                image.Save(ms, System.Drawing.Imaging.ImageFormat.Jpeg);
                byte[] imageBytes = ms.ToArray();
                return Convert.ToBase64String(imageBytes);
            }
        }

        /// <summary>
        /// GPT-4 Vision APIにリクエストを送信します
        /// </summary>
        private async Task<string> SendRequestToGpt4Vision(string apiKey, string base64Image)
        {
            try
            {
                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
                _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                // プロンプトの作成
                var requestBody = new
                {
                    model = "gpt-4-vision-preview",
                    messages = new[]
                    {
                        new
                        {
                            role = "user",
                            content = new object[]
                            {
                                new { type = "text", text = "Extract all visible text from this game screenshot. For each text element, provide the exact text content and its position (x, y, width, height) in the image. Format as JSON with an array of text regions. Be extremely accurate with the text content." },
                                new
                                {
                                    type = "image_url",
                                    image_url = new
                                    {
                                        url = $"data:image/jpeg;base64,{base64Image}"
                                    }
                                }
                            }
                        }
                    },
                    max_tokens = 1000
                };

                string json = JsonConvert.SerializeObject(requestBody);
                StringContent content = new StringContent(json, Encoding.UTF8, "application/json");

                Debug.WriteLine("GPT-4 Vision APIリクエスト送信中...");
                HttpResponseMessage response = await _httpClient.PostAsync(OPENAI_API_URL, content);

                if (!response.IsSuccessStatusCode)
                {
                    string errorContent = await response.Content.ReadAsStringAsync();
                    Debug.WriteLine($"GPT-4 Vision APIエラー: {response.StatusCode} - {errorContent}");
                    throw new HttpRequestException($"GPT-4 Vision APIエラー: {response.StatusCode}");
                }

                string responseContent = await response.Content.ReadAsStringAsync();
                Debug.WriteLine("GPT-4 Vision APIレスポンス受信: " + responseContent.Substring(0, Math.Min(100, responseContent.Length)) + "...");
                return responseContent;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"GPT-4 Vision APIリクエスト中にエラーが発生しました: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Gemini Pro Vision APIにリクエストを送信します
        /// </summary>
        private async Task<string> SendRequestToGeminiVision(string apiKey, string base64Image)
        {
            try
            {
                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                // プロンプトの作成
                var requestBody = new
                {
                    contents = new[]
                    {
                        new
                        {
                            parts = new object[]
                            {
                                new { text = "Extract all visible text from this game screenshot. For each text element, provide the exact text content and its position (x, y, width, height) in the image. Format as JSON with an array of text regions. Be extremely accurate with the text content." },
                                new
                                {
                                    inline_data = new
                                    {
                                        mime_type = "image/jpeg",
                                        data = base64Image
                                    }
                                }
                            }
                        }
                    }
                };

                string json = JsonConvert.SerializeObject(requestBody);
                StringContent content = new StringContent(json, Encoding.UTF8, "application/json");

                string apiUrlWithKey = $"{GEMINI_API_URL}?key={apiKey}";
                Debug.WriteLine("Gemini Pro Vision APIリクエスト送信中...");
                HttpResponseMessage response = await _httpClient.PostAsync(apiUrlWithKey, content);

                if (!response.IsSuccessStatusCode)
                {
                    string errorContent = await response.Content.ReadAsStringAsync();
                    Debug.WriteLine($"Gemini Pro Vision APIエラー: {response.StatusCode} - {errorContent}");
                    throw new HttpRequestException($"Gemini Pro Vision APIエラー: {response.StatusCode}");
                }

                string responseContent = await response.Content.ReadAsStringAsync();
                Debug.WriteLine("Gemini Pro Vision APIレスポンス受信: " + responseContent.Substring(0, Math.Min(100, responseContent.Length)) + "...");
                return responseContent;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Gemini Pro Vision APIリクエスト中にエラーが発生しました: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// GPT-4 Visionのレスポンスを解析してTextRegionのリストに変換します
        /// </summary>
        private List<TextRegion> ParseGpt4VisionResponse(string response, int imageWidth, int imageHeight)
        {
            try
            {
                List<TextRegion> result = new List<TextRegion>();

                JObject responseObj = JObject.Parse(response);
                string contentText = responseObj["choices"]?[0]?["message"]?["content"]?.ToString();

                if (string.IsNullOrEmpty(contentText))
                {
                    Debug.WriteLine("GPT-4 Visionレスポンスからテキストコンテンツが見つかりませんでした");
                    return result;
                }

                // JSONブロックを抽出する
                string jsonContent = ExtractJsonFromText(contentText);
                if (string.IsNullOrEmpty(jsonContent))
                {
                    Debug.WriteLine("GPT-4 VisionレスポンスからJSONが見つかりませんでした");
                    return result;
                }

                // JSONを解析してTextRegionに変換
                try
                {
                    JArray regionsArray = JArray.Parse(jsonContent);
                    foreach (JToken token in regionsArray)
                    {
                        string text = token["text"]?.ToString();
                        double x = token["x"]?.Value<double>() ?? 0;
                        double y = token["y"]?.Value<double>() ?? 0;
                        double width = token["width"]?.Value<double>() ?? 0;
                        double height = token["height"]?.Value<double>() ?? 0;

                        // 相対座標から絶対ピクセル座標に変換
                        Rectangle bounds = ConvertToPixelBounds(x, y, width, height, imageWidth, imageHeight);

                        if (!string.IsNullOrEmpty(text) && bounds.Width > 0 && bounds.Height > 0)
                        {
                            result.Add(new TextRegion
                            {
                                Text = text,
                                Bounds = bounds,
                                Confidence = 0.99f // GPT-4 Visionは高精度なので高い信頼度を設定
                            });
                        }
                    }
                }
                catch (JsonException ex)
                {
                    Debug.WriteLine($"JSONの解析に失敗しました: {ex.Message}");

                    // フォールバック: テキスト全体を1つのTextRegionとして返す
                    result.Add(new TextRegion
                    {
                        Text = contentText,
                        Bounds = new Rectangle(0, 0, imageWidth, imageHeight),
                        Confidence = 0.7f
                    });
                }

                return result;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"GPT-4 Visionレスポンスの解析中にエラーが発生しました: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Gemini Pro Visionのレスポンスを解析してTextRegionのリストに変換します
        /// </summary>
        private List<TextRegion> ParseGeminiVisionResponse(string response, int imageWidth, int imageHeight)
        {
            try
            {
                List<TextRegion> result = new List<TextRegion>();

                JObject responseObj = JObject.Parse(response);
                string contentText = responseObj["candidates"]?[0]?["content"]?["parts"]?[0]?["text"]?.ToString();

                if (string.IsNullOrEmpty(contentText))
                {
                    Debug.WriteLine("Gemini Pro Visionレスポンスからテキストコンテンツが見つかりませんでした");
                    return result;
                }

                // JSONブロックを抽出する
                string jsonContent = ExtractJsonFromText(contentText);
                if (string.IsNullOrEmpty(jsonContent))
                {
                    Debug.WriteLine("Gemini Pro VisionレスポンスからJSONが見つかりませんでした");
                    return result;
                }

                // JSONを解析してTextRegionに変換
                try
                {
                    JArray regionsArray = JArray.Parse(jsonContent);
                    foreach (JToken token in regionsArray)
                    {
                        string text = token["text"]?.ToString();
                        double x = token["x"]?.Value<double>() ?? 0;
                        double y = token["y"]?.Value<double>() ?? 0;
                        double width = token["width"]?.Value<double>() ?? 0;
                        double height = token["height"]?.Value<double>() ?? 0;

                        // 相対座標から絶対ピクセル座標に変換
                        Rectangle bounds = ConvertToPixelBounds(x, y, width, height, imageWidth, imageHeight);

                        if (!string.IsNullOrEmpty(text) && bounds.Width > 0 && bounds.Height > 0)
                        {
                            result.Add(new TextRegion
                            {
                                Text = text,
                                Bounds = bounds,
                                Confidence = 0.95f // Gemini Pro Visionは高精度だが、GPT-4よりやや低めに設定
                            });
                        }
                    }
                }
                catch (JsonException ex)
                {
                    Debug.WriteLine($"JSONの解析に失敗しました: {ex.Message}");

                    // フォールバック: テキスト全体を1つのTextRegionとして返す
                    result.Add(new TextRegion
                    {
                        Text = contentText,
                        Bounds = new Rectangle(0, 0, imageWidth, imageHeight),
                        Confidence = 0.7f
                    });
                }

                return result;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Gemini Pro Visionレスポンスの解析中にエラーが発生しました: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// テキスト内のJSONブロックを抽出します
        /// </summary>
        private string ExtractJsonFromText(string text)
        {
            try
            {
                // 標準的なJSONブロックをパターン検索
                int jsonStart = text.IndexOf('[');
                int jsonEnd = text.LastIndexOf(']');

                if (jsonStart >= 0 && jsonEnd > jsonStart)
                {
                    return text.Substring(jsonStart, jsonEnd - jsonStart + 1);
                }

                // コードブロック内のJSONを検索（Markdown形式の場合）
                const string jsonCodeBlockStart = "```json";
                const string codeBlockEnd = "```";

                int codeStart = text.IndexOf(jsonCodeBlockStart);
                if (codeStart >= 0)
                {
                    codeStart += jsonCodeBlockStart.Length;
                    int codeEnd = text.IndexOf(codeBlockEnd, codeStart);
                    if (codeEnd > codeStart)
                    {
                        return text.Substring(codeStart, codeEnd - codeStart).Trim();
                    }
                }

                // 一般的なコードブロック内のJSONを検索
                const string genericCodeBlockStart = "```";
                codeStart = text.IndexOf(genericCodeBlockStart);
                if (codeStart >= 0)
                {
                    codeStart += genericCodeBlockStart.Length;
                    int codeEnd = text.IndexOf(codeBlockEnd, codeStart);
                    if (codeEnd > codeStart)
                    {
                        string codeContent = text.Substring(codeStart, codeEnd - codeStart).Trim();
                        // JSONのように見えるか確認
                        if (codeContent.StartsWith("[") && codeContent.EndsWith("]"))
                        {
                            return codeContent;
                        }
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"JSON抽出中にエラーが発生しました: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 相対座標を実際のピクセル座標に変換します
        /// </summary>
        private Rectangle ConvertToPixelBounds(double x, double y, double width, double height, int imageWidth, int imageHeight)
        {
            try
            {
                // AIが返す座標が相対座標の場合（0.0〜1.0）、絶対座標に変換
                if (x <= 1.0 && y <= 1.0 && width <= 1.0 && height <= 1.0)
                {
                    return new Rectangle(
                        (int)(x * imageWidth),
                        (int)(y * imageHeight),
                        (int)(width * imageWidth),
                        (int)(height * imageHeight)
                    );
                }

                // AIが返す座標が絶対座標の場合、そのまま返す
                return new Rectangle(
                    (int)x,
                    (int)y,
                    (int)width,
                    (int)height
                );
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"座標変換中にエラーが発生しました: {ex.Message}");
                // エラー時はデフォルト値を返す
                return new Rectangle(0, 0, imageWidth, imageHeight);
            }
        }
    }
}