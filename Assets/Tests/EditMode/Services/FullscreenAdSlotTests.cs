using BottleSort.Services;
using NUnit.Framework;

namespace BottleSort.Tests
{
    public class FullscreenAdSlotTests
    {
        const double Minute = 60;

        static FullscreenAdSlot LoadedInterstitial(double at)
        {
            var slot = FullscreenAdSlot.Interstitial();
            slot.OnLoadStarted(at);
            slot.OnLoaded(at);
            return slot;
        }

        static void Fail(FullscreenAdSlot slot, double at)
        {
            slot.OnLoadStarted(at);
            slot.OnFailed(at);
        }

        [Test]
        public void Empty_NeedsLoad()
        {
            Assert.IsTrue(FullscreenAdSlot.Interstitial().NeedsLoad(0));
            Assert.IsTrue(FullscreenAdSlot.Rewarded().NeedsLoad(0));
        }

        [Test]
        public void Loading_NoSecondRequest()
        {
            var slot = FullscreenAdSlot.Interstitial();
            slot.OnLoadStarted(0);
            Assert.IsFalse(slot.NeedsLoad(30));
            Assert.IsFalse(slot.NeedsLoad(30, playerWaiting: true), "даже если игрок ждёт — две загрузки сразу не идут");
        }

        [Test]
        public void HungLoad_CountsAsErrorAfterAMinute()
        {
            // Ни ответа, ни ошибки: без таймаута слот ждал бы до перезапуска игры и межстраничной больше не было бы.
            var slot = FullscreenAdSlot.Interstitial();
            slot.OnLoadStarted(100);
            Assert.IsFalse(slot.LoadTimedOut(159));
            Assert.IsTrue(slot.LoadTimedOut(160));
            slot.OnFailed(160);
            Assert.IsTrue(slot.NeedsLoad(165));
        }

        [Test]
        public void Interstitial_FreshAd_NoRequestsFor25Minutes()
        {
            var slot = LoadedInterstitial(0);
            Assert.IsFalse(slot.NeedsLoad(24 * Minute));
            Assert.IsTrue(slot.IsFresh(24 * Minute));
        }

        [Test]
        public void Interstitial_ReplacementLoadsWhileOldOneStillWorks()
        {
            var slot = LoadedInterstitial(0);
            Assert.IsTrue(slot.NeedsLoad(25 * Minute), "за 5 минут до конца срока грузим замену");
            slot.OnLoadStarted(25 * Minute);
            Assert.IsTrue(slot.IsFresh(26 * Minute), "пока замена грузится, старую можно показать");

            slot.OnLoaded(26 * Minute);
            Assert.IsTrue(slot.IsFresh(50 * Minute));
            Assert.IsFalse(slot.NeedsLoad(50 * Minute));
        }

        [Test]
        public void Interstitial_ReplacementFailed_OldOneStaysAndRetriesSoon()
        {
            var slot = LoadedInterstitial(0);
            Fail(slot, 25 * Minute);
            Assert.IsTrue(slot.IsFresh(25 * Minute + 1));
            Assert.IsFalse(slot.NeedsLoad(25 * Minute + 4));
            Assert.IsTrue(slot.NeedsLoad(25 * Minute + 5));
        }

        [Test]
        public void Interstitial_ExpiresAfter30Minutes()
        {
            var slot = LoadedInterstitial(0);
            Assert.IsFalse(slot.IsFresh(30 * Minute));
            slot.OnTaken();
            Assert.IsTrue(slot.NeedsLoad(30 * Minute));
        }

        [Test]
        public void Interstitial_AfterShow_LoadsAgainRightAway()
        {
            var slot = LoadedInterstitial(0);
            slot.OnTaken();
            Assert.IsTrue(slot.NeedsLoad(Minute));
        }

        [Test]
        public void Interstitial_AfterShow_LoadsAtOnceEvenIfReplacementJustFailed()
        {
            var slot = LoadedInterstitial(0);
            Fail(slot, 25 * Minute);
            Fail(slot, 25 * Minute + 5);
            slot.OnTaken();
            Assert.IsTrue(slot.NeedsLoad(25 * Minute + 6));
        }

