using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;

namespace GameTranslationOverlay.Core.Utils
{
    /// <summary>
    /// メモリ状態に応じて動的にキャッシュサイズを調整する汎用キャッシュマネージャー。
    /// 高性能で効率的なキャッシュ管理を提供します。
    /// </summary>
    /// <typeparam name="TKey">キャッシュのキーの型</typeparam>
    /// <typeparam name="TValue">キャッシュの値の型</typeparam>
    public class AdaptiveCacheManager<TKey, TValue>
    {
        // キャッシュデータを格納する辞書
        private readonly Dictionary<TKey, CacheItem> _cache = new Dictionary<TKey, CacheItem>();

        // 最大エントリ数
        private int _maxItems;

        // デフォルトの有効期限
        private readonly TimeSpan _defaultExpiration;

        // メモリ圧迫の閾値（バイト単位）
        private readonly long _memoryThresholdBytes;

        // 統計情報
        private long _cacheHits = 0;
        private long _cacheMisses = 0;
        private readonly Stopwatch _performanceTimer = new Stopwatch();
        private long _totalAccessTimeMs = 0;
        private int _accessCount = 0;

        // スレッド安全のためのロックオブジェクト
        private readonly object _cacheLock = new object();

        // 最後のメモリチェック時刻
        private DateTime _lastMemoryCheckTime = DateTime.MinValue;

        // メモリチェックの間隔（ミリ秒）
        private const int MemoryCheckIntervalMs = 10000; // 10秒

        /// <summary>
        /// キャッシュアイテムを表すクラス
        /// </summary>
        private class CacheItem
        {
            public TValue Value { get; set; }
            public DateTime LastAccess { get; set; }
            public DateTime Expiration { get; set; }
            public long EstimatedSize { get; set; }
        }

        /// <summary>
        /// コンストラクタ
        /// </summary>
        /// <param name="maxItems">キャッシュの最大エントリ数</param>
        /// <param name="defaultExpiration">デフォルトの有効期限（null=無期限）</param>
        /// <param name="memoryThresholdMb">メモリ圧迫とみなす閾値（MB）</param>
        public AdaptiveCacheManager(int maxItems = 1000, TimeSpan? defaultExpiration = null, long memoryThresholdMb = 200)
        {
            _maxItems = maxItems;
            _defaultExpiration = defaultExpiration ?? TimeSpan.FromHours(2);
            _memoryThresholdBytes = memoryThresholdMb * 1024 * 1024;

            Debug.WriteLine($"AdaptiveCacheManager: 初期化 (最大エントリ={maxItems}, 有効期限={_defaultExpiration}, メモリ閾値={memoryThresholdMb}MB)");

            // パフォーマンス計測用タイマーを開始
            _performanceTimer.Start();
        }

