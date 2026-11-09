using Fusion;
using GameJam.Core;
using GameJam.Gameplay;
using GameJam.Player;
using UnityEngine;

namespace GameJam.Net
{
    /// <summary>
    /// The run in the open backyard: lobby, countdown, then 3:00 to guide the balloon through every hoop (any order) and
    /// down through the finish basket, then a medal. A pop sends the balloon back beside the last hoop cleared; the crew stays put.
    /// Owned by the Shared Mode master client (Master Client Object flag), so it moves to a new master if the owner leaves.
    /// Anyone can report events; only the owner decides.
    /// </summary>
    public sealed class GameState : NetworkBehaviour, IRunStatus
    {
        private const float MasterStallSeconds = 2.5f;
        private const float ReassignRetrySeconds = 3f;
        private const int NoHoop = -1;
        private const string LobbyHeadline = "Slap the big red button to start!";
        private const float LockedFinishReportSeconds = 2f;

        [SerializeField] private RunConfig _config;

        private FusionTimeSource _timeSource;
        private int _hoopCount;
        private int _lastSeenHeartbeat = -1;
        private float _heartbeatChangedAt;
        private float _nextReassignRequestAt;
        private float _nextLockedFinishReport;

        public static GameState Instance { get; private set; }

        [Networked] public RunState State { get; private set; }

        /// <summary>Bit i set = hoop i cleared this run.</summary>
        [SerializeField] private AudioSource _winAudio;
        [Tooltip("Crowd cheering, with the fanfare, on a medal finish.")]
        [SerializeField] private AudioSource _cheerAudio;
        [SerializeField] private AudioSource _timeUpAudio;
        private bool _playedWin;
        private bool _playedTimeUp;

        [Networked] public int HoopMask { get; private set; }

        /// <summary>The last hoop cleared, or -1 before the first one.</summary>
        [Networked] public int LastHoop { get; private set; }

        [Networked] public int Pops { get; private set; }

        [Networked] public int LastScore { get; private set; }

        [Networked] public int BestScore { get; private set; }

        [Networked] private TickTimer Timer { get; set; }

        [Networked] private NetworkString<_64> Headline { get; set; }

        /// <summary>Incremented by the master every tick; other clients watch it to spot a frozen master.</summary>
        [Networked] private int Heartbeat { get; set; }

        /// <summary>The balloon only moves during a run (it waits frozen through the countdown and the result).</summary>
        public bool IsBalloonLive => State == RunState.Running;

        /// <summary>The balloon is hidden in the lobby and shows up when the red button is slapped.</summary>
        public bool IsBalloonShown => State != RunState.Lobby;

        public int CurrentCheckpoint => LastHoop + 1;

        public int HoopCount => _hoopCount;

        public int HoopsCleared => CountBits(HoopMask);

        public bool AllHoopsCleared => _hoopCount > 0 && HoopsCleared >= _hoopCount;

        public float TimeLeft => State switch
        {
            RunState.Running => Timer.RemainingTime(Runner) ?? 0f,
            RunState.Finished => 0f,
            _ => _config.RunSeconds,
        };

        public string WristText => State switch
        {
            RunState.Running => $"{TimeFormat.MinutesSeconds(TimeLeft)}  {HoopsCleared}/{_hoopCount}",
            RunState.Countdown => "Ready",
            RunState.Finished => LastScore > 0 ? MedalText(LastScore) : "Time up",
            _ => "Slap the red\nbutton to start",
        };

        /// <summary>The run ended because the clock ran out (no medal): the referee whistle and the "time's up" banner.</summary>
        public bool IsTimeUp => State == RunState.Finished && LastScore == 0;

        /// <summary>Seconds until everyone goes back to the lobby after a run ends (0 outside the result).</summary>
        public float SecondsToLobby => State == RunState.Finished ? Timer.RemainingTime(Runner) ?? 0f : 0f;

        /// <summary>
        /// Everyone hears the fanfare and the crowd cheering once when a run ends with a medal (and confetti bursts round
        /// each player's own view), or the referee whistle once when time runs out; both re-arm for the next run.
        /// </summary>
        private void PlayResultOnce()
        {
            bool won = State == RunState.Finished && LastScore > 0;
            if (won && !_playedWin)
            {
                if (_winAudio != null)
                {
                    _winAudio.Play();
                }

                if (_cheerAudio != null)
                {
                    _cheerAudio.Play();
                }

                Camera view = Camera.main;
                WinCelebration.Play(view != null ? view.transform : null);
            }

            _playedWin = won;

            bool timeUp = IsTimeUp;
            if (timeUp && !_playedTimeUp && _timeUpAudio != null)
            {
                _timeUpAudio.Play();
            }

            _playedTimeUp = timeUp;
        }

