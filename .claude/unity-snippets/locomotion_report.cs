// Summarises locomotion_record.cs: how much the real hands moved, how far the rig root travelled, how often the hands
// were planted, the head-to-hand reach, and frames where a planted hand moved but the root didn't follow.
var rows = System.AppDomain.CurrentDomain.GetData("locoRows") as System.Collections.Generic.List<string>;
if (rows == null) return "no recording";
UnityEngine.Vector3 P(string s) { var p = s.Split(','); return new UnityEngine.Vector3(float.Parse(p[0]), float.Parse(p[1]), float.Parse(p[2])); }
int n = 0, planted = 0, stuckFrames = 0; float handTravel = 0f, rootTravel = 0f, maxReach = 0f, minReach = 99f;
float maxSpeed = 0f; string extra = ""; bool done = false;
UnityEngine.Vector3? lastRoot = null, lastL = null, lastR = null;
var sample = new System.Text.StringBuilder();
foreach (var row in rows)
{
    if (row == "done") { done = true; continue; }
    var c = row.Split(';');
    var root = P(c[1]); var head = P(c[2]); var l = P(c[3]); var r = P(c[4]);
    bool lc = c[7] == "True", rc = c[8] == "True";
    var vel = P(c[9]);
    n++;
    if (lc || rc) planted++;
    maxSpeed = UnityEngine.Mathf.Max(maxSpeed, vel.magnitude);
    float reach = UnityEngine.Mathf.Max((l - head).magnitude, (r - head).magnitude);
    maxReach = UnityEngine.Mathf.Max(maxReach, reach); minReach = UnityEngine.Mathf.Min(minReach, reach);
    if (lastRoot.HasValue)
    {
        // real hand motion relative to the rig root (what the player actually does with their arms)
        float dl = ((l - root) - (lastL.Value - lastRoot.Value)).magnitude, dr = ((r - root) - (lastR.Value - lastRoot.Value)).magnitude;
        handTravel += dl + dr;
        float dRoot = (root - lastRoot.Value).magnitude;
        rootTravel += dRoot;
        if ((lc && dl > 0.01f || rc && dr > 0.01f) && dRoot < 0.002f) stuckFrames++;
    }
    if (n % 60 == 1) sample.Append($"\n t={c[0]} root={c[1]} head={c[2]} L={c[3]} R={c[4]} Lplant={lc} Rplant={rc} vel={c[9]} layers={c[10]} disable={c[11]} body={c[12]}");
    lastRoot = root; lastL = l - root + root; lastL = l; lastR = r;
}
return $"done={done} frames={n} plantedFrames={planted} realHandTravel={handTravel:F2}m rootTravel={rootTravel:F2}m maxSpeed={maxSpeed:F2} reach head-hand {minReach:F2}..{maxReach:F2}m stuckFrames(planted hand moved, root didn't)={stuckFrames}" + sample;
