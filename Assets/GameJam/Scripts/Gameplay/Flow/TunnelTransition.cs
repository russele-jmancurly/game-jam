using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR;

namespace GameJam.Gameplay
{
    /// <summary>
    /// The mole tunnel between the yard and the tutorial (Tunnel.unity): the player drops down a shaft of soil, roots and
    /// stones while the destination scene (<see cref="SceneTravel.Destination"/>) loads in the background. The player
    /// stays where they are and the shaft's rings rush up past them and wrap round (an endless fall from a dozen rings),
    /// the daylight overhead shrinks away, wind rushes and soil streaks by. The head is tracked like in the splash, so
    /// looking around works; the fall lasts <see cref="_minSeconds"/> or until the load is ready, then the view fades and
    /// the destination takes over with its landing (<see cref="TunnelArrival"/>).
    /// </summary>
    public sealed class TunnelTransition : MonoBehaviour
    {
        [SerializeField] private Transform _head;
        [Tooltip("Everything the player falls through (rings, daylight, label, soil streaks): moved under the player's head once.")]
        [SerializeField] private Transform _shaft;
        [Tooltip("Longest wait (seconds) for the headset to report its pose before the shaft is placed anyway.")]
        [SerializeField] private float _maxPoseWait = 1f;
        [Tooltip("The shaft's rings, evenly spaced along Y. They move up past the player and wrap to the bottom.")]
        [SerializeField] private Transform[] _rings = System.Array.Empty<Transform>();
        [SerializeField] private float _ringSpacing = 1.5f;
        [Tooltip("The bright disc of daylight overhead: it shrinks as the player falls away from it.")]
        [SerializeField] private Transform _daylight;
        [SerializeField] private TextMesh _label;
        [SerializeField] private SoundLevel _wind;
        [SerializeField] private string _fallbackScene = SceneTravel.TutorialScene;

        [Header("Fall")]
        [SerializeField] private float _startSpeed = 3f;
        [SerializeField] private float _topSpeed = 11f;
        [SerializeField] private float _accelerateSeconds = 1.2f;
        [SerializeField] private float _minSeconds = 2.6f;
        [SerializeField] private float _fadeOutSeconds = 0.35f;

        private float _speed;
        private bool _isRising; // the way back up: everything runs the other way
        private bool _hasHeadPose;
        private float _elapsed;
        private float _bottom;
        private float _top;
        private Vector3 _daylightScale;

        public void Configure(Transform head, Transform shaft, Transform[] rings, float ringSpacing, Transform daylight, TextMesh label, SoundLevel wind)
        {
            _head = head;
            _shaft = shaft;
            _rings = rings;
            _ringSpacing = ringSpacing;
            _daylight = daylight;
            _label = label;
            _wind = wind;
        }

        private void Awake()
        {
            _bottom = float.MaxValue;
            _top = float.MinValue;
            foreach (Transform ring in _rings)
            {
                _bottom = Mathf.Min(_bottom, ring.localPosition.y);
                _top = Mathf.Max(_top, ring.localPosition.y);
            }

            _top += _ringSpacing; // a ring passing this height wraps back to the bottom
            _speed = _startSpeed;
            if (_daylight != null)
            {
                _daylightScale = _daylight.localScale;
            }
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

            string destination = string.IsNullOrEmpty(SceneTravel.Destination) ? _fallbackScene : SceneTravel.Destination;
            _isRising = destination != SceneTravel.TutorialScene;
            if (_label != null)
            {
                _label.text = _isRising ? "BACK UP TO THE YARD..." : "DOWN TO THE TUTORIAL...";
            }

            if (_wind != null)
            {
                _wind.FadeIn(); // through the game's mix (SoundLevel), like every other loop
            }

            AsyncOperation load = SceneManager.LoadSceneAsync(destination, LoadSceneMode.Single);
            if (load == null)
            {
                Debug.LogError($"TunnelTransition: scene '{destination}' is not in the build settings.");
                yield break;
            }

            load.allowSceneActivation = false;

            // The view is still black. Wait for the headset's pose, then put the shaft round where the player really
            // stands in their room, turned so the label is in front of them (same lesson as the splash logo: placed at
            // the origin it was out of sight, or the player stood inside the shaft's wall).
            for (float waited = 0f; !_hasHeadPose && XRSettings.isDeviceActive && waited < _maxPoseWait; waited += Time.unscaledDeltaTime)
            {
                yield return null;
            }

            PlaceShaftRoundPlayer();
            // A load held back by allowSceneActivation stops at 0.9.
            while (_elapsed < _minSeconds || load.progress < 0.9f)
            {
                yield return null;
            }

            ScreenFade.FadeOut(_fadeOutSeconds);
            if (_wind != null)
            {
                _wind.FadeOut();
            }

            yield return new WaitForSecondsRealtime(_fadeOutSeconds);
            yield return null; // one fully black frame before the switch
            SceneTravel.MarkArrived();
            load.allowSceneActivation = true;
        }

        private void Update()
        {
            float delta = Time.deltaTime;
            _elapsed += delta;
            _speed = Mathf.Lerp(_startSpeed, _topSpeed, Mathf.SmoothStep(0f, 1f, _elapsed / _accelerateSeconds));

            // Falling: the shaft rushes up past the player. Going back up: it rushes down.
            float step = (_isRising ? -_speed : _speed) * delta;
            float span = _top - _bottom;
            foreach (Transform ring in _rings)
            {
                Vector3 position = ring.localPosition;
                position.y += step;
                if (position.y >= _top)
                {
                    position.y -= span;
                }
                else if (position.y < _bottom)
                {
                    position.y += span;
                }

                ring.localPosition = position;
            }

            if (_daylight != null)
            {
                // The opening overhead falls away to a speck on the way down, and grows from a speck on the way up.
                float far = _isRising ? Mathf.Max(0f, _minSeconds - _elapsed) : _elapsed;
                _daylight.localScale = _daylightScale / (1f + far * 2.2f);
            }
        }

        private void PlaceShaftRoundPlayer()
        {
            if (_shaft == null || _head == null)
            {
                return;
            }

            Vector3 forward = Vector3.ProjectOnPlane(_head.forward, Vector3.up);
            if (forward.sqrMagnitude < 0.01f)
            {
                forward = Vector3.ProjectOnPlane(_head.up, Vector3.up); // looking straight up or down
            }

            if (forward.sqrMagnitude < 0.0001f)
            {
                forward = Vector3.forward;
            }

            _shaft.SetPositionAndRotation(new Vector3(_head.position.x, _shaft.position.y, _head.position.z),
                Quaternion.LookRotation(forward.normalized, Vector3.up));
        }

        /// <summary>Same head tracking as LocalRig and the splash: the headset pose drives the camera (no pose in PC mode).</summary>
        private void TrackHead()
        {
            InputDevice hmd = InputDevices.GetDeviceAtXRNode(XRNode.Head);
            if (!hmd.isValid || _head == null)
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
    }
}
