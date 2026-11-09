using UnityEngine;

namespace GameJam.Core
{
    /// <summary>Layer collision rules the game relies on, applied before the first scene loads.</summary>
    public static class PhysicsLayerRules
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Apply()
        {
            // The balloon never physically bumps players' local rig bodies or held items; slaps, spikes and swung items
            // are detected by overlap checks instead, which every client evaluates the same way.
            Physics.IgnoreLayerCollision(GameLayers.Balloon, GameLayers.IgnoreRaycast, true);

            // A giant's ragdoll lands on the world and on itself, and nothing else (see GameLayers.Ragdoll).
            for (int layer = 0; layer < 32; layer++)
            {
                if (layer != GameLayers.Default && layer != GameLayers.Ragdoll)
                {
                    Physics.IgnoreLayerCollision(GameLayers.Ragdoll, layer, true);
                }
            }
        }
    }
}
