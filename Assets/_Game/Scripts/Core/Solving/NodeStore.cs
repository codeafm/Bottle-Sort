using System;
using System.Collections.Generic;

namespace BottleSort.Core
{
    // Все позиции поиска лежат подряд в одном массиве ulong по Width штук.
    // HashSet хранит только номера узлов, а сравнивает по содержимому массива.
    // Новый узел сначала пишется в «черновой» слот (номер Count) и ищется в наборе, пока не закоммичен.
    internal sealed class NodeStore : IEqualityComparer<int>
    {
        private readonly int width;
        private readonly HashSet<int> index;
        private ulong[] packed;
        private int[] parents;
        private int[] depths;

        public int Count { get; private set; }

        public NodeStore(int width, int initialCapacity = 4096)
        {
            this.width = width;
            packed = new ulong[width * (initialCapacity + 1)];
            parents = new int[initialCapacity + 1];
            depths = new int[initialCapacity + 1];
            index = new HashSet<int>(this);
        }

        public ulong[] Buffer => packed;
        public int DraftOffset => Count * width;
        public int OffsetOf(int node) => node * width;
        public int ParentOf(int node) => parents[node];
        public int DepthOf(int node) => depths[node];

        public void SetDepth(int node, int depth, int parent)
        {
            depths[node] = depth;
            parents[node] = parent;
        }

        // Ищет черновик среди уже известных узлов. -1 — такой позиции ещё не было.
        public int FindDraft() => index.TryGetValue(Count, out int existing) ? existing : -1;

        public int CommitDraft(int parent, int depth)
        {
            int node = Count;
            parents[node] = parent;
            depths[node] = depth;
            index.Add(node);
            Count++;
            EnsureDraftSlot();
            return node;
        }

        private void EnsureDraftSlot()
        {
            if (parents.Length > Count)
                return;
            int capacity = parents.Length * 2;
            Array.Resize(ref parents, capacity);
            Array.Resize(ref depths, capacity);
            Array.Resize(ref packed, capacity * width);
        }

        public bool Equals(int a, int b)
        {
            int offsetA = a * width, offsetB = b * width;
            for (int i = 0; i < width; i++)
                if (packed[offsetA + i] != packed[offsetB + i])
                    return false;
            return true;
        }

        public int GetHashCode(int node)
        {
            ulong hash = 14695981039346656037UL;
            int offset = node * width;
            for (int i = 0; i < width; i++)
            {
                hash ^= packed[offset + i];
                hash *= 1099511628211UL;
                hash ^= hash >> 29;
            }
            return (int)(hash ^ (hash >> 32));
        }
    }
}
