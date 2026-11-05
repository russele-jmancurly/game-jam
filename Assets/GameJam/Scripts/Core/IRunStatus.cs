namespace GameJam.Core
{
    /// <summary>Read-only view of the networked run, for local code (wrist timer, respawns, start button).</summary>
    public interface IRunStatus
    {
        RunState State { get; }

        int CurrentSection { get; }

        float TimeLeft { get; }

        /// <summary>Short text for the wrist display.</summary>
        string WristText { get; }

        /// <summary>Asks the run owner to start a new run. Ignored while one is running.</summary>
        void RequestStart();
    }
}
