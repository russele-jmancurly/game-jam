using System.Collections.Generic;
using GameJam.Core;
using GameJam.Player;
using UnityEngine;
using UnityEngine.XR;

namespace GorillaLocomotion
{
    /// <summary>
    /// Gorilla Tag style arm-swing locomotion. Ported from GorillaPlus (github.com/baggZ-idk/GorillaPlus, MIT), which
    /// updates Another Axiom's GorillaLocomotion (MIT, Kerestell) to its April 2022 behaviour. Project changes are listed
    /// in Locomotion/README.md; keep this file close to upstream so later GorillaPlus fixes can be diffed in.
    /// </summary>
    public class Player : MonoBehaviour
    {
        public static Player Instance { get; set; }

        public GameObject turnParent;

        [Header("Player")]

        public Transform headFollower;
        public SphereCollider headCollider;

        [Space]
        public CapsuleCollider bodyCollider;

        [Space]
        public Transform leftFollower;
        public Transform leftController;

        [Space]
        public Transform rightFollower;
        public Transform rightController;

        [Header("Audio")]
        public AudioSource leftTapSource;
        public AudioSource rightTapSource;
        public Sounds sounds;

        [Space]
        public AudioSource leftSlipSource;
        public AudioSource rightSlipSource;

        [Header("Offsets")]
        public Vector3 leftHandOffset;
        public Vector3 rightHandOffset;
        public Vector3 bodyOffset;

        [Header("Physics")]
        public bool disableMovement;

        [Range(2, 10)]
        public int velocityHistorySize = 6;

        [Range(0.1f, 1.5f)]
        public float velocityLimit = 0.3f;

        [Range(0.1f, 1.5f)]
        public float slideVelocityLimit = 0.7f;

        [Range(0.001f, 0.1f)]
        public float minimumRaycastDistance = 0.03f;

        public LayerMask locomotionEnabledLayers;
        private Rigidbody playerRigidBody;

        [Header("Locomotion")]

        [Range(1f, 2.5f)]
        public float maxArmLength = 1f;

        [Range(0.2f, 1f)]
        public float unStickDistance = 1f;

        [Range(0.9f, 1f)]
        public float defaultPrecision = 0.99f;

        [Range(0.7f, 1.3f)]
        public float teleportThresholdNoVel = 1f;

        [Header("Speed")]
        public float maxJumpSpeed;
        public float jumpMultiplier;

        [Header("Surfaces")]

        [Range(0.03f, 1f)]
        public float defaultSlideFactor = 0.03f;

        [Range(0.001f, 0.005f)]
        public float slideControl = 0.00425f;

        [Range(0.001f, 0.1f)]
        public float stickDepth = 0.008f;

        [Range(0.1f, 0.98f)]
        public float iceThreshold = 0.95f;

        [Tooltip("A PhysicsMaterial only makes a surface slippery when its static friction is below this, so ordinary " +
                 "physics materials (default friction 0.6) keep the normal gorilla grip. A Surface component always wins.")]
        [Range(0f, 1f)]
        public float slipperyFrictionThreshold = 0.3f;

        [Space]
        [Range(0.05f, 0.1f)]
        public float tapHapticDuration = 0.05f;

        [Range(0.1f, 0.75f)]
        public float tapHapticStrength = 0.5f;

        [Range(0.05f, 0.1f)]
        public float slideHapticStrength = 0.075f;

        [Range(0f, 1f)]
        public float tapCoolDown = 0.15f;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else if (Instance != this) Destroy(this);

            InitializeValues();
            playerRigidBody.maxAngularVelocity = 0f;

            bodyOffsetVector = new Vector3(0f, -bodyCollider.height / 2f, 0f);
            bodyInitialHeight = bodyCollider.height;
            bodyInitialRadius = bodyCollider.radius;

            rayCastNonAllocColliders = new RaycastHit[5];
            rayCastNonAllocActuallyColliders = new Collider[5];

            emptyHit = default;
            crazyCheckVectors = new Vector3[7] { Vector3.up, Vector3.down, Vector3.left, Vector3.right, Vector3.forward, Vector3.back, Vector3.zero };
        }

        public void InitializeValues()
        {
            playerRigidBody = GetComponent<Rigidbody>();
            velocityHistory = new Vector3[velocityHistorySize];
            slideAverageHistory = new Vector3[velocityHistorySize];

            for (int i = 0; i < velocityHistory.Length; i++)
            {
                velocityHistory[i] = Vector3.zero;
                slideAverageHistory[i] = Vector3.zero;
            }

            lastLeftHandPosition = leftFollower.transform.position;
            lastRightHandPosition = rightFollower.transform.position;
            lastHeadPosition = headFollower.transform.position;
            velocityIndex = 0;

            denormalizedVelocityAverage = Vector3.zero;
            slideAverage = Vector3.zero;
            lastPosition = transform.position;
            lastRealTime = Time.realtimeSinceStartup;
        }

        public void FixedUpdate()
        {
            if ((headFollower.transform.position - lastHeadPosition).magnitude >= teleportThresholdNoVel + playerRigidBody.linearVelocity.magnitude * calcDeltaTime) transform.position = transform.position + lastHeadPosition - headFollower.transform.position;
        }

        private float GetBouncePercentage(RaycastHit hit)
        {
            Surface surface = ResolveSurfaceOverride(hit.collider);
            if (surface != null)
            {
                return Mathf.Clamp01(surface.bouncePercentage);
            }

            PhysicsMaterial material = hit.collider != null ? hit.collider.sharedMaterial : null;
            return material != null ? Mathf.Clamp01(material.bounciness) : 0f;
        }

        private Surface ResolveSurfaceOverride(Collider collider)
        {
            if (collider == null)
            {
                return null;
            }

            if (!surfaceCache.TryGetValue(collider, out Surface surface))
            {
                surface = collider.GetComponentInParent<Surface>();
                surfaceCache[collider] = surface;
            }

            return surface;
        }

