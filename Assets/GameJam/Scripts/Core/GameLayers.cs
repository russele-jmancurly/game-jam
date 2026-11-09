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

        /// <summary>
        /// The balloon's own layer (named "Balloon" by the scene builder). It bounces off the world (Default) and
        /// thrown items, but never off players' local rig colliders (Ignore Raycast), which only exist on one headset
        /// and would make the forecast physics disagree between players.
        /// </summary>
        public const int Balloon = 8;

        /// <summary>
        /// Loose and thrown items (named "Item" by the scene builder). Not Default, so gorilla hands never grip an item as if
        /// it were ground; items still collide with the world, the balloon and each other.
        /// </summary>
        public const int Item = 9;

        /// <summary>
        /// A knocked-down giant's ragdoll bones (named "Ragdoll" by the scene builder). They only land on the world (Default)
        /// and each other: never on players, the balloon or items, so a falling giant can't shove anyone or bat the balloon.
        /// </summary>
        public const int Ragdoll = 10;

        public const int DefaultMask = 1 << Default;
    }
}
