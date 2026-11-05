using Fusion;
using Photon.Voice.Unity;
using UnityEngine;

namespace GameJam.Net
{
    /// <summary>
    /// Proximity voice for a player's avatar. Photon's <c>VoiceNetworkObject</c> (on the same object) links the
    /// Recorder and Speaker to the voice session; this component only starts the local player's microphone once
    /// permission is granted, and keeps remote copies of the Recorder silent.
    /// The Speaker sits on the head, so each voice comes from that player's mouth in 3D.
    /// </summary>
    public sealed class AvatarVoice : NetworkBehaviour
    {
        [SerializeField] private Recorder _recorder;
        [SerializeField] private Speaker _speaker;

        public bool IsTalking => _recorder != null && _recorder.IsCurrentlyTransmitting;

        public bool IsSpeakerPlaying => _speaker != null && _speaker.IsPlaying;

        public void Configure(Recorder recorder, Speaker speaker)
        {
            _recorder = recorder;
            _speaker = speaker;
        }

        public override void Spawned()
        {
            if (_recorder == null)
            {
                return;
            }

            if (!HasStateAuthority)
            {
                _recorder.RecordingEnabled = false; // remote copies never touch this headset's microphone
                return;
            }

            MicrophoneAccess.Request(OnMicrophoneAccess);
        }

        private void OnMicrophoneAccess(bool isGranted)
        {
            if (this == null || _recorder == null)
            {
                return; // despawned while the permission dialog was open
            }

            if (!isGranted)
            {
                Debug.LogWarning("[Voice] No microphone (or permission denied): you can hear others but they can't hear you.");
                return;
            }

            _recorder.TransmitEnabled = true;
            _recorder.RecordingEnabled = true;
            Debug.Log("[Voice] Microphone on.");
        }
    }
}
