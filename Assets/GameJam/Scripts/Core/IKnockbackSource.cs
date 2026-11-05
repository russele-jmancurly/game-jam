using UnityEngine;

namespace GameJam.Core
{
    /// <summary>Something that bowls players over when it moves into them (moving obstacles, the roaming character).</summary>
    public interface IKnockbackSource
    {
        bool KnocksPlayers { get; }

        /// <summary>How fast its surface moves at a point; the knockback goes along it.</summary>
        Vector3 PointVelocity(Vector3 worldPoint);
    }
}
