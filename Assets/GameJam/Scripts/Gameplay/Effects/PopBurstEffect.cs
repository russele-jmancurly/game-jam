using UnityEngine;

namespace GameJam.Gameplay
{
    /// <summary>Placeholder pop: little bits of rubber flying out in the last hitter's colour.</summary>
    public static class PopBurstEffect
    {
        private const int PieceCount = 10;
        private const float Lifetime = 0.8f;
        private const float PieceSize = 0.06f;
        private const float BurstSpeed = 4f;

        public static void Play(Vector3 position, Color color, Material material)
        {
            if (BurstEffects.Pop(position, color))
            {
                return; // the scene's pooled shreds and puff
            }

            // Fallback without BurstEffects in the scene: little rubber cubes.
            var root = new GameObject("PopBurst");
            Object.Destroy(root, Lifetime);

            for (int i = 0; i < PieceCount; i++)
            {
                Rigidbody piece = PlaceholderPrimitives.CreatePart(
                    root.transform, PrimitiveType.Cube, position, Vector3.one * PieceSize, material, color, 0.05f);
                piece.GetComponent<Collider>().enabled = false;
                piece.AddForce(Random.onUnitSphere * BurstSpeed, ForceMode.VelocityChange);
            }
        }
    }
}
