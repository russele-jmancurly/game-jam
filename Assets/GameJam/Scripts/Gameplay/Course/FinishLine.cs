using GameJam.Core;
using UnityEngine;

namespace GameJam.Gameplay
{
    /// <summary>
    /// The finish basket: a horizontal ring high over the finish pad. The run is won when the balloon drops down through
    /// the ring after every hoop is cleared; flying in from the side, gliding over the rim or rising up through it doesn't
    /// count (playtest: the old box over the pad could be reached from any side). Until every hoop is cleared the rim
    /// shows locked (red) and the commentator says how many hoops are left.
    /// The trigger sits on this object, centred on the ring; <see cref="Accepts"/> is the scoring rule.
    /// </summary>
    public sealed class FinishLine : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        [Tooltip("Rim pieces: red while locked, mint once every hoop is cleared.")]
        [SerializeField] private Renderer[] _indicators = System.Array.Empty<Renderer>();
        [Tooltip("The balloon's centre must be within this distance of the ring's axis (metres): the opening less a little.")]
        [SerializeField, Min(0.1f)] private float _openingRadius = 0.6f;
        [Tooltip("...its centre no higher than the rim plane plus this, so a balloon skimming over the rim doesn't count...")]
        [SerializeField] private float _maxAboveRim;
        [Tooltip("...and no lower than this under it, so the balloon still overlaps the rim plane: sneaking in under the rim " +
                 "from the side doesn't count either (the rim tube is in the way at that height)...")]
        [SerializeField, Min(0f)] private float _maxBelowRim = 0.25f;
        [Tooltip("...and moving down at least this fast (m/s).")]
        [SerializeField, Min(0f)] private float _minDropSpeed = 0.05f;

        private MaterialPropertyBlock _block;
        private int _paintedState = -1;

        public void Configure(Renderer[] indicators, float openingRadius)
        {
            _indicators = indicators;
            _openingRadius = openingRadius;
        }

        /// <summary>The middle of the ring, on the rim plane (for tests and aiming).</summary>
        public Vector3 RingCenter => transform.position;

        public float OpeningRadius => _openingRadius;

        /// <summary>True when a balloon here, moving like this, is dropping down through the ring.</summary>
        public bool Accepts(Vector3 balloonPosition, Vector3 balloonVelocity)
        {
            Vector3 offset = balloonPosition - transform.position;
            float sideways = new Vector2(offset.x, offset.z).magnitude;
            return balloonVelocity.y <= -_minDropSpeed && offset.y <= _maxAboveRim && offset.y >= -_maxBelowRim
                   && sideways <= _openingRadius;
        }

        private void Awake() => _block = new MaterialPropertyBlock();

        private void Update()
        {
            if (_indicators == null || _indicators.Length == 0)
            {
                return;
            }

            IRunStatus run = GameServices.Run;
            int state = run != null && run.HoopCount > 0 && run.HoopsCleared >= run.HoopCount ? 1 : 0;
            if (state == _paintedState)
            {
                return;
            }

            _paintedState = state;
            Color color = state == 1 ? ArtPalette.CandyMint : ArtPalette.Hazard;
            _block.SetColor(BaseColorId, color);
            _block.SetColor(EmissionColorId, color * 1.3f);
            foreach (Renderer indicator in _indicators)
            {
                if (indicator != null)
                {
                    indicator.SetPropertyBlock(_block);
                }
            }
        }
    }
}
