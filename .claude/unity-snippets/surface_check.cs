// Lists the rig's hand-tap surfaces (Sounds.Surfaces): name, dirt flag, clip count and materials. Edit mode or Play mode.
var sounds = UnityEngine.Object.FindFirstObjectByType<GorillaLocomotion.Sounds>();
if (sounds == null) { return "no Sounds component in the scene"; }
var sb = new System.Text.StringBuilder();
foreach (var s in sounds.Surfaces)
{
    var names = new System.Collections.Generic.List<string>();
    foreach (var m in s.Materials) { names.Add(m != null ? m.name : "null"); }
    sb.AppendLine($"{s.Name} dirt={s.KicksUpDirt} clips={s.Sounds.Length}: {string.Join(", ", names)}");
}
var dirt = UnityEngine.Object.FindFirstObjectByType<GameJam.Player.HandTapEffects>();
var bursts = UnityEngine.Object.FindFirstObjectByType<GameJam.Gameplay.BurstEffects>();
sb.AppendLine($"HandTapEffects={(dirt != null)} BurstEffects={(bursts != null)}");
return sb.ToString();
