using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;

namespace GameTranslationOverlay.Core.Utils
{
    /// <summary>
    /// ディスポーザブルなリソースを追跡・管理するためのユーティリティクラス。
    /// アプリケーション全体でのリソースリークを防止します。
    /// </summary>
    public static class ResourceManager
    {
        // WeakReferenceを使用してリソースを追跡（GCの邪魔をしない）
        private static readonly List<WeakReference<IDisposable>> _trackedResources = new List<WeakReference<IDisposable>>();
        private static readonly List<ResourceInfo> _resourceStats = new List<ResourceInfo>();
        private static readonly object _lockObject = new object();

        // 統計情報
        private static int _totalCreated = 0;
        private static int _totalDisposed = 0;
        private static int _gcCollections = 0;
        private static DateTime _lastCleanupTime = DateTime.MinValue;

        /// <summary>
        /// リソース情報を格納するクラス
        /// </summary>
        private class ResourceInfo
        {
            public string ResourceType { get; set; }
            public DateTime CreationTime { get; set; }
            public long EstimatedSize { get; set; }
            public WeakReference<IDisposable> ResourceRef { get; set; }
        }

        /// <summary>
        /// リソースを追跡リストに追加します
        /// </summary>
        /// <param name="resource">追跡するリソース</param>
        public static void TrackResource(IDisposable resource)
        {
            if (resource == null)
                return;

            lock (_lockObject)
            {
                // 不要になったリソースの参照を削除
                CleanupDeadReferences();

                // 新しいリソースを追加
                var weakRef = new WeakReference<IDisposable>(resource);
                _trackedResources.Add(weakRef);
                _totalCreated++;

                // 追加のリソース情報を記録
                var info = new ResourceInfo
                {
                    ResourceType = resource.GetType().Name,
                    CreationTime = DateTime.Now,
                    EstimatedSize = EstimateResourceSize(resource),
                    ResourceRef = weakRef
                };

                _resourceStats.Add(info);

                // ログ出力（大きなリソースのみ）
                if (info.EstimatedSize > 1024 * 1024) // 1MB以上
                {
                    Debug.WriteLine($"大きなリソースを追跡開始: {info.ResourceType}, サイズ≈{info.EstimatedSize / (1024 * 1024)}MB");
                }

                // 定期的なクリーンアップ（追跡リソースが多くなった場合）
                if (_trackedResources.Count > 100 && (DateTime.Now - _lastCleanupTime).TotalMinutes >= 1)
                {
                    PerformCleanup();
                    _lastCleanupTime = DateTime.Now;
                }
            }
        }

        /// <summary>
        /// リソースを明示的に解放し、追跡リストから削除します
        /// </summary>
        /// <param name="resource">解放するリソース</param>
        public static void ReleaseResource(IDisposable resource)
        {
            if (resource == null)
                return;

            lock (_lockObject)
            {
                // 対象リソースを検索
                var toRemove = new List<WeakReference<IDisposable>>();
                var statsToRemove = new List<ResourceInfo>();

                foreach (var weakRef in _trackedResources)
                {
                    if (weakRef.TryGetTarget(out IDisposable target) && target == resource)
                    {
                        try
                        {
                            target.Dispose();
                            _totalDisposed++;
                            toRemove.Add(weakRef);
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"リソース解放エラー: {ex.Message}");
                        }
                    }
                }

                // 統計情報からも削除
                foreach (var info in _resourceStats)
                {
                    if (info.ResourceRef.TryGetTarget(out IDisposable target) && target == resource)
                    {
                        statsToRemove.Add(info);
                    }
                }

                // リストから削除
                foreach (var item in toRemove)
                {
                    _trackedResources.Remove(item);
                }

                foreach (var item in statsToRemove)
                {
                    _resourceStats.Remove(item);
                }
            }
        }

