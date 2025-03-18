using GameTranslationOverlay.Core.Diagnostics;
using GameTranslationOverlay.Core.Security;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace GameTranslationOverlay.Core.OCR.AI
{
    public class VisionServiceClient
    {
        private readonly HttpClient _httpClient;
        private const string OPENAI_API_URL = "https://api.openai.com/v1/chat/completions";
        private const string GEMINI_API_URL = "https://generativelanguage.googleapis.com/v1beta/models/gemini-pro-vision:generateContent";
        private readonly string _debugDir;

        public VisionServiceClient()
        {
            _httpClient = new HttpClient();
            _httpClient.Timeout = TimeSpan.FromSeconds(60);

            // デバッグディレクトリの作成
            _debugDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "GameTranslationOverlay", "Debug");

            if (!Directory.Exists(_debugDir))
            {
                Directory.CreateDirectory(_debugDir);
            }
        }

        /// <summary>
        /// 画像から最適なAIサービスを使用してテキストを抽出します
        /// </summary>
        /// <param name="image">テキスト抽出を行う画像</param>
        /// <param name="isJapaneseText">日本語テキストかどうか</param>
        /// <returns>テキスト領域のリスト</returns>
        public async Task<List<TextRegion>> ExtractTextFromImage(Bitmap image, bool isJapaneseText)
        {
            Logger.Instance.Info("VisionServiceClient", $"テキスト抽出開始: 日本語テキスト={isJapaneseText}");

            // 入力画像のデバッグ保存
            string imagePath = Path.Combine(_debugDir, $"ai_input_{DateTime.Now:yyyyMMdd_HHmmss}.png");
            try
            {
                image.Save(imagePath, System.Drawing.Imaging.ImageFormat.Png);
                Logger.Instance.LogDebug("VisionServiceClient", $"AI入力画像を保存しました: {imagePath}");
            }
            catch (Exception ex)
            {
                Logger.Instance.LogError($"AI入力画像の保存に失敗しました: {ex.Message}", ex);
            }

            try
            {
                // 適切なAPIキーを取得
                string apiKey;
                if (isJapaneseText)
                {
                    // 日本語テキストの場合はGPT-4 Visionを使用
                    // ApiKeyProtectorに必要なメソッドがない場合は一般的なGetDecryptedApiKeyを使用
                    apiKey = ApiKeyProtector.Instance.GetDecryptedApiKey();
                    return await ExtractTextWithGPT4Vision(image, apiKey);
                }
                else
                {
                    // 非日本語テキストの場合はGemini Pro Visionを使用
                    // ApiKeyProtectorに必要なメソッドがない場合は一般的なGetDecryptedApiKeyを使用
                    apiKey = ApiKeyProtector.Instance.GetDecryptedApiKey();
                    return await ExtractTextWithGeminiVision(image, apiKey);
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.LogError($"テキスト抽出中にエラーが発生しました: {ex.Message}", ex);
                throw;
            }
        }

        /// <summary>
        /// GPT-4 Visionを使用してテキストを抽出
        /// </summary>
        private async Task<List<TextRegion>> ExtractTextWithGPT4Vision(Bitmap image, string apiKey)
        {
            Logger.Instance.LogDebug("VisionServiceClient", "GPT-4 Visionによるテキスト抽出開始");

            // 画像のBase64エンコード
            string base64Image = ImageToBase64(image);

            // API呼び出し
            string response = await SendRequestToGpt4Vision(apiKey, base64Image);

            // レスポンス解析
            List<TextRegion> regions = ParseGpt4VisionResponse(response, image.Width, image.Height);

            Logger.Instance.LogDebug("VisionServiceClient", $"GPT-4 Visionが{regions.Count}個のテキスト領域を検出しました");
            return regions;
        }

        /// <summary>
        /// Gemini Pro Visionを使用してテキストを抽出
        /// </summary>
        private async Task<List<TextRegion>> ExtractTextWithGeminiVision(Bitmap image, string apiKey)
        {
            Logger.Instance.LogDebug("VisionServiceClient", "Gemini Pro Visionによるテキスト抽出開始");

            // 画像のBase64エンコード
            string base64Image = ImageToBase64(image);

            // API呼び出し
            string response = await SendRequestToGeminiVision(apiKey, base64Image);

            // レスポンス解析
            List<TextRegion> regions = ParseGeminiVisionResponse(response, image.Width, image.Height);

            Logger.Instance.LogDebug("VisionServiceClient", $"Gemini Pro Visionが{regions.Count}個のテキスト領域を検出しました");
            return regions;
        }

        /// <summary>
        /// 画像をBase64エンコードする
        /// </summary>
        private string ImageToBase64(Bitmap image)
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

                // リクエストをデバッグ用に保存
                string requestLogPath = Path.Combine(_debugDir, $"gpt4_request_{DateTime.Now:yyyyMMdd_HHmmss}.json");
                File.WriteAllText(requestLogPath, json);
                Logger.Instance.LogDebug("VisionServiceClient", $"GPT-4 Vision APIリクエストを保存しました: {requestLogPath}");

                StringContent content = new StringContent(json, Encoding.UTF8, "application/json");

                Debug.WriteLine("GPT-4 Vision APIリクエスト送信中...");
                Logger.Instance.LogDebug("VisionServiceClient", "GPT-4 Vision APIリクエスト送信中...");

                // キャンセルトークンの作成（タイムアウト管理用）
                using (var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(55)))
                {
                    HttpResponseMessage response = await _httpClient.PostAsync(OPENAI_API_URL, content, cts.Token);

                    if (!response.IsSuccessStatusCode)
                    {
                        string errorContent = await response.Content.ReadAsStringAsync();
                        Logger.Instance.LogError($"GPT-4 Vision APIエラー: {response.StatusCode} - {errorContent}");

                        // APIの使用履歴を記録
                        ApiUsageManager.Instance.RecordApiCall("GPT4Vision", false);

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

                    // レスポンスをデバッグ用に保存
                    string responseLogPath = Path.Combine(_debugDir, $"gpt4_response_{DateTime.Now:yyyyMMdd_HHmmss}.json");
                    File.WriteAllText(responseLogPath, responseContent);
                    Logger.Instance.LogDebug("VisionServiceClient", $"GPT-4 Vision APIレスポンスを保存しました: {responseLogPath}");

                    // APIの使用履歴を記録（成功）
                    ApiUsageManager.Instance.RecordApiCall("GPT4Vision", true);

                    // レスポンスの長さをログに記録
                    int responseLength = responseContent.Length;
                    stopwatch.Stop();
                    Logger.Instance.LogDebug("VisionServiceClient", $"GPT-4 Vision APIレスポンス受信: 長さ={responseLength}バイト, 処理時間={stopwatch.ElapsedMilliseconds}ms");

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
                string error = $"GPT-4 Vision APIリクエストがタイムアウトしました（{stopwatch.ElapsedMilliseconds}ms経過）";
                Logger.Instance.LogError(error);

                // APIの使用履歴を記録（失敗）
                ApiUsageManager.Instance.RecordApiCall("GPT4Vision", false);

                throw new TimeoutException(error);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                Logger.Instance.LogError($"GPT-4 Vision APIリクエスト中にエラーが発生しました: {ex.Message}", ex);

                // API使用を記録（明示的にマークされていない場合）
                ApiUsageManager.Instance.RecordApiCall("GPT4Vision", false);

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

                // リクエストをデバッグ用に保存
                string requestLogPath = Path.Combine(_debugDir, $"gemini_request_{DateTime.Now:yyyyMMdd_HHmmss}.json");
                File.WriteAllText(requestLogPath, json);
                Logger.Instance.LogDebug("VisionServiceClient", $"Gemini Vision APIリクエストを保存しました: {requestLogPath}");

                StringContent content = new StringContent(json, Encoding.UTF8, "application/json");

                string apiUrlWithKey = $"{GEMINI_API_URL}?key={apiKey}";
                Debug.WriteLine("Gemini Pro Vision APIリクエスト送信中...");
                Logger.Instance.LogDebug("VisionServiceClient", "Gemini Pro Vision APIリクエスト送信中...");

                // キャンセルトークンの作成（タイムアウト管理用）
                using (var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(55)))
                {
                    HttpResponseMessage response = await _httpClient.PostAsync(apiUrlWithKey, content, cts.Token);

                    if (!response.IsSuccessStatusCode)
                    {
                        string errorContent = await response.Content.ReadAsStringAsync();
                        Logger.Instance.LogError($"Gemini Pro Vision APIエラー: {response.StatusCode} - {errorContent}");

                        // APIの使用履歴を記録
                        ApiUsageManager.Instance.RecordApiCall("GeminiVision", false);

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

                    // レスポンスをデバッグ用に保存
                    string responseLogPath = Path.Combine(_debugDir, $"gemini_response_{DateTime.Now:yyyyMMdd_HHmmss}.json");
                    File.WriteAllText(responseLogPath, responseContent);
                    Logger.Instance.LogDebug("VisionServiceClient", $"Gemini Vision APIレスポンスを保存しました: {responseLogPath}");

                    // APIの使用履歴を記録（成功）
                    ApiUsageManager.Instance.RecordApiCall("GeminiVision", true);

                    // レスポンスの長さをログに記録
                    int responseLength = responseContent.Length;
                    stopwatch.Stop();
                    Logger.Instance.LogDebug("VisionServiceClient", $"Gemini Pro Vision APIレスポンス受信: 長さ={responseLength}バイト, 処理時間={stopwatch.ElapsedMilliseconds}ms");

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
                string error = $"Gemini Pro Vision APIリクエストがタイムアウトしました（{stopwatch.ElapsedMilliseconds}ms経過）";
                Logger.Instance.LogError(error);

                // APIの使用履歴を記録（失敗）
                ApiUsageManager.Instance.RecordApiCall("GeminiVision", false);

                throw new TimeoutException(error);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                Logger.Instance.LogError($"Gemini Pro Vision APIリクエスト中にエラーが発生しました: {ex.Message}", ex);

                // API使用を記録（明示的にマークされていない場合）
                ApiUsageManager.Instance.RecordApiCall("GeminiVision", false);

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
                    Logger.Instance.LogError("GPT-4 Visionレスポンスからテキストコンテンツが見つかりませんでした");
                    return result;
                }

                // JSONブロックを抽出する
                string jsonContent = ExtractJsonFromText(contentText);
                if (string.IsNullOrEmpty(jsonContent))
                {
                    Logger.Instance.LogError("GPT-4 VisionレスポンスからJSONが見つかりませんでした");

                    // エラーログ用に抽出前のコンテンツを保存
                    string contentLogPath = Path.Combine(_debugDir, $"gpt4_content_{DateTime.Now:yyyyMMdd_HHmmss}.txt");
                    File.WriteAllText(contentLogPath, contentText);
                    Logger.Instance.LogDebug("VisionServiceClient", $"GPT-4 Visionコンテンツを保存しました: {contentLogPath}");

                    return result;
                }

                // JSONを解析してTextRegionに変換
                try
                {
                    JArray regionsArray = JArray.Parse(jsonContent);
                    Logger.Instance.LogDebug("VisionServiceClient", $"抽出されたJSONに{regionsArray.Count}個のテキスト領域があります");

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
                    Logger.Instance.LogError($"JSONの解析に失敗しました: {ex.Message}", ex);

                    // JSONとしてパースできなかった内容をデバッグ用に保存
                    string jsonErrorPath = Path.Combine(_debugDir, $"gpt4_json_error_{DateTime.Now:yyyyMMdd_HHmmss}.json");
                    File.WriteAllText(jsonErrorPath, jsonContent);
                    Logger.Instance.LogDebug("VisionServiceClient", $"解析できなかったJSONを保存しました: {jsonErrorPath}");

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
                Logger.Instance.LogError($"GPT-4 Visionレスポンスの解析中にエラーが発生しました: {ex.Message}", ex);
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
                    Logger.Instance.LogError("Gemini Pro Visionレスポンスからテキストコンテンツが見つかりませんでした");
                    return result;
                }

                // JSONブロックを抽出する
                string jsonContent = ExtractJsonFromText(contentText);
                if (string.IsNullOrEmpty(jsonContent))
                {
                    Logger.Instance.LogError("Gemini Pro VisionレスポンスからJSONが見つかりませんでした");

                    // エラーログ用に抽出前のコンテンツを保存
                    string contentLogPath = Path.Combine(_debugDir, $"gemini_content_{DateTime.Now:yyyyMMdd_HHmmss}.txt");
                    File.WriteAllText(contentLogPath, contentText);
                    Logger.Instance.LogDebug("VisionServiceClient", $"Gemini Visionコンテンツを保存しました: {contentLogPath}");

                    return result;
                }

                // JSONを解析してTextRegionに変換
                try
                {
                    JArray regionsArray = JArray.Parse(jsonContent);
                    Logger.Instance.LogDebug("VisionServiceClient", $"抽出されたJSONに{regionsArray.Count}個のテキスト領域があります");

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
                    Logger.Instance.LogError($"JSONの解析に失敗しました: {ex.Message}", ex);

                    // JSONとしてパースできなかった内容をデバッグ用に保存
                    string jsonErrorPath = Path.Combine(_debugDir, $"gemini_json_error_{DateTime.Now:yyyyMMdd_HHmmss}.json");
                    File.WriteAllText(jsonErrorPath, jsonContent);
                    Logger.Instance.LogDebug("VisionServiceClient", $"解析できなかったJSONを保存しました: {jsonErrorPath}");

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
                Logger.Instance.LogError($"Gemini Pro Visionレスポンスの解析中にエラーが発生しました: {ex.Message}", ex);
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
                    Logger.Instance.LogDebug("VisionServiceClient", "JSON抽出: 入力テキストが空です");
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
                        Logger.Instance.LogDebug("VisionServiceClient", "標準形式のJSONブロックを抽出しました");
                        return jsonCandidate;
                    }
                    catch
                    {
                        // JSONとして解析できない場合は次の方法を試す
                        Logger.Instance.LogDebug("VisionServiceClient", "標準形式のJSONブロック抽出に失敗、次の方法を試行");
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
                            Logger.Instance.LogDebug("VisionServiceClient", "JSONコードブロックからJSONを抽出しました");
                            return jsonCandidate;
                        }
                        catch
                        {
                            Logger.Instance.LogDebug("VisionServiceClient", "JSONコードブロックからの抽出に失敗、次の方法を試行");
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
                                Logger.Instance.LogDebug("VisionServiceClient", "一般コードブロックからJSONを抽出しました");
                                return codeContent;
                            }
                            catch
                            {
                                Logger.Instance.LogDebug("VisionServiceClient", "一般コードブロックからの抽出に失敗");
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
                            Logger.Instance.LogDebug("VisionServiceClient", "テキスト内からJSONらしき部分を抽出しました");
                            return jsonCandidate;
                        }
                        catch
                        {
                            Logger.Instance.LogDebug("VisionServiceClient", "テキスト内のJSON抽出に失敗");
                        }
                    }
                }

                Logger.Instance.LogError("テキスト内からJSONを抽出できませんでした");

                // 元のテキスト全体をデバッグ用に保存（JSONの抽出に失敗した場合）
                string fullTextPath = Path.Combine(_debugDir, $"json_extraction_failed_{DateTime.Now:yyyyMMdd_HHmmss}.txt");
                File.WriteAllText(fullTextPath, text);
                Logger.Instance.LogDebug("VisionServiceClient", $"JSON抽出に失敗したテキストを保存しました: {fullTextPath}");

                return null;
            }
            catch (Exception ex)
            {
                Logger.Instance.LogError($"JSON抽出中にエラーが発生しました: {ex.Message}", ex);
                return null;
            }
        }

        /// <summary>
        /// 相対座標から絶対ピクセル座標に変換
        /// </summary>
        private Rectangle ConvertToPixelBounds(double x, double y, double width, double height, int imageWidth, int imageHeight)
        {
            // AIはしばしば0-1の相対座標、またはピクセル座標で返す場合がある
            // 1より大きい値はピクセル座標と仮定
            bool isRelative = (x <= 1 && y <= 1 && width <= 1 && height <= 1);

            int pixelX, pixelY, pixelWidth, pixelHeight;

            if (isRelative)
            {
                // 相対座標（0-1）からピクセル座標に変換
                pixelX = (int)(x * imageWidth);
                pixelY = (int)(y * imageHeight);
                pixelWidth = (int)(width * imageWidth);
                pixelHeight = (int)(height * imageHeight);
            }
            else
            {
                // すでにピクセル座標と仮定
                pixelX = (int)x;
                pixelY = (int)y;
                pixelWidth = (int)width;
                pixelHeight = (int)height;
            }

            // 正常な領域となるように境界を調整
            pixelX = Math.Max(0, Math.Min(pixelX, imageWidth - 1));
            pixelY = Math.Max(0, Math.Min(pixelY, imageHeight - 1));
            pixelWidth = Math.Max(1, Math.Min(pixelWidth, imageWidth - pixelX));
            pixelHeight = Math.Max(1, Math.Min(pixelHeight, imageHeight - pixelY));

            return new Rectangle(pixelX, pixelY, pixelWidth, pixelHeight);
        }
    }
}