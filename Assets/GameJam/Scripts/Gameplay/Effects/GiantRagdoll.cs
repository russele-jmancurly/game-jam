using System.Collections.Generic;
using GameJam.Core;
using UnityEngine;

namespace GameJam.Gameplay
{
    /// <summary>
    /// A real ragdoll for a rigged show character, built from its own skeleton the first time it falls (the 19-bone
    /// humanoid from tools/blender/scripts/rig_character.py: Hips > Spine > Chest > Neck > Head, UpperArm/LowerArm/Hand and
    /// UpperLeg/LowerLeg/Foot per side). Ten bones get a Rigidbody, a collider sized to the body and a CharacterJoint
    /// twisting along the bone. The head is a collider on the chest's body, not a body of its own: hinged on its own it
    /// flopped back at a broken-looking angle as the giant fell.
    /// Standing, they are kinematic with colliders off, so the Animator owns the pose. <see cref="Fall"/> hands the bones to
    /// physics; <see cref="GetUp"/> gives them back and blends the lying pose into the animation, so the character gets up
    /// instead of snapping. Every client runs its own (only the knockdown is networked), so the fall differs a little per
    /// screen. Bones sit on <see cref="GameLayers.Ragdoll"/>: they land on the world and nothing else.
    /// </summary>
    public sealed class GiantRagdoll : MonoBehaviour
    {
        private enum Shape { Capsule, Sphere }

        // Bone, the bone its collider reaches to, shape, radius as a share of the body height, mass share (0 = a collider
        // on the parent's body, no joint), joint swing and twist (degrees).
        private static readonly (string bone, string reachTo, Shape shape, float radius, float mass, float swing, float twist)[] Parts =
        {
            ("Hips", "Spine", Shape.Sphere, 0.12f, 0.18f, 0f, 0f),
            ("Chest", "Neck", Shape.Capsule, 0.12f, 0.28f, 20f, 15f),
            ("Head", null, Shape.Sphere, 0.1f, 0f, 0f, 0f),
            ("UpperArm.L", "LowerArm.L", Shape.Capsule, 0.04f, 0.04f, 60f, 30f),
            ("LowerArm.L", "Hand.L", Shape.Capsule, 0.035f, 0.03f, 40f, 15f),
            ("UpperArm.R", "LowerArm.R", Shape.Capsule, 0.04f, 0.04f, 60f, 30f),
            ("LowerArm.R", "Hand.R", Shape.Capsule, 0.035f, 0.03f, 40f, 15f),
            ("UpperLeg.L", "LowerLeg.L", Shape.Capsule, 0.055f, 0.1f, 40f, 15f),
            ("LowerLeg.L", "Foot.L", Shape.Capsule, 0.045f, 0.06f, 35f, 8f),
            ("UpperLeg.R", "LowerLeg.R", Shape.Capsule, 0.055f, 0.1f, 40f, 15f),
            ("LowerLeg.R", "Foot.R", Shape.Capsule, 0.045f, 0.06f, 35f, 8f),
        };

        private const float TotalMass = 200f;

        private readonly List<Rigidbody> _bodies = new List<Rigidbody>();
        private readonly List<float> _flingShares = new List<float>();
        private readonly List<Collider> _colliders = new List<Collider>();
        private Transform[] _skeleton = System.Array.Empty<Transform>();
        private Quaternion[] _fromRotations = System.Array.Empty<Quaternion>();   // lying pose (local), when it gets up
        private Quaternion[] _standRotations = System.Array.Empty<Quaternion>();  // standing pose (local), when it fell
        private Vector3[] _standPositions = System.Array.Empty<Vector3>();
        private Animator _animator;
        private Transform _hips;
        private Transform _head;
        private Vector3 _fromHipsPosition;
        private float _bodyHeight = 4f;
        private float _getUpSeconds = 0.8f;
        private float _getUpStart = -1f;
        private bool _built;

        public bool IsDown { get; private set; }

        /// <summary>True when the model has the skeleton the ragdoll needs.</summary>
        public bool IsValid => _built && _bodies.Count > 0;

        /// <summary>The head bone (follows the ragdoll while down): where the dizzy stars circle.</summary>
        public Transform Head => _head;

        /// <summary>Where the ragdoll's hips are now, on the ground plane of the given height.</summary>
        public Vector3 HipsGroundPoint(float groundY) =>
            _hips != null ? new Vector3(_hips.position.x, groundY, _hips.position.z) : transform.position;

