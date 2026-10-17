using System.Threading.Tasks;
using BottleSort.Core;
using BottleSort.Gameplay;
using BottleSort.Services;

namespace BottleSort.UI
{
    // Проигрыш: кончились ходы на уровне с лимитом или позиция точно тупиковая (LEVEL_DESIGN §7.3).
    // Игроку — карточка с выбором: продолжить за рекламу (+5 ходов / +1 бутылка) или «Заново».
    // Проигрыш считается для межстраничной рекламы один раз за попытку, ролик — только на «Заново».
    public sealed partial class GameScreen
    {
        const int ExtraMovesForAd = 5;
        // Карточка выходит, когда налив доиграл и игрок увидел последний ход.
        const float LossCardDelay = 0.9f;
        // Как в §7.3: проверка в фоне, бюджет небольшой; не успели доказать тупик — молчим.
        const int DeadEndBudget = 20_000;

        bool _lossCounted;
        int _deadEndCheck;

        // Новый уровень или «Заново»: сброс проигрыша и новый запрос игрового баннера.
        void OnAttemptStarted()
        {
            _lossCounted = false;
            _deadEndCheck++;
            Tweens.Kill(this, "loss");
            Ads.LevelStarted(_level);
        }

        void CountLoss()
        {
            if (_lossCounted || _session.IsTutorial)
                return;
            _lossCounted = true;
            Ads.LevelLost(_level);
        }

        void OnOutOfMoves()
        {
            CountLoss();
            Tweens.Delay(this, "loss", LossCardDelay, ShowOutOfMoves);
        }

        void ShowOutOfMoves()
        {
            if (!IsOpen || _session == null || !_session.IsOutOfMoves)
                return;
            if (IsAnimating)
            {
                Tweens.Delay(this, "loss", 0.3f, ShowOutOfMoves);
                return;
            }
            var card = Router.Get<RewardPopup>();
            if (card == null)
            {
                toast.ShowKey("toast_out_of_moves", 2.5f);
                return;
            }
            card.SetupChoice("lose_moves_title", "lose_moves_text", default, "lose_moves_ad", WatchAdForMoves,
                "lose_restart", RestartAfterLoss);
            // Мимо карточки — закрыть и, например, отменить ход.
            card.AllowDismiss();
            Router.ShowPopup(ScreenId.Reward);
        }

        void WatchAdForMoves()
        {
            var session = _session;
            RewardedAds.Show(watched =>
            {
                if (session != _session || !IsOpen)
                    return;
                if (!watched)
                {
                    toast.ShowKey("toast_reward_ad_failed", 2.2f);
                    ShowOutOfMoves();
                    return;
                }
                if (!_session.AddMoves(ExtraMovesForAd))
                    return;
                _lossCounted = false;
                RefreshHud();
                toast.Show(Loc.Format("toast_extra_moves", ExtraMovesForAd), 1.6f);
                UiFeedback.Play(UiSound.Reward);
            });
        }

        void RestartAfterLoss() => Ads.ShowInterstitialAfterLossIfDue(_level, () =>
        {
            if (IsOpen && _session != null)
                Restart();
        });

        async void CheckDeadEnd()
        {
            if (_guide != null || _session == null || _session.IsWon || _session.IsOutOfMoves)
                return;
            int check = ++_deadEndCheck;
            var session = _session;
            int moves = session.MovesMade;
            int bottles = session.State.BottleCount;
            var snapshot = session.State.Clone();
            bool deadEnd = await Task.Run(() => Solver.IsDeadEnd(snapshot, DeadEndBudget));
            // Пока считали, игрок мог походить, отменить ход или взять бутылку — тогда ответ уже не про эту позицию.
            if (!deadEnd || check != _deadEndCheck || session != _session || !IsOpen || _finished
                || session.MovesMade != moves || session.State.BottleCount != bottles)
                return;
            CountLoss();
            Tweens.Delay(this, "loss", LossCardDelay, () => ShowDeadEnd(moves));
        }

        void ShowDeadEnd(int moves)
        {
            if (!IsOpen || _finished || _session == null || _session.MovesMade != moves)
                return;
            // Карточка не всплывает посреди налива.
            if (IsAnimating)
            {
                Tweens.Delay(this, "loss", 0.3f, () => ShowDeadEnd(moves));
                return;
            }
            var card = Router.Get<RewardPopup>();
            if (card == null)
                return;
            if (_session.CanAddBottle && !_generated.Spec.BigFlask)
                card.SetupChoice("deadend_title", "deadend_text", new RewardBundle(0, bottles: 1), "deadend_bottle_ad",
                    WatchAdForBottle, "lose_restart", RestartAfterLoss);
            else
                card.Setup("deadend_title", "deadend_text_restart", default, "lose_restart", RestartAfterLoss);
            card.AllowDismiss();
            Router.ShowPopup(ScreenId.Reward);
        }

        // Бутылка за рекламу не тратит запас бустеров «+Бутылка».
        void WatchAdForBottle()
        {
            var session = _session;
            RewardedAds.Show(watched =>
            {
                if (session != _session || !IsOpen)
                    return;
                if (!watched)
                {
                    toast.ShowKey("toast_reward_ad_failed", 2.2f);
                    return;
                }
                if (!_session.AddBottle())
                    return;
                _lossCounted = false;
                GameEvents.RaiseExtraBottleAdded();
                SkipPours();
                Deselect();
                field.Build(_session.State);
                RefreshHud();
                RefreshBoosters();
                UiFeedback.Play(UiSound.Reward);
            });
        }
    }
}
