using UnityEngine;

namespace GameJam.Core
{
    /// <summary>Looks up per-section spawn positions for the crew and the balloon.</summary>
    public static class CourseLayout
    {
        private const float PlayerSpacing = 1.2f;
        private static readonly Vector3 FallbackBalloonSpawn = new Vector3(0f, 2.5f, 1f);

        private static SpawnPoint[] _points;

        /// <summary>Where a player's feet go at the start of a section, spread out by player slot.</summary>
        public static Vector3 PlayerSpawn(int section, int playerSlot)
        {
            int slot = Mathf.Abs(playerSlot) % 4;
            var offset = new Vector3((slot % 2) * PlayerSpacing - PlayerSpacing * 0.5f, 0f, (slot / 2) * -PlayerSpacing);

            SpawnPoint point = Find(section);
            return point == null ? Vector3.up + offset : point.transform.position + point.transform.rotation * offset;
        }

        public static Vector3 BalloonSpawn(int section)
        {
            SpawnPoint point = Find(section);
            return point == null ? FallbackBalloonSpawn : point.BalloonPosition;
        }

        private static SpawnPoint Find(int section)
        {
            if (_points == null || _points.Length == 0 || _points[0] == null)
            {
                _points = Object.FindObjectsByType<SpawnPoint>(FindObjectsSortMode.None);
            }

            SpawnPoint fallback = null;
            foreach (SpawnPoint point in _points)
            {
                if (point == null)
                {
                    continue;
                }

                if (point.Section == section)
                {
                    return point;
                }

                if (point.Section == 0)
                {
                    fallback = point;
                }
            }

            return fallback;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => _points = null;
    }
}
