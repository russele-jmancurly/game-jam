namespace GameJam.Core
{
    /// <summary>Read-only view of the networked run, for local code (wrist timer, respawns, hoops, start button).</summary>
    public interface IRunStatus
    {
        RunState State { get; }

        /// <summary>Where the balloon and fallen players come back: 0 = start line, k = beside the last hoop cleared (hoop k-1).</summary>
        int CurrentCheckpoint { get; }

        int HoopsCleared { get; }

        /// <summary>How many hoops this yard has.</summary>
        int HoopCount { get; }

        float TimeLeft { get; }

        /// <summary>Short text for the wrist display.</summary>
        string WristText { get; }

        bool IsHoopCleared(int hoopIndex);

        /// <summary>Asks the run owner to start a new run. Ignored while one is running.</summary>
        void RequestStart();
    }
}
