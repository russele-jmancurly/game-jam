using System;
using UnityEngine;

namespace GameJam.Gameplay
{
    /// <summary>
    /// Poses the skinned porcupine player body (Art/Characters/PorcupineBody.fbx, built by
    /// tools/blender/scripts/build_porcupine_body.py) from three tracked points: the head turns with the player's look and
    /// each arm reaches its glove with a two-bone IK from the shoulder, stretching a little past full reach like a
    /// cartoon arm. This transform is the model root: place it at the head with the body's yaw. Purely visual and
    /// computed locally on every client from the already-synced head and hands, so it costs no network traffic.
    /// The owner calls <see cref="Solve"/> after the targets have moved this frame.
    /// </summary>
    public sealed class GorillaBodyRig : MonoBehaviour
    {
        [Serializable]
        public sealed class Arm
        {
            public Transform UpperArm;
            public Transform Forearm;
            public Transform Hand;
            [Tooltip("Non-deforming bone where the tracked hand (controller / glove centre) sits.")]
            public Transform GloveCenter;
            public Transform Target;
            [Tooltip("Finger chains, three bones each (Index, Middle, Ring), curled toward the palm by the grip.")]
            public Transform[] Fingers = Array.Empty<Transform>();
            [Tooltip("Thumb chain (Thumb1, Thumb2), folded over the fingers by the grip.")]
            public Transform[] Thumb = Array.Empty<Transform>();

            [NonSerialized] public Quaternion[] FingerRest;
            [NonSerialized] public Vector3[] FingerAxis;
            [NonSerialized] public Quaternion[] ThumbRest;
            [NonSerialized] public Vector3[] ThumbAxis;
            [NonSerialized] public float Grip;
            [NonSerialized] public float GripTarget;
            [NonSerialized] public float AppliedGrip = -1f;
            [NonSerialized] public Quaternion UpperRest;
            [NonSerialized] public Quaternion ForearmRest;
            [NonSerialized] public Vector3 ForearmRestPosition;
            [NonSerialized] public Vector3 HandRestPosition;
            [NonSerialized] public Quaternion HandFromRoot;
            [NonSerialized] public Vector3 GloveFromWrist;
            [NonSerialized] public bool IsReady;
        }

        [SerializeField] private Transform _head;
        [SerializeField] private Transform _headTarget;
        [SerializeField] private Arm _left = new Arm();
        [SerializeField] private Arm _right = new Arm();
        [SerializeField] private Renderer _bodyRenderer;
        [SerializeField] private Renderer _armsRenderer;

        [Tooltip("Which way the elbows bend, in body space for the right arm (x is mirrored for the left): out, down, back.")]
        [SerializeField] private Vector3 _elbowHint = new Vector3(0.45f, -1f, -0.25f);
        [Tooltip("How far an arm may stretch past its length to keep the glove attached (1 = never).")]
        [Range(1f, 2f)] [SerializeField] private float _maxStretch = 1.35f;
        [Tooltip("Head pitch/roll is limited to this many degrees away from upright, so the neck never folds.")]
        [Range(0f, 90f)] [SerializeField] private float _maxHeadTilt = 70f;
        [Tooltip("Closed fist: degrees each finger joint bends toward the palm (root, middle, tip). render_paw.py previews it.")]
        [SerializeField] private Vector3 _fingerCurl = new Vector3(60f, 70f, 50f);
        [Tooltip("Closed fist: degrees the thumb joints fold over the fingers (root, tip).")]
        [SerializeField] private Vector2 _thumbCurl = new Vector2(30f, 40f);
        [Tooltip("How fast the paws open and close (fraction of a full fist per second, eased).")]
        [Min(0f)] [SerializeField] private float _gripSpeed = 14f;

        private Quaternion _headFromRoot = Quaternion.identity;
        private bool _isCached;

        public Renderer BodyRenderer => _bodyRenderer;

        /// <summary>This is the local player's own body (hidden from their eyes; the stage mirror still shows it).</summary>
        public bool IsLocalPlayer { get; set; }

        public Renderer ArmsRenderer => _armsRenderer;

        public void Configure(Transform head, Arm left, Arm right, Renderer bodyRenderer, Renderer armsRenderer)
        {
            _head = head;
            _left = left;
            _right = right;
            _bodyRenderer = bodyRenderer;
            _armsRenderer = armsRenderer;
        }

        /// <summary>
        /// How closed each paw should be, 0 = open, 1 = fist (the grip button; holding an item keeps it closed). The paws
        /// ease toward it in <see cref="Solve"/>.
        /// </summary>
        public void SetGrip(float left, float right)
        {
            _left.GripTarget = Mathf.Clamp01(left);
            _right.GripTarget = Mathf.Clamp01(right);
        }

        /// <summary>How closed the paw drawn this frame is (0 open, 1 fist).</summary>
        public float GetGrip(bool isRightHand) => isRightHand ? _right.Grip : _left.Grip;

        public void SetTargets(Transform head, Transform leftHand, Transform rightHand)
        {
            _headTarget = head;
            _left.Target = leftHand;
            _right.Target = rightHand;
        }

