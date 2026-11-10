using System.Collections;
using System.Threading.Tasks;
using GameJam.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GameJam.Gameplay
{
    /// <summary>
    /// One player's trip between scenes down a mole tunnel: lobby -> Tunnel -> Tutorial and Tutorial -> Tunnel -> lobby.
    /// Only the local player travels (the others stay in the room), so the network session is left first
    /// (<see cref="GameServices.LeaveSession"/>); coming back, the game scene's bootstrap joins the room again.
    /// <see cref="Go"/> fades the view to black, leaves the session and loads the Tunnel scene, whose
    /// <see cref="TunnelTransition"/> plays the fall while <see cref="Destination"/> loads behind it.
    /// </summary>
    public sealed class SceneTravel : MonoBehaviour
    {
        public const string TunnelScene = "Tunnel";
        public const string TutorialScene = "Tutorial";
        public const string GameScene = "Prototype";

        private const float FadeSeconds = 0.35f;
        private const float LeaveTimeoutSeconds = 2f;

        /// <summary>The scene the tunnel leads to.</summary>
        public static string Destination { get; private set; } = TutorialScene;

        /// <summary>True from the moment a hole is entered until the destination scene takes over.</summary>
        public static bool IsTravelling { get; private set; }

        /// <summary>The scene now starting was reached through the tunnel (it fades in from black and plays a landing).</summary>
        public static bool ArrivedByTunnel { get; private set; }

        /// <summary>Starts the trip. Ignored while one is already under way.</summary>
        public static void Go(string destination)
        {
            if (IsTravelling || string.IsNullOrEmpty(destination))
            {
                return;
            }

            IsTravelling = true;
            Destination = destination;
            // Its own object, kept across the load: the hole that started the trip may be switched off or destroyed first.
            var traveller = new GameObject("SceneTravel").AddComponent<SceneTravel>();
            DontDestroyOnLoad(traveller.gameObject);
        }

        /// <summary>The tunnel calls this as it hands over to the destination scene.</summary>
        public static void MarkArrived()
        {
            IsTravelling = false;
            ArrivedByTunnel = true;
        }

        /// <summary>The destination's landing effect calls this once, so a later plain scene load doesn't replay it.</summary>
        public static bool ConsumeArrival()
        {
            bool arrived = ArrivedByTunnel;
            ArrivedByTunnel = false;
            return arrived;
        }

        private IEnumerator Start()
        {
            ScreenFade.FadeOut(FadeSeconds);
            yield return new WaitForSecondsRealtime(FadeSeconds + 0.05f);

            Task leave = GameServices.LeaveSession?.Invoke();
            float deadline = Time.unscaledTime + LeaveTimeoutSeconds;
            while (leave != null && !leave.IsCompleted && Time.unscaledTime < deadline)
            {
                yield return null;
            }

            SceneManager.LoadScene(TunnelScene, LoadSceneMode.Single);
            Destroy(gameObject);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Destination = TutorialScene;
            IsTravelling = false;
            ArrivedByTunnel = false;
        }
    }
}
