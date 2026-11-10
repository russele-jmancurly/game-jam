using UnityEngine;

namespace GameJam.Gameplay
{
    /// <summary>
    /// Fades the player's whole view to black and back: a black quad just in front of the head camera, drawn on top of
    /// everything (no full-screen post-processing pass, which the Quest can't afford). Used going into and coming out of
    /// the mole tunnel. A scene reached through the tunnel starts black and fades in.
    /// </summary>
    public sealed class ScreenFade : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [SerializeField] private Renderer _quad;
        [Tooltip("Always start black and fade in (the tunnel scene). Otherwise only after arriving through the tunnel.")]
        [SerializeField] private bool _alwaysFadeIn;
        [SerializeField] private float _fadeInSeconds = 0.7f;

        private MaterialPropertyBlock _block;
        private float _alpha;
        private float _target;
        private float _speed;

        public static ScreenFade Instance { get; private set; }

        /// <summary>How black the view is now: 0 clear, 1 black.</summary>
        public float Alpha => _alpha;

        public void Configure(Renderer quad, bool alwaysFadeIn)
        {
            _quad = quad;
            _alwaysFadeIn = alwaysFadeIn;
        }

        /// <summary>Fades to black. Does nothing in a scene without a fader.</summary>
        public static void FadeOut(float seconds)
        {
            if (Instance != null)
            {
                Instance.FadeTo(1f, seconds);
            }
        }

        /// <summary>Fades back to a clear view.</summary>
        public static void FadeIn(float seconds)
        {
            if (Instance != null)
            {
                Instance.FadeTo(0f, seconds);
            }
        }

        public void FadeTo(float alpha, float seconds)
        {
            _target = Mathf.Clamp01(alpha);
            _speed = seconds > 0.001f ? 1f / seconds : 1000f;
        }

        private void Awake()
        {
            Instance = this;
            _block = new MaterialPropertyBlock();
            if (_alwaysFadeIn || SceneTravel.ArrivedByTunnel)
            {
                _alpha = 1f;
                FadeTo(0f, _fadeInSeconds);
            }

            Apply();
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void Update()
        {
            if (Mathf.Approximately(_alpha, _target))
            {
                return;
            }

            _alpha = Mathf.MoveTowards(_alpha, _target, _speed * Time.unscaledDeltaTime);
            Apply();
        }

        private void Apply()
        {
            if (_quad == null)
            {
                return;
            }

            _quad.enabled = _alpha > 0.001f;
            _block.SetColor(BaseColorId, new Color(0f, 0f, 0f, _alpha));
            _quad.SetPropertyBlock(_block);
        }
    }
}
