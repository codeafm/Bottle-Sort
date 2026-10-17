using System;
using System.Collections.Generic;
using BottleSort.Core;
using UnityEngine;

namespace BottleSort.UI
{
    // Раскладка бутылок рядами на стеклянных полках, как в Game.dc.html.
    // Всё считается в пикселях макета (ширина 390), потом доска целиком масштабируется под свободное место.
    public sealed class BottleField : MonoBehaviour
    {
        const float MaxScale = 2.5f;
        const float MaxAvailableHeight = 420f;
        const float BottomReserve = 12f;
        const float Gap = 12f;
        const float RowPadding = 10f;
        const float TopPadding = 24f;
        const float ShelfHeight = 12f;
        const float ShelfOverlap = 6f;
        // Фигурка спасения в масштабе большой колбы: viewBox 200×240 при 1.12 px на единицу
        const float FigureWidth = 224f;
        const int BesideFigure = 8;

        [SerializeField] RectTransform board;
        [SerializeField] BottleSlot slotTemplate;
        [SerializeField] RectTransform shelfTemplate;
        // Любой компонент с IBottleView: сейчас заглушка, потом адаптер BottleView.
        [SerializeField] MonoBehaviour bottleTemplate;

        readonly List<BottleSlot> _slots = new List<BottleSlot>();
        readonly List<RectTransform> _shelves = new List<RectTransform>();
        readonly List<Vector3> _shelfPlan = new List<Vector3>();
        Vector2 _boardSize;

        public event Action<int> BottleTapped;
        public IReadOnlyList<BottleSlot> Slots => _slots;
        public float Scale => board.localScale.x;

        struct Placement
        {
            public float CenterX;
            public float Bottom;
            public float Width;
            public float Height;
        }

        public void Build(LevelState state) => Spawn(state, bottleTemplate, shelfTemplate, false);

        // Для FieldPreview: та же раскладка в редакторе, но с бутылками и полками на uGUI,
        // и всё созданное не сохраняется в сцену.
        public void BuildPreview(LevelState state, MonoBehaviour bottle, RectTransform shelf) => Spawn(state, bottle, shelf, true);

        public void ClearPreview() => Clear();

        public void Refit()
        {
            if (_boardSize != Vector2.zero)
                Fit();
        }

        void Spawn(LevelState state, MonoBehaviour bottlePrefab, RectTransform shelfPrefab, bool preview)
        {
            Clear();
            _shelfPlan.Clear();
            var placements = new Placement[state.BottleCount];
            int center = FindCenterpiece(state);
            bool figure = center >= 0 && state[center].Recipe != null;
            // Сетка с колбой посередине — как уровень 45 в макете; когда бутылок больше, колба просто стоит в ряду (L1500).
            // Фигурка спасения всегда посередине, лишние бутылки встают рядом ниже.
            if (figure || (center >= 0 && state.BottleCount <= 9))
                LayoutGrid(state, center, figure, placements);
            else
                LayoutRows(state, placements);
            foreach (var shelf in _shelfPlan)
                AddShelf(shelfPrefab, shelf.x, shelf.y, shelf.z);

            for (int i = 0; i < placements.Length; i++)
            {
                var place = placements[i];
                var slot = Instantiate(slotTemplate, board);
                slot.gameObject.SetActive(true);
                slot.name = "Bottle " + (i + 1);
                var rect = slot.Rect;
                rect.sizeDelta = new Vector2(place.Width, place.Height);
                rect.anchoredPosition = ToBoard(place.CenterX, place.Bottom);

                var viewObject = Instantiate(bottlePrefab);
                viewObject.gameObject.SetActive(true);
                var view = (IBottleView)viewObject;
                view.Root.sizeDelta = new Vector2(place.Width - 2f, place.Height - 4f);
                slot.Init(i, view);
                if (!preview)
                    slot.Tapped += OnSlotTapped;
                _slots.Add(slot);
            }

            Refresh(state, false);
            Fit();
            if (!preview)
                return;
            foreach (var slot in _slots)
                DontSave(slot.transform);
            foreach (var shelf in _shelves)
                DontSave(shelf);
        }

        static void DontSave(Transform root)
        {
            root.gameObject.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
            foreach (Transform child in root)
                DontSave(child);
        }

        public void Refresh(LevelState state, bool animated = true)
        {
            for (int i = 0; i < _slots.Count && i < state.BottleCount; i++)
            {
                var bottle = state[i];
                bool completed = LooksCompleted(bottle, state.Unit);
                _slots[i].View.Show(bottle, state.Unit);
                _slots[i].View.SetCompleted(completed, animated);
                _slots[i].SetDone(completed, animated);
            }
        }

