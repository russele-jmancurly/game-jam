using UnityEngine;

namespace GameJam.Gameplay
{
    /// <summary>
    /// Placeholder ragdoll made of primitives and joints. Spawned locally on every other player's headset when someone is
    /// knocked over (only the knockback travels over the network, not the bones), so it looks slightly different on each
    /// screen. It inherits the player's motion, flops, then springs back toward where the player really is, so the
    /// hand-back to the live avatar doesn't snap. Swap in the real Jman ragdoll when the models arrive.
    /// </summary>
    public sealed class RagdollPuppet : MonoBehaviour
    {
        private const float MinLifetime = 0.5f;
        private const float RandomImpulse = 0.5f;
        private const float RandomSpin = 6f;
        // Share of the lifetime after which the puppet is pulled back toward the real player.
        private const float RecoverStart = 0.55f;
        private const float RecoverSpring = 60f;
        private const float RecoverDamping = 10f;
        private const float ShrinkSeconds = 0.12f;

        private Rigidbody _torso;
        private Transform[] _parts;
        private Vector3[] _partScales;
        private Transform _followTarget;
        private float _spawnTime;
        private float _lifetime;

        /// <param name="followTarget">Where the player's body really is (their synced avatar); the puppet recovers toward it.</param>
        /// <param name="inheritedVelocity">The player's motion at the moment of the hit, so the flop continues it.</param>
        public static RagdollPuppet Spawn(
            Vector3 headPosition, Vector3 bodyPosition, Vector3 impulse, Vector3 inheritedVelocity, Color color, Material material,
            float lifetime, Transform followTarget)
        {
            var root = new GameObject(nameof(RagdollPuppet));
            var puppet = root.AddComponent<RagdollPuppet>();
            puppet._lifetime = Mathf.Max(MinLifetime, lifetime);
            puppet._spawnTime = Time.time;
            puppet._followTarget = followTarget;
            Destroy(root, puppet._lifetime);

            Transform parent = root.transform;
            Rigidbody torso = PlaceholderPrimitives.CreatePart(parent, PrimitiveType.Capsule, bodyPosition, new Vector3(0.4f, 0.3f, 0.35f), material, color, 6f);
            Rigidbody head = PlaceholderPrimitives.CreatePart(parent, PrimitiveType.Sphere, headPosition, Vector3.one * 0.28f, material, color, 2f);
            Rigidbody leftArm = PlaceholderPrimitives.CreatePart(parent, PrimitiveType.Capsule, bodyPosition + new Vector3(-0.3f, 0.1f, 0f), new Vector3(0.12f, 0.22f, 0.12f), material, Color.white, 1f);
            Rigidbody rightArm = PlaceholderPrimitives.CreatePart(parent, PrimitiveType.Capsule, bodyPosition + new Vector3(0.3f, 0.1f, 0f), new Vector3(0.12f, 0.22f, 0.12f), material, Color.white, 1f);
            Rigidbody leftLeg = PlaceholderPrimitives.CreatePart(parent, PrimitiveType.Capsule, bodyPosition + new Vector3(-0.12f, -0.5f, 0f), new Vector3(0.15f, 0.25f, 0.15f), material, color, 2f);
            Rigidbody rightLeg = PlaceholderPrimitives.CreatePart(parent, PrimitiveType.Capsule, bodyPosition + new Vector3(0.12f, -0.5f, 0f), new Vector3(0.15f, 0.25f, 0.15f), material, color, 2f);
            puppet._torso = torso;

            Attach(head, torso);
            Attach(leftArm, torso);
            Attach(rightArm, torso);
            Attach(leftLeg, torso);
            Attach(rightLeg, torso);

            foreach (Rigidbody part in root.GetComponentsInChildren<Rigidbody>())
            {
                part.linearVelocity = inheritedVelocity;
                part.AddForce(impulse + Random.insideUnitSphere * RandomImpulse, ForceMode.VelocityChange);
            }

            torso.AddTorque(Random.onUnitSphere * RandomSpin, ForceMode.VelocityChange);

            Rigidbody[] bodies = root.GetComponentsInChildren<Rigidbody>();
            puppet._parts = new Transform[bodies.Length];
            puppet._partScales = new Vector3[bodies.Length];
            for (int i = 0; i < bodies.Length; i++)
            {
                puppet._parts[i] = bodies[i].transform;
                puppet._partScales[i] = bodies[i].transform.localScale;
            }

            return puppet;
        }

        private void FixedUpdate()
        {
            if (_torso == null || _followTarget == null)
            {
                return;
            }

            float progress = (Time.time - _spawnTime) / _lifetime;
            if (progress < RecoverStart)
            {
                return;
            }

            // Critically-damped-ish spring toward the real body: the flop ends where the player actually is.
            float strength = Mathf.InverseLerp(RecoverStart, 1f, progress);
            Vector3 toTarget = _followTarget.position - _torso.position;
            Vector3 acceleration = (toTarget * RecoverSpring - _torso.linearVelocity * RecoverDamping) * strength;
            _torso.AddForce(acceleration, ForceMode.Acceleration);
        }

        private void LateUpdate()
        {
            float remaining = _spawnTime + _lifetime - Time.time;
            if (remaining >= ShrinkSeconds || _parts == null)
            {
                return;
            }

            // Shrink each part in place in the last frames instead of vanishing, as the live avatar reappears.
            float scale = Mathf.Max(0.01f, remaining / ShrinkSeconds);
            for (int i = 0; i < _parts.Length; i++)
            {
                if (_parts[i] != null)
                {
                    _parts[i].localScale = _partScales[i] * scale;
                }
            }
        }

        private static void Attach(Rigidbody child, Rigidbody parent)
        {
            var joint = child.gameObject.AddComponent<CharacterJoint>();
            joint.connectedBody = parent;
            joint.enableProjection = true;
        }
    }
}
