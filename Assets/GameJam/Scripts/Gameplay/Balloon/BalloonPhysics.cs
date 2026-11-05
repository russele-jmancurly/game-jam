using GameJam.Core;
using UnityEngine;

namespace GameJam.Gameplay
{
    /// <summary>
    /// The balloon's local simulation: floaty fall, drag, bounces, wind, bounce pads, hazards and spikes.
    /// Plain C# with no networking, so it can run solo and be tested. The networked balloon calls
    /// <see cref="Step"/> on its state authority and syncs the result.
    /// </summary>
    public sealed class BalloonPhysics
    {
        private const int MaxOverlaps = 24;
        private const float SkinWidth = 0.01f;
        private const float CastRadiusScale = 0.95f;
        private const float ObstacleSwatScale = 1.2f;
        private const float ObstaclePushSpeed = 1.5f;

        private readonly BalloonConfig _config;
        private readonly Transform _self;
        private readonly Collider[] _overlaps = new Collider[MaxOverlaps];

        public BalloonPhysics(BalloonConfig config, Transform self)
        {
            _config = config;
            _self = self;
        }

        public BalloonStepResult Step(in BalloonStepInput input)
        {
            var result = new BalloonStepResult { GateReached = BalloonStepResult.NoGate };
            float dt = input.DeltaTime;
            Vector3 position = input.Position;
            Vector3 velocity = input.Velocity;

            // Floaty fall; leaking balloons are heavier.
            float gravity = _config.Gravity * (1f + _config.GravityPerLeakStep * (int)input.Condition);
            velocity += Vector3.down * (gravity * dt);
            velocity *= 1f / (1f + _config.Drag * dt);

            if (!ApplyContacts(input, position, ref velocity, ref result))
            {
                return result;
            }

            if (!MoveAndBounce(ref position, ref velocity, dt, ref result))
            {
                return result;
            }

            result.Position = position;
            result.Velocity = Vector3.ClampMagnitude(velocity, _config.MaxHitSpeed * _config.FreeFlightSpeedMultiplier);
            return result;
        }

        /// <summary>Handles everything overlapping the balloon. Returns false if it popped.</summary>
        private bool ApplyContacts(in BalloonStepInput input, Vector3 position, ref Vector3 velocity, ref BalloonStepResult result)
        {
            int count = Physics.OverlapSphereNonAlloc(position, _config.Radius, _overlaps, ~0, QueryTriggerInteraction.Collide);
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
                    velocity += wind.BalloonAcceleration * input.DeltaTime;
                }

                var pad = other.GetComponentInParent<BouncePad>();
                if (pad != null && velocity.y < pad.BalloonLaunchSpeed)
                {
                    velocity.y = pad.BalloonLaunchSpeed;
                }

                var gate = other.GetComponentInParent<SectionGate>();
                if (gate != null)
                {
                    result.GateReached = Mathf.Max(result.GateReached, gate.SectionIndex);
                }

                if (other.GetComponentInParent<FinishLine>() != null)
                {
                    result.FinishReached = true;
                }

                if (input.AreSpikesArmed && other.GetComponentInParent<ISpikeSurface>() != null)
                {
                    if (!TouchSpikes(input, other, position, ref velocity, ref result))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private bool TryGetPopReason(Collider other, out string reason)
        {
            reason = null;

            if (other.GetComponentInParent<Water>() != null)
            {
                reason = "Splash! Into the water!";
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

        /// <summary>A hard touch pops it; a graze leaks air and pushes it away. Returns false if it popped.</summary>
        private bool TouchSpikes(in BalloonStepInput input, Collider spikes, Vector3 position, ref Vector3 velocity, ref BalloonStepResult result)
        {
            if (velocity.magnitude > _config.BodyPopSpeed)
            {
                result.PopReason = "Right into the spikes!";
                return false;
            }

            if (input.CanLeak && !result.Leaked)
            {
                if (input.Condition >= BalloonCondition.NearlyFlat)
                {
                    result.PopReason = "It's out of air!";
                    return false;
                }

                result.Leaked = true;
            }

            Vector3 push = position - spikes.bounds.center;
            velocity += (push.sqrMagnitude > 0.0001f ? push.normalized : Vector3.up) * _config.SpikePushSpeed;
            return true;
        }

        /// <summary>Moves the balloon, bouncing off the solid (Default layer) world. Returns false if it popped.</summary>
        private bool MoveAndBounce(ref Vector3 position, ref Vector3 velocity, float dt, ref BalloonStepResult result)
        {
            Vector3 move = velocity * dt;
            float distance = move.magnitude;
            if (distance < 0.00001f)
            {
                return true;
            }

            Vector3 direction = move / distance;
            bool hitSomething = Physics.SphereCast(
                position, _config.Radius * CastRadiusScale, direction, out RaycastHit hit, distance,
                GameLayers.DefaultMask, QueryTriggerInteraction.Ignore);

            if (!hitSomething)
            {
                position += move;
                return true;
            }

            if (TryGetPopReason(hit.collider, out string popReason))
            {
                result.PopReason = popReason;
                return false;
            }

            position += direction * Mathf.Max(0f, hit.distance - SkinWidth);
            velocity = Vector3.Reflect(velocity, hit.normal) * _config.Bounciness;

            var obstacle = hit.collider.GetComponentInParent<MovingObstacle>();
            if (obstacle != null)
            {
                velocity += obstacle.PointVelocity(hit.point);
            }

            // Bounces off walls, furniture and floors never clear the last hitter, but they end the rally.
            result.RallyBroken = true;
            return true;
        }
    }
}
