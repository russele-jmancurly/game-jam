using GameJam.Core;
using UnityEngine;

namespace GameJam.Editor
{
    /// <summary>Primitive-building helpers for greybox geometry and placeholder characters.</summary>
    public static class PlaceholderShapes
    {
        public static GameObject Primitive(
            PrimitiveType type, string name, Transform parent, Vector3 localPosition, Vector3 scale,
            Material material, bool keepCollider, Quaternion? localRotation = null)
        {
            GameObject shape = GameObject.CreatePrimitive(type);
            shape.name = name;
            if (parent != null)
            {
                shape.transform.SetParent(parent, false);
            }

            shape.transform.localPosition = localPosition;
            shape.transform.localScale = scale;
            if (localRotation.HasValue)
            {
                shape.transform.localRotation = localRotation.Value;
            }

            shape.GetComponent<Renderer>().sharedMaterial = material;
            if (!keepCollider)
            {
                Object.DestroyImmediate(shape.GetComponent<Collider>());
            }

            return shape;
        }

        public static GameObject Box(string name, Transform parent, Vector3 localPosition, Vector3 size, Material material, bool hasCollider = true) =>
            Primitive(PrimitiveType.Cube, name, parent, localPosition, size, material, hasCollider);

        /// <summary>An invisible trigger volume on Ignore Raycast, so gorilla hands never climb it.</summary>
        public static GameObject TriggerBox(string name, Transform parent, Vector3 localPosition, Vector3 size, Quaternion? localRotation = null)
        {
            var trigger = new GameObject(name);
            trigger.transform.SetParent(parent, false);
            trigger.transform.localPosition = localPosition;
            if (localRotation.HasValue)
            {
                trigger.transform.localRotation = localRotation.Value;
            }

            trigger.transform.localScale = size;
            trigger.AddComponent<BoxCollider>().isTrigger = true;
            trigger.layer = GameLayers.IgnoreRaycast;
            return trigger;
        }

        public static TextMesh Text(string name, Transform parent, Vector3 localPosition, float characterSize, string text, Color color)
        {
            var holder = new GameObject(name);
            holder.transform.SetParent(parent, false);
            holder.transform.localPosition = localPosition;

            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var textMesh = holder.AddComponent<TextMesh>();
            textMesh.font = font;
            holder.GetComponent<MeshRenderer>().sharedMaterial = font.material;
            textMesh.fontSize = 64;
            textMesh.characterSize = characterSize;
            textMesh.anchor = TextAnchor.MiddleCenter;
            textMesh.alignment = TextAlignment.Center;
            textMesh.color = color;
            textMesh.text = text;
            return textMesh;
        }

        public static void SetLayerRecursively(GameObject root, int layer)
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                child.gameObject.layer = layer;
            }
        }
    }
}
