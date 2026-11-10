// Slow-push check (Play mode, offline, PC test mode). A slow touch must never pop the balloon.
// Starts a run, turns the solo pass-rule exemption off (as with 2+ players), then:
//   A) quills: holds the balloon against the local player's own head hitbox, pressing in slowly (0.25 m/s) and holding
//      for 2 s, twice (the player leaning into a balloon). Each touch may leak once (a graze), never pop.
//   B) hands: two stand-in SlapHands push slowly (0.8 m/s, just over the slap minimum) back and forth inside a balloon
//      that is held still, like the balloon's real body on a client that doesn't own it (it moves only a round trip
//      later). The first touch is a hit; the hands staying inside must not hit again (that was a "Double hit!" pop).
//   C) nudge: the balloon drifts toward the player at 0.7 m/s and a still stand-in hand (below the slap minimum) waits
//      in its way. The balloon must drift off the hand (a nudge) instead of passing through to the quills: no leak, no pop.
//   D) no free lift: a still hand held under a falling balloon must not keep it up (nudges never push up, use no turn).
//   E) still pops: the balloon flying into the player's quills faster than BodyPopSpeed pops ("Right into the spikes!").
//   F) still pops: the same player hitting twice pops it ("Double hit!") once RehitSeconds have passed; a repeat inside
//      RehitSeconds (both hands, one touch) is ignored.
// Logs "[SlowPush]" lines with the pop headline, then DONE. Restores AllowSoloDoubleHits.
//   unity command script eval --project-path "E:\Project\game-jam" -- --file .claude/unity-snippets/slow_push_check.cs
using System.Reflection;
using UnityEngine;

var balloon = GameJam.Net.NetworkBalloon.Instance;
var state = GameJam.Net.GameState.Instance;
if (balloon == null || state == null) return "not ready";
GameJam.Net.AvatarHitbox head = null;
foreach (var box in Object.FindObjectsByType<GameJam.Net.AvatarHitbox>(FindObjectsSortMode.None))
{
    if (box.Avatar != null && box.Avatar.IsLocal && box.GetComponent<SphereCollider>() != null) head = box;
}
if (head == null) return "no local head hitbox";

