namespace Plop.Core
{
    public enum GameEventType : byte
    {
        /// <summary>A Plop moved one cell (From -> To).</summary>
        PlopMoved,
        /// <summary>An awake Plop could not move (see <see cref="GameEvent.BlockReason"/>).</summary>
        PlopBlocked,
        /// <summary>A Plop reached a matching Nest and fell asleep at To.</summary>
        PlopSettled,
        /// <summary>The last awake Plop fell asleep.</summary>
        LevelCompleted,
        /// <summary>Undo restored the previous state; presentation should resync from the engine state.</summary>
        MoveUndone,
        /// <summary>Restart restored the initial state; presentation should resync from the engine state.</summary>
        LevelRestarted,
        /// <summary>A new level (or arbitrary state) was loaded; presentation should rebuild.</summary>
        LevelLoaded,
        /// <summary>Frost slide: a Plop slid in a straight line From -> To (one or more cells).</summary>
        PlopSlid,
        /// <summary>Breeze: a fan pushed a Plop From -> To in the fan's direction (one or more cells).</summary>
        PlopPushed,
        /// <summary>Loop: a Plop teleported from one portal (From) to its pair (To).</summary>
        PlopTeleported,
        /// <summary>A Plop entered Lava at To. The attempt fails (see <see cref="LevelFailed"/>).</summary>
        PlopBurned,
        /// <summary>The attempt failed (Lava). Undo or Restart recover.</summary>
        LevelFailed,
        /// <summary>A Limited Moves challenge ran out of moves without completing.</summary>
        OutOfMoves
    }

    public enum BlockReason : byte
    {
        None,
        Edge,
        Wall,
        Plop,
        /// <summary>A Loop's exit was occupied, so the teleport was cancelled.</summary>
        PortalExit
    }

    /// <summary>
    /// Allocation-free gameplay event for presentation layers. Gameplay never touches visuals;
    /// views subscribe to these events instead.
    /// </summary>
    public readonly struct GameEvent
    {
        public readonly GameEventType Type;
        public readonly int PlopId;
        public readonly GridPosition From;
        public readonly GridPosition To;
        public readonly Direction Direction;
        public readonly BlockReason BlockReason;
        /// <summary>Id of the blocking Plop when <see cref="BlockReason"/> is Plop, else -1.</summary>
        public readonly int BlockerId;
        /// <summary>Move count after the event's transition.</summary>
        public readonly int MoveCount;

        public GameEvent(GameEventType type, int plopId, GridPosition from, GridPosition to, Direction direction,
            BlockReason blockReason, int blockerId, int moveCount)
        {
            Type = type;
            PlopId = plopId;
            From = from;
            To = to;
            Direction = direction;
            BlockReason = blockReason;
            BlockerId = blockerId;
            MoveCount = moveCount;
        }

        public override string ToString()
        {
            switch (Type)
            {
                case GameEventType.PlopMoved: return "Moved p" + PlopId + " " + From + "->" + To;
                case GameEventType.PlopBlocked: return "Blocked p" + PlopId + " at " + From + " by " + BlockReason + (BlockerId >= 0 ? " p" + BlockerId : "");
                case GameEventType.PlopSettled: return "Settled p" + PlopId + " at " + To;
                case GameEventType.PlopSlid: return "Slid p" + PlopId + " " + From + "->" + To + " " + Direction;
                case GameEventType.PlopPushed: return "Pushed p" + PlopId + " " + From + "->" + To + " " + Direction;
                case GameEventType.PlopTeleported: return "Teleported p" + PlopId + " " + From + "->" + To;
                case GameEventType.PlopBurned: return "Burned p" + PlopId + " at " + To;
                default: return Type + " moves=" + MoveCount;
            }
        }
    }
}
