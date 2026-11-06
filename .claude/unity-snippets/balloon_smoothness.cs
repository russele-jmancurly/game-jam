// Play mode: hits the balloon sideways and records its rendered position every frame for ~1.5 s, then (second run with
// the recorder finished) reports how many frames repeated the previous position (stepping) and the speed profile.
var domain = System.AppDomain.CurrentDomain;
var rows = domain.GetData("balloonRows") as System.Collections.Generic.List<string>;
if (rows != null && rows.Count > 0 && rows[rows.Count - 1] == "done")
{
    int frames = 0, repeats = 0; float maxJump = 0f, sumJump = 0f; string speeds = "";
    UnityEngine.Vector3? last = null;
    for (int i = 0; i < rows.Count - 1; i++)
    {
        var p = rows[i].Split(',');
        var pos = new UnityEngine.Vector3(float.Parse(p[1]), float.Parse(p[2]), float.Parse(p[3]));
        if (last.HasValue) { float d = (pos - last.Value).magnitude; frames++; if (d < 0.0005f) repeats++; maxJump = UnityEngine.Mathf.Max(maxJump, d); sumJump += d; }
        if (i % 15 == 0) speeds += p[0] + "s:" + p[4] + " ";
        last = pos;
    }
    domain.SetData("balloonRows", null);
    return $"frames={frames} repeatedPositionFrames={repeats} avgStep={sumJump / UnityEngine.Mathf.Max(frames, 1):F4} maxStep={maxJump:F4} (smooth: steps even, no repeats) speeds m/s: {speeds}";
}
var b = GameJam.Net.NetworkBalloon.Instance;
var rig = GameJam.Player.LocalRig.Instance;
var start = rig != null ? rig.Head.position + rig.Head.forward * 1.2f : new UnityEngine.Vector3(0f, 2f, 0f);
start.y = UnityEngine.Mathf.Max(start.y, 1.5f);
b.ResetTo(start);
var list = new System.Collections.Generic.List<string>();
domain.SetData("balloonRows", list);
float t0 = UnityEngine.Time.realtimeSinceStartup; int lastFrame = -1; bool hit = false;
UnityEditor.EditorApplication.CallbackFunction tick = null;
tick = () =>
{
    if (!UnityEngine.Application.isPlaying) { UnityEditor.EditorApplication.update -= tick; return; }
    if (UnityEngine.Time.frameCount == lastFrame) return;
    lastFrame = UnityEngine.Time.frameCount;
    float t = UnityEngine.Time.realtimeSinceStartup - t0;
    if (!hit && t > 0.3f) { b.RequestHit(Fusion.PlayerRef.None, new UnityEngine.Vector3(3f, 1.5f, 0f)); hit = true; }
    if (!hit) return;
    var p = b.transform.position;
    list.Add($"{t:F2},{p.x:F4},{p.y:F4},{p.z:F4},{b.Velocity.magnitude:F2}");
    if (t > 2.3f) { list.Add("done"); UnityEditor.EditorApplication.update -= tick; }
};
UnityEditor.EditorApplication.update += tick;
return "recording balloon";
