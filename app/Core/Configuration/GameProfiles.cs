using System;
using System.Collections.Generic;
using System.IO;
using System.Diagnostics;
using System.Text.Json;
using GameTranslationOverlay.Core.OCR.AI;
using GameTranslationOverlay.Core.Security;
using GameTranslationOverlay.Core.Utils;
using GameTranslationOverlay.Core.Configuration;
using System.Linq;

namespace GameTranslationOverlay.Core.Configuration
{
    /// <summary>
    /// ゲームごとの最適化プロファイルを管理するクラス
    /// </summary>
    public class GameProfiles : IGameProfiles
    {
        private readonly Dictionary<string, OcrOptimizer.OptimalSettings> _profiles = new Dictionary<string, OcrOptimizer.OptimalSettings>();
        private readonly string _profilesFilePath;
        private const string ENCRYPTION_KEY = "GameTranslationOverlayProfiles";

        /// <summary>
        /// コンストラクタ
        /// </summary>
        public GameProfiles()
        {
            // 設定ファイルパスの設定
            string appDataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "GameTranslationOverlay");

            if (!Directory.Exists(appDataPath))
            {
                Directory.CreateDirectory(appDataPath);
            }

            _profilesFilePath = Path.Combine(appDataPath, "game_profiles.json");

            // 既存のプロファイルを読み込む
            LoadProfiles();
        }

        /// <summary>
        /// プロファイルの保存
        /// </summary>
        /// <param name="gameTitle">ゲームタイトル</param>
        /// <param name="settings">最適化設定</param>
        public void SaveProfile(string gameTitle, OcrOptimizer.OptimalSettings settings)
        {
            if (string.IsNullOrWhiteSpace(gameTitle) || settings == null)
                return;

            // プロファイルを更新または追加
            _profiles[gameTitle] = settings;

            // 全てのプロファイルを保存
            SaveProfiles();

            Debug.WriteLine($"ゲーム '{gameTitle}' のプロファイルを保存しました");

            bool exists = _profiles.ContainsKey(gameTitle);
            OnProfileChanged(gameTitle, exists ?
                ProfileChangedEventArgs.ChangeType.Updated :
                ProfileChangedEventArgs.ChangeType.Added);
        }

        /// <summary>
        /// プロファイルの取得
        /// </summary>
        /// <param name="gameTitle">ゲームタイトル</param>
        /// <returns>最適化設定（存在しない場合はnull）</returns>
        public OcrOptimizer.OptimalSettings GetProfile(string gameTitle)
        {
            if (string.IsNullOrWhiteSpace(gameTitle) || !_profiles.ContainsKey(gameTitle))
                return null;

            return _profiles[gameTitle];
        }

        /// <summary>
        /// プロファイルの削除
        /// </summary>
        /// <param name="gameTitle">ゲームタイトル</param>
        public void DeleteProfile(string gameTitle)
        {
            if (string.IsNullOrWhiteSpace(gameTitle) || !_profiles.ContainsKey(gameTitle))
                return;

            _profiles.Remove(gameTitle);
            SaveProfiles();

            Debug.WriteLine($"ゲーム '{gameTitle}' のプロファイルを削除しました");

            OnProfileChanged(gameTitle, ProfileChangedEventArgs.ChangeType.Deleted);
        }

        /// <summary>
        /// 全プロファイルの取得
        /// </summary>
        /// <returns>ゲームタイトルのリスト</returns>
        public List<string> GetAllProfileNames()
        {
            return new List<string>(_profiles.Keys);
        }

        /// <summary>
        /// プロファイルが存在するか確認
        /// </summary>
        /// <param name="gameTitle">ゲームタイトル</param>
        /// <returns>存在する場合はtrue</returns>
        public bool HasProfile(string gameTitle)
        {
            return !string.IsNullOrWhiteSpace(gameTitle) && _profiles.ContainsKey(gameTitle);
        }

