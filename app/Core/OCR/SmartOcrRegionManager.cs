using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;

namespace GameTranslationOverlay.Core.OCR
{
    /// <summary>
    /// OCR処理を効率化するために、テキストが出現する可能性が高い領域を管理するクラス。
    /// 過去の検出結果から学習し、最適な処理範囲を特定します。
    /// </summary>
    public class SmartOcrRegionManager
    {
        // 現在のアクティブな領域（OCR処理対象）
        private List<Rectangle> _activeRegions = new List<Rectangle>();

        // 領域ごとの成功回数カウント（学習用）
        private readonly Dictionary<string, int> _regionSuccessCount = new Dictionary<string, int>();

        // 領域ごとの失敗回数カウント
        private readonly Dictionary<string, int> _regionFailureCount = new Dictionary<string, int>();

        // 有力な領域と判断するための成功閾値
        private readonly int _successThreshold = 3;

        // 領域を無効化するための失敗閾値
        private readonly int _failureThreshold = 5;

        // グリッドのサイズ（横x縦の分割数）
        private readonly Size _gridSize;

        // 最後に検出された全てのテキスト領域
        private List<TextRegion> _lastDetectedRegions = new List<TextRegion>();

        // 統計情報
        private int _totalScans = 0;
        private int _successfulScans = 0;

        /// <summary>
        /// コンストラクタ
        /// </summary>
        /// <param name="gridSize">画面分割グリッドのサイズ（デフォルト 3x3）</param>
        /// <param name="successThreshold">有力領域と判断するための成功閾値</param>
        /// <param name="failureThreshold">領域を無効化するための失敗閾値</param>
        public SmartOcrRegionManager(Size? gridSize = null, int successThreshold = 3, int failureThreshold = 5)
        {
            _gridSize = gridSize ?? new Size(3, 3);
            _successThreshold = successThreshold;
            _failureThreshold = failureThreshold;

            Debug.WriteLine($"SmartOcrRegionManager: 初期化 (グリッドサイズ={_gridSize.Width}x{_gridSize.Height}, 成功閾値={_successThreshold}, 失敗閾値={_failureThreshold})");
        }

        /// <summary>
        /// ゲーム画面を分割して、最も有望な領域を特定します
        /// </summary>
        /// <param name="gameWindow">ゲームウィンドウの矩形</param>
        /// <param name="detectedTexts">検出されたテキスト領域リスト</param>
        public void UpdateRegions(Rectangle gameWindow, List<TextRegion> detectedTexts)
        {
            _totalScans++;
            bool hasDetectedText = detectedTexts != null && detectedTexts.Count > 0;

            // 検出結果を保存
            _lastDetectedRegions = detectedTexts?.ToList() ?? new List<TextRegion>();

            if (hasDetectedText)
            {
                _successfulScans++;
            }

            // ウィンドウサイズが無効な場合はスキップ
            if (gameWindow.Width <= 0 || gameWindow.Height <= 0)
            {
                Debug.WriteLine("SmartOcrRegionManager: 無効なウィンドウサイズ");
                return;
            }

            // ゲーム画面を格子状に分割
            int cellWidth = gameWindow.Width / _gridSize.Width;
            int cellHeight = gameWindow.Height / _gridSize.Height;

            // 各セルにテキスト領域が含まれているかをカウント
            Dictionary<string, bool> cellHasText = new Dictionary<string, bool>();

            // グリッド全体を初期化（全てfalseで）
            for (int y = 0; y < _gridSize.Height; y++)
            {
                for (int x = 0; x < _gridSize.Width; x++)
                {
                    Rectangle cell = new Rectangle(
                        gameWindow.X + x * cellWidth,
                        gameWindow.Y + y * cellHeight,
                        cellWidth,
                        cellHeight);

                    string cellKey = GetCellKey(x, y);
                    cellHasText[cellKey] = false;
                }
            }

            // テキスト領域がない場合
            if (!hasDetectedText)
            {
                // 全てのアクティブセルの失敗カウントを増加
                foreach (var key in _regionSuccessCount.Keys.ToList())
                {
                    IncrementFailureCount(key);
                }

                // アクティブ領域が空の場合は全画面を対象とする
                if (_activeRegions.Count == 0)
                {
                    _activeRegions.Add(gameWindow);
                }

                return;
            }

            // 各セルにテキスト領域が含まれているかを確認
            foreach (var textRegion in detectedTexts)
            {
                for (int y = 0; y < _gridSize.Height; y++)
                {
                    for (int x = 0; x < _gridSize.Width; x++)
                    {
                        Rectangle cell = new Rectangle(
                            gameWindow.X + x * cellWidth,
                            gameWindow.Y + y * cellHeight,
                            cellWidth,
                            cellHeight);

                        if (cell.IntersectsWith(textRegion.Bounds))
                        {
                            string cellKey = GetCellKey(x, y);
                            cellHasText[cellKey] = true;

                            // 成功カウントを更新
                            IncrementSuccessCount(cellKey);
                        }
                    }
                }
            }

            // テキストが見つからなかったセルの失敗カウントを更新
            foreach (var entry in cellHasText)
            {
                if (!entry.Value && _regionSuccessCount.ContainsKey(entry.Key))
                {
                    IncrementFailureCount(entry.Key);
                }
            }

            // 有力な領域を更新
            UpdateActiveRegions(gameWindow, cellWidth, cellHeight);

            // デバッグログ
            if (_activeRegions.Count > 0 && _activeRegions.Count < _gridSize.Width * _gridSize.Height)
            {
                Debug.WriteLine($"SmartOcrRegionManager: {_activeRegions.Count}個のアクティブ領域を特定 (全体の{(double)_activeRegions.Count / (_gridSize.Width * _gridSize.Height):P0})");
            }
        }

