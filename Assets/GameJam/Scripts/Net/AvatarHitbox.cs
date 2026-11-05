using GameJam.Core;
using UnityEngine;

namespace GameJam.Net
{
    /// <summary>
    /// A spiky part of a player (head or body). The balloon pops or leaks when it touches one, and a hard
    /// slap or a thrown item knocks that player over.
    /// </summary>
    public sealed class AvatarHitbox : MonoBehaviour, ISlappable, ISpikeSurface
    {
        private static readonly HapticPulse SlapFeedback = new HapticPulse(0.9f, 0.12f);

        [SerializeField] private NetworkAvatar _avatar;
        [SerializeField] private SlapConfig _slapConfig;

        public NetworkAvatar Avatar => _avatar;

        public void Configure(NetworkAvatar avatar, SlapConfig slapConfig)
        {
            _avatar = avatar;
            _slapConfig = slapConfig;
        }

        public bool TryReceiveSlap(in SlapContext slap, out HapticPulse feedback)
        {
            feedback = HapticPulse.None;
            if (_avatar == null || _avatar.IsLocal)
            {
                return false; // never slap yourself
            }

            float speed = slap.Speed;
            if (speed < _slapConfig.FriendSlapSpeed)
            {
                return false;
            }

            float force = Mathf.Min(speed * _slapConfig.FriendForceScale, _slapConfig.FriendMaxForce);
            _avatar.SendKnockback(slap.HandVelocity.normalized, force, _slapConfig.FriendKnockdownSeconds);
            feedback = SlapFeedback;
            return true;
        }
    }
}
