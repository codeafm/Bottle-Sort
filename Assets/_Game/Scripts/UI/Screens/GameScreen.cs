using System.Threading.Tasks;
using BottleSort.Core;
using UnityEngine;

namespace BottleSort.UI
{
    // Игровой экран. Пока у gameplay нет GameController, ходы идут прямо в GameSession / TutorialGuide.
    // Анимации переливания нет: слои обновляются сразу, полёт и наклон бутылки — следующий этап gameplay.
    public sealed class GameScreen : UiScreen
    {
        const float WinDelay = 0.8f;

        [SerializeField] GameHud hud;
        [SerializeField] BottleField field;
        [SerializeField] BoosterButton[] boosters;
        [SerializeField] TutorialDirector tutorial;
        [SerializeField] Toast toast;

        int _level = 1;
        int _loadVersion;
        GeneratedLevel _generated;
        GameSession _session;
        TutorialGuide _guide;
        int _selected = -1;
        int _hintTarget = -1;
        bool _ready;
        bool _hintPending;
        bool _finished;

        public override ScreenId Id => ScreenId.Game;
        public override Theme? BackdropTheme => LevelPlan.PlayerTheme ?? LevelPlan.ThemeOf(_level);
        public int Level => _level;
        public int Selected => _selected;
        public BottleField Field => field;
        public bool IsBusy => !_ready || _finished;

        void Awake()
        {
            hud.Home.onClick.AddListener(() => Router.Show(ScreenId.Menu));
            field.BottleTapped += OnBottleTapped;
            foreach (var button in boosters)
                button.Pressed += OnBooster;
        }

        public void Prepare(int level)
        {
            _level = Mathf.Max(1, level);
        }

        protected override void OnOpen()
        {
            StartLevel(_level);
        }

        protected override void OnClose()
        {
            tutorial.Stop();
            toast.HideNow();
        }

        public override bool OnBack()
        {
            Router.Show(ScreenId.Menu);
            return true;
        }

        public void SetInfo(string text) => hud.SetInfo(text);

        async void StartLevel(int level)
        {
            int version = ++_loadVersion;
            _ready = false;
            _finished = false;
            _selected = -1;
            _hintTarget = -1;
            hud.SetLevel(level);
            hud.SetInfo(Loc.Get("hud_loading"), false);
            RefreshBoosters();

            GeneratedLevel generated;
            try
            {
                generated = await LevelLoader.Get(level);
            }
            catch (System.Exception e)
            {
                Debug.LogException(e);
                toast.ShowKey("toast_level_failed", 3f);
                return;
            }

            if (version != _loadVersion || !IsOpen)
                return;
            Begin(generated);
            LevelLoader.Preload(level + 1);
        }

        void Begin(GeneratedLevel generated)
        {
            _generated = generated;
            _session = generated.CreateSession();
            _guide = generated.IsTutorial ? HandmadeLevels.CreateGuide(generated, _session) : null;
            field.Build(_session.State, generated.IsTutorial);
            tutorial.SetHandScale(field.Scale / 2.5f);
            _ready = true;

            hud.SetInfo(DefaultInfo(), false);
            RefreshHud(false);
            RefreshBoosters();
            if (_guide != null)
                tutorial.Begin(this, _level, _guide);
            else
                tutorial.Stop();
        }

        string DefaultInfo()
        {
            switch (LevelPlan.TypeOf(_level))
            {
                case LevelType.Special: return Loc.Get("info_special");
                case LevelType.BigFlask: return Loc.Format("info_flask", _session.State.Unit * 2);
                case LevelType.Rescue: return Loc.Get("info_rescue");
                case LevelType.Boss:
                case LevelType.ChapterBoss: return Loc.Get("info_boss");
            }
            foreach (var bottle in _session.State.Bottles)
                if (bottle.HiddenCount > 0)
                    return Loc.Get("info_hidden");
            return Loc.Get("info_normal");
        }

        void RefreshHud(bool animated = true)
        {
            var state = _session.State;
            hud.SetProgress(state.CompletedCount, state.CountColors(), animated);
            hud.SetMoves(_session.MovesMade, !_session.IsTutorial);
            hud.SetStars(_session.Stars);
        }

