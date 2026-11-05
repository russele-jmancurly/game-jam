using Fusion;
using GameJam.Core;

namespace GameJam.Net
{
    /// <summary>Feeds the network's simulation time to <see cref="CourseClock"/> so moving hazards agree.</summary>
    public sealed class FusionTimeSource : ISharedTimeSource
    {
        private readonly NetworkRunner _runner;

        public FusionTimeSource(NetworkRunner runner) => _runner = runner;

        public bool IsRunning => _runner != null && _runner.IsRunning;

        public float SharedTime => _runner.SimulationTime;
    }
}
