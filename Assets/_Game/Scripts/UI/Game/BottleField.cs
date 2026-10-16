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
        const float Gap = 12f;
        const float RowPadding = 10f;
        const float TopPadding = 24f;
        const float ShelfHeight = 12f;
        const float ShelfOverlap = 6f;

        [SerializeField] RectTransform board;
        [SerializeField] BottleSlot slotTemplate;
        [SerializeField] RectTransform shelfTemplate;
        // Любой компонент с IBottleView: сейчас заглушка, потом адаптер BottleView.
        [SerializeField] MonoBehaviour bottleTemplate;

        readonly List<BottleSlot> _slots = new List<BottleSlot>();
        readonly List<RectTransform> _shelves = new List<RectTransform>();
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

        public void Build(LevelState state, bool singleRow)
        {
            Clear();
            var placements = new Placement[state.BottleCount];
            int flask = FindFlask(state);
            if (flask >= 0 && !singleRow)
                LayoutGrid(state, flask, placements);
            else
                LayoutRows(state, singleRow, placements);

            for (int i = 0; i < placements.Length; i++)
            {
                var place = placements[i];
                var slot = Instantiate(slotTemplate, board);
                slot.gameObject.SetActive(true);
                slot.name = "Bottle " + (i + 1);
                var rect = slot.Rect;
                rect.sizeDelta = new Vector2(place.Width, place.Height);
                rect.anchoredPosition = ToBoard(place.CenterX, place.Bottom);

                var viewObject = Instantiate(bottleTemplate);
                viewObject.gameObject.SetActive(true);
                var view = (IBottleView)viewObject;
                view.Root.sizeDelta = new Vector2(place.Width - 2f, place.Height - 4f);
                slot.Init(i, view);
                slot.Tapped += OnSlotTapped;
                _slots.Add(slot);
            }

            Refresh(state, false);
            Fit();
        }

        public void Refresh(LevelState state, bool animated = true)
        {
            for (int i = 0; i < _slots.Count && i < state.BottleCount; i++)
            {
                var bottle = state[i];
                bool completed = bottle.IsCompleted(state.Unit);
                _slots[i].View.Show(bottle, state.Unit);
                _slots[i].View.SetCompleted(completed, animated);
                _slots[i].SetDone(completed, animated);
            }
        }

        public BottleSlot SlotAt(int index) => index >= 0 && index < _slots.Count ? _slots[index] : null;

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
            float scale = Mathf.Min(MaxScale, area.width / _boardSize.x, area.height / _boardSize.y);
            board.sizeDelta = _boardSize;
            board.localScale = new Vector3(scale, scale, 1f);
        }

        void Clear()
        {
            foreach (var slot in _slots)
                Destroy(slot.gameObject);
            foreach (var shelf in _shelves)
                Destroy(shelf.gameObject);
            _slots.Clear();
            _shelves.Clear();
        }

        static int FindFlask(LevelState state)
        {
            for (int i = 0; i < state.BottleCount; i++)
                if (state[i].Capacity > state.Unit)
                    return i;
            return -1;
        }

        void LayoutRows(LevelState state, bool singleRow, Placement[] result)
        {
            int count = state.BottleCount;
            int rows = singleRow ? 1 : count <= 10 ? 2 : 3;
            int perRow = Mathf.CeilToInt(count / (float)rows);
            float baseSize = singleRow || count <= 8 ? 70f : count <= 10 ? 58f : 50f;
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
                AddShelf(boardWidth / 2f, bottom - ShelfOverlap, rowWidths[r] + 16f);
                y = bottom + ShelfHeight - ShelfOverlap + rowGap;
            }
        }

        // Большая колба в центре на два ряда, остальные бутылки слева и справа, как раскладка grid в макете.
        void LayoutGrid(LevelState state, int flask, Placement[] result)
        {
            const float column = 52f;
            const float flaskColumn = 116f;
            const float cellHeight = 150f;
            const float cellGap = 6f;
            const float gridTop = 20f;

            var others = new List<int>();
            for (int i = 0; i < state.BottleCount; i++)
                if (i != flask)
                    others.Add(i);

            int perRow = Mathf.CeilToInt(others.Count / 2f);
            int left = Mathf.CeilToInt(perRow / 2f);
            int columns = perRow + 1;
            float gridWidth = perRow * column + flaskColumn + (columns - 1) * cellGap;
            float shelfWidth = gridWidth + 8f;
            _boardSize = new Vector2(shelfWidth, gridTop + cellHeight * 2f + cellGap + ShelfHeight - ShelfOverlap);
            float x0 = (shelfWidth - gridWidth) / 2f;

            float ColumnCenter(int c)
            {
                float x = x0;
                for (int k = 0; k < c; k++)
                    x += (k == left ? flaskColumn : column) + cellGap;
                return x + (c == left ? flaskColumn : column) / 2f;
            }

            for (int n = 0; n < others.Count; n++)
            {
                int row = n / perRow;
                int position = n % perRow;
                int col = position < left ? position : position + 1;
                int index = others[n];
                result[index].Width = column;
                result[index].Height = Mathf.Round(50f * 2.4f) + 4f;
                result[index].CenterX = ColumnCenter(col);
                result[index].Bottom = gridTop + cellHeight * (row + 1) + cellGap * row;
            }

            result[flask].Width = 114f;
            result[flask].Height = Mathf.Round(112f * 2.4f) + 4f;
            result[flask].CenterX = ColumnCenter(left);
            result[flask].Bottom = gridTop + cellHeight * 2f + cellGap;
            AddShelf(shelfWidth / 2f, result[flask].Bottom - ShelfOverlap, shelfWidth);
        }

        static float SizeOf(Bottle bottle, int unit, float baseSize) =>
            bottle.Capacity > unit ? Mathf.Round(baseSize * 1.35f) : baseSize;

        void AddShelf(float centerX, float top, float width)
        {
            var shelf = Instantiate(shelfTemplate, board);
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