        public BottleSlot SlotAt(int index) => index >= 0 && index < _slots.Count ? _slots[index] : null;

        // Фигурка «собрана», когда рецепт налит до верха; одноцветная половина двухцветного рецепта — ещё нет.
        public static bool LooksCompleted(Bottle bottle, int unit) =>
            bottle.Recipe != null ? bottle.IsFull : bottle.IsCompleted(unit);

        void OnSlotTapped(BottleSlot slot) => BottleTapped?.Invoke(slot.Index);

        void OnRectTransformDimensionsChange()
        {
            if (_boardSize != Vector2.zero)
                Fit();
        }

        void Fit()
        {
            var area = ((RectTransform)transform).rect;
            if (area.width <= 0f || area.height <= 0f)
                return;
            // Как zoom в Game.dc.html: высота берётся не больше 420 pt, поэтому на длинных экранах бутылки не раздуваются.
            // Снизу держим запас: мировая полка спускается ниже своей ячейки, а над бустерами торчат бейджи.
            float reserve = BottomReserve * MaxScale;
            float height = Mathf.Min(area.height - reserve, MaxAvailableHeight * MaxScale);
            float scale = Mathf.Min(MaxScale, area.width / _boardSize.x, height / _boardSize.y);
            board.sizeDelta = _boardSize;
            board.localScale = new Vector3(scale, scale, 1f);

            // Доска по центру поля, но если снизу остаётся меньше запаса — поднимаем её.
            float bottomGap = (area.height - _boardSize.y * scale) / 2f;
            board.anchoredPosition = new Vector2(0f, Mathf.Max(0f, reserve - bottomGap));
        }

        void Clear()
        {
            foreach (var slot in _slots)
                if (slot != null)
                    Discard(slot.gameObject);
            foreach (var shelf in _shelves)
                if (shelf != null)
                    Discard(shelf.gameObject);
            _slots.Clear();
            _shelves.Clear();
        }

        static void Discard(GameObject target)
        {
            if (Application.isPlaying)
                Destroy(target);
            else
                DestroyImmediate(target);
        }

        // Что ставить посередине на два ряда: фигурку спасения, иначе большую колбу.
        static int FindCenterpiece(LevelState state)
        {
            for (int i = 0; i < state.BottleCount; i++)
                if (state[i].Recipe != null)
                    return i;
            for (int i = 0; i < state.BottleCount; i++)
                if (state[i].Capacity > state.Unit)
                    return i;
            return -1;
        }

        void LayoutRows(LevelState state, Placement[] result)
        {
            int count = state.BottleCount;
            int rows = count <= 10 ? 2 : 3;
            int perRow = Mathf.CeilToInt(count / (float)rows);
            float baseSize = count <= 8 ? 70f : count <= 10 ? 58f : 50f;
            float rowGap = rows == 3 ? 22f : 34f;

            var rowWidths = new List<float>();
            var rowHeights = new List<float>();
            for (int start = 0; start < count; start += perRow)
            {
                int end = Mathf.Min(count, start + perRow);
                float width = RowPadding * 2f + Gap * (end - start - 1);
                float height = 0f;
                for (int i = start; i < end; i++)
                {
                    float size = SizeOf(state[i], state.Unit, baseSize);
                    result[i].Width = size + 2f;
                    result[i].Height = Mathf.Round(size * 2.4f) + 4f;
                    width += result[i].Width;
                    height = Mathf.Max(height, result[i].Height);
                }
                rowWidths.Add(width);
                rowHeights.Add(height);
            }

            float boardWidth = 0f;
            float boardHeight = TopPadding;
            for (int r = 0; r < rowWidths.Count; r++)
            {
                boardWidth = Mathf.Max(boardWidth, rowWidths[r] + 16f);
                boardHeight += rowHeights[r] + ShelfHeight - ShelfOverlap + (r > 0 ? rowGap : 0f);
            }
            _boardSize = new Vector2(boardWidth, boardHeight);

            float y = TopPadding;
            for (int r = 0, start = 0; r < rowWidths.Count; r++, start += perRow)
            {
                float bottom = y + rowHeights[r];
                float x = (boardWidth - rowWidths[r]) / 2f + RowPadding;
                for (int i = start; i < Mathf.Min(count, start + perRow); i++)
                {
                    result[i].CenterX = x + result[i].Width / 2f;
                    result[i].Bottom = bottom;
                    x += result[i].Width + Gap;
                }
                _shelfPlan.Add(new Vector3(boardWidth / 2f, bottom - ShelfOverlap, rowWidths[r] + 16f));
                y = bottom + ShelfHeight - ShelfOverlap + rowGap;
            }
        }

