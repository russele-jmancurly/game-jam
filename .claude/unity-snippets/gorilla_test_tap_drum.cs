// Drums one hand on the ground 5 times (lift 6 cm, tap down, 40 frames per tap): expects exactly 5 tap sounds.
using UnityEngine;
var rig = GameJam.Player.LocalRig.Instance;
var p = Object.FindFirstObjectByType<GorillaLocomotion.Player>();
rig.GetComponent<GameJam.Player.DesktopRigInput>().enabled = false;
var field = typeof(GorillaLocomotion.Player).GetField("lastLeftTap", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
Vector3 home = new Vector3(-0.25f, 0.85f, 0.3f);
float gy = 0f, last = 0f;
int f = -120, taps = 0;
UnityEngine.Events.UnityAction tick = null;
tick = () =>
{
    f++;
    if (f <= 0)
    {
        rig.LeftController.localPosition = home;
        if (f == 0)
        {
            Physics.Raycast(rig.Head.position, Vector3.down, out RaycastHit h, 8f, GameJam.Core.GameLayers.DefaultMask);
            gy = rig.TrackingSpace.InverseTransformPoint(h.point).y;
            last = (float)field.GetValue(p);
        }
        return;
    }
    int phase = (f - 1) % 40;
    float lift = phase < 20 ? 0.06f * (1f - phase / 19f) : 0.06f * ((phase - 20) / 19f); // down over 20 frames, up over 20
    // The hand is a 5 cm sphere: its centre meets the ground at gy + 0.05. Bottom = 1 cm pressed in, top = 5 cm clear.
    rig.LeftController.localPosition = new Vector3(-0.25f, gy + 0.04f + lift, 0.35f);
    float now = (float)field.GetValue(p);
    if (now != last) { taps++; last = now; if (System.Environment.GetEnvironmentVariable("LOCOTEST_TRACE") == "1") { var bf = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance; var tp = (Vector3)typeof(GorillaLocomotion.Player).GetField("lastLeftTapPoint", bf).GetValue(p); var tn = (Vector3)typeof(GorillaLocomotion.Player).GetField("lastLeftTapNormal", bf).GetValue(p); Debug.Log($"[LocoTrace] tap #{taps} f={f} follower={p.leftFollower.position.ToString("F3")} ctrl={rig.LeftController.position.ToString("F3")} tapPoint={tp.ToString("F3")} normal={tn.ToString("F2")} root={p.transform.position.ToString("F3")}"); } }
    if (f == 40 * 5 + 5) // the 6th cycle would tap at frame ~218
    {
        Application.onBeforeRender -= tick;
        rig.LeftController.localPosition = home;
        rig.GetComponent<GameJam.Player.DesktopRigInput>().enabled = true;
        Debug.Log($"[LocoTest] {(taps == 5 ? "PASS" : "FAIL")} drumming taps: {taps} tap sounds for 5 deliberate taps (lift 6 cm each)");
    }
};
Application.onBeforeRender += tick;
return "drum test armed";
