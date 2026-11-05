// Gorilla locomotion play-mode test suite (run in Play mode, offline, PC mode; no headset needed).
// Drives the controllers frame by frame like real arms and logs one "[LocoTest]" line per scenario, then "[LocoTest] DONE".
//   unity command script eval --project-path "E:\Project\game-jam" --file .claude/unity-snippets/gorilla_test_suite.cs
// Scenarios: idle, run (alternating hands), jump (two hands), wall climb, bounce pad, ice slide, snap turn, respawn, PC WASD,
// slap the balloon in a live run. Temporary test objects are destroyed afterwards.
// Debug: set env LOCOTEST_ONLY=<name> (runs idle + that one) and LOCOTEST_TRACE=1 (per-frame climb trace) via script eval.
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

var rig = GameJam.Player.LocalRig.Instance;
var p = Object.FindFirstObjectByType<GorillaLocomotion.Player>();
var desk = rig.GetComponent<GameJam.Player.DesktopRigInput>();
var body = rig.Body;
Transform ts = rig.TrackingSpace;
int mask = GameJam.Core.GameLayers.DefaultMask;
desk.enabled = false;
rig.Head.localPosition = new Vector3(0f, 1.2f, 0f);
rig.Head.localRotation = Quaternion.identity;

Vector3 homeL = new Vector3(-0.25f, 0.85f, 0.3f);
Vector3 homeR = new Vector3(0.25f, 0.85f, 0.3f);
System.Func<float> groundLocalY = () =>
{
    Physics.Raycast(rig.Head.position, Vector3.down, out RaycastHit hit, 8f, mask, QueryTriggerInteraction.Ignore);
    return ts.InverseTransformPoint(hit.point).y;
};
System.Action home = () => { rig.LeftController.localPosition = homeL; rig.RightController.localPosition = homeR; };
System.Func<Vector3, Vector3, float, Vector3> lerp = (a, b, t) => Vector3.Lerp(a, b, Mathf.Clamp01(t));
System.Func<Vector3, bool> bad = v => float.IsNaN(v.x) || float.IsNaN(v.y) || float.IsNaN(v.z) || float.IsInfinity(v.x);
var log = new System.Text.StringBuilder();
System.Action<string, bool, string> report = (name, pass, detail) =>
    Debug.Log($"[LocoTest] {(pass ? "PASS" : "FAIL")} {name}: {detail}");

// One stroke of an arm on the ground: reach forward-down, plant, pull back, lift, swing forward in the air.
System.Func<int, float, float, Vector3> groundStroke = (f, x, gy) =>
{
    Vector3 up = new Vector3(x, gy + 0.35f, 0.45f), down = new Vector3(x, gy - 0.04f, 0.45f);
    Vector3 back = new Vector3(x, gy - 0.04f, -0.15f), backUp = new Vector3(x, gy + 0.35f, -0.15f);
    if (f < 6) return lerp(up, down, f / 5f);
    if (f < 18) return lerp(down, back, (f - 6) / 11f);
    if (f < 24) return lerp(back, backUp, (f - 18) / 5f);
    return lerp(backUp, up, (f - 24) / 7f);
};

// Shared scenario state.
Vector3 start = default;
float peak = 0f, topSpeed = 0f, maxUp = 0f, gyL = 0f, gyR = 0f;
bool flag = false;
long allocIdle = 0, allocRun = 0, lastAlloc = 0;
int idleFrames = 0, runFrames = 0;
GameObject temp = null;
GorillaLocomotion.Surface tempSurface = null;
var balloon = Object.FindFirstObjectByType<GameJam.Net.NetworkBalloon>();

var scenarios = new System.Collections.Generic.List<(string name, System.Func<int, bool> step)>();

scenarios.Add(("idle", f =>
{
    if (f == 0) { home(); start = p.transform.position; lastAlloc = System.GC.GetAllocatedBytesForCurrentThread(); }
    long now = System.GC.GetAllocatedBytesForCurrentThread();
    if (f > 30) { allocIdle += now - lastAlloc; idleFrames++; }
    lastAlloc = now;
    if (f < 120) return false;
    float drift = (p.transform.position - start).magnitude;
    report("idle (standing still, hands in the air)", drift < 0.05f && !bad(p.transform.position) && !p.IsHandTouching(true),
        $"drift={drift:F3} m, touching={p.IsHandTouching(true) || p.IsHandTouching(false)}");
    return true;
}));

