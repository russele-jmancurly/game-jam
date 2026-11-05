using UnityEngine;

namespace GameJam.Core
{
    public static class TimeFormat
    {
        /// <summary>Formats seconds as m:ss, rounding up so the clock reads 0:00 only at the end.</summary>
        public static string MinutesSeconds(float seconds)
        {
            int total = Mathf.CeilToInt(Mathf.Max(0f, seconds));
            return $"{total / 60}:{total % 60:00}";
        }
    }
}
