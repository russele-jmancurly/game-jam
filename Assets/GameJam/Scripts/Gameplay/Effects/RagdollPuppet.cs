using UnityEngine;

namespace GameJam.Gameplay
{
    /// <summary>
    /// Placeholder ragdoll made of primitives and joints. Spawned locally on every other player's headset
    /// when someone is knocked over (only the knockback travels over the network, not the bones), so it
    /// looks slightly different on each screen. Swap in the real Jman ragdoll when the models arrive.
    /// </summary>
    public sealed class RagdollPuppet : MonoBehaviour
    {
        private const float MinLifetime = 0.5f;
        private const float RandomImpulse = 0.5f;
        private const float RandomSpin = 6f;

        public static void Spawn(Vector3 headPosition, Vector3 bodyPosition, Vector3 impulse, Color color, Material material, float lifetime)
        {
            var root = new GameObject(nameof(RagdollPuppet));
            root.AddComponent<RagdollPuppet>();
            Destroy(root, Mathf.Max(MinLifetime, lifetime));

            Transform parent = root.transform;
            Rigidbody torso = PlaceholderPrimitives.CreatePart(parent, PrimitiveType.Capsule, bodyPosition, new Vector3(0.4f, 0.3f, 0.35f), material, color, 6f);
            Rigidbody head = PlaceholderPrimitives.CreatePart(parent, PrimitiveType.Sphere, headPosition, Vector3.one * 0.28f, material, color, 2f);
            Rigidbody leftArm = PlaceholderPrimitives.CreatePart(parent, PrimitiveType.Capsule, bodyPosition + new Vector3(-0.3f, 0.1f, 0f), new Vector3(0.12f, 0.22f, 0.12f), material, Color.white, 1f);
            Rigidbody rightArm = PlaceholderPrimitives.CreatePart(parent, PrimitiveType.Capsule, bodyPosition + new Vector3(0.3f, 0.1f, 0f), new Vector3(0.12f, 0.22f, 0.12f), material, Color.white, 1f);
            Rigidbody leftLeg = PlaceholderPrimitives.CreatePart(parent, PrimitiveType.Capsule, bodyPosition + new Vector3(-0.12f, -0.5f, 0f), new Vector3(0.15f, 0.25f, 0.15f), material, color, 2f);
            Rigidbody rightLeg = PlaceholderPrimitives.CreatePart(parent, PrimitiveType.Capsule, bodyPosition + new Vector3(0.12f, -0.5f, 0f), new Vector3(0.15f, 0.25f, 0.15f), material, color, 2f);

            Attach(head, torso);
            Attach(leftArm, torso);
            Attach(rightArm, torso);
            Attach(leftLeg, torso);
            Attach(rightLeg, torso);

            foreach (Rigidbody part in root.GetComponentsInChildren<Rigidbody>())
            {
                part.AddForce(impulse + Random.insideUnitSphere * RandomImpulse, ForceMode.VelocityChange);
            }

            torso.AddTorque(Random.onUnitSphere * RandomSpin, ForceMode.VelocityChange);
        }

        private static void Attach(Rigidbody child, Rigidbody parent)
        {
            var joint = child.gameObject.AddComponent<CharacterJoint>();
            joint.connectedBody = parent;
            joint.enableProjection = true;
        }
    }
}
