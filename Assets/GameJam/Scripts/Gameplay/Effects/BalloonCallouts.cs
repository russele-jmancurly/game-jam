using GameJam.Core;
using UnityEngine;

namespace GameJam.Gameplay
{
    /// <summary>
    /// Local-only feedback over the balloon: a "SAVE!" pop-up with a chime and confetti when someone rescues it just above
    /// the ground, and the rally number's drama (it grows and changes colour every <see cref="BalloonConfig.RallyMilestoneStep"/>
    /// passes with the critters going "ooh", punches on every pass, and droops away grey with a sad trombone when a long rally breaks).
    /// NetworkBalloon tells it what happened; it never touches the network or the balloon's flight.
    /// </summary>
    public sealed class BalloonCallouts : MonoBehaviour
    {
        private const float SaveSeconds = 1.3f;
        private const float SaveHeightAbove = 0.6f;
        private const float SaveRise = 0.45f;
        private const float RallyHeightAbove = 0.5f;
        private const float BreakSeconds = 1.4f;
        private const float BreakDrop = 0.35f;
        private const float PassPunch = 0.25f;
        private const float MilestonePunch = 0.9f;
        private const float TierGrowth = 0.3f;
        private const int MaxTier = 3;
        private const float MilestonePitchStep = 0.06f;
        private static readonly Vector3 ShadowOffset = new Vector3(0.024f, -0.024f, 0.01f); // right, down, behind (text space)

        [SerializeField] private BalloonConfig _config;
        [Tooltip("The balloon's visuals; the texts float above it in world space (so a spinning balloon never tilts them).")]
        [SerializeField] private Transform _anchor;
        [SerializeField] private TextMesh _rallyText;
        [SerializeField] private TextMesh _saveText;
        [Tooltip("2D source for the cues: everyone hears them the same wherever they are.")]
        [SerializeField] private AudioSource _cueAudio;
        [SerializeField] private AudioClip _saveClip;
        [SerializeField] private AudioClip _milestoneClip;
        [SerializeField] private AudioClip _breakClip;

        private int _shownRally;
        private float _punch;
        private float _punchTime = -10f;
        private float _saveTime = -10f;
        private float _breakTime = -10f;
        private int _brokenRally;
        private string _rallyLabel = string.Empty;
        private string _brokenLabel = string.Empty;
        private Vector3 _rallyBaseScale = Vector3.one;
        private Vector3 _saveBaseScale = Vector3.one;
        private TextMesh _rallyShadow;
        private TextMesh _saveShadow;

        /// <summary>When the last save pop-up started (Time.time), for tests.</summary>
        public float LastSaveTime => _saveTime;

        /// <summary>When the last broken-rally trombone played (Time.time), for tests.</summary>
        public float LastBreakTime => _breakTime;

        /// <summary>When the last rally milestone played (Time.time), for tests.</summary>
        public float LastMilestoneTime { get; private set; } = -10f;

        public void Configure(BalloonConfig config, Transform anchor, TextMesh rallyText, TextMesh saveText, AudioSource cueAudio,
            AudioClip saveClip, AudioClip milestoneClip, AudioClip breakClip)
        {
            _config = config;
            _anchor = anchor;
            _rallyText = rallyText;
            _saveText = saveText;
            _cueAudio = cueAudio;
            _saveClip = saveClip;
            _milestoneClip = milestoneClip;
            _breakClip = breakClip;
        }

        private void Awake()
        {
            CritterCrowd.LookTarget = _anchor != null ? _anchor : transform; // the critter audience watches the balloon
            if (_rallyText != null)
            {
                _rallyBaseScale = _rallyText.transform.localScale;
                _rallyShadow = CreateShadow(_rallyText);
            }

            if (_saveText != null)
            {
                _saveBaseScale = _saveText.transform.localScale;
                _saveText.text = string.Empty;
                _saveShadow = CreateShadow(_saveText);
            }
        }

        /// <summary>
        /// A dark copy just behind and below the text, so a bright number still reads against sky, white fence or sun. The
        /// text shader ignores depth, so draw order comes from sortingOrder (shadow first).
        /// </summary>
        private static TextMesh CreateShadow(TextMesh main)
        {
            var holder = new GameObject(main.name + "Shadow");
            holder.layer = main.gameObject.layer;
            holder.transform.SetParent(main.transform, false);
            holder.transform.localPosition = ShadowOffset;
            var shadow = holder.AddComponent<TextMesh>();
            shadow.font = main.font;
            shadow.fontSize = main.fontSize;
            shadow.fontStyle = main.fontStyle;
            shadow.characterSize = main.characterSize;
            shadow.anchor = main.anchor;
            shadow.alignment = main.alignment;
            shadow.color = ArtPalette.TextDark;
            shadow.text = string.Empty;
            var mainRenderer = main.GetComponent<MeshRenderer>();
            var shadowRenderer = holder.GetComponent<MeshRenderer>();
            shadowRenderer.sharedMaterial = mainRenderer.sharedMaterial;
            shadowRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            shadowRenderer.sortingOrder = mainRenderer.sortingOrder;
            mainRenderer.sortingOrder = shadowRenderer.sortingOrder + 1;
            return shadow;
        }

        /// <summary>Someone saved it just above the ground: "SAVE!", a chime and a pinch of confetti.</summary>
        public void PlaySave(Vector3 position)
        {
            _saveTime = Time.time;
            if (_saveText != null)
            {
                SetText(_saveText, _saveShadow, "SAVE!");
            }

            Play(_saveClip, 1f, 1f);
            BurstEffects.Confetti(position);
            CritterCrowd.Cheer(position, 2);
        }

