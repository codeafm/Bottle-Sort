using System.Collections.Generic;
using BottleSort.Core;
using BottleSort.Gameplay;
using BottleSort.Services;

namespace BottleSort.UI
{
    // Проигрыш: кончились ходы на уровне с лимитом или перелить больше нечего.
    // Игроку — карточка с выбором: продолжить (+5 ходов за рекламу, +1 бутылка, отмена хода) или «Заново».
    // Проигрыш считается для межстраничной рекламы один раз за попытку, ролик — только на «Заново».
    public sealed partial class GameScreen
    {
        const int ExtraMovesForAd = 5;
        // Карточка выходит, когда налив доиграл и игрок увидел последний ход.
        const float LossCardDelay = 0.9f;

        bool _lossCounted;
        // Какая по счёту попытка этого уровня: 1 — первый заход, дальше «Заново».
        int _attempt;

        // Новый уровень или «Заново»: сброс проигрыша. Баннер тут не перезапрашиваем — его обновляет SDK.
        void OnAttemptStarted(bool newLevel)
        {
            _lossCounted = false;
            _attempt = newLevel ? 1 : _attempt + 1;
            Tweens.Kill(this, "loss");
            Analytics.LevelStarted(_level, LevelPlan.TypeOf(_level), _attempt);
        }

        void CountLoss(string reason)
        {
            if (_lossCounted || _session.IsTutorial)
                return;
            _lossCounted = true;
            Analytics.LevelLost(_level, reason, _session.MovesMade);
        }

        void OnOutOfMoves()
        {
            CountLoss("Кончились ходы");
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
            // Ролик ещё грузится: говорим об этом и через пару секунд снова даём выбор.
            if (!AdLoadingHint.Check(toast))
            {
                Tweens.Delay(this, "loss", 2f, ShowOutOfMoves);
                return;
            }
            var session = _session;
            RewardedAds.Show("+5 ходов после проигрыша", watched =>
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

        // «Ходов нет» — только когда перелить действительно нечего (решение заказчика). Солвер тут не спрашиваем:
        // карточку «уровень уже не решить» при живых ходах игроки принимали за ошибку.
        void CheckNoMoves()
        {
            if (_guide != null || _session == null || _session.IsWon || _session.IsOutOfMoves || HasPour(_session.State))
                return;
            CountLoss("Нет ходов");
            int moves = _session.MovesMade;
            Tweens.Delay(this, "loss", LossCardDelay, () => ShowNoMoves(moves));
        }

        // Переворота песочных часов в интерфейсе пока нет, поэтому считаем только переливы.
        static bool HasPour(LevelState state)
        {
            var moves = new List<Move>();
            state.GetLegalMoves(moves);
            return moves.Exists(m => m.Kind == MoveKind.Pour);
        }

        void ShowNoMoves(int moves)
        {
            // Пока ждали, игрок мог отменить ход или взять бутылку — тогда карточка уже не нужна.
            if (!IsOpen || _finished || _session == null || _session.MovesMade != moves || HasPour(_session.State))
                return;
            // Карточка не всплывает посреди налива.
            if (IsAnimating)
            {
                Tweens.Delay(this, "loss", 0.3f, () => ShowNoMoves(moves));
                return;
            }
            var card = Router.Get<RewardPopup>();
            if (card == null)
            {
                toast.ShowKey("toast_no_moves", 2.5f);
                return;
            }

            var data = PlayerData.Current;
            if (_session.CanAddBottle && !_generated.Spec.BigFlask)
            {
                var bottle = new RewardBundle(0, bottles: 1);
                if (data.extraBottleCount > 0)
                {
                    card.SetupPlainChoice("deadend_title", "deadend_text", bottle, "deadend_bottle_ad", UseStoredBottle,
                        "lose_restart", RestartAfterLoss);
                    card.SetHighlightCaption("×" + Loc.Number(data.extraBottleCount));
                }
                else
                    card.SetupChoice("deadend_title", "deadend_text", bottle, "deadend_bottle_ad", WatchAdForBottle,
                        "lose_restart", RestartAfterLoss);
            }
            else if (data.undoCount > 0)
            {
                card.SetupPlainChoice("deadend_title", "deadend_text_restart", default, "deadend_undo", Undo,
                    "lose_restart", RestartAfterLoss);
                card.SetHighlightCaption("×" + Loc.Number(data.undoCount));
            }
            else
                card.SetupChoice("deadend_title", "deadend_text_restart", default, "deadend_undo", WatchAdForUndo,
                    "lose_restart", RestartAfterLoss);
            card.AllowDismiss();
            Router.ShowPopup(ScreenId.Reward);
        }

        // Бутылка из запаса — тот же бустер «+Бутылка», что на панели внизу.
        void UseStoredBottle()
        {
            if (IsOpen && _session != null)
                AddBottle();
        }

        // Бутылка за рекламу не тратит запас бустеров «+Бутылка».
        void WatchAdForBottle()
        {
            if (!AdLoadingHint.Check(toast))
            {
                int moves = _session.MovesMade;
                Tweens.Delay(this, "loss", 2f, () => ShowNoMoves(moves));
                return;
            }
            var session = _session;
            RewardedAds.Show("+бутылка без ходов", watched =>
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

        // Отмен в запасе нет: за ролик даём одну и сразу её тратим, так что запас не меняется.
        void WatchAdForUndo()
        {
            if (!AdLoadingHint.Check(toast))
            {
                int moves = _session.MovesMade;
                Tweens.Delay(this, "loss", 2f, () => ShowNoMoves(moves));
                return;
            }
            var session = _session;
            RewardedAds.Show("Отмена хода без ходов", watched =>
            {
                if (session != _session || !IsOpen)
                    return;
                if (!watched)
                {
                    toast.ShowKey("toast_reward_ad_failed", 2.2f);
                    return;
                }
                var data = PlayerData.Current;
                data.undoCount++;
                data.Save();
                _lossCounted = false;
                Undo();
                UiFeedback.Play(UiSound.Reward);
            });
        }
    }
}