        /// <summary>
        /// すべてのプロファイルを保存
        /// </summary>
        private void SaveProfiles()
        {
            try
            {
                // シリアライズ用のデータ構造に変換
                var serializableData = new Dictionary<string, object>();
                foreach (var pair in _profiles)
                {
                    var settings = pair.Value;
                    serializableData[pair.Key] = new
                    {
                        ConfidenceThreshold = settings.ConfidenceThreshold,
                        PreprocessingOptions = new
                        {
                            ContrastLevel = settings.PreprocessingOptions.ContrastLevel,
                            BrightnessLevel = settings.PreprocessingOptions.BrightnessLevel,
                            SharpnessLevel = settings.PreprocessingOptions.SharpnessLevel,
                            NoiseReduction = settings.PreprocessingOptions.NoiseReduction,
                            ScaleFactor = settings.PreprocessingOptions.ScaleFactor,
                            Threshold = settings.PreprocessingOptions.Threshold,
                            Padding = settings.PreprocessingOptions.Padding
                        },
                        LastOptimized = settings.LastOptimized.ToString("o"),
                        OptimizationAttempts = settings.OptimizationAttempts,
                        IsOptimized = settings.IsOptimized
                    };
                }

                // JSON形式でシリアライズ
                string json = JsonSerializer.Serialize(serializableData, new JsonSerializerOptions
                {
                    WriteIndented = true
                });

                // ファイルに保存（暗号化オプション）
                string encryptedJson = EncryptionHelper.EncryptWithAes(json, ENCRYPTION_KEY);
                File.WriteAllText(_profilesFilePath, encryptedJson);

                Debug.WriteLine($"ゲームプロファイルを {_profilesFilePath} に保存しました");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ゲームプロファイル保存エラー: {ex.Message}");
            }
        }