        public void Configure(RunConfig config) => _config = config;

        /// <summary>
        /// Builder wiring: 2D sources everyone hears, the win fanfare and crowd cheer (a medal finish) and the referee whistle
        /// (time up).
        /// </summary>
        public void ConfigureSounds(AudioSource winAudio, AudioSource cheerAudio, AudioSource timeUpAudio)
        {
            _winAudio = winAudio;
            _cheerAudio = cheerAudio;
            _timeUpAudio = timeUpAudio;
        }

        public bool IsHoopCleared(int hoopIndex) => hoopIndex >= 0 && hoopIndex < 32 && (HoopMask & (1 << hoopIndex)) != 0;

        public void RequestStart() => RpcRequestStart();

        /// <summary>The balloon went through a hoop: it counts during a run (in the lobby the balloon is hidden).</summary>
        public void ReportHoop(int hoopIndex)
        {
            if (State == RunState.Running && !IsHoopCleared(hoopIndex))
            {
                RpcReportHoop(hoopIndex);
            }
        }

        public void ReportFinish(BalloonCondition condition, int bestRally)
        {
            if (State != RunState.Running)
            {
                return;
            }

            // While the finish is locked the balloon may sit on the pad every physics step; nag at most every 2 seconds.
            if (!AllHoopsCleared && Time.time < _nextLockedFinishReport)
            {
                return;
            }

            _nextLockedFinishReport = Time.time + LockedFinishReportSeconds;
            RpcReportFinish(condition, bestRally);
        }

        public void ReportPop(string line) => RpcReportPop(line);

        /// <summary>A one-off commentator line (the roaming character's antics).</summary>
        public void Announce(string line) => RpcAnnounce(line);

        public override void Spawned()
        {
            Instance = this;
            GameServices.Run = this;
            _hoopCount = FindObjectsByType<HoopCheckpoint>(FindObjectsSortMode.None).Length; // same scene on every client

            // A spawned network object means the simulation state has arrived, so the clock is readable now.
            _timeSource = new FusionTimeSource(Runner);
            CourseClock.SetSource(_timeSource);

            if (HasStateAuthority)
            {
                State = RunState.Lobby;
                LastHoop = NoHoop;
                Headline = LobbyHeadline;
            }
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            if (Instance == this)
            {
                Instance = null;
            }

            if (ReferenceEquals(GameServices.Run, this))
            {
                GameServices.Run = null;
            }

            CourseClock.ClearSource(_timeSource);
        }

        public override void FixedUpdateNetwork()
        {
            if (!HasStateAuthority)
            {
                return;
            }

            Heartbeat++;

            if (State == RunState.Countdown && Timer.Expired(Runner))
            {
                State = RunState.Running;
                Timer = TickTimer.CreateFromSeconds(Runner, _config.RunSeconds);
                Headline = "GO! Find the hoops!";
            }
            else if (State == RunState.Running && Timer.Expired(Runner))
            {
                State = RunState.Finished;
                Timer = TickTimer.CreateFromSeconds(Runner, _config.ResultSeconds);
                LastScore = 0;
                Headline = "Time's up! Back to the start...";
            }
            else if (State == RunState.Finished && Timer.Expired(Runner))
            {
                ReturnToLobby();
            }
        }

