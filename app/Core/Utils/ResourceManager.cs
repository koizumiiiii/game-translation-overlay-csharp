using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;

namespace GameTranslationOverlay.Core.Utils
{
    /// <summary>
    /// アプリケーション全体のリソース管理を担当するクラス。
    /// Disposeが必要なリソースを追跡し、必要に応じて解放します。
    /// メモリ管理の最適化に貢献します。
    /// </summary>
    public static class ResourceManager
    {
        // 追跡中のリソースへの弱参照を保持するリスト
        private static readonly List<WeakReference<IDisposable>> _trackedResources = new List<WeakReference<IDisposable>>();

        // 同時アクセスのためのロックオブジェクト
        private static readonly object _resourceLock = new object();

        // 統計情報
        private static int _totalTrackedCount = 0;
        private static int _totalReleasedCount = 0;
        private static int _totalCleanupCalls = 0;
        private static DateTime _lastCleanupTime = DateTime.MinValue;

        // 自動クリーンアップの閾値（追跡リソース数）
        private const int AutoCleanupThreshold = 100;

        // 最後のクリーンアップから経過すべき最小時間（ミリ秒）
        private const int MinCleanupIntervalMs = 10000; // 10秒

        /// <summary>
        /// リソースを追跡対象として登録します
        /// </summary>
        /// <param name="resource">追跡するリソース（IDisposableを実装したオブジェクト）</param>
        public static void TrackResource(IDisposable resource)
        {
            if (resource == null)
                return;

            lock (_resourceLock)
            {
                // 既に解放されたリソースを除去（リスト整理）
                CleanupDeadReferences();

                // 新しいリソースを追加
                _trackedResources.Add(new WeakReference<IDisposable>(resource));
                _totalTrackedCount++;

                // 追跡リソース数が多い場合、自動クリーンアップを検討
                if (_trackedResources.Count > AutoCleanupThreshold &&
                    (DateTime.Now - _lastCleanupTime).TotalMilliseconds > MinCleanupIntervalMs)
                {
                    PerformCleanup();
                }
            }
        }

        /// <summary>
        /// リソースを手動で解放し、追跡リストから削除します
        /// </summary>
        /// <param name="resource">解放するリソース</param>
        /// <returns>解放に成功した場合はtrue</returns>
        public static bool ReleaseResource(IDisposable resource)
        {
            if (resource == null)
                return false;

            bool released = false;

            lock (_resourceLock)
            {
                // マッチするWeakReferenceを探す
                WeakReference<IDisposable> matchingRef = null;

                foreach (var weakRef in _trackedResources)
                {
                    IDisposable target;
                    if (weakRef.TryGetTarget(out target) && target == resource)
                    {
                        matchingRef = weakRef;
                        break;
                    }
                }

                // 見つかったら削除してDisposeを呼び出す
                if (matchingRef != null)
                {
                    _trackedResources.Remove(matchingRef);

                    try
                    {
                        resource.Dispose();
                        released = true;
                        _totalReleasedCount++;
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"リソース解放エラー: {ex.Message}");
                    }
                }
            }

            return released;
        }

        /// <summary>
        /// 追跡リストから既に解放されたリソースへの参照を削除
        /// </summary>
        private static void CleanupDeadReferences()
        {
            // 削除対象の参照を収集
            List<WeakReference<IDisposable>> toRemove = new List<WeakReference<IDisposable>>();

            foreach (var weakRef in _trackedResources)
            {
                IDisposable resource;
                if (!weakRef.TryGetTarget(out resource) || resource == null)
                {
                    toRemove.Add(weakRef);
                }
            }

            // 無効な参照を削除
            foreach (var deadRef in toRemove)
            {
                _trackedResources.Remove(deadRef);
            }

            if (toRemove.Count > 0)
            {
                Debug.WriteLine($"{toRemove.Count}個の無効なリソース参照を削除しました");
            }
        }

