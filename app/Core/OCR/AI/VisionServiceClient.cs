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
using GameTranslationOverlay.Properties;

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
            _httpClient.Timeout = TimeSpan.FromSeconds(60); // タイムアウトを60秒に延長
            _keyProtector = ApiMultiKeyProtector.Instance;

            // HTTP要求ヘッダーのデフォルト設定
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("GameTranslationOverlay/1.0");

            Debug.WriteLine("VisionServiceClient: 初期化完了");
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
            // VisionServiceClient.cs - GetOpenAIApiKey メソッド
            try
            {
                // ApiMultiKeyProtectorを使用してAPIキーを取得
                string apiKey = _keyProtector.GetApiKey(ApiMultiKeyProtector.ApiProvider.OpenAI);

                // 通常のOpenAIキーが取得できた場合はそれを返す
                if (!string.IsNullOrEmpty(apiKey))
                {
                    return apiKey;
                }

                // エラーログ
                Debug.WriteLine("OpenAI APIキーの取得に失敗しました");
                return string.Empty;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"OpenAI APIキーの取得中にエラー: {ex.Message}");
                return string.Empty;
            }
        }

        /// <summary>
        /// Vision APIキーを取得します
        /// </summary>
        private string GetVisionApiKey()
        {
            try
            {
                // Vision用のOpenAIキーを検索
                var keyInfoList = _keyProtector.GetKeyInfoList(ApiMultiKeyProtector.ApiProvider.OpenAI);
                if (keyInfoList.Any(k => (string)k["KeyId"] == "vision" && (bool)k["IsActive"]))
                {
                    // "vision"キーIDのキーを復号化
                    byte[] encryptedBytes = Convert.FromBase64String(Resources.EncryptedVisionApiKey);
                    byte[] decryptedBytes = System.Security.Cryptography.ProtectedData.Unprotect(
                        encryptedBytes,
                        null,
                        System.Security.Cryptography.DataProtectionScope.CurrentUser);
                    return Encoding.UTF8.GetString(decryptedBytes);
                }

                // Vision特定のキーがなければ通常のOpenAIキーを返す
                return GetOpenAIApiKey();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Vision APIキーの取得中にエラー: {ex.Message}");
                return string.Empty;
            }
        }

        /// <summary>
        /// Gemini APIキーを取得します
        /// </summary>
        private string GetGeminiApiKey()
        {
            try
            {
                // ApiMultiKeyProtectorを使用してAPIキーを取得
                string apiKey = _keyProtector.GetApiKey(ApiMultiKeyProtector.ApiProvider.GoogleGemini);

                // キーが取得できた場合はそれを返す
                if (!string.IsNullOrEmpty(apiKey))
                {
                    return apiKey;
                }

                // エラーログ
                Debug.WriteLine("Gemini APIキーの取得に失敗しました");
                return string.Empty;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Gemini APIキーの取得中にエラー: {ex.Message}");
                return string.Empty;
            }
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
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();

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
                    max_tokens = 1500  // トークン数を増やして長いテキストにも対応
                };

                string json = JsonConvert.SerializeObject(requestBody);
                StringContent content = new StringContent(json, Encoding.UTF8, "application/json");

                Debug.WriteLine("GPT-4 Vision APIリクエスト送信中...");

                // キャンセルトークンの作成（タイムアウト管理用）
                using (var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(55)))
                {
                    HttpResponseMessage response = await _httpClient.PostAsync(OPENAI_API_URL, content, cts.Token);

                    if (!response.IsSuccessStatusCode)
                    {
                        string errorContent = await response.Content.ReadAsStringAsync();
                        Debug.WriteLine($"GPT-4 Vision APIエラー: {response.StatusCode} - {errorContent}");

                        // エラーコードに応じた処理
                        if ((int)response.StatusCode == 429) // 429 = Too Many Requests
                        {
                            throw new Exception("APIレート制限に達しました。しばらく待ってから再試行してください。");
                        }
                        else if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                        {
                            throw new Exception("API認証エラー: APIキーが無効または期限切れです。");
                        }
                        else
                        {
                            throw new HttpRequestException($"GPT-4 Vision APIエラー: {response.StatusCode} - {errorContent.Substring(0, Math.Min(200, errorContent.Length))}");
                        }
                    }

                    string responseContent = await response.Content.ReadAsStringAsync();

                    // レスポンスの長さをログに記録
                    int responseLength = responseContent.Length;
                    stopwatch.Stop();
                    Debug.WriteLine($"GPT-4 Vision APIレスポンス受信: 長さ={responseLength}バイト, 処理時間={stopwatch.ElapsedMilliseconds}ms");

                    // 簡易的なレスポンス検証
                    if (string.IsNullOrWhiteSpace(responseContent) || !responseContent.Contains("choices"))
                    {
                        throw new Exception("GPT-4 Vision APIから無効なレスポンスが返されました。");
                    }

                    return responseContent;
                }
            }
            catch (TaskCanceledException)
            {
                stopwatch.Stop();
                Debug.WriteLine($"GPT-4 Vision APIリクエストがタイムアウトしました（{stopwatch.ElapsedMilliseconds}ms経過）");
                throw new TimeoutException("GPT-4 Vision APIリクエストがタイムアウトしました。ネットワーク接続を確認してください。");
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                Debug.WriteLine($"GPT-4 Vision APIリクエスト中にエラーが発生しました: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Gemini Pro Vision APIにリクエストを送信します
        /// </summary>
        private async Task<string> SendRequestToGeminiVision(string apiKey, string base64Image)
        {
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();

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
            },
                    generationConfig = new
                    {
                        temperature = 0.1,  // 創造性を抑え、より確実な抽出を促進
                        maxOutputTokens = 1500  // より長い応答を許可
                    }
                };

                string json = JsonConvert.SerializeObject(requestBody);
                StringContent content = new StringContent(json, Encoding.UTF8, "application/json");

                string apiUrlWithKey = $"{GEMINI_API_URL}?key={apiKey}";
                Debug.WriteLine("Gemini Pro Vision APIリクエスト送信中...");

                // キャンセルトークンの作成（タイムアウト管理用）
                using (var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(55)))
                {
                    HttpResponseMessage response = await _httpClient.PostAsync(apiUrlWithKey, content, cts.Token);

                    if (!response.IsSuccessStatusCode)
                    {
                        string errorContent = await response.Content.ReadAsStringAsync();
                        Debug.WriteLine($"Gemini Pro Vision APIエラー: {response.StatusCode} - {errorContent}");

                        // エラーコードに応じた処理
                        if ((int)response.StatusCode == 429) // 429 = Too Many Requests
                        {
                            throw new Exception("APIレート制限に達しました。しばらく待ってから再試行してください。");
                        }
                        else if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                        {
                            throw new Exception("API認証エラー: APIキーが無効または期限切れです。");
                        }
                        else
                        {
                            throw new HttpRequestException($"Gemini Pro Vision APIエラー: {response.StatusCode} - {errorContent.Substring(0, Math.Min(200, errorContent.Length))}");
                        }
                    }

                    string responseContent = await response.Content.ReadAsStringAsync();

                    // レスポンスの長さをログに記録
                    int responseLength = responseContent.Length;
                    stopwatch.Stop();
                    Debug.WriteLine($"Gemini Pro Vision APIレスポンス受信: 長さ={responseLength}バイト, 処理時間={stopwatch.ElapsedMilliseconds}ms");

                    // 簡易的なレスポンス検証
                    if (string.IsNullOrWhiteSpace(responseContent) || !responseContent.Contains("candidates"))
                    {
                        throw new Exception("Gemini Pro Vision APIから無効なレスポンスが返されました。");
                    }

                    return responseContent;
                }
            }
            catch (TaskCanceledException)
            {
                stopwatch.Stop();
                Debug.WriteLine($"Gemini Pro Vision APIリクエストがタイムアウトしました（{stopwatch.ElapsedMilliseconds}ms経過）");
                throw new TimeoutException("Gemini Pro Vision APIリクエストがタイムアウトしました。ネットワーク接続を確認してください。");
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
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
                if (string.IsNullOrWhiteSpace(text))
                {
                    Debug.WriteLine("JSON抽出: 入力テキストが空です");
                    return null;
                }

                // 標準的なJSONブロックをパターン検索
                int jsonStart = text.IndexOf('[');
                int jsonEnd = text.LastIndexOf(']');

                if (jsonStart >= 0 && jsonEnd > jsonStart)
                {
                    string jsonCandidate = text.Substring(jsonStart, jsonEnd - jsonStart + 1);
                    // 簡易的な検証（正しいJSONかどうか試行）
                    try
                    {
                        JsonConvert.DeserializeObject(jsonCandidate);
                        Debug.WriteLine("標準形式のJSONブロックを抽出しました");
                        return jsonCandidate;
                    }
                    catch
                    {
                        // JSONとして解析できない場合は次の方法を試す
                        Debug.WriteLine("標準形式のJSONブロック抽出に失敗、次の方法を試行");
                    }
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
                        string jsonCandidate = text.Substring(codeStart, codeEnd - codeStart).Trim();
                        try
                        {
                            JsonConvert.DeserializeObject(jsonCandidate);
                            Debug.WriteLine("JSONコードブロックからJSONを抽出しました");
                            return jsonCandidate;
                        }
                        catch
                        {
                            Debug.WriteLine("JSONコードブロックからの抽出に失敗、次の方法を試行");
                        }
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
                        if ((codeContent.StartsWith("[") && codeContent.EndsWith("]")) ||
                            (codeContent.StartsWith("{") && codeContent.EndsWith("}")))
                        {
                            try
                            {
                                JsonConvert.DeserializeObject(codeContent);
                                Debug.WriteLine("一般コードブロックからJSONを抽出しました");
                                return codeContent;
                            }
                            catch
                            {
                                Debug.WriteLine("一般コードブロックからの抽出に失敗");
                            }
                        }
                    }
                }

                // 最後の手段として、文字列内のJSONらしき部分を探す
                int bracketStart = text.IndexOf('[');
                if (bracketStart >= 0)
                {
                    // 対応する閉じ括弧を見つける
                    int bracketCount = 1;
                    int pos = bracketStart + 1;
                    while (pos < text.Length && bracketCount > 0)
                    {
                        if (text[pos] == '[') bracketCount++;
                        else if (text[pos] == ']') bracketCount--;
                        pos++;
                    }

                    if (bracketCount == 0)
                    {
                        string jsonCandidate = text.Substring(bracketStart, pos - bracketStart);
                        try
                        {
                            JsonConvert.DeserializeObject(jsonCandidate);
                            Debug.WriteLine("テキスト内からJSONらしき部分を抽出しました");
                            return jsonCandidate;
                        }
                        catch
                        {
                            Debug.WriteLine("テキスト内のJSON抽出に失敗");
                        }
                    }
                }

                Debug.WriteLine("テキスト内からJSONを抽出できませんでした");
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

        /// <summary>
        /// 画像からテキスト領域を抽出（最適なAIサービスを自動選択）
        /// </summary>
        /// <param name="image">テキスト抽出対象の画像</param>
        /// <param name="isJapaneseText">日本語テキストかどうか</param>
        /// <returns>検出されたテキスト領域のリスト</returns>
        public async Task<List<TextRegion>> ExtractTextFromImage(Bitmap image, bool isJapaneseText)
        {
            try
            {
                List<TextRegion> regions = new List<TextRegion>();
                Exception primaryException = null;

                // 言語に基づいて最適なAPIを選択
                try
                {
                    if (isJapaneseText)
                    {
                        // 日本語テキストの場合はGPT-4 Visionを使用
                        regions = await ExtractTextWithGpt4Vision(image);
                    }
                    else
                    {
                        // 英語などのテキストの場合はGemini Pro Visionを使用
                        regions = await ExtractTextWithGeminiVision(image);
                    }

                    // 成功した場合はそのまま返す
                    return regions;
                }
                catch (Exception ex)
                {
                    // エラーを記録して代替APIを試す
                    primaryException = ex;
                    Debug.WriteLine($"プライマリAIサービスでのテキスト抽出に失敗: {ex.Message}");
                }

                // 代替APIで再試行
                try
                {
                    Debug.WriteLine("代替AIサービスを使用して再試行します...");
                    if (isJapaneseText)
                    {
                        // プライマリが失敗したらGemini Visionを試す
                        regions = await ExtractTextWithGeminiVision(image);
                    }
                    else
                    {
                        // プライマリが失敗したらGPT-4 Visionを試す
                        regions = await ExtractTextWithGpt4Vision(image);
                    }

                    // 代替APIが成功した場合
                    Debug.WriteLine("代替AIサービスでのテキスト抽出に成功しました");
                    return regions;
                }
                catch (Exception fallbackEx)
                {
                    // 両方のAPIが失敗した場合
                    Debug.WriteLine($"代替AIサービスでもテキスト抽出に失敗: {fallbackEx.Message}");
                    throw new AggregateException("すべてのAIサービスでテキスト抽出に失敗しました",
                        new[] { primaryException, fallbackEx });
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"テキスト抽出中にエラーが発生しました: {ex.Message}");
                throw;
            }
        }
    }
}