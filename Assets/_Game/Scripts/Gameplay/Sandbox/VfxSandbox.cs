using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace BottleSort.Gameplay.Sandbox
{
    // Песочница визуала: тап по бутылке — выбрать, по второй — перелить (упрощённые правила, без Game.Core).
    // Тап мимо бутылок или пробел — следующая тема фона. C — залп конфетти, V — дождь конфетти, H — подсказка.
    public sealed class VfxSandbox : MonoBehaviour
    {
        [SerializeField] BackgroundView background;
        [SerializeField] PourAnimator pourAnimator;
        [SerializeField] ConfettiEffect confetti;
        [SerializeField] List<BottleView> bottles = new List<BottleView>();

        BottleView selected;
        BottleView hinted;
        bool raining;

        void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.spaceKey.wasPressedThisFrame) background.NextTheme();
                if (keyboard.cKey.wasPressedThisFrame) confetti.Burst();
                if (keyboard.vKey.wasPressedThisFrame) ToggleRain();
                if (keyboard.hKey.wasPressedThisFrame) ShowHint();
            }

            var pointer = Pointer.current;
            if (pointer == null || !pointer.press.wasPressedThisFrame || pourAnimator.IsPlaying)
                return;

            var cam = Camera.main;
            Vector2 world = cam.ScreenToWorldPoint(pointer.position.ReadValue());
            var hit = HitTest(world);
            if (hit == null)
            {
                Deselect();
                background.NextTheme();
                return;
            }
            Tap(hit);
        }

        void Tap(BottleView bottle)
        {
            ClearHint();
            if (selected == null)
            {
                if (bottle.IsLocked || bottle.FrozenTurns > 0 || bottle.LayerCount == 0 || IsDone(bottle))
                {
                    bottle.Shake();
                    return;
                }
                selected = bottle;
                bottle.SetState(BottleVisualState.Selected);
                return;
            }

            if (bottle == selected)
            {
                Deselect();
                return;
            }

            var source = selected;
            selected = null;
            if (!CanPour(source, bottle))
            {
                source.SetState(IsDone(source) ? BottleVisualState.Completed : BottleVisualState.Normal);
                bottle.Shake();
                return;
            }
            Pour(source, bottle);
        }

        void Pour(BottleView source, BottleView target)
        {
            var from = new List<int>(source.Colors);
            var to = new List<int>(target.Colors);
            int top = from[from.Count - 1];
            int amount = 0;
            while (from.Count > 0 && from[from.Count - 1] == top && to.Count < target.Capacity)
            {
                from.RemoveAt(from.Count - 1);
                to.Add(top);
                amount++;
            }
            int sourceHidden = Mathf.Min(source.HiddenCount, Mathf.Max(from.Count - 1, 0));

            pourAnimator.Play(source, target, from, sourceHidden, to, target.HiddenCount, () => AfterPour(target));
        }

        void AfterPour(BottleView target)
        {
            bool completed = IsDone(target);
            foreach (var b in bottles)
                if (b.FrozenTurns > 0)
                    b.SetFrozen(b.FrozenTurns - 1);
            if (completed)
            {
                target.PlayCompleted();
                foreach (var b in bottles)
                    if (b.IsLocked && (b.LockColor < 0 || b.LockColor == target.Colors[0]))
                        b.SetLocked(false, b.LockColor);
            }

            bool won = true;
            foreach (var b in bottles)
                if (b.LayerCount > 0 && !IsDone(b))
                    won = false;
            if (won)
                confetti.Burst();
        }

        void ShowHint()
        {
            ClearHint();
            Deselect();
            foreach (var source in bottles)
            {
                if (source.LayerCount == 0 || IsDone(source) || source.IsLocked || source.FrozenTurns > 0)
                    continue;
                foreach (var target in bottles)
                {
                    if (target == source || target.LayerCount == 0 || !CanPour(source, target))
                        continue;
                    source.SetState(BottleVisualState.Selected);
                    target.SetState(BottleVisualState.Hint);
                    selected = source;
                    hinted = target;
                    return;
                }
            }
        }

        void ClearHint()
        {
            if (hinted == null)
                return;
            hinted.SetState(IsDone(hinted) ? BottleVisualState.Completed : BottleVisualState.Normal);
            hinted = null;
        }

        void Deselect()
        {
            if (selected == null)
                return;
            selected.SetState(IsDone(selected) ? BottleVisualState.Completed : BottleVisualState.Normal);
            selected = null;
        }

        void ToggleRain()
        {
            raining = !raining;
            if (raining) confetti.StartRain();
            else confetti.StopRain();
        }

        static bool CanPour(BottleView source, BottleView target)
        {
            if (target.IsLocked || target.FrozenTurns > 0 || target.LayerCount >= target.Capacity)
                return false;
            if (target.LayerCount == 0)
                return true;
            return target.Colors[target.LayerCount - 1] == source.Colors[source.LayerCount - 1];
        }

        static bool IsDone(BottleView bottle)
        {
            var colors = bottle.Colors;
            if (colors.Count == 0 || colors.Count != Mathf.Min(bottle.Capacity, 4) && colors.Count != bottle.Capacity)
                return false;
            for (int i = 1; i < colors.Count; i++)
                if (colors[i] != colors[0])
                    return false;
            return true;
        }

        BottleView HitTest(Vector2 world)
        {
            foreach (var bottle in bottles)
            {
                var outline = bottle.LocalOutline;
                var scale = bottle.transform.lossyScale;
                var origin = (Vector2)bottle.transform.position;
                var rect = new Rect(origin + new Vector2(outline.x * scale.x, outline.y * scale.y),
                    new Vector2(outline.width * scale.x, outline.height * scale.y));
                if (rect.Contains(world))
                    return bottle;
            }
            return null;
        }
    }
}
