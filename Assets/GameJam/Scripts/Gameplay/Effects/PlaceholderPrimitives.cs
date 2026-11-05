using GameJam.Core;
using UnityEngine;

namespace GameJam.Gameplay
{
    /// <summary>Builds throwaway physics primitives for placeholder effects, until real art arrives.</summary>
    public static class PlaceholderPrimitives
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static MaterialPropertyBlock _block;

        /// <summary>Creates a coloured primitive with a rigidbody on the Ignore Raycast layer (never climbable).</summary>
        public static Rigidbody CreatePart(
            Transform parent, PrimitiveType type, Vector3 position, Vector3 scale, Material material, Color color, float mass)
        {
            GameObject part = GameObject.CreatePrimitive(type);
            part.layer = GameLayers.IgnoreRaycast;
            part.transform.SetParent(parent, true);
            part.transform.position = position;
            part.transform.localScale = scale;

            var renderer = part.GetComponent<Renderer>();
            if (material != null)
            {
                renderer.sharedMaterial = material;
            }

            _block ??= new MaterialPropertyBlock();
            _block.SetColor(BaseColorId, color);
            renderer.SetPropertyBlock(_block);

            var body = part.AddComponent<Rigidbody>();
            body.mass = mass;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            return body;
        }
    }
}
