// Play mode (lobby is fine: the balloon is hidden, so the giants only wander). Run once to start: speeds the game up
// (Time.timeScale = SPEED) and samples every giant's position each 0.5 game-seconds. Run again after ~50 s for the report:
// share of time west/east of the middle, in the south/middle/north thirds of the yard, and how many of the wander spots each
// giant came within 2 m of. Restores Time.timeScale = 1 when it reports.
var domain = System.AppDomain.CurrentDomain;
if (domain.GetData("roamSamples") is System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<UnityEngine.Vector3>> done)
{
    UnityEngine.Time.timeScale = 1f;
    if (domain.GetData("roamTick") is UnityEditor.EditorApplication.CallbackFunction t) UnityEditor.EditorApplication.update -= t;
    domain.SetData("roamSamples", null);
    var spots = GameJam.Editor.BackyardCourseBuilder.RoamerWanderPoints;
    var lines = new System.Collections.Generic.List<string>();
    foreach (var kv in done)
    {
        var s = kv.Value;
        if (s.Count == 0) continue;
        int west = 0, south = 0, middle = 0, north = 0;
        var visited = new System.Collections.Generic.HashSet<int>();
        foreach (var p in s)
        {
            if (p.x < 0f) west++;
            if (p.z < 18f) south++; else if (p.z < 50f) middle++; else north++;
            for (int i = 0; i < spots.Length; i++)
                if (new UnityEngine.Vector2(p.x - spots[i].x, p.z - spots[i].z).magnitude < 2f) visited.Add(i);
        }
        float n = s.Count;
        lines.Add($"{kv.Key}: {s.Count} samples | west {west / n:P0} east {(n - west) / n:P0} | south {south / n:P0} middle {middle / n:P0} north {north / n:P0} | spots reached {visited.Count}/{spots.Length}");
    }
    return string.Join("\n", lines);
}

float speed = SPEED;
var samples = new System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<UnityEngine.Vector3>>();
domain.SetData("roamSamples", samples);
UnityEngine.Time.timeScale = speed;
float next = UnityEngine.Time.time;
UnityEditor.EditorApplication.CallbackFunction tick = null;
tick = () =>
{
    if (!UnityEngine.Application.isPlaying) { UnityEditor.EditorApplication.update -= tick; return; }
    if (UnityEngine.Time.time < next) return;
    next = UnityEngine.Time.time + 0.5f;
    foreach (var giant in GameJam.Net.MischiefMaker.Active)
    {
        if (!samples.TryGetValue(giant.DisplayName, out var list)) samples[giant.DisplayName] = list = new System.Collections.Generic.List<UnityEngine.Vector3>();
        list.Add(giant.transform.position);
    }
};
domain.SetData("roamTick", tick);
UnityEditor.EditorApplication.update += tick;
return $"sampling {GameJam.Net.MischiefMaker.Active.Count} giants at {speed}x";
