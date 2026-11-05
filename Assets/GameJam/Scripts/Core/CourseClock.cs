using UnityEngine;

namespace GameJam.Core
{
    /// <summary>
    /// Time that every player roughly agrees on, so moving obstacles stand in the same place on every
    /// headset without sending their positions over the network. Uses local time until a session runs,
    /// then eases onto the shared network clock.
    /// </summary>
    public static class CourseClock
    {
        private const float CatchUpRate = 0.05f;

        private static ISharedTimeSource _source;
        private static float _offset;
        private static int _lastFrame = -1;
        private static bool _isSynced;

        public static float Time
        {
            get
            {
                int frame = UnityEngine.Time.frameCount;
                if (_lastFrame != frame)
                {
                    _lastFrame = frame;
                    Resync();
                }

                return UnityEngine.Time.time + _offset;
            }
        }

        public static void SetSource(ISharedTimeSource source)
        {
            _source = source;
            _isSynced = false;
        }

        public static void ClearSource(ISharedTimeSource source)
        {
            if (_source == source)
            {
                _source = null;
            }
        }

        private static void Resync()
        {
            if (_source == null || !_source.IsRunning)
            {
                return;
            }

            float target = _source.SharedTime - UnityEngine.Time.time;
            _offset = _isSynced ? Mathf.Lerp(_offset, target, CatchUpRate) : target;
            _isSynced = true;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _source = null;
            _offset = 0f;
            _lastFrame = -1;
            _isSynced = false;
        }
    }
}
