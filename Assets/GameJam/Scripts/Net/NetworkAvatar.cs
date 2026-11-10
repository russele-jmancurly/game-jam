using Fusion;
using GameJam.Core;
using GameJam.Gameplay;
using GameJam.Player;
using UnityEngine;

namespace GameJam.Net
{
    /// <summary>
    /// A player's networked body: head, body and two gloves, each synced by its own NetworkTransform. Every player is the
    /// same porcupine (a skinned gorilla-locomotion body, <see cref="GorillaBodyRig"/>, in natural colours) whose arms reach
    /// the synced gloves; a name tag in the player's colour tells the crew apart. You see your own arms, everyone else
    /// sees all of you. Knockbacks arrive here: the hit player gets a straight-line shove (comfortable in VR) and everyone
    /// else sees a floppy ragdoll.
    /// Runs after gorilla locomotion (LateUpdate, order 0) so the arms meet the hands where they were drawn this frame.
    /// </summary>
    [DefaultExecutionOrder(100)]
    public sealed class NetworkAvatar : NetworkBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [SerializeField] private Transform _head;
        [SerializeField] private Transform _body;
        [SerializeField] private Transform _leftHand;
        [SerializeField] private Transform _rightHand;
        [SerializeField] private Renderer[] _tintRenderers;
        [SerializeField] private Material _ragdollMaterial;
        [Tooltip("The body sits this far below the head.")]
        [SerializeField] private float _bodyDrop = 0.45f;
        [SerializeField] private TextMesh _nameTag;
        [Tooltip("The porcupine body; without it only the gloves show.")]
        [SerializeField] private GorillaBodyRig _bodyRig;

        private float _hiddenUntil;
        private int _ownArmsShown = -1;
        private bool _isVisible = true;
        private Vector3 _lastBodyPosition;
        private Vector3 _bodyVelocity;

        /// <summary>How closed each paw is (0 open .. 255 fist), written by the owner so everyone sees the hands close.</summary>
        [Networked] private byte LeftGrip { get; set; }

        [Networked] private byte RightGrip { get; set; }

        public static NetworkAvatar Local { get; private set; }

        /// <summary>Every player's avatar in the session (the giants look for players to kick here).</summary>
        public static readonly System.Collections.Generic.List<NetworkAvatar> All = new System.Collections.Generic.List<NetworkAvatar>();

        /// <summary>The player's body (on other clients, where their synced body is now).</summary>
        public Vector3 BodyPosition => _body.position;

        /// <summary>
        /// Lying in a ragdoll right now: the local player while knocked down, anyone else while their ragdoll is shown here.
        /// </summary>
        public bool IsDown => IsLocal ? LocalRig.Instance != null && LocalRig.Instance.IsKnockedDown : _hiddenUntil > 0f;

        /// <summary>The player who owns this avatar.</summary>
        public PlayerRef Owner => Object != null && Object.IsValid ? Object.StateAuthority : PlayerRef.None;

        public bool IsLocal => Object != null && Object.IsValid && HasStateAuthority;

        public Color PlayerColor => Object.StateAuthority.ToPaletteColor();

        public void Configure(Transform head, Transform body, Transform leftHand, Transform rightHand, Renderer[] tintRenderers, Material ragdollMaterial)
        {
            _head = head;
            _body = body;
            _leftHand = leftHand;
            _rightHand = rightHand;
            _tintRenderers = tintRenderers;
            _ragdollMaterial = ragdollMaterial;
        }

        public void ConfigureBody(GorillaBodyRig bodyRig, TextMesh nameTag)
        {
            _bodyRig = bodyRig;
            _nameTag = nameTag;
        }

        /// <summary>Another player's slap or item hit this player: a light shove, never a knockdown.</summary>
        public void SendShove(Vector3 direction) => RpcShove(direction);

        /// <summary>
        /// A giant kicked this player: the only knockdown in the game (ragdoll for everyone else, flung with hands off and
        /// items dropped for the player). Everything else is a shove.
        /// </summary>
        public void SendGiantKick(Vector3 direction, float force, float seconds) => RpcGiantKick(direction, force, seconds);

        public override void Spawned()
        {
            if (!All.Contains(this))
            {
                All.Add(this);
            }

            Tint(PlayerColor);
            _lastBodyPosition = _body.position;
            if (_nameTag != null)
            {
                _nameTag.text = $"Player {Mathf.Max(1, Object.StateAuthority.PlayerId)}";
                _nameTag.color = PlayerColor;
            }

            if (HasStateAuthority)
            {
                Local = this;
                SetVisible(false); // you never see your own body; ShowOwnArms brings back just the arms
                if (_bodyRig != null)
                {
                    _bodyRig.IsLocalPlayer = true; // the stage mirror shows it
                }
            }
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            All.Remove(this);
            if (Local == this)
            {
                Local = null;
            }

        }

        public override void FixedUpdateNetwork()
        {
            if (HasStateAuthority)
            {
                FollowRig();
            }
        }