        public void BodyCollider()
        {
            if (MaxSphereSizeForNoOverlap(bodyInitialRadius, (headCollider.transform.position + bodyOffset), out bodyMaxRadius))
            {
                bodyCollider.radius = bodyMaxRadius;
                bodyCollider.height = Physics.SphereCast((headCollider.transform.position + bodyOffset), bodyMaxRadius, Vector3.down, out bodyHitInfo, bodyInitialHeight - bodyMaxRadius, locomotionEnabledLayers, QueryTriggerInteraction.Ignore) ? (bodyHitInfo.distance + bodyMaxRadius) : bodyInitialHeight;

                if (!bodyCollider.gameObject.activeSelf) bodyCollider.gameObject.SetActive(true);
            }
            else bodyCollider.gameObject.SetActive(false);

            bodyCollider.height = Mathf.Lerp(bodyCollider.height, bodyInitialHeight, bodyLerp);
            bodyCollider.radius = Mathf.Lerp(bodyCollider.radius, bodyInitialRadius, bodyLerp);

            bodyOffsetVector = Vector3.down * bodyCollider.height / 2f;
            bodyCollider.transform.position = (headCollider.transform.position + bodyOffset) + bodyOffsetVector;
            bodyCollider.transform.eulerAngles = new Vector3(0f, headCollider.transform.eulerAngles.y, 0f);
        }

        private Vector3 CurrentHandPosition(Transform handTransform, Vector3 handOffset) => (PositionWithOffset(handTransform, handOffset) - headFollower.transform.position).magnitude < maxArmLength ? PositionWithOffset(handTransform, handOffset) : headFollower.transform.position + (PositionWithOffset(handTransform, handOffset) - headFollower.transform.position).normalized * maxArmLength;
        private Vector3 CurrentLeftHandPosition() => (PositionWithOffset(leftController, leftHandOffset) - headFollower.transform.position).magnitude < maxArmLength ? PositionWithOffset(leftController, leftHandOffset) : headFollower.transform.position + (PositionWithOffset(leftController, leftHandOffset) - headFollower.transform.position).normalized * maxArmLength;
        private Vector3 CurrentRightHandPosition() => (PositionWithOffset(rightController, rightHandOffset) - headFollower.transform.position).magnitude < maxArmLength ? PositionWithOffset(rightController, rightHandOffset) : headFollower.transform.position + (PositionWithOffset(rightController, rightHandOffset) - headFollower.transform.position).normalized * maxArmLength;
        private Vector3 PositionWithOffset(Transform transformToModify, Vector3 offsetVector) => transformToModify.position + transformToModify.rotation * offsetVector;