        void RefreshBoosters()
        {
            var data = PlayerData.Current;
            bool tutorialLevel = HandmadeLevels.IsTutorial(_level);
            foreach (var button in boosters)
            {
                button.SetVisible(!tutorialLevel && LevelPlan.BoosterUnlocked(button.Kind, _level));
                switch (button.Kind)
                {
                    case Booster.Undo: button.SetCount(data.undoCount); break;
                    case Booster.Hint: button.SetCount(data.hintCount); break;
                    case Booster.ExtraBottle: button.SetCount(data.extraBottleCount); break;
                    default: button.SetCount(-1); break;
                }
            }
        }

        void OnBottleTapped(int index)
        {
            if (IsBusy)
                return;
            tutorial.OnTap();
            var state = _session.State;

            if (_selected < 0)
            {
                var selectError = state.CheckSelection(index);
                if (selectError != MoveError.None)
                {
                    RejectSelection(index, selectError);
                    return;
                }
                if (!tutorial.CanSelect(index))
                {
                    tutorial.OnIgnoredTap();
                    return;
                }
                Select(index);
                return;
            }

            if (index == _selected)
            {
                Deselect();
                UiFeedback.Play(UiSound.Deselect);
                return;
            }

            var move = Move.Pour(_selected, index);
            MoveOutcome outcome;
            var error = _guide != null ? _guide.TryMove(move, out outcome) : _session.TryMove(move, out outcome);
            if (error == MoveError.NotRecommended)
            {
                tutorial.OnIgnoredTap();
                return;
            }
            if (error != MoveError.None)
            {
                field.SlotAt(index).Shake();
                UiFeedback.Play(UiSound.Wrong);
                Deselect();
                if (_guide == null && (error == MoveError.TargetLocked || error == MoveError.TargetFrozen))
                    ShowBlockedToast(index, error);
                tutorial.OnMistake(error);
                return;
            }

            Deselect();
            AfterMove(move, outcome);
        }

        void AfterMove(Move move, MoveOutcome outcome)
        {
            field.Refresh(_session.State);
            RefreshHud();
            UiFeedback.Play(UiSound.Pour);

            if (outcome.CompletedBottle)
            {
                UiFeedback.Play(UiSound.BottleComplete);
                if (_guide == null)
                    toast.Show(Loc.Format("toast_color_done", Loc.Get("color_" + outcome.Color)));
            }
            if (_guide == null)
            {
                if (outcome.UnlockedAny)
                    toast.ShowKey("toast_unlocked", 1.5f);
                else if (outcome.ThawedAny && !outcome.CompletedBottle)
                    toast.ShowKey("toast_thawed", 1.3f);
            }

            tutorial.OnMove(move, outcome);

            if (_session.IsWon)
            {
                _finished = true;
                Tweens.Delay(this, "win", WinDelay, ShowWin);
            }
            else if (_session.IsOutOfMoves)
            {
                toast.ShowKey("toast_out_of_moves", 2.5f);
            }
        }

        void RejectSelection(int index, MoveError error)
        {
            // Пустая или уже собранная бутылка без выбора — просто ничего не происходит.
            if (error != MoveError.SourceLocked && error != MoveError.SourceFrozen)
                return;
            field.SlotAt(index).Shake();
            UiFeedback.Play(UiSound.Wrong);
            if (_guide == null)
                ShowBlockedToast(index, error);
            else
                _guide.ReportMistake(error);
        }

        void ShowBlockedToast(int index, MoveError error)
        {
            var bottle = _session.State[index];
            if (error == MoveError.SourceLocked || error == MoveError.TargetLocked)
                toast.ShowKey("toast_locked");
            else
                toast.Show(Loc.Format("toast_frozen", bottle.FrozenTurns));
        }

        void Select(int index)
        {
            _selected = index;
            var slot = field.SlotAt(index);
            slot.SetLifted(true);
            slot.View.SetGlow(BottleGlow.Selected);
            UiFeedback.Play(UiSound.Select);
            tutorial.OnSelectionChanged();
        }

        void Deselect()
        {
            if (_selected >= 0)
            {
                var slot = field.SlotAt(_selected);
                slot.SetLifted(false);
                slot.View.SetGlow(BottleGlow.None);
            }
            if (_hintTarget >= 0)
                field.SlotAt(_hintTarget)?.View.SetGlow(BottleGlow.None);
            _selected = -1;
            _hintTarget = -1;
            tutorial.OnSelectionChanged();
        }

        void OnBooster(Booster booster)
        {
            if (IsBusy || _hintPending)
                return;
            switch (booster)
            {
                case Booster.Undo: Undo(); break;
                case Booster.Restart: Restart(); break;
                case Booster.Hint: Hint(); break;
                case Booster.ExtraBottle: AddBottle(); break;
            }
        }

