using UnityEngine;

namespace GameJam.Gameplay
{
    /// <summary>
    /// Billboard for floating labels (text that isn't painted on a sign or a wall): turns to face the player's view every
    /// frame, so it never reads edge-on or mirrored from the side or from behind. It only turns about the vertical axis,
    /// so the text stays upright however the head tilts. Text on boards and walls doesn't get this: it belongs to its
    /// surface.
    /// </summary>
    public sealed class FaceCamera : MonoBehaviour
    {
        private Transform _view;

        private void LateUpdate()
        {
            if (_view == null)
            {
                Camera main = Camera.main;
                if (main == null)
                {
                    return;
                }

                _view = main.transform;
            }

            // A TextMesh reads from the side its forward axis points away from, so look along the line from the viewer.
            Vector3 away = transform.position - _view.position;
            away.y = 0f;
            if (away.sqrMagnitude < 0.0001f)
            {
                return;
            }

            transform.rotation = Quaternion.LookRotation(away, Vector3.up);
        }
    }
}