        [Test]
        public void Interstitial_RetriesIn5_15_30_ThenEveryMinute()
        {
            var slot = FullscreenAdSlot.Interstitial();
            Assert.AreEqual(5, slot.RetryDelay(1));
            Assert.AreEqual(15, slot.RetryDelay(2));
            Assert.AreEqual(30, slot.RetryDelay(3));
            Assert.AreEqual(60, slot.RetryDelay(4));
            Assert.AreEqual(60, slot.RetryDelay(20), "дальше раз в минуту, пока игра открыта");

            Fail(slot, 100);
            Assert.IsFalse(slot.NeedsLoad(104));
            Assert.IsTrue(slot.NeedsLoad(105));
            Fail(slot, 105);
            Assert.IsFalse(slot.NeedsLoad(119));
            Assert.IsTrue(slot.NeedsLoad(120));
        }

        [Test]
        public void Interstitial_TwoEmptyAnswersAfterShow_StillReadyForNextLevel()
        {
            // Реклама показана в 0 с, следующая положена через минуту. Яндекс дважды отвечает «нет рекламы»
            // (загрузка ~3 с), третий раз отдаёт ролик. Раньше пауза была 30 с, потом минута, и к концу
            // следующего уровня ролика не было — это и есть «Не загрузилась» в аналитике.
            var slot = FullscreenAdSlot.Interstitial();
            double t = 0;
            for (int attempt = 0; attempt < 3; attempt++)
            {
                while (!slot.NeedsLoad(t))
                    t++;
                slot.OnLoadStarted(t);
                t += 3;
                if (attempt < 2)
                    slot.OnFailed(t);
                else
                    slot.OnLoaded(t);
            }
            Assert.IsTrue(slot.IsFresh(InterstitialPolicy.MinSecondsBetween), $"ролик пришёл в {t} с");
        }

        [Test]
        public void SuccessResetsThePause()
        {
            var slot = FullscreenAdSlot.Interstitial();
            Fail(slot, 0);
            Fail(slot, 5);
            Fail(slot, 20);
            slot.OnLoadStarted(50);
            slot.OnLoaded(52);
            slot.OnTaken();
            Fail(slot, 60);
            Assert.IsTrue(slot.NeedsLoad(65), "счёт ошибок начался заново: снова 5 с");
        }

        [Test]
        public void Rewarded_LivesUntilShown_NoReplacementBeforeItExpires()
        {
            var slot = FullscreenAdSlot.Rewarded();
            slot.OnLoadStarted(0);
            slot.OnLoaded(0);
            Assert.IsFalse(slot.NeedsLoad(29 * Minute), "ролик за награду заранее не меняем — лишняя загрузка");
            Assert.IsFalse(slot.IsFresh(30 * Minute));
            slot.OnTaken();
            Assert.IsTrue(slot.NeedsLoad(30 * Minute));
        }

        [Test]
        public void Rewarded_AfterThreeErrors_WaitsForThePlayer()
        {
            var slot = FullscreenAdSlot.Rewarded();
            Fail(slot, 0);
            Assert.IsFalse(slot.NeedsLoad(29));
            Assert.IsTrue(slot.NeedsLoad(30));
            Fail(slot, 30);
            Assert.IsTrue(slot.NeedsLoad(90));
            Fail(slot, 90);
            Assert.IsTrue(slot.NeedsLoad(210));
            Fail(slot, 210);
            Assert.IsFalse(slot.NeedsLoad(210 + 60 * Minute), "у игрока нет рекламы — сами больше не просим");

            Assert.IsTrue(slot.NeedsLoad(300, playerWaiting: true), "нажал кнопку — грузим сразу");
        }

        [Test]
        public void Rewarded_PlayerTaps_LoadsAtOnce_ButNotOnEveryTap()
        {
            var slot = FullscreenAdSlot.Rewarded();
            Fail(slot, 0);
            Assert.IsFalse(slot.NeedsLoad(5));
            Assert.IsFalse(slot.NeedsLoad(5, playerWaiting: true), "с прошлой загрузки меньше 10 с");
            Assert.IsTrue(slot.NeedsLoad(10, playerWaiting: true));
            slot.OnLoadStarted(10);
            slot.OnLoaded(12);
            Assert.IsFalse(slot.NeedsLoad(13, playerWaiting: true), "ролик уже есть");
        }
    }
}