        /// <summary>The ragdoll for this character model, building it on first use.</summary>
        public static GiantRagdoll For(GameObject model, float bodyHeight)
        {
            if (!model.TryGetComponent(out GiantRagdoll ragdoll))
            {
                ragdoll = model.AddComponent<GiantRagdoll>();
            }

            ragdoll._bodyHeight = bodyHeight;
            ragdoll.Build();
            return ragdoll;
        }

        /// <summary>Drops the character: physics takes the bones, flung along the hit.</summary>
        public void Fall(Vector3 fling)
        {
            if (!IsValid)
            {
                return;
            }

            IsDown = true;
            _getUpStart = -1f;
            for (int i = 0; i < _skeleton.Length; i++)
            {
                _standRotations[i] = _skeleton[i].localRotation;
                _standPositions[i] = _skeleton[i].localPosition;
            }

            if (_animator != null)
            {
                _animator.enabled = false; // the PlayableGraph stops writing the pose
            }

            foreach (Collider part in _colliders)
            {
                part.enabled = true;
            }

            foreach (Rigidbody body in _bodies)
            {
                body.isKinematic = false;
                body.interpolation = RigidbodyInterpolation.Interpolate;
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }

            // The top of the body takes the hit and the legs lag behind, so it topples instead of sliding.
            for (int i = 0; i < _bodies.Count; i++)
            {
                _bodies[i].AddForce(fling * _flingShares[i] + Random.insideUnitSphere * 0.4f, ForceMode.VelocityChange);
            }
        }

        /// <summary>Hands the bones back to the Animator, blending from where they lie into the animation.</summary>
        public void GetUp(float seconds)
        {
            if (!IsValid || !IsDown)
            {
                return;
            }

            IsDown = false;
            for (int i = 0; i < _skeleton.Length; i++)
            {
                _fromRotations[i] = _skeleton[i].localRotation;
            }

            _fromHipsPosition = _hips.position;
            foreach (Rigidbody body in _bodies)
            {
                body.interpolation = RigidbodyInterpolation.None; // kinematic and animated: interpolation would fight the Animator
                body.isKinematic = true;
            }

            foreach (Collider part in _colliders)
            {
                part.enabled = false;
            }

            if (_animator != null)
            {
                _animator.enabled = true;
            }

            _getUpSeconds = Mathf.Max(seconds, 0.05f);
            _getUpStart = Time.time;
        }

        /// <summary>
        /// While getting up, puts every bone back in its standing pose before the Animator runs. The clips don't drive every
        /// bone (the neck, for one): without this those kept their lying angle and the head stayed bent back after it stood up.
        /// </summary>
        private void Update()
        {
            if (_getUpStart < 0f)
            {
                return;
            }

            for (int i = 0; i < _skeleton.Length; i++)
            {
                _skeleton[i].localRotation = _standRotations[i];
                _skeleton[i].localPosition = _standPositions[i];
            }
        }

        private void LateUpdate()
        {
            if (_getUpStart < 0f)
            {
                return;
            }

            // The Animator has just written this frame's pose: lean it toward the lying pose, less every frame.
            float t = Mathf.Clamp01((Time.time - _getUpStart) / _getUpSeconds);
            float weight = 1f - t * t * (3f - 2f * t);
            if (t >= 1f)
            {
                _getUpStart = -1f;
                return;
            }

            _hips.position = Vector3.Lerp(_hips.position, _fromHipsPosition, weight);
            for (int i = 0; i < _skeleton.Length; i++)
            {
                _skeleton[i].localRotation = Quaternion.Slerp(_skeleton[i].localRotation, _fromRotations[i], weight);
            }
        }

        private void Build()
        {
            if (_built)
            {
                return;
            }

            _built = true;
            _animator = GetComponent<Animator>();
            var bones = new Dictionary<string, Transform>();
            foreach (Transform child in GetComponentsInChildren<Transform>(true))
            {
                bones[child.name] = child;
            }

            if (!bones.TryGetValue("Hips", out _hips))
            {
                return; // not one of our rigs: the caller falls back to tipping the model over
            }

            bones.TryGetValue("Head", out _head);
            var skeleton = new List<Transform>();
            foreach (Transform bone in _hips.GetComponentsInChildren<Transform>(true))
            {
                skeleton.Add(bone);
            }

            _skeleton = skeleton.ToArray();
            _fromRotations = new Quaternion[_skeleton.Length];
            _standRotations = new Quaternion[_skeleton.Length];
            _standPositions = new Vector3[_skeleton.Length];

            foreach (var part in Parts)
            {
                if (!bones.TryGetValue(part.bone, out Transform bone))
                {
                    Debug.LogWarning($"[Ragdoll] {name} has no '{part.bone}' bone: no ragdoll.");
                    Clear();
                    return;
                }

                Transform reachTo = part.reachTo != null && bones.TryGetValue(part.reachTo, out Transform end) ? end : null;
                AddPart(bone, reachTo, part.shape, part.radius, part.mass, part.swing, part.twist);
                if (part.mass > 0f)
                {
                    // The top of the body takes the hit and the legs lag behind, so it topples instead of sliding.
                    _flingShares.Add(part.bone.Contains("Leg") ? 0.45f : part.bone == "Hips" ? 0.7f : 1.15f);
                }
            }
        }

