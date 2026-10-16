using BottleSort.Core;
using UnityEngine;

namespace BottleSort.UI
{
    public enum BottleGlow
    {
        None,
        Selected,
        Hint
    }

    // Что игровому экрану нужно от картинки бутылки. BottleViewAdapter — настоящая бутылка vfx-art,
    // BottleStubView — простая на uGUI для меню, заставки и магазина.
    public interface IBottleView
    {
        RectTransform Root { get; }
        // Вид сам поднимает выбранную бутылку и трясёт её; иначе это делает ячейка поля.
        bool MovesItself { get; }
        void Show(Bottle bottle, int unit);
        void SetGlow(BottleGlow glow);
        void SetCompleted(bool completed, bool animated);
        void SetDimmed(bool dimmed);
        void Shake();
    }
}