        /// <summary>Shows the torso/head and the arms separately: your own view shows only the arms.</summary>
        public void SetPartsVisible(bool body, bool arms)
        {
            if (_bodyRenderer != null)
            {
                _bodyRenderer.enabled = body;
            }

            if (_armsRenderer != null)
            {
                _armsRenderer.enabled = arms;
            }
        }

        /// <summary>Every body in the scene (the mirror shows the local player's hidden torso through this).</summary>
        public static readonly System.Collections.Generic.List<GorillaBodyRig> All = new System.Collections.Generic.List<GorillaBodyRig>();

        private void OnEnable() => All.Add(this);

        private void OnDisable() => All.Remove(this);

        private void Awake() => CacheRestPose();

        /// <summary>Poses head and arms toward the targets. Call once per frame, after the targets moved.</summary>
        public void Solve()
        {
            if (!_isCached)
            {
                CacheRestPose();
            }

            if (_head != null && _headTarget != null)
            {
                _head.rotation = LimitTilt(_headTarget.rotation) * _headFromRoot;
            }

            SolveArm(_left, -1f); // the left arm is on -X
            SolveArm(_right, 1f);
            CurlPaw(_left);
            CurlPaw(_right);
        }

        /// <summary>Eases the paw toward its grip and bends the finger and thumb bones (only when the amount changed).</summary>
        private void CurlPaw(Arm arm)
        {
            if (arm.FingerRest == null)
            {
                return;
            }

            arm.Grip = Mathf.MoveTowards(arm.Grip, arm.GripTarget, _gripSpeed * Time.deltaTime);
            if (Mathf.Abs(arm.Grip - arm.AppliedGrip) < 0.001f)
            {
                return;
            }

            arm.AppliedGrip = arm.Grip;
            float eased = arm.Grip * arm.Grip * (3f - 2f * arm.Grip);
            for (int i = 0; i < arm.Fingers.Length; i++)
            {
                if (arm.Fingers[i] != null)
                {
                    float degrees = _fingerCurl[Mathf.Min(i % 3, 2)];
                    arm.Fingers[i].localRotation = arm.FingerRest[i] * Quaternion.AngleAxis(degrees * eased, arm.FingerAxis[i]);
                }
            }

            for (int i = 0; i < arm.Thumb.Length; i++)
            {
                if (arm.Thumb[i] != null)
                {
                    float degrees = _thumbCurl[Mathf.Min(i, 1)];
                    arm.Thumb[i].localRotation = arm.ThumbRest[i] * Quaternion.AngleAxis(degrees * eased, arm.ThumbAxis[i]);
                }
            }
        }

        /// <summary>
        /// Rest rotations and curl axes (in each bone's own space): a finger bone turns about the axis that swings its tip
        /// toward the palm; the thumb folds toward the palm and down. Same rule as tools/blender/scripts/render_paw.py.
        /// </summary>
        private void CacheFingers(Arm arm)
        {
            if (arm.Hand == null || arm.Fingers == null || arm.Thumb == null)
            {
                return;
            }

            // The palm faces the body's midline (the paws are held palm-in, like holding the controllers).
            float handSide = Mathf.Sign(transform.InverseTransformPoint(arm.Hand.position).x);
            Vector3 palm = transform.right * -handSide;
            Vector3 thumbFold = (palm * 0.6f - transform.up).normalized;
            arm.FingerRest = new Quaternion[arm.Fingers.Length];
            arm.FingerAxis = new Vector3[arm.Fingers.Length];
            for (int i = 0; i < arm.Fingers.Length; i++)
            {
                CacheCurl(arm.Fingers, i, palm, out arm.FingerRest[i], out arm.FingerAxis[i]);
            }

            arm.ThumbRest = new Quaternion[arm.Thumb.Length];
            arm.ThumbAxis = new Vector3[arm.Thumb.Length];
            for (int i = 0; i < arm.Thumb.Length; i++)
            {
                CacheCurl(arm.Thumb, i, thumbFold, out arm.ThumbRest[i], out arm.ThumbAxis[i]);
            }

            arm.AppliedGrip = -1f;
        }

        private static void CacheCurl(Transform[] chain, int index, Vector3 toward, out Quaternion rest, out Vector3 localAxis)
        {
            Transform bone = chain[index];
            rest = bone != null ? bone.localRotation : Quaternion.identity;
            localAxis = Vector3.right;
            if (bone == null)
            {
                return;
            }

            // Along the bone: toward the next bone of the chain, or on from the previous one for the tip.
            Transform next = index + 1 < chain.Length && chain[index + 1] != null && chain[index + 1].parent == bone ? chain[index + 1] : null;
            Vector3 along = next != null ? next.position - bone.position : bone.position - bone.parent.position;
            Vector3 axis = Vector3.Cross(along, toward);
            if (along.sqrMagnitude < 1e-10f || axis.sqrMagnitude < 1e-10f)
            {
                return;
            }

            axis.Normalize();
            // Whatever the handedness, the curl must swing the tip toward the palm.
            if (Vector3.Dot(Quaternion.AngleAxis(10f, axis) * along - along, toward) < 0f)
            {
                axis = -axis;
            }

            localAxis = Quaternion.Inverse(bone.rotation) * axis;
        }