        public override void Render()
        {
            PlayResultOnce();
            WatchMasterClient();

            CommentatorBoard board = CommentatorBoard.Instance;
            if (board == null)
            {
                return;
            }

            string headline = State == RunState.Countdown
                ? $"{Mathf.CeilToInt(Timer.RemainingTime(Runner) ?? 0f)}..."
                : Headline.ToString();
            board.SetHeadline(headline);

            NetworkBalloon balloon = NetworkBalloon.Instance;
            int bestRally = balloon != null ? balloon.BestRally : 0;
            string soloNote = balloon != null && balloon.IsPassRuleSuspended ? "\nSOLO TEST: pass rule off" : string.Empty;
            board.SetScoreboard(
                $"Time  {TimeFormat.MinutesSeconds(TimeLeft)}\n" +
                $"Hoops  {HoopsCleared}/{_hoopCount}\n" +
                $"Pops  {Pops}   Best rally  {bestRally}\n" +
                $"Last  {LastScore}   Best  {BestScore}" +
                soloNote);
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        private void RpcRequestStart()
        {
            if (State == RunState.Countdown || State == RunState.Running)
            {
                return;
            }

            State = RunState.Countdown;
            Timer = TickTimer.CreateFromSeconds(Runner, _config.CountdownSeconds);
            HoopMask = 0;
            LastHoop = NoHoop;
            Pops = 0;
            Headline = "Here comes the balloon...";

            if (NetworkBalloon.Instance != null)
            {
                NetworkBalloon.Instance.ResetTo(CourseLayout.BalloonSpawn(0));
            }

            foreach (MischiefMaker giant in MischiefMaker.Active)
            {
                giant.Reroll(); // fresh wander for every giant (each keeps its own character)
            }

            RpcRespawnCrewAtStart();
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RpcRespawnCrewAtStart()
        {
            if (LocalRig.Instance != null)
            {
                LocalRig.Instance.RespawnAt(CourseLayout.PlayerSpawn(0, Runner.LocalPlayer.PlayerId));
            }
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        private void RpcReportHoop(int hoopIndex)
        {
            if (hoopIndex < 0 || hoopIndex >= 32)
            {
                return;
            }

            if (State != RunState.Running || IsHoopCleared(hoopIndex))
            {
                return;
            }

            HoopMask |= 1 << hoopIndex;
            LastHoop = hoopIndex;
            Headline = AllHoopsCleared
                ? "All hoops! Drop it in the finish basket!"
                : $"Hoop {HoopsCleared}/{_hoopCount}! Keep it up!";
        }

        /// <summary>
        /// After the result: hoops back, balloon back on station at the start, crew back on the stage, ready for another go
        /// (the balloon used to stay frozen wherever the run ended until someone pressed the red button).
        /// </summary>
        private void ReturnToLobby()
        {
            State = RunState.Lobby;
            HoopMask = 0;
            LastHoop = NoHoop;
            Pops = 0;
            Headline = LobbyHeadline;
            if (NetworkBalloon.Instance != null)
            {
                NetworkBalloon.Instance.ResetTo(CourseLayout.BalloonSpawn(0));
            }

            foreach (MischiefMaker giant in MischiefMaker.Active)
            {
                giant.Reroll();
            }

            // Every item back on its spot, used-up ones too: a fresh yard for the next run.
            foreach (NetworkItem item in FindObjectsByType<NetworkItem>(FindObjectsSortMode.None))
            {
                item.ReturnHome();
            }

            RpcRespawnCrewAtStart();
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        private void RpcReportFinish(BalloonCondition condition, int bestRally)
        {
            if (State != RunState.Running)
            {
                return;
            }

            if (!AllHoopsCleared)
            {
                Headline = $"Finish is locked! {_hoopCount - HoopsCleared} hoops to go.";
                return;
            }

            float secondsLeft = Timer.RemainingTime(Runner) ?? 0f;
            int score = ScoreCalculator.FinishScore(_config, secondsLeft, condition, bestRally, HoopsCleared);

            LastScore = score;
            BestScore = Mathf.Max(BestScore, score);
            State = RunState.Finished;
            Timer = TickTimer.CreateFromSeconds(Runner, _config.ResultSeconds);
            Headline = $"{MedalText(score)}! {score} points!";
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        private void RpcAnnounce(string line)
        {
            if (State == RunState.Lobby || State == RunState.Running)
            {
                Headline = line;
            }
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        private void RpcReportPop(string line)
        {
            if (State != RunState.Running)
            {
                return;
            }

            // Open yard: the balloon comes back beside the last hoop (NetworkBalloon does that); the crew stays put and
            // runs over, with the clock still running.
            Pops++;
            Headline = line;
        }

        /// <summary>
        /// Fusion 2.1 master client recovery: if the master stops ticking (its headset went to sleep, its app froze),
        /// ask to take over. Fusion only grants it after the master has been idle past
        /// NetworkProjectConfig.MasterClientRequestReassignThreshold (2 s), and the Master Client Object flag then moves
        /// the game state and the balloon to the new master, so the timer and score carry on.
        /// </summary>
        private void WatchMasterClient()
        {
            if (Runner.GameMode != GameMode.Shared || HasStateAuthority || Runner.IsSharedModeMasterClient)
            {
                _lastSeenHeartbeat = -1;
                return;
            }

            float now = Time.unscaledTime;
            if (Heartbeat != _lastSeenHeartbeat)
            {
                _lastSeenHeartbeat = Heartbeat;
                _heartbeatChangedAt = now;
                return;
            }

            if (now - _heartbeatChangedAt > MasterStallSeconds && now >= _nextReassignRequestAt)
            {
                _nextReassignRequestAt = now + ReassignRetrySeconds;
                Debug.LogWarning("[Net] Master client looks frozen; requesting to take over the game state.");
                Runner.RequestMasterClientReassign();
            }
        }

        private string MedalText(int score) => ScoreCalculator.MedalFor(_config, score).ToString().ToUpperInvariant();

        private static int CountBits(int mask)
        {
            int count = 0;
            while (mask != 0)
            {
                mask &= mask - 1;
                count++;
            }

            return count;
        }
    }
}
