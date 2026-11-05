namespace GameJam.Core
{
    /// <summary>A controller rumble: amplitude 0-1 for a number of seconds.</summary>
    public readonly struct HapticPulse
    {
        public static readonly HapticPulse None = new HapticPulse(0f, 0f);

        public readonly float Amplitude;
        public readonly float Seconds;

        public HapticPulse(float amplitude, float seconds)
        {
            Amplitude = amplitude;
            Seconds = seconds;
        }
    }
}
