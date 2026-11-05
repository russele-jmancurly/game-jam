namespace GameJam.Core
{
    /// <summary>The balloon's three air levels. One more leak step after NearlyFlat pops it.</summary>
    public enum BalloonCondition : byte
    {
        Full = 0,
        Leaking = 1,
        NearlyFlat = 2,
    }
}
