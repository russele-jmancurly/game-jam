using GameJam.Core;
using UnityEngine;

namespace GameJam.Gameplay
{
    /// <summary>
    /// Works out the hit that sends the balloon through a point, using the balloon's own flight model
    /// (<see cref="BalloonPhysics"/>: constant gravity g plus linear drag k). With linear drag the flight has a closed
    /// form: after time T the balloon has moved v0 * f + (g / k) * (f - T) * down, where f = (1 - e^-kT) / k. Solving for
    /// v0 gives the launch velocity that arrives at the target after T seconds.
    /// </summary>
    public static class HoopAim
    {
        /// <summary>
        /// The velocity to pass to a balloon hit so the balloon passes through <paramref name="target"/>: the result already
        /// leaves room for the hit's built-in upward assist, and is capped to the hit speed limit.
        /// </summary>
        public static Vector3 HitVelocityThrough(Vector3 from, Vector3 target, BalloonConfig config)
        {
            Vector3 delta = target - from;
            float k = Mathf.Max(0.01f, config.Drag);
            float g = config.Gravity;

            // Further hoops get a longer, loftier flight; close ones a quick pop.
            float time = Mathf.Clamp(0.55f + delta.magnitude * 0.12f, 0.6f, 1.8f);
            float f = (1f - Mathf.Exp(-k * time)) / k;

            Vector3 launch = delta / f;
            launch.y = (delta.y + g / k * (time - f)) / f;
            launch -= Vector3.up * config.UpwardAssist; // the hit adds this back
            return Vector3.ClampMagnitude(launch, config.MaxHitSpeed);
        }

        /// <summary>Blends a hit toward the nearest open hoop in range; returns the hit unchanged when none is near.</summary>
        public static Vector3 Assist(Vector3 balloonPosition, Vector3 hitVelocity, float assist, float range, BalloonConfig config)
        {
            if (assist <= 0f || config == null)
            {
                return hitVelocity;
            }

            HoopCheckpoint hoop = HoopCheckpoint.NearestOpen(balloonPosition, range);
            if (hoop == null)
            {
                return hitVelocity;
            }

            Vector3 aimed = HitVelocityThrough(balloonPosition, hoop.transform.position, config);
            return Vector3.Lerp(hitVelocity, aimed, assist);
        }
    }
}
