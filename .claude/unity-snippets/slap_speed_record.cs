// Play mode with a headset: for SECONDS s, logs every slap this client lands on the balloon (hand speed, launch
// velocity, height). Run again after it finishes for the list. Use it to tune SlapConfig/BalloonConfig to real swings.
var domain = System.AppDomain.CurrentDomain;
var log = domain.GetData("slapLog") as System.Collections.Generic.List<string>;
if (log != null && log.Count > 0 && log[log.Count - 1] == "done") { domain.SetData("slapLog", null); return log.Count <= 1 ? "no slaps landed" : string.Join("\n", log.GetRange(0, log.Count - 1)); }
var b = GameJam.Net.NetworkBalloon.Instance;
var hitField = typeof(GameJam.Net.NetworkBalloon).GetField("_lastHitTime", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
var list = new System.Collections.Generic.List<string>();
domain.SetData("slapLog", list);
float t0 = UnityEngine.Time.realtimeSinceStartup; float seen = (float)hitField.GetValue(b); float pending = -1f; int n = 0;
UnityEditor.EditorApplication.CallbackFunction tick = null;
tick = () =>
{
    if (!UnityEngine.Application.isPlaying || b == null) { UnityEditor.EditorApplication.update -= tick; return; }
    float t = UnityEngine.Time.realtimeSinceStartup - t0;
    float hit = (float)hitField.GetValue(b);
    if (hit != seen) { seen = hit; pending = t + 0.05f; }
    if (pending > 0f && t >= pending)
    {
        pending = -1f; n++;
        var v = b.Velocity;
        list.Add($"slap {n} at {t:F1}s: hand {b.LastSlapHandSpeed:F2} m/s -> balloon {v.magnitude:F2} m/s (up {v.y:F2}, sideways {new UnityEngine.Vector2(v.x, v.z).magnitude:F2}), height {b.transform.position.y:F2}");
    }
    if (t > SECONDS) { list.Add("done"); UnityEditor.EditorApplication.update -= tick; }
};
UnityEditor.EditorApplication.update += tick;
return "recording slaps for SECONDS s";