        private void LateUpdate()
        {
            rigidBodyMovement = Vector3.zero;
            firstIterationLeftHand = Vector3.zero;
            firstIterationRightHand = Vector3.zero;
            firstIterationHead = Vector3.zero;

            tempRealTime = Time.realtimeSinceStartup;
            calcDeltaTime = tempRealTime - lastRealTime;
            lastRealTime = tempRealTime;

            if (bounceReleaseTimer > 0f)
            {
                bounceReleaseTimer -= Time.deltaTime;
            }

            if (calcDeltaTime > 0.1f)
            {
                calcDeltaTime = 0.05f;
            }

            lastLeftHandPosition = UnembedHand(lastLeftHandPosition);
            lastRightHandPosition = UnembedHand(lastRightHandPosition);

            if (!didAJump && (wasLeftHandTouching || wasRightHandTouching))
            {
                transform.position = transform.position + 4.9f * calcDeltaTime * calcDeltaTime * Vector3.down;
                if (Vector3.Dot(denormalizedVelocityAverage, slideAverageNormal) <= 0f && Vector3.Dot(Vector3.down, slideAverageNormal) <= 0f)
                {
                    transform.position = transform.position - Vector3.Project(Mathf.Min(stickDepth, Vector3.Project(denormalizedVelocityAverage, slideAverageNormal).magnitude * calcDeltaTime) * slideAverageNormal, Vector3.down);
                }
            }
            if (!didAJump && (wasLeftHandSlide || wasRightHandSlide))
            {
                transform.position = transform.position + slideAverage * calcDeltaTime;
                slideAverage += 9.8f * calcDeltaTime * Vector3.down;
            }

            FirstHandIteration(leftController, leftHandOffset, lastLeftHandPosition, wasLeftHandSlide, wasLeftHandTouching, out firstIterationLeftHand, ref leftHandSlipPercentage, ref leftHandSlide, ref leftHandSlideNormal, ref leftHandColliding, ref leftHandMaterialTouchIndex, ref leftHandSurfaceOverride);
            FirstHandIteration(rightController, rightHandOffset, lastRightHandPosition, wasRightHandSlide, wasRightHandTouching, out firstIterationRightHand, ref rightHandSlipPercentage, ref rightHandSlide, ref rightHandSlideNormal, ref rightHandColliding, ref rightHandMaterialTouchIndex, ref rightHandSurfaceOverride);
            touchPoints = 0;
            rigidBodyMovement = Vector3.zero;

            if (leftHandColliding || wasLeftHandTouching)
            {
                rigidBodyMovement += firstIterationLeftHand;
                touchPoints++;
            }
            if (rightHandColliding || wasRightHandTouching)
            {
                rigidBodyMovement += firstIterationRightHand;
                touchPoints++;
            }
            if (touchPoints != 0)
            {
                rigidBodyMovement /= (float)touchPoints;
            }

            if (!MaxSphereSizeForNoOverlap(headCollider.radius * 0.4f, lastHeadPosition, out maxSphereSize1) && !CrazyCheck2(headCollider.radius * 0.4f * 0.75f, lastHeadPosition))
            {
                lastHeadPosition = lastOpenHeadPosition;
            }
            if (IterativeCollisionSphereCast(lastHeadPosition, headCollider.radius * 0.4f, headFollower.transform.position + rigidBodyMovement - lastHeadPosition, out finalPosition, false, out slipPercentage, out junkHit, true))
            {
                rigidBodyMovement = finalPosition - headFollower.transform.position;
            }
            if (!MaxSphereSizeForNoOverlap(headCollider.radius * 0.4f, lastHeadPosition + rigidBodyMovement, out maxSphereSize1) || !CrazyCheck2(headCollider.radius * 0.4f * 0.75f, lastHeadPosition + rigidBodyMovement))
            {
                lastHeadPosition = lastOpenHeadPosition;
                rigidBodyMovement = lastHeadPosition - headFollower.transform.position;
            }
            else if (headCollider.radius * 0.4f * 0.825f < maxSphereSize1)
            {
                lastOpenHeadPosition = headFollower.transform.position + rigidBodyMovement;
            }

            if (rigidBodyMovement != Vector3.zero)
            {
                transform.position += rigidBodyMovement;
            }

            lastHeadPosition = headFollower.transform.position;
            areBothTouching = ((!leftHandColliding && !wasLeftHandTouching) || (!rightHandColliding && !wasRightHandTouching));
            lastLeftHandPosition = FinalHandPosition(leftController, leftHandOffset, lastLeftHandPosition, areBothTouching, leftHandColliding, out leftHandColliding, leftHandSlide, out leftHandSlide, leftHandMaterialTouchIndex, out leftHandMaterialTouchIndex, leftHandSurfaceOverride, out leftHandSurfaceOverride);
            lastRightHandPosition = FinalHandPosition(rightController, rightHandOffset, lastRightHandPosition, areBothTouching, rightHandColliding, out rightHandColliding, rightHandSlide, out rightHandSlide, rightHandMaterialTouchIndex, out rightHandMaterialTouchIndex, rightHandSurfaceOverride, out rightHandSurfaceOverride);
            StoreVelocities();
            didAJump = false;

            if (rightHandSlide || leftHandSlide)
            {
                slideAverageNormal = Vector3.zero;
                touchPoints = 0;
                averageSlipPercentage = 0f;
                if (leftHandSlide)
                {
                    slideAverageNormal += leftHandSlideNormal.normalized;
                    averageSlipPercentage += leftHandSlipPercentage;
                    touchPoints++;
                }
                if (rightHandSlide)
                {
                    slideAverageNormal += rightHandSlideNormal.normalized;
                    averageSlipPercentage += rightHandSlipPercentage;
                    touchPoints++;
                }
                slideAverageNormal = slideAverageNormal.normalized;
                averageSlipPercentage /= (float)touchPoints;
                if (touchPoints == 1)
                {
                    surfaceDirection = (rightHandSlide ? Vector3.ProjectOnPlane(rightController.forward, rightHandSlideNormal) : Vector3.ProjectOnPlane(leftController.forward, leftHandSlideNormal));
                    if (Vector3.Dot(slideAverage, surfaceDirection) > 0f)
                    {
                        slideAverage = Vector3.Project(slideAverage, Vector3.Slerp(slideAverage, surfaceDirection.normalized * slideAverage.magnitude, slideControl));
                    }
                    else
                    {
                        slideAverage = Vector3.Project(slideAverage, Vector3.Slerp(slideAverage, -surfaceDirection.normalized * slideAverage.magnitude, slideControl));
                    }
                }
                if (!wasLeftHandSlide && !wasRightHandSlide)
                {
                    slideAverage = ((Vector3.Dot(playerRigidBody.linearVelocity, slideAverageNormal) <= 0f) ? Vector3.ProjectOnPlane(playerRigidBody.linearVelocity, slideAverageNormal) : playerRigidBody.linearVelocity);
                }
                else
                {
                    slideAverage = ((Vector3.Dot(slideAverage, slideAverageNormal) <= 0f) ? Vector3.ProjectOnPlane(slideAverage, slideAverageNormal) : slideAverage);
                }
                slideAverage = slideAverage.normalized * Mathf.Min(slideAverage.magnitude, Mathf.Max(0.5f, denormalizedVelocityAverage.magnitude * 2f));
                playerRigidBody.linearVelocity = Vector3.zero;
            }
            else if (leftHandColliding || rightHandColliding)
            {
                if (!didATurn)
                {
                    playerRigidBody.linearVelocity = Vector3.zero;
                }
                else
                {
                    playerRigidBody.linearVelocity = playerRigidBody.linearVelocity.normalized * Mathf.Min(2f, playerRigidBody.linearVelocity.magnitude);
                }
            }
            else if (wasLeftHandSlide || wasRightHandSlide)
            {
                playerRigidBody.linearVelocity = ((Vector3.Dot(slideAverage, slideAverageNormal) <= 0f) ? Vector3.ProjectOnPlane(slideAverage, slideAverageNormal) : slideAverage);
            }

            if ((rightHandColliding || leftHandColliding) && !disableMovement && !didATurn)
            {
                if (rightHandSlide || leftHandSlide)
                {
                    if (Vector3.Project(denormalizedVelocityAverage, slideAverageNormal).magnitude > slideVelocityLimit && Vector3.Dot(denormalizedVelocityAverage, slideAverageNormal) > 0f && Vector3.Project(denormalizedVelocityAverage, slideAverageNormal).magnitude > Vector3.Project(slideAverage, slideAverageNormal).magnitude)
                    {
                        leftHandSlide = false;
                        rightHandSlide = false;
                        didAJump = true;
                        playerRigidBody.linearVelocity = Mathf.Min(maxJumpSpeed, jumpMultiplier * Vector3.Project(denormalizedVelocityAverage, slideAverageNormal).magnitude) * slideAverageNormal.normalized + Vector3.ProjectOnPlane(slideAverage, slideAverageNormal);
                    }
                }
                else if (denormalizedVelocityAverage.magnitude > velocityLimit)
                {
                    didAJump = true;
                    playerRigidBody.linearVelocity = Mathf.Min(maxJumpSpeed, jumpMultiplier * denormalizedVelocityAverage.magnitude) * denormalizedVelocityAverage.normalized;
                }
            }

            if (pendingBounce && !disableMovement && !didATurn)
            {
                bounceReleaseTimer = 0.15f;
                Vector3 incomingVelocity = pendingBounceIncomingVelocity;
                float normalVelocity = Vector3.Dot(incomingVelocity, pendingBounceNormal);

                if (normalVelocity < 0f)
                {
                    playerRigidBody.linearVelocity =
                        incomingVelocity -
                        (1f + pendingBouncePercentage) *
                        normalVelocity *
                        pendingBounceNormal;

                    didAJump = true;
                }

                pendingBounce = false;
                pendingBounceImpactSpeed = 0f;
            }

            if (leftHandColliding && (CurrentLeftHandPosition() - lastLeftHandPosition).magnitude > unStickDistance && !Physics.Raycast(headFollower.transform.position, (CurrentLeftHandPosition() - headFollower.transform.position).normalized, out hitInfo, (CurrentLeftHandPosition() - headFollower.transform.position).magnitude, locomotionEnabledLayers.value, QueryTriggerInteraction.Ignore))
            {
                lastLeftHandPosition = CurrentLeftHandPosition();
                leftHandColliding = false;
            }

            if (rightHandColliding && (CurrentRightHandPosition() - lastRightHandPosition).magnitude > unStickDistance && !Physics.Raycast(headFollower.transform.position, (CurrentRightHandPosition() - headFollower.transform.position).normalized, out hitInfo, (CurrentRightHandPosition() - headFollower.transform.position).magnitude, locomotionEnabledLayers.value, QueryTriggerInteraction.Ignore))
            {
                lastRightHandPosition = CurrentRightHandPosition();
                rightHandColliding = false;
            }

            leftFollower.position = lastLeftHandPosition;
            leftFollower.rotation = leftController.rotation;
            rightFollower.position = lastRightHandPosition;
            rightFollower.rotation = rightController.rotation;
            wasLeftHandTouching = leftHandColliding;
            wasRightHandTouching = rightHandColliding;
            wasLeftHandSlide = leftHandSlide;
            wasRightHandSlide = rightHandSlide;
            didATurn = false;
            BodyCollider();

            if (!IsHandSliding(true) && IsHandTouching(true) && leftTapArmed && Time.time > lastLeftTap + tapCoolDown)
            {
                PlayHandTap(leftTapSource, leftHandTapMaterial);
                StartVibration(true, tapHapticStrength, tapHapticDuration);
                lastLeftTap = Time.time;
                lastLeftTapPoint = leftFollower.position;
                lastLeftTapNormal = leftHandSlideNormal;
                leftTapArmed = false;
            }
            else if (IsHandSliding(true))
            {
                if (leftSlipSource != null && !leftSlipSource.isPlaying)
                {
                    leftSlipSource.Play();
                }

                StartVibration(true, slideHapticStrength, SlideHapticSeconds);
            }

            if (!IsHandSliding(true) && leftSlipSource != null && leftSlipSource.isPlaying)
            {
                leftSlipSource.Stop();
            }

            if (!IsHandSliding(false) && IsHandTouching(false) && rightTapArmed && Time.time > lastRightTap + tapCoolDown)
            {
                PlayHandTap(rightTapSource, rightHandTapMaterial);
                StartVibration(false, tapHapticStrength, tapHapticDuration);
                lastRightTap = Time.time;
                lastRightTapPoint = rightFollower.position;
                lastRightTapNormal = rightHandSlideNormal;
                rightTapArmed = false;
            }
            else if (IsHandSliding(false))
            {
                if (rightSlipSource != null && !rightSlipSource.isPlaying)
                {
                    rightSlipSource.Play();
                }

                StartVibration(false, slideHapticStrength, SlideHapticSeconds);
            }

            if (!IsHandSliding(false) && rightSlipSource != null && rightSlipSource.isPlaying)
            {
                rightSlipSource.Stop();
            }

            leftHandTouching = IsHandTouching(true);
            rightHandTouching = IsHandTouching(false);

            if (!IsHandTouching(true))
            {
                if (leftTapReleaseTime == 0f)
                    leftTapReleaseTime = Time.time;

            // Re-arm only once the hand has lifted off the surface it tapped: a hand resting still (or settling and
            // sliding a little) flickers in and out of contact, which would otherwise repeat the tap and its rumble.
            if (Time.time - leftTapReleaseTime >= tapReleaseDelay && HasLiftedOff(leftFollower.position, lastLeftTapPoint, lastLeftTapNormal))
                leftTapArmed = true;
            }
            else
            {
                leftTapReleaseTime = 0f;
            }

            if (!IsHandTouching(false))
            {
                if (rightTapReleaseTime == 0f)
                    rightTapReleaseTime = Time.time;

                if (Time.time - rightTapReleaseTime >= tapReleaseDelay && HasLiftedOff(rightFollower.position, lastRightTapPoint, lastRightTapNormal))
                    rightTapArmed = true;
            }
            else
            {
                rightTapReleaseTime = 0f;
            }
        }