        void Undo()
        {
            if (!_session.CanUndo)
            {
                toast.ShowKey("toast_nothing_to_undo", 1.2f);
                return;
            }
            var data = PlayerData.Current;
            if (data.undoCount <= 0)
            {
                OfferForAd(Booster.Undo);
                return;
            }

            Deselect();
            if (_guide != null) _guide.Undo(); else _session.Undo();
            data.undoCount--;
            data.Save();
            // Бутылка от бустера переживает отмену, так что число бутылок не меняется.
            field.Refresh(_session.State, false);
            RefreshHud();
            RefreshBoosters();
            tutorial.OnUndoOrRestart();
        }

        void Restart()
        {
            Deselect();
            if (_guide != null) _guide.Restart(); else _session.Restart();
            field.Build(_session.State, _generated.IsTutorial);
            hud.SetInfo(DefaultInfo());
            RefreshHud();
            tutorial.OnUndoOrRestart();
        }

        async void Hint()
        {
            var data = PlayerData.Current;
            if (data.hintCount <= 0)
            {
                OfferForAd(Booster.Hint);
                return;
            }

            _hintPending = true;
            var snapshot = _session.State.Clone();
            var session = _session;
            Move? hint = await Task.Run(() => Solver.Hint(snapshot, SolverOptions.Quick(60_000)));
            _hintPending = false;
            if (session != _session || IsBusy)
                return;
            if (!hint.HasValue)
            {
                toast.ShowKey("toast_no_moves", 1.5f);
                return;
            }

            data.hintCount--;
            data.Save();
            RefreshBoosters();
            Deselect();
            Select(hint.Value.From);
            _hintTarget = hint.Value.To;
            field.SlotAt(_hintTarget).View.SetGlow(BottleGlow.Hint);
        }

        void AddBottle()
        {
            if (_generated.Spec.BigFlask)
            {
                toast.ShowKey("toast_flask_helps", 1.3f);
                return;
            }
            if (_session.ExtraBottleUsed)
            {
                toast.ShowKey("toast_bottle_already", 1.2f);
                return;
            }
            var data = PlayerData.Current;
            if (data.extraBottleCount <= 0)
            {
                OfferForAd(Booster.ExtraBottle);
                return;
            }
            if (!_session.AddBottle())
                return;
            data.extraBottleCount--;
            data.Save();
            Deselect();
            field.Build(_session.State, _generated.IsTutorial);
            RefreshHud();
            RefreshBoosters();
        }

        // Как в макете: когда бустеры кончились, бейдж «AD» — смотришь рекламу и получаешь один.
        void OfferForAd(Booster booster)
        {
            RewardedAds.Show(watched =>
            {
                if (!watched)
                    return;
                var data = PlayerData.Current;
                switch (booster)
                {
                    case Booster.Undo: data.undoCount++; toast.ShowKey("toast_ad_undo", 1.5f); break;
                    case Booster.Hint: data.hintCount++; toast.ShowKey("toast_ad_hint", 1.5f); break;
                    default: data.extraBottleCount++; toast.ShowKey("toast_ad_bottle", 1.5f); break;
                }
                data.Save();
                RefreshBoosters();
            });
        }

        void ShowWin()
        {
            var data = PlayerData.Current;
            int stars = _session.Stars;
            int previousStars = data.StarsOf(_level);
            int reward = LevelPlan.Reward(_level, stars);
            // Переигровка доплачивает только разницу за новые звёзды.
            if (previousStars > 0)
                reward = stars > previousStars ? reward - LevelPlan.Reward(_level, previousStars) : 0;

            data.RecordWin(_level, stars);
            data.AddCoins(reward);
            UiFeedback.Play(UiSound.Win);

            var result = new WinResult
            {
                Level = _level,
                Stars = stars,
                Moves = _session.MovesMade,
                Reward = reward,
                IsTutorial = _session.IsTutorial,
                IsBest = stars > previousStars && previousStars > 0
            };
            Router.Get<WinScreen>().Setup(result);
            Router.Show(ScreenId.Win);
        }

        public static string ErrorKey(MoveError error)
        {
            switch (error)
            {
                case MoveError.TargetFull: return "err_full";
                case MoveError.ColorMismatch: return "err_color";
                case MoveError.TargetRejectsColor: return "err_tint";
                case MoveError.TargetLocked:
                case MoveError.SourceLocked: return "err_locked";
                case MoveError.TargetFrozen:
                case MoveError.SourceFrozen: return "err_frozen";
                default: return null;
            }
        }
    }
}
