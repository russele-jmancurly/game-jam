// Hand-stuck regression test (Play mode, offline, PC mode; no headset needed).
//   unity command script eval --project-path "D:\Unity\game-jam" -- --file .claude/unity-snippets/hand_stuck_test.cs
// Plants a hand with random strokes on the lawn, the start stage, the paddling pool floor and the pool rim (its box seams),
// then pulls the hand 35 cm straight off the surface and holds it there. The glove must follow the controller again
// (within 5 cm). Logs one "[StuckTest]" line per site (strokes, stuck count) and a detail line per stuck stroke, then DONE.
// Env STUCKTEST_STROKES=<n> sets strokes per site (default 40).
using UnityEngine;

var rig = GameJam.Player.LocalRig.Instance;
var p = Object.FindFirstObjectByType<GorillaLocomotion.Player>();
var desk = rig.GetComponent<GameJam.Player.DesktopRigInput>();
desk.enabled = false;
Transform ts = rig.TrackingSpace;
int mask = GameJam.Core.GameLayers.DefaultMask;
rig.Head.localPosition = new Vector3(0f, 1.2f, 0f);
rig.Head.localRotation = Quaternion.identity;
Vector3 homeL = new Vector3(-0.25f, 0.85f, 0.3f), homeR = new Vector3(0.25f, 0.85f, 0.3f);
var rng = new System.Random(1234);
System.Func<float, float, float> rand = (a, b) => a + (float)rng.NextDouble() * (b - a);
int strokesPerSite = int.TryParse(System.Environment.GetEnvironmentVariable("STUCKTEST_STROKES"), out int n) ? n : 40;

Vector3 pool = GameJam.Editor.BackyardCourseBuilder.PoolCenter;
// Each site: where the feet go, which way the player faces, and whether strokes aim at the floor or ahead (a wall); wall
// strokes aim between upMin and upMax (vertical component of the aim, forward = 1).
var sites = new System.Collections.Generic.List<(string name, Vector3 feet, float yaw, bool wall, float upMin, float upMax)>
{
    ("lawn", GameJam.Core.CourseLayout.PlayerSpawn(1, 0), 0f, false, 0f, 0f),
    ("stage", new Vector3(-2f, 0.4f, -6f), 0f, false, 0f, 0f),
    ("pool floor", pool + new Vector3(-3f, 0.16f, 2f), 30f, false, 0f, 0f),
    ("pool rim (box middle)", pool + new Vector3(0f, 0.16f, 7.9f), 0f, true, -0.9f, 0.1f),
    ("pool rim (seam)", pool + Quaternion.Euler(0f, 9f, 0f) * new Vector3(0f, 0.16f, 7.9f), 9f, true, -0.9f, 0.1f),
    ("pool rim (seam 2)", pool + Quaternion.Euler(0f, 117f, 0f) * new Vector3(0f, 0.16f, 7.9f), 117f, true, -0.9f, 0.1f),
    // Pool corners: where the floor meets the rim wall (on an axis the square floor stops 5 cm short of the wall: a crack),
    // the same at a seam between rim blocks, the square floor's corner sticking out past the round rim onto the lawn
    // (a 16 cm step), and the 18 cm slot between the rim top and the diving board passing over it.
    ("pool corner: floor meets wall (axis crack)", pool + new Vector3(0f, 0.16f, 8.0f), 0f, true, -1.8f, -0.5f),
    ("pool corner: floor meets wall (seam)", pool + Quaternion.Euler(0f, 45f, 0f) * new Vector3(0f, 0.16f, 8.0f), 45f, true, -1.8f, -0.5f),
    ("pool corner: square floor step outside", pool + new Vector3(-9.2f, 0f, -9.2f), 45f, true, -1.8f, -0.4f),
    ("pool corner: diving board over the rim", pool + new Vector3(-7.6f, 0.16f, 0.4f), 270f, true, 0f, 0.9f),
};
string onlySites = System.Environment.GetEnvironmentVariable("STUCKTEST_ONLY");
if (!string.IsNullOrEmpty(onlySites)) sites.RemoveAll(s => !s.name.Contains(onlySites));

float gap37 = 0f;
int site = 0, stroke = 0, frame = 0, stuck = 0, hand = 0, settle = 0, skipped = 0;
bool skipStroke = false;
int waitPlan = 0, placedStroke = -1;
Vector3 a = default, press = default, drag = default, lift = default;
var details = new System.Text.StringBuilder();
float startYaw = ts.eulerAngles.y;

System.Action place = () =>
{
    var s = sites[site];
    float turn = s.yaw - ts.eulerAngles.y;
    rig.Turn(turn);
    rig.RespawnAt(s.feet);
    rig.LeftController.localPosition = homeL;
    rig.RightController.localPosition = homeR;
};