        private void FirstHandIteration(Transform handTransform, Vector3 handOffset, Vector3 lastHandPosition, bool wasHandSlide, bool wasHandTouching, out Vector3 firstIteration, ref float handSlipPercentage, ref bool handSlide, ref Vector3 slideNormal, ref bool handColliding, ref int materialTouchIndex, ref Surface touchedOverride)
        {
            firstIteration = Vector3.zero;
            distanceTraveled = CurrentHandPosition(handTransform, handOffset) - lastHandPosition;

            if (!didAJump && wasHandSlide && Vector3.Dot(slideNormal, Vector3.up) > 0f)
            {
                distanceTraveled += Vector3.Project(-slideAverageNormal * stickDepth, Vector3.down);
            }

            if (IterativeCollisionSphereCast(lastHandPosition, minimumRaycastDistance, distanceTraveled, out finalPosition, true, out slipPercentage, out tempHitInfo, false))
            {
                if (handTransform == leftController) leftHandTapMaterial = currentMaterial;
                else rightHandTapMaterial = currentMaterial;

                if (!wasHandTouching && tempHitInfo.collider != null)
                {
                    float bouncePercentage = GetBouncePercentage(tempHitInfo);

                    if (bouncePercentage > 0f)
                    {
                        Vector3 incomingVelocity = denormalizedVelocityAverage;
                        Vector3 rigidbodyVelocity = playerRigidBody.linearVelocity;

                        if (Vector3.Dot(rigidbodyVelocity, tempHitInfo.normal) < Vector3.Dot(incomingVelocity, tempHitInfo.normal))
                        {
                            incomingVelocity = rigidbodyVelocity;
                        }

                        float impactSpeed = -Vector3.Dot(incomingVelocity, tempHitInfo.normal);

                        if (impactSpeed > 0f &&
                            (!pendingBounce || impactSpeed > pendingBounceImpactSpeed))
                        {
                            pendingBounce = true;
                            pendingBounceNormal = tempHitInfo.normal;
                            pendingBouncePercentage = bouncePercentage;
                            pendingBounceImpactSpeed = impactSpeed;
                            pendingBounceIncomingVelocity = incomingVelocity;
                        }
                    }
                }

                if (wasHandTouching && slipPercentage <= defaultSlideFactor && bounceReleaseTimer <= 0f)
                {
                    firstIteration = lastHandPosition - CurrentHandPosition(handTransform, handOffset);
                }
                else
                {
                    firstIteration = finalPosition - CurrentHandPosition(handTransform, handOffset);
                }

                handSlipPercentage = slipPercentage;
                handSlide = (slipPercentage > iceThreshold);
                slideNormal = tempHitInfo.normal;
                handColliding = true;
                materialTouchIndex = currentMaterialIndex;
                touchedOverride = currentOverride;

                return;
            }

            handSlipPercentage = 0f;
            handSlide = false;
            slideNormal = Vector3.up;
            handColliding = false;
            materialTouchIndex = 0;
            touchedOverride = null;
        }

