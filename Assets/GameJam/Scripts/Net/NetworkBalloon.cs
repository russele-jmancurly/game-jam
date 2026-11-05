using Fusion;
using GameJam.Core;
using GameJam.Gameplay;
using GameJam.Player;
using UnityEngine;

namespace GameJam.Net
{
    /// <summary>
    /// The networked balloon. Its state authority (the master client, via the Master Client Object flag)
    /// runs <see cref="BalloonPhysics"/>; everyone else sends hits through an RPC, and the first hit the
    /// authority receives wins. Enforces the pass rule: the same player can never hit it twice in a row.
    /// </summary>
    public sealed class NetworkBalloon : NetworkBehaviour, ISlappable
    {
        private const float WobbleSpeed = 6f;
        private const float WobbleAmount = 0.03f;

        [SerializeField] private BalloonConfig _config;
        [SerializeField] private SlapConfig _slapConfig;
        [SerializeField] private Renderer _visual;
        [SerializeField] private TextMesh _rallyText;
        [SerializeField] private Material _effectMaterial;

        private BalloonPhysics _physics;
        private Vector3 _baseVisualScale;
        private MaterialPropertyBlock _colorBlock;
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        public static NetworkBalloon Instance { get; private set; }

        [Networked] public Vector3 Velocity { get; private set; }

        [Networked] public PlayerRef LastHitter { get; private set; }

        [Networked] public BalloonCondition Condition { get; private set; }

        [Networked] public int Rally { get; private set; }

        [Networked] public int BestRally { get; private set; }

        [Networked] public int PopCount { get; private set; }

        [Networked] private TickTimer LeakCooldown { get; set; }

        [Networked] private TickTimer HitGrace { get; set; }

        /// <summary>
        /// True while a lone player is testing: the pass rule (no double hits) is off so one person can
        /// play the whole loop. Turns itself back on as soon as a second player joins.
        /// </summary>
        public bool IsPassRuleSuspended => _config.AllowSoloDoubleHits && CountPlayers() < 2;

        private bool IsLive => GameState.Instance == null || GameState.Instance.IsBalloonLive;

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
            if (speed < _slapConfig.MinBalloonSlapSpeed || Object == null || !Object.IsValid)
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
            if (Object != null && Object.IsValid)
            {
                RpcApplyHit(hitter, velocity);
            }
        }

        public void RequestPop(string reason)
        {
            if (Object != null && Object.IsValid)
            {
                RpcForcePop(reason);
            }
        }

        public void ResetTo(Vector3 position) => RpcResetTo(position);

        public override void Spawned()
        {
            Instance = this;
            _physics = new BalloonPhysics(_config, transform);
            _colorBlock = new MaterialPropertyBlock();
            if (_visual != null)
            {
                _baseVisualScale = _visual.transform.localScale;
            }
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        public override void FixedUpdateNetwork()
        {
            if (!HasStateAuthority)
            {
                return;
            }

            if (!IsLive)
            {
                Velocity = Vector3.zero;
                return;
            }

            var input = new BalloonStepInput
            {
                Position = transform.position,
                Velocity = Velocity,
                Condition = Condition,
                AreSpikesArmed = HitGrace.ExpiredOrNotRunning(Runner),
                CanLeak = LeakCooldown.ExpiredOrNotRunning(Runner),
                DeltaTime = Runner.DeltaTime,
            };

            BalloonStepResult result = _physics.Step(input);
            if (result.Popped)
            {
                Pop(result.PopReason);
                return;
            }

            ApplyStep(result);
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
            Velocity = Vector3.ClampMagnitude(capped + Vector3.up * _config.UpwardAssist, _config.MaxHitSpeed);
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
            transform.position = position;
            Velocity = Vector3.zero;
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
                if (result.GateReached != BalloonStepResult.NoGate)
                {
                    gameState.ReportGate(result.GateReached);
                }

                if (result.FinishReached)
                {
                    gameState.ReportFinish(Condition, BestRally);
                }
            }

            transform.position = result.Position;
            Velocity = result.Velocity;
        }

        private void Pop(string reason)
        {
            PopCount++;
            RpcPlayPopEffects(transform.position, LastHitter.ToPaletteColor());

            GameState gameState = GameState.Instance;
            int section = gameState != null ? gameState.Section : 0;

            transform.position = CourseLayout.BalloonSpawn(section);
            Velocity = Vector3.zero;
            LastHitter = PlayerRef.None;
            Condition = BalloonCondition.Full;
            Rally = 0;

            if (gameState != null)
            {
                gameState.ReportPop(reason);
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
