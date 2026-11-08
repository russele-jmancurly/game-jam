// Left-stick walking test (Play mode, offline, PC mode; no headset needed). Feeds LocalRig.StickWalk a full-forward
// stick every frame, as the headset's left thumbstick would:
//   1. open lawn, hands in the air: walks about StickMoveSpeed m/s, no hop
//   2. one hand planted on the ground: still walks (the planted hand doesn't hold you back), no launch
//   3. into a wall: stops in front of it, head never inside it
//   unity command script eval --project-path "D:\Unity\game-jam" -- --file .claude/unity-snippets/stick_walk_test.cs
// Logs "[WalkTest]" lines, then "[WalkTest] DONE".
using UnityEngine;

var rig = GameJam.Player.LocalRig.Instance;
var p = Object.FindFirstObjectByType<GorillaLocomotion.Player>();
var desk = rig.GetComponent<GameJam.Player.DesktopRigInput>();
desk.enabled = false;
Transform ts = rig.TrackingSpace;
int mask = GameJam.Core.GameLayers.DefaultMask;
float speed = rig.Config.StickMoveSpeed;
rig.Head.localPosition = new Vector3(0f, 1.2f, 0f);
rig.Head.localRotation = Quaternion.identity;
Vector3 homeL = new Vector3(-0.25f, 0.85f, 0.3f), homeR = new Vector3(0.25f, 0.85f, 0.3f);
Vector3 lawn = GameJam.Core.CourseLayout.PlayerSpawn(1, 0);
float startYaw = ts.eulerAngles.y;
rig.Turn(-startYaw);

int stage = 0, frame = 0;
Vector3 start = default;
float time0 = 0f, maxUp = 0f, minGap = float.MaxValue, gyFixed = 0f;
int plantedFrames = 0;
GameObject wall = null;
System.Action<string, bool, string> report = (name, pass, detail) => Debug.Log($"[WalkTest] {(pass ? "PASS" : "FAIL")} {name}: {detail}");
System.Func<float> groundLocalY = () =>
{
    Physics.Raycast(rig.Head.position, Vector3.down, out RaycastHit hit, 8f, mask, QueryTriggerInteraction.Ignore);
    return ts.InverseTransformPoint(hit.point).y;
};

UnityEngine.Events.UnityAction tick = null;
tick = () =>
{
    int f = frame++;
    if (f == 0)
    {
        rig.RespawnAt(lawn);
        rig.LeftController.localPosition = homeL;
        rig.RightController.localPosition = homeR;
        maxUp = 0f;
        minGap = float.MaxValue;
        if (stage == 2)
        {
            wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = "WalkTest_Wall";
            wall.transform.localScale = new Vector3(4f, 3f, 0.3f);
            wall.transform.position = lawn + new Vector3(0f, 1.5f, 1.5f);
        }
        return;
    }

    if (stage == 1 && f % 6 == 0 && f >= 96 && f <= 300 && System.Environment.GetEnvironmentVariable("WALKTEST_TRACE") == "2")
        Debug.Log($"[WalkTrace] f={f} root.y={p.transform.position.y:F3} vel.y={rig.Body.linearVelocity.y:F2} touchL={p.IsHandTouching(true)} ctrl.y={rig.LeftController.position.y:F3} glove.y={p.leftFollower.position.y:F3} ctrlLocal.y={rig.LeftController.localPosition.y:F3} gy={gyFixed:F3}");
    if (stage == 1 && f >= 150)
    {
        // Once the body has settled, lower the left hand gently onto the ground (a slam would be a push-off), then rest it
        // there, fixed relative to the body like a real resting hand, and keep it there while walking.
        if (f == 150) gyFixed = groundLocalY();
        Vector3 hover = new Vector3(-0.25f, gyFixed + 0.06f, 0.35f);
        rig.LeftController.localPosition = f < 190 ? Vector3.Lerp(homeL, hover, (f - 150) / 39f)
            : hover + Vector3.down * Mathf.Min(0.07f, (f - 190) * 0.001f); // the last 7 cm at ~1 mm per frame: a rest, not a push
    }

    if (f < (stage == 1 ? 300 : 60)) return; // land (and plant the hand)
    if (stage == 1) f -= 240;
    if (f == 60) { start = p.transform.position; time0 = Time.time; maxUp = 0f; plantedFrames = 0; }

    rig.StickWalk(new Vector2(0f, 1f));
    if (stage == 1 && System.Environment.GetEnvironmentVariable("WALKTEST_TRACE") == "1" && f < 110)
        Debug.Log($"[WalkTrace] f={f} frame={Time.frameCount} touchL={p.IsHandTouching(true)} vel={rig.Body.linearVelocity.ToString("F2")} root={p.transform.position.ToString("F3")} glove={p.leftFollower.position.ToString("F3")} ctrl={rig.LeftController.position.ToString("F3")}");
    maxUp = Mathf.Max(maxUp, rig.Body.linearVelocity.y);
    if (stage == 1 && p.IsHandTouching(true)) plantedFrames++;
    if (wall != null) minGap = Mathf.Min(minGap, wall.transform.position.z - 0.15f - rig.Head.position.z);
    if (f < 60 + 120) return;

    Vector3 moved = p.transform.position - start;
    float forward = moved.z;
    float expected = speed * (Time.time - time0);
    if (stage == 0)
        report("open lawn, hands in the air", forward > expected * 0.8f && Mathf.Abs(moved.x) < 0.1f && maxUp < 1f,
            $"walked {forward:F2} m in {Time.time - time0:F2} s (expected ~{expected:F2}), sideways {moved.x:F2}, top upward speed {maxUp:F2} m/s");
    else if (stage == 1)
        report("one hand planted on the ground", forward > expected * 0.8f && maxUp < 1f && plantedFrames > 0,
            $"walked {forward:F2} m (expected ~{expected:F2}), hand planted {plantedFrames}/121 walking frames, top upward speed {maxUp:F2} m/s");
    else
    {
        report("into a wall 1.5 m ahead", minGap > 0f && forward < 1.5f,
            $"walked {forward:F2} m, head closest {minGap:F2} m from the wall face (inside = negative)");
        Object.Destroy(wall);
    }

    stage++;
    frame = 0;
    if (stage > 2)
    {
        Application.onBeforeRender -= tick;
        rig.LeftController.localPosition = homeL;
        rig.RightController.localPosition = homeR;
        rig.Turn(startYaw);
        rig.RespawnAtCheckpoint();
        desk.enabled = true;
        Debug.Log("[WalkTest] DONE");
    }
};
Application.onBeforeRender += tick;
return "WalkTest started";
