using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace GameTranslationOverlay.Core.Utils
{
    /// <summary>
    /// アプリケーション全体のリソース管理を行うクラス
    /// メモリリークを防止し、リソースの適切な解放を保証します
    /// </summary>
    public static class ResourceManager
    {
        private static readonly object _lockObject = new object();
        private static readonly HashSet<WeakReference> _trackedResources = new HashSet<WeakReference>();
        private static int _resourceCount = 0;
        private static DateTime _lastCleanupTime = DateTime.MinValue;

        // 自動クリーンアップの間隔（10秒）
        private const int AUTO_CLEANUP_INTERVAL_MS = 10000;

        /// <summary>
        /// リソースを追跡対象に追加
        /// </summary>
        /// <param name="resource">追跡対象のリソース（IDisposableを実装していること）</param>
        public static void TrackResource(IDisposable resource)
        {
            if (resource == null)
                return;

            lock (_lockObject)
            {
                // 大量のリソースが追加された場合、自動的にクリーンアップを実行
                if (_resourceCount > 100 || (DateTime.Now - _lastCleanupTime).TotalMilliseconds > AUTO_CLEANUP_INTERVAL_MS)
                {
                    CleanupDeadReferences();
                }

                _trackedResources.Add(new WeakReference(resource));
                _resourceCount++;

                if (_resourceCount % 10 == 0)
                {
                    Debug.WriteLine($"ResourceManager: {_resourceCount}個のリソースを追跡中");
                }
            }
        }

        /// <summary>
        /// 特定のリソースを解放
        /// </summary>
        /// <param name="resource">解放するリソース</param>
        public static void ReleaseResource(IDisposable resource)
        {
            if (resource == null)
                return;

            try
            {
                resource.Dispose();

                lock (_lockObject)
                {
                    // 対応する弱参照を削除（必須ではないが、メモリ効率のため）
                    _trackedResources.RemoveWhere(wr =>
                        wr.IsAlive && wr.Target == resource);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ResourceManager: リソース解放中にエラー: {ex.Message}");
            }
        }

        /// <summary>
        /// 不要な参照のクリーンアップ
        /// </summary>
        /// <returns>削除された参照の数</returns>
        public static int CleanupDeadReferences()
        {
            lock (_lockObject)
            {
                int initialCount = _trackedResources.Count;
                int removed = _trackedResources.RemoveWhere(wr => !wr.IsAlive);

                if (removed > 0)
                {
                    _resourceCount -= removed;
                    Debug.WriteLine($"ResourceManager: {removed}個の無効なリソース参照を削除しました");
                }

                _lastCleanupTime = DateTime.Now;
                return removed;
            }
        }

        /// <summary>
        /// すべてのリソースの解放
        /// </summary>
        /// <returns>解放されたリソースの数</returns>
        public static int DisposeAll()
        {
            lock (_lockObject)
            {
                int disposedCount = 0;

                foreach (var wr in _trackedResources)
                {
                    if (wr.IsAlive && wr.Target is IDisposable disposable)
                    {
                        try
                        {
                            disposable.Dispose();
                            disposedCount++;
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"ResourceManager: リソース解放中にエラー: {ex.Message}");
                        }
                    }
                }

                _trackedResources.Clear();
                int oldCount = _resourceCount;
                _resourceCount = 0;

                Debug.WriteLine($"ResourceManager: {disposedCount}/{oldCount}個のリソースを解放しました");
                return disposedCount;
            }
        }

        /// <summary>
        /// 現在追跡中のリソース数を取得
        /// </summary>
        public static int GetResourceCount()
        {
            lock (_lockObject)
            {
                return _resourceCount;
            }
        }

        /// <summary>
        /// メモリ圧迫時の緊急クリーンアップ
        /// GCも強制的に実行
        /// </summary>
        public static void PerformEmergencyCleanup()
        {
            Debug.WriteLine("ResourceManager: 緊急クリーンアップを実行します");

            int disposed = DisposeAll();

            GC.Collect();
            GC.WaitForPendingFinalizers();

            Debug.WriteLine($"ResourceManager: 緊急クリーンアップ完了 - {disposed}個のリソースを解放");
        }
    }
}