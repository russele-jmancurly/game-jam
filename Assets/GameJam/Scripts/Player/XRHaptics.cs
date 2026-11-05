using GameJam.Core;
using UnityEngine.XR;

namespace GameJam.Player
{
    /// <summary>Sends controller rumbles. Does nothing without a headset.</summary>
    public static class XRHaptics
    {
        public static void Send(XRNode node, in HapticPulse pulse)
        {
            if (pulse.Amplitude <= 0f || pulse.Seconds <= 0f)
            {
                return;
            }

            InputDevice device = InputDevices.GetDeviceAtXRNode(node);
            if (device.isValid)
            {
                device.SendHapticImpulse(0, pulse.Amplitude, pulse.Seconds);
            }
        }
    }
}
