namespace GameJam.Core
{
    /// <summary>Which volume in <see cref="AudioConfig"/> a sound follows.</summary>
    public enum SoundCategory : byte
    {
        /// <summary>One-shots: balloon slaps and pop, hoop chime, trampoline, win fanfare.</summary>
        Effects,

        /// <summary>World loops: the fan's wind, the bubble wand, the leaf blower.</summary>
        Ambience,

        /// <summary>The background music.</summary>
        Music,
    }
}
