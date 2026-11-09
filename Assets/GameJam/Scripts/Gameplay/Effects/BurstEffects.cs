using UnityEngine;

namespace GameJam.Gameplay
{
    /// <summary>
    /// The scene's one-shot bursts, pooled: three particle systems built by the scene builder and fired with Emit, so a pop
    /// or a hoop clear never instantiates anything. A balloon pop flings rubber shreds in the hitter's colour with a quick
    /// puff of air; a cleared hoop fires a confetti burst in the candy colours.
    /// </summary>
    public sealed class BurstEffects : MonoBehaviour
    {
        [SerializeField] private ParticleSystem _confetti;
        [SerializeField] private ParticleSystem _shreds;
        [SerializeField] private ParticleSystem _puff;
        [Range(10, 120)] [SerializeField] private int _confettiCount = 48;
        [Range(4, 40)] [SerializeField] private int _shredCount = 14;

        public static BurstEffects Instance { get; private set; }

        public void Configure(ParticleSystem confetti, ParticleSystem shreds, ParticleSystem puff)
        {
            _confetti = confetti;
            _shreds = shreds;
            _puff = puff;
        }

        /// <summary>A confetti burst (a cleared hoop). False when the scene has no burst effects (fall back).</summary>
        public static bool Confetti(Vector3 position)
        {
            if (Instance == null || Instance._confetti == null)
            {
                return false;
            }

            var emit = new ParticleSystem.EmitParams { position = position, applyShapeToPosition = true };
            Instance._confetti.Emit(emit, Instance._confettiCount);
            return true;
        }

        /// <summary>
        /// A big celebration burst (the win party): more strips than a hoop's, each about <paramref name="size"/> metres
        /// long so they read from a few metres away. False when the scene has no burst effects.
        /// </summary>
        public static bool BigConfetti(Vector3 position, int count, float size)
        {
            if (Instance == null || Instance._confetti == null)
            {
                return false;
            }

            var emit = new ParticleSystem.EmitParams { position = position, applyShapeToPosition = true, startSize = size };
            Instance._confetti.Emit(emit, count);
            return true;
        }

        /// <summary>A balloon pop: shreds in the given colour and a puff. False when the scene has none (fall back).</summary>
        public static bool Pop(Vector3 position, Color color)
        {
            if (Instance == null || Instance._shreds == null)
            {
                return false;
            }

            var shreds = new ParticleSystem.EmitParams { position = position, applyShapeToPosition = true, startColor = color };
            Instance._shreds.Emit(shreds, Instance._shredCount);
            if (Instance._puff != null)
            {
                var puff = new ParticleSystem.EmitParams { position = position, applyShapeToPosition = false };
                Instance._puff.Emit(puff, 1);
            }

            return true;
        }

        private void Awake() => Instance = this;

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }
    }
}
