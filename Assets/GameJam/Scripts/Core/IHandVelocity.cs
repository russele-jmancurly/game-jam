using UnityEngine;

namespace GameJam.Core
{
    /// <summary>A tracked hand's smoothed world velocity.</summary>
    public interface IHandVelocity
    {
        Vector3 Velocity { get; }
    }
}
