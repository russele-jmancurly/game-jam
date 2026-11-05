using UnityEngine;

namespace GameJam.Core
{
    /// <summary>
    /// Tiny service locator that lets local code (rig, hands, course) reach networked systems without
    /// referencing the network assembly. The network layer fills these in as objects spawn.
    /// </summary>
    public static class GameServices
    {
        /// <summary>The current run, or null before the game state has spawned.</summary>
        public static IRunStatus Run { get; set; }

        /// <summary>The local player's networked avatar, or null while offline.</summary>
        public static IKnockbackRelay LocalKnockback { get; set; }

        /// <summary>The local player's avatar as a character switcher, or null while offline.</summary>
        public static ICharacterSelector LocalCharacter { get; set; }

        /// <summary>The local player's network id, or -1 while offline. Used for spawn slots and colours.</summary>
        public static int LocalPlayerId { get; set; } = -1;

        public static int CurrentCheckpoint => Run != null ? Run.CurrentCheckpoint : 0;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Run = null;
            LocalKnockback = null;
            LocalCharacter = null;
            LocalPlayerId = -1;
        }
    }
}
