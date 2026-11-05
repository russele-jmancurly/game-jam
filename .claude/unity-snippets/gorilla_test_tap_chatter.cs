// Counts hand-tap sounds while one hand rests perfectly still on the ground for ~5 s (should be exactly 1 tap).
// The hand is a 5 cm sphere: its centre meets the ground at gy + 0.05, so resting = gy + 0.04 (1 cm pressed in).
using UnityEngine;
var rig = GameJam.Player.LocalRig.Instance;
var p = Object.FindFirstObjectByType<GorillaLocomotion.Player>();
rig.GetComponent<GameJam.Player.DesktopRigInput>().enabled = false;
var field = typeof(GorillaLocomotion.Player).GetField("lastLeftTap", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
Physics.Raycast(rig.Head.position, Vector3.down, out RaycastHit g, 8f, GameJam.Core.GameLayers.DefaultMask);
float gy = rig.TrackingSpace.InverseTransformPoint(g.point).y;
Vector3 home = new Vector3(-0.25f, 0.85f, 0.3f), rest = new Vector3(-0.25f, gy + 0.04f, 0.35f);
int f = -120, taps = 0, flips = 0; // 120 frames to let a respawn settle first
float last = (float)field.GetValue(p);
bool wasTouching = false;
UnityEngine.Events.UnityAction tick = null;
tick = () =>
{
    f++;
    if (f <= 0) { rig.LeftController.localPosition = home; if (f == 0) { Physics.Raycast(rig.Head.position, Vector3.down, out RaycastHit h, 8f, GameJam.Core.GameLayers.DefaultMask); gy = rig.TrackingSpace.InverseTransformPoint(h.point).y; rest = new Vector3(-0.25f, gy + 0.04f, 0.35f); last = (float)field.GetValue(p); } return; }
    rig.LeftController.localPosition = f < 40 ? Vector3.Lerp(home, rest, f / 39f) : rest;
    float now = (float)field.GetValue(p);
    if (now != last) { taps++; last = now; if (System.Environment.GetEnvironmentVariable("LOCOTEST_TRACE") == "1") { var bf = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance; var tp = (Vector3)typeof(GorillaLocomotion.Player).GetField("lastLeftTapPoint", bf).GetValue(p); var tn = (Vector3)typeof(GorillaLocomotion.Player).GetField("lastLeftTapNormal", bf).GetValue(p); Debug.Log($"[LocoTrace] tap #{taps} f={f} follower={p.leftFollower.position.ToString("F3")} ctrl={rig.LeftController.position.ToString("F3")} tapPoint={tp.ToString("F3")} normal={tn.ToString("F2")} root={p.transform.position.ToString("F3")}"); } }
    if (System.Environment.GetEnvironmentVariable("LOCOTEST_TRACE") == "1" && f > 20 && f < 80 && f % 2 == 0) Debug.Log($"[LocoTrace] f={f} touch={p.IsHandTouching(true)} follower={p.leftFollower.position.ToString("F3")} ctrl={rig.LeftController.position.ToString("F3")} root={p.transform.position.ToString("F3")}");
    bool touching = p.IsHandTouching(true);
    if (f > 60 && touching != wasTouching) flips++;
    wasTouching = touching;
    if (f == 560)
    {
        Application.onBeforeRender -= tick;
        rig.LeftController.localPosition = home;
        rig.GetComponent<GameJam.Player.DesktopRigInput>().enabled = true;
        Debug.Log($"[LocoTest] {(taps <= 1 ? "PASS" : "FAIL")} resting hand taps: {taps} tap sounds in ~5 s of a still hand (touch flag flipped {flips} times)");
    }
};
Application.onBeforeRender += tick;
return "chatter test armed";
