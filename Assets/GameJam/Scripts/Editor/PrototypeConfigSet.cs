using GameJam.Core;

namespace GameJam.Editor
{
    /// <summary>All tuning assets the prototype uses, loaded (or created with defaults) from Assets/GameJam/Config.</summary>
    public sealed class PrototypeConfigSet
    {
        public BalloonConfig Balloon { get; private set; }

        public SlapConfig Slap { get; private set; }

        public RunConfig Run { get; private set; }

        public PlayerRigConfig PlayerRig { get; private set; }

        public KnockbackConfig Knockback { get; private set; }

        public static PrototypeConfigSet LoadOrCreate()
        {
            const string folder = PlaceholderAssets.ConfigFolder;
            return new PrototypeConfigSet
            {
                Balloon = PlaceholderAssets.LoadOrCreateConfig<BalloonConfig>($"{folder}/BalloonConfig.asset"),
                Slap = PlaceholderAssets.LoadOrCreateConfig<SlapConfig>($"{folder}/SlapConfig.asset"),
                Run = PlaceholderAssets.LoadOrCreateConfig<RunConfig>($"{folder}/RunConfig.asset"),
                PlayerRig = PlaceholderAssets.LoadOrCreateConfig<PlayerRigConfig>($"{folder}/PlayerRigConfig.asset"),
                Knockback = PlaceholderAssets.LoadOrCreateConfig<KnockbackConfig>($"{folder}/KnockbackConfig.asset"),
            };
        }
    }
}
