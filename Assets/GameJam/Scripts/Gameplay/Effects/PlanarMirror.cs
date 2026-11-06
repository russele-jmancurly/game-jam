using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace GameJam.Gameplay
{
    /// <summary>
    /// A flat mirror on a Unity Quad (its visible face looks along -Z). A second camera stands at the viewer's eye reflected
    /// through the mirror plane and renders, through the quad as a window (an off-axis frustum whose near plane is the
    /// mirror, so nothing behind it gets in), into a RenderTexture; the quad shows it flipped left-right. It only renders
    /// while the viewer is close and in front, and shows the local player's own body, which is hidden from their eyes.
    /// </summary>
    [RequireComponent(typeof(Renderer))]
    public sealed class PlanarMirror : MonoBehaviour
    {
        [Tooltip("RenderTexture width in pixels (height follows the mirror's aspect).")]
        [SerializeField] private int _textureWidth = 512;
        [Tooltip("The mirror renders only while the viewer is within this distance.")]
        [SerializeField] private float _activeDistance = 10f;
        [SerializeField] private float _farClip = 60f;

        private static readonly int BaseMap = Shader.PropertyToID("_BaseMap");
        private static readonly int MainTex = Shader.PropertyToID("_MainTex");

        private readonly List<Renderer> _shownForMirror = new List<Renderer>();
        private readonly Plane[] _viewerPlanes = new Plane[6];
        private Camera _mirrorCamera;
        private RenderTexture _texture;
        private Renderer _surface;

        private void OnEnable()
        {
            _surface = GetComponent<Renderer>();
            Vector3 size = transform.lossyScale;
            int height = Mathf.Clamp(Mathf.RoundToInt(_textureWidth * Mathf.Abs(size.y / Mathf.Max(0.01f, size.x))), 16, 2048);
            _texture = new RenderTexture(_textureWidth, height, 24) { name = "MirrorTexture", antiAliasing = 2 };

            var cameraObject = new GameObject("MirrorCamera") { hideFlags = HideFlags.DontSave };
            cameraObject.transform.SetParent(transform, false);
            _mirrorCamera = cameraObject.AddComponent<Camera>();
            _mirrorCamera.enabled = false;
            _mirrorCamera.targetTexture = _texture;
            _mirrorCamera.stereoTargetEye = StereoTargetEyeMask.None; // never draw to the headset
            _mirrorCamera.depth = -50f; // before the player's camera
            _mirrorCamera.clearFlags = CameraClearFlags.Skybox;

            Material material = _surface.material; // per-mirror instance
            material.SetTexture(BaseMap, _texture);
            material.SetTexture(MainTex, _texture);
            // Seen through a mirror, left and right swap.
            material.SetTextureScale(BaseMap, new Vector2(-1f, 1f));
            material.SetTextureOffset(BaseMap, new Vector2(1f, 0f));
            material.SetTextureScale(MainTex, new Vector2(-1f, 1f));
            material.SetTextureOffset(MainTex, new Vector2(1f, 0f));

            RenderPipelineManager.beginCameraRendering += OnBeginCamera;
            RenderPipelineManager.endCameraRendering += OnEndCamera;
        }

        private void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= OnBeginCamera;
            RenderPipelineManager.endCameraRendering -= OnEndCamera;
            if (_mirrorCamera != null)
            {
                Destroy(_mirrorCamera.gameObject);
            }

            if (_texture != null)
            {
                _texture.Release();
                Destroy(_texture);
            }
        }

        private void LateUpdate()
        {
            Camera viewer = Camera.main;
            if (viewer == null || _mirrorCamera == null)
            {
                return;
            }

            Vector3 eye = viewer.transform.position;
            Vector3 center = transform.position;
            Vector3 front = -transform.forward; // the quad's visible side
            float inFront = Vector3.Dot(eye - center, front);
            // Renderer.isVisible would count the mirror camera itself (the glass lies on its near plane): test the viewer's
            // own frustum instead, so it stops rendering when nobody looks at it.
            bool isActive = inFront > 0.05f && (eye - center).sqrMagnitude < _activeDistance * _activeDistance && IsSeenBy(viewer);
            _mirrorCamera.enabled = isActive;
            if (isActive)
            {
                PlaceCamera(eye, center, front, inFront);
            }
        }

        private bool IsSeenBy(Camera viewer)
        {
            GeometryUtility.CalculateFrustumPlanes(viewer, _viewerPlanes);
            return GeometryUtility.TestPlanesAABB(_viewerPlanes, _surface.bounds);
        }

        /// <summary>Generalised perspective projection (Kooima) from the reflected eye through the mirror quad.</summary>
        private void PlaceCamera(Vector3 eye, Vector3 center, Vector3 front, float distance)
        {
            Vector3 reflectedEye = eye - 2f * distance * front;

            // Corners as seen from behind the mirror (its right is the quad's left): lower-left, lower-right, upper-left.
            Vector3 lowerLeft = transform.TransformPoint(new Vector3(0.5f, -0.5f, 0f));
            Vector3 lowerRight = transform.TransformPoint(new Vector3(-0.5f, -0.5f, 0f));
            Vector3 upperLeft = transform.TransformPoint(new Vector3(0.5f, 0.5f, 0f));
            Vector3 right = (lowerRight - lowerLeft).normalized;
            Vector3 up = (upperLeft - lowerLeft).normalized;
            Vector3 towardEye = -front; // from the plane toward the reflected eye

            Vector3 toLowerLeft = lowerLeft - reflectedEye;
            Vector3 toLowerRight = lowerRight - reflectedEye;
            Vector3 toUpperLeft = upperLeft - reflectedEye;
            float near = Mathf.Max(0.01f, -Vector3.Dot(toLowerLeft, towardEye)); // the near plane lies on the mirror

            float left = Vector3.Dot(right, toLowerLeft);
            float rightEdge = Vector3.Dot(right, toLowerRight);
            float bottom = Vector3.Dot(up, toLowerLeft);
            float top = Vector3.Dot(up, toUpperLeft);

            _mirrorCamera.transform.SetPositionAndRotation(reflectedEye, Quaternion.LookRotation(-towardEye, up));
            _mirrorCamera.nearClipPlane = near;
            _mirrorCamera.farClipPlane = near + _farClip;
            _mirrorCamera.projectionMatrix = Matrix4x4.Frustum(left, rightEdge, bottom, top, near, near + _farClip);
        }

        /// <summary>For the mirror's render only, shows the bodies hidden from their own player's eyes.</summary>
        private void OnBeginCamera(ScriptableRenderContext context, Camera rendering)
        {
            if (rendering != _mirrorCamera)
            {
                return;
            }

            _shownForMirror.Clear();
            foreach (GorillaBodyRig body in GorillaBodyRig.All)
            {
                // Only the local player's own body (hidden from their eyes). Other hidden bodies are knocked-down players
                // shown as a ragdoll: leave those hidden, or the mirror shows them twice.
                if (body.IsLocalPlayer)
                {
                    Show(body.BodyRenderer);
                    Show(body.ArmsRenderer);
                }
            }
        }

        private void Show(Renderer part)
        {
            if (part != null && !part.enabled)
            {
                part.enabled = true;
                _shownForMirror.Add(part);
            }
        }

        private void OnEndCamera(ScriptableRenderContext context, Camera rendering)
        {
            if (rendering != _mirrorCamera)
            {
                return;
            }

            foreach (Renderer part in _shownForMirror)
            {
                if (part != null)
                {
                    part.enabled = false;
                }
            }

            _shownForMirror.Clear();
        }
    }
}
