using UnityEngine;

namespace GameJam.Gameplay
{
    /// <summary>The master client spawns an item here when the session starts.</summary>
    public sealed class ItemSpawnPoint : MonoBehaviour
    {
        [Tooltip("Index into the bootstrap's item prefab list (wraps around).")]
        [SerializeField, Min(0)] private int _itemIndex;

        public int ItemIndex => _itemIndex;

        public void Configure(int itemIndex) => _itemIndex = itemIndex;

        private void OnDrawGizmos()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(transform.position, Vector3.one * 0.3f);
        }
    }
}