        /// <summary>
        /// すべての追跡リソースを解放します
        /// 通常はアプリケーション終了時に呼び出します
        /// </summary>
        public static void DisposeAll()
        {
            lock (_lockObject)
            {
                int disposedCount = 0;

                foreach (var weakRef in _trackedResources)
                {
                    if (weakRef.TryGetTarget(out IDisposable resource))
                    {
                        try
                        {
                            resource.Dispose();
                            disposedCount++;
                            _totalDisposed++;
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"リソース解放中にエラー: {ex.Message}");
                        }
                    }
                }

                Debug.WriteLine($"すべてのリソースを解放しました: {disposedCount}個のリソースを解放");
                _trackedResources.Clear();
                _resourceStats.Clear();
            }
        }

        /// <summary>
        /// 定期的なクリーンアップを実行します
        /// メモリ圧迫時や定期的なメンテナンスに使用します
        /// </summary>
        public static void PerformCleanup()
        {
            lock (_lockObject)
            {
                // 不要になったリソースの参照を削除
                int removedCount = CleanupDeadReferences();

                // 古いリソースを解放（一定時間以上経過したもの）
                int oldResourcesCount = CleanupOldResources(TimeSpan.FromMinutes(10));

                // メモリ使用量が多い場合は、より積極的にリソースを解放
                if (IsMemoryUnderPressure())
                {
                    Debug.WriteLine("メモリ圧迫を検出: 追加クリーンアップを実行します");
                    int aggressiveCount = CleanupOldResources(TimeSpan.FromMinutes(1));

                    // Bitmapリソースの優先的な解放
                    int bitmapCount = CleanupResourcesByType("Bitmap", 50);

                    Debug.WriteLine($"メモリ圧迫クリーンアップ: {aggressiveCount}個の古いリソースと{bitmapCount}個のBitmapを解放");
                }

                Debug.WriteLine($"クリーンアップ完了: {removedCount}個の参照と{oldResourcesCount}個の古いリソースを解放");

                // GC統計
                _gcCollections = GC.CollectionCount(0);
            }
        }

        /// <summary>
        /// 参照されなくなったリソース（GCが回収済み）のエントリを削除します
        /// </summary>
        /// <returns>削除されたエントリの数</returns>
        private static int CleanupDeadReferences()
        {
            // 既に解放されたリソースを検出
            var toRemove = _trackedResources.Where(wr => !wr.TryGetTarget(out _)).ToList();

            // 統計情報からも削除
            var statsToRemove = _resourceStats.Where(info => !info.ResourceRef.TryGetTarget(out _)).ToList();

            // リストから削除
            foreach (var item in toRemove)
            {
                _trackedResources.Remove(item);
            }

            foreach (var item in statsToRemove)
            {
                _resourceStats.Remove(item);
            }

            return toRemove.Count;
        }

        /// <summary>
        /// 指定した時間以上経過した古いリソースを解放します
        /// </summary>
        /// <param name="age">経過時間の閾値</param>
        /// <returns>解放されたリソースの数</returns>
        private static int CleanupOldResources(TimeSpan age)
        {
            int cleanedCount = 0;
            DateTime threshold = DateTime.Now - age;

            // 古いリソースを取得
            var oldResources = _resourceStats
                .Where(info => info.CreationTime < threshold && info.ResourceRef.TryGetTarget(out _))
                .ToList();

            foreach (var info in oldResources)
            {
                if (info.ResourceRef.TryGetTarget(out IDisposable resource))
                {
                    try
                    {
                        resource.Dispose();
                        cleanedCount++;
                        _totalDisposed++;
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"古いリソース解放中にエラー: {ex.Message}");
                    }
                }
            }

            // 不要になった参照を削除
            CleanupDeadReferences();

            return cleanedCount;
        }

        /// <summary>
        /// 特定の型のリソースを一定の割合で解放します
        /// </summary>
        /// <param name="typeName">リソースの型名</param>
        /// <param name="percentToClean">解放する割合（%）</param>
        /// <returns>解放されたリソースの数</returns>
        private static int CleanupResourcesByType(string typeName, int percentToClean)
        {
            int cleanedCount = 0;

            // 特定の型のリソースを取得
            var resources = _resourceStats
                .Where(info => info.ResourceType == typeName && info.ResourceRef.TryGetTarget(out _))
                .OrderBy(info => info.CreationTime)  // 古いものから解放
                .ToList();

            // 解放する数を計算
            int countToClean = (int)Math.Ceiling(resources.Count * percentToClean / 100.0);

            // 指定した数だけ解放
            for (int i = 0; i < countToClean && i < resources.Count; i++)
            {
                if (resources[i].ResourceRef.TryGetTarget(out IDisposable resource))
                {
                    try
                    {
                        resource.Dispose();
                        cleanedCount++;
                        _totalDisposed++;
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"型別リソース解放中にエラー: {ex.Message}");
                    }
                }
            }

            // 不要になった参照を削除
            CleanupDeadReferences();

            return cleanedCount;
        }

        /// <summary>
        /// メモリが圧迫されているかどうかを判定します
        /// </summary>
        /// <returns>メモリ圧迫時はtrue</returns>
        private static bool IsMemoryUnderPressure()
        {
            // 現在のメモリ使用量を取得（概算）
            long currentMemory = GC.GetTotalMemory(false);

            // 200MB以上を使用している場合は圧迫していると判断
            // 注: この閾値は実際のアプリケーションサイズに応じて調整すべき
            return currentMemory > 200L * 1024 * 1024;
        }

        /// <summary>
        /// リソースのサイズを概算します
        /// </summary>
        /// <param name="resource">サイズを推定するリソース</param>
        /// <returns>推定サイズ（バイト）</returns>
        private static long EstimateResourceSize(IDisposable resource)
        {
            if (resource is Bitmap bitmap)
            {
                // Bitmapのサイズを概算: 幅 x 高さ x 1ピクセルあたりのバイト数
                int bytesPerPixel = System.Drawing.Image.GetPixelFormatSize(bitmap.PixelFormat) / 8;
                return bitmap.Width * bitmap.Height * bytesPerPixel;
            }

            // その他のリソースは標準サイズを返す
            return 1024; // 1KB（デフォルト値）
        }

        /// <summary>
        /// リソース管理の統計情報を取得します
        /// </summary>
        /// <returns>統計情報を含む文字列</returns>
        public static string GetStatistics()
        {
            lock (_lockObject)
            {
                // 最新の状態に更新
                CleanupDeadReferences();

                // 現在のメモリ使用量
                long currentMemory = GC.GetTotalMemory(false);

                // リソースタイプ別の統計
                var typeStats = _resourceStats
                    .GroupBy(info => info.ResourceType)
                    .Select(g => new {
                        Type = g.Key,
                        Count = g.Count(),
                        TotalSize = g.Sum(info => info.EstimatedSize)
                    })
                    .OrderByDescending(x => x.TotalSize)
                    .ToList();

                // 統計レポートの作成
                System.Text.StringBuilder report = new System.Text.StringBuilder();
                report.AppendLine("=== リソース管理統計 ===");
                report.AppendLine($"作成されたリソース合計: {_totalCreated}");
                report.AppendLine($"解放されたリソース合計: {_totalDisposed}");
                report.AppendLine($"現在追跡中のリソース: {_trackedResources.Count}");
                report.AppendLine($"GCコレクション回数: {_gcCollections}");
                report.AppendLine($"現在のメモリ使用量: {currentMemory / (1024 * 1024)}MB");
                report.AppendLine("リソースタイプ別統計:");

                foreach (var stat in typeStats)
                {
                    report.AppendLine($"  {stat.Type}: {stat.Count}個 ({stat.TotalSize / (1024 * 1024.0):F2}MB)");
                }

                return report.ToString();
            }
        }
    }
}