using UnityEngine;

namespace GameJam.Gameplay
{
    /// <summary>
    /// The MC (Gonzales on the headline board) joins in whenever the critter audience cheers (a save, a rally milestone,
    /// a cleared hoop, the finish: <see cref="CritterCrowd.Cheered"/>): he punches both fists into the air (the Cheer
    /// clip). Local only, like the crowd. Without a rigged model or a Cheer clip it does nothing.
    /// </summary>
    public sealed class McReactions : MonoBehaviour
    {
        [Tooltip("The MC's figure; its animator plays the cheer.")]
        [SerializeField] private CharacterVisual _visual;
        [Tooltip("Shortest time between two cheers (seconds), so a burst of events is one cheer, not a stutter.")]
        [SerializeField] private float _minGapSeconds = 1.5f;

        private float _lastCheer = -10f;

        public void Configure(CharacterVisual visual) => _visual = visual;

        private void OnEnable() => CritterCrowd.Cheered += OnCheered;

        private void OnDisable() => CritterCrowd.Cheered -= OnCheered;

        private void OnCheered(Vector3? where, int hops)
        {
            CharacterAnimator animator = _visual != null ? _visual.Animation : null;
            if (animator == null || !animator.HasClip(CharacterAction.Cheer) || Time.time - _lastCheer < _minGapSeconds)
            {
                return;
            }

            _lastCheer = Time.time;
            animator.Play(CharacterAction.Cheer);
        }
    }
}