        private void PlayHandTap(AudioSource source, Material material)
        {
            if (source == null) return;

            AudioClip clip = sounds != null ? sounds.GetSound(material) : null;

            if (clip != null) source.PlayOneShot(clip);
            else if (source.clip != null) source.Play();
        }

        private Material ResolveSurfaceMaterial(RaycastHit hit)
        {
            Collider col = hit.collider;
            if (col == null) return null;

            if (!rendererCache.TryGetValue(col, out Renderer rend))
            {
                rend = col.GetComponent<Renderer>();
                if (rend == null) rend = col.GetComponentInParent<Renderer>();
                rendererCache[col] = rend;
            }
            if (rend == null) return null;

            rend.GetSharedMaterials(sharedMatsBuffer);
            if (sharedMatsBuffer.Count == 0) return null;
            if (sharedMatsBuffer.Count == 1) return sharedMatsBuffer[0];

            MeshCollider meshCol = col as MeshCollider;
            if (meshCol == null || meshCol.sharedMesh == null) return sharedMatsBuffer[0];

            int tri = hit.triangleIndex;
            if (tri < 0)
            {
                if (Physics.Raycast(hit.point + hit.normal * 0.02f, -hit.normal, out RaycastHit rayHit, 0.05f, locomotionEnabledLayers.value, QueryTriggerInteraction.Ignore) && rayHit.collider == col)
                {
                    tri = rayHit.triangleIndex;
                }
            }
            if (tri < 0) return sharedMatsBuffer[0];

            Mesh mesh = meshCol.sharedMesh;
            int indexPos = tri * 3;
            int subCount = Mathf.Min(mesh.subMeshCount, sharedMatsBuffer.Count);

            for (int i = 0; i < subCount; i++)
            {
                var sm = mesh.GetSubMesh(i);
                if (indexPos >= sm.indexStart && indexPos < sm.indexStart + sm.indexCount)
                {
                    return sharedMatsBuffer[i];
                }
            }

            return sharedMatsBuffer[0];
        }

        public void ClearSurfaceCache()
        {
            rendererCache.Clear();
            surfaceCache.Clear();
        }

        public void StartVibration(bool forLeftController, float amplitude, float duration)
        {
            XRHaptics.Send(forLeftController ? XRNode.LeftHand : XRNode.RightHand, new HapticPulse(amplitude, duration));
        }

        private Vector3 FinalHandPosition(Transform handTransform, Vector3 handOffset, Vector3 lastHandPosition, bool bothTouching, bool isHandTouching, out bool handColliding, bool isHandSlide, out bool handSlide, int currentMaterialTouchIndex, out int materialTouchIndex, Surface currentSurface, out Surface touchedOverride)
        {
            handColliding = isHandTouching;
            handSlide = isHandSlide;
            materialTouchIndex = currentMaterialTouchIndex;
            touchedOverride = currentSurface;
            distanceTraveled = CurrentHandPosition(handTransform, handOffset) - lastHandPosition;

            if (IterativeCollisionSphereCast(lastHandPosition, minimumRaycastDistance, distanceTraveled, out finalPosition, bothTouching, out slipPercentage, out junkHit, false))
            {
                if (handTransform == leftController) leftHandTapMaterial = currentMaterial;
                else if (handTransform == rightController) rightHandTapMaterial = currentMaterial;

                handColliding = true;
                handSlide = (slipPercentage > iceThreshold);
                materialTouchIndex = currentMaterialIndex;
                touchedOverride = currentOverride;
                return finalPosition;
            }

            return CurrentHandPosition(handTransform, handOffset);
        }

