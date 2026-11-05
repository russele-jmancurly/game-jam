namespace GameJam.Core
{
    /// <summary>
    /// Built-in layers the prototype relies on. The climbable world is Default. Players, triggers and
    /// held items live on Ignore Raycast so gorilla hands never climb them.
    /// </summary>
    public static class GameLayers
    {
        public const int Default = 0;
        public const int IgnoreRaycast = 2;

        public const int DefaultMask = 1 << Default;
    }
}
