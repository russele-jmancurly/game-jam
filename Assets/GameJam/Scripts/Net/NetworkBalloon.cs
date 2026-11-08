using Fusion;
using GameJam.Core;
using GameJam.Gameplay;
using GameJam.Player;
using UnityEngine;

namespace GameJam.Net
{
    /// <summary>
    /// The networked balloon, a Rigidbody synced with Fusion 2.1 Forecast Physics: every client extrapolates it into its
    /// own local time, so a slap lands where the balloon visibly is instead of where it was a round trip ago.
    /// Its state authority (the master client, via the Master Client Object flag) applies the game rules from
    /// <see cref="BalloonPhysics"/> in FixedUpdate; everyone else sends hits through an RPC, and the first hit the
    /// authority receives wins. Enforces the pass rule: the same player can never hit it twice in a row.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public sealed class NetworkBalloon : NetworkBehaviour, ISlappable
    {
        private const float WobbleSpeed = 6f;
        private const float WobbleAmount = 0.03f;
        private const int SendEveryTick = 1;
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [SerializeField] private BalloonConfig _config;
        [SerializeField] private SlapConfig _slapConfig;
        [SerializeField] private Renderer _visual;
        [SerializeField] private TextMesh _rallyText;
        [SerializeField] private Material _effectMaterial;
        [Tooltip("Plays the hit sound at the balloon (3D).")]
        [SerializeField] private AudioSource _hitAudio;
        [Tooltip("Slap sounds; one is picked at random per hit.")]
        [SerializeField] private AudioClip[] _hitClips = System.Array.Empty<AudioClip>();
        [Tooltip("Played where the balloon pops.")]
        [SerializeField] private AudioClip _popClip;

        private Rigidbody _body;
        private NetworkTransform _networkTransform;
        private BalloonPhysics _physics;
        private Vector3 _baseVisualScale;
        private MaterialPropertyBlock _colorBlock;
        private Transform _string;
        private Vector3 _stringRest;
        private float _lastHitTime = -10f;
        private float _lastHitStrength;
        private Vector3 _lastHitDirection = Vector3.up;

        public static NetworkBalloon Instance { get; private set; }

        [Networked] public PlayerRef LastHitter { get; private set; }

        [Networked] public BalloonCondition Condition { get; private set; }

        [Networked] public int Rally { get; private set; }

        [Networked] public int BestRally { get; private set; }

        [Networked] public int PopCount { get; private set; }

        [Networked] private TickTimer LeakCooldown { get; set; }

        [Networked] private TickTimer HitGrace { get; set; }

        /// <summary>
        /// Waiting at its spawn point (after spawning, a reset or a pop): it hovers there with a gentle bob instead of
        /// dropping, until the first hit sends it off.
        /// </summary>
        [Networked] public NetworkBool IsStationed { get; private set; }

        [Networked] private Vector3 StationPoint { get; set; }

        /// <summary>The balloon's current velocity (synced by the NetworkTransform's physics state).</summary>
        public Vector3 Velocity => _body != null ? _body.linearVelocity : Vector3.zero;

        /// <summary>
        /// True while a lone player is testing: the pass rule (no double hits) is off so one person can play the whole
        /// loop. Turns itself back on as soon as a second player joins.
        /// </summary>
        public bool IsPassRuleSuspended => _config.AllowSoloDoubleHits && CountPlayers() < 2;

        /// <summary>The local player hit it last and the pass rule is on: their next touch would pop it.</summary>
        public bool IsNotLocalPlayersTurn =>
            Runner != null && LastHitter != PlayerRef.None && LastHitter == Runner.LocalPlayer && !IsPassRuleSuspended;

        /// <summary>The balloon's flight tuning (items use it to aim a hit through a hoop).</summary>
        public BalloonConfig Config => _config;

        private bool IsLive => GameState.Instance == null || GameState.Instance.IsBalloonLive;

        /// <summary>Hidden in the lobby: it shows up on its station when the red button is slapped.</summary>
        private bool IsShown => GameState.Instance == null || GameState.Instance.IsBalloonShown;

        private Renderer[] _renderers = System.Array.Empty<Renderer>();
        private Collider[] _colliders = System.Array.Empty<Collider>();
        private int _appliedShown = -1;

        private bool IsReady => Object != null && Object.IsValid;

        public void ConfigurePopSound(AudioClip popClip) => _popClip = popClip;

        public void Configure(BalloonConfig config, SlapConfig slapConfig, Renderer visual, TextMesh rallyText, Material effectMaterial,
            AudioSource hitAudio, AudioClip[] hitClips)
        {
            _hitAudio = hitAudio;
            _hitClips = hitClips;
            _config = config;
            _slapConfig = slapConfig;
            _visual = visual;
            _rallyText = rallyText;
            _effectMaterial = effectMaterial;
        }

        public bool TryReceiveSlap(in SlapContext slap, out HapticPulse feedback)
        {
            feedback = HapticPulse.None;
            float speed = slap.Speed;
            if (speed < _slapConfig.MinBalloonSlapSpeed || !IsReady || !IsShown)
            {
                return false;
            }

            float strength = SlapStrength(speed);
            LastSlapHandSpeed = speed;
            RequestHit(Runner.LocalPlayer, SlapVelocity(slap.HandVelocity * _slapConfig.BalloonVelocityScale, strength));
            feedback = new HapticPulse(Mathf.Lerp(0.45f, 1f, strength), Mathf.Lerp(0.05f, 0.12f, strength)); // a hard slap kicks
            return true;
        }

        /// <summary>Hand speed of the last slap this client landed on the balloon (m/s), for tuning.</summary>
        public float LastSlapHandSpeed { get; private set; }

        private float SlapStrength(float handSpeed) =>
            Mathf.InverseLerp(_slapConfig.MinBalloonSlapSpeed, _config.SlapFullStrengthHandSpeed, handSpeed);

        /// <summary>
        /// The balloon flies the way the hand moved, as hard as it moved. Gentle taps get a little extra lift so
        /// keepy-uppy touches pop it up; hard slaps get almost none, so they go where they're aimed.
        /// </summary>
        private Vector3 SlapVelocity(Vector3 hand, float strength) =>
            hand + Vector3.up * Mathf.Lerp(_config.SlapTapLift, _config.SlapHardLift, strength);

        /// <summary>A player's hit (slap, thrown or swung item). PlayerRef.None counts as nobody's hit.</summary>
        public void RequestHit(PlayerRef hitter, Vector3 velocity)
        {
            if (IsReady)
            {
                RpcApplyHit(hitter, velocity);
            }
        }

        public void RequestPop(string reason)
        {
            if (IsReady)
            {
                RpcForcePop(reason);
            }
        }

        public void ResetTo(Vector3 position) => RpcResetTo(position);

        public override void Spawned()
        {
            Instance = this;
            _body = GetComponent<Rigidbody>();
            _networkTransform = GetComponent<NetworkTransform>();
            _physics = new BalloonPhysics(_config, transform);
            _colorBlock = new MaterialPropertyBlock();
            if (_visual != null)
            {
                _baseVisualScale = _visual.transform.localScale;
            }

            _string = transform.Find("String");
            if (_string != null)
            {
                _stringRest = _string.localPosition;
            }

            _renderers = GetComponentsInChildren<Renderer>(true);
            _colliders = GetComponentsInChildren<Collider>(true);
            _appliedShown = -1;

            // Fusion 2.1 send priority: on this client, receive the balloon's state every send tick (it is the object
            // everyone is looking at). Shared Mode only accepts the local player here.
            if (Runner.GameMode == GameMode.Shared)
            {
                Object.SetPriority(Runner.LocalPlayer, SendEveryTick);
            }

            if (HasStateAuthority)
            {
                Station(transform.position);
            }
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        /// <summary>Forecast Physics requires physics code in FixedUpdate (Unity's physics step), not FixedUpdateNetwork.</summary>
        private void FixedUpdate()
        {
            if (!IsReady || !HasStateAuthority)
            {
                return;
            }

            bool isLive = IsLive;
            if (_body.isKinematic == isLive)
            {
                _body.isKinematic = !isLive; // frozen during the countdown and after the run; Fusion syncs isKinematic
            }

            if (!isLive)
            {
                return;
            }

            if (IsStationed)
            {
                Hover();
                return;
            }

            var input = new BalloonStepInput
            {
                Position = _body.position,
                Velocity = _body.linearVelocity,
                Condition = Condition,
                AreSpikesArmed = HitGrace.ExpiredOrNotRunning(Runner),
                CanLeak = LeakCooldown.ExpiredOrNotRunning(Runner),
                DeltaTime = Time.fixedDeltaTime,
                Time = Time.time,
            };

            BalloonStepResult result = _physics.Step(input);
            if (result.Popped)
            {
                Pop(result.PopReason);
                return;
            }

            ApplyStep(result);
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (!IsReady || !HasStateAuthority)
            {
                return;
            }

            // Bounces off walls, floors and furniture never clear the last hitter, but they end the rally.
            // A thrown item's hit is a pass (counted when its RPC arrives), so it doesn't break the rally.
            if (collision.collider.GetComponentInParent<NetworkItem>() == null)
            {
                Rally = 0;
            }
        }

        /// <summary>Shows or hides the whole balloon (body, string, rally text) and its colliders, only when it changes.</summary>
        private void ApplyShown()
        {
            int shown = IsShown ? 1 : 0;
            if (shown == _appliedShown)
            {
                return;
            }

            _appliedShown = shown;
            foreach (Renderer part in _renderers)
            {
                if (part != null)
                {
                    part.enabled = shown == 1;
                }
            }

            foreach (Collider part in _colliders)
            {
                if (part != null)
                {
                    part.enabled = shown == 1;
                }
            }
        }

        public override void Render()
        {
            ApplyShown();
            if (_visual != null)
            {
                // A white flash on contact, fading in a few frames.
                float since = Time.time - _lastHitTime;
                float flash = Mathf.Exp(-since * 22f) * _lastHitStrength;
                // Always the hitter's colour: whose turn it is shows on screen (TurnIndicator), not by greying the balloon.
                Color color = LastHitter.ToPaletteColor();
                _colorBlock.SetColor(BaseColorId, Color.Lerp(color, Color.white, flash * 0.8f));
                _visual.SetPropertyBlock(_colorBlock);

                // Idle breathing wobble, plus a squash along the hit direction that springs back and jiggles (latex).
                float wobble = 1f + Mathf.Sin(Time.time * WobbleSpeed) * WobbleAmount;
                float jiggle = _config.HitSquash * _lastHitStrength * Mathf.Exp(-since * 7f) * Mathf.Cos(since * 28f);
                var squash = new Vector3(1f + jiggle * 0.6f, 1f - jiggle, 1f + jiggle * 0.6f);
                _visual.transform.rotation = Quaternion.FromToRotation(Vector3.up, _lastHitDirection); // squash axis = hit
                _visual.transform.localScale = Vector3.Scale(_baseVisualScale, squash) * (_config.ScaleFor(Condition) * wobble);
            }

            if (_string != null)
            {
                // The string trails behind the motion and hangs down when still.
                Vector3 trail = Vector3.down - Velocity * 0.35f;
                Quaternion tilt = Quaternion.FromToRotation(Vector3.down, trail.normalized);
                Quaternion local = Quaternion.Inverse(transform.rotation) * tilt;
                _string.localPosition = local * _stringRest;
                _string.localRotation = local;
            }

            if (_rallyText != null)
            {
                string text = Rally > 1 ? Rally.ToString() : string.Empty;
                if (_rallyText.text != text)
                {
                    _rallyText.text = text;
                }

                Camera view = Camera.main;
                if (view != null)
                {
                    _rallyText.transform.rotation = Quaternion.LookRotation(_rallyText.transform.position - view.transform.position);
                }
            }
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        private void RpcApplyHit(PlayerRef hitter, Vector3 velocity)
        {
            if (!IsLive)
            {
                return;
            }

            if (hitter != PlayerRef.None)
            {
                if (!IsPassRuleSuspended && hitter == LastHitter)
                {
                    Pop("Double hit! Pass it to a friend!");
                    return;
                }

                if (LastHitter != PlayerRef.None && hitter != LastHitter)
                {
                    Rally++;
                    BestRally = Mathf.Max(BestRally, Rally);
                }

                LastHitter = hitter;
            }

            Vector3 capped = Vector3.ClampMagnitude(velocity, _config.MaxHitSpeed);
            IsStationed = false;
            _body.isKinematic = false;
            _body.linearVelocity = Vector3.ClampMagnitude(capped + Vector3.up * _config.UpwardAssist, _config.MaxHitSpeed);
            HitGrace = TickTimer.CreateFromSeconds(Runner, _config.HitGraceSeconds);
            RpcHitEffects(_body.linearVelocity.normalized, Mathf.Clamp01(capped.magnitude / _config.MaxHitSpeed));
        }

        /// <summary>Everyone hears the hit (louder and higher for a harder hit) and sees the balloon jiggle.</summary>
        [Rpc(RpcSources.StateAuthority, RpcTargets.All, TickAligned = false)]
        private void RpcHitEffects(Vector3 direction, float strength)
        {
            _lastHitTime = Time.time;
            _lastHitDirection = direction.sqrMagnitude > 0.001f ? direction : Vector3.up;
            _lastHitStrength = Mathf.Lerp(0.35f, 1f, strength);
            if (_hitAudio != null && _hitClips != null && _hitClips.Length > 0)
            {
                AudioClip clip = _hitClips[Random.Range(0, _hitClips.Length)];
                _hitAudio.pitch = Mathf.Lerp(0.96f, 1.08f, strength) * Random.Range(0.97f, 1.03f);
                _hitAudio.PlayOneShot(clip, Mathf.Lerp(0.5f, 1f, strength));
            }
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        private void RpcForcePop(string reason)
        {
            if (IsLive)
            {
                Pop(reason);
            }
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        private void RpcResetTo(Vector3 position)
        {
            TeleportTo(position);
            LastHitter = PlayerRef.None;
            Condition = BalloonCondition.Full;
            Rally = 0;
            BestRally = 0;
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RpcPlayPopEffects(Vector3 position, Color color)
        {
            PopBurstEffect.Play(position, color, _effectMaterial);
            if (_popClip != null)
            {
                AudioSource.PlayClipAtPoint(_popClip, position, 1f);
            }

            // Pop blast: knocks over anyone standing right next to it.
            LocalRig rig = LocalRig.Instance;
            if (rig == null)
            {
                return;
            }

            Vector3 away = rig.Head.position - position;
            if (away.magnitude < _config.PopBlastRadius)
            {
                rig.Knock(away.normalized, _config.PopBlastForce, _config.PopBlastKnockdownSeconds);
            }
        }

        private void ApplyStep(in BalloonStepResult result)
        {
            if (result.Leaked)
            {
                Condition = (BalloonCondition)Mathf.Min((int)Condition + 1, (int)BalloonCondition.NearlyFlat);
                LeakCooldown = TickTimer.CreateFromSeconds(Runner, _config.LeakCooldown);
            }

            if (result.RallyBroken)
            {
                Rally = 0;
            }

            GameState gameState = GameState.Instance;
            if (gameState != null)
            {
                if (result.HoopReached != BalloonStepResult.NoHoop)
                {
                    gameState.ReportHoop(result.HoopReached);
                }

                if (result.FinishReached)
                {
                    gameState.ReportFinish(Condition, BestRally);
                }
            }

            _body.linearVelocity = result.Velocity;
        }

        private void Pop(string reason)
        {
            PopCount++;
            RpcPlayPopEffects(_body.position, LastHitter.ToPaletteColor());

            GameState gameState = GameState.Instance;
            int checkpoint = gameState != null ? gameState.CurrentCheckpoint : 0;

            TeleportTo(CourseLayout.BalloonSpawn(checkpoint)); // back beside the last hoop cleared
            LastHitter = PlayerRef.None;
            Condition = BalloonCondition.Full;
            Rally = 0;

            if (gameState != null)
            {
                gameState.ReportPop(reason);
            }
        }

        /// <summary>
        /// Moves the balloon without interpolating across the map: NetworkTransform.Teleport networks a teleport event
        /// and adjusts the Rigidbody when Forecast Physics is on.
        /// </summary>
        /// <summary>Holds the balloon at a spot (spring toward it, with a gentle bob) until the next hit.</summary>
        private void Station(Vector3 position)
        {
            StationPoint = position;
            IsStationed = true;
        }

        private void Hover()
        {
            Vector3 target = StationPoint + Vector3.up * (Mathf.Sin(Time.time * _config.StationBobSpeed) * _config.StationBobHeight);
            _body.linearVelocity = (target - _body.position) * _config.StationSpring;
        }

        private void TeleportTo(Vector3 position)
        {
            Station(position); // every reset and pop puts it back on station
            if (!_body.isKinematic) // frozen between runs: Unity warns when a kinematic body's velocity is set
            {
                _body.linearVelocity = Vector3.zero;
                _body.angularVelocity = Vector3.zero;
            }

            if (_networkTransform != null)
            {
                _networkTransform.Teleport(position, Quaternion.identity);
            }
            else
            {
                _body.position = position;
                transform.position = position;
            }
        }

        private int CountPlayers()
        {
            int count = 0;
            foreach (PlayerRef _ in Runner.ActivePlayers)
            {
                count++;
            }

            return count;
        }
    }
}