        /// <summary>
        /// Project change: a hand point that ended up inside a collider is moved back out to the surface, on the side the
        /// head sees it from. Every sweep that starts inside geometry reports a hit at distance 0, so such a hand stayed
        /// "planted" forever (and the unstick check can't free it: the ray from the head to the controller hits that surface).
        /// </summary>
        private Vector3 UnembedHand(Vector3 handPosition)
        {
            int layers = locomotionEnabledLayers.value;
            float probe = minimumRaycastDistance * 0.75f;
            if (layers == 0 || !Physics.CheckSphere(handPosition, probe, layers, QueryTriggerInteraction.Ignore))
            {
                return handPosition;
            }

            Vector3 head = headFollower.transform.position;
            Vector3 toHand = handPosition - head;
            float distance = toHand.magnitude;
            Vector3 candidate = handPosition;
            if (distance > 0.0001f && Physics.Raycast(head, toHand / distance, out RaycastHit entry, distance, layers, QueryTriggerInteraction.Ignore))
            {
                candidate = entry.point + entry.normal * minimumRaycastDistance;
            }

            // Corners and seams: step toward the head until the hand sphere is clear.
            for (int i = 0; i < UnembedSteps && Physics.CheckSphere(candidate, probe, layers, QueryTriggerInteraction.Ignore); i++)
            {
                candidate = Vector3.MoveTowards(candidate, head, UnembedStepLength);
            }

            return candidate;
        }

        private const int UnembedSteps = 20;
        private const float UnembedStepLength = 0.01f;

        private bool IterativeCollisionSphereCast(Vector3 startPosition, float sphereRadius, Vector3 movementVector, out Vector3 endPosition, bool singleHand, out float slipPercentage, out RaycastHit iterativeHitInfo, bool fullSlide)
        {
            slipPercentage = defaultSlideFactor;
            currentMaterial = null;

            if (!CollisionsSphereCast(startPosition, sphereRadius, movementVector, out endPosition, out tempIterativeHit))
            {
                iterativeHitInfo = tempIterativeHit;
                endPosition = Vector3.zero;
                return false;
            }

            firstPosition = endPosition;
            iterativeHitInfo = tempIterativeHit;
            currentMaterial = ResolveSurfaceMaterial(tempIterativeHit);
            currentOverride = ResolveSurfaceOverride(tempIterativeHit.collider);
            slideFactor = GetSlidePercentage(iterativeHitInfo);
            slipPercentage = (slideFactor != defaultSlideFactor) ? slideFactor : ((!singleHand) ? defaultSlideFactor : 0.001f);

            if (fullSlide)
                slipPercentage = 1f;

            movementToProjectedAboveCollisionPlane = Vector3.ProjectOnPlane(startPosition + movementVector - firstPosition, iterativeHitInfo.normal) * slipPercentage;

            if (CollisionsSphereCast(firstPosition, sphereRadius, movementToProjectedAboveCollisionPlane, out endPosition, out tempIterativeHit))
            {
                iterativeHitInfo = tempIterativeHit;
                return true;
            }

            if (CollisionsSphereCast(movementToProjectedAboveCollisionPlane + firstPosition, sphereRadius, startPosition + movementVector - (movementToProjectedAboveCollisionPlane + firstPosition), out endPosition, out tempIterativeHit))
            {
                iterativeHitInfo = tempIterativeHit;
                return true;
            }

            endPosition = Vector3.zero;
            return false;
        }

        private bool CollisionsSphereCast(Vector3 startPosition, float sphereRadius, Vector3 movementVector, out Vector3 finalPosition, out RaycastHit collisionsHitInfo)
        {
            MaxSphereSizeForNoOverlap(sphereRadius, startPosition, out maxSphereSize1);
            ClearRaycasthitBuffer(ref rayCastNonAllocColliders);

            bufferCount = Physics.SphereCastNonAlloc(startPosition, maxSphereSize1, movementVector.normalized, rayCastNonAllocColliders, movementVector.magnitude, locomotionEnabledLayers.value, QueryTriggerInteraction.Ignore);

            if (bufferCount > 0)
            {
                tempHitInfo = rayCastNonAllocColliders[0];
                for (int i = 0; i < bufferCount; i++)
                {
                    if (rayCastNonAllocColliders[i].distance < tempHitInfo.distance)
                    {
                        tempHitInfo = rayCastNonAllocColliders[i];
                    }
                }

                collisionsHitInfo = tempHitInfo;
                finalPosition = collisionsHitInfo.point + collisionsHitInfo.normal * sphereRadius;
                ClearRaycasthitBuffer(ref rayCastNonAllocColliders);
                Vector3 towardFinal = (finalPosition - startPosition).normalized;
                bufferCount = Physics.RaycastNonAlloc(startPosition, towardFinal, rayCastNonAllocColliders, (finalPosition - startPosition).magnitude, locomotionEnabledLayers.value, QueryTriggerInteraction.Ignore);

                if (bufferCount > 0)
                {
                    tempHitInfo = rayCastNonAllocColliders[0];
                    for (int j = 0; j < bufferCount; j++)
                    {
                        if (rayCastNonAllocColliders[j].distance < tempHitInfo.distance)
                        {
                            tempHitInfo = rayCastNonAllocColliders[j];
                        }
                    }
                    // Project change: stop a sphere radius short of the surface, along the ray that hit it. Upstream put the
                    // hand centre on the surface (and along the movement, not the ray), half inside the collider.
                    finalPosition = startPosition + towardFinal * Mathf.Max(0f, tempHitInfo.distance - sphereRadius);
                }

                MaxSphereSizeForNoOverlap(sphereRadius, finalPosition, out maxSphereSize2);
                ClearRaycasthitBuffer(ref rayCastNonAllocColliders);
                bufferCount = Physics.SphereCastNonAlloc(startPosition, Mathf.Min(maxSphereSize1, maxSphereSize2), (finalPosition - startPosition).normalized, rayCastNonAllocColliders, (finalPosition - startPosition).magnitude, locomotionEnabledLayers.value, QueryTriggerInteraction.Ignore);

                if (bufferCount > 0)
                {
                    tempHitInfo = rayCastNonAllocColliders[0];
                    for (int k = 0; k < bufferCount; k++)
                    {
                        if (rayCastNonAllocColliders[k].collider != null && rayCastNonAllocColliders[k].distance < tempHitInfo.distance)
                        {
                            tempHitInfo = rayCastNonAllocColliders[k];
                        }
                    }
                    finalPosition = startPosition + tempHitInfo.distance * (finalPosition - startPosition).normalized;
                    collisionsHitInfo = tempHitInfo;
                }

                ClearRaycasthitBuffer(ref rayCastNonAllocColliders);
                bufferCount = Physics.RaycastNonAlloc(startPosition, (finalPosition - startPosition).normalized, rayCastNonAllocColliders, (finalPosition - startPosition).magnitude, locomotionEnabledLayers.value, QueryTriggerInteraction.Ignore);

                if (bufferCount > 0)
                {
                    tempHitInfo = rayCastNonAllocColliders[0];
                    for (int l = 0; l < bufferCount; l++)
                    {
                        if (rayCastNonAllocColliders[l].distance < tempHitInfo.distance)
                        {
                            tempHitInfo = rayCastNonAllocColliders[l];
                        }
                    }
                    collisionsHitInfo = tempHitInfo;
                    finalPosition = startPosition;
                }

                return true;
            }

            ClearRaycasthitBuffer(ref rayCastNonAllocColliders);
            bufferCount = Physics.RaycastNonAlloc(startPosition, movementVector.normalized, rayCastNonAllocColliders, movementVector.magnitude, locomotionEnabledLayers.value, QueryTriggerInteraction.Ignore);

            if (bufferCount > 0)
            {
                tempHitInfo = rayCastNonAllocColliders[0];
                for (int m = 0; m < bufferCount; m++)
                {
                    if (rayCastNonAllocColliders[m].collider != null && rayCastNonAllocColliders[m].distance < tempHitInfo.distance)
                    {
                        tempHitInfo = rayCastNonAllocColliders[m];
                    }
                }
                collisionsHitInfo = tempHitInfo;
                finalPosition = startPosition;

                return true;
            }

            finalPosition = startPosition + movementVector;
            collisionsHitInfo = default;

            return false;
        }

