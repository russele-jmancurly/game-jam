using UnityEngine;

namespace GameJam.Core
{
    /// <summary>
    /// Tuning for the timed run and its score (GDD: Core loop and time limit). Open yard: clear every hoop in any order,
    /// then land the balloon on the finish pad.
    /// </summary>
    [CreateAssetMenu(menuName = "Game Jam/Config/Run Config", fileName = "RunConfig")]
    public sealed class RunConfig : ScriptableObject
    {
        [Header("Run")]
        [Tooltip("Length of a run: 6 minutes. When it runs out the referee whistles, TIME'S UP shows in everyone's view and " +
                 "the crew goes back to the lobby after ResultSeconds.")]
        [Min(10f)] public float RunSeconds = 360f;
        [Min(0f)] public float CountdownSeconds = 3f;
        [Tooltip("After a run ends (medal or time up) the result shows this long, then everyone, the balloon and the hoops " +
                 "go back to the start lobby.")]
        [Min(1f)] public float ResultSeconds = 8f;

        [Header("Score")]
        public int BasePoints = 1000;
        public int PointsPerSecondLeft = 10;
        public int PointsPerHoop = 150;
        public int PointsPerRallyPass = 50;
        [Tooltip("Score multiplier per balloon condition at the finish: full, leaking, nearly flat.")]
        public float[] ConditionMultipliers = { 1f, 0.75f, 0.5f };

        [Header("Medals")]
        [Tooltip("A clean run (about 2:15 of the 6 minutes, six hoops, a 10-pass rally) lands near 4650; the thresholds " +
                 "rose by 1800 when the run went from 3 to 6 minutes (180 more seconds left x 10 points).")]
        public int SilverScore = 3900;
        public int GoldScore = 4500;

        public float MultiplierFor(BalloonCondition condition)
        {
            int index = (int)condition;
            return ConditionMultipliers != null && index < ConditionMultipliers.Length ? ConditionMultipliers[index] : 1f;
        }
    }
}
