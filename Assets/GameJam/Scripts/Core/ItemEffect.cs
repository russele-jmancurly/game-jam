namespace GameJam.Core
{
    /// <summary>
    /// What a thrown item sets off where it lands (GDD: Items, "throw something at it"). The effect is a wind zone that
    /// carries the balloon, shown with particles, for <see cref="ItemDefinition.EffectSeconds"/>.
    /// </summary>
    public enum ItemEffect : byte
    {
        None,

        /// <summary>Bubble wand: a column of bubbles that lifts the balloon straight up (hoops in the tree, on the fence).</summary>
        Updraft,

        /// <summary>Leaf blower: lands and blows toward the nearest hoop not yet cleared.</summary>
        HoopGust,
    }
}
