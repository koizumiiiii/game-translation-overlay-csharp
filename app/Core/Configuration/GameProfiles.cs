using System;
using System.Collections.Generic;
using System.IO;
using System.Diagnostics;
using System.Text.Json;
using GameTranslationOverlay.Core.OCR.AI;
using GameTranslationOverlay.Core.Security;
using GameTranslationOverlay.Core.Utils;
using GameTranslationOverlay.Core.Configuration;

namespace GameTranslationOverlay.Core.Configuration
{
    /// <summary>
    /// ゲームごとの最適化プロファイルを管理するクラス
    /// </summary>
    public class GameProfiles
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
    }
}