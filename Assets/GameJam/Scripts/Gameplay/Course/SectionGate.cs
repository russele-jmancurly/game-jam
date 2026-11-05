using UnityEngine;

namespace GameJam.Gameplay
{
    /// <summary>Checkpoint trigger: the balloon passing through it unlocks the next section.</summary>
    public sealed class SectionGate : MonoBehaviour
    {
        [SerializeField, Min(1)] private int _sectionIndex = 1;

        public int SectionIndex => _sectionIndex;

        public void Configure(int sectionIndex) => _sectionIndex = sectionIndex;
    }
}
