using UnityEngine;

namespace GameJam.Gameplay
{
    /// <summary>
    /// The wind an item sets off where it lands (bubble column, leaf-blower gust): a trigger box running along this
    /// transform's forward axis from its origin, with a <see cref="WindVolume"/> the balloon's contact check already reads
    /// and particles that show the flow. Purely local: the owning item positions and switches it on every client from
    /// networked state, so the balloon's owner simulates the same push everyone sees.
    /// </summary>
    public sealed class ItemEffectZone : MonoBehaviour
    {
        [SerializeField] private BoxCollider _trigger;
        [SerializeField] private WindVolume _wind;
        [SerializeField] private ParticleSystem _particles;

        private bool _isOn;

        public void Configure(BoxCollider trigger, WindVolume wind, ParticleSystem particles)
        {
            _trigger = trigger;
            _wind = wind;
            _particles = particles;
        }

        private void Awake() => Apply(false);

        /// <summary>
        /// Places the zone (origin, flow direction, reach, width, strength) and switches it on or off. With a
        /// <paramref name="target"/> the wind homes the balloon onto it (leaf blower to a hoop) and cancels its gravity
        /// with <paramref name="lift"/>; without one it blows straight along the flow (bubble updraft).
        /// </summary>
        public void Show(bool isOn, Vector3 origin, Quaternion flow, float length, float width, float strength, Vector3? target = null, float lift = 0f)
        {
            if (isOn)
            {
                transform.SetPositionAndRotation(origin, flow);
                _trigger.size = new Vector3(width, width, length);
                _trigger.center = new Vector3(0f, 0f, length * 0.5f);
                _wind.Configure(strength);
                _wind.SetTarget(target, lift);

                ParticleSystem.ShapeModule shape = _particles.shape;
                shape.scale = new Vector3(width * 0.8f, width * 0.8f, 0.1f);
                ParticleSystem.MainModule main = _particles.main;
                float speed = Mathf.Max(1f, strength * 0.8f);
                main.startSpeed = speed;
                main.startLifetime = length / speed;
            }

            if (isOn != _isOn)
            {
                Apply(isOn);
            }
        }

        private void Apply(bool isOn)
        {
            _isOn = isOn;
            if (_trigger != null)
            {
                _trigger.enabled = isOn;
            }

            if (_particles == null)
            {
                return;
            }

            if (isOn)
            {
                _particles.Play();
            }
            else
            {
                _particles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
        }
    }
}
