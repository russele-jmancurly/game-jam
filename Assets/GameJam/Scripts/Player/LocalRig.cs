using System.Collections.Generic;
using GameJam.Core;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.XR;

namespace GameJam.Player
{
    /// <summary>
    /// The local player's VR rig on top of gorilla locomotion: head and controller tracking, snap turn,
    /// grabbing, knockbacks, respawning and the wrist timer. Hazards live in <see cref="RigHazardSensor"/>
    /// and the no-headset controls in <see cref="DesktopRigInput"/>.
    /// </summary>
    [DefaultExecutionOrder(-100)] // track the controllers before gorilla locomotion moves the player
    public sealed class LocalRig : MonoBehaviour
    {
        private const float SnapResetThreshold = 0.3f;
        private const float SnapTriggerThreshold = 0.75f;

        [Header("Config")]
        [SerializeField] private PlayerRigConfig _config;
        [SerializeField] private KnockbackConfig _knockbackConfig;

        [Header("References")]
        [SerializeField] private GorillaLocomotion.Player _locomotion;
        [SerializeField] private Transform _head;
        [SerializeField] private Transform _leftController;
        [SerializeField] private Transform _rightController;
        [SerializeField] private Transform _leftHandFollower;
        [SerializeField] private Transform _rightHandFollower;
        [SerializeField] private SlapHand _leftSlapHand;
        [SerializeField] private SlapHand _rightSlapHand;
        [SerializeField] private TextMesh _wristText;

        private Rigidbody _body;
        private LayerMask _climbableLayers;
        private HandGrip _leftGrip;
        private HandGrip _rightGrip;
        private float _knockedDownUntil = -1f;
        private bool _isSnapReady = true;

        public static LocalRig Instance { get; private set; }

        public PlayerRigConfig Config => _config;

        public KnockbackConfig KnockbackConfig => _knockbackConfig;

        public Transform Head => _head;

        public Transform LeftController => _leftController;

        public Transform RightController => _rightController;

        public Transform LeftHandFollower => _leftHandFollower;

        public Transform RightHandFollower => _rightHandFollower;

        public Transform LocomotionRoot => _locomotion.transform;

        public Rigidbody Body => _body;

        public Vector3 BodyCenter => _locomotion.bodyCollider != null ? _locomotion.bodyCollider.transform.position : _head.position;

        public bool IsKnockedDown => Time.time < _knockedDownUntil;

        /// <summary>True when the headset drives the rig; false in PC test mode (keyboard and mouse).</summary>
        public bool UsesHeadset => XRSettings.isDeviceActive && !_config.ForceDesktopMode;

        public bool IsHolding(SlapHand hand) =>
            (hand == _leftSlapHand && _leftGrip?.Held != null) || (hand == _rightSlapHand && _rightGrip?.Held != null);

        /// <summary>Wires the rig up; used by the editor scene builder.</summary>
        public void Configure(
            PlayerRigConfig config, KnockbackConfig knockbackConfig, GorillaLocomotion.Player locomotion, Transform head,
            Transform leftController, Transform rightController, SlapHand leftSlapHand, SlapHand rightSlapHand, TextMesh wristText)
        {
            _config = config;
            _knockbackConfig = knockbackConfig;
            _locomotion = locomotion;
            _head = head;
            _leftController = leftController;
            _rightController = rightController;
            _leftHandFollower = leftSlapHand.transform;
            _rightHandFollower = rightSlapHand.transform;
            _leftSlapHand = leftSlapHand;
            _rightSlapHand = rightSlapHand;
            _wristText = wristText;
        }

        /// <summary>Feeds a grip button state for one hand (XR controllers or the desktop test mode).</summary>
        public void SetGrip(bool isRightHand, bool isDown)
        {
            HandGrip grip = isRightHand ? _rightGrip : _leftGrip;
            grip.Update(isDown, !IsKnockedDown, _config.GrabRadius, _config.ThrowBoost);
        }

        public void Turn(float degrees) => _locomotion.Turn(degrees);

        /// <summary>
        /// Knocks the local player over through the network, so friends see the ragdoll.
        /// Falls back to a local shove while offline.
        /// </summary>
        public void Knock(Vector3 direction, float force, float seconds)
        {
            if (GameServices.LocalKnockback != null)
            {
                GameServices.LocalKnockback.SendKnockback(direction, force, seconds);
            }
            else
            {
                ApplyKnockback(direction, force, seconds);
            }
        }

        /// <summary>
        /// Your own view of a ragdoll: a straight-line shove (never a camera spin) and noodle hands that
        /// can't push off or slap until it wears off.
        /// </summary>
        public void ApplyKnockback(Vector3 direction, float force, float seconds)
        {
            Vector3 shove = direction;
            shove.y = Mathf.Max(shove.y, _knockbackConfig.MinUpward);
            _body.linearVelocity = shove.normalized * force;

            _locomotion.locomotionEnabledLayers = 0;
            _knockedDownUntil = Time.time + seconds;
            _leftGrip.Drop();
            _rightGrip.Drop();

            var pulse = new HapticPulse(_knockbackConfig.HapticAmplitude, _knockbackConfig.HapticSeconds);
            XRHaptics.Send(XRNode.LeftHand, pulse);
            XRHaptics.Send(XRNode.RightHand, pulse);
        }

        public void RespawnAtCheckpoint() =>
            RespawnAt(CourseLayout.PlayerSpawn(GameServices.CurrentCheckpoint, GameServices.LocalPlayerId));

