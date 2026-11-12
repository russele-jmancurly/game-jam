// Solo play check (Play mode, one player, Prototype scene). Starts a run, then the local player hits the balloon
// eight times in a row: the pass rule must be off (no pop, no "not your turn"), and every hit after the first must
// count toward the rally (7 after 8 hits). Logs "[Solo]" lines, then "[Solo] DONE".
//   unity command script eval --project-path "E:\Project\game-jam" -- --file .claude/unity-snippets/solo_play_check.cs
using UnityEngine;

var balloon = GameJam.Net.NetworkBalloon.Instance;
var state = GameJam.Net.GameState.Instance;
if (balloon == null || state == null) return "not ready";
var body = balloon.GetComponent<Rigidbody>();
var netTransform = balloon.GetComponent<Fusion.NetworkTransform>();
var me = balloon.Runner.LocalPlayer;
System.Action<string, bool, string> report = (name, pass, detail) => Debug.Log($"[Solo] {(pass ? "PASS" : "FAIL")} {name}: {detail}");

int step = 0;
float at = 0f;
int hits = 0;
int popsBefore = 0;
bool sawNotMyTurn = false;
Vector3 station = Vector3.zero;
UnityEngine.Events.UnityAction tick = null;
tick = () =>
{
    float now = Time.time;
    switch (step)
    {
        case 0:
            state.RequestStart();
            step = 1;
            break;
        case 1:
            if (state.State == GameJam.Core.RunState.Running)
            {
                report("one player in the room", balloon.IsPassRuleSuspended, $"suspended={balloon.IsPassRuleSuspended}");
                station = balloon.transform.position;
                popsBefore = balloon.PopCount;
                hits = 0;
                at = now;
                step = 2;
            }
            break;
        case 2:
            if (now - at > 0.35f)
            {
                // Keep it up in the open so it can't land on anything between hits.
                Vector3 up = station + Vector3.up * 1.2f;
                netTransform.Teleport(up, Quaternion.identity);
                body.position = up;
                body.linearVelocity = Vector3.zero;
                balloon.RequestHit(me, Vector3.up * 0.9f);
                hits++;
                at = now;
                if (hits == 8)
                {
                    step = 3;
                }
            }
            else if (hits > 0 && balloon.IsNotLocalPlayersTurn)
            {
                sawNotMyTurn = true;
            }
            break;
        case 3:
            if (now - at > 0.2f)
            {
                report("same player hits 8 times without popping", balloon.PopCount == popsBefore, $"pops {popsBefore} -> {balloon.PopCount}");
                report("never 'not your turn'", !sawNotMyTurn && !balloon.IsNotLocalPlayersTurn, $"seen={sawNotMyTurn}");
                report("solo hits build the rally", balloon.Rally == 7 && balloon.BestRally >= 7, $"rally={balloon.Rally} best={balloon.BestRally}");
                var board = GameJam.Gameplay.McBoard.Instance;
                string boardText = board != null ? board.GetComponentInChildren<TextMesh>(true) != null ? string.Join(" | ", System.Array.ConvertAll(board.GetComponentsInChildren<TextMesh>(true), t => t.text.Replace("\n", " / "))) : "(no TextMesh)" : "(no board)";
                Debug.Log($"[Solo] INFO board: {boardText}");
                Application.onBeforeRender -= tick;
                Debug.Log("[Solo] DONE");
                step = 4;
            }
            break;
    }
};
Application.onBeforeRender += tick;
return "running: read [Solo] lines in ~4 s";
