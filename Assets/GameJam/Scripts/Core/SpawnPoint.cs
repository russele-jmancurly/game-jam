using UnityEngine;

namespace GameJam.Core
{
    /// <summary>
    /// Where the crew and the balloon come back for a checkpoint: 0 is the start line, k sits beside hoop k-1 so a popped
    /// balloon returns next to the last hoop the crew cleared.
    /// </summary>
    public sealed class SpawnPoint : MonoBehaviour
    {
        [SerializeField, Min(0)] private int _checkpoint;
        [SerializeField] private Vector3 _balloonOffset = new Vector3(0f, 1.6f, 1.2f);

        public int Checkpoint => _checkpoint;

        public Vector3 BalloonPosition => transform.position + transform.rotation * _balloonOffset;

        public void Configure(int checkpoint, Vector3 balloonOffset)
        {
            _checkpoint = checkpoint;
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
