using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace GameTranslationOverlay.Core.OCR.AI
{
    /// <summary>
    /// API使用制限を管理するクラス
    /// </summary>
    public class ApiUsageManager
    {
        private readonly Dictionary<string, GameApiUsage> _gameUsage = new Dictionary<string, GameApiUsage>();
        private const int MaxCallsPerDay = 10;  // 1日あたりの最大API呼び出し回数
        private const int MaxConsecutiveFailures = 3;  // 連続失敗許容回数

        private class GameApiUsage
        {
            public int CallCount { get; set; }
            public DateTime LastReset { get; set; }
            public List<DateTime> CallHistory { get; set; } = new List<DateTime>();
            public int ConsecutiveFailures { get; set; }
            public bool IsBlocked { get; set; }
        }

        /// <summary>
        /// シングルトンインスタンス
        /// </summary>
        private static ApiUsageManager _instance;
        public static ApiUsageManager Instance => _instance ?? (_instance = new ApiUsageManager());

        private ApiUsageManager()
        {
            // プライベートコンストラクタ（シングルトン用）
            CleanupOldEntries();
        }

        /// <summary>
        /// 指定したゲームに対してAPIを呼び出せるかどうかを判定
        /// </summary>
        /// <param name="gameId">ゲームの識別子</param>
        /// <returns>APIを呼び出し可能ならtrue</returns>
        public bool CanCallApi(string gameId)
        {
            if (string.IsNullOrEmpty(gameId))
            {
                Debug.WriteLine("ゲームIDが指定されていません");
                return false;
            }

            CleanupOldEntries();

            if (!_gameUsage.TryGetValue(gameId, out var usage))
            {
                // 新規ゲームの場合は制限なし
                _gameUsage[gameId] = new GameApiUsage { LastReset = DateTime.Now };
                return true;
            }

            // ブロックされているか確認
            if (usage.IsBlocked)
            {
                Debug.WriteLine($"APIコールはブロックされています: {gameId}");
                return false;
            }

            // 24時間経過で制限をリセット
            if ((DateTime.Now - usage.LastReset).TotalHours >= 24)
            {
                usage.CallCount = 0;
                usage.LastReset = DateTime.Now;
                usage.ConsecutiveFailures = 0;
                usage.IsBlocked = false;
            }

            // 連続失敗チェック
            if (usage.ConsecutiveFailures >= MaxConsecutiveFailures)
            {
                Debug.WriteLine($"連続失敗によりAPIコールをスキップ: {gameId}, 失敗回数: {usage.ConsecutiveFailures}");
                return false;
            }

            // 回数制限チェック
            if (usage.CallCount >= MaxCallsPerDay)
            {
                Debug.WriteLine($"API呼び出し制限に達しました: {gameId}");
                return false;
            }

            return true;
        }

        /// <summary>
        /// API呼び出しを記録
        /// </summary>
        /// <param name="gameId">ゲームの識別子</param>
        /// <param name="success">成功したかどうか</param>
        public void RecordApiCall(string gameId, bool success)
        {
            if (string.IsNullOrEmpty(gameId))
            {
                return;
            }

            if (!_gameUsage.TryGetValue(gameId, out var usage))
            {
                usage = new GameApiUsage { LastReset = DateTime.Now };
                _gameUsage[gameId] = usage;
            }

            usage.CallCount++;
            usage.CallHistory.Add(DateTime.Now);

            // 成功/失敗の記録
            if (success)
            {
                usage.ConsecutiveFailures = 0;
            }
            else
            {
                usage.ConsecutiveFailures++;

                // 連続失敗が多い場合はブロック
                if (usage.ConsecutiveFailures >= MaxConsecutiveFailures)
                {
                    usage.IsBlocked = true;
                    Debug.WriteLine($"ゲーム{gameId}は連続{MaxConsecutiveFailures}回失敗したためブロックされました");
                }
            }

            Debug.WriteLine($"API呼び出しを記録: ゲーム={gameId}, 成功={success}, 残り呼び出し回数={MaxCallsPerDay - usage.CallCount}");
        }

        /// <summary>
        /// 残りのAPI呼び出し回数を取得
        /// </summary>
        /// <param name="gameId">ゲームの識別子</param>
        /// <returns>残り回数</returns>
        public int GetRemainingCalls(string gameId)
        {
            if (string.IsNullOrEmpty(gameId) || !_gameUsage.TryGetValue(gameId, out var usage))
            {
                return MaxCallsPerDay;
            }

            // 24時間経過で制限をリセット
            if ((DateTime.Now - usage.LastReset).TotalHours >= 24)
            {
                return MaxCallsPerDay;
            }

            return Math.Max(0, MaxCallsPerDay - usage.CallCount);
        }

        /// <summary>
        /// ゲームがブロックされているかを確認
        /// </summary>
        /// <param name="gameId">ゲームの識別子</param>
        /// <returns>ブロックされているならtrue</returns>
        public bool IsGameBlocked(string gameId)
        {
            if (string.IsNullOrEmpty(gameId) || !_gameUsage.TryGetValue(gameId, out var usage))
            {
                return false;
            }

            // 24時間経過でブロックを解除
            if ((DateTime.Now - usage.LastReset).TotalHours >= 24)
            {
                usage.IsBlocked = false;
            }

            return usage.IsBlocked;
        }

        /// <summary>
        /// ブロックを手動で解除
        /// </summary>
        /// <param name="gameId">ゲームの識別子</param>
        public void UnblockGame(string gameId)
        {
            if (string.IsNullOrEmpty(gameId) || !_gameUsage.TryGetValue(gameId, out var usage))
            {
                return;
            }

            usage.IsBlocked = false;
            usage.ConsecutiveFailures = 0;
            Debug.WriteLine($"ゲーム{gameId}のブロックを解除しました");
        }

        /// <summary>
        /// 古いエントリをクリーンアップ
        /// </summary>
        private void CleanupOldEntries()
        {
            var keysToReset = new List<string>();

            foreach (var kvp in _gameUsage)
            {
                if ((DateTime.Now - kvp.Value.LastReset).TotalHours >= 24)
                {
                    keysToReset.Add(kvp.Key);
                }
            }

            foreach (var key in keysToReset)
            {
                _gameUsage[key].CallCount = 0;
                _gameUsage[key].LastReset = DateTime.Now;
                _gameUsage[key].ConsecutiveFailures = 0;
                _gameUsage[key].IsBlocked = false;
                _gameUsage[key].CallHistory.Clear();
                Debug.WriteLine($"ゲーム{key}の使用制限をリセットしました");
            }
        }

        /// <summary>
        /// 全ての使用状況をクリア（テスト用）
        /// </summary>
        public void ClearAllUsage()
        {
            _gameUsage.Clear();
            Debug.WriteLine("すべてのAPI使用状況をクリアしました");
        }
    }
}