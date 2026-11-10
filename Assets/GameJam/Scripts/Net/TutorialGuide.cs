using GameJam.Core;
using GameJam.Gameplay;
using GameJam.Player;
using UnityEngine;

namespace GameJam.Net
{
    /// <summary>
    /// Runs the tutorial one section at a time (tutorial scene only, local). The burrow is five rooms in a row, each with
    /// one lesson and a closed gate to the next: 1 move around, 2 slap the balloon and put it through the hoop, 3 see what
    /// pops it, 4 grab an item and use one, 5 knock the giant down with a thrown item. Only the current section's goal is
    /// watched. When it is met its sign turns green with a chime and confetti, the gate grinds open, and the balloon and
    /// the respawn point move on to the next room (<see cref="GameState.PracticeCheckpoint"/>). After the last one a fanfare
    /// plays, confetti bursts round the player and the holes back to the yard say so.
    /// </summary>
    public sealed class TutorialGuide : MonoBehaviour
    {
        private const int Move = 0, Slap = 1, Pop = 2, Items = 3, Giant = 4, LessonCount = 5;
        private const float MoveGoalMetres = 8f;
        private const int SlapGoal = 3;
        private const float ItemScanSeconds = 1f;
        private const float NextSectionDelay = 1.6f; // the gate is opening: let the player look before the balloon moves on
        private static readonly Color DoneColor = ArtPalette.Moss;

        [SerializeField] private TextMesh[] _goals = System.Array.Empty<TextMesh>();
        [Tooltip("Where each lesson's confetti goes off (its sign).")]
        [SerializeField] private Transform[] _signs = System.Array.Empty<Transform>();
        [Tooltip("Gate i closes section i off from section i + 1 and opens when lesson i is done.")]
        [SerializeField] private SectionGate[] _gates = System.Array.Empty<SectionGate>();
        [SerializeField] private AudioSource _chime;
        [SerializeField] private AudioSource _fanfare;
        [Tooltip("The lines over the holes back to the yard; rewritten when every lesson is done.")]
        [SerializeField] private TextMesh[] _exitHints = System.Array.Empty<TextMesh>();

        private NetworkItem[] _items = System.Array.Empty<NetworkItem>();
        private float _nextItemScan;
        private int _section;
        private float _readyAt;       // the current section starts counting from here
        private bool _isArmed;        // baselines taken for the current section
        private int _shownProgress = -1;
        private Vector3 _lastHead;
        private bool _hasLastHead;
        private float _moved;
        private int _hitsAtStart;
        private int _popsAtStart;
        private bool _hoopCleared;
        private bool _grabbed;
        private bool _used;

        /// <summary>The section the player is in: 0..4, or 5 when the tutorial is finished.</summary>
        public int Section => _section;

        public int LessonsDone => _section;

        public bool IsDone(int lesson) => lesson >= 0 && lesson < _section;

        public void Configure(TextMesh[] goals, Transform[] signs, SectionGate[] gates, AudioSource chime, AudioSource fanfare, TextMesh[] exitHints)
        {
            _goals = goals;
            _signs = signs;
            _gates = gates;
            _chime = chime;
            _fanfare = fanfare;
            _exitHints = exitHints;
        }

        private void Update()
        {
            if (_section >= LessonCount || Time.time < _readyAt)
            {
                return;
            }

            NetworkBalloon balloon = NetworkBalloon.Instance;
            bool hasBalloon = balloon != null && balloon.Object != null && balloon.Object.IsValid;
            if (!_isArmed)
            {
                if (_section != Move && !hasBalloon)
                {
                    return; // still spawning
                }

                Arm(balloon);
            }

            switch (_section)
            {
                case Move:
                    TrackMovement();
                    int metres = Mathf.Min((int)_moved, (int)MoveGoalMetres);
                    Show(metres, metres >= (int)MoveGoalMetres);
                    break;
                case Slap:
                    GameState state = GameState.Instance;
                    _hoopCleared |= state != null && state.HoopsCleared > 0;
                    int slaps = hasBalloon ? Mathf.Clamp(balloon.Hits - _hitsAtStart, 0, SlapGoal) : 0;
                    Show(slaps * 2 + (_hoopCleared ? 1 : 0), slaps >= SlapGoal && _hoopCleared);
                    break;
                case Pop:
                    bool popped = hasBalloon && balloon.PopCount > _popsAtStart;
                    Show(0, popped);
                    break;
                case Items:
                    TrackItems();
                    Show((_grabbed ? 1 : 0) + (_used ? 2 : 0), _grabbed && _used);
                    break;
                default:
                    Show(0, IsGiantDown());
                    break;
            }
        }

