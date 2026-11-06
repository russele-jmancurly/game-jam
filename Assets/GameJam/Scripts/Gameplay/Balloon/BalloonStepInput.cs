using GameJam.Core;
using UnityEngine;

namespace GameJam.Gameplay
{
    /// <summary>The balloon's state going into one physics step.</summary>
    public struct BalloonStepInput
    {
        public Vector3 Position;
        public Vector3 Velocity;
        public BalloonCondition Condition;

        /// <summary>False right after a hit, so the hitter's own spikes don't count.</summary>
        public bool AreSpikesArmed;

        /// <summary>False while the leak cooldown runs.</summary>
        public bool CanLeak;

        public float DeltaTime;

        /// <summary>Seconds since start, for the air flutter (any steadily increasing clock).</summary>
        public float Time;
    }
}