        /// <summary>
        /// 成功カウントを増加させる
        /// </summary>
        /// <param name="cellKey">セルキー</param>
        private void IncrementSuccessCount(string cellKey)
        {
            if (!_regionSuccessCount.ContainsKey(cellKey))
            {
                _regionSuccessCount[cellKey] = 0;
            }

            _regionSuccessCount[cellKey]++;

            // 失敗カウントをリセット
            _regionFailureCount[cellKey] = 0;
        }

        /// <summary>
        /// 失敗カウントを増加させる
        /// </summary>
        /// <param name="cellKey">セルキー</param>
        private void IncrementFailureCount(string cellKey)
        {
            if (!_regionFailureCount.ContainsKey(cellKey))
            {
                _regionFailureCount[cellKey] = 0;
            }

            _regionFailureCount[cellKey]++;

            // 失敗閾値を超えたら成功カウントを減少
            if (_regionFailureCount[cellKey] >= _failureThreshold)
            {
                if (_regionSuccessCount.ContainsKey(cellKey))
                {
                    _regionSuccessCount[cellKey] = Math.Max(0, _regionSuccessCount[cellKey] - 1);
                }

                // 失敗カウントをリセット
                _regionFailureCount[cellKey] = 0;
            }
        }

        /// <summary>
        /// アクティブ領域を更新
        /// </summary>
        private void UpdateActiveRegions(Rectangle gameWindow, int cellWidth, int cellHeight)
        {
            // 有力な領域（閾値以上の成功回数）のみを抽出
            var successfulRegions = _regionSuccessCount
                .Where(kv => kv.Value >= _successThreshold)
                .Select(kv => kv.Key)
                .ToList();

            // アクティブ領域のリストをクリア
            _activeRegions.Clear();

            // 有力な領域が見つからない場合は全画面を対象とする
            if (successfulRegions.Count == 0)
            {
                _activeRegions.Add(gameWindow);
                return;
            }

            // 有力な領域をRectangleに変換して追加
            foreach (var key in successfulRegions)
            {
                int[] coordinates = ParseCellKey(key);
                int x = coordinates[0];
                int y = coordinates[1];

                Rectangle cell = new Rectangle(
                    gameWindow.X + x * cellWidth,
                    gameWindow.Y + y * cellHeight,
                    cellWidth,
                    cellHeight);

                _activeRegions.Add(cell);
            }

            // 隣接する領域をマージして効率化（オプション）
            MergeAdjacentRegions(gameWindow, cellWidth, cellHeight);
        }

