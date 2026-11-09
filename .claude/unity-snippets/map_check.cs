// Play mode, after a map change. Part 1 (instant): item spawns and the giant's spawn/wander spots are inside the fence and
// not inside solid geometry. Part 2 (SECONDS s, so swings and spinners cover their range): every collider in the Course
// (lawn excluded; balloon-popping triggers included) vs every hoop ring (< 1.2 m from the centre can clip it) and every
// checkpoint's waiting balloon (< 0.35 m touches it). Run again after it finishes for the part-2 report.
const float SECONDS = 12f; // part 2 sampling time (real seconds)
var domain = System.AppDomain.CurrentDomain;
if (domain.GetData("mapCheck") is System.Collections.Generic.Dictionary<string, (float, string)> res && domain.GetData("mapCheckDone") is bool fin && fin)
{
    var sb = new System.Text.StringBuilder();
    foreach (var kv in res) { bool hoop = kv.Key.StartsWith("Hoop"); bool bad = kv.Value.Item1 < (hoop ? 1.2f : 0.35f); sb.Append(kv.Key).Append(" nearest ").Append(kv.Value.Item2).Append(' ').Append(kv.Value.Item1.ToString("F2")).Append(bad ? " CLASH" : " ok").Append('\n'); }
    domain.SetData("mapCheck", null); domain.SetData("mapCheckDone", null);
    return sb.ToString();
}
var course = UnityEngine.GameObject.Find("Course").transform;
float hw = GameJam.Editor.BackyardCourseBuilder.HalfWidth, south = GameJam.Editor.BackyardCourseBuilder.SouthEdge, north = GameJam.Editor.BackyardCourseBuilder.NorthEdge;
bool Inside(UnityEngine.Vector3 p) => p.x > -hw + 0.5f && p.x < hw - 0.5f && p.z > south + 0.5f && p.z < north - 0.5f;
bool Buried(UnityEngine.Vector3 p, float r)
{
    foreach (var c in UnityEngine.Physics.OverlapSphere(p, r, ~0, UnityEngine.QueryTriggerInteraction.Ignore))
        if (c.name != "Lawn" && c.transform.IsChildOf(course)) return true;
    return false;
}
var part1 = new System.Text.StringBuilder("part 1: ");
int bad1 = 0;
foreach (var s in UnityEngine.Object.FindObjectsByType<GameJam.Gameplay.ItemSpawnPoint>(UnityEngine.FindObjectsSortMode.None))
{
    var p = s.transform.position;
    if (!Inside(p) || Buried(p, 0.2f)) { bad1++; part1.Append(s.name).Append(" at ").Append(p.ToString("F1")).Append(Inside(p) ? " buried; " : " outside; "); }
}
var spots = new System.Collections.Generic.List<UnityEngine.Vector3>(GameJam.Editor.BackyardCourseBuilder.RoamerWanderPoints); spots.AddRange(GameJam.Editor.BackyardCourseBuilder.RoamerSpawns);
foreach (var p in spots)
{
    var body = p + UnityEngine.Vector3.up * 1.5f;
    if (!Inside(p) || Buried(body, 1f)) { bad1++; part1.Append("roamer spot ").Append(p.ToString("F0")).Append(Inside(p) ? " blocked; " : " outside; "); }
}
// The giant doesn't navigate by colliders: it steers around RoamerAvoidCircles with its body radius
// (0.6 x RoamerScale 2.4 x 0.5 = 0.72 m). A spot inside a circle + that radius can never be reached exactly.
const float giantBody = 0.72f;
foreach (var p in spots)
    foreach (var circle in GameJam.Editor.BackyardCourseBuilder.RoamerAvoidCircles)
    {
        float d = new UnityEngine.Vector2(p.x - circle.x, p.z - circle.y).magnitude;
        if (d < circle.z + giantBody) { bad1++; part1.Append("roamer spot ").Append(p.ToString("F0")).Append(" inside avoid circle ").Append(circle.ToString("F1")).Append("; "); }
    }
