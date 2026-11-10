// Giant kick check (Play mode, offline, PC test mode). A giant goes for the balloon first, and otherwise kicks a nearby
// player into a ragdoll. Puts the first giant on open lawn east of the start stage (14, 0, 2), facing west, then:
//   1) lobby: the player stands 4 m in front of it for 3 s. It must not go after them (kicks only during a run).
//   2) run, balloon low and 3 m from the giant, player 4 m away: it must chase the balloon, not the player.
//   3) run, balloon far away, player 4 m away and standing still: ChasePlayer -> KickWindUp -> Kick, then the player is
//      knocked down (LocalRig.IsKnockedDown, launched) for about RoamerKickKnockdownSeconds, and the view dims.
// Also points ReviewCam_Kick at the giant (display 8). Logs "[KickTest]" lines, then DONE.
//   unity command script eval --project-path "E:\Project\game-jam" -- --file .claude/unity-snippets/giant_kick_check.cs
using UnityEngine;

var giant = GameJam.Net.MischiefMaker.Instance;
var state = GameJam.Net.GameState.Instance;
var balloon = GameJam.Net.NetworkBalloon.Instance;
var rig = GameJam.Player.LocalRig.Instance;
if (giant == null || state == null || balloon == null || rig == null) return "not ready";
System.Action<string> log = s => Debug.Log("[KickTest] " + s);
System.Action<string, bool, string> report = (name, pass, detail) => Debug.Log($"[KickTest] {(pass ? "PASS" : "FAIL")} {name}: {detail}");

var giantPos = new Vector3(14f, 0f, 2f);
var facing = Quaternion.LookRotation(Vector3.left);
var nt = giant.GetComponent<Fusion.NetworkTransform>();
System.Action placeGiant = () => { nt.Teleport(giantPos, facing); giant.transform.SetPositionAndRotation(giantPos, facing); };
Vector3 playerSpot = giantPos + Vector3.left * 4f;
var body = rig.GetComponent<Rigidbody>();

var camGo = GameObject.Find("ReviewCam_Kick");
if (camGo == null)
{
    camGo = new GameObject("ReviewCam_Kick") { hideFlags = HideFlags.DontSave };
    camGo.AddComponent<Camera>().targetDisplay = 7;
}
camGo.transform.position = giantPos + new Vector3(-2f, 3.5f, -10f);
camGo.transform.LookAt(giantPos + new Vector3(-2f, 1.2f, 0f));

// The other giants: park them far off so they stay out of the test.
foreach (var other in GameJam.Net.MischiefMaker.Active)
{
    if (other == giant) continue;
    var far = new Vector3(-20f, 0f, 70f);
    other.GetComponent<Fusion.NetworkTransform>().Teleport(far, Quaternion.identity);
    other.transform.position = far;
}

int phase = 0;
float phaseStart = Time.time;
string moods = "";
GameJam.Net.RoamerMood lastMood = (GameJam.Net.RoamerMood)255;
bool sawDown = false, sawDim = false;
float downAt = -1f, upAt = -1f, maxSpeed = 0f;
var fade = GameJam.Gameplay.ScreenFade.Instance;

UnityEngine.Events.UnityAction tick = null;
tick = () =>
{
    float t = Time.time - phaseStart;
    if (giant.Mood != lastMood)
    {
        lastMood = giant.Mood;
        moods += (moods.Length > 0 ? ">" : "") + lastMood;
    }

    switch (phase)
    {
        case 0: // lobby
            if (t < 0.1f) { placeGiant(); rig.RespawnAt(playerSpot); moods = ""; lastMood = giant.Mood; }
            if (t > 3f)
            {
                report("no kick in the lobby", !moods.Contains("ChasePlayer") && !moods.Contains("Kick"), $"state {state.State}, moods {moods}");
                state.RequestStart();
                phase = 1; phaseStart = Time.time;
            }
            break;
        case 1: // wait for the run
            if (state.State == GameJam.Core.RunState.Running && t > 0.5f)
            {
                placeGiant();
                rig.RespawnAt(playerSpot);
                balloon.ResetTo(giantPos + new Vector3(-0.5f, 1.8f, 3f)); // low, 3 m from the giant: in reach
                balloon.RequestHit(Fusion.PlayerRef.None, Vector3.zero);    // live
                moods = ""; lastMood = giant.Mood;
                phase = 2; phaseStart = Time.time;
            }
            break;
        case 2: // balloon first
            if (t > 1.2f)
            {
                report("balloon first", moods.Contains("Chase") && !moods.Contains("ChasePlayer"), $"moods {moods}");
                balloon.ResetTo(new Vector3(0f, 2f, -40f)); // far away, stationed (giants ignore it there)
                giant.Reroll(); // ends the slap's cooldown: back to wandering (a fixed character keeps its face)
                placeGiant();
                rig.RespawnAt(playerSpot);
                moods = ""; lastMood = giant.Mood;
                phase = 3; phaseStart = Time.time;
            }
            break;
        case 3: // the kick
        {
            float speed = body != null ? body.linearVelocity.magnitude : 0f;
            if (rig.IsKnockedDown && !sawDown && moods.Contains("Kick")) { sawDown = true; downAt = Time.time; }
            if (sawDown) { maxSpeed = Mathf.Max(maxSpeed, speed); if (fade != null && fade.Alpha > 0.2f) sawDim = true; }
            if (sawDown && upAt < 0f && !rig.IsKnockedDown) { upAt = Time.time; }
            if (t > 9f || (upAt > 0f && Time.time - upAt > 0.6f))
            {
                report("chase, wind up, kick", moods.Contains("ChasePlayer>KickWindUp>Kick"), $"moods {moods}");
                report("player knocked down", sawDown && downAt > 0f, $"down {(upAt > 0f ? (upAt - downAt).ToString("F2") : "?")} s, peak speed {maxSpeed:F1} m/s");
                report("view dims while down, clears after", sawDim && (fade == null || fade.Alpha < 0.05f), fade == null ? "no ScreenFade in scene" : $"dimmed {sawDim}, alpha now {fade.Alpha:F2}");
                Application.onBeforeRender -= tick;
                log("DONE");
            }
            break;
        }
    }
};
Application.onBeforeRender += tick;
return "KickTest started (~15 s)";