scenarios.Add(("run", f =>
{
    if (f == 0) { start = p.transform.position; topSpeed = 0f; gyL = gyR = groundLocalY(); lastAlloc = System.GC.GetAllocatedBytesForCurrentThread(); }
    long now = System.GC.GetAllocatedBytesForCurrentThread();
    if (f > 0) { allocRun += now - lastAlloc; runFrames++; }
    lastAlloc = now;
    const int stroke = 32, strokes = 8;
    if (f < stroke * strokes)
    {
        int fl = f % stroke, fr = (f + stroke / 2) % stroke;
        if (fl == 0) gyL = groundLocalY();
        if (fr == 0) gyR = groundLocalY();
        rig.LeftController.localPosition = groundStroke(fl, -0.25f, gyL);
        rig.RightController.localPosition = groundStroke(fr, 0.25f, gyR);
        topSpeed = Mathf.Max(topSpeed, body.linearVelocity.magnitude);
        return false;
    }
    if (f == stroke * strokes) home();
    if (f < stroke * strokes + 90) return false;
    Vector3 moved = p.transform.position - start;
    float forward = Vector3.Dot(moved, ts.forward);
    report("run (8 alternating hand strokes)", forward > 2f && Mathf.Abs(Vector3.Dot(moved, ts.right)) < 1.5f && !bad(p.transform.position),
        $"forward={forward:F2} m, sideways={Vector3.Dot(moved, ts.right):F2} m, topSpeed={topSpeed:F2} m/s");
    return true;
}));

scenarios.Add(("jump", f =>
{
    if (f == 0) { gyL = groundLocalY(); start = p.transform.position; peak = start.y; flag = false; }
    Vector3 a = new Vector3(0f, gyL + 0.15f, 0.25f), b = new Vector3(0f, gyL - 0.02f, 0.25f), c = new Vector3(0f, gyL - 0.32f, 0.25f);
    if (f < 6) { Vector3 v = lerp(a, b, f / 5f); rig.LeftController.localPosition = v + Vector3.left * 0.2f; rig.RightController.localPosition = v + Vector3.right * 0.2f; return false; }
    if (f < 12) { Vector3 v = lerp(b, c, (f - 6) / 5f); rig.LeftController.localPosition = v + Vector3.left * 0.2f; rig.RightController.localPosition = v + Vector3.right * 0.2f; return false; }
    if (f == 12) home();
    peak = Mathf.Max(peak, p.transform.position.y);
    if (f < 160) return false;
    float rise = peak - start.y;
    report("jump (two-hand push down, release)", rise > 0.35f, $"rise={rise:F2} m (the push alone lifts 0.30 m)");
    return true;
}));