        /// <summary>
        /// The current rally, every frame. <paramref name="announce"/> is false while the change is a reset rather than
        /// play (lobby, a pop, the balloon put back on its station): the number just updates quietly then.
        /// </summary>
        public void ShowRally(int rally, bool announce)
        {
            if (rally == _shownRally)
            {
                return;
            }

            int previous = _shownRally;
            _shownRally = rally;
            _rallyLabel = rally > 1 ? rally.ToString() : string.Empty; // built once per change, not every frame
            if (!announce || _config == null)
            {
                return;
            }

            int step = Mathf.Max(2, _config.RallyMilestoneStep);
            if (rally > previous)
            {
                bool milestone = rally % step == 0;
                Punch(milestone ? MilestonePunch : PassPunch);
                if (milestone)
                {
                    LastMilestoneTime = Time.time;
                    int tier = rally / step;
                    Play(_milestoneClip, 0.9f, 1f + MilestonePitchStep * (Mathf.Min(tier, MaxTier + 1) - 1));
                    if (_anchor != null)
                    {
                        BurstEffects.Confetti(_anchor.position + Vector3.up * RallyHeightAbove);
                        CritterCrowd.Cheer(_anchor.position, 1);
                    }
                }
            }
            else if (rally == 0 && previous >= _config.RallyBreakMinimum)
            {
                _brokenRally = previous;
                _brokenLabel = previous.ToString();
                _breakTime = Time.time;
                Play(_breakClip, 0.85f, 1f);
            }
        }

        private void Punch(float amount)
        {
            _punch = amount;
            _punchTime = Time.time;
        }

        private void Play(AudioClip clip, float volume, float pitch)
        {
            if (_cueAudio != null && clip != null)
            {
                _cueAudio.pitch = pitch;
                _cueAudio.PlayOneShot(clip, volume);
            }
        }

        private void LateUpdate()
        {
            Camera view = Camera.main;
            UpdateRallyText(view);
            UpdateSaveText(view);
        }

        private void UpdateRallyText(Camera view)
        {
            if (_rallyText == null || _anchor == null)
            {
                return;
            }

            float sinceBreak = Time.time - _breakTime;
            bool showingBreak = _shownRally == 0 && sinceBreak < BreakSeconds;
            Transform text = _rallyText.transform;
            if (showingBreak)
            {
                // The lost number droops, shrinks and fades in grey.
                float t = sinceBreak / BreakSeconds;
                SetText(_rallyText, _rallyShadow, _brokenLabel);
                Color grey = Color.Lerp(ArtPalette.Steel, ArtPalette.Charcoal, t);
                grey.a = 1f - t * t;
                SetColor(_rallyText, _rallyShadow, grey);
                text.position = _anchor.position + Vector3.up * (RallyHeightAbove - BreakDrop * t * t);
                text.localScale = _rallyBaseScale * (TierScale(_brokenRally) * (1f - 0.4f * t));
            }
            else
            {
                SetText(_rallyText, _rallyShadow, _rallyLabel);
                int tier = Tier(_shownRally);
                Color color = TierColor(tier);
                float since = Time.time - _punchTime;
                float punch = _punch * Mathf.Exp(-since * 7f) * Mathf.Cos(since * 22f);
                float pulse = tier >= MaxTier ? 0.06f * Mathf.Sin(Time.time * 9f) : 0f; // a long rally throbs
                if (since < 0.15f && _punch >= MilestonePunch)
                {
                    color = Color.Lerp(color, Color.white, 1f - since / 0.15f); // a flash on the milestone
                }

                SetColor(_rallyText, _rallyShadow, color);
                text.position = _anchor.position + Vector3.up * RallyHeightAbove;
                text.localScale = _rallyBaseScale * (TierScale(_shownRally) * (1f + punch + pulse));
            }

            Face(text, view);
        }

        private void UpdateSaveText(Camera view)
        {
            if (_saveText == null || _anchor == null)
            {
                return;
            }

            float since = Time.time - _saveTime;
            if (since >= SaveSeconds)
            {
                SetText(_saveText, _saveShadow, string.Empty);
                return;
            }

            float t = since / SaveSeconds;
            // Pops in big, settles, rises and fades in the last third.
            float pop = since < 0.12f ? since / 0.12f * 1.35f : Mathf.Lerp(1.35f, 1f, Mathf.Clamp01((since - 0.12f) / 0.2f));
            Color color = ArtPalette.Accent;
            color.a = t < 0.66f ? 1f : 1f - (t - 0.66f) / 0.34f;
            SetColor(_saveText, _saveShadow, color);
            Transform text = _saveText.transform;
            text.position = _anchor.position + Vector3.up * (SaveHeightAbove + SaveRise * t);
            text.localScale = _saveBaseScale * pop;
            Face(text, view);
        }

        private int Tier(int rally) => _config == null ? 0 : Mathf.Min(rally / Mathf.Max(2, _config.RallyMilestoneStep), MaxTier);

        private float TierScale(int rally) => 1f + TierGrowth * Tier(rally);

        private static Color TierColor(int tier) => tier switch
        {
            0 => Color.white,
            1 => ArtPalette.Accent,
            2 => ArtPalette.Climbable,
            _ => ArtPalette.Hazard,
        };

        private static void Face(Transform text, Camera view)
        {
            if (view != null)
            {
                text.rotation = Quaternion.LookRotation(text.position - view.transform.position);
            }
        }

        private static void SetText(TextMesh target, TextMesh shadow, string value)
        {
            if (target.text != value)
            {
                target.text = value;
                if (shadow != null)
                {
                    shadow.text = value;
                }
            }
        }

        private static void SetColor(TextMesh target, TextMesh shadow, Color color)
        {
            target.color = color;
            if (shadow != null)
            {
                Color dark = ArtPalette.TextDark;
                dark.a = color.a * 0.85f;
                shadow.color = dark;
            }
        }
    }
}
