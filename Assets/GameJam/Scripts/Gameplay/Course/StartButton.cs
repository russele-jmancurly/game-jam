using GameJam.Core;
using UnityEngine;

namespace GameJam.Gameplay
{
    /// <summary>Big red button at the start line. Slap it to start a run.</summary>
    public sealed class StartButton : MonoBehaviour, ISlappable
    {
        private static readonly HapticPulse PressFeedback = new HapticPulse(0.3f, 0.05f);

        public bool TryReceiveSlap(in SlapContext slap, out HapticPulse feedback)
        {
            feedback = PressFeedback;
            IRunStatus run = GameServices.Run;
            if (run == null)
            {
                return false;
            }

            run.RequestStart();
            return true;
        }
    }
}
