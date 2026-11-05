using GameJam.Core;
using UnityEngine;

namespace GameJam.Gameplay
{
    /// <summary>
    /// A platform on a rhythm: solid, shakes and turns red, disappears, comes back. Timed by the shared
    /// clock so it collapses at the same moment for everyone.
    /// </summary>
    public sealed class CollapsingPlatform : MonoBehaviour
    {
        private const float ShakeAmount = 0.03f;

        [SerializeField, Min(0.1f)] private float _period = 6f;
        [SerializeField, Min(0f)] private float _solidSeconds = 3.5f;
        [SerializeField, Min(0f)] private float _shakeSeconds = 1f;
        [SerializeField] private float _phase;
        [SerializeField] private Collider _solid;
        [SerializeField] private Renderer _visual;

        private Vector3 _homePosition;
        private Color _baseColor;

        public void Configure(float phase) => _phase = phase;

        private void Awake()
        {
            if (_solid == null)
            {
                _solid = GetComponent<Collider>();
            }

            if (_visual == null)
            {
                _visual = GetComponent<Renderer>();
            }

            _homePosition = transform.position;
            if (_visual != null)
            {
                _baseColor = _visual.sharedMaterial.color;
            }
        }

        private void Update()
        {
            float t = Mathf.Repeat(CourseClock.Time + _phase, _period);
            bool isUp = t < _solidSeconds + _shakeSeconds;
            bool isShaking = isUp && t >= _solidSeconds;

            if (_solid != null)
            {
                _solid.enabled = isUp;
            }

            if (_visual != null)
            {
                _visual.enabled = isUp;
                _visual.material.color = isShaking ? Color.Lerp(_baseColor, Color.red, 0.6f) : _baseColor;
            }

            transform.position = _homePosition + (isShaking ? Random.insideUnitSphere * ShakeAmount : Vector3.zero);
        }
    }
}
