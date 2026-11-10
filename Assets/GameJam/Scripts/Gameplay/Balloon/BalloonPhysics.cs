using GameJam.Core;
using UnityEngine;

namespace GameJam.Gameplay
{
    /// <summary>
    /// The balloon's game rules for one physics step: floaty fall, drag, wind, bounce pads, hazards, spikes, the ground,
    /// hoops and the finish. Plain C# with no networking. The Rigidbody moves the balloon and handles bounces (so Fusion 2.1 Forecast
    /// Physics can extrapolate it on every client); this class only decides the new velocity and what happened.
    /// </summary>
    public sealed class BalloonPhysics
    {
        private const int MaxOverlaps = 24;
        private const float ContactSkin = 1.08f; // overlap a touch past the collider so resting contacts register
        private const float ObstacleSwatScale = 1.2f;
        private const float ObstaclePushSpeed = 1.5f;

        private readonly BalloonConfig _config;
        private readonly Transform _self;
        private readonly Collider[] _overlaps = new Collider[MaxOverlaps];
        private bool _wasTouchingSpikes; // last step: a held contact leaks once, not every LeakCooldown

        public BalloonPhysics(BalloonConfig config, Transform self)
        {
            _config = config;
            _self = self;
        }

        public BalloonStepResult Step(in BalloonStepInput input)
        {
            var result = new BalloonStepResult { HoopReached = BalloonStepResult.NoHoop, Position = input.Position };
            float dt = input.DeltaTime;
            Vector3 velocity = input.Velocity;

            // Floaty fall; leaking balloons are heavier.
            float gravity = _config.Gravity * (1f + _config.GravityPerLeakStep * (int)input.Condition);
            velocity += Vector3.down * (gravity * dt);

            // Flutter: a slowly wandering sideways push (two noise channels), so it never falls dead straight.
            if (_config.FlutterStrength > 0f)
            {
                float t = input.Time * _config.FlutterSpeed;
                float x = Mathf.PerlinNoise(t, 0.37f) * 2f - 1f;
                float z = Mathf.PerlinNoise(0.71f, t) * 2f - 1f;
                velocity += new Vector3(x, 0f, z) * (_config.FlutterStrength * dt);
            }

            // Air drag: a constant part plus a part that grows with speed, so hard hits bleed speed quickly and slow
            // drifting lingers. Implicit form, stable at any frame rate.
            float drag = _config.Drag + _config.QuadraticDrag * velocity.magnitude;
            velocity *= 1f / (1f + drag * dt);

            if (!ApplyContacts(input, ref velocity, ref result))
            {
                return result;
            }

            result.Velocity = Vector3.ClampMagnitude(velocity, _config.MaxHitSpeed * _config.FreeFlightSpeedMultiplier);
            return result;
        }

        /// <summary>Handles everything overlapping the balloon. Returns false if it popped.</summary>
        private bool ApplyContacts(in BalloonStepInput input, ref Vector3 velocity, ref BalloonStepResult result)
        {
            Vector3 position = input.Position;
            bool touchingSpikes = false;
            bool newSpikeContact = !_wasTouchingSpikes;
            _wasTouchingSpikes = false; // stays false if it pops (it goes back on station)
            int count = Physics.OverlapSphereNonAlloc(position, _config.Radius * ContactSkin, _overlaps, ~0, QueryTriggerInteraction.Collide);
            for (int i = 0; i < count; i++)
            {
                Collider other = _overlaps[i];
                if (other.transform.IsChildOf(_self))
                {
                    continue;
                }

                if (TryGetPopReason(other, out string popReason))
                {
                    result.PopReason = popReason;
                    return false;
                }

                var obstacle = other.GetComponentInParent<MovingObstacle>();
                if (obstacle != null && !other.isTrigger)
                {
                    Vector3 away = position - other.ClosestPoint(position);
                    Vector3 pushDirection = away.sqrMagnitude > 0.0001f ? away.normalized : Vector3.up;
                    velocity = obstacle.PointVelocity(position) * ObstacleSwatScale + pushDirection * ObstaclePushSpeed;
                    result.RallyBroken = true;
                }

                var wind = other.GetComponentInParent<WindVolume>();
                if (wind != null)
                {
                    velocity += wind.BalloonAccelerationAt(position) * input.DeltaTime;
                }

                var pad = other.GetComponentInParent<BouncePad>();
                if (pad != null && pad.LaunchesBalloon && velocity.y < pad.BalloonLaunchSpeed)
                {
                    velocity.y = pad.BalloonLaunchSpeed;
                }

                var hoop = other.GetComponentInParent<HoopCheckpoint>();
                if (hoop != null)
                {
                    result.HoopReached = hoop.Index;
                }

                if (other.GetComponentInParent<FinishLine>() is { } finish && finish.Accepts(position, velocity))
                {
                    result.FinishReached = true; // only dropping down through the basket's ring
                }

                if (input.AreSpikesArmed && other.GetComponentInParent<ISpikeSurface>() != null)
                {
                    touchingSpikes = true;
                    if (!TouchSpikes(input, other, position, newSpikeContact, ref velocity, ref result))
                    {
                        return false;
                    }
                }
            }

            _wasTouchingSpikes = touchingSpikes;
            return true;
        }

        private static bool TryGetPopReason(Collider other, out string reason)
        {
            reason = null;

            if (other.GetComponentInParent<Water>() != null)
            {
                reason = "Splash! Into the water!";
            }
            else if (!other.isTrigger && other.GetComponentInParent<BalloonGround>() is { } ground)
            {
                reason = ground.PopLine; // the title rule: don't let the balloon drop
            }
            else if (other.GetComponentInParent<Sharp>() is { IsActive: true } sharp)
            {
                reason = sharp.PopLine;
            }
            else if (other.GetComponentInParent<PulsingHazard>() is { IsActive: true } flame)
            {
                reason = flame.PopLine;
            }
            else if (!other.isTrigger && other.GetComponentInParent<MovingObstacle>() is { PopsBalloon: true })
            {
                reason = "Crushed!";
            }

            return reason != null;
        }

        /// <summary>
        /// Flying into the spikes pops it; a graze leaks a little air (once per touch, not per step or per second while
        /// it rests there) and bounces it off. Returns false if it popped.
        /// </summary>
        private bool TouchSpikes(in BalloonStepInput input, Collider spikes, Vector3 position, bool newContact,
            ref Vector3 velocity, ref BalloonStepResult result)
        {
            Vector3 push = position - spikes.bounds.center;
            Vector3 away = push.sqrMagnitude > 0.0001f ? push.normalized : Vector3.up;

            // Only speed toward the spikes counts: a balloon skimming past, or already bouncing off, isn't a hard hit.
            float closing = -Vector3.Dot(velocity, away);
            if (closing > _config.BodyPopSpeed)
            {
                result.PopReason = "Right into the spikes!";
                return false;
            }

            if (newContact && input.CanLeak && !result.Leaked)
            {
                if (input.Condition >= BalloonCondition.NearlyFlat)
                {
                    result.PopReason = "It's out of air!";
                    return false;
                }

                result.Leaked = true;
            }

            // Bounce off at SpikePushSpeed: set the outward part, never add it every step (that used to stack past
            // BodyPopSpeed within three steps of a slow, held touch and pop it).
            float outward = Vector3.Dot(velocity, away);
            if (outward < _config.SpikePushSpeed)
            {
                velocity += away * (_config.SpikePushSpeed - outward);
            }

            return true;
        }
    }
}
