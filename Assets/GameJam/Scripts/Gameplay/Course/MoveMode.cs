namespace GameJam.Gameplay
{
    public enum MoveMode : byte
    {
        Spin,
        Swing,
        PingPong,

        /// <summary>Travels along waypoints through the yard (passing objects).</summary>
        Path,
    }
}
