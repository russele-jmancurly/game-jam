using Fusion;
using GameJam.Core;
using GameJam.Gameplay;
using GameJam.Player;
using UnityEngine;

namespace GameJam.Net
{
    /// <summary>
    /// A player's networked body: head, body and two gloves, each synced by its own NetworkTransform, wearing one of the
    /// roster's characters (Jmancurly, Squoof, Protobro, Elliot). The owner copies their local rig into it and chooses the
    /// character (default by player slot, changeable at the start-stage pedestals). Knockbacks arrive here: the hit player
    /// gets a straight-line shove (comfortable in VR) and everyone else sees a floppy ragdoll.
    /// </summary>
    public sealed class NetworkAvatar : NetworkBehaviour, IKnockbackRelay, ICharacterSelector
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

        [Header("Character")]
        [SerializeField] private CharacterRoster _roster;
        [SerializeField] private CharacterVisual _characterVisual;
        [SerializeField] private TextMesh _nameTag;
        [Tooltip("The model's top sits this far above the head, so the camera eye lines up with the model's face.")]
        [SerializeField] private float _modelTopAboveHead = 0.12f;

        private float _hiddenUntil;
        private bool _isVisible = true;
        private int _shownCharacter = -1;
        private Vector3 _lastBodyPosition;
        private Vector3 _bodyVelocity;

        public static NetworkAvatar Local { get; private set; }

        /// <summary>Index into the roster; written only by this player.</summary>
        [Networked] public int CharacterIndex { get; private set; }

        public bool IsLocal => Object != null && Object.IsValid && HasStateAuthority;

        public Color PlayerColor => Object.StateAuthority.ToPaletteColor();

        public int CurrentCharacter => CharacterIndex;

        public void Configure(Transform head, Transform body, Transform leftHand, Transform rightHand, Renderer[] tintRenderers, Material ragdollMaterial)
        {
            _head = head;
            _body = body;
            _leftHand = leftHand;
            _rightHand = rightHand;
            _tintRenderers = tintRenderers;
            _ragdollMaterial = ragdollMaterial;
        }

        public void ConfigureCharacter(CharacterRoster roster, CharacterVisual characterVisual, TextMesh nameTag)
        {
            _roster = roster;
            _characterVisual = characterVisual;
            _nameTag = nameTag;
        }

        /// <summary>Knocks this player over. Anyone can call it: slaps, thrown items, hazards, pop blasts.</summary>
        public void SendKnockback(Vector3 direction, float force, float seconds) => RpcKnockback(direction, force, seconds);

        /// <summary>Called by the start-stage pedestals on the local player's own avatar.</summary>
        public void SelectCharacter(int rosterIndex)
        {
            if (HasStateAuthority && _roster != null && _roster.Count > 0)
            {
                CharacterIndex = ((rosterIndex % _roster.Count) + _roster.Count) % _roster.Count;
            }
        }

        public override void Spawned()
        {
            Tint(PlayerColor);
            _lastBodyPosition = _body.position;

            if (HasStateAuthority)
            {
                Local = this;
                GameServices.LocalKnockback = this;
                GameServices.LocalCharacter = this;

                // Default: a different character per player slot, so a full crew is all four.
                int count = _roster != null ? _roster.Count : 0;
                CharacterIndex = count > 0 ? Mathf.Max(0, Object.StateAuthority.PlayerId - 1) % count : 0;
                SetVisible(false); // you never see your own avatar; your rig has its own gloves
            }

            ApplyCharacter();
        }

        public override void Render()
        {
            if (CharacterIndex != _shownCharacter)
            {
                ApplyCharacter();
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

            if (ReferenceEquals(GameServices.LocalCharacter, this))
            {
                GameServices.LocalCharacter = null;
            }
        }

        /// <summary>Swaps the visible model to the networked character choice and updates the name tag.</summary>
        private void ApplyCharacter()
        {
            _shownCharacter = CharacterIndex;
            CharacterDefinition character = _roster != null ? _roster.Get(CharacterIndex) : null;
            if (character == null || _characterVisual == null)
            {
                return;
            }

            // Feet so the model's top sits just above the head: the camera eye lines up with the character's face.
            float height = character.Height;
            _characterVisual.transform.localPosition = new Vector3(0f, _bodyDrop + _modelTopAboveHead - height, 0f);
            _characterVisual.Show(character, height);
            _characterVisual.SetVisible(_isVisible);

            if (_nameTag != null)
            {
                _nameTag.text = character.DisplayName;
                _nameTag.color = PlayerColor;
                _nameTag.GetComponent<Renderer>().enabled = _isVisible;
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
                return;
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
        /// Not tick-aligned (Fusion RpcAttribute.TickAligned = false): a knockback is a one-off reaction, so it plays as
        /// soon as it arrives instead of waiting for the local simulation to reach the sender's tick.
        /// </summary>
        [Rpc(RpcSources.All, RpcTargets.All, TickAligned = false)]
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
