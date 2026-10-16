using System.Collections.Generic;

namespace BottleSort.Core
{
    // Данные для руки-подсказки на обучающих уровнях: какой ход показать и была ли ошибка.
    // Тексты и ветки сценария — у ui, здесь только то, что следует из правил.
    public sealed class TutorialGuide
    {
        private readonly GameSession session;
        private readonly IReadOnlyList<Move> script;
        private readonly List<Move> played = new List<Move>();

        // На первом уровне ведём строго: любой ход, кроме показанного, не выполняется.
        public bool Strict { get; }
        public bool OnScript { get; private set; } = true;
        public Move? Recommended { get; private set; }
        public MoveError LastError { get; private set; }
        public bool LastMoveFollowedHint { get; private set; }
        public int Mistakes { get; private set; }
        public int MistakesInRow { get; private set; }

        public TutorialGuide(GameSession session, IReadOnlyList<Move> script, bool strict)
        {
            this.session = session;
            this.script = script ?? new List<Move>();
            Strict = strict;
            Refresh();
        }

        public GameSession Session => session;
        public bool LastMoveWasMistake => LastError != MoveError.None;

        public MoveError TryMove(Move move, out MoveOutcome outcome)
        {
            outcome = default;
            var error = Strict && Recommended.HasValue && move != Recommended.Value
                ? MoveError.NotRecommended
                : session.TryMove(move, out outcome);

            LastError = error;
            if (error != MoveError.None)
            {
                Mistakes++;
                MistakesInRow++;
                return error;
            }

            LastMoveFollowedHint = Recommended.HasValue && move == Recommended.Value;
            MistakesInRow = 0;
            played.Add(move);
            Refresh();
            return MoveError.None;
        }

        // Ошибку можно отметить и без хода: например, тап по бутылке, которую нельзя взять.
        public void ReportMistake(MoveError error)
        {
            LastError = error;
            Mistakes++;
            MistakesInRow++;
        }

        public bool Undo()
        {
            if (!session.Undo())
                return false;
            played.RemoveAt(played.Count - 1);
            LastError = MoveError.None;
            Refresh();
            return true;
        }

        public void Restart()
        {
            session.Restart();
            played.Clear();
            LastError = MoveError.None;
            MistakesInRow = 0;
            Refresh();
        }

        // Пока игрок идёт по сценарию, показываем ход сценария, потом — лучший ход солвера из текущей позиции.
        private void Refresh()
        {
            OnScript = played.Count <= script.Count;
            for (int i = 0; OnScript && i < played.Count; i++)
                OnScript = played[i] == script[i];

            if (session.IsWon)
                Recommended = null;
            else if (OnScript && played.Count < script.Count)
                Recommended = script[played.Count];
            else
                Recommended = Solver.Hint(session.State);
        }
    }
}
