using Fusion;
using GameJam.Core;
using GameJam.Gameplay;
using GameJam.Player;
using UnityEngine;

namespace GameJam.Net
{
    /// <summary>
    /// The run: lobby, countdown, a timed run through the sections, then a medal. Owned by the Shared Mode
    /// master client (Master Client Object flag), so it moves to a new master if the owner leaves.
    /// Anyone can report events; only the owner decides.
    /// </summary>
    public sealed class GameState : NetworkBehaviour, IRunStatus
    {
        [SerializeField] private RunConfig _config;

        private FusionTimeSource _timeSource;

        public static GameState Instance { get; private set; }

        [Networked] public RunState State { get; private set; }

        [Networked] public int Section { get; private set; }

        [Networked] public int Pops { get; private set; }

        [Networked] public int LastScore { get; private set; }

        [Networked] public int BestScore { get; private set; }

        [Networked] private TickTimer Timer { get; set; }

        [Networked] private NetworkString<_64> Headline { get; set; }

        /// <summary>The balloon only moves in the lobby (free play) and during a run.</summary>
        public bool IsBalloonLive => State == RunState.Lobby || State == RunState.Running;

        public int CurrentSection => Section;

        public float TimeLeft => State switch
        {
            RunState.Running => Timer.RemainingTime(Runner) ?? 0f,
            RunState.Finished => 0f,
            _ => _config.RunSeconds,
        };

        public string WristText => State switch
        {
            RunState.Running => TimeFormat.MinutesSeconds(TimeLeft),
            RunState.Countdown => "Ready",
            RunState.Finished => LastScore > 0 ? MedalText(LastScore) : "Time up",
            _ => "Lobby",
        };

        public void Configure(RunConfig config) => _config = config;

        public void RequestStart() => RpcRequestStart();

        public void ReportGate(int sectionIndex)
        {
            if (State == RunState.Running && sectionIndex > Section)
            {
                RpcReportGate(sectionIndex);
            }
        }

        public void ReportFinish(BalloonCondition condition, int bestRally)
        {
            if (State == RunState.Running)
            {
                RpcReportFinish(condition, bestRally);
            }
        }

        public void ReportPop(string line) => RpcReportPop(line);

        public override void Spawned()
        {
            Instance = this;
            GameServices.Run = this;

            // A spawned network object means the simulation state has arrived, so the clock is readable now.
            _timeSource = new FusionTimeSource(Runner);
            CourseClock.SetSource(_timeSource);

            if (HasStateAuthority)
            {
                State = RunState.Lobby;
                Headline = "Slap the red button to start!";
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

            if (State == RunState.Countdown && Timer.Expired(Runner))
            {
                State = RunState.Running;
                Timer = TickTimer.CreateFromSeconds(Runner, _config.RunSeconds);
                Headline = "GO! Don't let the balloon drop!";
            }
            else if (State == RunState.Running && Timer.Expired(Runner))
            {
                State = RunState.Finished;
                LastScore = 0;
                Headline = "Time's up! No medal this time.";
            }
        }

        public override void Render()
        {
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
                $"Section  {Mathf.Min(Section + 1, _config.SectionCount)}/{_config.SectionCount}\n" +
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
            Section = 0;
            Pops = 0;
            Headline = "Get ready...";

            if (NetworkBalloon.Instance != null)
            {
                NetworkBalloon.Instance.ResetTo(CourseLayout.BalloonSpawn(0));
            }

            RpcRespawnCrew(0);
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RpcRespawnCrew(int section)
        {
            if (LocalRig.Instance != null)
            {
                LocalRig.Instance.RespawnAt(CourseLayout.PlayerSpawn(section, Runner.LocalPlayer.PlayerId));
            }
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        private void RpcReportGate(int sectionIndex)
        {
            if (State != RunState.Running || sectionIndex <= Section)
            {
                return;
            }

            Section = Mathf.Min(sectionIndex, _config.SectionCount - 1);
            Headline = $"Section {Section + 1}! Keep it up!";
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        private void RpcReportFinish(BalloonCondition condition, int bestRally)
        {
            if (State != RunState.Running)
            {
                return;
            }

            float secondsLeft = Timer.RemainingTime(Runner) ?? 0f;
            int score = ScoreCalculator.FinishScore(_config, secondsLeft, condition, bestRally);

            LastScore = score;
            BestScore = Mathf.Max(BestScore, score);
            State = RunState.Finished;
            Headline = $"{MedalText(score)}! {score} points!";
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        private void RpcReportPop(string line)
        {
            if (State != RunState.Running)
            {
                return;
            }

            Pops++;
            Headline = line;
            RpcRespawnCrew(Section); // the whole crew goes back to the start of this section, clock still running
        }

        private string MedalText(int score) => ScoreCalculator.MedalFor(_config, score).ToString().ToUpperInvariant();
    }
}
