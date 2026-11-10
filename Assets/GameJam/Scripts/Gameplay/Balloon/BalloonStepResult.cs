using UnityEngine;

namespace GameJam.Gameplay
{
    /// <summary>What happened to the balloon during one physics step.</summary>
    public struct BalloonStepResult
    {
        public const int NoHoop = -1;

        public Vector3 Position;
        public Vector3 Velocity;

        /// <summary>Set when the balloon popped; holds the MC's line.</summary>
        public string PopReason;

        /// <summary>The balloon grazed spikes and lost one step of air.</summary>
        public bool Leaked;

        /// <summary>It touched something that isn't a player's hit, which ends the rally.</summary>
        public bool RallyBroken;

        /// <summary>A hoop it is passing through this step, or <see cref="NoHoop"/>.</summary>
        public int HoopReached;

        public bool FinishReached;

        public bool Popped => PopReason != null;
    }
}
