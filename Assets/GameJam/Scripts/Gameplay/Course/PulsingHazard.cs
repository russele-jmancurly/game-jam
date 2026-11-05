using GameJam.Core;
using UnityEngine;

namespace GameJam.Gameplay
{
    /// <summary>
    /// A hazard on a rhythm (barbecue flames). Glows yellow as a warning, then turns on. While on it
    /// pops the balloon and knocks players back. Timed by the shared clock so everyone sees the same beat.
    /// </summary>
    public sealed class PulsingHazard : MonoBehaviour
    {
        private const float ActiveHeight = 0.5f;
        private const float IdleHeight = 0.1f;

        [SerializeField, Min(0.1f)] private float _period = 3f;
        [SerializeField, Min(0f)] private float _activeSeconds = 1.2f;
        [SerializeField, Min(0f)] private float _warningSeconds = 0.8f;
        [SerializeField] private float _phase;
        [SerializeField] private string _popLine = "Toasted on the grill!";
        [SerializeField] private Renderer _visual;
        [SerializeField] private Color _idleColor = new Color(0.25f, 0.1f, 0.1f);
        [SerializeField] private Color _warningColor = new Color(1f, 0.8f, 0.2f);
        [SerializeField] private Color _activeColor = new Color(1f, 0.3f, 0f);

        public string PopLine => _popLine;

        public bool IsActive => Cycle < _activeSeconds;

        private float Cycle => Mathf.Repeat(CourseClock.Time + _phase, _period);

        public void Configure(Renderer visual) => _visual = visual;

        private void Update()
        {
            if (_visual == null)
            {
                return;
            }

            float t = Cycle;
            bool active = t < _activeSeconds;
            bool warning = !active && t > _period - _warningSeconds;
            _visual.material.color = active ? _activeColor : warning ? _warningColor : _idleColor;

            Vector3 scale = _visual.transform.localScale;
            scale.y = active ? ActiveHeight : IdleHeight;
            _visual.transform.localScale = scale;
        }
    }
}
