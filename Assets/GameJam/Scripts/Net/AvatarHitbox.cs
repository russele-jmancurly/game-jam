using GameJam.Core;
using UnityEngine;

namespace GameJam.Net
{
    /// <summary>
    /// A spiky part of a player (head or body). The balloon pops or leaks when it touches one, and a hard
    /// slap or a thrown item gives that player a light shove (players never knock each other down).
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

            _avatar.SendShove(slap.HandVelocity.normalized);

            feedback = SlapFeedback;
            return true;
        }
    }
}
