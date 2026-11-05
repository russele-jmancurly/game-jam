namespace GameJam.Core
{
    /// <summary>A clock every player agrees on, provided by the network layer once a session runs.</summary>
    public interface ISharedTimeSource
    {
        bool IsRunning { get; }

        /// <summary>Seconds since the session started, the same on every client (give or take latency).</summary>
        float SharedTime { get; }
    }
}
