using UnityEngine;

namespace GameJam.Net
{
    /// <summary>
    /// Helps every player find the balloon (local only, nothing networked), only while it waits to be hit (stationed: at
    /// the start, after a reset or a pop): a bobbing arrow marks it, and when it is
    /// out of your view an arrow at the edge of your view points at it, with its distance, so after a pop you can turn
    /// straight to where it respawned. The first hit puts it in play and everything disappears. Hidden in the lobby.
    /// </summary>
    public sealed class BalloonLocator : MonoBehaviour
    {
        [SerializeField] private NetworkBalloon _balloon;
        [Tooltip("The balloon's visuals (they move ahead of the network on a predicted hit).")]
        [SerializeField] private Transform _view;
        [Tooltip("Bobbing arrow over the balloon, shown while it waits to be hit.")]
        [SerializeField] private GameObject _beacon;
        [Tooltip("Edge-of-view pointer: a root that is placed in front of the camera, with the arrow and distance label under it.")]
        [SerializeField] private Transform _pointer;
        [SerializeField] private Transform _pointerArrow;
        [SerializeField] private TextMesh _distanceLabel;

        [Header("Pointer")]
        [SerializeField] private float _pointerDistance = 0.9f;
        [Tooltip("How far from the middle of your view the pointer sits, toward the balloon (metres at Pointer Distance).")]
        [SerializeField] private float _edgeOffset = 0.26f;
        [Tooltip("The pointer hides once the balloon is within this angle of where you look (and whenever it is in play).")]
        [SerializeField] private float _hiddenWithinDegrees = 30f;
        private int _shownMetres = -1;

        public void Configure(NetworkBalloon balloon, Transform view, GameObject beacon, Transform pointer, Transform pointerArrow, TextMesh distanceLabel)
        {
            _balloon = balloon;
            _view = view;
            _beacon = beacon;
            _pointer = pointer;
            _pointerArrow = pointerArrow;
            _distanceLabel = distanceLabel;
        }

        private void Awake()
        {
            if (_pointer != null)
            {
                _pointer.SetParent(null, true); // placed in front of the camera every frame, not carried by the balloon
                _pointer.gameObject.SetActive(false);
            }

            if (_beacon != null)
            {
                _beacon.SetActive(false);
            }
        }

        private void OnDestroy()
        {
            if (_pointer != null)
            {
                Destroy(_pointer.gameObject);
            }
        }

        private void LateUpdate()
        {
            bool isSpawned = _balloon != null && _balloon.Object != null && _balloon.Object.IsValid;
            bool isShown = isSpawned && (GameState.Instance == null || GameState.Instance.IsBalloonShown);
            bool isWaiting = isShown && _balloon.IsStationed;
            if (_beacon != null && _beacon.activeSelf != isWaiting)
            {
                _beacon.SetActive(isWaiting);
            }

            Camera camera = Camera.main;
            if (_pointer == null || camera == null)
            {
                return;
            }

            Transform head = camera.transform;
            Vector3 target = _view != null ? _view.position : _balloon != null ? _balloon.transform.position : head.position;
            Vector3 toBalloon = target - head.position;
            float distance = toBalloon.magnitude;
            bool isOutOfView = Vector3.Angle(head.forward, toBalloon) > _hiddenWithinDegrees;
            bool showPointer = isWaiting && isOutOfView; // only while it waits to be hit: once it is in play, everything goes
            if (_pointer.gameObject.activeSelf != showPointer)
            {
                _pointer.gameObject.SetActive(showPointer);
            }

            if (!showPointer)
            {
                return;
            }

            // Sit toward the balloon's side of the view (straight down when it is right behind you), pointing at it.
            Vector3 local = head.InverseTransformDirection(toBalloon);
            var side = new Vector2(local.x, local.y);
            side = side.sqrMagnitude > 0.0001f ? side.normalized : Vector2.down;
            Vector3 position = head.position + head.forward * _pointerDistance + (head.right * side.x + head.up * side.y) * _edgeOffset;
            _pointer.SetPositionAndRotation(position, Quaternion.LookRotation(position - head.position, head.up));
            if (_pointerArrow != null)
            {
                _pointerArrow.rotation = Quaternion.LookRotation(target - position, head.up);
                _pointerArrow.localScale = Vector3.one * (1f + 0.1f * Mathf.Sin(Time.time * 7f)); // a gentle pulse draws the eye
            }

            int metres = Mathf.RoundToInt(distance);
            if (_distanceLabel != null && metres != _shownMetres)
            {
                _shownMetres = metres;
                _distanceLabel.text = $"BALLOON {metres} m";
            }
        }
    }
}
