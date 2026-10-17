using System.Collections.Generic;
using System.Threading.Tasks;
using BottleSort.Core;
using BottleSort.Gameplay;
using UnityEngine;

namespace BottleSort.UI
{
    // Игровой экран. Ходы идут прямо в GameSession / TutorialGuide, ядро меняется сразу,
    // а картинка догоняет его через PourAnimator. Пока бутылка летит или в неё льют, её не трогаем;
    // остальные бутылки можно брать — правила это не ломает, потому что ход уже применён.
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
        bool _winScheduled;

        sealed class ActivePour
        {
            public Move Move;
            public MoveOutcome Outcome;
            public MoveChanges Changes;
            public BottleView Source;
            public BottleView Target;
        }

        // Что ход поменял у бутылок, кроме слоёв: события об этом уходят, когда налив закончился.
        sealed class MoveChanges
        {
            public readonly List<int> Unlocked = new List<int>();
            public readonly List<(int bottle, int layers)> Revealed = new List<(int bottle, int layers)>();
        }

        PourAnimator _pour;
        readonly List<ActivePour> _active = new List<ActivePour>();
        readonly HashSet<int> _pouring = new HashSet<int>();

        public override ScreenId Id => ScreenId.Game;
        public override Theme? BackdropTheme => LevelPlan.PlayerTheme ?? LevelPlan.ThemeOf(_level);
        public int Level => _level;
        public int Selected => _selected;
        public BottleField Field => field;

        // Шапка как у 24-го уровня из макета; само поле в редакторе рисует FieldPreview.
        public override void FillForEditor()
        {
            hud.SetLevel(24);
            hud.SetProgress(0, 6, false);
            hud.SetMoves(0, true);
            hud.SetStars(3);
        }
        public bool IsBusy => !_ready || _finished;
        public bool IsAnimating => _active.Count > 0;
        public GameSession Session => _session;

        void Awake()
        {
            hud.Home.onClick.AddListener(() => Router.Show(ScreenId.Menu));
            field.BottleTapped += OnBottleTapped;
            foreach (var button in boosters)
                button.Pressed += OnBooster;
            _pour = new GameObject("Pour Animator").AddComponent<PourAnimator>();
            _pour.StreamStarted += OnStreamStarted;
        }

        void OnDestroy()
        {
            if (_pour != null)
                Destroy(_pour.gameObject);
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
            SkipPours();
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
            SkipPours();
            _ready = false;
            _finished = false;
            _winScheduled = false;
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
            field.Build(_session.State);
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
                if (tutorialLevel)
                {
                    button.SetVisible(false);
                    continue;
                }
                if (!LevelPlan.BoosterUnlocked(button.Kind, _level))
                {
                    button.SetLocked(LevelPlan.BoosterUnlockLevel(button.Kind));
                    continue;
                }
                button.SetVisible(true);
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
            if (IsBusy || _pouring.Contains(index))
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
            var before = _session.State.Clone();
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
                GameEvents.RaiseInvalidMove(error);
                Deselect();
                if (_guide == null && (error == MoveError.TargetLocked || error == MoveError.TargetFrozen))
                    ShowBlockedToast(index, error);
                tutorial.OnMistake(error);
                return;
            }

