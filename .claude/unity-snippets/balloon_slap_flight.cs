// Play mode: puts the balloon at 2 m in open lawn, gives it the velocity a hand slap of HAND (m/s, world) would give
// (through NetworkBalloon's slap conversion), and records the flight. Run again after ~5 s for the report:
// rise to the apex, time to apex, sideways travel, descent speed, time back down to the start height.
var domain = System.AppDomain.CurrentDomain;
var b = GameJam.Net.NetworkBalloon.Instance;
var rows = domain.GetData("slapRows") as System.Collections.Generic.List<UnityEngine.Vector4>;
if (rows != null && rows.Count > 0 && rows[rows.Count - 1].w < 0f)
{
    var s = rows[0]; float apex = s.y, apexT = 0f, backT = -1f; float descent = 0f;
    for (int i = 1; i < rows.Count - 1; i++)
    {
        var r = rows[i];
        if (r.y > apex) { apex = r.y; apexT = r.w; }
        if (backT < 0f && r.w > apexT + 0.1f && r.y <= s.y) backT = r.w;
        if (r.w > apexT + 0.8f && descent == 0f && i > 1) descent = (rows[i - 1].y - r.y) / UnityEngine.Mathf.Max(r.w - rows[i - 1].w, 0.001f);
    }
    var last = rows[rows.Count - 2];
    float side = new UnityEngine.Vector2(last.x - s.x, last.z - s.z).magnitude;
    domain.SetData("slapRows", null);
    return $"HAND rise={apex - s.y:F2}m apexAt={apexT:F2}s sideways={side:F2}m descent={descent:F2}m/s backToStartHeight={(backT < 0 ? "not within 5s" : backT.ToString("F1") + "s")}";
}
var start = new UnityEngine.Vector3(6f, 2f, 0f);
b.ResetTo(start);
var hand = HAND;
var m = typeof(GameJam.Net.NetworkBalloon).GetMethod("SlapVelocity", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
var sc = (GameJam.Core.SlapConfig)typeof(GameJam.Net.NetworkBalloon).GetField("_slapConfig", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(b);
var strength = (float)typeof(GameJam.Net.NetworkBalloon).GetMethod("SlapStrength", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(b, new object[] { hand.magnitude });
var v = (UnityEngine.Vector3)m.Invoke(b, new object[] { hand * sc.BalloonVelocityScale, strength });
var list = new System.Collections.Generic.List<UnityEngine.Vector4>();
domain.SetData("slapRows", list);
float t0 = UnityEngine.Time.realtimeSinceStartup; bool hit = false; int lastFrame = -1;
UnityEditor.EditorApplication.CallbackFunction tick = null;
tick = () =>
{
    if (!UnityEngine.Application.isPlaying) { UnityEditor.EditorApplication.update -= tick; return; }
    if (UnityEngine.Time.frameCount == lastFrame) return; lastFrame = UnityEngine.Time.frameCount;
    float t = UnityEngine.Time.realtimeSinceStartup - t0;
    if (!hit) { if (t < 0.4f) return; b.RequestHit(Fusion.PlayerRef.None, v); hit = true; t0 = UnityEngine.Time.realtimeSinceStartup; t = 0f; }
    var p = b.transform.position;
    list.Add(new UnityEngine.Vector4(p.x, p.y, p.z, t));
    if (t > 5f) { list.Add(new UnityEngine.Vector4(0, 0, 0, -1)); UnityEditor.EditorApplication.update -= tick; }
};
UnityEditor.EditorApplication.update += tick;
return "slap velocity " + v.ToString("F2");
