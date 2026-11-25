using System;
using System.Collections.Generic;

namespace Plop.Core
{
    public enum MoveStatus : byte
    {
        /// <summary>At least one Plop moved; the move counter increased.</summary>
        Moved,
        /// <summary>Nothing could move; state and move count are unchanged.</summary>
        NoOp,
        /// <summary>The level is completed, failed, out of moves or not loaded; command ignored.</summary>
        Ignored
    }

    /// <summary>Result of one <see cref="GameEngine.TryMove"/> call.</summary>
    public readonly struct MoveResult
    {
        public readonly MoveStatus Status;
        public readonly Direction Direction;
        public readonly int MoveCount;
        public readonly bool CompletedLevel;
        /// <summary>Events produced by this command (moved, blocked, settled, completed). Never null.</summary>
        public readonly GameEvent[] Events;

        public MoveResult(MoveStatus status, Direction direction, int moveCount, bool completedLevel, GameEvent[] events)
        {
            Status = status;
            Direction = direction;
            MoveCount = moveCount;
            CompletedLevel = completedLevel;
            Events = events ?? Array.Empty<GameEvent>();
        }

        public bool Changed => Status == MoveStatus.Moved;
    }

    /// <summary>
    /// Stateful game session: one level, current state, move history, undo and restart.
    /// Each command is an atomic state transition implemented by <see cref="MoveRules"/>.
    /// Never depends on frame timing; presentation subscribes to <see cref="EventRaised"/>.
    /// </summary>
    public sealed class GameEngine
    {
        private readonly struct HistoryEntry
        {
            public readonly GameState Before;
            public readonly Direction Direction;
            public HistoryEntry(GameState before, Direction direction) { Before = before; Direction = direction; }
        }

        private readonly List<HistoryEntry> _history = new List<HistoryEntry>();
        private readonly List<GameEvent> _eventBuffer = new List<GameEvent>(16);
        private LevelDefinition _level;
        private GameState _initial;
        private GameState _state;
        private int[] _orderScratch = Array.Empty<int>();

        /// <summary>Raised for every gameplay event, after the state has been updated.</summary>
        public event Action<GameEvent> EventRaised;

        public LevelDefinition Level => _level;
        public bool HasLevel => _level != null;
        /// <summary>Number of state-changing moves made since load/restart (no-ops are not counted).</summary>
        public int MoveCount => _history.Count;
        public bool IsCompleted => _state != null && _state.AllAsleep;
        /// <summary>The attempt failed (a Plop entered Lava). Only Undo and Restart continue.</summary>
        public bool IsFailed => _state != null && _state.Failed;
        /// <summary>A Limited Moves challenge used its last move without completing.</summary>
        public bool IsOutOfMoves => _level != null && _level.MoveLimit > 0 && MoveCount >= _level.MoveLimit && !IsCompleted;
        /// <summary>Moves left in a Limited Moves challenge, or -1 without a limit.</summary>
        public int MovesRemaining => _level == null || _level.MoveLimit <= 0 ? -1 : Math.Max(0, _level.MoveLimit - MoveCount);
        /// <summary>True while a command could still change the board.</summary>
        public bool CanPlay => _state != null && !IsCompleted && !IsFailed && !IsOutOfMoves;
        public bool CanUndo => _history.Count > 0;

        /// <summary>Validates and loads a level. Throws <see cref="InvalidLevelException"/> if invalid.</summary>
        public void LoadLevel(LevelDefinition level)
        {
            if (level == null) throw new ArgumentNullException(nameof(level));
            var validation = LevelValidator.Validate(level);
            if (!validation.IsValid) throw new InvalidLevelException(validation);
            LoadInternal(level, level.CreateInitialState());
        }

        /// <summary>
        /// Loads a level starting from an arbitrary (validated) state, e.g. to compute hints from the
        /// player's current position. Restart returns to this state.
        /// </summary>
        public void LoadState(LevelDefinition level, GameState state)
        {
            if (level == null) throw new ArgumentNullException(nameof(level));
            if (state == null) throw new ArgumentNullException(nameof(state));
            var validation = LevelValidator.Validate(level);
            if (validation.IsValid) validation = LevelValidator.ValidateState(level, state);
            if (!validation.IsValid) throw new InvalidLevelException(validation);
            LoadInternal(level, state.Clone());
        }