int[] climbState = new int[2], climbTimer = new int[2];
Vector3[] climbPos = new Vector3[2];
float minGap = float.MaxValue;
scenarios.Add(("climb", f =>
{
    if (f < 90) { home(); return false; } // let the previous jump land first
    f -= 90;
    if (f == 0)
    {
        Physics.Raycast(rig.Head.position, Vector3.down, out RaycastHit g, 8f, mask);
        temp = GameObject.CreatePrimitive(PrimitiveType.Cube);
        temp.name = "LocoTest_Wall";
        temp.transform.rotation = Quaternion.LookRotation(ts.forward);
        temp.transform.localScale = new Vector3(3f, 8f, 0.2f);
        temp.transform.position = new Vector3(rig.Head.position.x, g.point.y + 4f, rig.Head.position.z) + ts.forward * 0.55f;
        if (System.Environment.GetEnvironmentVariable("LOCOTEST_TRACE") == "1")
        {
            var near = Physics.OverlapBox(temp.transform.position, temp.transform.localScale * 0.5f, temp.transform.rotation, mask);
            string names = string.Join(",", System.Linq.Enumerable.Select(near, c => c.name));
            Debug.Log($"[LocoTrace] climb setup: head={rig.Head.position} ground={g.collider?.name}@{g.point.y:F2} vel={body.linearVelocity} wall overlaps=[{names}]");
        }
        start = p.transform.position; peak = start.y; minGap = float.MaxValue;
        climbState[0] = climbState[1] = 0;
        return false;
    }
    // A reactive climber, like a player watching the wall: each hand reaches for the wall face until it touches,
    // pulls down while pressing in slightly (a gorilla hand only stays stuck while it moves into the surface), lets go,
    // and resets. Each pull launches the player up the wall; the other hand catches the wall on the way. The right hand
    // starts 20 frames later. Reaching *into* the wall fast would be a push-off, so the last bit of a reach is ~0.3 m/s.
    const int frames = 700;
    int k = f - 1;
    if (k < frames)
    {
        float face = ts.InverseTransformPoint(temp.transform.position - temp.transform.forward * 0.1f).z;
        for (int h = 0; h < 2; h++)
        {
            if (h == 1 && k < 20) { rig.RightController.localPosition = homeR; continue; }
            float x = h == 0 ? -0.2f : 0.2f;
            bool touching = p.IsHandTouching(h == 0);
            Vector3 target = climbPos[h];
            switch (climbState[h])
            {
                case 0: // start a reach from above, 20 cm off the wall
                    target = new Vector3(x, 1.55f, face - 0.2f);
                    climbState[h] = 1; climbTimer[h] = 0;
                    break;
                case 1: // reach toward the face until the hand touches it
                    target.z = Mathf.Min(target.z + (face - target.z > 0.06f ? 0.02f : 0.003f), face + 0.01f);
                    if (touching || ++climbTimer[h] > 60) { climbState[h] = 2; climbTimer[h] = 0; }
                    break;
                case 2: // pull down 60 cm over 17 frames, pressing 4 cm in
                    target += new Vector3(0f, -0.6f / 17f, 0.04f / 17f);
                    if (++climbTimer[h] >= 17) { climbState[h] = 3; climbTimer[h] = 0; }
                    break;
                case 3: // let go: pull the hand back off the wall
                    target.z -= 0.06f;
                    if (++climbTimer[h] >= 3) { climbState[h] = 4; climbTimer[h] = 0; }
                    break;
                default: // swing back up in the air
                    target = Vector3.MoveTowards(target, new Vector3(x, 1.55f, face - 0.2f), 0.08f);
                    if (++climbTimer[h] >= 8) { climbState[h] = 0; }
                    break;
            }
            climbPos[h] = target;
            (h == 0 ? rig.LeftController : rig.RightController).localPosition = target;
        }
        float gap = Vector3.Dot(temp.transform.position - rig.Head.position, ts.forward) - 0.1f;
        minGap = Mathf.Min(minGap, gap);
        if (System.Environment.GetEnvironmentVariable("LOCOTEST_TRACE") == "1" && k % 6 == 0)
        {
            Vector3 lv = ts.InverseTransformDirection(body.linearVelocity);
            Debug.Log($"[LocoTrace] k={k} st={climbState[0]}/{climbState[1]} tL={p.IsHandTouching(true)} tR={p.IsHandTouching(false)} vel(local)=({lv.x:F2},{lv.y:F2},{lv.z:F2}) y={p.transform.position.y - start.y:F2} gap={gap:F2}");
        }
        peak = Mathf.Max(peak, p.transform.position.y);
        return false;
    }
    if (k == frames)
    {
        float gained = peak - start.y;
        // Each pull launches you up and slightly away from the wall (pressing in to keep grip pushes you back), so
        // the scripted climber drifts out of reach after a few pulls; 1 m = more than one pull, i.e. pulls chain.
        report("wall climb (reactive hand-over-hand, 7 s)", gained > 1f && minGap > 0f,
            $"climbed {gained:F2} m up an 8 m wall; head never closer than {minGap:F2} m to the face (inside = negative)");
        home();
        Object.Destroy(temp);
        p.ClearSurfaceCache();
    }
    return k > frames + 150;
}));