        public bool IsHandTouching(bool forLeftHand) => forLeftHand ? wasLeftHandTouching : wasRightHandTouching;

        public bool IsHandSliding(bool forLeftHand) => forLeftHand ? (wasLeftHandSlide || leftHandSlide) : (wasRightHandSlide || rightHandSlide);

        public float GetSlidePercentage(RaycastHit raycastHit)
        {
            Surface surface = ResolveSurfaceOverride(raycastHit.collider);
            if (surface != null)
            {
                return Mathf.Clamp01(surface.slipPercentage);
            }

            PhysicsMaterial material = raycastHit.collider != null ? raycastHit.collider.sharedMaterial : null;

            if (material == null || material.staticFriction >= slipperyFrictionThreshold)
                return defaultSlideFactor;

            return 1f - Mathf.Clamp01(material.staticFriction / 2f);
        }

        /// <summary>
        /// Project change: moves the player with a ride (a spinning platform) by a world offset, planted hand points and
        /// the head history included, so planted hands don't drag the body back and the move isn't counted as a throw.
        /// </summary>
        public void Carry(Vector3 delta)
        {
            transform.position += delta;
            // Absolute, not += delta: the rigidbody already picks up part of the transform write at the next physics step,
            // so adding the delta to it as well moved the player twice on every frame with a physics step (~20% too far).
            playerRigidBody.position = transform.position;
            lastLeftHandPosition += delta;
            lastRightHandPosition += delta;
            lastHeadPosition += delta;
            lastOpenHeadPosition += delta;
            lastPosition += delta;
        }

        /// <summary>
        /// Project change: turns the whole player about a vertical axis through a world point (riding a spinning platform),
        /// exactly, so a rider neither drifts outward nor slides against the ride's handles. View, planted hands and recent
        /// hand speeds turn with it. Unlike Turn it doesn't flag a turn, so jumping off the ride still works.
        /// </summary>
        public void Ride(Vector3 pivot, float degrees)
        {
            // Rotate the whole rig (rigidbody, colliders, tracking space) as one rigid piece about the ride's axis. Turning
            // only the tracking space every frame moved the colliders inside the rigidbody, and the player fell through.
            // The rigidbody pose is the source of truth (copying the interpolated transform into it threw away part of every
            // frame's motion, so a jump off the ride barely moved).
            Quaternion spin = Quaternion.AngleAxis(degrees, Vector3.up);
            playerRigidBody.position = pivot + spin * (playerRigidBody.position - pivot);
            playerRigidBody.rotation = spin * playerRigidBody.rotation;
            transform.RotateAround(pivot, Vector3.up, degrees);

            lastLeftHandPosition = pivot + spin * (lastLeftHandPosition - pivot);
            lastRightHandPosition = pivot + spin * (lastRightHandPosition - pivot);
            lastHeadPosition = pivot + spin * (lastHeadPosition - pivot);
            lastOpenHeadPosition = pivot + spin * (lastOpenHeadPosition - pivot);
            lastPosition = pivot + spin * (lastPosition - pivot);
            denormalizedVelocityAverage = Vector3.zero;
            for (int i = 0; i < velocityHistory.Length; i++)
            {
                velocityHistory[i] = spin * velocityHistory[i];
                denormalizedVelocityAverage += velocityHistory[i];
            }
        }

        public void Turn(float degrees)
        {
            Vector3 pivot = headFollower.transform.position;
            Quaternion spin = Quaternion.AngleAxis(degrees, transform.up);
            turnParent.transform.RotateAround(pivot, transform.up, degrees);
            lastLeftHandPosition = pivot + spin * (lastLeftHandPosition - pivot);
            lastRightHandPosition = pivot + spin * (lastRightHandPosition - pivot);
            lastHeadPosition = headFollower.transform.position;
            denormalizedVelocityAverage = Vector3.zero;

            for (int i = 0; i < velocityHistory.Length; i++)
            {
                velocityHistory[i] = Quaternion.Euler(0f, degrees, 0f) * velocityHistory[i];
                denormalizedVelocityAverage += velocityHistory[i];
            }

            didATurn = true;
        }

        private void StoreVelocities()
        {
            if (calcDeltaTime <= 0f)
            {
                return;
            }

            velocityIndex = (velocityIndex + 1) % velocityHistory.Length;
            currentVelocity = (transform.position - lastPosition) / calcDeltaTime;
            velocityHistory[velocityIndex] = currentVelocity;
            denormalizedVelocityAverage = Vector3.zero;

            for (int i = 0; i < velocityHistory.Length; i++)
            {
                denormalizedVelocityAverage += velocityHistory[i];
            }

            denormalizedVelocityAverage /= (float)velocityHistory.Length;
            lastPosition = transform.position;
        }

