// Play mode: can each hoop actually be reached with a real slap? For every hoop, from where a player would launch it (1 m
// above the floor / diving board / merry-go-round / branch, a little before the ring), slaps the balloon through
// NetworkBalloon's own slap conversion at hand speeds of 4.5, 6 and 7.5 m/s (players measured 4.5-7.8), aimed at the
// ring and then pitched up 0-60 degrees, and watches whether it passes through the trigger (wind, flames and water
// included). Run once to start (takes a few minutes), again for the report.
var domain = System.AppDomain.CurrentDomain;
if (domain.GetData("reachDone") is bool done && done)
{
    var lines = (System.Collections.Generic.List<string>)domain.GetData("reachLog");
    domain.SetData("reachDone", null);
    return string.Join("\n", lines);
}

var b = GameJam.Net.NetworkBalloon.Instance;
var hoops = GameJam.Editor.BackyardCourseBuilder.TrailHoops;
var cps = GameJam.Editor.BackyardCourseBuilder.TrailCheckpoints;
var tree = GameJam.Editor.BackyardCourseBuilder.TreeCenter;
var pool = GameJam.Editor.BackyardCourseBuilder.PoolCenter;
var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
var slapVelocity = typeof(GameJam.Net.NetworkBalloon).GetMethod("SlapVelocity", flags);
var slapStrength = typeof(GameJam.Net.NetworkBalloon).GetMethod("SlapStrength", flags);
var slapConfig = (GameJam.Core.SlapConfig)typeof(GameJam.Net.NetworkBalloon).GetField("_slapConfig", flags).GetValue(b);

UnityEngine.Vector3 Launch(int i)
{
    var h = hoops[i];
    var from = i == 0 ? new UnityEngine.Vector3(0f, 0f, -6.2f) : cps[i - 1];
    var dir = new UnityEngine.Vector3(h.x - from.x, 0f, h.z - from.z).normalized;
    switch (i)
    {
        case 2: return new UnityEngine.Vector3(pool.x - 6f, 1.625f + 1f, pool.z);          // the diving board's tip
        case 3: return new UnityEngine.Vector3(h.x, 0.35f + 1f, h.z) - dir * 1.5f;           // on the merry-go-round
        case 4: return new UnityEngine.Vector3(h.x, 1f, h.z) - dir * 4f;                    // lawn 4 m out (closer clips the grill box)
        case 5: return tree + new UnityEngine.Vector3(2.6f, 4.25f + 1f, -0.8f);             // on the low branch
        default: return new UnityEngine.Vector3(h.x, 1f, h.z) - dir * 2f;                   // lawn
    }
}

var log = new System.Collections.Generic.List<string>();
domain.SetData("reachLog", log); domain.SetData("reachDone", false);
float[] speeds = { 4.5f, 6f, 7.5f };
float[] pitches = { 0f, 15f, 30f, 45f, 60f };
int hoop = 0, si = 0, pi = 0, phase = 0; float phaseStart = 0f, best = 999f; bool hoopWorked = false; string worked = "";
UnityEditor.EditorApplication.CallbackFunction tick = null;
tick = () =>
{
    if (!UnityEngine.Application.isPlaying) { UnityEditor.EditorApplication.update -= tick; return; }
    float now = UnityEngine.Time.realtimeSinceStartup;
    var ring = hoops[hoop];
    if (phase == 0)
    {
        b.ResetTo(Launch(hoop)); phase = 1; phaseStart = now; best = 999f; return;
    }
    if (phase == 1)
    {
        if (now - phaseStart < 0.35f) return;
        var from = b.transform.position;
        var toRing = (ring - from).normalized;
        var side = UnityEngine.Vector3.Cross(UnityEngine.Vector3.up, new UnityEngine.Vector3(toRing.x, 0f, toRing.z).normalized);
        if (side.sqrMagnitude < 0.01f) side = UnityEngine.Vector3.right;
        var aim = UnityEngine.Quaternion.AngleAxis(-pitches[pi], side) * toRing;
        float speed = speeds[si];
        float strength = (float)slapStrength.Invoke(b, new object[] { speed });
        var v = (UnityEngine.Vector3)slapVelocity.Invoke(b, new object[] { aim * speed * slapConfig.BalloonVelocityScale, strength });
        b.RequestHit(Fusion.PlayerRef.None, v);
        phase = 2; phaseStart = now; return;
    }
    // phase 2: watch the flight
    float d = (b.transform.position - ring).magnitude;
    best = UnityEngine.Mathf.Min(best, d);
    bool passed = best < 0.95f;
    bool over = passed || now - phaseStart > 4f || b.IsStationed;
    if (!over) return;
    if (passed) { hoopWorked = true; worked += $" {speeds[si]}m/s@{pitches[pi]}deg"; si++; pi = 0; }
    else { pi++; if (pi >= pitches.Length) { worked += $" {speeds[si]}m/s:miss(closest {best:F1}m)"; si++; pi = 0; } }
    if (si >= speeds.Length)
    {
        log.Add($"Hoop{hoop + 1} at {ring.ToString("F1")} from {Launch(hoop).ToString("F1")}: " + (hoopWorked ? "REACHABLE" : "NOT REACHED") + " |" + worked);
        hoop++; si = 0; pi = 0; hoopWorked = false; worked = "";
        if (hoop >= hoops.Length) { domain.SetData("reachDone", true); UnityEditor.EditorApplication.update -= tick; return; }
    }
    phase = 0;
};
UnityEditor.EditorApplication.update += tick;
return "testing " + hoops.Length + " hoops";
