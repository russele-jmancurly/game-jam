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
        private const string PickUpControllersText = "Pick up your controllers";
        private const float TriggerDownPressure = 0.6f;
        private const float TriggerUpPressure = 0.3f;
        private const float RideStandHeight = 1f;          // eyes within this of a ride's surface = standing on it
        private const float RideLetGoVerticalSpeed = 1f;   // faster than this up or down = hopping, the ride lets go
        private const float WalkSkin = 0.02f;              // stick walking stops this far short of a wall
        private const int WalkSlideIterations = 2;
        private const float GroundProbe = 0.1f;            // the body's bottom within this of a walkable surface = grounded
        private const float GroundMinUp = 0.5f;            // walkable = surface normal at most ~60 degrees from straight up
        private const float GroundGraceSeconds = 0.12f;    // stick walking survives this long off the ground (small bumps)

        // Where an untracked controller's hand waits, relative to the head (x is mirrored for the left hand).
        private static readonly Vector3 ParkedHandOffset = new Vector3(0.15f, -0.3f, 0.25f);

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
        private bool _controllersTracked;
        private bool _leftTriggerDown;
        private bool _rightTriggerDown;
        private float _leftGripAnalog;
        private float _rightGripAnalog;

        public static LocalRig Instance { get; private set; }

        public PlayerRigConfig Config => _config;

        public KnockbackConfig KnockbackConfig => _knockbackConfig;

        public Transform Head => _head;

        public Transform LeftController => _leftController;

        public Transform RightController => _rightController;

        public Transform LeftHandFollower => _leftHandFollower;

        public Transform RightHandFollower => _rightHandFollower;

        public Transform LocomotionRoot => _locomotion.transform;

        /// <summary>The tracking space (head and controllers); snap turn rotates it around the head.</summary>
        public Transform TrackingSpace => _head.parent;

        public Rigidbody Body => _body;

        public Vector3 BodyCenter => _locomotion.bodyCollider != null ? _locomotion.bodyCollider.transform.position : _head.position;

        public bool IsKnockedDown => Time.time < _knockedDownUntil;

        /// <summary>
        /// Both Touch controllers are tracked. False while they sleep, are set down or the Quest is in hand-tracking mode:
        /// the hands then wait by the head and can't touch anything, so nothing gets planted on the floor.
        /// </summary>
        public bool AreControllersTracked => !UsesHeadset || _controllersTracked;

        /// <summary>True when the headset drives the rig; false in PC test mode (keyboard and mouse).</summary>
        public bool UsesHeadset => XRSettings.isDeviceActive && !_config.ForceDesktopMode;

        public bool IsHolding(SlapHand hand) =>
            (hand == _leftSlapHand && _leftGrip?.Held != null) || (hand == _rightSlapHand && _rightGrip?.Held != null);

        /// <summary>
        /// How closed a paw is drawn, 0 open to 1 fist: the analog grip in the headset (the grip button in PC test mode),
        /// always a fist while holding an item, open while knocked down or with the controller set down.
        /// </summary>
        public float GripAmount(bool isRightHand)
        {
            HandGrip grip = isRightHand ? _rightGrip : _leftGrip;
            if (grip == null || IsKnockedDown || !AreControllersTracked)
            {
                return 0f;
            }

            if (grip.Held != null)
            {
                return 1f;
            }

            if (UsesHeadset)
            {
                return isRightHand ? _rightGripAnalog : _leftGripAnalog;
            }

            return grip.IsDown ? 1f : 0f;
        }

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
        /// <summary>Trigger on a hand: uses the item that hand holds (PC test mode: hold F).</summary>
        public void SetUse(bool isRightHand, bool isPressed)
        {
            HandGrip grip = isRightHand ? _rightGrip : _leftGrip;
            grip.SetUse(isPressed && !IsKnockedDown);
        }

        public void SetGrip(bool isRightHand, bool isDown)
        {
            HandGrip grip = isRightHand ? _rightGrip : _leftGrip;
            grip.Update(isDown, !IsKnockedDown, _config.GrabRadius, _config.ThrowBoost);
        }

        public void Turn(float degrees) => _locomotion.Turn(degrees);

        /// <summary>Shows or hides the rig's own glove spheres (hidden while your avatar's gorilla arms draw the hands).</summary>
        public void SetGloveVisualsVisible(bool isVisible)
        {
            foreach (Transform follower in new[] { _leftHandFollower, _rightHandFollower })
            {
                if (follower == null)
                {
                    continue;
                }

                Transform glove = follower.Find("Glove");
                if (glove != null && glove.TryGetComponent(out Renderer gloveRenderer))
                {
                    gloveRenderer.enabled = isVisible;
                }
            }
        }

        /// <summary>
        /// Knocks the local player over through the network, so friends see the ragdoll.
        /// Falls back to a local shove while offline.
        /// </summary>
        public void Knock(Vector3 direction, float force, float seconds)
        {
            if (!_knockbackConfig.KnockdownsEnabled)
            {
                // Easy mode: no knockdowns, no ragdolls; just a light shove (your body's move syncs as usual).
                Push(direction, _knockbackConfig.PlayerPushForce);
                return;
            }

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

        /// <summary>
        /// A light shove (another player's slap or item in simple mode): you keep your hands, items and control; the hands
        /// only let go of the ground for a moment so the push can move you.
        /// </summary>
        public void Push(Vector3 direction, float force)
        {
            Vector3 shove = direction;
            shove.y = Mathf.Max(shove.y, _knockbackConfig.MinUpward);
            _body.linearVelocity = shove.normalized * force;
            _knockedDownUntil = Mathf.Max(_knockedDownUntil, Time.time + _knockbackConfig.PlayerPushReleaseSeconds);

            var pulse = new HapticPulse(_knockbackConfig.HapticAmplitude * 0.5f, 0.08f);
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
            ApplyLocomotionTuning();
            _locomotion.velocityHistorySize = _config.VelocityHistorySize;
            if (_locomotion.bodyCollider != null)
            {
                // GorillaPlus hangs the capsule from the head; place its centre BodyDrop below the head.
                _locomotion.bodyOffset = Vector3.up * (_locomotion.bodyCollider.height * 0.5f - _config.BodyDrop);
            }

            _locomotion.InitializeValues();
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

        private float _appliedLightBoost = 1f;
        private float _sunBaseIntensity = -1f;
        private Color _ambientSky;
        private Color _ambientEquator;
        private Color _ambientGround;

        /// <summary>
        /// Headset only (no HDR or tonemapping there, so bright colours clip): scale the sun and the ambient light by
        /// PlayerRigConfig.HeadsetLightScale. Re-applied whenever the value changes, from the authored base values.
        /// </summary>
        private void UpdateHeadsetLighting()
        {
            float boost = UsesHeadset && !_config.PostProcessingInHeadset ? _config.HeadsetLightScale : 1f;
            if (Mathf.Approximately(boost, _appliedLightBoost))
            {
                return;
            }

            if (_sunBaseIntensity < 0f)
            {
                _sunBaseIntensity = RenderSettings.sun != null ? RenderSettings.sun.intensity : 1f;
                _ambientSky = RenderSettings.ambientSkyColor;
                _ambientEquator = RenderSettings.ambientEquatorColor;
                _ambientGround = RenderSettings.ambientGroundColor;
            }

            _appliedLightBoost = boost;
            if (RenderSettings.sun != null)
            {
                RenderSettings.sun.intensity = _sunBaseIntensity * boost;
            }

            RenderSettings.ambientSkyColor = _ambientSky * boost;
            RenderSettings.ambientEquatorColor = _ambientEquator * boost;
            RenderSettings.ambientGroundColor = _ambientGround * boost;
        }

        private void Update()
        {
            UpdateHeadsetLighting();
            if (UsesHeadset)
            {
                UpdateTracking();
                UpdateControllerPresence();
                UpdateSnapTurn();
                if (_controllersTracked)
                {
                    StickWalk(ReadStick(XRNode.LeftHand));
                }

                SetGrip(false, _controllersTracked && ReadGrip(XRNode.LeftHand));
                SetGrip(true, _controllersTracked && ReadGrip(XRNode.RightHand));
                _leftGripAnalog = _controllersTracked ? ReadGripAmount(XRNode.LeftHand) : 0f;
                _rightGripAnalog = _controllersTracked ? ReadGripAmount(XRNode.RightHand) : 0f;
                SetUse(false, _controllersTracked && ReadTrigger(XRNode.LeftHand));
                SetUse(true, _controllersTracked && ReadTrigger(XRNode.RightHand));
            }

            if (_knockedDownUntil > 0f && Time.time >= _knockedDownUntil)
            {
                _knockedDownUntil = -1f;
                ResetHands();
            }

            // Hands only touch the world when they're real (tracked) and not ragdolled.
            int touchable = IsKnockedDown || !AreControllersTracked ? 0 : _climbableLayers.value;
            if (_locomotion.locomotionEnabledLayers.value != touchable)
            {
                _locomotion.locomotionEnabledLayers = touchable;
            }

            CarryOnRide();
            ApplyLocomotionTuning();
            UpdateWrist();
        }

        /// <summary>Copies the locomotion tuning from the config every frame, so it can be tuned live in Play mode.</summary>
        private void ApplyLocomotionTuning()
        {
            _locomotion.velocityLimit = _config.VelocityLimit;
            _locomotion.slideVelocityLimit = _config.SlideVelocityLimit;
            _locomotion.maxJumpSpeed = _config.MaxJumpSpeed;
            _locomotion.jumpMultiplier = _config.JumpMultiplier;
            _locomotion.maxArmLength = _config.MaxArmLength;
            _locomotion.defaultSlideFactor = _config.DefaultSlideFactor;
            _locomotion.tapHapticStrength = _config.TapHapticStrength;
            _locomotion.slideHapticStrength = _config.SlideHapticStrength;
            _locomotion.slideVolume = _config.SlideVolume;
            if (_locomotion.leftTapSource != null)
            {
                _locomotion.leftTapSource.volume = _config.TapVolume;
            }

            if (_locomotion.rightTapSource != null)
            {
                _locomotion.rightTapSource.volume = _config.TapVolume;
            }
        }

        /// <summary>
        /// Standing on a ride (the merry-go-round): turn and move with it, as if you were on it. Gorilla locomotion alone
        /// leaves you in place while the disc spins under you. (Moving in straight steps along the surface drifted riders
        /// outward into the handle bars, where they got pinned.)
        /// </summary>
        private void CarryOnRide()
        {
            // Only while standing on it: the moment you hop (moving up or down, or more than a metre up) the ride lets go,
            // so jumping off is a normal jump.
            Transform head = _locomotion.headCollider.transform;
            if (Mathf.Abs(_body.linearVelocity.y) > RideLetGoVerticalSpeed
                || !Physics.Raycast(head.position, Vector3.down, out RaycastHit floor, RideStandHeight, _climbableLayers, QueryTriggerInteraction.Ignore))
            {
                return;
            }

            var ride = floor.collider.GetComponentInParent<GameJam.Gameplay.MovingObstacle>();
            if (ride == null || !ride.CarriesPlayers)
            {
                return;
            }

            // Turn with it exactly about its centre (no outward drift into the handles), then follow any travel it has.
            float degrees = ride.AngularVelocity.y * Mathf.Rad2Deg * Time.deltaTime;
            if (Mathf.Abs(degrees) > 0.0001f)
            {
                _locomotion.Ride(ride.Pivot, degrees);
            }

            Vector3 travel = ride.Velocity;
            travel.y = 0f;
            if (travel.sqrMagnitude > 0.0001f)
            {
                _locomotion.Carry(travel * Time.deltaTime);
            }
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

            TrackController(_leftController, XRNode.LeftHand, -1f);
            TrackController(_rightController, XRNode.RightHand, 1f);
        }

        /// <summary>
        /// Follows a controller, or parks its hand by the head while it isn't tracked. Left alone, an untracked hand
        /// stays where it was (on the floor at start), gets planted there, and when tracking returns the locomotion
        /// drags the body down to keep it planted: the player ends up buried in the ground and stuck.
        /// </summary>
        private void TrackController(Transform target, XRNode node, float side)
        {
            InputDevice device = InputDevices.GetDeviceAtXRNode(node);
            if (IsTracked(device)
                && device.TryGetFeatureValue(CommonUsages.devicePosition, out Vector3 position))
            {
                target.localPosition = position;
                if (device.TryGetFeatureValue(CommonUsages.deviceRotation, out Quaternion rotation))
                {
                    target.localRotation = rotation;
                }

                return;
            }

            var offset = new Vector3(ParkedHandOffset.x * side, ParkedHandOffset.y, ParkedHandOffset.z);
            target.SetPositionAndRotation(_head.position + _head.rotation * offset, _head.rotation);
        }

        private static bool IsTracked(InputDevice device) =>
            device.isValid && (!device.TryGetFeatureValue(CommonUsages.isTracked, out bool tracked) || tracked);

        /// <summary>When both controllers come back, re-seat the hands on them and dig the player out if they sank.</summary>
        private void UpdateControllerPresence()
        {
            bool tracked = IsTracked(InputDevices.GetDeviceAtXRNode(XRNode.LeftHand))
                           && IsTracked(InputDevices.GetDeviceAtXRNode(XRNode.RightHand));
            if (tracked == _controllersTracked)
            {
                return;
            }

            _controllersTracked = tracked;
            if (!tracked)
            {
                _leftGrip.Drop();
                _rightGrip.Drop();
                return;
            }

            ResetHands();
            // Buried = the floor under the head is above the bottom of the head collider (which normally rests ~0.5 m
            // above it). Not the rig root: that is the tracking origin, ~1 m under the floor even when standing normally.
            // The ray starts just above the eyes, so a branch or table top overhead isn't mistaken for the floor.
            Vector3 above = _head.position + Vector3.up * 0.4f;
            Collider headCollider = _locomotion.headCollider;
            if (headCollider != null
                && Physics.Raycast(above, Vector3.down, out RaycastHit ground, 3f, _climbableLayers, QueryTriggerInteraction.Ignore)
                && ground.point.y > headCollider.bounds.min.y)
            {
                RespawnAt(ground.point); // sunk into the ground: lift them out
            }
        }

        /// <summary>
        /// Trigger as a pressure with hysteresis (down above 60 %, up below 30 %): the on/off trigger button can flicker
        /// while held.
        /// </summary>
        private bool ReadTrigger(XRNode node)
        {
            bool isRight = node == XRNode.RightHand;
            bool wasDown = isRight ? _rightTriggerDown : _leftTriggerDown;
            InputDevice device = InputDevices.GetDeviceAtXRNode(node);
            float pressure = device.isValid && device.TryGetFeatureValue(CommonUsages.trigger, out float value) ? value : 0f;
            bool isDown = wasDown ? pressure > TriggerUpPressure : pressure > TriggerDownPressure;
            if (isRight)
            {
                _rightTriggerDown = isDown;
            }
            else
            {
                _leftTriggerDown = isDown;
            }

            return isDown;
        }

        private static bool ReadGrip(XRNode node)
        {
            InputDevice device = InputDevices.GetDeviceAtXRNode(node);
            return device.isValid && device.TryGetFeatureValue(CommonUsages.gripButton, out bool isDown) && isDown;
        }

        /// <summary>How far the grip is squeezed (0..1); falls back to the button for controllers without an analog grip.</summary>
        private static float ReadGripAmount(XRNode node)
        {
            InputDevice device = InputDevices.GetDeviceAtXRNode(node);
            if (!device.isValid)
            {
                return 0f;
            }

            if (device.TryGetFeatureValue(CommonUsages.grip, out float amount))
            {
                return Mathf.Clamp01(amount);
            }

            return device.TryGetFeatureValue(CommonUsages.gripButton, out bool isDown) && isDown ? 1f : 0f;
        }

        /// <summary>
        /// Walks with a thumbstick (left stick in the headset): the whole rig moves the way you look, planted hands
        /// included so they don't hold you back, stopping at walls (sliding along them) and stepping over low bumps.
        /// Gravity and arm swinging work as usual on top; the walk isn't counted as hand speed, so it never launches you.
        /// </summary>
        public void StickWalk(Vector2 stick)
        {
            if (CheckGrounded())
            {
                _lastGroundedTime = Time.time;
            }

            float deflection = Mathf.Min(1f, stick.magnitude);
            float deadZone = _config.StickDeadZone;
            // Only on the ground: in the air (a jump, a launch off a pad, a fall) the stick does nothing, so nobody steers
            // mid-air. A short grace keeps the walk smooth over small bumps.
            if (!_config.StickMovement || IsKnockedDown || deflection <= deadZone || !IsGrounded)
            {
                return;
            }

            Vector3 forward = Vector3.ProjectOnPlane(_head.forward, Vector3.up);
            if (forward.sqrMagnitude < 0.01f)
            {
                forward = Vector3.ProjectOnPlane(_head.up, Vector3.up); // looking straight down
            }

            forward.Normalize();
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            Vector3 direction = (forward * stick.y + right * stick.x).normalized;
            float speed = _config.StickMoveSpeed * Mathf.Clamp01((deflection - deadZone) / (1f - deadZone));
            Vector3 step = SweepWalk(direction * (speed * Time.deltaTime));
            if (step.sqrMagnitude > 0f)
            {
                _locomotion.Carry(step);
            }
        }

        /// <summary>
        /// True while the body stands on a walkable surface (or left one under GroundGraceSeconds ago). Updated by StickWalk.
        /// </summary>
        public bool IsGrounded => Time.time - _lastGroundedTime <= GroundGraceSeconds;

        private float _lastGroundedTime = -1f;

        /// <summary>The body capsule's bottom is resting on (or just above) a walkable surface: a short sphere cast down.</summary>
        private bool CheckGrounded()
        {
            CapsuleCollider body = _locomotion.bodyCollider;
            if (body == null)
            {
                return true; // no body to test with: never block walking
            }

            float radius = body.radius * 0.9f;
            Vector3 bottomSphere = body.transform.position + Vector3.up * (body.radius - body.height * 0.5f);
            Vector3 origin = bottomSphere + Vector3.up * 0.05f;
            float distance = 0.05f + (body.radius - radius) + GroundProbe;
            return Physics.SphereCast(origin, radius, Vector3.down, out RaycastHit hit, distance, _climbableLayers, QueryTriggerInteraction.Ignore)
                   && hit.normal.y >= GroundMinUp;
        }

        /// <summary>How far a walk step can go before the head or body meets a wall; slides along it with the rest.</summary>
        private Vector3 SweepWalk(Vector3 move)
        {
            Vector3 total = Vector3.zero;
            for (int i = 0; i < WalkSlideIterations && move.sqrMagnitude > 1e-8f; i++)
            {
                float distance = move.magnitude;
                Vector3 direction = move / distance;
                if (!CastBody(total, direction, distance + WalkSkin, out RaycastHit hit))
                {
                    return total + move;
                }

                float allowed = Mathf.Max(0f, hit.distance - WalkSkin);
                total += direction * allowed;
                Vector3 wall = Vector3.ProjectOnPlane(hit.normal, Vector3.up);
                if (wall.sqrMagnitude < 1e-6f)
                {
                    break;
                }

                move = Vector3.ProjectOnPlane(direction * (distance - allowed), wall.normalized);
                move.y = 0f;
            }

            return total;
        }

        /// <summary>Sweeps the head sphere and the body capsule (minus the step height at its bottom) along a direction.</summary>
        private bool CastBody(Vector3 offset, Vector3 direction, float distance, out RaycastHit hit)
        {
            int layers = _climbableLayers.value;
            bool isBlocked = false;
            hit = default;
            SphereCollider headCollider = _locomotion.headCollider;
            if (headCollider != null
                && Physics.SphereCast(headCollider.transform.position + offset, headCollider.radius, direction, out RaycastHit headHit,
                    distance, layers, QueryTriggerInteraction.Ignore))
            {
                hit = headHit;
                isBlocked = true;
            }

            CapsuleCollider bodyCollider = _locomotion.bodyCollider;
            if (bodyCollider != null)
            {
                Vector3 center = bodyCollider.transform.position + offset;
                float radius = bodyCollider.radius;
                float halfHeight = bodyCollider.height * 0.5f;
                Vector3 top = center + Vector3.up * Mathf.Max(0f, halfHeight - radius);
                Vector3 bottom = center + Vector3.up * (radius - halfHeight + _config.StickStepHeight);
                if (bottom.y > top.y)
                {
                    bottom = top;
                }

                if (Physics.CapsuleCast(top, bottom, radius, direction, out RaycastHit bodyHit, distance, layers, QueryTriggerInteraction.Ignore)
                    && (!isBlocked || bodyHit.distance < hit.distance))
                {
                    hit = bodyHit;
                    isBlocked = true;
                }
            }

            return isBlocked;
        }

        private static Vector2 ReadStick(XRNode node)
        {
            InputDevice device = InputDevices.GetDeviceAtXRNode(node);
            return device.isValid && device.TryGetFeatureValue(CommonUsages.primary2DAxis, out Vector2 stick) ? stick : Vector2.zero;
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
            string text = !AreControllersTracked ? PickUpControllersText : run != null ? run.WristText : "Connecting...";
            if (_wristText.text != text)
            {
                _wristText.text = text;
            }

            _wristText.transform.rotation = Quaternion.LookRotation(_wristText.transform.position - _head.position);
        }
    }
}