        private void LateUpdate()
        {
            if (Object == null || !Object.IsValid)
            {
                return;
            }

            if (HasStateAuthority)
            {
                FollowRig();
                ShowOwnArms();
                if (_bodyRig != null)
                {
                    LocalRig rig = LocalRig.Instance;
                    if (rig != null)
                    {
                        _bodyRig.SetGrip(rig.GripAmount(false), rig.GripAmount(true)); // your own paws: no network delay
                    }

                    _bodyRig.Solve();
                }

                return;
            }

            if (_bodyRig != null && _isVisible)
            {
                _bodyRig.SetGrip(LeftGrip / 255f, RightGrip / 255f);
                _bodyRig.Solve();
            }

            // Smoothed body motion of this remote player, handed to the ragdoll so a knockback continues their momentum.
            float dt = Mathf.Max(Time.deltaTime, 0.0001f);
            Vector3 position = _body.position;
            _bodyVelocity = Vector3.Lerp(_bodyVelocity, (position - _lastBodyPosition) / dt, 0.3f);
            _lastBodyPosition = position;

            if (_hiddenUntil > 0f && Time.time >= _hiddenUntil)
            {
                _hiddenUntil = 0f;
                SetVisible(true);
            }

            Camera view = Camera.main;
            if (_nameTag != null && view != null)
            {
                _nameTag.transform.rotation = Quaternion.LookRotation(_nameTag.transform.position - view.transform.position);
            }
        }

        /// <summary>
        /// Not tick-aligned (Fusion RpcAttribute.TickAligned = false): a shove is a one-off reaction, so it plays as soon as
        /// it arrives instead of waiting for the local simulation to reach the sender's tick. Only the shoved player
        /// moves (a little); nobody sees a ragdoll.
        /// </summary>
        [Rpc(RpcSources.All, RpcTargets.StateAuthority, TickAligned = false)]
        private void RpcShove(Vector3 direction)
        {
            LocalRig rig = LocalRig.Instance;
            if (rig != null)
            {
                rig.Push(direction, rig.KnockbackConfig.PlayerPushForce);
            }
        }

        /// <summary>Not tick-aligned, like RpcShove. A giant's kick: the one thing that ragdolls a player.</summary>
        [Rpc(RpcSources.All, RpcTargets.All, TickAligned = false)]
        private void RpcGiantKick(Vector3 direction, float force, float seconds)
        {
            Knockdown(direction, force, seconds);
        }

        /// <summary>The kicked player is flung (ApplyKnockback); everyone else sees a floppy ragdoll in their place.</summary>
        private void Knockdown(Vector3 direction, float force, float seconds)
        {
            if (HasStateAuthority)
            {
                if (LocalRig.Instance != null)
                {
                    LocalRig.Instance.ApplyKnockback(direction, force, seconds);
                }

                return;
            }

            RagdollPuppet.Spawn(_head.position, _body.position, direction.normalized * force, _bodyVelocity, PlayerColor, _ragdollMaterial, seconds, _body);
            SetVisible(false);
            _hiddenUntil = Time.time + seconds;
        }

        private void FollowRig()
        {
            LocalRig rig = LocalRig.Instance;
            if (rig == null)
            {
                return;
            }

            Transform head = rig.Head;
            _head.SetPositionAndRotation(head.position, head.rotation);
            _leftHand.SetPositionAndRotation(rig.LeftHandFollower.position, rig.LeftHandFollower.rotation);
            _rightHand.SetPositionAndRotation(rig.RightHandFollower.position, rig.RightHandFollower.rotation);

            Quaternion yaw = Quaternion.Euler(0f, head.eulerAngles.y, 0f);
            _body.SetPositionAndRotation(head.position + Vector3.down * _bodyDrop, yaw);

            if (Object != null && Object.IsValid && HasStateAuthority)
            {
                LeftGrip = (byte)Mathf.RoundToInt(rig.GripAmount(false) * 255f);
                RightGrip = (byte)Mathf.RoundToInt(rig.GripAmount(true) * 255f);
            }
        }

        /// <summary>
        /// Your own avatar stays hidden except, with PlayerRigConfig.ShowOwnArms, the gorilla arms, whose mitts then
        /// replace the rig's glove spheres. Applied only when the setting changes, so it can be toggled live.
        /// </summary>
        private void ShowOwnArms()
        {
            LocalRig rig = LocalRig.Instance;
            if (_bodyRig == null || rig == null || rig.Config == null)
            {
                return;
            }

            int show = rig.Config.ShowOwnArms ? 1 : 0;
            if (show == _ownArmsShown)
            {
                return;
            }

            _ownArmsShown = show;
            _bodyRig.SetPartsVisible(false, show == 1);
            rig.SetGloveVisualsVisible(show == 0);
        }

        private void Tint(Color color)
        {
            if (_tintRenderers == null)
            {
                return;
            }

            var block = new MaterialPropertyBlock();
            block.SetColor(BaseColorId, color);
            foreach (Renderer target in _tintRenderers)
            {
                if (target != null)
                {
                    target.SetPropertyBlock(block);
                }
            }
        }

        /// <summary>Re-gathers renderers every call, so a model swapped in later is hidden or shown too.</summary>
        private void SetVisible(bool isVisible)
        {
            _isVisible = isVisible;
            foreach (Renderer target in GetComponentsInChildren<Renderer>(true))
            {
                if (target != null)
                {
                    target.enabled = isVisible;
                }
            }
        }
    }
}
