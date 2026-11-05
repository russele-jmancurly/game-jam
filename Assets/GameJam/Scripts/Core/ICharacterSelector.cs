namespace GameJam.Core
{
    /// <summary>Lets local code (the character pedestals) change the local player's character through the network.</summary>
    public interface ICharacterSelector
    {
        int CurrentCharacter { get; }

        void SelectCharacter(int rosterIndex);
    }
}
