using UnityEngine;

namespace GameJam.Core
{
    /// <summary>What a glove knows when it touches something.</summary>
    public readonly struct SlapContext
    {
        public readonly Vector3 HandVelocity;
        public readonly Vector3 HandPosition;

        public SlapContext(Vector3 handVelocity, Vector3 handPosition)
        {
            HandVelocity = handVelocity;
            HandPosition = handPosition;
        }

        public float Speed => HandVelocity.magnitude;
    }
}
