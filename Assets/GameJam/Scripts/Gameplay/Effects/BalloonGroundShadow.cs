using GameJam.Core;
using UnityEngine;

namespace GameJam.Gameplay
{
    /// <summary>
    /// A soft blob shadow straight under the balloon (GDD Comfort: "Ground shadow lets players track the
    /// balloon without looking straight up"). A cheap decal-style quad instead of real shadows, so it costs
    /// almost nothing on Quest and works at any distance.
    /// </summary>
    public sealed class BalloonGroundShadow : MonoBehaviour
    {
        private const float MaxDistance = 40f;
        private const float SurfaceOffset = 0.03f;
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [SerializeField] private Transform _shadow;
        [SerializeField] private Renderer _shadowRenderer;
        [Tooltip("Shadow diameter right under the balloon, and far below it.")]
        [SerializeField] private float _nearSize = 0.7f;
        [SerializeField] private float _farSize = 0.35f;
        [Tooltip("Height at which the shadow reaches its far size and faintest alpha.")]
        [SerializeField] private float _fadeHeight = 8f;
        [SerializeField, Range(0f, 1f)] private float _nearAlpha = 0.55f;
        [SerializeField, Range(0f, 1f)] private float _farAlpha = 0.2f;
        [Tooltip("The pool surface height, used when nothing solid is below.")]
        [SerializeField] private float _waterHeight = 0.05f;

        private MaterialPropertyBlock _block;

        /// <summary>
        /// Height of the surface under the balloon (ground, deck or the pool's water line) from the last frame's probe;
        /// negative infinity when nothing is below. NetworkBalloon reads it to spot a save.
        /// </summary>
        public float GroundY { get; private set; } = float.NegativeInfinity;

        public void Configure(Transform shadow, Renderer shadowRenderer)
        {
            _shadow = shadow;
            _shadowRenderer = shadowRenderer;
        }

        private void Awake()
        {
            _block = new MaterialPropertyBlock();
            if (_shadow != null)
            {
                _shadow.SetParent(null, true); // world-space so the balloon's wobble never tilts it
            }
        }

        private void OnDestroy()
        {
            if (_shadow != null)
            {
                Destroy(_shadow.gameObject);
            }
        }

        private void LateUpdate()
        {
            if (_shadow == null)
            {
                return;
            }

            Vector3 origin = transform.position;
            Vector3 point;
            Vector3 normal = Vector3.up;
            if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, MaxDistance, GameLayers.DefaultMask, QueryTriggerInteraction.Ignore))
            {
                point = hit.point;
                normal = hit.normal;
            }
            else if (origin.y > _waterHeight)
            {
                point = new Vector3(origin.x, _waterHeight, origin.z);
            }
            else
            {
                GroundY = float.NegativeInfinity;
                _shadowRenderer.enabled = false;
                return;
            }

            GroundY = point.y;

            float height01 = Mathf.Clamp01((origin.y - point.y) / _fadeHeight);
            float size = Mathf.Lerp(_nearSize, _farSize, height01);

            _shadowRenderer.enabled = true;
            _shadow.SetPositionAndRotation(point + normal * SurfaceOffset, Quaternion.FromToRotation(Vector3.forward, -normal));
            _shadow.localScale = new Vector3(size, size, 1f);

            Color color = ArtPalette.Shadow;
            color.a = Mathf.Lerp(_nearAlpha, _farAlpha, height01);
            _block.SetColor(BaseColorId, color);
            _shadowRenderer.SetPropertyBlock(_block);
        }
    }
}
