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

        private Rigidbody _body;
        private NetworkTransform _networkTransform;
        private BalloonPhysics _physics;
        private Vector3 _baseVisualScale;
        private MaterialPropertyBlock _colorBlock;

        public static NetworkBalloon Instance { get; private set; }

        [Networked] public PlayerRef LastHitter { get; private set; }

        [Networked] public BalloonCondition Condition { get; private set; }

        [Networked] public int Rally { get; private set; }

        [Networked] public int BestRally { get; private set; }

        [Networked] public int PopCount { get; private set; }

        [Networked] private TickTimer LeakCooldown { get; set; }

        [Networked] private TickTimer HitGrace { get; set; }

        /// <summary>The balloon's current velocity (synced by the NetworkTransform's physics state).</summary>
        public Vector3 Velocity => _body != null ? _body.linearVelocity : Vector3.zero;

        /// <summary>
        /// True while a lone player is testing: the pass rule (no double hits) is off so one person can play the whole
        /// loop. Turns itself back on as soon as a second player joins.
        /// </summary>
        public bool IsPassRuleSuspended => _config.AllowSoloDoubleHits && CountPlayers() < 2;

        private bool IsLive => GameState.Instance == null || GameState.Instance.IsBalloonLive;

        private bool IsReady => Object != null && Object.IsValid;

        public void Configure(BalloonConfig config, SlapConfig slapConfig, Renderer visual, TextMesh rallyText, Material effectMaterial)
        {
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
            if (speed < _slapConfig.MinBalloonSlapSpeed || !IsReady)
            {
                return false;
            }

            RequestHit(Runner.LocalPlayer, slap.HandVelocity * _slapConfig.BalloonVelocityScale);
            feedback = new HapticPulse(Mathf.Clamp01(0.3f + speed * 0.1f), 0.08f);
            return true;
        }

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

            // Fusion 2.1 send priority: on this client, receive the balloon's state every send tick (it is the object
            // everyone is looking at). Shared Mode only accepts the local player here.
            if (Runner.GameMode == GameMode.Shared)
            {
                Object.SetPriority(Runner.LocalPlayer, SendEveryTick);
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

            var input = new BalloonStepInput
            {
                Position = _body.position,
                Velocity = _body.linearVelocity,
                Condition = Condition,
                AreSpikesArmed = HitGrace.ExpiredOrNotRunning(Runner),
                CanLeak = LeakCooldown.ExpiredOrNotRunning(Runner),
                DeltaTime = Time.fixedDeltaTime,
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

        public override void Render()
        {
            if (_visual != null)
            {
                _colorBlock.SetColor(BaseColorId, LastHitter.ToPaletteColor());
                _visual.SetPropertyBlock(_colorBlock);

                float wobble = 1f + Mathf.Sin(Time.time * WobbleSpeed) * WobbleAmount;
                _visual.transform.localScale = _baseVisualScale * (_config.ScaleFor(Condition) * wobble);
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
            _body.isKinematic = false;
            _body.linearVelocity = Vector3.ClampMagnitude(capped + Vector3.up * _config.UpwardAssist, _config.MaxHitSpeed);
            HitGrace = TickTimer.CreateFromSeconds(Runner, _config.HitGraceSeconds);
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
        private void TeleportTo(Vector3 position)
        {
            _body.linearVelocity = Vector3.zero;
            _body.angularVelocity = Vector3.zero;
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
