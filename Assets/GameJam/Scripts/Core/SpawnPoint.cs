using UnityEngine;

namespace GameJam.Core
{
    /// <summary>Where the crew and the balloon start a section. Section 0 is the start line.</summary>
    public sealed class SpawnPoint : MonoBehaviour
    {
        [SerializeField, Min(0)] private int _section;
        [SerializeField] private Vector3 _balloonOffset = new Vector3(0f, 1.6f, 1.2f);

        public int Section => _section;

        public Vector3 BalloonPosition => transform.position + transform.rotation * _balloonOffset;

        public void Configure(int section, Vector3 balloonOffset)
        {
            _section = section;
            _balloonOffset = balloonOffset;
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, 0.4f);
            Gizmos.color = Color.white;
            Gizmos.DrawWireSphere(BalloonPosition, 0.25f);
        }
    }
}