        /// <summary>
        /// キャッシュからアイテムを取得、または指定された関数を使用して新しい値を生成・追加します
        /// </summary>
        /// <param name="key">キャッシュキー</param>
        /// <param name="valueFactory">値がキャッシュに存在しない場合に値を生成する関数</param>
        /// <param name="expiration">この項目の有効期限（nullの場合はデフォルト）</param>
        /// <param name="estimatedSize">値の推定サイズ（バイト、自動推定できない場合）</param>
        /// <returns>キャッシュから取得した値、または新しく生成された値</returns>
        public TValue GetOrAdd(TKey key, Func<TValue> valueFactory, TimeSpan? expiration = null, long estimatedSize = -1)
        {
            // メモリの状態を確認し、必要に応じてキャッシュサイズを調整
            CheckMemoryAndResize();

            lock (_cacheLock)
            {
                // 期限切れのエントリを削除
                RemoveExpiredEntries();

                // キャッシュにエントリが存在するか確認
                if (_cache.TryGetValue(key, out CacheItem item))
                {
                    // すでに期限切れの場合は新しい値で更新
                    if (DateTime.Now > item.Expiration)
                    {
                        _cacheMisses++;

                        // 値を生成
                        TValue newValue = valueFactory();

                        // 推定サイズを更新
                        long newSize = estimatedSize >= 0 ? estimatedSize : EstimateSize(newValue);

                        // 有効期限を設定
                        TimeSpan itemExpiration = expiration ?? _defaultExpiration;

                        // キャッシュアイテムを更新
                        item.Value = newValue;
                        item.LastAccess = DateTime.Now;
                        item.Expiration = DateTime.Now + itemExpiration;
                        item.EstimatedSize = newSize;

                        return newValue;
                    }

                    // キャッシュヒット
                    _cacheHits++;
                    item.LastAccess = DateTime.Now;

                    // アクセス統計を更新
                    _accessCount++;
                    _totalAccessTimeMs += _performanceTimer.ElapsedMilliseconds;
                    _performanceTimer.Restart();

                    return item.Value;
                }

                // キャッシュミス
                _cacheMisses++;

                // 容量を超えたら、最も古いアイテムを削除
                if (_cache.Count >= _maxItems)
                {
                    RemoveLeastRecentlyUsed();
                }

                // 新しい値を生成
                TValue newValue = valueFactory();

                // 値のサイズを推定
                long valueSize = estimatedSize >= 0 ? estimatedSize : EstimateSize(newValue);

                // 有効期限を設定
                TimeSpan itemExpiration = expiration ?? _defaultExpiration;

                // 新しいアイテムをキャッシュに追加
                _cache[key] = new CacheItem
                {
                    Value = newValue,
                    LastAccess = DateTime.Now,
                    Expiration = DateTime.Now + itemExpiration,
                    EstimatedSize = valueSize
                };

                // キャッシュのトータルサイズが大きい場合はデバッグログ
                long totalSize = _cache.Values.Sum(v => v.EstimatedSize);
                if (totalSize > 10 * 1024 * 1024) // 10MB以上
                {
                    Debug.WriteLine($"キャッシュサイズが大きくなっています: {totalSize / (1024 * 1024)}MB, {_cache.Count}エントリ");
                }

                return newValue;
            }
        }

        /// <summary>
        /// キャッシュから値を取得します
        /// </summary>
        /// <param name="key">キャッシュキー</param>
        /// <returns>キャッシュに存在する場合は値、存在しない場合はdefault(TValue)</returns>
        public TValue Get(TKey key)
        {
            lock (_cacheLock)
            {
                // キャッシュにエントリが存在するか確認
                if (_cache.TryGetValue(key, out CacheItem item))
                {
                    // 期限切れの場合は削除してデフォルト値を返す
                    if (DateTime.Now > item.Expiration)
                    {
                        _cache.Remove(key);
                        _cacheMisses++;
                        return default;
                    }

                    // キャッシュヒット
                    _cacheHits++;
                    item.LastAccess = DateTime.Now;

                    // アクセス統計を更新
                    _accessCount++;
                    _totalAccessTimeMs += _performanceTimer.ElapsedMilliseconds;
                    _performanceTimer.Restart();

                    return item.Value;
                }

                // キャッシュミス
                _cacheMisses++;
                return default;
            }
        }

        /// <summary>
        /// キャッシュに値を追加または更新します
        /// </summary>
        /// <param name="key">キャッシュキー</param>
        /// <param name="value">格納する値</param>
        /// <param name="expiration">この項目の有効期限（nullの場合はデフォルト）</param>
        /// <param name="estimatedSize">値の推定サイズ（バイト、自動推定できない場合）</param>
        public void AddOrUpdate(TKey key, TValue value, TimeSpan? expiration = null, long estimatedSize = -1)
        {
            // メモリの状態を確認し、必要に応じてキャッシュサイズを調整
            CheckMemoryAndResize();

            lock (_cacheLock)
            {
                // 容量を超えたら、最も古いアイテムを削除
                if (_cache.Count >= _maxItems && !_cache.ContainsKey(key))
                {
                    RemoveLeastRecentlyUsed();
                }

                // 値のサイズを推定
                long valueSize = estimatedSize >= 0 ? estimatedSize : EstimateSize(value);

                // 有効期限を設定
                TimeSpan itemExpiration = expiration ?? _defaultExpiration;

                // 新しいアイテムをキャッシュに追加または更新
                _cache[key] = new CacheItem
                {
                    Value = value,
                    LastAccess = DateTime.Now,
                    Expiration = DateTime.Now + itemExpiration,
                    EstimatedSize = valueSize
                };
            }
        }