// Plans one stroke in tracking-space coordinates (the arm moves relative to the body, as in the headset).
System.Func<bool> plan = () =>
{
    var s = sites[site];
    Vector3 head = rig.Head.position;
    Vector3 fwd = Vector3.ProjectOnPlane(ts.forward, Vector3.up).normalized;
    Vector3 right = Vector3.Cross(Vector3.up, fwd);
    Vector3 aim = s.wall
        ? fwd + right * rand(-0.5f, 0.5f) + Vector3.up * rand(s.upMin, s.upMax)
        : fwd * rand(0.15f, 0.6f) + right * rand(-0.45f, 0.45f) + Vector3.down;
    if (!Physics.Raycast(head, aim.normalized, out RaycastHit hit, 1.8f, mask, QueryTriggerInteraction.Ignore) || hit.distance > 1.1f) return false; // a real arm reaches ~1 m
    Vector3 nrm = hit.normal;
    Vector3 tangent = Vector3.ProjectOnPlane(Random.onUnitSphere, nrm).normalized;
    Vector3 S = hit.point;
    a = ts.InverseTransformPoint(S + nrm * 0.25f);
    press = ts.InverseTransformPoint(S - nrm * rand(0.01f, 0.07f));
    drag = ts.InverseTransformPoint(S - nrm * rand(0.0f, 0.05f) + tangent * rand(0.05f, 0.25f));
    lift = drag + ts.InverseTransformDirection(nrm) * 0.35f;
    return (ts.TransformPoint(lift) - head).magnitude < 1.3f; // inside MaxArmLength (1.5 m), or the glove is clamped short of it
};

UnityEngine.Events.UnityAction tick = null;
tick = () =>
{
    if (site >= sites.Count) return;
    Transform ctrl = hand == 0 ? rig.LeftController : rig.RightController;
    Transform other = hand == 0 ? rig.RightController : rig.LeftController;
    if (frame == 0)
    {
        if (stroke % 10 == 0 && placedStroke != stroke) { placedStroke = stroke; place(); settle = 45; return; }
        if (settle > 0) { settle--; return; } // let the body land after the respawn
        bool planned = false;
        for (int t = 0; t < 20 && !planned; t++) planned = plan();
        if (!planned && ++waitPlan < 150) return; // airborne after the last push: wait to land
        waitPlan = 0;
        if (!planned) { skipped++; skipStroke = true; frame = 50; }
    }

    other.localPosition = hand == 0 ? homeR : homeL;
    int f = frame++;
    if (f < 6) ctrl.localPosition = Vector3.Lerp(hand == 0 ? homeL : homeR, a, f / 5f);
    else if (f < 16) ctrl.localPosition = Vector3.Lerp(a, press, (f - 6) / 9f);
    else if (f < 28) ctrl.localPosition = Vector3.Lerp(press, drag, (f - 16) / 11f);
    else if (f < 36) ctrl.localPosition = Vector3.Lerp(drag, lift, (f - 28) / 7f);
    else if (f < 50) { ctrl.localPosition = lift; if (f == 37) gap37 = ((hand == 0 ? p.leftFollower : p.rightFollower).position - ctrl.position).magnitude; }
    else
    {
        Transform glove = hand == 0 ? p.leftFollower : p.rightFollower;
        float gap = skipStroke ? 0f : (glove.position - ctrl.position).magnitude;
        skipStroke = false;
        if (gap > 0.05f)
        {
            stuck++;
            var hits = Physics.OverlapSphere(glove.position, 0.045f, mask, QueryTriggerInteraction.Collide);
            string names = string.Join(",", System.Linq.Enumerable.Select(hits, h => h.name + (h.isTrigger ? "(trigger)" : "")));
            if (details.Length < 3000)
                details.Append($"\n  stroke {stroke} {(hand == 0 ? "L" : "R")}: glove {gap:F2} m from controller (1 frame after the lift: {gap37:F2}), body speed {rig.Body.linearVelocity.magnitude:F1} m/s, touching={p.IsHandTouching(hand == 0)}, glove overlaps [{names}]");
        }
        frame = 0;
        stroke++;
        hand = 1 - hand;
        if (stroke >= strokesPerSite)
        {
            Debug.Log($"[StuckTest] {(stuck == 0 && skipped < strokesPerSite / 2 ? "PASS" : "FAIL")} {sites[site].name}: {stuck}/{strokesPerSite - skipped} strokes left the glove stuck ({skipped} skipped: no surface within reach){details}");
            details.Clear();
            stuck = 0; stroke = 0; skipped = 0; placedStroke = -1; site++;
            if (site >= sites.Count)
            {
                Application.onBeforeRender -= tick;
                rig.LeftController.localPosition = homeL;
                rig.RightController.localPosition = homeR;
                rig.Turn(startYaw - ts.eulerAngles.y);
                rig.RespawnAtCheckpoint();
                desk.enabled = true;
                Debug.Log("[StuckTest] DONE");
            }
        }
    }
};
Application.onBeforeRender += tick;
return $"StuckTest started: {sites.Count} sites x {strokesPerSite} strokes";
