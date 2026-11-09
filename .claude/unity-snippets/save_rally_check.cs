// Save reward + rally drama check (Play mode, solo/offline). Starts a run, then:
//  1. a hit high up is not a save; a hit on a balloon falling just above the stage is (BalloonCallouts.LastSaveTime);
//  2. ten alternating hits from stand-in players 1 and 2 build a rally: milestones at 5 and 10 (LastMilestoneTime);
//  3. a hard downward hit bounces it off the stage: the rally breaks and the sad trombone plays (LastBreakTime),
//     unless it popped (then no trombone, by design).
// Places DontSave camera "SaveCam" looking at the balloon. Logs "[SaveRally]" lines, then "[SaveRally] DONE".
//   unity command script eval --project-path "E:\Project\game-jam" -- --file .claude/unity-snippets/save_rally_check.cs
using UnityEngine;

var balloon = GameJam.Net.NetworkBalloon.Instance;
var state = GameJam.Net.GameState.Instance;
if (balloon == null || state == null) return "not ready";
var callouts = balloon.GetComponent<GameJam.Gameplay.BalloonCallouts>();
var ground = balloon.GetComponent<GameJam.Gameplay.BalloonGroundShadow>();
if (callouts == null || ground == null) return "balloon has no BalloonCallouts/BalloonGroundShadow: rebuild the prototype";
var body = balloon.GetComponent<Rigidbody>();
var netTransform = balloon.GetComponent<Fusion.NetworkTransform>();
System.Action<string, bool, string> report = (name, pass, detail) => Debug.Log($"[SaveRally] {(pass ? "PASS" : "FAIL")} {name}: {detail}");
var p1 = Fusion.PlayerRef.FromIndex(1);
var p2 = Fusion.PlayerRef.FromIndex(2);

