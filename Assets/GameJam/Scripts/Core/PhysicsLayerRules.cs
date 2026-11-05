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
        }
    }
}
