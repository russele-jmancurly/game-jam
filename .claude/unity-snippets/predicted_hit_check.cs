// Predicted-slap check (Play mode, offline). Online, a slapper who doesn't own the balloon now sees and hears their hit at
// once (NetworkBalloon.PredictHit) and the visuals blend onto the owner's real hit when it arrives. Offline you own the
// balloon, so this fakes the network: it predicts a hit, then lets the "owner" apply the same hit LATENCY seconds later.
// Checks: the visuals move on the first frame; they never jump during the hand-over; they end exactly on the body.
// Logs "[PredictTest]" lines, then DONE. Env PREDICT_LATENCY (seconds, default 0.15).
//   unity command script eval --project-path "D:\Unity\game-jam" -- --file .claude/unity-snippets/predicted_hit_check.cs
using System.Reflection;
using UnityEngine;

var balloon = GameJam.Net.NetworkBalloon.Instance;
var state = GameJam.Net.GameState.Instance;
if (balloon == null || state == null) return "not ready";
float latency = float.TryParse(System.Environment.GetEnvironmentVariable("PREDICT_LATENCY"), out float l) ? l : 0.15f;
System.Action<string, bool, string> report = (name, pass, detail) => Debug.Log($"[PredictTest] {(pass ? "PASS" : "FAIL")} {name}: {detail}");
MethodInfo predict = typeof(GameJam.Net.NetworkBalloon).GetMethod("PredictHit", BindingFlags.NonPublic | BindingFlags.Instance);
Transform view = balloon.transform.Find("View");
Vector3 hit = new Vector3(1.5f, 4f, 2f); // a firm upward slap
int frame = 0;
float predictedAt = 0f, maxJump = 0f, firstMove = 0f;
bool applied = false;
Vector3 lastView = default;
UnityEngine.Events.UnityAction tick = null;
tick = () =>
{
    frame++;
    if (frame == 1) { state.RequestStart(); return; }
    if (predictedAt == 0f)
    {
        if (state.State != GameJam.Core.RunState.Running) return;
        if (frame % 30 != 0) return; // let the run settle a moment
        lastView = view.position;
        predict.Invoke(balloon, new object[] { hit });
        predictedAt = Time.time;
        return;
    }

    Vector3 now = view.position;
    float step = (now - lastView).magnitude;
    if (firstMove == 0f) firstMove = step;
    // Expected motion per frame is ~speed * dt; anything well beyond that is a visible jump.
    maxJump = Mathf.Max(maxJump, step - balloon.Velocity.magnitude * Time.deltaTime - hit.magnitude * Time.deltaTime);
    lastView = now;

    if (!applied && Time.time - predictedAt >= latency)
    {
        applied = true;
        balloon.RequestHit(balloon.Runner.LocalPlayer, hit); // the owner's real hit, LATENCY late
    }

    if (applied && Time.time - predictedAt > latency + 0.6f)
    {
        float residual = view.localPosition.magnitude;
        report("moves on the first frame", firstMove > 0.005f, $"first-frame move {firstMove * 100f:F1} cm");
        report("no jump during the hand-over", maxJump < 0.06f, $"largest extra per-frame move {maxJump * 100f:F1} cm (latency {latency * 1000f:F0} ms)");
        report("ends on the real balloon", residual < 0.005f && !balloon.IsShowingPrediction, $"visual offset {residual * 100f:F2} cm");
        Application.onBeforeRender -= tick;
        Debug.Log("[PredictTest] DONE");
    }
};
Application.onBeforeRender += tick;
return $"PredictTest started (simulated latency {latency * 1000f:F0} ms)";
