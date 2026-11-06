using GameJam.Core;
using UnityEngine;

namespace GameJam.Gameplay
{
    /// <summary>
    /// Sweeping bars, swinging pinatas and crushers. Moves on the shared clock so every player sees it
    /// in the same place without network traffic. Knocks players over and swats (or pops) the balloon.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public sealed class MovingObstacle : MonoBehaviour, IKnockbackSource
    {
        [SerializeField] private MoveMode _mode = MoveMode.Spin;
        [Tooltip("Local axis to spin or swing around.")]
        [SerializeField] private Vector3 _axis = Vector3.up;
        [Tooltip("Spin: degrees per second.")]
        [SerializeField] private float _spinSpeed = 60f;
        [Tooltip("Swing: maximum angle either side.")]
        [SerializeField] private float _swingAngle = 50f;
        [Tooltip("Swing and PingPong: seconds per full cycle.")]
        [SerializeField, Min(0.1f)] private float _period = 3f;
        [Tooltip("PingPong: how far it travels.")]
        [SerializeField] private Vector3 _travel = new Vector3(0f, -2f, 0f);
        [Tooltip("PingPong: share of the cycle spent waiting at each end.")]
        [SerializeField, Range(0f, 0.9f)] private float _holdFraction = 0.4f;
        [SerializeField] private float _phase;

        [Header("Path (passing objects: lawnmower, toy train, RC car, paper plane)")]
        [Tooltip("Path: world-space waypoints, travelled in order.")]
        [SerializeField] private Vector3[] _pathPoints = System.Array.Empty<Vector3>();
        [Tooltip("Path: metres per second along the route.")]
        [SerializeField, Min(0.1f)] private float _pathSpeed = 3f;
        [Tooltip("Path: loop back to the first point, or travel back and forth.")]
        [SerializeField] private bool _pathLoops = true;
        [Tooltip("Path: extra rotation on top of facing the direction of travel (model alignment, tilt).")]
        [SerializeField] private Vector3 _pathRotationOffset;

        [SerializeField] private bool _knocksPlayers = true;
        [Tooltip("A ride (the merry-go-round): players standing on it move with its surface instead of being knocked off.")]
        [SerializeField] private bool _carriesPlayers;
        [SerializeField] private bool _popsBalloon;

        [Tooltip("Flashes before a PingPong move, so players can read it.")]
        [SerializeField] private Renderer _warningRenderer;
        [SerializeField] private Color _warningColor = new Color(1f, 0.85f, 0.2f);

        private Rigidbody _body;
        private Vector3 _startPosition;
        private Vector3 _lastPosition;
        private Vector3 _velocity;
        private Vector3 _angularVelocity;
        private Quaternion _startRotation;
        private Quaternion _lastRotation;
        private Color _baseColor;

        public bool KnocksPlayers => _knocksPlayers;

        /// <summary>Players standing on it are carried along with its surface (see LocalRig).</summary>
        public bool CarriesPlayers => _carriesPlayers;

        /// <summary>Where it is now (the centre it spins about).</summary>
        public Vector3 Pivot => _body != null ? _body.position : transform.position;

        /// <summary>Its current linear velocity, m/s.</summary>
        public Vector3 Velocity => _velocity;

        /// <summary>Its current angular velocity, rad/s (world axis x speed).</summary>
        public Vector3 AngularVelocity => _angularVelocity;

        public bool PopsBalloon => _popsBalloon;

        public void ConfigureSpin(float degreesPerSecond, bool knocksPlayers = true, bool carriesPlayers = false)
        {
            _mode = MoveMode.Spin;
            _spinSpeed = degreesPerSecond;
            _knocksPlayers = knocksPlayers;
            _carriesPlayers = carriesPlayers;
        }

        public void ConfigureSwing(Vector3 axis, float angle, float period, bool knocksPlayers = true)
        {
            _mode = MoveMode.Swing;
            _axis = axis;
            _swingAngle = angle;
            _period = period;
            _knocksPlayers = knocksPlayers;
        }

        /// <summary>
        /// Something that travels through the yard along a route (a passing object), on the shared clock so every player
        /// sees it in the same spot without network traffic.
        /// </summary>
        public void ConfigurePath(Vector3[] worldPoints, float speed, bool loops, float phaseSeconds, bool knocksPlayers, bool popsBalloon,
            Vector3 rotationOffset = default)
        {
            _mode = MoveMode.Path;
            _pathPoints = worldPoints;
            _pathSpeed = speed;
            _pathLoops = loops;
            _phase = phaseSeconds;
            _knocksPlayers = knocksPlayers;
            _popsBalloon = popsBalloon;
            _pathRotationOffset = rotationOffset;
        }

        /// <summary>How fast the obstacle's surface moves at a point, for knockbacks and swats.</summary>
        public Vector3 PointVelocity(Vector3 worldPoint)
        {
            Vector3 center = _body != null ? _body.position : transform.position;
            return _velocity + Vector3.Cross(_angularVelocity, worldPoint - center);
        }

        private void Awake()
        {
            _body = GetComponent<Rigidbody>();
            _body.isKinematic = true;
            _body.interpolation = RigidbodyInterpolation.Interpolate;
            _startPosition = _lastPosition = transform.position;
            _startRotation = _lastRotation = transform.rotation;
            if (_warningRenderer != null)
            {
                _baseColor = _warningRenderer.sharedMaterial.color;
            }
        }

        private void FixedUpdate()
        {
            float t = CourseClock.Time + _phase;
            Vector3 position = _startPosition;
            Quaternion rotation = _startRotation;

            switch (_mode)
            {
                case MoveMode.Spin:
                    rotation = _startRotation * Quaternion.AngleAxis(t * _spinSpeed, _axis);
                    break;
                case MoveMode.Swing:
                    rotation = _startRotation * Quaternion.AngleAxis(Mathf.Sin(t * Mathf.PI * 2f / _period) * _swingAngle, _axis);
                    break;
                case MoveMode.PingPong:
                    position = _startPosition + _travel * PingPong01(t);
                    break;
                case MoveMode.Path:
                    SamplePath(t, out position, out rotation);
                    break;
            }

            TrackVelocity(position, rotation, Time.fixedDeltaTime);
            _body.MovePosition(position);
            _body.MoveRotation(rotation);

            if (_warningRenderer != null)
            {
                _warningRenderer.material.color = IsWarning(t) ? _warningColor : _baseColor;
            }
        }

        private void TrackVelocity(Vector3 position, Quaternion rotation, float deltaTime)
        {
            _velocity = (position - _lastPosition) / deltaTime;

            (rotation * Quaternion.Inverse(_lastRotation)).ToAngleAxis(out float angle, out Vector3 spinAxis);
            if (angle > 180f)
            {
                angle -= 360f;
            }

            bool isSpinning = Mathf.Abs(angle) > 0.001f && !float.IsInfinity(spinAxis.x);
            _angularVelocity = isSpinning ? spinAxis * (angle * Mathf.Deg2Rad / deltaTime) : Vector3.zero;

            _lastPosition = position;
            _lastRotation = rotation;
        }

        /// <summary>Position along the route at time t, facing the direction of travel (yaw only, plus the offset).</summary>
        private void SamplePath(float t, out Vector3 position, out Quaternion rotation)
        {
            position = _startPosition;
            rotation = _startRotation;
            int count = _pathPoints?.Length ?? 0;
            if (count < 2)
            {
                return;
            }

            int segments = _pathLoops ? count : count - 1;
            float length = 0f;
            for (int i = 0; i < segments; i++)
            {
                length += Vector3.Distance(_pathPoints[i], _pathPoints[(i + 1) % count]);
            }

            if (length < 0.001f)
            {
                return;
            }

            float distance = t * _pathSpeed;
            bool isReturning = false;
            if (_pathLoops)
            {
                distance = Mathf.Repeat(distance, length);
            }
            else
            {
                distance = Mathf.Repeat(distance, length * 2f);
                if (distance > length)
                {
                    distance = length * 2f - distance;
                    isReturning = true;
                }
            }

            for (int i = 0; i < segments; i++)
            {
                Vector3 from = _pathPoints[i];
                Vector3 to = _pathPoints[(i + 1) % count];
                float segment = Vector3.Distance(from, to);
                if (distance <= segment || i == segments - 1)
                {
                    position = Vector3.Lerp(from, to, segment > 0f ? Mathf.Clamp01(distance / segment) : 0f);
                    Vector3 heading = isReturning ? from - to : to - from;
                    heading.y = 0f;
                    if (heading.sqrMagnitude > 0.0001f)
                    {
                        rotation = Quaternion.LookRotation(heading.normalized, Vector3.up) * Quaternion.Euler(_pathRotationOffset);
                    }

                    return;
                }

                distance -= segment;
            }
        }

        private float PingPong01(float t)
        {
            float u = Mathf.Repeat(t, _period) / _period;
            float hold = _holdFraction * 0.5f;
            float move = (1f - _holdFraction) * 0.5f;

            if (u < hold)
            {
                return 0f;
            }

            u -= hold;
            if (u < move)
            {
                return Mathf.SmoothStep(0f, 1f, u / move);
            }

            u -= move;
            if (u < hold)
            {
                return 1f;
            }

            u -= hold;
            return Mathf.SmoothStep(1f, 0f, u / move);
        }

        private bool IsWarning(float t)
        {
            if (_mode != MoveMode.PingPong)
            {
                return false;
            }

            float u = Mathf.Repeat(t, _period) / _period;
            float hold = _holdFraction * 0.5f;
            return u > hold * 0.4f && u < hold;
        }
    }
}