        /// <summary>
        /// キャッシュから項目を削除します
        /// </summary>
        /// <param name="key">削除するキー</param>
        /// <returns>項目が削除された場合はtrue</returns>
        public bool Remove(TKey key)
        {
            lock (_cacheLock)
            {
                return _cache.Remove(key);
            }
        }

        /// <summary>
        /// キャッシュをクリアします
        /// </summary>
        public void Clear()
        {
            lock (_cacheLock)
            {
                _cache.Clear();
                Debug.WriteLine("キャッシュをクリアしました");
            }
        }

        /// <summary>
        /// キャッシュの最大エントリ数を設定します
        /// </summary>
        /// <param name="maxItems">新しい最大エントリ数</param>
        public void SetMaxItems(int maxItems)
        {
            if (maxItems <= 0)
                throw new ArgumentException("最大エントリ数は1以上である必要があります", nameof(maxItems));

            lock (_cacheLock)
            {
                int oldMax = _maxItems;
                _maxItems = maxItems;

                // 新しい上限が現在のキャッシュサイズよりも小さい場合、超過分を削除
                if (_cache.Count > maxItems)
                {
                    int removeCount = _cache.Count - maxItems;
                    for (int i = 0; i < removeCount; i++)
                    {
                        RemoveLeastRecentlyUsed();
                    }
                }

                Debug.WriteLine($"キャッシュの最大エントリ数を{oldMax}から{maxItems}に変更しました");
            }
        }

        /// <summary>
        /// 期限切れのエントリを削除します
        /// </summary>
        /// <returns>削除されたエントリの数</returns>
        private int RemoveExpiredEntries()
        {
            int removedCount = 0;
            DateTime now = DateTime.Now;

            // 削除するキーのリスト
            List<TKey> keysToRemove = new List<TKey>();

            foreach (var pair in _cache)
            {
                if (now > pair.Value.Expiration)
                {
                    keysToRemove.Add(pair.Key);
                }
            }

            // 実際の削除処理
            foreach (var key in keysToRemove)
            {
                _cache.Remove(key);
                removedCount++;
            }

            // 大量の期限切れエントリがあった場合はログ出力
            if (removedCount > 10)
            {
                Debug.WriteLine($"{removedCount}個の期限切れエントリを削除しました");
            }

            return removedCount;
        }

        /// <summary>
        /// 最も最近使用されていないエントリを削除します（LRUアルゴリズム）
        /// </summary>
        private void RemoveLeastRecentlyUsed()
        {
            if (_cache.Count == 0)
                return;

            TKey oldestKey = default;
            DateTime oldestAccess = DateTime.MaxValue;

            // 最も古いアクセス時間のエントリを検索
            foreach (var pair in _cache)
            {
                if (pair.Value.LastAccess < oldestAccess)
                {
                    oldestAccess = pair.Value.LastAccess;
                    oldestKey = pair.Key;
                }
            }

            // 見つかったエントリを削除
            if (!EqualityComparer<TKey>.Default.Equals(oldestKey, default))
            {
                _cache.Remove(oldestKey);
            }
        }