scenarios.Add(("bounce", f =>
{
    if (f == 0)
    {
        Physics.Raycast(rig.Head.position + ts.right * 3f, Vector3.down, out RaycastHit g, 8f, mask);
        temp = GameObject.CreatePrimitive(PrimitiveType.Cube);
        temp.name = "LocoTest_BouncePad";
        temp.transform.localScale = new Vector3(2f, 0.2f, 2f);
        temp.transform.position = g.point + Vector3.up * 0.1f;
        tempSurface = temp.AddComponent<GorillaLocomotion.Surface>();
        tempSurface.bouncePercentage = 0.8f;
        p.ClearSurfaceCache();
        rig.RespawnAt(g.point + Vector3.up * 1.8f);
        maxUp = 0f; flag = false;
        return false;
    }
    // Hands held low, below the feet, so they meet the pad first.
    rig.LeftController.localPosition = new Vector3(-0.2f, 0.45f, 0.15f);
    rig.RightController.localPosition = new Vector3(0.2f, 0.45f, 0.15f);
    if (body.linearVelocity.y < -2f) flag = true; // fell fast enough to bounce
    if (flag) maxUp = Mathf.Max(maxUp, body.linearVelocity.y);
    if (f < 150) return false;
    report("bounce pad (Surface bounce 0.8)", maxUp > 1.5f, $"rebound up at {maxUp:F2} m/s after falling");
    home();
    Object.Destroy(temp);
    p.ClearSurfaceCache();
    return true;
}));

scenarios.Add(("ice", f =>
{
    if (f == 0)
    {
        Physics.Raycast(rig.Head.position, Vector3.down, out RaycastHit g, 8f, mask);
        tempSurface = g.collider.gameObject.AddComponent<GorillaLocomotion.Surface>();
        tempSurface.slipPercentage = 0.99f;
        p.ClearSurfaceCache();
        gyL = groundLocalY(); flag = false; start = p.transform.position;
        lastAlloc = System.GC.GetAllocatedBytesForCurrentThread();
    }
    long now = System.GC.GetAllocatedBytesForCurrentThread();
    if (f > 0) { allocRun += now - lastAlloc; runFrames++; }
    lastAlloc = now;
    if (f < 64)
    {
        rig.LeftController.localPosition = groundStroke(f % 32, -0.25f, gyL);
        flag |= p.IsHandSliding(true);
        return false;
    }
    if (f == 64) { home(); Object.Destroy(tempSurface); p.ClearSurfaceCache(); }
    if (f < 120) return false;
    report("ice (Surface slip 0.99)", flag, $"hand slid={flag}, moved {(p.transform.position - start).magnitude:F2} m");
    return true;
}));

scenarios.Add(("turn", f =>
{
    // Lower one hand gently onto the ground (a fast slam would be a push-off), wait until it is planted and still,
    // then snap turn. The planted hand must not drag or fling the body.
    if (f == 0) { gyL = groundLocalY(); flag = false; }
    if (f < 40) { rig.LeftController.localPosition = lerp(homeL, new Vector3(-0.25f, gyL - 0.01f, 0.35f), f / 39f); return false; }
    if (System.Environment.GetEnvironmentVariable("LOCOTEST_TRACE") == "1" && f >= 30 && f < 90 && f % 3 == 0)
        Debug.Log($"[LocoTrace] turn f={f} touch={p.IsHandTouching(true)} y={p.transform.position.y:F3} vel={body.linearVelocity.ToString("F2")} hand={p.leftFollower.position.y:F3} ctrl={rig.LeftController.position.y:F3}");
    if (f >= 55 && f <= 70) flag |= p.IsHandTouching(true); // a still hand's touch flag flickers; any contact = planted
    if (f < 70) return false;
    if (f == 70)
    {
        start = p.transform.position; topSpeed = 0f;
        if (System.Environment.GetEnvironmentVariable("LOCOTEST_NOTURN") != "1") rig.Turn(45f);
    }
    topSpeed = Mathf.Max(topSpeed, body.linearVelocity.magnitude);
    if (f < 130) return false;
    float moved = (p.transform.position - start).magnitude;
    report("snap turn 45 degrees with a hand planted", flag && moved < 0.02f && topSpeed < 0.3f,
        $"hand planted={flag}, body moved {moved:F3} m, top speed {topSpeed:F2} m/s, still touching={p.IsHandTouching(true)}");
    home();
    if (System.Environment.GetEnvironmentVariable("LOCOTEST_NOTURN") != "1") rig.Turn(-45f);
    return true;
}));

