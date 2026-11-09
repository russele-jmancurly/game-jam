using System.Collections;
using UnityEngine;

namespace GameJam.Gameplay
{
    /// <summary>
    /// The win party for one player's own view (local, nothing networked: every client runs its own on a medal finish).
    /// Confetti bursts pop all around and above the viewer over a few seconds, using the pooled BurstEffects confetti, so
    /// the whole crew sees it wherever they are standing when the balloon lands on the finish.
    /// </summary>
    public sealed class WinCelebration : MonoBehaviour
    {
        private const int Bursts = 14;
        private const int PerBurst = 60;
        private const float Seconds = 3.5f;

        /// <summary>Starts the confetti around this viewer (usually the main camera).</summary>
        public static void Play(Transform viewer)
        {
            if (viewer == null)
            {
                return;
            }

            var host = new GameObject(nameof(WinCelebration));
            host.AddComponent<WinCelebration>().StartCoroutine(Run(viewer, host));
        }

        private static IEnumerator Run(Transform viewer, GameObject host)
        {
            for (int i = 0; i < Bursts && viewer != null; i++)
            {
                // A ring round the viewer, weighted to the front so most of it is in view, 2-3.5 m out and above head height.
                Vector3 forward = Vector3.ProjectOnPlane(viewer.forward, Vector3.up);
                forward = forward.sqrMagnitude > 0.01f ? forward.normalized : Vector3.forward;
                float yaw = (i % 3 == 0 ? Random.Range(-170f, 170f) : Random.Range(-65f, 65f));
                Vector3 offset = Quaternion.Euler(0f, yaw, 0f) * forward * Random.Range(2f, 3.5f) + Vector3.up * Random.Range(0.8f, 2.2f);
                BurstEffects.BigConfetti(viewer.position + offset, PerBurst, Random.Range(0.17f, 0.24f)); // hoop confetti is ~9 cm: too small from here
                yield return new WaitForSeconds(Seconds / Bursts * Random.Range(0.6f, 1.4f));
            }

            Destroy(host);
        }
    }
}
