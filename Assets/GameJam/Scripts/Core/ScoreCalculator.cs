using UnityEngine;

namespace GameJam.Core
{
    /// <summary>Pure scoring rules from the GDD, kept free of networking so they are easy to test.</summary>
    public static class ScoreCalculator
    {
        public static int FinishScore(RunConfig config, float secondsLeft, BalloonCondition condition, int bestRally, int hoopsCleared)
        {
            float raw = config.BasePoints
                        + Mathf.Max(0f, secondsLeft) * config.PointsPerSecondLeft
                        + Mathf.Max(0, hoopsCleared) * config.PointsPerHoop
                        + Mathf.Max(0, bestRally) * config.PointsPerRallyPass;
            return Mathf.RoundToInt(raw * config.MultiplierFor(condition));
        }

        public static Medal MedalFor(RunConfig config, int score)
        {
            if (score <= 0)
            {
                return Medal.None;
            }

            if (score >= config.GoldScore)
            {
                return Medal.Gold;
            }

            return score >= config.SilverScore ? Medal.Silver : Medal.Bronze;
        }
    }
}
