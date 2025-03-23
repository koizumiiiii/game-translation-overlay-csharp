using GameTranslationOverlay.Core.Diagnostics;
using GameTranslationOverlay.Core.Security;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SharpDX;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static System.Net.Mime.MediaTypeNames;

namespace GameTranslationOverlay.Core.OCR.AI
{
    public class VisionServiceClient
    {
        private readonly HttpClient _httpClient;
        private const string OPENAI_API_URL = "https://api.openai.com/v1/chat/completions";
        private const string GEMINI_API_URL = "https://generativelanguage.googleapis.com/v1/models/gemini-1.5-pro:generateContent";
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
                    apiKey = ApiKeyProtector.Instance.GetDecryptedApiKey(); // OpenAI用

                    // デバッグ情報の記録（セキュリティのため一部だけ）
                    if (!string.IsNullOrEmpty(apiKey))
                    {
                        string partialKey = apiKey.Length > 5 ? apiKey.Substring(0, 5) + "..." : "empty";
                        Debug.WriteLine($"Using OpenAI API key: {partialKey}, length: {apiKey.Length}");
                    }

                    return await ExtractTextWithGPT4Vision(image, apiKey);
                }
                else
                {
                    // 非日本語テキストの場合はGemini Pro Visionを使用
                    apiKey = ApiKeyProtector.Instance.GetDecryptedGeminiApiKey(); // Gemini用の新メソッド

                    // デバッグ情報の記録（セキュリティのため一部だけ）
                    if (!string.IsNullOrEmpty(apiKey))
                    {
                        string partialKey = apiKey.Length > 5 ? apiKey.Substring(0, 5) + "..." : "empty";
                        Debug.WriteLine($"Using Gemini API key: {partialKey}, length: {apiKey.Length}");
                    }

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

            // 詳細なAIレスポンス分析
            LogAiResponse(response, regions, "GPT4Vision", image);

            Logger.Instance.LogDebug("VisionServiceClient", $"GPT-4 Visionが{regions.Count}個のテキスト領域を検出しました");

            // AIレスポンスについての詳細なログ
            if (regions.Count > 0)
            {
                Logger.Instance.LogDebug("VisionServiceClient", $"=== GPT-4 Vision 検出テキストサンプル ===");
                foreach (var region in regions.Take(Math.Min(3, regions.Count)))
                {
                    Logger.Instance.LogDebug("VisionServiceClient", $"テキスト: '{TruncateText(region.Text, 30)}', " +
                                            $"位置: [{region.Bounds.X},{region.Bounds.Y},{region.Bounds.Width},{region.Bounds.Height}], " +
                                            $"信頼度: {region.Confidence:F2}");
                }
            }
            else
            {
                Logger.Instance.LogWarning("GPT-4 Visionはテキスト領域を検出できませんでした");
            }

            return regions;
        }

        // テキストを指定の長さに切り詰める補助メソッド
        private string TruncateText(string text, int maxLength)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            return text.Length <= maxLength ? text : text.Substring(0, maxLength - 3) + "...";
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

            // 詳細なAIレスポンス分析
            LogAiResponse(response, regions, "GeminiVision", image);

            Logger.Instance.LogDebug("VisionServiceClient", $"Gemini Pro Visionが{regions.Count}個のテキスト領域を検出しました");

