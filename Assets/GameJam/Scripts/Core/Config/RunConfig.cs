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
        [Min(10f)] public float RunSeconds = 180f;
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
        [Tooltip("A clean run (about 2:15 with six hoops and a 10-pass rally) lands near 2850.")]
        public int SilverScore = 2100;
        public int GoldScore = 2700;

        public float MultiplierFor(BalloonCondition condition)
        {
            int index = (int)condition;
            return ConditionMultipliers != null && index < ConditionMultipliers.Length ? ConditionMultipliers[index] : 1f;
        }
    }
}
