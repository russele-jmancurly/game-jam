using GameJam.Core;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace GameJam.Player
{
    /// <summary>
    /// Desktop test mode for the Editor when no headset is connected:
    /// WASD move, Space jump, hold right mouse to look, left click slap, hold G to grab and release to throw,
    /// K to knock yourself over, Enter to start the run.
    /// </summary>
    [DefaultExecutionOrder(-110)] // before LocalRig and gorilla locomotion
    [RequireComponent(typeof(LocalRig))]
    public sealed class DesktopRigInput : MonoBehaviour
    {
        private const float EyeHeight = 1.2f;
        private const float MaxPitch = 80f;
        private const float SlapSeconds = 0.2f;
        private const float RestReach = 0.3f;
        private const float SlapReach = 0.7f;

        private static readonly Vector3 LeftHandRest = new Vector3(-0.25f, -0.35f, 0.3f);
        private static readonly Vector3 RightHandRest = new Vector3(0.2f, -0.2f, 0f);

        private LocalRig _rig;
        private float _yaw;
        private float _pitch;
        private float _slapTimer;

        private void Awake() => _rig = GetComponent<LocalRig>();

        private void Update()
        {
            if (_rig.UsesHeadset)
            {
                return;
            }

#if ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            Mouse mouse = Mouse.current;
            if (keyboard == null || mouse == null)
            {
                return;
            }

            UpdateLook(mouse);
            UpdateHands(mouse);
            UpdateMovement(keyboard);

            _rig.SetGrip(true, keyboard.gKey.isPressed);
            _rig.SetUse(true, keyboard.fKey.isPressed);

            if (keyboard.kKey.wasPressedThisFrame)
            {
                _rig.Shove(-_rig.Head.forward); // test key: the shove every hazard gives
            }

            if (keyboard.enterKey.wasPressedThisFrame)
            {
                GameServices.Run?.RequestStart();
            }
#endif
        }

        private const string ControlsHelp =
            "PC TEST MODE\n" +
            "WASD  move     Space  jump\n" +
            "Hold right mouse  look\n" +
            "Left click  slap (right glove)\n" +
            "Hold G  grab, release  throw\n" +
            "Hold F  use the held item (its trigger)\n" +
            "K  knock yourself over\n" +
            "Enter  start the run (or slap the red button)";

        private GUIStyle _helpStyle;

        private void OnGUI()
        {
            if (_rig.UsesHeadset || !_rig.Config.ShowDesktopControlsHelp)
            {
                return;
            }

            if (_helpStyle == null)
            {
                // Palette-styled panel instead of the default IMGUI skin.
                var background = new Texture2D(1, 1);
                Color panel = ArtPalette.TextDark;
                panel.a = 0.85f;
                background.SetPixel(0, 0, panel);
                background.Apply();
                _helpStyle = new GUIStyle(GUI.skin.box)
                {
                    alignment = TextAnchor.UpperLeft,
                    fontSize = 14,
                    fontStyle = FontStyle.Bold,
                    padding = new RectOffset(12, 12, 10, 10),
                };
                _helpStyle.normal.background = background;
                _helpStyle.normal.textColor = ArtPalette.Accent;
            }
            string status = GameServices.Run != null ? $"\nRun: {GameServices.Run.State}  {GameServices.Run.WristText}" : "\nConnecting...";
            GUI.Box(new Rect(10f, 10f, 330f, 170f), ControlsHelp + status, _helpStyle);
        }

#if ENABLE_INPUT_SYSTEM
        private void UpdateLook(Mouse mouse)
        {
            if (mouse.rightButton.isPressed)
            {
                Vector2 delta = mouse.delta.ReadValue() * _rig.Config.DesktopMouseSensitivity;
                _yaw += delta.x;
                _pitch = Mathf.Clamp(_pitch - delta.y, -MaxPitch, MaxPitch);
            }

            _rig.Head.localPosition = new Vector3(0f, EyeHeight, 0f);
            _rig.Head.localRotation = Quaternion.Euler(_pitch, _yaw, 0f);
        }

        private void UpdateHands(Mouse mouse)
        {
            if (mouse.leftButton.wasPressedThisFrame)
            {
                _slapTimer = SlapSeconds;
            }

            _slapTimer -= Time.deltaTime;
            float reach = _slapTimer > 0f ? SlapReach : RestReach;

            Quaternion yaw = Quaternion.Euler(0f, _yaw, 0f);
            Vector3 eye = _rig.Head.localPosition;
            Vector3 rightOffset = RightHandRest + new Vector3(0f, 0f, reach);

            _rig.LeftController.localPosition = eye + yaw * LeftHandRest;
            _rig.RightController.localPosition = eye + _rig.Head.localRotation * rightOffset;
            _rig.LeftController.localRotation = yaw;
            _rig.RightController.localRotation = _rig.Head.localRotation;
        }

        private void UpdateMovement(Keyboard keyboard)
        {
            Rigidbody body = _rig.Body;
            Vector3 input = Vector3.zero;
            if (keyboard.wKey.isPressed) input += Vector3.forward;
            if (keyboard.sKey.isPressed) input += Vector3.back;
            if (keyboard.aKey.isPressed) input += Vector3.left;
            if (keyboard.dKey.isPressed) input += Vector3.right;

            if (input != Vector3.zero && !_rig.IsKnockedDown)
            {
                Quaternion yaw = Quaternion.Euler(0f, _yaw, 0f);
                Vector3 move = _rig.TrackingSpace.rotation * (yaw * input.normalized) * _rig.Config.DesktopMoveSpeed;
                Vector3 velocity = body.linearVelocity;
                velocity.x = move.x;
                velocity.z = move.z;
                body.linearVelocity = velocity;
            }

            if (keyboard.spaceKey.wasPressedThisFrame)
            {
                Vector3 velocity = body.linearVelocity;
                velocity.y = _rig.Config.DesktopJumpSpeed;
                body.linearVelocity = velocity;
            }
        }
#endif
    }
}