        private void LoadInternal(LevelDefinition level, GameState start)
        {
            _level = level;
            _initial = start;
            _state = start.Clone();
            _history.Clear();
            if (_orderScratch.Length < start.PlopCount) _orderScratch = new int[start.PlopCount];
            Raise(new GameEvent(GameEventType.LevelLoaded, -1, default, default, default, BlockReason.None, -1, 0));
        }

        /// <summary>Returns a copy of the current state (safe to keep).</summary>
        public GameState GetState() => _state?.Clone();

        /// <summary>Read-only access to the live state without copying. Do not mutate.</summary>
        public GameState CurrentState => _state;

        public GameState InitialState => _initial?.Clone();

        public IReadOnlyList<Direction> GetMoveHistory()
        {
            var moves = new Direction[_history.Count];
            for (int i = 0; i < moves.Length; i++) moves[i] = _history[i].Direction;
            return moves;
        }

        /// <summary>Attempts one command. No-ops leave state, history and move count unchanged.</summary>
        public MoveResult TryMove(Direction direction)
        {
            if (!CanPlay)
                return new MoveResult(MoveStatus.Ignored, direction, MoveCount, false, null);

            var before = _state.Clone();
            _eventBuffer.Clear();
            bool changed = MoveRules.Apply(_level, _state, direction, _eventBuffer, _orderScratch);

            if (changed) _history.Add(new HistoryEntry(before, direction));
            int moveCount = MoveCount;
            bool completed = changed && _state.AllAsleep;
            bool failed = changed && _state.Failed;
            bool outOfMoves = changed && !completed && !failed && IsOutOfMoves;

            int extra = completed || failed || outOfMoves ? 1 : 0;
            var events = new GameEvent[_eventBuffer.Count + extra];
            for (int i = 0; i < _eventBuffer.Count; i++)
            {
                var e = _eventBuffer[i];
                events[i] = new GameEvent(e.Type, e.PlopId, e.From, e.To, e.Direction, e.BlockReason, e.BlockerId, moveCount);
            }
            if (completed)
                events[events.Length - 1] = new GameEvent(GameEventType.LevelCompleted, -1, default, default, direction, BlockReason.None, -1, moveCount);
            else if (failed)
                events[events.Length - 1] = new GameEvent(GameEventType.LevelFailed, _state.FailedPlop, default, default, direction, BlockReason.None, -1, moveCount);
            else if (outOfMoves)
                events[events.Length - 1] = new GameEvent(GameEventType.OutOfMoves, -1, default, default, direction, BlockReason.None, -1, moveCount);

            for (int i = 0; i < events.Length; i++) Raise(events[i]);
            return new MoveResult(changed ? MoveStatus.Moved : MoveStatus.NoOp, direction, moveCount, completed, events);
        }

        /// <summary>Restores the exact state before the last state-changing move.</summary>
        public bool Undo()
        {
            if (_history.Count == 0) return false;
            var last = _history[_history.Count - 1];
            _history.RemoveAt(_history.Count - 1);
            _state.CopyFrom(last.Before);
            Raise(new GameEvent(GameEventType.MoveUndone, -1, default, default, last.Direction, BlockReason.None, -1, MoveCount));
            return true;
        }

        /// <summary>Restores the level's starting state and clears history.</summary>
        public void Restart()
        {
            if (_initial == null) return;
            _state.CopyFrom(_initial);
            _history.Clear();
            Raise(new GameEvent(GameEventType.LevelRestarted, -1, default, default, default, BlockReason.None, -1, 0));
        }

        private void Raise(GameEvent e) => EventRaised?.Invoke(e);
    }

    public sealed class InvalidLevelException : Exception
    {
        public ValidationResult Validation { get; }
        public InvalidLevelException(ValidationResult validation)
            : base("Invalid level: " + string.Join("; ", validation.Errors)) => Validation = validation;
    }
}
