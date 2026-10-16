using BottleSort.Core;
using UnityEngine;

namespace BottleSort.UI
{
    // Сценарии обучающих уровней 1–3 (LEVEL_DESIGN.md §3.4): куда показывает рука и что написано в строке подсказки.
    // Правила и выбор хода берём из TutorialGuide, здесь только подача.
    public sealed class TutorialDirector : MonoBehaviour
    {
        const float FirstDelay = 0.5f;
        const float NextStepDelay = 0.3f;
        const float IdleHint = 8f;
        const float ErrorTextTime = 2.2f;

        enum Stage
        {
            Guided,
            // L3: после ошибки рука показывает 1 → 5.
            AfterError,
            // L3: красный убран, ждём 3 с и показываем 2 → 1.
            RedCleared,
            Free
        }

        [SerializeField] TutorialHand hand;

        GameScreen _game;
        TutorialGuide _guide;
        int _level;
        Stage _stage;
        Move? _handMove;
        float _handDelay;
        float _idle;
        float _textHold;
        string _stepText;
        bool _urgent;

        public bool Active => _guide != null;

        public void Begin(GameScreen game, int level, TutorialGuide guide)
        {
            _game = game;
            _guide = guide;
            _level = level;
            _stage = Stage.Guided;
            _idle = 0f;
            _textHold = 0f;
            _urgent = false;
            hand.Hide();
            _handMove = null;

            switch (level)
            {
                case 1:
                case 2:
                    ShowStep(FirstDelay);
                    break;
                default:
                    SetStepText("tut3_start");
                    break;
            }
        }

        public void Stop()
        {
            _guide = null;
            _handMove = null;
            hand.Hide();
        }

        public void SetHandScale(float scale) => hand.SetScale(scale);

        // На первом уровне взять можно только бутылку, на которую показывает рука.
        public bool CanSelect(int index)
        {
            if (!Active || !_guide.Strict || !_guide.Recommended.HasValue)
                return true;
            return _guide.Recommended.Value.From == index;
        }

        public void OnTap() => _idle = 0f;

        public void OnSelectionChanged()
        {
            if (!Active)
                return;
            if (_level == 1 && _guide.Session.MovesMade == 0 && _textHold <= 0f)
                SetStepText(_game.Selected >= 0 ? "tut1_pick_empty" : "tut1_take");
            UpdateDimming();
        }

        public void OnIgnoredTap()
        {
            if (!Active)
                return;
            _guide.ReportMistake(MoveError.NotRecommended);
            _urgent = true;
            hand.SetUrgent(true);
            if (_guide.MistakesInRow >= 3)
                SetStepText("tut1_follow_finger");
        }

        public void OnMistake(MoveError error)
        {
            if (!Active)
                return;

            if (_level == 3 && (_stage == Stage.Guided || _stage == Stage.AfterError))
            {
                _stage = Stage.AfterError;
                SetStepText("tut3_error");
                var clearRed = Move.Pour(0, 4);
                _handMove = _guide.Session.State.CanApply(clearRed) ? clearRed : _guide.Recommended;
                _handDelay = 0.45f;
                return;
            }

            string reason = GameScreen.ErrorKey(error);
            if (reason != null)
                ShowTemporary(reason);
            if (_handMove.HasValue)
                _handDelay = 0.45f;
        }

        public void OnMove(Move move, MoveOutcome outcome)
        {
            if (!Active)
                return;
            _urgent = false;
            hand.SetUrgent(false);
            hand.Hide();
            _handMove = null;

            if (outcome.Won)
            {
                Stop();
                return;
            }

            switch (_level)
            {
                case 1: AfterMoveLevel1(outcome); break;
                case 2: AfterMoveLevel2(); break;
                default: AfterMoveLevel3(move, outcome); break;
            }
            UpdateDimming();
        }

        public void OnUndoOrRestart()
        {
            if (!Active)
                return;
            if (_guide.Session.MovesMade == 0)
            {
                Begin(_game, _level, _guide);
                return;
            }
            hand.Hide();
            _handMove = null;
            if (_stage == Stage.Guided && _level != 3)
                ShowStep(NextStepDelay);
        }

