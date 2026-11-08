using UnityEngine;

namespace GameJam.Net
{
    /// <summary>
    /// "TIME'S UP!" in every player's view when the run clock runs out, with a countdown back to the start lobby under it
    /// (GameState returns everyone, the balloon, hoops and items to the start when it reaches zero). Pops in, breathes,
    /// fades out as the lobby comes back. Lives on the rig's head camera, drawn on a dark panel over everything; purely
    /// local, read from the networked run state (the referee whistle is GameState's).
    /// </summary>
    public sealed class ResultBanner : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [SerializeField] private Renderer _panel;
        [SerializeField] private TextMesh _title;
        [SerializeField] private TextMesh _subtitle;
        [Tooltip("Full fades per second.")]
        [Min(0.1f)] [SerializeField] private float _fadeSpeed = 4f;

        private MaterialPropertyBlock _block;
        private Color _panelColor;
        private Color _titleColor;
        private Color _subtitleColor;
        private Vector3 _baseScale;
        private float _alpha;
        private float _shownAt;
        private bool _isShown = true;
        private int _shownSeconds = -1;

        public void Configure(Renderer panel, TextMesh title, TextMesh subtitle)
        {
            _panel = panel;
            _title = title;
            _subtitle = subtitle;
        }

        /// <summary>True while the banner is on screen (or fading).</summary>
        public bool IsVisible => _alpha > 0.001f;

        private void Awake()
        {
            _block = new MaterialPropertyBlock();
            _baseScale = transform.localScale;
            _panelColor = _panel != null ? _panel.sharedMaterial.color : Color.black;
            _titleColor = _title != null ? _title.color : Color.white;
            _subtitleColor = _subtitle != null ? _subtitle.color : Color.white;
            SetShown(false);
        }

        private void LateUpdate()
        {
            GameState state = GameState.Instance;
            bool show = state != null && state.IsTimeUp;
            if (show && _alpha <= 0.001f)
            {
                _shownAt = Time.time;
            }

            _alpha = Mathf.MoveTowards(_alpha, show ? 1f : 0f, Time.deltaTime * _fadeSpeed);
            SetShown(IsVisible);
            if (!IsVisible)
            {
                return;
            }

            float eased = _alpha * _alpha * (3f - 2f * _alpha);
            // Pop in with a little overshoot, then breathe gently.
            float since = Time.time - _shownAt;
            float pop = 1f + 0.25f * Mathf.Exp(-since * 7f) * Mathf.Cos(since * 18f);
            float breathe = 1f + 0.015f * Mathf.Sin(Time.time * 3f);
            transform.localScale = _baseScale * (pop * breathe * Mathf.Lerp(0.85f, 1f, eased));

            if (_panel != null)
            {
                Color c = _panelColor;
                c.a *= eased;
                _block.SetColor(BaseColorId, c);
                _panel.SetPropertyBlock(_block);
            }

            if (_title != null)
            {
                _title.color = WithAlpha(_titleColor, eased);
            }

            if (_subtitle != null && state != null)
            {
                int seconds = Mathf.CeilToInt(state.SecondsToLobby);
                if (seconds != _shownSeconds && show)
                {
                    _shownSeconds = seconds;
                    _subtitle.text = seconds > 0 ? $"Back to the start in {seconds}" : "Back to the start!";
                }

                _subtitle.color = WithAlpha(_subtitleColor, eased);
            }
        }

        private static Color WithAlpha(Color color, float alpha)
        {
            color.a *= alpha;
            return color;
        }

        private void SetShown(bool isShown)
        {
            if (isShown == _isShown)
            {
                return;
            }

            _isShown = isShown;
            foreach (Renderer part in GetComponentsInChildren<Renderer>(true))
            {
                part.enabled = isShown;
            }
        }
    }
}
