using System;
using System.Collections.Generic;

namespace MayaImporter.Utils
{
    /// <summary>
    /// ノード依存のトポロジカルソートなど、DAG/グラフ処理の汎用ユーティリティ。
    /// Maya → Unity 再構築時の評価順序決定に使用する。
    /// </summary>
    public static class GraphTraversalUtil
    {
        /// <summary>
        /// 依存関係（node → dependencies）でトポロジカルソートする。
        /// sorted: 依存が先に来る順序
        /// cycleNodes: サイクル検出されたノード
        /// </summary>
        public static List<T> TopologicalSort<T>(
            IEnumerable<T> nodes,
            Func<T, IEnumerable<T>> getDependencies,
            IEqualityComparer<T> comparer,
            out List<T> cycleNodes)
        {
            comparer ??= EqualityComparer<T>.Default;

            var result = new List<T>();
            var cycles = new List<T>();

            var temp = new HashSet<T>(comparer);
            var perm = new HashSet<T>(comparer);

            void Visit(T n)
            {
                if (perm.Contains(n))
                    return;

                if (temp.Contains(n))
                {
                    // cycle 検出
                    if (!cycles.Contains(n))
                        cycles.Add(n);
                    return;
                }

                temp.Add(n);

                var deps = getDependencies != null ? getDependencies(n) : null;
                if (deps != null)
                {
                    foreach (var d in deps)
                    {
                        if (d == null) continue;
                        Visit(d);
                    }
                }

                temp.Remove(n);
                perm.Add(n);
                result.Add(n);
            }

            if (nodes != null)
            {
                foreach (var n in nodes)
                {
                    if (n == null) continue;
                    Visit(n);
                }
            }

            cycleNodes = cycles;
            return result;
        }

        public static List<T> TopologicalSort<T>(
            IEnumerable<T> nodes,
            Func<T, IEnumerable<T>> getDependencies,
            out List<T> cycleNodes)
        {
            return TopologicalSort(nodes, getDependencies, EqualityComparer<T>.Default, out cycleNodes);
        }

        public static List<T> BreadthFirst<T>(
            T start,
            Func<T, IEnumerable<T>> next,
            IEqualityComparer<T> comparer = null)
        {
            comparer ??= EqualityComparer<T>.Default;

            var result = new List<T>();
            var queue = new Queue<T>();
            var visited = new HashSet<T>(comparer);

            queue.Enqueue(start);
            visited.Add(start);

            while (queue.Count > 0)
            {
                var n = queue.Dequeue();
                result.Add(n);

                var ns = next != null ? next(n) : null;
                if (ns == null) continue;

                foreach (var v in ns)
                {
                    if (v == null) continue;
                    if (visited.Add(v))
                        queue.Enqueue(v);
                }
            }

            return result;
        }
    }
}