        /// <summary>The section starts counting: the balloon comes to this room's station and the counters start from now.</summary>
        private void Arm(NetworkBalloon balloon)
        {
            _isArmed = true;
            _shownProgress = -1;
            if (balloon != null)
            {
                if (_section > Slap)
                {
                    balloon.ResetTo(CourseLayout.BalloonSpawn(_section)); // sections 0 and 1 share the first station
                }

                _hitsAtStart = balloon.Hits;
                _popsAtStart = balloon.PopCount;
            }
        }

        private void TrackMovement()
        {
            LocalRig rig = LocalRig.Instance;
            if (rig == null)
            {
                return;
            }

            Vector3 head = rig.Head.position;
            if (_hasLastHead)
            {
                Vector3 step = head - _lastHead;
                step.y = 0f;
                float distance = step.magnitude;
                if (distance < 1f) // a bigger jump in one frame is a respawn, not movement
                {
                    _moved += distance;
                }
            }

            _lastHead = head;
            _hasLastHead = true;
        }

        private void TrackItems()
        {
            if (Time.time >= _nextItemScan)
            {
                _nextItemScan = Time.time + ItemScanSeconds;
                _items = FindObjectsByType<NetworkItem>(FindObjectsSortMode.None);
            }

            foreach (NetworkItem item in _items)
            {
                if (item == null || item.Object == null || !item.Object.IsValid)
                {
                    continue;
                }

                _grabbed |= item.IsHeld;
                _used |= item.IsSpent;
            }
        }

        private static bool IsGiantDown()
        {
            foreach (MischiefMaker giant in MischiefMaker.Active)
            {
                if (giant != null && giant.Object != null && giant.Object.IsValid && giant.IsKnockedDown)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Rewrites the current lesson's goal line only when its progress changed (no per-frame strings).</summary>
        private void Show(int progress, bool complete)
        {
            if (complete)
            {
                Complete();
                return;
            }

            if (_shownProgress == progress || _section >= _goals.Length || _goals[_section] == null)
            {
                return;
            }

            _shownProgress = progress;
            _goals[_section].text = _section switch
            {
                Move => $"MOVE AROUND: {progress} / {(int)MoveGoalMetres} m",
                Slap => $"SLAPS {progress / 2} / {SlapGoal}   ·   HOOP {(progress % 2 == 1 ? "CLEARED" : "0 / 1")}",
                Pop => "POP IT ONCE TO SEE WHAT HAPPENS",
                Items => $"GRAB ONE {(_grabbed ? "OK" : "0 / 1")}   ·   USE ONE {(_used ? "OK" : "0 / 1")}",
                _ => "KNOCK THE GIANT DOWN: 0 / 1",
            };
        }

        private void Complete()
        {
            int lesson = _section;
            if (lesson < _goals.Length && _goals[lesson] != null)
            {
                _goals[lesson].text = lesson < LessonCount - 1 ? "DONE! THE GATE IS OPEN" : "DONE!";
                _goals[lesson].color = DoneColor;
            }

            if (_chime != null)
            {
                _chime.Play();
            }

            if (lesson < _signs.Length && _signs[lesson] != null)
            {
                BurstEffects.Confetti(_signs[lesson].position - _signs[lesson].forward * 0.4f);
            }

            if (lesson < _gates.Length && _gates[lesson] != null)
            {
                _gates[lesson].Open();
            }

            _section++;
            _isArmed = false;
            if (_section >= LessonCount)
            {
                Celebrate();
                return;
            }

            // Falling out of the map or a pop now comes back to the next room.
            GameState.PracticeCheckpoint = _section;
            _readyAt = Time.time + (_section > Slap ? NextSectionDelay : 0f);
        }

        private void Celebrate()
        {
            if (_fanfare != null)
            {
                _fanfare.Play();
            }

            foreach (TextMesh hint in _exitHints)
            {
                if (hint != null)
                {
                    hint.text = "ALL DONE! JUMP IN TO PLAY";
                    hint.color = DoneColor;
                }
            }

            LocalRig rig = LocalRig.Instance;
            if (rig != null)
            {
                Transform head = rig.Head;
                BurstEffects.BigConfetti(head.position + head.forward * 2f + Vector3.up * 1.2f, 90, 0.09f);
                BurstEffects.BigConfetti(head.position + head.forward * 2.5f + head.right * 1.5f + Vector3.up, 60, 0.09f);
                BurstEffects.BigConfetti(head.position + head.forward * 2.5f - head.right * 1.5f + Vector3.up, 60, 0.09f);
            }
        }
    }
}
