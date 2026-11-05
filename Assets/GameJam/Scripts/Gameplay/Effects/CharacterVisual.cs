using GameJam.Core;
using UnityEngine;

namespace GameJam.Gameplay
{
    /// <summary>
    /// Shows a character's model (or a coloured placeholder when the model isn't imported yet), scaled to a height with its
    /// feet at this transform's origin. Purely visual: colliders on the model are stripped, so gameplay hitboxes stay the
    /// ones the prefab defines. Used by player avatars, the roaming character and the pedestal statues.
    /// </summary>
    public sealed class CharacterVisual : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [SerializeField] private Material _placeholderMaterial;
        [Tooltip("Optional: a fixed character shown when the scene starts (map figures like the host and commentator).")]
        [SerializeField] private CharacterDefinition _showOnStart;
        [SerializeField] private float _showOnStartHeight = 1.6f;

        private GameObject _instance;
        private CharacterDefinition _shown;
        private float _shownHeight;

        public CharacterDefinition Shown => _shown;

        public void Configure(Material placeholderMaterial) => _placeholderMaterial = placeholderMaterial;

        /// <summary>Builder wiring: show this character at this height as soon as the scene starts.</summary>
        public void ShowOnLoad(CharacterDefinition character, float height)
        {
            _showOnStart = character;
            _showOnStartHeight = height;
        }

        private void Start()
        {
            if (_showOnStart != null && _shown == null)
            {
                Show(_showOnStart, _showOnStartHeight);
            }
        }

        public void Show(CharacterDefinition character, float height)
        {
            if (character == _shown && Mathf.Approximately(height, _shownHeight) && _instance != null)
            {
                return;
            }

            Clear();
            _shown = character;
            _shownHeight = height;
            if (character == null)
            {
                return;
            }

            _instance = character.Model != null ? Instantiate(character.Model, transform) : BuildPlaceholder(character);
            _instance.name = character.DisplayName;
            _instance.transform.localRotation = Quaternion.Euler(0f, character.YawOffset, 0f);
            foreach (Collider collider in _instance.GetComponentsInChildren<Collider>(true))
            {
                Destroy(collider);
            }

            foreach (Transform child in _instance.GetComponentsInChildren<Transform>(true))
            {
                child.gameObject.layer = GameLayers.IgnoreRaycast;
            }

            if (character.Model != null && character.MaterialOverride != null)
            {
                foreach (Renderer renderer in _instance.GetComponentsInChildren<Renderer>(true))
                {
                    var materials = new Material[renderer.sharedMaterials.Length];
                    for (int i = 0; i < materials.Length; i++)
                    {
                        materials[i] = character.MaterialOverride;
                    }

                    renderer.sharedMaterials = materials;
                }
            }

            FitToHeight(height);
        }

        public void SetVisible(bool isVisible)
        {
            if (_instance == null)
            {
                return;
            }

            foreach (Renderer renderer in _instance.GetComponentsInChildren<Renderer>(true))
            {
                renderer.enabled = isVisible;
            }
        }

        public void Clear()
        {
            if (_instance != null)
            {
                Destroy(_instance);
            }

            _instance = null;
            _shown = null;
        }

        /// <summary>Uniformly scales the instance to the height and puts its lowest point on this transform's origin.</summary>
        private void FitToHeight(float height)
        {
            _instance.transform.localPosition = Vector3.zero;
            _instance.transform.localScale = Vector3.one;
            Renderer[] renderers = _instance.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                return;
            }

            Bounds bounds = renderers[0].bounds;
            foreach (Renderer renderer in renderers)
            {
                bounds.Encapsulate(renderer.bounds);
            }

            float worldScale = Mathf.Max(transform.lossyScale.y, 0.0001f);
            float currentHeight = bounds.size.y / worldScale;
            if (currentHeight < 0.0001f)
            {
                return;
            }

            float factor = height / currentHeight;
            _instance.transform.localScale = Vector3.one * factor;

            Vector3 localBottom = transform.InverseTransformPoint(new Vector3(bounds.center.x, bounds.min.y, bounds.center.z));
            Vector3 localCenter = transform.InverseTransformPoint(bounds.center);
            _instance.transform.localPosition = new Vector3(-localCenter.x * factor, -localBottom.y * factor, -localCenter.z * factor);
        }

        /// <summary>A friendly blob in the character's colour until the real model is imported.</summary>
        private GameObject BuildPlaceholder(CharacterDefinition character)
        {
            var root = new GameObject("Placeholder");
            root.transform.SetParent(transform, false);
            var block = new MaterialPropertyBlock();
            block.SetColor(BaseColorId, character.Tint);

            Part(PrimitiveType.Capsule, root.transform, new Vector3(0f, 0.55f, 0f), new Vector3(0.6f, 0.55f, 0.5f), block);
            Part(PrimitiveType.Sphere, root.transform, new Vector3(0f, 1.3f, 0f), Vector3.one * 0.55f, block);
            var eyes = new MaterialPropertyBlock();
            eyes.SetColor(BaseColorId, ArtPalette.TextDark);
            Part(PrimitiveType.Sphere, root.transform, new Vector3(-0.1f, 1.36f, 0.24f), Vector3.one * 0.1f, eyes);
            Part(PrimitiveType.Sphere, root.transform, new Vector3(0.1f, 1.36f, 0.24f), Vector3.one * 0.1f, eyes);
            return root;
        }

        private void Part(PrimitiveType type, Transform parent, Vector3 localPosition, Vector3 scale, MaterialPropertyBlock block)
        {
            GameObject part = GameObject.CreatePrimitive(type);
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPosition;
            part.transform.localScale = scale;
            var renderer = part.GetComponent<Renderer>();
            if (_placeholderMaterial != null)
            {
                renderer.sharedMaterial = _placeholderMaterial;
            }

            renderer.SetPropertyBlock(block);
        }
    }
}
