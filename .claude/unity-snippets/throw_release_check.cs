// Play mode (PC test mode, Prototype). Throws the beach ball with a realistic swing: the right hand speeds up to 5 m/s,
// then slows and curves downward for the last 50 ms before the grip opens (people let go just after the swing's peak).
// Logs "[ThrowTest] ..." lines at 72 fps and uncapped: the ball must leave at about peak speed x ThrowBoost, along the
// swing, at both frame rates. "old" is what the release-frame velocity alone (the previous rule) would have thrown.
// Read the console ~8 s later (look for "[ThrowTest] DONE").
var rig = GameJam.Player.LocalRig.Instance;
if (rig == null) return "no LocalRig (enter Play mode in Prototype first)";
var desktop = rig.GetComponent<GameJam.Player.DesktopRigInput>();
void Log(string text) => UnityEngine.Debug.Log("[ThrowTest] " + text);
GameJam.Net.NetworkItem ball = null;
foreach (var it in UnityEngine.Object.FindObjectsByType<GameJam.Net.NetworkItem>(UnityEngine.FindObjectsSortMode.None))
    if (it.Definition.name.Contains("BeachBall") && it.HasStateAuthority && !it.IsHeld && !it.IsSpent) ball = it;
if (ball == null) return "no beach ball";
System.Collections.IEnumerator Run()
{
    const float peak = 5f, rampSeconds = 0.15f, slowSeconds = 0.05f, endSpeed = 1.2f;
    var body = ball.GetComponent<UnityEngine.Rigidbody>();
    UnityEngine.Vector3 home = ball.transform.position;
    int oldVSync = UnityEngine.QualitySettings.vSyncCount;
    desktop.enabled = false;
    var slapHand = rig.RightHandFollower.GetComponent<GameJam.Player.SlapHand>();
    foreach (int fps in new[] { 72, -1 })
    {
        UnityEngine.QualitySettings.vSyncCount = 0;
        UnityEngine.Application.targetFrameRate = fps;
        rig.RespawnAtCheckpoint();
        yield return new UnityEngine.WaitForSeconds(1.2f);
        UnityEngine.Transform hand = rig.RightController;
        UnityEngine.Vector3 forward = rig.Head.forward; forward.y = 0f; forward.Normalize();
        UnityEngine.Vector3 start = rig.Head.position + rig.Head.right * 0.25f - forward * 0.15f - UnityEngine.Vector3.up * 0.1f;
        hand.position = start;
        yield return new UnityEngine.WaitForSeconds(0.4f);
        body.position = rig.RightHandFollower.position; ball.transform.position = body.position;
        UnityEngine.Physics.SyncTransforms();
        rig.SetGrip(true, true);
        yield return new UnityEngine.WaitForSeconds(0.3f);
        if (!ball.IsHeldBy(rig.RightHandFollower)) { Log("FAIL could not grab the ball"); continue; }

        float t = 0f;
        while (t < rampSeconds + slowSeconds)
        {
            float dt = UnityEngine.Time.deltaTime;
            t += dt;
            UnityEngine.Vector3 velocity;
            if (t <= rampSeconds) { velocity = forward * (peak * t / rampSeconds); }
            else
            {
                float k = UnityEngine.Mathf.Clamp01((t - rampSeconds) / slowSeconds);
                // follow-through: slowing down and curving toward the ground
                velocity = UnityEngine.Vector3.Slerp(forward, (forward * 0.4f + UnityEngine.Vector3.down).normalized, k) * UnityEngine.Mathf.Lerp(peak, endSpeed, k);
            }
            hand.position += velocity * dt;
            yield return null;
        }
        UnityEngine.Vector3 old = slapHand.Velocity * rig.Config.ThrowBoost;
        rig.SetGrip(true, false);
        yield return new UnityEngine.WaitForFixedUpdate();
        UnityEngine.Vector3 thrown = body.linearVelocity;
        float want = peak * rig.Config.ThrowBoost;
        float angle = UnityEngine.Vector3.Angle(thrown, forward);
        bool ok = thrown.magnitude > want * 0.85f && thrown.magnitude < want * 1.15f && angle < 10f;
        Log((ok ? "PASS " : "FAIL ") + (fps > 0 ? fps + " fps" : "uncapped (" + (1f / UnityEngine.Time.smoothDeltaTime).ToString("F0") + " fps)")
            + ": thrown at " + thrown.magnitude.ToString("F2") + " m/s, " + angle.ToString("F1") + " deg off the swing (want about " + want.ToString("F1")
            + " m/s); old rule: " + old.magnitude.ToString("F2") + " m/s, " + UnityEngine.Vector3.Angle(old, forward).ToString("F1") + " deg off");
        yield return new UnityEngine.WaitForSeconds(0.5f);
        body.linearVelocity = UnityEngine.Vector3.zero; body.position = home; ball.transform.position = home;
    }
    UnityEngine.Application.targetFrameRate = -1;
    UnityEngine.QualitySettings.vSyncCount = oldVSync;
    desktop.enabled = true;
    Log("DONE");
}
rig.StartCoroutine(Run());
return "started";
