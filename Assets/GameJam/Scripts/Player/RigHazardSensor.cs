using GameJam.Core;
using GameJam.Gameplay;
using UnityEngine;

namespace GameJam.Player
{
    /// <summary>
    /// Checks what the local player's body touches: falling off the map respawns you (water doesn't), bounce
    /// pads launch you, wind pushes you, and moving obstacles and flames knock you (a shove while knockdowns are off).
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

            var hole = other.GetComponentInParent<MoleHole>();
            if (hole != null)
            {
                if (!SceneTravel.IsTravelling)
                {
                    _rig.DropHeldItems(); // while still in the session, so the others see the items let go
                }

                hole.Enter(); // down the mole tunnel to another scene; nothing else matters once that starts
                return false;
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
            // Moving obstacles, passing objects and a giant's body all shove players the same way (never a knockdown).
            var obstacle = other.GetComponentInParent<IKnockbackSource>();
            if (obstacle != null && obstacle.KnocksPlayers)
            {
                Vector3 surfaceVelocity = obstacle.PointVelocity(center);
                float speed = surfaceVelocity.magnitude;
                if (speed > knockback.ObstacleMinSpeed)
                {
                    Shove(surfaceVelocity.normalized);
                }
            }

            var flame = other.GetComponentInParent<PulsingHazard>();
            if (flame != null && flame.IsActive)
            {
                Shove((center - other.bounds.center).normalized);
            }

            return true;
        }

        /// <summary>A light shove away from the hazard, then a short immunity so it doesn't shove every frame.</summary>
        private void Shove(Vector3 direction)
        {
            _immuneUntil = Time.time + _rig.Config.HazardCooldown;
            _rig.Shove(direction);
        }
    }
}
