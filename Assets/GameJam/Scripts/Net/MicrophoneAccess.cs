using System;
using UnityEngine;
#if UNITY_ANDROID && !UNITY_EDITOR
using UnityEngine.Android;
#endif

namespace GameJam.Net
{
    /// <summary>
    /// Asks for microphone access before voice starts recording (GDD: "On Quest, request microphone
    /// permission before enabling the Recorder"). On desktop it only checks that a microphone exists.
    /// </summary>
    public static class MicrophoneAccess
    {
        public static void Request(Action<bool> onResult)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (Permission.HasUserAuthorizedPermission(Permission.Microphone))
            {
                onResult(true);
                return;
            }

            var callbacks = new PermissionCallbacks();
            callbacks.PermissionGranted += _ => onResult(true);
            callbacks.PermissionDenied += _ => onResult(false);
            callbacks.PermissionDeniedAndDontAskAgain += _ => onResult(false);
            Permission.RequestUserPermission(Permission.Microphone, callbacks);
#else
            onResult(Microphone.devices.Length > 0);
#endif
        }
    }
}