System.Action<string> log = s => Debug.Log("[SlowPush] " + s);
PropertyInfo headlineProp = typeof(GameJam.Net.GameState).GetProperty("Headline", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
System.Func<string> headline = () => headlineProp != null ? headlineProp.GetValue(state)?.ToString() : "?";
var config = balloon.Config;
bool soloWas = config.AllowSoloDoubleHits;
config.AllowSoloDoubleHits = false;
var body = balloon.GetComponent<Rigidbody>();
var slapConfig = (GameJam.Core.SlapConfig)typeof(GameJam.Net.NetworkBalloon).GetField("_slapConfig", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(balloon);

var rigHands = Object.FindObjectsByType<GameJam.Player.SlapHand>(FindObjectsSortMode.None); // the rig's own two hands
int phase = 0;       // 0 start run, 1 wait running, 2 quill test, 3 hand test, 4 done
float phaseStart = 0f;
string lastPop = "";
int frameF = 0, popsAfterRepeat = 0;
float fAt = 0f;
float handY = 0f, lowest = 99f;
int popsAtStart = 0, hitsAtStart = 0, quillPops = 0, quillLeaks = 0, handPops = 0, nudgePops = 0, nudgeLeaks = 0;
Vector3 nudgeStart = default, nudgeDir = default;
var lastCondition = balloon.Condition;
int lastPops = balloon.PopCount;
GameObject[] hands = null;
Vector3 holdPoint = default;

System.Action<Vector3> hold = p => { body.position = p; balloon.transform.position = p; body.linearVelocity = Vector3.zero; };
System.Action unstation = () => balloon.RequestHit(Fusion.PlayerRef.None, Vector3.up * 0.05f); // live, nobody's hit

UnityEngine.Events.UnityAction tick = null;
tick = () =>
{
    float t = Time.time - phaseStart;
    if (balloon.PopCount != lastPops)
    {
        lastPops = balloon.PopCount;
        if (phase == 2) quillPops++;
        if (phase == 3) handPops++;
        if (phase == 5) nudgePops++;
        lastPop = headline();
        log($"POP in phase {phase} at {t:F2}s: \"{lastPop}\"");
    }
    if (balloon.Condition != lastCondition)
    {
        if (phase == 2 && balloon.Condition > lastCondition) quillLeaks++;
        if (phase == 5 && balloon.Condition > lastCondition) nudgeLeaks++;
        log($"condition {lastCondition} -> {balloon.Condition} (phase {phase}, {t:F2}s)");
        lastCondition = balloon.Condition;
    }

    switch (phase)
    {
        case 0:
            state.RequestStart();
            phase = 1; phaseStart = Time.time;
            break;
        case 1:
            if (state.State == GameJam.Core.RunState.Running && balloon.Runner.IsRunning && t > 0.5f)
            {
                unstation();
                foreach (var h in rigHands) h.enabled = false; // A tests the quills alone: the rig's hands would nudge it
                phase = 2; phaseStart = Time.time; popsAtStart = balloon.PopCount;
            }
            break;
        case 2:
        {
            // 3 cycles of 4.1 s: approach 0.7 -> 0.3 m at 0.25 m/s (1.6 s), hold 2 s, back off 0.5 s.
            if (t < 0.4f) { break; } // past the hit grace
            float c = (t - 0.4f) % 4.1f;
            int cycle = (int)((t - 0.4f) / 4.1f);
            if (cycle >= 2)
            {
                foreach (var h in rigHands) h.enabled = true;
                log($"RESULT A quills: pops {quillPops}, leaks {quillLeaks} over 2 slow presses (want 0 pops, at most 1 leak per press)");
                // B: two stand-in hands
                var handA = new GameObject("SlowPushHandA") { hideFlags = HideFlags.DontSave };
                var handB = new GameObject("SlowPushHandB") { hideFlags = HideFlags.DontSave };
                hands = new[] { handA, handB };
                holdPoint = head.transform.position + head.transform.forward * 2.5f + Vector3.up * 0.2f;
                foreach (var h in hands)
                {
                    h.transform.position = holdPoint + Vector3.right * 0.6f;
                    h.AddComponent<GameJam.Player.SlapHand>().Configure(h == handA ? UnityEngine.XR.XRNode.LeftHand : UnityEngine.XR.XRNode.RightHand, slapConfig);
                }
                hitsAtStart = balloon.Hits;
                phase = 3; phaseStart = Time.time;
                break;
            }
            float d = c < 1.6f ? Mathf.Lerp(0.7f, 0.3f, c / 1.6f) : (c < 3.6f ? 0.3f : 1f);
            Vector3 dir = head.transform.forward;
            dir.y = 0f;
            dir = dir.sqrMagnitude > 0.01f ? dir.normalized : Vector3.forward;
            hold(head.transform.position + dir * d);
            break;
        }
        case 3:
        {
            // Hands sweep slowly (0.8 m/s) through the held balloon, side to side, for 3 s.
            hold(holdPoint);
            float swing = Mathf.PingPong(t * 0.8f, 0.6f) - 0.3f; // -0.3..0.3 m, 0.8 m/s
            hands[0].transform.position = holdPoint + new Vector3(swing, 0.05f, 0f);
            hands[1].transform.position = holdPoint + new Vector3(-swing, -0.05f, 0f);
            if (t > 3f)
            {
                int hits = balloon.Hits - hitsAtStart;
                log($"RESULT B hands: hits {hits}, pops {handPops} (want 1 hit, 0 pops)");
                Object.Destroy(hands[1]);
                // C: a full balloon again, for the nudge test.
                balloon.ResetTo(head.transform.position + Vector3.up * 3f);
                phase = 4; phaseStart = Time.time;
            }
            break;
        }
        case 4:
            if (t > 0.3f)
            {
                // The balloon drifts toward the player at 0.7 m/s; their hand waits still in its way, 0.45 m out.
                nudgeDir = head.transform.forward; nudgeDir.y = 0f; nudgeDir = nudgeDir.sqrMagnitude > 0.01f ? nudgeDir.normalized : Vector3.forward;
                nudgeStart = head.transform.position + nudgeDir * 0.45f;
                hold(head.transform.position + nudgeDir * 1.3f + Vector3.up * 0.9f); // PC-mode head is low (0.65 m off the floor)
                balloon.RequestHit(Fusion.PlayerRef.None, -nudgeDir * 0.7f); // plus the usual upward assist
                hands[0].transform.position = nudgeStart;
                lastCondition = balloon.Condition;
                phase = 5; phaseStart = Time.time;
            }
            break;
        case 5:
        {
            hands[0].transform.position = new Vector3(nudgeStart.x, body.position.y, nudgeStart.z); // a still hand at its height
            if (t > 3.8f)
            {
                float headGap = Vector3.Distance(body.position, head.transform.position);
                log($"RESULT C nudge: pops {nudgePops}, leaks {nudgeLeaks}, balloon {headGap:F2} m from the head (want 0, 0)");
                // D: a falling balloon 1 m above a still hand.
                Vector3 spot = head.transform.position + nudgeDir * 2.5f + Vector3.up * 2.0f;
                hold(spot);
                balloon.RequestHit(Fusion.PlayerRef.None, Vector3.down * 0.6f); // cancels the upward assist: it just falls
                handY = spot.y - 1f;
                hands[0].transform.position = new Vector3(spot.x, handY, spot.z);
                hitsAtStart = balloon.Hits;
                lowest = 99f;
                popsAtStart = balloon.PopCount;
                phase = 6; phaseStart = Time.time;
            }
            break;
        }
        case 6:
            if (balloon.PopCount == popsAtStart) lowest = Mathf.Min(lowest, body.position.y);
            if (t > 6f)
            {
                bool fellPast = lowest < handY - 0.3f || balloon.PopCount != popsAtStart;
                log($"RESULT D no free lift: fell past the hand {fellPast} (lowest {lowest:F2}, hand {handY:F2}, popped {balloon.PopCount != popsAtStart}), hits {balloon.Hits - hitsAtStart} (want true, 0)");
                Object.Destroy(hands[0]);
                // E: fly it into the head's quills at 4.5 m/s from 1.8 m (past the 0.25 s hit grace when it gets there).
                foreach (var h in rigHands) h.enabled = false; // a hand in the way would block it (a nudge)
                nudgeStart = head.transform.position + nudgeDir * 1.8f;
                hold(nudgeStart);
                balloon.RequestHit(Fusion.PlayerRef.None, -nudgeDir * 4.5f - Vector3.up * 0.6f);
                popsAtStart = balloon.PopCount;
                lastPop = "";
                phase = 7; phaseStart = Time.time;
            }
            break;
        case 7:
            if (balloon.PopCount != popsAtStart || t > 2f)
            {
                foreach (var h in rigHands) h.enabled = true;
                log($"RESULT E fast into the quills: popped {balloon.PopCount != popsAtStart} \"{lastPop}\" (want Right into the spikes!)");
                popsAtStart = balloon.PopCount;
                lastPop = "";
                phase = 8; phaseStart = Time.time;
            }
            break;
        case 8: // F: hits by the same player at 0, 0.1 s (same touch: ignored) and 0.6 s (a second hit: pops)
        {
            var me = balloon.Runner.LocalPlayer;
            if (t > 0.5f && hitsAtStart >= 0 && phaseStart > 0f && frameF == 0) { hold(head.transform.position + nudgeDir * 2.5f + Vector3.up * 1.5f); balloon.RequestHit(me, Vector3.up); frameF = 1; fAt = Time.time; }
            if (frameF == 1 && Time.time - fAt > 0.1f) { balloon.RequestHit(me, Vector3.up); frameF = 2; popsAfterRepeat = balloon.PopCount - popsAtStart; }
            if (frameF == 2 && Time.time - fAt > 0.6f) { balloon.RequestHit(me, Vector3.up); frameF = 3; }
            if (frameF == 3 && Time.time - fAt > 1f)
            {
                log($"RESULT F double hit: repeat inside RehitSeconds popped {popsAfterRepeat > 0}, second hit after it popped {balloon.PopCount - popsAtStart > 0} \"{lastPop}\" (want false, true Double hit!)");
                config.AllowSoloDoubleHits = soloWas;
                Application.onBeforeRender -= tick;
                phase = 9;
                log($"DONE (AllowSoloDoubleHits restored to {config.AllowSoloDoubleHits})");
            }

            break;
        }
    }
};
Application.onBeforeRender += tick;
return "SlowPush started (~27 s)";