// Every checkpoint's 4 player slots: standing on lawn or deck (not water, flames, a pool or inside anything).
foreach (var sp in UnityEngine.Object.FindObjectsByType<GameJam.Core.SpawnPoint>(UnityEngine.FindObjectsSortMode.None))
{
    int k = int.Parse(sp.name.Replace("SpawnPoint_Checkpoint", ""));
    for (int slot = 0; slot < 4; slot++)
    {
        var feet = GameJam.Core.CourseLayout.PlayerSpawn(k, slot);
        string problem = null;
        if (!Inside(feet)) problem = "outside the fence";
        else if (Buried(feet + UnityEngine.Vector3.up * 0.6f, 0.3f)) problem = "inside something";
        else
        {
            foreach (var c in UnityEngine.Physics.OverlapSphere(feet + UnityEngine.Vector3.up * 0.5f, 0.4f, ~0, UnityEngine.QueryTriggerInteraction.Collide))
                if (c.GetComponentInParent<GameJam.Gameplay.Water>() != null || c.GetComponentInParent<GameJam.Gameplay.PulsingHazard>() != null || c.GetComponentInParent<GameJam.Gameplay.Sharp>() != null) problem = "in " + c.name;
            // Highest map surface under the slot (players' own rigs and avatars aren't part of the Course).
            float top = -99f;
            foreach (var hit in UnityEngine.Physics.RaycastAll(feet + UnityEngine.Vector3.up * 2f, UnityEngine.Vector3.down, 3f, ~0, UnityEngine.QueryTriggerInteraction.Ignore))
                if (hit.collider.transform.IsChildOf(course)) top = UnityEngine.Mathf.Max(top, hit.point.y);
            if (problem == null && (top < -50f || top > 0.6f)) problem = "not on lawn or deck";
        }
        if (problem != null) { bad1++; part1.Append(sp.name).Append(" slot ").Append(slot).Append(' ').Append(problem).Append("; "); }
    }
}

// Solid set-piece colliders that overlap each other (stations packed too tightly). Parts of one piece touching is normal:
// read the list and judge. Lawn and fence are skipped.
var solids = new System.Collections.Generic.List<UnityEngine.Collider>();
foreach (var c in course.GetComponentsInChildren<UnityEngine.Collider>(true))
    if (!c.isTrigger && c.enabled && c.name != "Lawn" && !c.name.StartsWith("Picket") && !(c is UnityEngine.MeshCollider mc && !mc.convex)) solids.Add(c);
var overlaps = new System.Collections.Generic.HashSet<string>();
for (int a = 0; a < solids.Count; a++)
    for (int b = a + 1; b < solids.Count; b++)
    {
        var ca = solids[a]; var cb = solids[b];
        if (!ca.bounds.Intersects(cb.bounds)) continue;
        if (UnityEngine.Physics.ComputePenetration(ca, ca.transform.position, ca.transform.rotation, cb, cb.transform.position, cb.transform.rotation, out _, out float depth) && depth > 0.05f)
        {
            string x = string.CompareOrdinal(ca.name, cb.name) < 0 ? ca.name + "+" + cb.name : cb.name + "+" + ca.name;
            overlaps.Add(x);
        }
    }