        /// <summary>
        /// メモリ使用量が高い場合や明示的に呼び出された場合に、積極的にリソースを解放します
        /// </summary>
        public static void PerformCleanup()
        {
            if ((DateTime.Now - _lastCleanupTime).TotalMilliseconds < MinCleanupIntervalMs)
            {
                // 前回のクリーンアップから十分な時間が経過していない場合はスキップ
                return;
            }

            lock (_resourceLock)
            {
                _totalCleanupCalls++;
                _lastCleanupTime = DateTime.Now;

                // 無効な参照を削除
                CleanupDeadReferences();

                // Bitmap資源を優先的に解放（長時間追跡されているもの）
                List<WeakReference<IDisposable>> oldBitmaps = new List<WeakReference<IDisposable>>();

                foreach (var weakRef in _trackedResources)
                {
                    IDisposable resource;
                    if (weakRef.TryGetTarget(out resource) && resource is System.Drawing.Bitmap)
                    {
                        oldBitmaps.Add(weakRef);
                    }
                }

                // 古いBitmapの一部を解放（半分程度）
                int releaseCount = oldBitmaps.Count / 2;

                for (int i = 0; i < releaseCount && i < oldBitmaps.Count; i++)
                {
                    IDisposable resource;
                    if (oldBitmaps[i].TryGetTarget(out resource) && resource != null)
                    {
                        try
                        {
                            resource.Dispose();
                            _totalReleasedCount++;
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"Bitmap解放エラー: {ex.Message}");
                        }
                    }

                    _trackedResources.Remove(oldBitmaps[i]);
                }

                if (releaseCount > 0)
                {
                    Debug.WriteLine($"クリーンアップ: {releaseCount}個のBitmapリソースを解放しました");

                    // GCを促進
                    GC.Collect(0, GCCollectionMode.Optimized);
                }
            }
        }

        /// <summary>
        /// 全ての追跡リソースを解放します（アプリケーション終了時などに使用）
        /// </summary>
        public static void DisposeAll()
        {
            lock (_resourceLock)
            {
                int disposedCount = 0;
                int failedCount = 0;

                foreach (var weakRef in _trackedResources)
                {
                    IDisposable resource;
                    if (weakRef.TryGetTarget(out resource) && resource != null)
                    {
                        try
                        {
                            resource.Dispose();
                            disposedCount++;
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"リソース解放エラー: {ex.Message}");
                            failedCount++;
                        }
                    }
                }

                _trackedResources.Clear();

                Debug.WriteLine($"全リソース解放: 成功={disposedCount}, 失敗={failedCount}");
            }
        }

        /// <summary>
        /// 追跡中のリソース数を取得します
        /// </summary>
        /// <returns>追跡中のリソース数</returns>
        public static int GetActiveResourceCount()
        {
            lock (_resourceLock)
            {
                int count = 0;

                foreach (var weakRef in _trackedResources)
                {
                    IDisposable resource;
                    if (weakRef.TryGetTarget(out resource) && resource != null)
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        /// <summary>
        /// 統計情報を取得します
        /// </summary>
        /// <returns>統計情報を含む文字列</returns>
        public static string GetStatistics()
        {
            lock (_resourceLock)
            {
                int activeCount = GetActiveResourceCount();

                return string.Format(
                    "ResourceManager 統計:\n" +
                    "追跡リソース数: {0}\n" +
                    "有効リソース数: {1}\n" +
                    "これまでの追跡数: {2}\n" +
                    "解放した数: {3}\n" +
                    "クリーンアップ実行数: {4}\n" +
                    "最終クリーンアップ: {5}",
                    _trackedResources.Count,
                    activeCount,
                    _totalTrackedCount,
                    _totalReleasedCount,
                    _totalCleanupCalls,
                    _lastCleanupTime != DateTime.MinValue ? _lastCleanupTime.ToString("HH:mm:ss") : "なし"
                );
            }
        }
    }
}