        /// <summary>
        /// プロファイルの読み込み
        /// </summary>
        private void LoadProfiles()
        {
            try
            {
                // ファイルが存在しない場合はスキップ
                if (!File.Exists(_profilesFilePath))
                {
                    Debug.WriteLine("ゲームプロファイルファイルが見つかりません");
                    return;
                }

                // ファイルから読み込み（暗号化されている場合は復号化）
                string encryptedJson = File.ReadAllText(_profilesFilePath);
                string json = EncryptionHelper.DecryptWithAes(encryptedJson, ENCRYPTION_KEY);

                if (string.IsNullOrWhiteSpace(json))
                {
                    // 復号化に失敗した場合は暗号化されていないとみなして直接読み込み
                    json = encryptedJson;
                }

                // JSONデシリアライズ
                var serializableData = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json);
                _profiles.Clear();

                foreach (var pair in serializableData)
                {
                    string gameTitle = pair.Key;
                    var element = pair.Value;

                    // 設定を復元
                    var settings = new OcrOptimizer.OptimalSettings
                    {
                        ConfidenceThreshold = element.GetProperty("ConfidenceThreshold").GetSingle(),
                        PreprocessingOptions = new PreprocessingOptions
                        {
                            ContrastLevel = element.GetProperty("PreprocessingOptions").GetProperty("ContrastLevel").GetSingle(),
                            BrightnessLevel = element.GetProperty("PreprocessingOptions").GetProperty("BrightnessLevel").GetSingle(),
                            SharpnessLevel = element.GetProperty("PreprocessingOptions").GetProperty("SharpnessLevel").GetSingle(),
                            NoiseReduction = element.GetProperty("PreprocessingOptions").GetProperty("NoiseReduction").GetInt32(),
                            ScaleFactor = element.GetProperty("PreprocessingOptions").GetProperty("ScaleFactor").GetSingle(),
                            Threshold = element.GetProperty("PreprocessingOptions").GetProperty("Threshold").GetInt32(),
                            Padding = element.GetProperty("PreprocessingOptions").GetProperty("Padding").GetInt32()
                        },
                        LastOptimized = DateTime.Parse(element.GetProperty("LastOptimized").GetString()),
                        OptimizationAttempts = element.GetProperty("OptimizationAttempts").GetInt32(),
                        IsOptimized = element.GetProperty("IsOptimized").GetBoolean()
                    };

                    _profiles[gameTitle] = settings;
                }

                Debug.WriteLine($"{_profiles.Count} 件のゲームプロファイルを読み込みました");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ゲームプロファイル読み込みエラー: {ex.Message}");
                // 読み込みに失敗した場合は空の状態を維持
                _profiles.Clear();
            }
        }

        // GameProfiles.csに追加する内容（LoadProfiles()メソッドの後）

        /// <summary>
        /// プロファイルの変更イベント
        /// </summary>
        public event EventHandler<ProfileChangedEventArgs> ProfileChanged;

        /// <summary>
        /// プロファイル変更イベント引数
        /// </summary>
        public class ProfileChangedEventArgs : EventArgs
        {
            public string GameTitle { get; }
            public ChangeType Type { get; }

            public enum ChangeType
            {
                Added,
                Updated,
                Deleted
            }

            public ProfileChangedEventArgs(string gameTitle, ChangeType type)
            {
                GameTitle = gameTitle;
                Type = type;
            }
        }

        /// <summary>
        /// 既存のプロファイルへの上書き前に確認が必要かどうか
        /// </summary>
        public bool ConfirmOverwrite { get; set; } = true;

        /// <summary>
        /// プロファイル変更イベントを発火
        /// </summary>
        private void OnProfileChanged(string gameTitle, ProfileChangedEventArgs.ChangeType type)
        {
            ProfileChanged?.Invoke(this, new ProfileChangedEventArgs(gameTitle, type));
        }

        // 以下は既存のSaveProfileとDeleteProfileメソッドを修正せず、
        // 代わりに各メソッドの最後にイベント通知コードを追加します

        // 既存のSaveProfileメソッド内で、プロファイル保存後に以下のコードを追加
        // Debug.WriteLine($"ゲーム '{gameTitle}' のプロファイルを保存しました"); の後に:
        // OnProfileChanged(gameTitle, _profiles.ContainsKey(gameTitle) ? 
        //     ProfileChangedEventArgs.ChangeType.Updated : 
        //     ProfileChangedEventArgs.ChangeType.Added);

        // 既存のDeleteProfileメソッド内で、プロファイル削除後に以下のコードを追加
        // Debug.WriteLine($"ゲーム '{gameTitle}' のプロファイルを削除しました"); の後に:
        // OnProfileChanged(gameTitle, ProfileChangedEventArgs.ChangeType.Deleted);

        /// <summary>
        /// 検索条件に一致するプロファイルを検索
        /// </summary>
        /// <param name="searchText">検索テキスト（部分一致）</param>
        /// <returns>一致するゲームタイトルのリスト</returns>
        public List<string> SearchProfiles(string searchText)
        {
            if (string.IsNullOrWhiteSpace(searchText))
                return GetAllProfileNames();

            var results = new List<string>();
            foreach (var title in _profiles.Keys)
            {
                if (title.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    results.Add(title);
                }
            }

            Debug.WriteLine($"検索キーワード '{searchText}' で {results.Count} 件のプロファイルが見つかりました");
            return results;
        }

        /// <summary>
        /// 実行中のプロセスからゲームプロファイルを検索
        /// </summary>
        /// <returns>一致するゲームタイトルと実行中プロセス名のペアのリスト</returns>
        public List<KeyValuePair<string, string>> DetectRunningGames()
        {
            var result = new List<KeyValuePair<string, string>>();

            try
            {
                // 実行中のプロセスを取得
                var processes = System.Diagnostics.Process.GetProcesses();

                // プロセス名とウィンドウタイトルのマッピングを作成
                Dictionary<string, string> runningProcesses = new Dictionary<string, string>();
                foreach (var process in processes)
                {
                    try
                    {
                        if (!string.IsNullOrEmpty(process.MainWindowTitle))
                        {
                            runningProcesses[process.ProcessName] = process.MainWindowTitle;
                        }
                    }
                    catch { /* プロセスにアクセスできない場合は無視 */ }
                }

                // プロファイルとプロセスを照合
                foreach (var title in _profiles.Keys)
                {
                    foreach (var process in runningProcesses)
                    {
                        // プロセス名とゲームタイトルの一致をチェック
                        if (title.IndexOf(process.Key, StringComparison.OrdinalIgnoreCase) >= 0 ||
                            process.Key.IndexOf(title, StringComparison.OrdinalIgnoreCase) >= 0 ||
                            process.Value.IndexOf(title, StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            result.Add(new KeyValuePair<string, string>(title, process.Key));
                            break;
                        }
                    }
                }

                Debug.WriteLine($"{result.Count} 件の実行中ゲームを検出しました");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"実行中ゲーム検出エラー: {ex.Message}");
            }

            return result;
        }

        /// <summary>
        /// 複数のプロファイルを統合
        /// </summary>
        /// <param name="profiles">統合するプロファイルのタイトルリスト</param>
        /// <param name="newTitle">新しいプロファイルのタイトル</param>
        /// <returns>統合が成功したかどうか</returns>
        public bool MergeProfiles(List<string> profiles, string newTitle)
        {
            if (profiles == null || profiles.Count < 2 || string.IsNullOrWhiteSpace(newTitle))
                return false;

            try
            {
                // 有効なプロファイルを収集
                var validProfiles = new List<OcrOptimizer.OptimalSettings>();
                foreach (var title in profiles)
                {
                    if (_profiles.TryGetValue(title, out var settings))
                    {
                        validProfiles.Add(settings);
                    }
                }

                if (validProfiles.Count < 2)
                    return false;

                // 設定値の平均を計算
                float avgConfidence = validProfiles.Average(p => p.ConfidenceThreshold);
                float avgContrast = validProfiles.Average(p => p.PreprocessingOptions.ContrastLevel);
                float avgBrightness = validProfiles.Average(p => p.PreprocessingOptions.BrightnessLevel);
                float avgSharpness = validProfiles.Average(p => p.PreprocessingOptions.SharpnessLevel);
                int avgNoise = (int)Math.Round(validProfiles.Average(p => p.PreprocessingOptions.NoiseReduction));
                float avgScale = validProfiles.Average(p => p.PreprocessingOptions.ScaleFactor);
                int avgThreshold = (int)Math.Round(validProfiles.Average(p => p.PreprocessingOptions.Threshold));
                int avgPadding = (int)Math.Round(validProfiles.Average(p => p.PreprocessingOptions.Padding));

                // 新しいプロファイルを作成
                var newSettings = new OcrOptimizer.OptimalSettings
                {
                    ConfidenceThreshold = avgConfidence,
                    PreprocessingOptions = new PreprocessingOptions
                    {
                        ContrastLevel = avgContrast,
                        BrightnessLevel = avgBrightness,
                        SharpnessLevel = avgSharpness,
                        NoiseReduction = avgNoise,
                        ScaleFactor = avgScale,
                        Threshold = avgThreshold,
                        Padding = avgPadding
                    },
                    LastOptimized = DateTime.Now,
                    OptimizationAttempts = 1,
                    IsOptimized = true
                };

                // 新しいプロファイルを保存
                SaveProfile(newTitle, newSettings);

                Debug.WriteLine($"{profiles.Count} 件のプロファイルを統合し、'{newTitle}' として保存しました");
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"プロファイル統合エラー: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 2つのプロファイルを比較
        /// </summary>
        /// <param name="profile1">比較元プロファイル</param>
        /// <param name="profile2">比較先プロファイル</param>
        /// <returns>差異情報を含む辞書</returns>
        public Dictionary<string, object> CompareProfiles(string profile1, string profile2)
        {
            var result = new Dictionary<string, object>();

            if (string.IsNullOrWhiteSpace(profile1) || string.IsNullOrWhiteSpace(profile2))
            {
                result["Error"] = "無効なプロファイル名";
                return result;
            }

            if (!_profiles.TryGetValue(profile1, out var settings1))
            {
                result["Error"] = $"プロファイル '{profile1}' が見つかりません";
                return result;
            }

            if (!_profiles.TryGetValue(profile2, out var settings2))
            {
                result["Error"] = $"プロファイル '{profile2}' が見つかりません";
                return result;
            }

            // 各設定値の差異を計算
            result["ConfidenceThreshold"] = new
            {
                Profile1 = settings1.ConfidenceThreshold,
                Profile2 = settings2.ConfidenceThreshold,
                Difference = settings2.ConfidenceThreshold - settings1.ConfidenceThreshold
            };

            result["ContrastLevel"] = new
            {
                Profile1 = settings1.PreprocessingOptions.ContrastLevel,
                Profile2 = settings2.PreprocessingOptions.ContrastLevel,
                Difference = settings2.PreprocessingOptions.ContrastLevel - settings1.PreprocessingOptions.ContrastLevel
            };

            result["BrightnessLevel"] = new
            {
                Profile1 = settings1.PreprocessingOptions.BrightnessLevel,
                Profile2 = settings2.PreprocessingOptions.BrightnessLevel,
                Difference = settings2.PreprocessingOptions.BrightnessLevel - settings1.PreprocessingOptions.BrightnessLevel
            };

            result["SharpnessLevel"] = new
            {
                Profile1 = settings1.PreprocessingOptions.SharpnessLevel,
                Profile2 = settings2.PreprocessingOptions.SharpnessLevel,
                Difference = settings2.PreprocessingOptions.SharpnessLevel - settings1.PreprocessingOptions.SharpnessLevel
            };

            // 他の設定も同様に比較

            result["LastOptimized"] = new
            {
                Profile1 = settings1.LastOptimized,
                Profile2 = settings2.LastOptimized,
                DaysDifference = (settings2.LastOptimized - settings1.LastOptimized).TotalDays
            };

            Debug.WriteLine($"プロファイル '{profile1}' と '{profile2}' の比較を実行しました");
            return result;
        }

        /// <summary>
        /// ゲームの表示名（ファイル名から拡張子と特殊文字を削除）を取得
        /// </summary>
        /// <param name="processName">プロセス名またはファイル名</param>
        /// <returns>表示名</returns>
        public static string GetGameDisplayName(string processName)
        {
            if (string.IsNullOrWhiteSpace(processName))
                return string.Empty;

            // 拡張子と特殊文字を削除
            string name = processName;

            // 拡張子を削除
            if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            {
                name = name.Substring(0, name.Length - 4);
            }

            // 特殊文字を空白に置換
            name = System.Text.RegularExpressions.Regex.Replace(name, "[_.]", " ");

            // 連続する空白を単一の空白に置換
            while (name.Contains("  "))
            {
                name = name.Replace("  ", " ");
            }

            // 先頭の文字を大文字に
            if (name.Length > 0)
            {
                name = char.ToUpper(name[0]) + name.Substring(1);
            }

            return name.Trim();
        }
    }
}