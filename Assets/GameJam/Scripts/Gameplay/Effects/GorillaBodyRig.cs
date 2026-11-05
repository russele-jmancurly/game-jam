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

        private Quaternion _headFromRoot = Quaternion.identity;
        private bool _isCached;

        public Renderer BodyRenderer => _bodyRenderer;

        public Renderer ArmsRenderer => _armsRenderer;

        public void Configure(Transform head, Arm left, Arm right, Renderer bodyRenderer, Renderer armsRenderer)
        {
            _head = head;
            _left = left;
            _right = right;
            _bodyRenderer = bodyRenderer;
            _armsRenderer = armsRenderer;
        }

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
