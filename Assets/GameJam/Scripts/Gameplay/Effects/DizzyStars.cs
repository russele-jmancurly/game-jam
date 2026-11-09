using UnityEngine;

namespace GameJam.Gameplay
{
    /// <summary>
    /// Cartoon "seeing stars": a ring of little yellow stars circling above a knocked-out character's head. The ring stays
    /// level whatever the head does and each star turns to face the camera. Visual only, no colliders. The stars are
    /// generated meshes, so there is no texture or font to miss.
    /// </summary>
    public sealed class DizzyStars : MonoBehaviour
    {
        private const int StarCount = 4;
        private const float SpinDegreesPerSecond = 220f;

        [SerializeField] private Material _material;

        private static Mesh _starMesh;
        private Transform[] _stars = System.Array.Empty<Transform>();
        private Transform _anchor;
        private float _ringRadius = 0.5f;
        private float _height = 0.5f;
        private float _spin;

        public void Configure(Material material) => _material = material;

        /// <summary>Starts circling above the anchor (a head bone, or the character's root plus a height).</summary>
        public void Show(Transform anchor, float bodyHeight, float heightAboveAnchor)
        {
            EnsureStars();
            _anchor = anchor;
            _ringRadius = 0.15f * bodyHeight;
            _height = heightAboveAnchor;
            float size = 0.08f * bodyHeight; // big enough to read from across the lawn
            foreach (Transform star in _stars)
            {
                star.localScale = Vector3.one * size;
            }

            gameObject.SetActive(true);
        }

        public void Hide()
        {
            _anchor = null;
            gameObject.SetActive(false);
        }

        private void LateUpdate()
        {
            if (_anchor == null)
            {
                return;
            }

            _spin += SpinDegreesPerSecond * Time.deltaTime;
            float bob = Mathf.Sin(Time.time * 5f) * 0.08f * _ringRadius;
            transform.SetPositionAndRotation(_anchor.position + Vector3.up * (_height + bob), Quaternion.Euler(0f, _spin, 0f));

            Camera view = Camera.main;
            for (int i = 0; i < _stars.Length; i++)
            {
                float angle = i * Mathf.PI * 2f / _stars.Length;
                Transform star = _stars[i];
                star.localPosition = new Vector3(Mathf.Cos(angle), 0.15f * Mathf.Sin(angle * 2f + Time.time * 4f), Mathf.Sin(angle)) * _ringRadius;
                if (view != null)
                {
                    star.rotation = Quaternion.LookRotation(star.position - view.transform.position, Vector3.up)
                                    * Quaternion.Euler(0f, 0f, Time.time * 160f + i * 40f); // each star twirls too
                }
            }
        }

        private void EnsureStars()
        {
            if (_stars.Length > 0)
            {
                return;
            }

            _stars = new Transform[StarCount];
            for (int i = 0; i < StarCount; i++)
            {
                var star = new GameObject($"Star{i}");
                star.transform.SetParent(transform, false);
                star.AddComponent<MeshFilter>().sharedMesh = StarMesh();
                var renderer = star.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = _material;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                _stars[i] = star.transform;
            }
        }

        /// <summary>A flat five-pointed star, one unit across, with both faces (it is seen from every side).</summary>
        private static Mesh StarMesh()
        {
            if (_starMesh != null)
            {
                return _starMesh;
            }

            var vertices = new Vector3[11];
            vertices[0] = Vector3.zero;
            for (int i = 0; i < 10; i++)
            {
                float angle = Mathf.PI * 0.5f + i * Mathf.PI / 5f;
                float radius = i % 2 == 0 ? 0.5f : 0.21f;
                vertices[i + 1] = new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f);
            }

            var triangles = new int[10 * 3 * 2];
            for (int i = 0; i < 10; i++)
            {
                int a = i + 1;
                int b = (i + 1) % 10 + 1;
                triangles[i * 6 + 0] = 0;
                triangles[i * 6 + 1] = a;
                triangles[i * 6 + 2] = b;
                triangles[i * 6 + 3] = 0;
                triangles[i * 6 + 4] = b;
                triangles[i * 6 + 5] = a;
            }

            _starMesh = new Mesh { name = "DizzyStar", vertices = vertices, triangles = triangles };
            _starMesh.RecalculateNormals();
            _starMesh.RecalculateBounds();
            return _starMesh;
        }
    }
}
