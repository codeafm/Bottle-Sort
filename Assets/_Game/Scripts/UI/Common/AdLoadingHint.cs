using TMPro;
using UnityEngine.UI;

namespace BottleSort.UI
{
    // Кнопка «за рекламу», а ролик ещё не загружен: не ждём молча, а сразу говорим и догружаем.
    // Где есть тост — пишем в тост, в попапах без тоста на пару секунд меняем подпись самой кнопки.
    public static class AdLoadingHint
    {
        const float Seconds = 2f;
        const string Channel = "ad-loading";

        public static bool Check(Toast toast)
        {
            if (RewardedAds.EnsureReady())
                return true;
            if (toast != null)
                toast.ShowKey("toast_ad_loading", Seconds);
            return false;
        }

        public static bool Check(Button button)
        {
            if (RewardedAds.EnsureReady())
                return true;
            var label = button != null ? button.GetComponentInChildren<TMP_Text>(true) : null;
            // Пока висит «Загружается…», повторные тапы ничего не меняют, иначе вернули бы не ту подпись.
            if (label == null || Tweens.IsRunning(label, Channel))
                return false;
            string original = label.text;
            label.text = Loc.Get("ad_loading_short");
            Tweens.Delay(label, Channel, Seconds, () => label.text = original);
            return false;
        }
    }
}