int step = 0;
float at = 0f;
int hits = 0;
float saveBefore = 0f, milestoneBefore = 0f, breakBefore = 0f;
int popsBefore = 0;
Vector3 station = Vector3.zero;
Camera cam = null;
bool savedShot = false;
System.Action<string> shoot = file =>
{
    if (cam == null) return;
    var rt = new RenderTexture(960, 540, 24);
    cam.targetTexture = rt;
    cam.Render();
    RenderTexture.active = rt;
    var tex = new Texture2D(960, 540, TextureFormat.RGB24, false);
    tex.ReadPixels(new Rect(0, 0, 960, 540), 0, 0);
    tex.Apply();
    System.IO.Directory.CreateDirectory("Screenshots");
    System.IO.File.WriteAllBytes("Screenshots/" + file, tex.EncodeToPNG());
    RenderTexture.active = null;
    cam.targetTexture = null;
    Object.Destroy(rt);
    Object.Destroy(tex);
};
UnityEngine.Events.UnityAction tick = null;
tick = () =>
{
    float now = Time.time;
    if (cam != null)
    {
        // From the player's side: the callouts turn to face the head camera, as every player sees them.
        Vector3 head = Camera.main != null ? Camera.main.transform.position : balloon.transform.position + Vector3.back * 2.6f;
        cam.transform.position = head + (balloon.transform.position - head) * 0.4f; // the player's view, a bit closer
        cam.transform.LookAt(balloon.transform.position + Vector3.up * 0.35f);
    }

    switch (step)
    {
        case 0:
            state.RequestStart();
            step = 1;
            break;
        case 1:
            if (state.State == GameJam.Core.RunState.Running)
            {
                station = balloon.transform.position;
                cam = new GameObject("SaveCam") { hideFlags = HideFlags.DontSave }.AddComponent<Camera>();
                cam.targetDisplay = 7;
                saveBefore = callouts.LastSaveTime;
                balloon.RequestHit(p1, Vector3.up * 0.5f); // high up, off station: not a save
                at = now;
                step = 2;
            }
            break;
        case 2:
            if (now - at > 0.3f)
            {
                report("high hit is not a save", callouts.LastSaveTime == saveBefore,
                    $"height={balloon.transform.position.y - ground.GroundY:F2} m");
                // Put it just above the surface below, falling.
                Vector3 low = new Vector3(station.x, ground.GroundY + 0.45f, station.z);
                netTransform.Teleport(low, Quaternion.identity);
                body.position = low;
                body.linearVelocity = Vector3.down * 0.7f;
                at = now;
                step = 3;
            }
            break;
        case 3:
            if (now - at > 0.05f)
            {
                float height = balloon.transform.position.y - ground.GroundY;
                float vy = body.linearVelocity.y;
                body.linearVelocity = Vector3.down * 0.7f; // keep it falling for the check
                balloon.RequestHit(p2, Vector3.up * 2.5f);
                Debug.Log($"[SaveRally] INFO save hit sent at height={height:F2} m vy={vy:F2}");
                at = now;
                step = 4;
            }
            break;
        case 4:
            if (!savedShot && now - at > 0.35f)
            {
                savedShot = true;
                shoot("save_popup.png");
            }

            if (now - at > 0.8f)
            {
                report("low falling hit is a save", callouts.LastSaveTime > saveBefore, $"saveAt={callouts.LastSaveTime:F2}");
                // Rally: lift it up in the open and alternate hitters.
                Vector3 up = station + Vector3.up * 1.2f;
                netTransform.Teleport(up, Quaternion.identity);
                body.position = up;
                body.linearVelocity = Vector3.zero;
                milestoneBefore = callouts.LastMilestoneTime;
                hits = 0;
                at = now;
                step = 5;
            }
            break;
        case 5:
            if (now - at > 0.35f)
            {
                balloon.RequestHit(hits % 2 == 0 ? p1 : p2, Vector3.up * 0.9f);
                hits++;
                at = now;
                if (hits == 11)
                {
                    step = 6;
                }
            }
            break;
        case 6:
            if (now - at > 0.1f)
            {
                report("rally builds to 10", balloon.Rally >= 10, $"rally={balloon.Rally} best={balloon.BestRally}");
                report("milestones fired", callouts.LastMilestoneTime > milestoneBefore,
                    $"lastMilestone={callouts.LastMilestoneTime:F2}");
                shoot("rally.png");
                var rt = balloon.transform.Find("View/RallyText");
                var rtm = rt.GetComponent<TextMesh>();
                Debug.Log($"[SaveRally] INFO rally text=[{rtm.text}] color={rtm.color} scale={rt.localScale} offset={rt.position - balloon.transform.position} rendererOn={rt.GetComponent<MeshRenderer>().enabled}");
                Debug.Log("[SaveRally] INFO wrote Screenshots/save_popup.png and Screenshots/rally.png");
                at = now;
                step = 7;
            }
            break;
        case 7:
            if (now - at > 1.5f)
            {
                breakBefore = callouts.LastBreakTime;
                popsBefore = balloon.PopCount;
                Vector3 near = new Vector3(balloon.transform.position.x, ground.GroundY + 0.7f, balloon.transform.position.z);
                netTransform.Teleport(near, Quaternion.identity);
                body.position = near;
                body.linearVelocity = Vector3.down * 3f; // into the stage: a world bounce ends the rally
                at = now;
                step = 8;
            }
            break;
        case 8:
            if (now - at > 1.2f)
            {
                bool popped = balloon.PopCount != popsBefore;
                report("rally broken", balloon.Rally == 0, $"rally={balloon.Rally} popped={popped}");
                report(popped ? "popped: trombone stays quiet" : "trombone on a broken rally",
                    popped ? callouts.LastBreakTime == breakBefore : callouts.LastBreakTime > breakBefore,
                    $"breakAt={callouts.LastBreakTime:F2}");
                if (cam != null) Object.Destroy(cam.gameObject);
                Application.onBeforeRender -= tick;
                Debug.Log("[SaveRally] DONE");
                step = 9;
            }
            break;
    }
};
Application.onBeforeRender += tick;
return "running: read [SaveRally] lines in ~8 s";