        /// <summary>
        /// 隣接する領域をマージしてより大きな矩形にする
        /// </summary>
        private void MergeAdjacentRegions(Rectangle gameWindow, int cellWidth, int cellHeight)
        {
            bool[,] grid = new bool[_gridSize.Width, _gridSize.Height];

            // グリッドにアクティブセルをマーク
            foreach (var key in _regionSuccessCount.Where(kv => kv.Value >= _successThreshold).Select(kv => kv.Key))
            {
                int[] coordinates = ParseCellKey(key);
                int x = coordinates[0];
                int y = coordinates[1];

                if (x >= 0 && x < _gridSize.Width && y >= 0 && y < _gridSize.Height)
                {
                    grid[x, y] = true;
                }
            }

            // 水平方向のマージ処理
            for (int y = 0; y < _gridSize.Height; y++)
            {
                for (int x = 0; x < _gridSize.Width - 1; x++)
                {
                    if (grid[x, y] && grid[x + 1, y])
                    {
                        // 水平に隣接するセルをマーク（後で処理）
                    }
                }
            }

            // 垂直方向のマージ処理
            for (int x = 0; x < _gridSize.Width; x++)
            {
                for (int y = 0; y < _gridSize.Height - 1; y++)
                {
                    if (grid[x, y] && grid[x, y + 1])
                    {
                        // 垂直に隣接するセルをマーク（後で処理）
                    }
                }
            }

            // マージ処理の完全な実装はここでは省略
            // 実際の実装では、連結成分のラベリングやグラフアルゴリズムを使用して
            // 最適なマージを行うことができる
        }

        /// <summary>
        /// OCR処理を行うべき領域を取得
        /// </summary>
        /// <returns>アクティブな領域のリスト</returns>
        public List<Rectangle> GetActiveRegions()
        {
            // アクティブ領域がない場合は空リストを返す
            return _activeRegions.Count > 0 ?
                new List<Rectangle>(_activeRegions) :
                new List<Rectangle>();
        }

        /// <summary>
        /// 全ての領域と統計情報をリセット
        /// </summary>
        public void Reset()
        {
            _activeRegions.Clear();
            _regionSuccessCount.Clear();
            _regionFailureCount.Clear();
            _lastDetectedRegions.Clear();
            _totalScans = 0;
            _successfulScans = 0;

            Debug.WriteLine("SmartOcrRegionManager: 全領域をリセットしました");
        }

        /// <summary>
        /// セルの座標からキーを生成
        /// </summary>
        /// <param name="x">X座標</param>
        /// <param name="y">Y座標</param>
        /// <returns>セルキー</returns>
        private string GetCellKey(int x, int y)
        {
            return $"{x},{y}";
        }

        /// <summary>
        /// セルキーから座標を解析
        /// </summary>
        /// <param name="key">セルキー</param>
        /// <returns>座標配列 [x, y]</returns>
        private int[] ParseCellKey(string key)
        {
            string[] parts = key.Split(',');
            return new int[] { int.Parse(parts[0]), int.Parse(parts[1]) };
        }

        /// <summary>
        /// テキスト検出成功率を取得
        /// </summary>
        /// <returns>成功率（0.0～1.0）</returns>
        public double GetSuccessRate()
        {
            return _totalScans == 0 ? 0 : (double)_successfulScans / _totalScans;
        }

        /// <summary>
        /// 最後に検出されたテキスト領域を取得
        /// </summary>
        /// <returns>テキスト領域のリスト</returns>
        public List<TextRegion> GetLastDetectedRegions()
        {
            return new List<TextRegion>(_lastDetectedRegions);
        }

        /// <summary>
        /// 統計情報を取得
        /// </summary>
        /// <returns>統計情報を含む文字列</returns>
        public string GetStatistics()
        {
            int totalCells = _gridSize.Width * _gridSize.Height;
            int activeCells = _regionSuccessCount.Count(kv => kv.Value >= _successThreshold);

            return string.Format(
                "スマートOCR領域統計:\n" +
                "グリッドサイズ: {0}x{1} ({2}セル)\n" +
                "アクティブセル数: {3} ({4:P0})\n" +
                "スキャン回数: {5}\n" +
                "テキスト検出回数: {6}\n" +
                "検出成功率: {7:P2}\n" +
                "有効な領域数: {8}",
                _gridSize.Width, _gridSize.Height, totalCells,
                activeCells, (double)activeCells / totalCells,
                _totalScans, _successfulScans,
                GetSuccessRate(),
                _activeRegions.Count
            );
        }
    }
}