scenarios.Add(("respawn", f =>
{
    if (f == 0) { rig.RespawnAtCheckpoint(); }
    if (f < 60) return false;
    Vector3 expected = GameJam.Core.CourseLayout.PlayerSpawn(GameJam.Core.GameServices.CurrentCheckpoint, GameJam.Core.GameServices.LocalPlayerId);
    Vector2 off = new Vector2(rig.Head.position.x - expected.x, rig.Head.position.z - expected.z);
    report("respawn (teleport is not undone by the anti-clip check)", off.magnitude < 0.3f, $"head lands {off.magnitude:F2} m from the spawn point");
    return true;
}));

scenarios.Add(("wasd", f =>
{
    if (f == 0) { desk.enabled = true; start = p.transform.position; InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState(Key.W)); }
    if (f < 90) return false;
    if (f == 90) InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState());
    if (f < 120) return false;
    float moved = new Vector2(p.transform.position.x - start.x, p.transform.position.z - start.z).magnitude;
    report("PC mode WASD (hold W for 90 frames)", moved > 0.5f, $"walked {moved:F2} m");
    return true;
}));

scenarios.Add(("slap", f =>
{
    var run = GameJam.Core.GameServices.Run;
    if (f == 0) { run?.RequestStart(); }
    if (run == null) { report("slap the balloon in a live run", false, "no run status"); return true; }
    if (run.State != GameJam.Core.RunState.Running) return f > 1500 && Fail("run never started");
    if (!flag)
    {
        flag = true;
        balloon.ResetTo(rig.RightController.position + rig.Head.forward * 0.35f);
        peak = f;
        return false;
    }
    if (f == (int)peak + 3) InputSystem.QueueStateEvent(Mouse.current, new MouseState().WithButton(MouseButton.Left));
    if (f == (int)peak + 5) InputSystem.QueueStateEvent(Mouse.current, new MouseState());
    if (f < (int)peak + 30) return false;
    bool hit = balloon.LastHitter != Fusion.PlayerRef.None;
    report("slap the balloon in a live run", hit, $"state={run.State}, lastHitter={balloon.LastHitter}");
    return true;
}));

bool Fail(string why) { Debug.Log($"[LocoTest] FAIL slap the balloon in a live run: {why}"); return true; }

string only = System.Environment.GetEnvironmentVariable("LOCOTEST_ONLY");
if (!string.IsNullOrEmpty(only)) scenarios.RemoveAll(sc => sc.name != only && sc.name != "idle");
int index = 0, frame = 0;
UnityEngine.Events.UnityAction tick = null;
tick = () =>
{
    if (index >= scenarios.Count) return;
    if (scenarios[index].name == "slap" && frame == 0) flag = false;
    bool done;
    try { done = scenarios[index].step(frame); }
    catch (System.Exception e) { Debug.Log($"[LocoTest] FAIL {scenarios[index].name}: exception {e.Message}"); done = true; }
    frame++;
    if (done)
    {
        index++;
        frame = 0;
        if (index >= scenarios.Count)
        {
            Application.onBeforeRender -= tick;
            float idleAvg = idleFrames > 0 ? (float)allocIdle / idleFrames : 0f;
            float runAvg = runFrames > 0 ? (float)allocRun / runFrames : 0f;
            Debug.Log($"[LocoTest] INFO garbage per frame on the main thread: idle {idleAvg:F0} B, while hands tap/slide {runAvg:F0} B (difference = locomotion cost)");
            Debug.Log("[LocoTest] DONE");
        }
    }
};
Application.onBeforeRender += tick;
return $"LocoTest started at frame {Time.frameCount}: {scenarios.Count} scenarios";