        /// <summary>
        /// メモリ状態をチェックし、必要に応じてキャッシュサイズを調整します
        /// </summary>
        private void CheckMemoryAndResize()
        {
            // 一定間隔でのみチェックを実行
            if ((DateTime.Now - _lastMemoryCheckTime).TotalMilliseconds < MemoryCheckIntervalMs)
                return;

            _lastMemoryCheckTime = DateTime.Now;

            try
            {
                // 現在のメモリ使用量を取得
                long currentMemory = GC.GetTotalMemory(false);

                // メモリ使用量が閾値を超えている場合、キャッシュを削減
                if (currentMemory > _memoryThresholdBytes)
                {
                    lock (_cacheLock)
                    {
                        // 使用頻度の低いものから半分削除
                        int itemsToRemove = _cache.Count / 2;

                        if (itemsToRemove > 0)
                        {
                            Debug.WriteLine($"メモリ圧迫検出: {currentMemory / (1024 * 1024)}MB, キャッシュを{itemsToRemove}エントリ削減します");

                            // アクセス時間でソートし、古いものから削除
                            var itemsByAccess = _cache.OrderBy(kv => kv.Value.LastAccess).Take(itemsToRemove).ToList();

                            foreach (var item in itemsByAccess)
                            {
                                _cache.Remove(item.Key);
                            }

                            // GCを促進
                            GC.Collect();

                            Debug.WriteLine($"キャッシュを削減しました: 現在{_cache.Count}エントリ");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"メモリチェックエラー: {ex.Message}");
            }
        }

        /// <summary>
        /// オブジェクトのサイズを概算します
        /// </summary>
        /// <param name="value">サイズを推定する値</param>
        /// <returns>推定サイズ（バイト）</returns>
        private long EstimateSize(TValue value)
        {
            if (value == null)
                return 0;

            // 文字列の場合は文字数 * 2バイト（おおよその推定）
            if (value is string stringValue)
            {
                return stringValue.Length * sizeof(char);
            }

            // バイト配列の場合は配列長
            if (value is byte[] byteArray)
            {
                return byteArray.Length;
            }

            // Bitmapの場合はピクセル数 * 色深度
            if (value is System.Drawing.Bitmap bitmap)
            {
                int bytesPerPixel = System.Drawing.Image.GetPixelFormatSize(bitmap.PixelFormat) / 8;
                return bitmap.Width * bitmap.Height * bytesPerPixel;
            }

            // その他の型は64バイトと仮定（最小値）
            return 64;
        }

        /// <summary>
        /// キャッシュヒット率を取得します（0.0～1.0）
        /// </summary>
        /// <returns>キャッシュヒット率</returns>
        public double GetHitRate()
        {
            long total = Interlocked.Read(ref _cacheHits) + Interlocked.Read(ref _cacheMisses);
            return total == 0 ? 0 : (double)Interlocked.Read(ref _cacheHits) / total;
        }

        /// <summary>
        /// 平均アクセス時間をミリ秒単位で取得します
        /// </summary>
        /// <returns>平均アクセス時間（ミリ秒）</returns>
        public double GetAverageAccessTimeMs()
        {
            int count = _accessCount;
            return count == 0 ? 0 : (double)_totalAccessTimeMs / count;
        }

        /// <summary>
        /// エントリ数を取得します
        /// </summary>
        public int Count
        {
            get
            {
                lock (_cacheLock)
                {
                    return _cache.Count;
                }
            }
        }

        /// <summary>
        /// キャッシュの統計情報を取得します
        /// </summary>
        /// <returns>統計情報を含む文字列</returns>
        public string GetStatistics()
        {
            lock (_cacheLock)
            {
                long cacheHits = Interlocked.Read(ref _cacheHits);
                long cacheMisses = Interlocked.Read(ref _cacheMisses);
                long totalAccesses = cacheHits + cacheMisses;
                double hitRate = totalAccesses == 0 ? 0 : (double)cacheHits / totalAccesses;

                // キャッシュサイズを計算
                long totalSize = _cache.Values.Sum(v => v.EstimatedSize);

                // 統計文字列を生成
                return string.Format(
                    "キャッシュ統計:\n" +
                    "エントリ数: {0}\n" +
                    "推定合計サイズ: {1:F2}MB\n" +
                    "ヒット数: {2}\n" +
                    "ミス数: {3}\n" +
                    "ヒット率: {4:P2}\n" +
                    "平均アクセス時間: {5:F2}ms",
                    _cache.Count,
                    totalSize / (1024.0 * 1024.0),
                    cacheHits,
                    cacheMisses,
                    hitRate,
                    GetAverageAccessTimeMs()
                );
            }
        }
    }
}