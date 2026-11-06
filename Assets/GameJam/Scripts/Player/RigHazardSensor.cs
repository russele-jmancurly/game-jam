using GameJam.Core;
using GameJam.Gameplay;
using UnityEngine;

namespace GameJam.Player
{
    /// <summary>
    /// Checks what the local player's body touches: water and falling off the map respawn you, bounce
    /// pads launch you, wind pushes you, and moving obstacles and flames knock you over.
    /// </summary>
    [RequireComponent(typeof(LocalRig))]
    public sealed class RigHazardSensor : MonoBehaviour
    {
        private const int MaxOverlaps = 24;

        private readonly Collider[] _overlaps = new Collider[MaxOverlaps];
        private LocalRig _rig;
        private float _immuneUntil;

        private void Awake() => _rig = GetComponent<LocalRig>();

        private void Update()
        {
            PlayerRigConfig config = _rig.Config;
            if (_rig.Head.position.y < config.KillHeight)
            {
                _rig.RespawnAtCheckpoint();
                return;
            }

            Vector3 center = _rig.BodyCenter;
            int count = Physics.OverlapSphereNonAlloc(center, config.HazardRadius, _overlaps, ~0, QueryTriggerInteraction.Collide);
            for (int i = 0; i < count; i++)
            {
                if (!HandleContact(_overlaps[i], center))
                {
                    return;
                }
            }
        }

        /// <summary>Returns false if the player was respawned and the rest of the contacts should be skipped.</summary>
        private bool HandleContact(Collider other, Vector3 center)
        {
            if (other.GetComponentInParent<Water>() != null)
            {
                return true; // players can wade and swim in the pool; only the balloon pops in water
            }

            Rigidbody body = _rig.Body;
            var pad = other.GetComponentInParent<BouncePad>();
            if (pad != null)
            {
                Vector3 velocity = body.linearVelocity;
                if (velocity.y < pad.LaunchSpeed)
                {
                    velocity.y = pad.LaunchSpeed;
                    body.linearVelocity = velocity;
                    pad.PlayBounce();
                }
            }

            var wind = other.GetComponentInParent<WindVolume>();
            if (wind != null)
            {
                body.linearVelocity += wind.PlayerAcceleration * Time.deltaTime;
            }

            if (Time.time < _immuneUntil || _rig.IsKnockedDown)
            {
                return true;
            }

            KnockbackConfig knockback = _rig.KnockbackConfig;
            // Moving obstacles, passing objects and the roaming character all bowl players over the same way.
            var obstacle = other.GetComponentInParent<IKnockbackSource>();
            if (obstacle != null && obstacle.KnocksPlayers)
            {
                Vector3 surfaceVelocity = obstacle.PointVelocity(center);
                float speed = surfaceVelocity.magnitude;
                if (speed > knockback.ObstacleMinSpeed)
                {
                    float force = Mathf.Clamp(speed * knockback.ObstacleForceScale, knockback.ObstacleMinForce, knockback.ObstacleMaxForce);
                    KnockOver(surfaceVelocity.normalized, force, knockback.ObstacleKnockdownSeconds);
                }
            }

            var flame = other.GetComponentInParent<PulsingHazard>();
            if (flame != null && flame.IsActive)
            {
                KnockOver((center - other.bounds.center).normalized, knockback.FlameForce, knockback.FlameKnockdownSeconds);
            }

            return true;
        }

        private void KnockOver(Vector3 direction, float force, float seconds)
        {
            _immuneUntil = Time.time + _rig.Config.HazardCooldown;
            _rig.Knock(direction, force, seconds);
        }
    }
}