        void AfterMoveLevel1(MoveOutcome outcome)
        {
            if (outcome.CompletedBottle)
            {
                ShowTemporary("tut1_red_done", 1f);
                ShowStep(1f + NextStepDelay);
                return;
            }
            ShowStep(NextStepDelay);
        }

        void AfterMoveLevel2()
        {
            if (_stage == Stage.Free)
                return;
            if (!_guide.LastMoveFollowedHint)
            {
                // Свой допустимый ход — значит, понял. Дальше без руки.
                _stage = Stage.Free;
                SetStepText("tut2_own");
                return;
            }
            if (_guide.Session.MovesMade >= 3)
            {
                _stage = Stage.Free;
                SetStepText("tut2_free");
                return;
            }
            ShowStep(NextStepDelay);
        }

        void AfterMoveLevel3(Move move, MoveOutcome outcome)
        {
            if (outcome.CompletedBottle)
            {
                _stage = Stage.Free;
                SetStepText(outcome.Color == Palette.Yellow ? "tut3_yellow_done" : "tut3_color_done");
                return;
            }

            bool clearedRed = move.From == 0 && outcome.Color == Palette.Red && (move.To == 4 || move.To == 5);
            if (_stage != Stage.Free && clearedRed)
            {
                _stage = Stage.RedCleared;
                SetStepText("tut3_good");
                _idle = 0f;
            }
        }

        void ShowStep(float delay)
        {
            var next = _guide.Recommended;
            _handMove = next;
            _handDelay = delay;
            if (_level == 1)
            {
                int moves = _guide.Session.MovesMade;
                SetStepText(moves == 0 ? (_game.Selected >= 0 ? "tut1_pick_empty" : "tut1_take")
                    : moves == 1 ? "tut1_step2" : "tut1_step3");
            }
            else if (_level == 2)
            {
                int moves = _guide.Session.MovesMade;
                SetStepText(moves == 0 ? "tut2_step1" : moves == 1 ? "tut2_step2" : "tut2_step3");
            }
            UpdateDimming();
        }

        void SetStepText(string key)
        {
            _stepText = key;
            if (_textHold <= 0f)
                _game.SetInfo(Loc.Get(key));
        }

        void ShowTemporary(string key, float seconds = ErrorTextTime)
        {
            _textHold = seconds;
            _game.SetInfo(Loc.Get(key));
        }

        void UpdateDimming()
        {
            var slots = _game.Field.Slots;
            bool dim = _level == 1 && _guide != null && _guide.Recommended.HasValue;
            for (int i = 0; i < slots.Count; i++)
            {
                bool focus = !dim || i == _guide.Recommended.Value.From || i == _guide.Recommended.Value.To;
                slots[i].View.SetDimmed(!focus);
            }
        }

        void Update()
        {
            if (!Active)
                return;
            float dt = Time.unscaledDeltaTime;

            if (_textHold > 0f)
            {
                _textHold -= dt;
                if (_textHold <= 0f && _stepText != null)
                    _game.SetInfo(Loc.Get(_stepText));
            }

            _idle += dt;
            if (_stage == Stage.RedCleared && !_handMove.HasValue && _idle >= 3f)
            {
                var yellow = Move.Pour(1, 0);
                _handMove = _guide.Session.State.CanApply(yellow) ? yellow : _guide.Recommended;
                _handDelay = 0f;
            }
            else if ((_stage == Stage.Free || (_level == 3 && _stage == Stage.Guided)) && !_handMove.HasValue && _idle >= IdleHint)
            {
                _handMove = _guide.Recommended;
                _handDelay = 0f;
            }

            UpdateHand(dt);
        }

        void UpdateHand(float dt)
        {
            if (!_handMove.HasValue || _game.IsBusy)
            {
                hand.Hide();
                return;
            }
            if (_handDelay > 0f)
            {
                _handDelay -= dt;
                return;
            }

            var move = _handMove.Value;
            int selected = _game.Selected;
            int target;
            if (selected < 0)
                target = move.From;
            else if (selected == move.From)
                target = move.To;
            else
                target = -1;

            var slot = _game.Field.SlotAt(target);
            if (slot == null)
            {
                hand.Hide();
                return;
            }
            hand.Point(slot.HandAnchor, _urgent);
        }
    }
}
