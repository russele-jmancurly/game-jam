using GameJam.Core;
using UnityEngine;

namespace GameJam.Gameplay
{
    /// <summary>
    /// Sweeping bars, swinging pinatas and crushers. Moves on the shared clock so every player sees it
    /// in the same place without network traffic. Knocks players over and swats (or pops) the balloon.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public sealed class MovingObstacle : MonoBehaviour
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

        [SerializeField] private bool _knocksPlayers = true;
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

        public bool PopsBalloon => _popsBalloon;

        public void ConfigureSpin(float degreesPerSecond)
        {
            _mode = MoveMode.Spin;
            _spinSpeed = degreesPerSecond;
        }

        public void ConfigureSwing(Vector3 axis, float angle, float period, bool knocksPlayers = true)
        {
            _mode = MoveMode.Swing;
            _axis = axis;
            _swingAngle = angle;
            _period = period;
            _knocksPlayers = knocksPlayers;
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