            Deselect();
            AfterMove(move, outcome, CompareBottles(before));
        }

        MoveChanges CompareBottles(LevelState before)
        {
            var state = _session.State;
            var changes = new MoveChanges();
            for (int i = 0; i < before.BottleCount; i++)
            {
                if (before[i].FrozenTurns != state[i].FrozenTurns)
                    GameEvents.RaiseIceChanged(i, state[i].FrozenTurns);
                if (before[i].IsLocked && !state[i].IsLocked)
                    changes.Unlocked.Add(i);
                if (before[i].HiddenCount > state[i].HiddenCount)
                    changes.Revealed.Add((i, before[i].HiddenCount - state[i].HiddenCount));
            }
            return changes;
        }

        void AfterMove(Move move, MoveOutcome outcome, MoveChanges changes)
        {
            hud.SetMoves(_session.MovesMade, !_session.IsTutorial);
            hud.SetStars(_session.Stars);
            // Лёд тает с каждым ходом сразу, а замки и галочки — когда налив закончится.
            MeltIdleIce();
            tutorial.OnMove(move, outcome);

            if (_session.IsWon)
                _finished = true;
            else if (_session.IsOutOfMoves)
                toast.ShowKey("toast_out_of_moves", 2.5f);

            if (!StartPour(move, outcome, changes))
            {
                GameEvents.RaisePourStarted(move, outcome);
                FinishMove(move, outcome, changes);
            }
        }

        bool StartPour(Move move, MoveOutcome outcome, MoveChanges changes)
        {
            var source = WorldViewAt(move.From);
            var target = WorldViewAt(move.To);
            if (move.Kind != MoveKind.Pour || source == null || target == null)
                return false;

            var pour = new ActivePour { Move = move, Outcome = outcome, Changes = changes, Source = source, Target = target };
            _active.Add(pour);
            _pouring.Add(move.From);
            _pouring.Add(move.To);
            var state = _session.State;
            _pour.Play(source, target, state[move.From], state[move.To], () => OnPourFinished(pour));
            return true;
        }

        void OnStreamStarted(BottleView source, BottleView target)
        {
            foreach (var pour in _active)
            {
                if (pour.Source != source || pour.Target != target)
                    continue;
                GameEvents.RaisePourStarted(pour.Move, pour.Outcome);
                return;
            }
        }

        void OnPourFinished(ActivePour pour)
        {
            _active.Remove(pour);
            _pouring.Remove(pour.Move.From);
            _pouring.Remove(pour.Move.To);
            GameEvents.RaisePourFinished(pour.Move, pour.Outcome);
            FinishMove(pour.Move, pour.Outcome, pour.Changes);
        }

        void FinishMove(Move move, MoveOutcome outcome, MoveChanges changes)
        {
            var state = _session.State;
            for (int i = 0; i < field.Slots.Count && i < state.BottleCount; i++)
                if (!_pouring.Contains(i))
                    SyncSlot(i, true);
            RefreshHud();

            if (outcome.CompletedBottle)
            {
                int completed = state[move.To].IsCompleted(state.Unit) ? move.To : move.From;
                GameEvents.RaiseBottleCompleted(completed, outcome.Color);
                if (_guide == null)
                    toast.Show(Loc.Format("toast_color_done", Loc.Get("color_" + outcome.Color)));
            }
            foreach (int bottle in changes.Unlocked)
                GameEvents.RaiseBottleUnlocked(bottle);
            foreach (var (bottle, layers) in changes.Revealed)
                GameEvents.RaiseHiddenRevealed(bottle, layers);

            if (_guide == null)
            {
                if (outcome.UnlockedAny)
                    toast.ShowKey("toast_unlocked", 1.5f);
                else if (outcome.ThawedAny && !outcome.CompletedBottle)
                    toast.ShowKey("toast_thawed", 1.3f);
            }

            TryScheduleWin();
        }

        // Победа — только когда долита последняя бутылка.
        void TryScheduleWin()
        {
            if (!_finished || _winScheduled || _active.Count > 0 || !_session.IsWon)
                return;
            _winScheduled = true;
            Tweens.Delay(this, "win", WinDelay, ShowWin);
        }

        void SyncSlot(int index, bool animated)
        {
            var slot = field.SlotAt(index);
            var bottle = _session.State[index];
            bool completed = bottle.IsCompleted(_session.State.Unit);
            slot.View.Show(bottle, _session.State.Unit);
            slot.View.SetCompleted(completed, animated);
            slot.SetDone(completed, animated);
        }

        void MeltIdleIce()
        {
            var state = _session.State;
            for (int i = 0; i < field.Slots.Count && i < state.BottleCount; i++)
                if (!_pouring.Contains(i))
                    WorldViewAt(i)?.SetFrozen(state[i].FrozenTurns);
        }

        BottleView WorldViewAt(int index) => (field.SlotAt(index)?.View as BottleViewAdapter)?.View;

        // Отмена, «Заново», подсказка и +бутылка не ждут анимацию: доигрываем её мгновенно и рисуем поле по ядру.
        void SkipPours()
        {
            if (_active.Count == 0)
                return;
            _pour.SkipAll();
            _active.Clear();
            _pouring.Clear();
            if (_session != null)
                field.Refresh(_session.State, false);
        }

        void RejectSelection(int index, MoveError error)
        {
            // Пустая или уже собранная бутылка без выбора — просто ничего не происходит.
            if (error != MoveError.SourceLocked && error != MoveError.SourceFrozen)
                return;
            field.SlotAt(index).Shake();
            GameEvents.RaiseInvalidMove(error);
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

            SkipPours();
            Deselect();
            if (_guide != null) _guide.Undo(); else _session.Undo();
            data.undoCount--;
            data.Save();
            GameEvents.RaiseUndoUsed();
            // Бутылка от бустера переживает отмену, так что число бутылок не меняется.
            field.Refresh(_session.State, false);
            RefreshHud();
            RefreshBoosters();
            tutorial.OnUndoOrRestart();
        }

        void Restart()
        {
            SkipPours();
            Deselect();
            if (_guide != null) _guide.Restart(); else _session.Restart();
            GameEvents.RaiseRestartUsed();
            field.Build(_session.State);
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
            SkipPours();
            Deselect();
            Select(hint.Value.From);
            GameEvents.RaiseHintShown(hint.Value);
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
            GameEvents.RaiseExtraBottleAdded();
            SkipPours();
            Deselect();
            field.Build(_session.State);
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
            GameEvents.RaiseLevelWon(_level, stars);

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
