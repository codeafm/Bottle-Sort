using System;
using UnityEngine;
using UnityEngine.UI;

namespace BottleSort.UI
{
    // Монеты веером летят из карточки награды в счётчик. Копии иконки создаются на время полёта и удаляются,
    // поэтому в сцене под это ничего держать не нужно (колесо удачи делает то же своими монетами).
    public static class CoinFlight
    {
        const float FlightTime = 0.6f;
        const float Stagger = 0.06f;

        public static void Fly(RectTransform layer, Image template, Vector3 fromWorld, Vector3 toWorld, int count,
            Action<int> coinArrived)
        {
            Vector2 from = layer.InverseTransformPoint(fromWorld);
            Vector2 to = layer.InverseTransformPoint(toWorld);
            var size = template.rectTransform.rect.size;
            for (int i = 0; i < count; i++)
            {
                var coin = new GameObject("FlyingCoin", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
                coin.SetParent(layer, false);
                coin.sizeDelta = size;
                var image = coin.GetComponent<Image>();
                image.sprite = template.sprite;
                image.raycastTarget = false;
                coin.anchoredPosition = from;

                var bend = new Vector2(UnityEngine.Random.Range(-120f, 120f), UnityEngine.Random.Range(40f, 160f));
                int index = i;
                Tweens.Run(coin, "fly", FlightTime, t =>
                {
                    var mid = (from + to) / 2f + bend;
                    coin.anchoredPosition = Vector2.Lerp(Vector2.Lerp(from, mid, t), Vector2.Lerp(mid, to, t), t);
                    coin.localScale = Vector3.one * Mathf.Lerp(1f, 0.6f, t);
                }, Ease.InOutSine, () =>
                {
                    UnityEngine.Object.Destroy(coin.gameObject);
                    coinArrived?.Invoke(index);
                }, i * Stagger);
            }
        }
    }
}
