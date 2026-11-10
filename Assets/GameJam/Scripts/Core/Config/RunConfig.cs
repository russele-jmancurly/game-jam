using UnityEngine;

namespace GameJam.Core
{
    /// <summary>
    /// Tuning for the timed run and its score (GDD: Core loop and time limit). Open yard: clear every hoop in any order,
    /// then drop the balloon down through the finish basket.
    /// </summary>
    [CreateAssetMenu(menuName = "Game Jam/Config/Run Config", fileName = "RunConfig")]
    public sealed class RunConfig : ScriptableObject
    {
        [Header("Run")]
        [Tooltip("Length of a run: 6 minutes. When it runs out the referee whistles, TIME'S UP shows in everyone's view and " +
                 "the crew goes back to the lobby after ResultSeconds.")]
        [Min(10f)] public float RunSeconds = 480f; // 8 minutes: the trail is ~135 m with an obstacle in every gap (it was 6 minutes for ~87 m)
        [Min(0f)] public float CountdownSeconds = 3f;
        [Tooltip("After a run ends (finish or time up) the result shows this long, then everyone, the balloon and the hoops " +
                 "go back to the start lobby.")]
        [Min(1f)] public float ResultSeconds = 10f;

        [Header("Score")]
        public int BasePoints = 1000;
        public int PointsPerSecondLeft = 10;
        public int PointsPerHoop = 150;
        public int PointsPerRallyPass = 50;
        [Tooltip("Score multiplier per balloon condition at the finish: full, leaking, nearly flat.")]
        public float[] ConditionMultipliers = { 1f, 0.75f, 0.5f };

        public float MultiplierFor(BalloonCondition condition)
        {
            int index = (int)condition;
            return ConditionMultipliers != null && index < ConditionMultipliers.Length ? ConditionMultipliers[index] : 1f;
        }
    }
}
