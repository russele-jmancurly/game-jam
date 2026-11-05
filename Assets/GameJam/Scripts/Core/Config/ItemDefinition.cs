using UnityEngine;

namespace GameJam.Core
{
    /// <summary>One throwable item's tuning (GDD: Items). One asset per item.</summary>
    [CreateAssetMenu(menuName = "Game Jam/Config/Item Definition", fileName = "Item_New")]
    public sealed class ItemDefinition : ScriptableObject
    {
        public string DisplayName = "Item";
        public ItemKind Kind = ItemKind.Soft;
        [Tooltip("Balloon speed = item speed x this, capped by BalloonConfig.MaxHitSpeed.")]
        [Min(0f)] public float BalloonPower = 1f;
        [Tooltip("Knockback force on a friend (heavy items; soft items use KnockbackConfig.SoftItemForce).")]
        [Min(0f)] public float FriendForce = 4f;
        [Tooltip("Slower than this, a contact does not count as a hit.")]
        [Min(0f)] public float MinHitSpeed = 0.8f;
        [Tooltip("Below this height the item respawns at its spot.")]
        public float KillHeight = -2f;
        [Min(0.01f)] public float Mass = 0.3f;
        [Tooltip("Seconds between two hits from the same item.")]
        [Min(0f)] public float HitCooldown = 0.3f;
    }
}
