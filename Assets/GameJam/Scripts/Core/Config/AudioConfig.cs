using UnityEngine;

namespace GameJam.Core
{
    /// <summary>
    /// The game's mix: one volume per kind of sound, applied live to every source by SoundLevel, so the balance can be
    /// tuned in Play mode. The clips themselves are normalised by GeneratedAudio, so these numbers are the whole story.
    /// Hand taps and the slide hiss follow PlayerRigConfig (TapVolume, SlideVolume).
    /// </summary>
    [CreateAssetMenu(menuName = "Game Jam/Config/Audio Config", fileName = "AudioConfig")]
    public sealed class AudioConfig : ScriptableObject
    {
        [Range(0f, 1f)] public float Master = 1f;
        [Tooltip("Background music: under the game, never over it.")]
        [Range(0f, 1f)] public float Music = 0.15f;
        [Tooltip("World loops: the fan's wind, the bubble wand, the leaf blower.")]
        [Range(0f, 1f)] public float Ambience = 0.5f;
        [Tooltip("One-shots: balloon slaps and pop, hoop chime, trampoline, win fanfare.")]
        [Range(0f, 1f)] public float Effects = 0.85f;

        [Header("Fades")]
        [Tooltip("Seconds a loop takes to fade in or out when it starts or stops (no clicks, no stutter).")]
        [Min(0.01f)] public float LoopFadeSeconds = 0.25f;
        [Tooltip("Seconds the music takes to fade in when the game starts.")]
        [Min(0.01f)] public float MusicFadeInSeconds = 3f;

        public float Volume(SoundCategory category) => Master * category switch
        {
            SoundCategory.Music => Music,
            SoundCategory.Ambience => Ambience,
            _ => Effects,
        };
    }
}
