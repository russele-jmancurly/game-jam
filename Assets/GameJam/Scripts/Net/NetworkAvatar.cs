using Fusion;
using GameJam.Core;
using GameJam.Gameplay;
using GameJam.Player;
using UnityEngine;

namespace GameJam.Net
{
    /// <summary>
    /// A player's networked placeholder body: head, spiky body and two gloves, each synced by its own
    /// NetworkTransform. The owner copies their local rig into it. Knockbacks arrive here: the hit player
    /// gets a straight-line shove (comfortable in VR) and everyone else sees a floppy ragdoll.
    /// </summary>
    public sealed class NetworkAvatar : NetworkBehaviour, IKnockbackRelay
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

        private Renderer[] _renderers;
        private float _hiddenUntil;

        public static NetworkAvatar Local { get; private set; }

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

        /// <summary>Knocks this player over. Anyone can call it: slaps, thrown items, hazards, pop blasts.</summary>
        public void SendKnockback(Vector3 direction, float force, float seconds) => RpcKnockback(direction, force, seconds);

        public override void Spawned()
        {
            _renderers = GetComponentsInChildren<Renderer>(true);
            Tint(PlayerColor);

            if (HasStateAuthority)
            {
                Local = this;
                GameServices.LocalKnockback = this;
                SetVisible(false); // you never see your own avatar; your rig has its own gloves
            }
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            if (Local == this)
            {
                Local = null;
            }

            if (ReferenceEquals(GameServices.LocalKnockback, this))
            {
                GameServices.LocalKnockback = null;
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
            }
            else if (_hiddenUntil > 0f && Time.time >= _hiddenUntil)
            {
                _hiddenUntil = 0f;
                SetVisible(true);
            }
        }

        [Rpc(RpcSources.All, RpcTargets.All)]
        private void RpcKnockback(Vector3 direction, float force, float seconds)
        {
            if (HasStateAuthority)
            {
                if (LocalRig.Instance != null)
                {
                    LocalRig.Instance.ApplyKnockback(direction, force, seconds);
                }

                return;
            }

            RagdollPuppet.Spawn(_head.position, _body.position, direction.normalized * force, PlayerColor, _ragdollMaterial, seconds);
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

        private void SetVisible(bool isVisible)
        {
            if (_renderers == null)
            {
                return;
            }

            foreach (Renderer target in _renderers)
            {
                if (target != null)
                {
                    target.enabled = isVisible;
                }
            }
        }
    }
}