// Giants only walk straight lines that clear their avoid circles (MischiefMaker.HasClearPath): every wander spot must be
// reachable from every other in hops, or a giant can get stranded on one side of a set-piece wall.
bool Clear(UnityEngine.Vector3 from, UnityEngine.Vector3 to)
{
    var a2 = new UnityEngine.Vector2(from.x, from.z); var ab = new UnityEngine.Vector2(to.x, to.z) - a2;
    foreach (var circle in GameJam.Editor.BackyardCourseBuilder.RoamerAvoidCircles)
    {
        var c = new UnityEngine.Vector2(circle.x, circle.y);
        float t = UnityEngine.Mathf.Clamp01(UnityEngine.Vector2.Dot(c - a2, ab) / UnityEngine.Mathf.Max(ab.sqrMagnitude, 0.0001f));
        if ((a2 + ab * t - c).magnitude < circle.z + giantBody + 0.3f) return false;
    }
    return true;
}
var reached = new System.Collections.Generic.HashSet<int> { 0 };
var queue = new System.Collections.Generic.Queue<int>(); queue.Enqueue(0);
while (queue.Count > 0) { int i = queue.Dequeue(); for (int j = 0; j < spots.Count; j++) if (!reached.Contains(j) && Clear(spots[i], spots[j])) { reached.Add(j); queue.Enqueue(j); } }
if (reached.Count < spots.Count) { for (int j = 0; j < spots.Count; j++) if (!reached.Contains(j)) { bad1++; part1.Append("giant spot ").Append(spots[j].ToString("F0")).Append(" unreachable in straight hops; "); } }
part1.Append(bad1 == 0 ? "all item spawns, giant spots and checkpoint slots ok" : bad1 + " problems");
part1.Append(" | touching solid pairs: ").Append(string.Join(", ", overlaps));

var colliders = new System.Collections.Generic.List<UnityEngine.Collider>();
foreach (var c in course.GetComponentsInChildren<UnityEngine.Collider>(true))
{
    if (c.name == "Lawn" || c.GetComponentInParent<GameJam.Gameplay.HoopCheckpoint>() != null) continue;
    if (c.isTrigger && c.GetComponentInParent<GameJam.Gameplay.Water>() == null && c.GetComponentInParent<GameJam.Gameplay.PulsingHazard>() == null && c.GetComponentInParent<GameJam.Gameplay.Sharp>() == null) continue;
    colliders.Add(c);
}
var targets = new System.Collections.Generic.List<(string, UnityEngine.Vector3)>();
foreach (var h in UnityEngine.Object.FindObjectsByType<GameJam.Gameplay.HoopCheckpoint>(UnityEngine.FindObjectsSortMode.None)) targets.Add((h.name, h.transform.position));
foreach (var p in UnityEngine.Object.FindObjectsByType<GameJam.Core.SpawnPoint>(UnityEngine.FindObjectsSortMode.None)) targets.Add(("WaitingBalloon@" + p.name.Replace("SpawnPoint_", ""), p.BalloonPosition));
var dict = new System.Collections.Generic.Dictionary<string, (float, string)>();
foreach (var t in targets) dict[t.Item1] = (999f, "-");
domain.SetData("mapCheck", dict); domain.SetData("mapCheckDone", false);
float t0 = UnityEngine.Time.realtimeSinceStartup;
UnityEditor.EditorApplication.CallbackFunction tick = null;
tick = () =>
{
    if (!UnityEngine.Application.isPlaying) { UnityEditor.EditorApplication.update -= tick; return; }
    foreach (var (tn, tp) in targets)
        foreach (var c in colliders)
        {
            if (c == null || !c.enabled || !c.gameObject.activeInHierarchy) continue;
            bool startStageUnderStartBalloon = tn.EndsWith("Checkpoint0") && c.name == "StartStage";
            float d = (c.isTrigger || c is UnityEngine.MeshCollider m && !m.convex ? c.bounds.ClosestPoint(tp) : c.ClosestPoint(tp)) - tp is var v ? v.magnitude : 0f;
            if (!startStageUnderStartBalloon && d < dict[tn].Item1) dict[tn] = (d, c.name);
        }
    if (UnityEngine.Time.realtimeSinceStartup - t0 > SECONDS) { domain.SetData("mapCheckDone", true); UnityEditor.EditorApplication.update -= tick; }
};
UnityEditor.EditorApplication.update += tick;
return part1 + " | part 2 measuring " + colliders.Count + " colliders vs " + targets.Count + " targets";
