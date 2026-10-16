using System.Collections.Generic;

namespace BottleSort.Core
{
    // Храним полные снимки, а не обратные ходы: так отмена честно возвращает и лёд, и замки, и скрытые слои.
    // Позиция весит пару сотен байт, история без лимита, как во Flutter-версии.
    public sealed class UndoStack
    {
        private readonly List<LevelState> snapshots = new List<LevelState>();

        public int Count => snapshots.Count;

        public void Push(LevelState state) => snapshots.Add(state.Clone());

        public bool TryPop(out LevelState state)
        {
            if (snapshots.Count == 0)
            {
                state = null;
                return false;
            }
            int last = snapshots.Count - 1;
            state = snapshots[last];
            snapshots.RemoveAt(last);
            return true;
        }

        public void Clear() => snapshots.Clear();
    }
}