        private void AddPart(Transform bone, Transform reachTo, Shape shape, float radiusShare, float massShare, float swing, float twist)
        {
            float scale = Mathf.Max(bone.lossyScale.x, 0.0001f);
            float radius = radiusShare * _bodyHeight / scale; // collider sizes are in the bone's (scaled) local space
            bone.gameObject.layer = GameLayers.Ragdoll;

            Collider collider;
            if (shape == Shape.Capsule && reachTo != null)
            {
                Vector3 end = bone.InverseTransformPoint(reachTo.position);
                var capsule = bone.gameObject.AddComponent<CapsuleCollider>();
                capsule.direction = LongestAxis(end);
                capsule.center = end * 0.5f;
                capsule.radius = radius;
                capsule.height = end.magnitude + radius; // overlaps the next part a little: no gaps at the joints
                collider = capsule;
            }
            else
            {
                var sphere = bone.gameObject.AddComponent<SphereCollider>();
                sphere.radius = radius;
                // The head sphere sits on the skull, not on the neck joint the bone starts at.
                sphere.center = reachTo != null ? bone.InverseTransformPoint(reachTo.position) * 0.5f
                    : bone == _head ? Vector3.up * radius * 0.8f : Vector3.zero;
                collider = sphere;
            }

            collider.enabled = false;
            _colliders.Add(collider);
            if (massShare <= 0f)
            {
                return; // rides on the nearest parent body (the head on the chest)
            }

            var body = bone.gameObject.AddComponent<Rigidbody>();
            body.mass = TotalMass * massShare;
            body.isKinematic = true;
            body.interpolation = RigidbodyInterpolation.None;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            body.solverIterations = 12;
            body.linearDamping = 0.05f;
            body.angularDamping = 0.6f;
            _bodies.Add(body);

            Rigidbody parentBody = bone.parent != null ? bone.parent.GetComponentInParent<Rigidbody>() : null;
            if (parentBody == null || swing <= 0f)
            {
                return;
            }

            // Twist around the bone's own length (where its collider points), swing in the two directions across it.
            Vector3 along = reachTo != null ? bone.InverseTransformPoint(reachTo.position) : Vector3.up;
            Vector3 twistAxis = AxisVector(LongestAxis(along)) * Mathf.Sign(along[LongestAxis(along)]);
            var joint = bone.gameObject.AddComponent<CharacterJoint>();
            joint.connectedBody = parentBody;
            joint.axis = twistAxis;
            joint.swingAxis = AxisVector((LongestAxis(along) + 1) % 3);
            joint.enableProjection = true;
            joint.enablePreprocessing = false;
            joint.lowTwistLimit = new SoftJointLimit { limit = -twist };
            joint.highTwistLimit = new SoftJointLimit { limit = twist };
            joint.swing1Limit = new SoftJointLimit { limit = swing };
            joint.swing2Limit = new SoftJointLimit { limit = swing * 0.6f };
        }

        private void Clear()
        {
            foreach (Collider part in _colliders)
            {
                Destroy(part);
            }

            foreach (Rigidbody body in _bodies)
            {
                if (body.TryGetComponent(out CharacterJoint joint))
                {
                    Destroy(joint);
                }

                Destroy(body);
            }

            _colliders.Clear();
            _bodies.Clear();
            _flingShares.Clear();
        }

        private static Vector3 AxisVector(int axis) => axis == 0 ? Vector3.right : axis == 1 ? Vector3.up : Vector3.forward;

        private static int LongestAxis(Vector3 v)
        {
            float x = Mathf.Abs(v.x);
            float y = Mathf.Abs(v.y);
            float z = Mathf.Abs(v.z);
            return x > y && x > z ? 0 : y > z ? 1 : 2;
        }
    }
}
