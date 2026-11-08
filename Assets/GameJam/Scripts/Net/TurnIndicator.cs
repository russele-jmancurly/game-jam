using UnityEngine;

namespace GameJam.Net
{
    /// <summary>
    /// On-screen "not your turn" cue for the local player: a balloon with a red no-sign (and a short label) low in the
    /// middle of the view, between the arms. It shows while you hit the balloon last and the pass rule is on (touching it
    /// again would pop it) during a run, and fades away the moment someone else hits it. Lives on the rig's head
    /// camera; drawn on top of everything (the arms can't hide it). Purely local: reads the networked balloon state.
    /// </summary>
    public sealed class TurnIndicator : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [SerializeField] private Renderer _icon;
        [SerializeField] private TextMesh _label;
        [Tooltip("How fast the icon fades in and out (full fades per second).")]
        [Min(0.1f)] [SerializeField] private float _fadeSpeed = 6f;
        [Tooltip("Gentle breathing of the icon's size while it shows (fraction).")]
        [Range(0f, 0.2f)] [SerializeField] private float _pulse = 0.05f;

        private MaterialPropertyBlock _block;
        private Vector3 _iconScale;
        private Color _labelColor;
        private float _alpha;
        private bool _isShown = true;

        public void Configure(Renderer icon, TextMesh label)
        {
            _icon = icon;
            _label = label;
        }

        /// <summary>True while the cue is on screen (or fading out).</summary>
        public bool IsVisible => _alpha > 0.001f;

        private void Awake()
        {
            _block = new MaterialPropertyBlock();
            if (_icon != null)
            {
                _iconScale = _icon.transform.localScale;
            }

            if (_label != null)
            {
                _labelColor = _label.color;
            }

            SetShown(false);
        }

        private void LateUpdate()
        {
            NetworkBalloon balloon = NetworkBalloon.Instance;
            GameState state = GameState.Instance;
            bool show = balloon != null && balloon.IsNotLocalPlayersTurn && (state == null || state.IsBalloonLive);
            _alpha = Mathf.MoveTowards(_alpha, show ? 1f : 0f, Time.deltaTime * _fadeSpeed);
            SetShown(IsVisible);
            if (!IsVisible)
            {
                return;
            }

            float eased = _alpha * _alpha * (3f - 2f * _alpha);
            if (_icon != null)
            {
                _block.SetColor(BaseColorId, new Color(1f, 1f, 1f, eased));
                _icon.SetPropertyBlock(_block);
                float breathe = 1f + Mathf.Sin(Time.time * 4f) * _pulse;
                _icon.transform.localScale = _iconScale * (breathe * Mathf.Lerp(0.8f, 1f, eased)); // pops in
            }

            if (_label != null)
            {
                Color c = _labelColor;
                c.a *= eased;
                _label.color = c;
            }
        }

        private void SetShown(bool isShown)
        {
            if (isShown == _isShown)
            {
                return;
            }

            _isShown = isShown;
            if (_icon != null)
            {
                _icon.enabled = isShown;
            }

            if (_label != null && _label.TryGetComponent(out Renderer labelRenderer))
            {
                labelRenderer.enabled = isShown;
            }
        }
    }
}
