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

        /// <summary>The local player's network id, or -1 while offline. Used for spawn slots and colours.</summary>
        public static int LocalPlayerId { get; set; } = -1;

        /// <summary>
        /// Leaves the network session (set by the network layer). Called before the local player changes scene on their
        /// own (the lobby's tutorial hole): the returned task completes once the session is shut down.
        /// </summary>
        public static System.Func<System.Threading.Tasks.Task> LeaveSession { get; set; }

        public static int CurrentCheckpoint => Run != null ? Run.CurrentCheckpoint : 0;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Run = null;
            LocalPlayerId = -1;
            LeaveSession = null;
        }
    }
}
