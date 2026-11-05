using UnityEngine;

namespace GameJam.Gameplay
{
    /// <summary>What happened to the balloon during one physics step.</summary>
    public struct BalloonStepResult
    {
        public const int NoGate = -1;

        public Vector3 Position;
        public Vector3 Velocity;

        /// <summary>Set when the balloon popped; holds the commentator line.</summary>
        public string PopReason;

        /// <summary>The balloon grazed spikes and lost one step of air.</summary>
        public bool Leaked;

        /// <summary>It touched something that isn't a player's hit, which ends the rally.</summary>
        public bool RallyBroken;

        /// <summary>Highest section gate it passed through this step, or <see cref="NoGate"/>.</summary>
        public int GateReached;

        public bool FinishReached;

        public bool Popped => PopReason != null;
    }
}
