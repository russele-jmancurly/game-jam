// Easy mode check (Play mode, PC test mode): with KnockbackConfig.KnockdownsEnabled off, nothing knocks the player down.
//   1. a hazard knock (LocalRig.Knock, what the giant, obstacles, flames and pop blasts call): only a short shove
//   2. a networked knockback (NetworkAvatar.SendKnockback, friends' hits): a shove, no ragdoll puppet anywhere
// Logs "[KnockTest]" lines, then "[KnockTest] DONE".
//   unity command script eval --project-path "D:\Unity\game-jam" -- --file .claude/unity-snippets/knockdown_check.cs
using UnityEngine;

var rig = GameJam.Player.LocalRig.Instance;
var avatar = GameJam.Net.NetworkAvatar.Local;
if (rig == null || avatar == null) return "not ready";
System.Action<string, bool, string> report = (name, pass, detail) => Debug.Log($"[KnockTest] {(pass ? "PASS" : "FAIL")} {name}: {detail}");
var config = rig.KnockbackConfig;
report("knockdowns are off", !config.KnockdownsEnabled, $"KnockdownsEnabled={config.KnockdownsEnabled}");

int frame = 0;
float knockedSeconds = 0f, topSpeed = 0f;
bool handsOff = false;
int puppetsAtStart = Object.FindObjectsByType<GameJam.Gameplay.RagdollPuppet>(FindObjectsSortMode.None).Length;
UnityEngine.Events.UnityAction tick = null;
tick = () =>
{
    frame++;
    if (frame == 1 || frame == 200)
    {
        knockedSeconds = 0f;
        topSpeed = 0f;
        handsOff = false;
        rig.RespawnAtCheckpoint();
    }

    if (frame == 20) rig.Knock(Vector3.forward, 7f, 1.5f);                     // a big hazard knock (giant-sized)
    if (frame == 220) avatar.SendKnockback(Vector3.forward, 7f, 1.5f);         // a knockdown over the network

    if (rig.IsKnockedDown) knockedSeconds += Time.deltaTime;
    if (rig.Config != null && !rig.AreControllersTracked) handsOff = true;
    topSpeed = Mathf.Max(topSpeed, rig.Body.linearVelocity.magnitude);

    if (frame == 180)
        report("hazard knock is a shove", knockedSeconds < 0.3f && topSpeed > 1f && topSpeed < 5f, // the respawn drop adds ~3 m/s; a 7 m/s knock would show
            $"knocked down for {knockedSeconds:F2} s, top speed {topSpeed:F2} m/s (shove {config.PlayerPushForce} m/s)");

    if (frame == 380)
    {
        int puppets = Object.FindObjectsByType<GameJam.Gameplay.RagdollPuppet>(FindObjectsSortMode.None).Length;
        report("networked knockback is a shove, no ragdoll", knockedSeconds < 0.3f && puppets == puppetsAtStart && topSpeed > 1f,
            $"knocked down for {knockedSeconds:F2} s, top speed {topSpeed:F2} m/s, ragdoll puppets {puppets}");
        Application.onBeforeRender -= tick;
        Debug.Log("[KnockTest] DONE");
    }
};
Application.onBeforeRender += tick;
return "KnockTest started";