        private void CacheRestPose()
        {
            Quaternion toRoot = Quaternion.Inverse(transform.rotation);
            if (_head != null)
            {
                _headFromRoot = toRoot * _head.rotation;
            }

            CacheArm(_left, toRoot);
            CacheArm(_right, toRoot);
            CacheFingers(_left);
            CacheFingers(_right);
            _isCached = true;
        }

        private static void CacheArm(Arm arm, Quaternion toRoot)
        {
            arm.IsReady = arm.UpperArm != null && arm.Forearm != null && arm.Hand != null;
            if (!arm.IsReady)
            {
                return;
            }

            arm.UpperRest = arm.UpperArm.localRotation;
            arm.ForearmRest = arm.Forearm.localRotation;
            arm.ForearmRestPosition = arm.Forearm.localPosition;
            arm.HandRestPosition = arm.Hand.localPosition;
            arm.HandFromRoot = toRoot * arm.Hand.rotation;
            Vector3 glove = arm.GloveCenter != null ? arm.GloveCenter.position : arm.Hand.position;
            arm.GloveFromWrist = Quaternion.Inverse(arm.Hand.rotation) * (glove - arm.Hand.position);
        }

        private void SolveArm(Arm arm, float side)
        {
            if (!arm.IsReady || arm.Target == null)
            {
                return;
            }

            // Start from the rest pose every frame so nothing drifts.
            arm.UpperArm.localRotation = arm.UpperRest;
            arm.Forearm.localRotation = arm.ForearmRest;
            arm.Forearm.localPosition = arm.ForearmRestPosition;
            arm.Hand.localPosition = arm.HandRestPosition;

            // The glove centre sits on the tracked hand, so the wrist sits behind it along the hand's own rotation.
            Quaternion handRotation = arm.Target.rotation * arm.HandFromRoot;
            Vector3 wrist = arm.Target.position - handRotation * arm.GloveFromWrist;

            Vector3 shoulder = arm.UpperArm.position;
            float upper = Vector3.Distance(shoulder, arm.Forearm.position);
            float lower = Vector3.Distance(arm.Forearm.position, arm.Hand.position);
            Vector3 toWrist = wrist - shoulder;
            float distance = toWrist.magnitude;
            if (distance < 0.0001f || upper < 0.0001f || lower < 0.0001f)
            {
                return;
            }

            // Past full reach the arm stretches (both segments evenly) up to _maxStretch, so the glove stays attached.
            float reach = (upper + lower) * 0.999f;
            if (distance > reach)
            {
                float stretch = Mathf.Min(distance / reach, _maxStretch);
                arm.Forearm.localPosition = arm.ForearmRestPosition * stretch;
                arm.Hand.localPosition = arm.HandRestPosition * stretch;
                upper *= stretch;
                lower *= stretch;
            }

            Vector3 direction = toWrist / distance;
            distance = Mathf.Clamp(distance, Mathf.Abs(upper - lower) + 0.001f, (upper + lower) * 0.999f);

            // Elbow: law of cosines in the plane holding the shoulder, the wrist and the bend hint.
            Vector3 hint = transform.TransformDirection(new Vector3(_elbowHint.x * side, _elbowHint.y, _elbowHint.z));
            Vector3 bend = hint - direction * Vector3.Dot(hint, direction);
            if (bend.sqrMagnitude < 0.000001f)
            {
                bend = Vector3.Cross(direction, transform.forward * side);
            }

            bend.Normalize();
            float cos = Mathf.Clamp((upper * upper + distance * distance - lower * lower) / (2f * upper * distance), -1f, 1f);
            float sin = Mathf.Sqrt(1f - cos * cos);
            Vector3 elbow = shoulder + direction * (cos * upper) + bend * (sin * upper);

            arm.UpperArm.rotation = Quaternion.FromToRotation(arm.Forearm.position - shoulder, elbow - shoulder) * arm.UpperArm.rotation;
            Vector3 elbowNow = arm.Forearm.position;
            arm.Forearm.rotation = Quaternion.FromToRotation(arm.Hand.position - elbowNow, shoulder + toWrist - elbowNow) * arm.Forearm.rotation;
            arm.Hand.rotation = handRotation;
        }

        /// <summary>Keeps the head's up axis within _maxHeadTilt of the body's up, so extreme looks don't fold the neck.</summary>
        private Quaternion LimitTilt(Quaternion look)
        {
            Vector3 up = transform.up;
            Vector3 headUp = look * Vector3.up;
            float angle = Vector3.Angle(up, headUp);
            if (angle <= _maxHeadTilt)
            {
                return look;
            }

            Quaternion back = Quaternion.FromToRotation(headUp, Vector3.Slerp(up, headUp, _maxHeadTilt / angle));
            return back * look;
        }
    }
}
