namespace GameJam.Core
{
    /// <summary>Phases of one run. Lobby is free play at the start line.</summary>
    public enum RunState : byte
    {
        Lobby,
        Countdown,
        Running,
        Finished,
    }
}
