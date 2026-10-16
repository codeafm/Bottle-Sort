using System.Collections.Generic;
using BottleSort.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BottleSort.UI
{
    // Карта уровней одной главы: извилистая дорожка снизу вверх, как в Levels.dc.html.
    public sealed class LevelMapScreen : UiScreen
    {
        const float StepY = 205f;
        const float DotSpacing = 32f;
        const float EdgePadding = 300f;

        // X узлов в долях ширины, повторяется по кругу — та же «змейка», что в макете.
        static readonly float[] Path = { 0.5f, 0.75f, 0.78f, 0.54f, 0.28f, 0.24f, 0.49f, 0.74f, 0.77f, 0.51f, 0.24f, 0.26f };

        [SerializeField] Button backButton;
        [SerializeField] TMP_Text totalStars;
        [SerializeField] LocalizedText chapterTitle;
        [SerializeField] LocalizedText chapterName;
        [SerializeField] TMP_Text chapterCount;
        [SerializeField] RectTransform chapterFill;
        [SerializeField] Button previousChapter;
        [SerializeField] Button nextChapter;
        [SerializeField] ScrollRect scroll;
        [SerializeField] RectTransform content;
        [SerializeField] LevelNode nodeTemplate;
        [SerializeField] Image dotTemplate;
        [SerializeField] Toast toast;

        readonly List<LevelNode> _nodePool = new List<LevelNode>();
        readonly List<Image> _dotPool = new List<Image>();
        int _chapter = 1;

        public override ScreenId Id => ScreenId.Levels;
        public override Theme? BackdropTheme => LevelPlan.ThemeOf(LevelSchedule.ChapterStart(_chapter));

        void Awake()
        {
            backButton.onClick.AddListener(() => Router.Show(ScreenId.Menu));
            previousChapter.onClick.AddListener(() => ShowChapter(_chapter - 1));
            nextChapter.onClick.AddListener(() => ShowChapter(_chapter + 1));
        }

        public override bool OnBack()
        {
            Router.Show(ScreenId.Menu);
            return true;
        }

        // Позиции узлов зависят от ширины экрана: при повороте или смене разрешения строим дорожку заново.
        void OnRectTransformDimensionsChange()
        {
            if (IsOpen && nodeTemplate != null)
                ShowChapter(_chapter);
        }

        protected override void OnOpen()
        {
            totalStars.text = Loc.Number(PlayerData.Current.TotalStars);
            ShowChapter(LevelPlan.ChapterOf(PlayerData.Current.highestUnlocked));
        }

        void ShowChapter(int chapter)
        {
            var data = PlayerData.Current;
            int lastOpenChapter = LevelPlan.ChapterOf(data.highestUnlocked);
            _chapter = Mathf.Clamp(chapter, 1, lastOpenChapter + 1);
            Router.RefreshBackdrop();

            int first = LevelSchedule.ChapterStart(_chapter);
            int last = LevelPlan.ChapterEnd(_chapter);
            int done = Mathf.Clamp(data.highestUnlocked - first, 0, last - first + 1);
            chapterTitle.SetKey("map_chapter", _chapter, first, last);
            chapterName.SetKey(LevelPlan.ChapterNameKey(_chapter));
            chapterCount.text = $"{done} / {last - first + 1}";
            chapterFill.anchorMax = new Vector2(done / (float)(last - first + 1), 1f);
            previousChapter.interactable = _chapter > 1;
            nextChapter.interactable = _chapter <= lastOpenChapter;

            BuildPath(first, last);
        }

        void BuildPath(int first, int last)
        {
            foreach (var node in _nodePool)
                node.gameObject.SetActive(false);
            foreach (var dot in _dotPool)
                dot.gameObject.SetActive(false);

            Canvas.ForceUpdateCanvases();
            var data = PlayerData.Current;
            int count = last - first + 1;
            float width = content.rect.width;
            content.sizeDelta = new Vector2(content.sizeDelta.x, count * StepY + EdgePadding * 2f);
            float height = content.sizeDelta.y;

            var points = new Vector2[count];
            for (int i = 0; i < count; i++)
            {
                float x = (Path[i % Path.Length] - 0.5f) * width;
                // Уровни идут снизу вверх, content растёт вниз от верхнего края.
                float y = -(height - EdgePadding - i * StepY);
                points[i] = new Vector2(x, y);
            }

            int dotIndex = 0;
            for (int i = 0; i + 1 < count; i++)
            {
                bool walked = first + i + 1 <= data.highestUnlocked;
                float length = Vector2.Distance(points[i], points[i + 1]);
                int dots = Mathf.Max(1, Mathf.FloorToInt(length / DotSpacing));
                for (int d = 1; d < dots; d++)
                {
                    var dot = DotAt(dotIndex++);
                    dot.rectTransform.anchoredPosition = Vector2.Lerp(points[i], points[i + 1], d / (float)dots);
                    dot.color = walked ? UiColors.Hex("#FFC83D") : new Color(1f, 1f, 1f, 0.35f);
                    dot.rectTransform.sizeDelta = Vector2.one * (walked ? 18f : 15f);
                }
            }

            int currentIndex = 0;
            for (int i = 0; i < count; i++)
            {
                int level = first + i;
                LevelNode.State state;
                if (level < data.highestUnlocked)
                    state = LevelNode.State.Done;
                else if (level == data.highestUnlocked)
                    state = LevelNode.State.Current;
                else
                    state = LevelPlan.IsBossNode(level) ? LevelNode.State.Boss : LevelNode.State.Locked;
                if (state == LevelNode.State.Current)
                    currentIndex = i;

                var node = NodeAt(i);
                ((RectTransform)node.transform).anchoredPosition = points[i];
                node.Setup(level, state, data.StarsOf(level), HandmadeLevels.IsTutorial(level));
            }
            // Текущий узел с пузырём «Играть!» рисуем поверх соседей.
            _nodePool[currentIndex].transform.SetAsLastSibling();

            Canvas.ForceUpdateCanvases();
            float viewport = scroll.viewport.rect.height;
            float target = height - EdgePadding - currentIndex * StepY - viewport * 0.5f;
            float range = Mathf.Max(1f, height - viewport);
            scroll.verticalNormalizedPosition = 1f - Mathf.Clamp01(target / range);
        }

        LevelNode NodeAt(int index)
        {
            while (_nodePool.Count <= index)
            {
                var node = Instantiate(nodeTemplate, content);
                node.Pressed += OnNodePressed;
                _nodePool.Add(node);
            }
            var result = _nodePool[index];
            result.gameObject.SetActive(true);
            result.transform.SetAsLastSibling();
            return result;
        }

        Image DotAt(int index)
        {
            while (_dotPool.Count <= index)
                _dotPool.Add(Instantiate(dotTemplate, content));
            var dot = _dotPool[index];
            dot.gameObject.SetActive(true);
            dot.transform.SetAsFirstSibling();
            return dot;
        }

        void OnNodePressed(int level)
        {
            if (level > PlayerData.Current.highestUnlocked)
            {
                toast.ShowKey("toast_level_locked", 1.2f);
                return;
            }
            Router.PlayLevel(level);
        }
    }
}