        /// <summary>Moves the player so their feet (under their real head position) land on this point.</summary>
        public void RespawnAt(Vector3 feetPosition)
        {
            Transform root = _locomotion.transform;
            Vector3 headOffset = _head.position - root.position;
            headOffset.y = 0f;
            Vector3 target = feetPosition - headOffset;

            _body.linearVelocity = Vector3.zero;
            _body.position = target;
            root.position = target;
            Physics.SyncTransforms();
            ResetHands();
        }

        private void Awake()
        {
            Instance = this;
            _body = _locomotion.GetComponent<Rigidbody>();
            _climbableLayers = _locomotion.locomotionEnabledLayers;
            _leftGrip = new HandGrip(_leftHandFollower, _leftSlapHand);
            _rightGrip = new HandGrip(_rightHandFollower, _rightSlapHand);
            Application.onBeforeRender += OnBeforeRender;
        }

        private void OnDestroy()
        {
            Application.onBeforeRender -= OnBeforeRender;
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void Start()
        {
            var subsystems = new List<XRInputSubsystem>();
            SubsystemManager.GetSubsystems(subsystems);
            foreach (XRInputSubsystem subsystem in subsystems)
            {
                subsystem.TrySetTrackingOriginMode(TrackingOriginModeFlags.Floor);
            }

            if (_head.TryGetComponent(out Camera view))
            {
                // PC test mode with a headset plugged in: render to the Game view, not the headset.
                if (_config.ForceDesktopMode)
                {
                    view.stereoTargetEye = StereoTargetEyeMask.None;
                }

                if (view.TryGetComponent(out UniversalAdditionalCameraData cameraData))
                {
                    cameraData.renderPostProcessing = !UsesHeadset || _config.PostProcessingInHeadset;
                }
            }
        }

        private void OnBeforeRender()
        {
            if (UsesHeadset)
            {
                UpdateTracking();
            }
        }

        private void Update()
        {
            if (UsesHeadset)
            {
                UpdateTracking();
                UpdateSnapTurn();
                SetGrip(false, ReadGrip(XRNode.LeftHand));
                SetGrip(true, ReadGrip(XRNode.RightHand));
            }

            if (_knockedDownUntil > 0f && Time.time >= _knockedDownUntil)
            {
                _knockedDownUntil = -1f;
                _locomotion.locomotionEnabledLayers = _climbableLayers;
                ResetHands();
            }

            if (_locomotion.bodyCollider != null)
            {
                _locomotion.bodyCollider.transform.position = _head.position + Vector3.down * _config.BodyDrop;
            }

            UpdateWrist();
        }

        private void LateUpdate()
        {
            // Gloves follow the controller rotation; gorilla locomotion only moves their position.
            _leftHandFollower.rotation = _leftController.rotation;
            _rightHandFollower.rotation = _rightController.rotation;
        }

        private void UpdateTracking()
        {
            InputDevice hmd = InputDevices.GetDeviceAtXRNode(XRNode.Head);
            if (hmd.isValid)
            {
                if (hmd.TryGetFeatureValue(CommonUsages.centerEyePosition, out Vector3 position)
                    || hmd.TryGetFeatureValue(CommonUsages.devicePosition, out position))
                {
                    _head.localPosition = position;
                }

                if (hmd.TryGetFeatureValue(CommonUsages.centerEyeRotation, out Quaternion rotation)
                    || hmd.TryGetFeatureValue(CommonUsages.deviceRotation, out rotation))
                {
                    _head.localRotation = rotation;
                }
            }

            TrackController(_leftController, XRNode.LeftHand);
            TrackController(_rightController, XRNode.RightHand);
        }

        private static void TrackController(Transform target, XRNode node)
        {
            InputDevice device = InputDevices.GetDeviceAtXRNode(node);
            if (!device.isValid)
            {
                return;
            }

            if (device.TryGetFeatureValue(CommonUsages.devicePosition, out Vector3 position))
            {
                target.localPosition = position;
            }

            if (device.TryGetFeatureValue(CommonUsages.deviceRotation, out Quaternion rotation))
            {
                target.localRotation = rotation;
            }
        }

        private static bool ReadGrip(XRNode node)
        {
            InputDevice device = InputDevices.GetDeviceAtXRNode(node);
            return device.isValid && device.TryGetFeatureValue(CommonUsages.gripButton, out bool isDown) && isDown;
        }

        private void UpdateSnapTurn()
        {
            InputDevice device = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
            if (!device.TryGetFeatureValue(CommonUsages.primary2DAxis, out Vector2 stick))
            {
                return;
            }

            float x = stick.x;
            if (Mathf.Abs(x) < SnapResetThreshold)
            {
                _isSnapReady = true;
            }
            else if (_isSnapReady && Mathf.Abs(x) > SnapTriggerThreshold)
            {
                _isSnapReady = false;
                Turn(Mathf.Sign(x) * _config.SnapTurnDegrees);
            }
        }

        private void ResetHands()
        {
            _leftHandFollower.position = _leftController.position;
            _rightHandFollower.position = _rightController.position;
            _locomotion.InitializeValues();
        }

        private void UpdateWrist()
        {
            if (_wristText == null)
            {
                return;
            }

            IRunStatus run = GameServices.Run;
            string text = run != null ? run.WristText : "Connecting...";
            if (_wristText.text != text)
            {
                _wristText.text = text;
            }

            _wristText.transform.rotation = Quaternion.LookRotation(_wristText.transform.position - _head.position);
        }
    }
}
