using System.Collections;
using System.Collections.Generic;
using GameJam.Core;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR;

namespace GameJam.Gameplay
{
    /// <summary>
    /// The studio splash that opens the game (Splash.unity, build index 0): the logo pops in on a dark backdrop,
    /// holds while the background music plays (louder than in game: AudioConfig.SplashMusic), fades out with it, then
    /// the game scene takes over (8 s in all). The logo is placed once, 5 m in front of where the player is looking,
    /// and then stays there (it does not follow the head). It is only placed once the headset reports a real pose:
    /// placed earlier it landed wherever the untracked camera faced, out of the player's sight.
    /// The game scene loads in the background the whole time, so the switch is quick.
    /// </summary>
    public sealed class StudioSplash : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [SerializeField] private Transform _head;
        [SerializeField] private Transform _logo;
        [SerializeField] private Renderer _logoRenderer;
        [SerializeField] private AudioSource _music;
        [SerializeField] private AudioConfig _audio;
        [SerializeField] private string _nextScene = SceneTravel.TutorialScene;

        [Header("Timing (seconds)")]
        [SerializeField] private float _startDelay = 0.6f;
        [SerializeField] private float _fadeIn = 0.9f;
        [SerializeField] private float _hold = 5.4f;
        [SerializeField] private float _fadeOut = 1.1f;

        [Header("Placement")]
        [SerializeField] private float _distance = 5f;
        [Tooltip("Longest wait (seconds) for the headset to report its pose before placing the logo anyway.")]
        [SerializeField] private float _maxPoseWait = 3f;
        [SerializeField] private float _popStartScale = 0.82f;

        private MaterialPropertyBlock _block;
        private Vector3 _logoScale;
        private float _musicVolume;
        private bool _hasHeadPose;

        public void Configure(Transform head, Transform logo, Renderer logoRenderer, AudioSource music, AudioConfig audio, string nextScene)
        {
            _head = head;
            _logo = logo;
            _logoRenderer = logoRenderer;
            _music = music;
            _audio = audio;
            _nextScene = nextScene;
        }

        private void Awake()
        {
            _block = new MaterialPropertyBlock();
            _logoScale = _logo.localScale;
            _musicVolume = _audio != null ? _audio.Master * _audio.SplashMusic : 0.5f;
            if (_music != null)
            {
                _music.volume = _musicVolume;
            }

            SetAlpha(0f);
        }

        private void OnEnable()
        {
            Application.onBeforeRender += TrackHead;
        }

        private void OnDisable()
        {
            Application.onBeforeRender -= TrackHead;
        }

        private IEnumerator Start()
        {
            var subsystems = new List<XRInputSubsystem>();
            SubsystemManager.GetSubsystems(subsystems);
            foreach (XRInputSubsystem subsystem in subsystems)
            {
                subsystem.TrySetTrackingOriginMode(TrackingOriginModeFlags.Floor);
            }

            AsyncOperation load = SceneManager.LoadSceneAsync(_nextScene, LoadSceneMode.Single);
            if (load != null)
            {
                load.allowSceneActivation = false;
            }

            // Wait for the headset to report its pose, so the logo goes where the player is really looking.
            yield return new WaitForSeconds(_startDelay);
            for (float waited = 0f; !_hasHeadPose && XRSettings.isDeviceActive && waited < _maxPoseWait; waited += Time.unscaledDeltaTime)
            {
                yield return null;
            }

            yield return null; // one more frame so the camera has rendered with that pose
            PlaceInFront();
            if (_music != null)
            {
                _music.Play();
            }

            for (float t = 0f; t < _fadeIn; t += Time.deltaTime)
            {
                float k = t / _fadeIn;
                SetAlpha(Mathf.SmoothStep(0f, 1f, k));
                _logo.localScale = _logoScale * Mathf.LerpUnclamped(_popStartScale, 1f, EaseOutBack(k));
                yield return null;
            }

            SetAlpha(1f);
            _logo.localScale = _logoScale;

            for (float t = 0f; t < _hold; t += Time.deltaTime)
            {
                _logo.localScale = _logoScale * (1f + 0.015f * Mathf.Sin(t * 2.4f)); // a gentle breathe
                yield return null;
            }

            for (float t = 0f; t < _fadeOut; t += Time.deltaTime)
            {
                float k = 1f - t / _fadeOut;
                SetAlpha(k * k);
                if (_music != null)
                {
                    _music.volume = _musicVolume * k;
                }

                yield return null;
            }

            SetAlpha(0f);
            if (load != null)
            {
                load.allowSceneActivation = true;
            }
            else
            {
                Debug.LogError($"StudioSplash: scene '{_nextScene}' is not in the build settings.");
            }
        }

        /// <summary>Same head tracking as LocalRig: the headset pose drives the camera (no pose in PC mode).</summary>
        private void TrackHead()
        {
            InputDevice hmd = InputDevices.GetDeviceAtXRNode(XRNode.Head);
            if (!hmd.isValid)
            {
                return;
            }

            if (hmd.TryGetFeatureValue(CommonUsages.centerEyePosition, out Vector3 position)
                || hmd.TryGetFeatureValue(CommonUsages.devicePosition, out position))
            {
                _head.localPosition = position;
            }

            if (hmd.TryGetFeatureValue(CommonUsages.centerEyeRotation, out Quaternion rotation)
                || hmd.TryGetFeatureValue(CommonUsages.deviceRotation, out rotation))
            {
                _head.localRotation = rotation;
                _hasHeadPose = true;
            }
        }

        private void PlaceInFront()
        {
            _logo.SetPositionAndRotation(TargetPosition(out Quaternion rotation), rotation);
        }

        /// <summary>Straight ahead of the player at eye height, facing them (level, so it never tilts with the head).</summary>
        private Vector3 TargetPosition(out Quaternion rotation)
        {
            Vector3 forward = Vector3.ProjectOnPlane(_head.forward, Vector3.up);
            if (forward.sqrMagnitude < 0.01f)
            {
                forward = Vector3.ProjectOnPlane(_head.up, Vector3.up); // looking straight up or down
            }

            if (forward.sqrMagnitude < 0.0001f)
            {
                forward = Vector3.forward;
            }

            forward.Normalize();
            rotation = Quaternion.LookRotation(forward, Vector3.up);
            return _head.position + forward * _distance;
        }

        private void SetAlpha(float alpha)
        {
            _block.SetColor(BaseColorId, new Color(1f, 1f, 1f, alpha));
            _logoRenderer.SetPropertyBlock(_block);
        }

        private static float EaseOutBack(float x)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            return 1f + c3 * Mathf.Pow(x - 1f, 3f) + c1 * Mathf.Pow(x - 1f, 2f);
        }
    }
}