            // AIレスポンスについての詳細なログ
            if (regions.Count > 0)
            {
                Logger.Instance.LogDebug("VisionServiceClient", $"=== Gemini Vision 検出テキストサンプル ===");
                foreach (var region in regions.Take(Math.Min(3, regions.Count)))
                {
                    Logger.Instance.LogDebug("VisionServiceClient", $"テキスト: '{TruncateText(region.Text, 30)}', " +
                                            $"位置: [{region.Bounds.X},{region.Bounds.Y},{region.Bounds.Width},{region.Bounds.Height}], " +
                                            $"信頼度: {region.Confidence:F2}");
                }
            }
            else
            {
                Logger.Instance.LogWarning("Gemini Visionはテキスト領域を検出できませんでした");
            }

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
                // APIキーのフォーマットを確認（OpenAIのキーはsk-で始まるはず）
                if (!string.IsNullOrEmpty(apiKey) && !apiKey.StartsWith("sk-"))
                {
                    Logger.Instance.LogWarning("OpenAI API key format appears to be invalid (should start with 'sk-')");
                    Debug.WriteLine("Warning: OpenAI API key format appears to be invalid");
                }

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
                // APIキーのフォーマットを確認（GeminiのキーはAIzaから始まるはず）
                if (!string.IsNullOrEmpty(apiKey) && !apiKey.StartsWith("AIza"))
                {
                    string warningMessage = "Gemini API key format appears to be invalid (should start with 'AIza')";
                    Logger.Instance.LogWarning(warningMessage);
                    Debug.WriteLine(warningMessage);

                    // APIキーの長さを出力（セキュリティのため内容は出力しない）
                    Debug.WriteLine($"Key length: {apiKey.Length}, First 4 chars: {(apiKey.Length >= 4 ? apiKey.Substring(0, 4) : "less than 4 chars")}");
                }


                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                // 以下、現在のリクエスト作成と送信処理をそのまま使用
                // プロンプトの作成
                var requestBody = new
                {
                    contents = new[]
                    {
                        new
                        {
                            role = "user", // role フィールドの追加
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
                        temperature = 0.1,
                        maxOutputTokens = 1500
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

                // 解析前のデバッグログ
                Logger.Instance.LogDebug("VisionServiceClient", "Gemini Visionレスポンスの解析を開始します");

                JObject responseObj = JObject.Parse(response);
                string contentText = responseObj["candidates"]?[0]?["content"]?["parts"]?[0]?["text"]?.ToString();

                if (string.IsNullOrEmpty(contentText))
                {
                    Logger.Instance.LogError("Gemini Pro Visionレスポンスからテキストコンテンツが見つかりませんでした");
                    // レスポンス全体をデバッグログに保存
                    string responseLogPath = Path.Combine(_debugDir, $"gemini_empty_response_{DateTime.Now:yyyyMMdd_HHmmss}.json");
                    File.WriteAllText(responseLogPath, response);
                    Logger.Instance.LogDebug("VisionServiceClient", $"空のコンテンツを持つレスポンスを保存しました: {responseLogPath}");
                    return result;
                }

                // JSONブロックを抽出する
                Logger.Instance.LogDebug("VisionServiceClient", "JSONブロックの抽出を試みます");
                string jsonContent = ExtractJsonFromText(contentText);
                if (string.IsNullOrEmpty(jsonContent))
                {
                    Logger.Instance.LogError("Gemini Pro VisionレスポンスからJSONが見つかりませんでした");

                    // エラーログ用に抽出前のコンテンツを保存
                    string contentLogPath = Path.Combine(_debugDir, $"gemini_content_{DateTime.Now:yyyyMMdd_HHmmss}.txt");
                    File.WriteAllText(contentLogPath, contentText);
                    Logger.Instance.LogDebug("VisionServiceClient", $"Gemini Visionコンテンツを保存しました: {contentLogPath}");

                    // 代替のJSONフォーマット検索を試みる
                    Logger.Instance.LogDebug("VisionServiceClient", "代替のJSON抽出方法を試みます");
                    jsonContent = TryAlternativeJsonExtraction(contentText);
                    if (string.IsNullOrEmpty(jsonContent))
                    {
                        return result;
                    }
                }

                // JSONを解析してTextRegionに変換
                try
                {
                    Logger.Instance.LogDebug("VisionServiceClient", $"JSONの解析を開始します: {TruncateText(jsonContent, 100)}");
                    JArray regionsArray = JArray.Parse(jsonContent);
                    Logger.Instance.LogDebug("VisionServiceClient", $"抽出されたJSONに{regionsArray.Count}個のテキスト領域があります");

                    foreach (JToken token in regionsArray)
                    {
                        try
                        {
                            string text = token["text"]?.ToString();
                            double x = token["x"]?.Value<double>() ?? 0;
                            double y = token["y"]?.Value<double>() ?? 0;
                            double width = token["width"]?.Value<double>() ?? 0;
                            double height = token["height"]?.Value<double>() ?? 0;

                            // 値のログ記録
                            Logger.Instance.LogDebug("VisionServiceClient", $"解析されたテキスト領域: テキスト='{TruncateText(text, 20)}', " +
                                                    $"x={x}, y={y}, width={width}, height={height}");

                            // 相対座標から絶対ピクセル座標に変換
                            Rectangle bounds = ConvertToPixelBounds(x, y, width, height, imageWidth, imageHeight);
                            Logger.Instance.LogDebug("VisionServiceClient", $"変換後の座標: [{bounds.X},{bounds.Y},{bounds.Width},{bounds.Height}]");

                            if (!string.IsNullOrEmpty(text) && bounds.Width > 0 && bounds.Height > 0)
                            {
                                result.Add(new TextRegion
                                {
                                    Text = text,
                                    Bounds = bounds,
                                    Confidence = 0.95f // Gemini Pro Visionは高精度だが、GPT-4よりやや低めに設定
                                });
                            }
                            else
                            {
                                // この部分はループ内にあるため、text と bounds 変数にアクセスできる
                                string displayText = text ?? "[null]";
                                Logger.Instance.LogWarning($"無効なテキスト領域データをスキップ: テキスト='{displayText}', 座標=[{bounds.X},{bounds.Y},{bounds.Width},{bounds.Height}]");
                            }
                        }
                        catch (Exception tokenEx)
                        {
                            Logger.Instance.LogError($"テキスト領域の解析中にエラーが発生しました: {tokenEx.Message}", tokenEx);
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
                    Logger.Instance.LogDebug("VisionServiceClient", "フォールバック: テキスト全体を1つの領域として処理します");
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
        /// 代替のJSON抽出方法を試みる
        /// </summary>
        private string TryAlternativeJsonExtraction(string text)
        {
            try
            {
                // 最終手段: テキスト解析によるJSON構造の検出
                Logger.Instance.LogDebug("VisionServiceClient", "代替JSON抽出: テキスト解析による検出を試みます");

                // 各行を解析して配列を構築
                List<JObject> items = new List<JObject>();
                string[] lines = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

                foreach (string line in lines)
                {
                    // "領域X: テキスト「XXXX」、座標(x, y, width, height)" のようなパターンを検出
                    if (line.Contains("テキスト") && (line.Contains("座標") || line.Contains("位置")))
                    {
                        try
                        {
                            JObject item = new JObject();

                            // テキスト部分を抽出
                            int textStart = line.IndexOf("「");
                            int textEnd = line.IndexOf("」");
                            if (textStart >= 0 && textEnd > textStart)
                            {
                                string extractedText = line.Substring(textStart + 1, textEnd - textStart - 1);
                                item["text"] = extractedText;
                            }

                            // 座標部分を抽出
                            int coordStart = line.IndexOf("(");
                            int coordEnd = line.IndexOf(")");
                            if (coordStart >= 0 && coordEnd > coordStart)
                            {
                                string coords = line.Substring(coordStart + 1, coordEnd - coordStart - 1);
                                string[] parts = coords.Split(',');
                                if (parts.Length >= 4)
                                {
                                    double.TryParse(parts[0].Trim(), out double x);
                                    double.TryParse(parts[1].Trim(), out double y);
                                    double.TryParse(parts[2].Trim(), out double width);
                                    double.TryParse(parts[3].Trim(), out double height);

                                    item["x"] = x;
                                    item["y"] = y;
                                    item["width"] = width;
                                    item["height"] = height;
                                }
                            }

                            if (item.ContainsKey("text") && item.ContainsKey("x"))
                            {
                                items.Add(item);
                            }
                        }
                        catch (Exception ex)
                        {
                            Logger.Instance.LogDebug("VisionServiceClient", $"行の解析に失敗: {ex.Message}");
                        }
                    }
                }

                if (items.Count > 0)
                {
                    JArray array = new JArray(items);
                    Logger.Instance.LogDebug("VisionServiceClient", $"代替抽出で{items.Count}個のテキスト領域を見つけました");
                    return array.ToString();
                }

                Logger.Instance.LogDebug("VisionServiceClient", "代替JSONの抽出に失敗しました");
                return null;
            }
            catch (Exception ex)
            {
                Logger.Instance.LogError($"代替JSON抽出中にエラーが発生しました: {ex.Message}", ex);
                return null;
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

        /// <summary>
        /// AIからのレスポンスと抽出されたテキスト領域の詳細をログに記録します
        /// </summary>
        private void LogAiResponse(string response, List<TextRegion> extractedRegions, string serviceType, Bitmap originalImage)
        {
            try
            {
                // AIログ専用ディレクトリの作成
                string logDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "GameTranslationOverlay", "AILogs");

                Directory.CreateDirectory(logDir);
                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff");

                // 生のAIレスポンスを保存
                string rawResponsePath = Path.Combine(logDir, $"{serviceType}_raw_response_{timestamp}.json");
                File.WriteAllText(rawResponsePath, response);

                // 抽出されたテキスト領域の詳細を保存
                string regionsLogPath = Path.Combine(logDir, $"{serviceType}_extracted_regions_{timestamp}.txt");
                using (StreamWriter writer = new StreamWriter(regionsLogPath))
                {
                    writer.WriteLine($"抽出されたテキスト領域数: {extractedRegions.Count}\n");

                    for (int i = 0; i < extractedRegions.Count; i++)
                    {
                        var region = extractedRegions[i];
                        writer.WriteLine($"領域 #{i + 1}:");
                        writer.WriteLine($"  テキスト: \"{region.Text}\"");
                        writer.WriteLine($"  座標: [{region.Bounds.X}, {region.Bounds.Y}, {region.Bounds.Width}, {region.Bounds.Height}]");
                        writer.WriteLine($"  信頼度: {region.Confidence}");
                        writer.WriteLine();
                    }
                }

                // テキスト領域を視覚化した画像を保存
                if (originalImage != null && extractedRegions.Count > 0)
                {
                    string visualizationPath = Path.Combine(logDir, $"{serviceType}_visualization_{timestamp}.png");
                    SaveVisualization(originalImage, extractedRegions, visualizationPath);
                }

                Logger.Instance.LogDebug("VisionServiceClient", $"AIレスポンスログを保存しました: {rawResponsePath}");
                Logger.Instance.LogDebug("VisionServiceClient", $"抽出テキスト領域ログを保存しました: {regionsLogPath}");
            }
            catch (Exception ex)
            {
                Logger.Instance.LogError($"AIレスポンスのログ記録中にエラーが発生しました: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// テキスト領域を視覚化して画像として保存します
        /// </summary>
        private void SaveVisualization(Bitmap originalImage, List<TextRegion> regions, string outputPath)
        {
            try
            {
                // 元のイメージのコピーを作成
                using (Bitmap visualization = new Bitmap(originalImage))
                {
                    using (Graphics g = Graphics.FromImage(visualization))
                    {
                        // 各テキスト領域を描画
                        foreach (var region in regions)
                        {
                            // 領域を赤い枠で囲む
                            using (Pen pen = new Pen(Color.Red, 2))
                            {
                                g.DrawRectangle(pen, region.Bounds);
                            }

                            // テキスト内容を描画（短くする）
                            string shortText = region.Text.Length > 20
                                ? region.Text.Substring(0, 17) + "..."
                                : region.Text;

                            // テキストの背景を半透明の黒にして読みやすくする
                            using (Brush bgBrush = new SolidBrush(Color.FromArgb(128, 0, 0, 0)))
                            {
                                // テキスト測定
                                SizeF textSize = g.MeasureString(shortText, SystemFonts.DefaultFont);
                                g.FillRectangle(bgBrush,
                                    region.Bounds.X,
                                    region.Bounds.Y - textSize.Height - 2,
                                    textSize.Width,
                                    textSize.Height);
                            }

                            // テキストを白色で描画
                            using (Brush textBrush = new SolidBrush(Color.White))
                            {
                                g.DrawString(shortText, SystemFonts.DefaultFont, textBrush,
                                    region.Bounds.X, region.Bounds.Y - SystemFonts.DefaultFont.Height - 2);
                            }

                            // 信頼度の表示
                            string confidenceText = $"{region.Confidence:P0}";
                            g.DrawString(confidenceText, SystemFonts.DefaultFont, Brushes.Yellow,
                                region.Bounds.X, region.Bounds.Y + region.Bounds.Height + 2);
                        }
                    }

                    // 画像を保存
                    visualization.Save(outputPath, System.Drawing.Imaging.ImageFormat.Png);
                    Logger.Instance.LogDebug("VisionServiceClient", $"テキスト領域の視覚化を保存しました: {outputPath}");
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.LogError($"視覚化画像の保存中にエラーが発生しました: {ex.Message}", ex);
            }
        }
    }
}