        private bool MaxSphereSizeForNoOverlap(float testRadius, Vector3 checkPosition, out float overlapRadiusTest)
        {
            overlapRadiusTest = testRadius;
            overlapAttempts = 0;

            while (overlapAttempts < 100 && overlapRadiusTest > testRadius * 0.75f)
            {
                ClearColliderBuffer(ref overlapColliders);
                bufferCount = Physics.OverlapSphereNonAlloc(checkPosition, overlapRadiusTest, overlapColliders, locomotionEnabledLayers.value, QueryTriggerInteraction.Ignore);

                if (bufferCount <= 0)
                {
                    overlapRadiusTest *= 0.995f;
                    return true;
                }

                overlapRadiusTest *= 0.99f;
                overlapAttempts++;
            }

            return false;
        }

        private bool CrazyCheck2(float sphereSize, Vector3 startPosition)
        {
            for (int i = 0; i < crazyCheckVectors.Length; i++)
            {
                if (NonAllocRaycast(startPosition, startPosition + crazyCheckVectors[i] * sphereSize) > 0)
                {
                    return false;
                }
            }

            return true;
        }

        private int NonAllocRaycast(Vector3 startPosition, Vector3 endPosition) => Physics.RaycastNonAlloc(startPosition, (endPosition - startPosition).normalized, rayCastNonAllocColliders, (endPosition - startPosition).magnitude, locomotionEnabledLayers.value, QueryTriggerInteraction.Ignore);

        private void ClearColliderBuffer(ref Collider[] colliders)
        {
            for (int i = 0; i < colliders.Length; i++)
            {
                colliders[i] = null;
            }
        }

        private void ClearRaycasthitBuffer(ref RaycastHit[] raycastHits)
        {
            for (int i = 0; i < raycastHits.Length; i++)
            {
                raycastHits[i] = emptyHit;
            }
        }

        private Material currentMaterial;
        private readonly List<Material> sharedMatsBuffer = new List<Material>(8);
        private readonly Dictionary<Collider, Renderer> rendererCache = new Dictionary<Collider, Renderer>();
        private readonly Dictionary<Collider, Surface> surfaceCache = new Dictionary<Collider, Surface>();
        private const float SlideHapticSeconds = 0.05f;

        private Material leftHandTapMaterial, rightHandTapMaterial;
        private bool leftHandTouching, rightHandTouching;
        private float lastLeftTap, lastRightTap;
        private bool leftTapArmed = true;
        private bool rightTapArmed = true;

        private float leftTapReleaseTime;
        private float rightTapReleaseTime;

        private const float tapReleaseDelay = 0.04f;
        private const float TapRearmDistance = 0.02f;
        private Vector3 lastLeftTapPoint = Vector3.positiveInfinity, lastRightTapPoint = Vector3.positiveInfinity;
        private Vector3 lastLeftTapNormal = Vector3.up, lastRightTapNormal = Vector3.up;

        /// <summary>True once the hand has moved TapRearmDistance away from the tapped surface (along its normal).</summary>
        private static bool HasLiftedOff(Vector3 hand, Vector3 tapPoint, Vector3 tapNormal) =>
            float.IsInfinity(tapPoint.x) || Vector3.Dot(hand - tapPoint, tapNormal.normalized) > TapRearmDistance;
        private float bodyInitialRadius, bodyInitialHeight;
        private RaycastHit bodyHitInfo;
        private Vector3 lastLeftHandPosition, lastRightHandPosition, lastHeadPosition;
        private Vector3[] velocityHistory, slideAverageHistory;
        private int velocityIndex;
        private Vector3 currentVelocity, denormalizedVelocityAverage, lastPosition;

        private bool wasLeftHandTouching, wasRightHandTouching;
        private int currentMaterialIndex;
        private bool leftHandSlide, rightHandSlide;
        private Vector3 leftHandSlideNormal, rightHandSlideNormal;
        private float rightHandSlipPercentage, leftHandSlipPercentage;
        private bool wasLeftHandSlide, wasRightHandSlide;
        private bool didATurn;

        private int leftHandMaterialTouchIndex, rightHandMaterialTouchIndex;
        private Surface leftHandSurfaceOverride, rightHandSurfaceOverride, currentOverride;
        private bool leftHandColliding, rightHandColliding;
        private Vector3 finalPosition, rigidBodyMovement, firstIterationLeftHand, firstIterationRightHand, firstIterationHead;
        private RaycastHit hitInfo;
        private float slipPercentage;
        private Vector3 bodyOffsetVector, distanceTraveled, movementToProjectedAboveCollisionPlane;
        private float lastRealTime, calcDeltaTime, tempRealTime;
        private Vector3 slideAverage, slideAverageNormal;
        private RaycastHit tempHitInfo, junkHit;
        private Vector3 firstPosition;
        private RaycastHit tempIterativeHit;
        private float maxSphereSize1, maxSphereSize2;
        private Collider[] overlapColliders = new Collider[10];
        private int overlapAttempts, touchPoints;
        private float averageSlipPercentage;
        private Vector3 surfaceDirection;
        private float bodyMaxRadius, bodyLerp = 0.17f;
        private bool areBothTouching, didAJump;
        private float slideFactor;
        private RaycastHit[] rayCastNonAllocColliders;
        private Collider[] rayCastNonAllocActuallyColliders;
        private Vector3[] crazyCheckVectors;
        private RaycastHit emptyHit;
        private int bufferCount;
        private Vector3 lastOpenHeadPosition;

        private Vector3 pendingBounceIncomingVelocity;
        private bool pendingBounce;
        private Vector3 pendingBounceNormal;
        private float pendingBouncePercentage;
        private float pendingBounceImpactSpeed;
        private float bounceReleaseTimer = 0f;
    }
}
