namespace GameJam.Core
{
    /// <summary>Anything a local glove can slap: the balloon, a friend, the start button.</summary>
    public interface ISlappable
    {
        /// <summary>Returns true if the slap landed; the glove then cools down and plays the feedback.</summary>
        bool TryReceiveSlap(in SlapContext slap, out HapticPulse feedback);
    }
}