        // Большая колба или фигурка в центре на два ряда, остальные бутылки слева и справа, как раскладка grid в макете.
        // Фигурка вдвое шире колбы, поэтому рядом с ней по две бутылки с каждой стороны, а лишние — рядом ниже.
        void LayoutGrid(LevelState state, int center, bool figure, Placement[] result)
        {
            const float column = 52f;
            const float cellHeight = 150f;
            const float cellGap = 6f;
            const float gridTop = 20f;
            const float rowGap = 30f;
            float centerColumn = figure ? FigureWidth + 4f : 116f;

            var others = new List<int>();
            for (int i = 0; i < state.BottleCount; i++)
                if (i != center)
                    others.Add(i);
            int besideCount = figure ? Mathf.Min(others.Count, BesideFigure) : others.Count;
            // Одна бутылка в нижнем ряду смотрится случайной: тогда рядом с фигуркой по одной с каждой стороны, внизу пять
            if (figure && others.Count == BesideFigure + 1)
                besideCount = BesideFigure / 2;

            int perRow = Mathf.CeilToInt(besideCount / 2f);
            int left = Mathf.CeilToInt(perRow / 2f);
            int columns = perRow + 1;
            float gridWidth = perRow * column + centerColumn + (columns - 1) * cellGap;
            float shelfWidth = gridWidth + 8f;
            float gridBottom = gridTop + cellHeight * 2f + cellGap;

            int below = others.Count - besideCount;
            float bottleHeight = Mathf.Round(50f * 2.4f) + 4f;
            float belowWidth = below > 0 ? RowPadding * 2f + below * column + (below - 1) * Gap : 0f;
            float boardWidth = Mathf.Max(shelfWidth, belowWidth + 16f);
            float boardHeight = gridBottom + ShelfHeight - ShelfOverlap;
            float belowBottom = boardHeight + rowGap + bottleHeight;
            if (below > 0)
                boardHeight = belowBottom + ShelfHeight - ShelfOverlap;
            _boardSize = new Vector2(boardWidth, boardHeight);
            float x0 = (boardWidth - gridWidth) / 2f;

            float ColumnCenter(int c)
            {
                float x = x0;
                for (int k = 0; k < c; k++)
                    x += (k == left ? centerColumn : column) + cellGap;
                return x + (c == left ? centerColumn : column) / 2f;
            }

            for (int n = 0; n < besideCount; n++)
            {
                int row = n / perRow;
                int position = n % perRow;
                int col = position < left ? position : position + 1;
                int index = others[n];
                result[index].Width = column;
                result[index].Height = bottleHeight;
                result[index].CenterX = ColumnCenter(col);
                result[index].Bottom = gridTop + cellHeight * (row + 1) + cellGap * row;
            }

            result[center].Width = figure ? FigureWidth + 2f : 114f;
            result[center].Height = figure ? Mathf.Round(FigureWidth * 1.2f) + 4f : Mathf.Round(112f * 2.4f) + 4f;
            result[center].CenterX = ColumnCenter(left);
            result[center].Bottom = gridBottom;
            _shelfPlan.Add(new Vector3(boardWidth / 2f, gridBottom - ShelfOverlap, shelfWidth));

            if (below == 0)
                return;
            float x = (boardWidth - belowWidth) / 2f + RowPadding;
            for (int n = besideCount; n < others.Count; n++)
            {
                int index = others[n];
                result[index].Width = column;
                result[index].Height = bottleHeight;
                result[index].CenterX = x + column / 2f;
                result[index].Bottom = belowBottom;
                x += column + Gap;
            }
            _shelfPlan.Add(new Vector3(boardWidth / 2f, belowBottom - ShelfOverlap, belowWidth + 16f));
        }

        static float SizeOf(Bottle bottle, int unit, float baseSize) =>
            bottle.Capacity > unit ? Mathf.Round(baseSize * 1.35f) : baseSize;

        void AddShelf(RectTransform template, float centerX, float top, float width)
        {
            var shelf = Instantiate(template, board);
            shelf.name = "Shelf " + (_shelves.Count + 1);
            shelf.gameObject.SetActive(true);
            shelf.SetAsFirstSibling();
            shelf.sizeDelta = new Vector2(width, ShelfHeight);
            shelf.anchoredPosition = ToBoard(centerX, top + ShelfHeight);
            _shelves.Add(shelf);
        }

        // Макет считает y сверху вниз от левого верхнего угла, доска — от центра вверх.
        Vector2 ToBoard(float x, float bottomY) =>
            new Vector2(x - _boardSize.x / 2f, _boardSize.y / 2f - bottomY);
    }
}
