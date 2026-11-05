using UnityEngine;

namespace GameJam.Core
{
    /// <summary>Tuning for the timed run and its score (GDD: Core loop and time limit).</summary>
    [CreateAssetMenu(menuName = "Game Jam/Config/Run Config", fileName = "RunConfig")]
    public sealed class RunConfig : ScriptableObject
    {
        [Header("Run")]
        [Min(10f)] public float RunSeconds = 180f;
        [Min(0f)] public float CountdownSeconds = 3f;
        [Min(1)] public int SectionCount = 3;

        [Header("Score")]
        public int BasePoints = 1000;
        public int PointsPerSecondLeft = 10;
        public int PointsPerRallyPass = 50;
        [Tooltip("Score multiplier per balloon condition at the finish: full, leaking, nearly flat.")]
        public float[] ConditionMultipliers = { 1f, 0.75f, 0.5f };

        [Header("Medals")]
        public int SilverScore = 1700;
        public int GoldScore = 2300;

        public float MultiplierFor(BalloonCondition condition)
        {
            int index = (int)condition;
            return ConditionMultipliers != null && index < ConditionMultipliers.Length ? ConditionMultipliers[index] : 1f;
        }
    }
}
