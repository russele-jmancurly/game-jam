using UnityEngine;

namespace GameJam.Gameplay
{
    /// <summary>
    /// Cosmetic life for set dressing: a gentle bob, sway or spin so nothing in the backyard is statuary.
    /// Purely local and visual (no colliders should rely on it).
    /// </summary>
    public sealed class IdleMotion : MonoBehaviour
    {
        [SerializeField] private float _bobHeight = 0.15f;
        [SerializeField] private float _bobSpeed = 0.6f;
        [SerializeField] private float _swayDegrees = 4f;
        [SerializeField] private float _spinDegreesPerSecond;
        [SerializeField] private float _phase;

        private Vector3 _startPosition;
        private Quaternion _startRotation;

        public void Configure(float bobHeight, float bobSpeed, float swayDegrees, float spinDegreesPerSecond, float phase)
        {
            _bobHeight = bobHeight;
            _bobSpeed = bobSpeed;
            _swayDegrees = swayDegrees;
            _spinDegreesPerSecond = spinDegreesPerSecond;
            _phase = phase;
        }

        private void Awake()
        {
            _startPosition = transform.localPosition;
            _startRotation = transform.localRotation;
        }

        private void Update()
        {
            float t = Time.time * _bobSpeed * Mathf.PI * 2f + _phase;
            transform.localPosition = _startPosition + Vector3.up * (Mathf.Sin(t) * _bobHeight);
            transform.localRotation = _startRotation
                                      * Quaternion.Euler(Mathf.Sin(t * 0.7f) * _swayDegrees, Time.time * _spinDegreesPerSecond, Mathf.Cos(t * 0.5f) * _swayDegrees);
        }
    }
}
