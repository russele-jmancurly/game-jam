using GameJam.Core;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace GameJam.Gameplay
{
    /// <summary>
    /// Plays a rigged show character's clips without an Animator Controller: an Idle/Walk locomotion blend driven by
    /// ground speed (walk playback matched to the stride so feet don't skate), with one-shot gestures (WindUp, Slap,
    /// Wave) faded over the top. Added by <see cref="CharacterVisual"/> to the model instance when the character has clips.
    /// </summary>
    public sealed class CharacterAnimator : MonoBehaviour
    {
        private const float LocomotionBlendSpeed = 6f;
        private const float ActionFadeSeconds = 0.12f;

        private PlayableGraph _graph;
        private AnimationMixerPlayable _mixer;
        private AnimationClipPlayable _idle;
        private AnimationClipPlayable _walk;
        private AnimationClipPlayable _action;
        private CharacterDefinition _character;
        private CharacterAction _actionKind;
        private bool _holdAction;
        private float _actionWeight;
        private float _walkWeight;
        private float _speed;
        private float _bodyHeight = 1.6f;
        private bool _ambientWave;
        private float _nextWave;

        public CharacterAction CurrentAction => _actionKind;

        public void Initialize(Animator animator, CharacterDefinition character, float bodyHeight)
        {
            _character = character;
            _bodyHeight = Mathf.Max(bodyHeight, 0.1f);
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;

            _graph = PlayableGraph.Create($"{character.DisplayName} animation");
            _graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
            _mixer = AnimationMixerPlayable.Create(_graph, 3);
            _idle = AnimationClipPlayable.Create(_graph, character.Idle);
            _idle.SetTime(Random.value * character.Idle.length); // crew members don't breathe in sync
            _graph.Connect(_idle, 0, _mixer, 0);
            if (character.Walk != null)
            {
                _walk = AnimationClipPlayable.Create(_graph, character.Walk);
                _graph.Connect(_walk, 0, _mixer, 1);
            }

            AnimationPlayableOutput output = AnimationPlayableOutput.Create(_graph, "Character", animator);
            output.SetSourcePlayable(_mixer);
            ApplyWeights();
            _graph.Play();
        }

        /// <summary>Ground speed in m/s this frame; blends Idle into Walk and sets the walk's playback rate.</summary>
        public void SetSpeed(float metresPerSecond) => _speed = Mathf.Max(metresPerSecond, 0f);

        /// <summary>The MC waves at the yard (or shadowboxes, with that clip) every so often while idle.</summary>
        public void SetAmbientWave(bool enabled)
        {
            _ambientWave = enabled;
            _nextWave = Time.time + Random.Range(3f, 8f);
        }

        /// <summary>
        /// Plays a gesture over the locomotion. Duration (seconds) stretches the clip to fit a gameplay timer: for WindUp
        /// the whole clip, for Slap the part up to the hit. A held gesture stays on its last frame until the next one.
        /// </summary>
        public void Play(CharacterAction action, float duration = 0f, bool hold = false)
        {
            AnimationClip clip = ClipFor(action);
            if (clip == null || !_graph.IsValid())
            {
                return;
            }

            if (_action.IsValid())
            {
                _graph.Disconnect(_mixer, 2);
                _action.Destroy();
            }

            _action = AnimationClipPlayable.Create(_graph, clip);
            _action.SetApplyFootIK(false);
            float natural = action switch
            {
                CharacterAction.Slap => _character.SlapHitTime,
                CharacterAction.Kick => _character.KickHitTime,
                _ => clip.length,
            };
            _action.SetSpeed(duration > 0.01f ? Mathf.Clamp(natural / duration, 0.25f, 4f) : 1f);
            _graph.Connect(_action, 0, _mixer, 2);
            _actionKind = action;
            _holdAction = hold;
        }

        /// <summary>Fades the current gesture out (e.g. a held wind-up that was called off).</summary>
        public void Stop()
        {
            _holdAction = false;
            if (_action.IsValid())
            {
                _action.SetTime(_action.GetAnimationClip().length);
            }
        }

        private void Update()
        {
            if (!_graph.IsValid())
            {
                return;
            }

            float dt = Time.deltaTime;
            float fullWalkSpeed = 0.15f * _bodyHeight;
            float walkTarget = _walk.IsValid() ? Mathf.Clamp01(_speed / fullWalkSpeed) : 0f;
            _walkWeight = Mathf.MoveTowards(_walkWeight, walkTarget, LocomotionBlendSpeed * dt);
            if (_walk.IsValid())
            {
                float strideSpeed = _character.WalkStrideHeights * _bodyHeight;
                _walk.SetSpeed(Mathf.Clamp(_speed / Mathf.Max(strideSpeed, 0.01f), 0.3f, 2.5f));
            }

            float actionTarget = 0f;
            if (_action.IsValid())
            {
                float length = _action.GetAnimationClip().length;
                double time = _action.GetTime();
                bool finished = !_holdAction && time >= length;
                float tail = (float)(length - time) / Mathf.Max((float)_action.GetSpeed(), 0.01f);
                actionTarget = finished ? 0f : _holdAction ? 1f : Mathf.Clamp01(tail / ActionFadeSeconds + 0.0001f);
                if (finished && _actionWeight <= 0.001f)
                {
                    _graph.Disconnect(_mixer, 2);
                    _action.Destroy();
                    _actionKind = CharacterAction.None;
                }
            }

            _actionWeight = Mathf.MoveTowards(_actionWeight, actionTarget, dt / ActionFadeSeconds);
            ApplyWeights();

            if (_ambientWave && !_action.IsValid() && _walkWeight < 0.05f && Time.time >= _nextWave)
            {
                // A character with a Shadowbox clip (the MC) mixes it in with the waves.
                bool box = _character.Shadowbox != null && Random.value < 0.5f;
                Play(box ? CharacterAction.Shadowbox : CharacterAction.Wave);
                _nextWave = Time.time + Random.Range(7f, 14f);
            }
        }

        private void ApplyWeights()
        {
            float locomotion = 1f - _actionWeight;
            _mixer.SetInputWeight(0, locomotion * (1f - _walkWeight));
            _mixer.SetInputWeight(1, _walk.IsValid() ? locomotion * _walkWeight : 0f);
            _mixer.SetInputWeight(2, _action.IsValid() ? _actionWeight : 0f);
        }

        /// <summary>True when the character has a clip for this gesture (the giant falls back to WindUp + Slap without Kick).</summary>
        public bool HasClip(CharacterAction action) => _character != null && ClipFor(action) != null;

        private AnimationClip ClipFor(CharacterAction action) => action switch
        {
            CharacterAction.WindUp => _character.WindUp,
            CharacterAction.Slap => _character.Slap,
            CharacterAction.Wave => _character.Wave,
            CharacterAction.Kick => _character.Kick,
            CharacterAction.Cheer => _character.Cheer,
            CharacterAction.Shadowbox => _character.Shadowbox,
            _ => null,
        };

        private void OnDestroy()
        {
            if (_graph.IsValid())
            {
                _graph.Destroy();
            }
        }
    }
}
