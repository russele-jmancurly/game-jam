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

        [Header("Helping the balloon to a hoop")]
        [Tooltip("0 = the hit goes where the item was going; 1 = it is aimed to fly through the nearest hoop not yet cleared.")]
        [Range(0f, 1f)] public float HoopAssist;
        [Tooltip("Only hoops within this distance of the balloon are aimed at.")]
        [Min(0f)] public float HoopAssistRange = 9f;

        [Header("Effect where it lands (bubble wand, leaf blower)")]
        public ItemEffect Effect = ItemEffect.None;
        [Min(0f)] public float EffectSeconds = 6f;
        [Tooltip("Wind acceleration on the balloon inside the zone, m/s² (balloon gravity is 1).")]
        [Min(0f)] public float EffectStrength = 3f;
        [Tooltip("Width of the wind zone, m.")]
        [Min(0.1f)] public float EffectWidth = 1.8f;
        [Tooltip("Longest reach of the wind zone, m (an updraft's height; a gust stops at the hoop).")]
        [Min(0.5f)] public float EffectLength = 7f;
    }
}
