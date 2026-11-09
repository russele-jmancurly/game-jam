// Finish basket check (Play mode, solo/offline). Starts a run, marks every hoop cleared, then throws the balloon at the
// basket three ways: in from the side under the rim and skimming over the rim (neither may finish), then dropping down
// through the ring (must finish). Writes Screenshots/finish_basket.png from the approach. Logs "[Finish]" lines, then DONE.
//   unity command script eval --project-path "E:\Project\game-jam" -- --file .claude/unity-snippets/finish_check.cs
using UnityEngine;

var balloon = GameJam.Net.NetworkBalloon.Instance;
var state = GameJam.Net.GameState.Instance;
var finish = Object.FindFirstObjectByType<GameJam.Gameplay.FinishLine>();
if (balloon == null || state == null || finish == null) return "not ready";
var body = balloon.GetComponent<Rigidbody>();
var netTransform = balloon.GetComponent<Fusion.NetworkTransform>();
System.Action<string, bool, string> report = (name, pass, detail) => Debug.Log($"[Finish] {(pass ? "PASS" : "FAIL")} {name}: {detail}");
var hoops = GameJam.Editor.BackyardCourseBuilder.TrailHoops;
Vector3 ring = finish.RingCenter;
Vector3 along = ring - hoops[hoops.Length - 1];
along.y = 0f;
along.Normalize();
Vector3 side = Vector3.Cross(Vector3.up, along);
var p1 = Fusion.PlayerRef.FromIndex(1);

// Each case: where the balloon starts and how it moves; whether it should finish.
var cases = new (string name, Vector3 offset, Vector3 velocity, bool shouldFinish)[]
{
    ("in from the side under the rim", side * 1.6f + Vector3.down * 0.15f, -side * 2.5f + Vector3.down * 0.1f, false),
    // Fast enough to sail past (drag stops a slow one over the opening, and then dropping in is a fair finish).
    ("skimming over the rim", side * 1.6f + Vector3.up * 0.4f, -side * 5f, false),
    ("in from the front under the rim", -along * 1.6f + Vector3.down * 0.15f, along * 2.5f, false),
    ("dropping down through the ring", Vector3.up * 0.8f, Vector3.down * 0.5f, true),
};

int step = 0, index = 0;
float at = 0f;
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
                for (int i = 0; i < hoops.Length; i++) state.ReportHoop(i);
                at = now;
                step = 2;
            }
            break;
        case 2:
            if (now - at > 0.3f)
            {
                report("all hoops cleared", state.HoopsCleared == state.HoopCount, $"{state.HoopsCleared}/{state.HoopCount}");
                // Screenshot of the basket from the approach (rim should be mint now).
                var cam = new GameObject("FinishCam") { hideFlags = HideFlags.DontSave }.AddComponent<Camera>();
                cam.targetDisplay = 7;
                cam.transform.position = ring - along * 6f + Vector3.down * 1.2f;
                cam.transform.LookAt(ring + Vector3.down * 0.4f);
                var rt = new RenderTexture(960, 540, 24);
                cam.targetTexture = rt;
                cam.Render();
                RenderTexture.active = rt;
                var tex = new Texture2D(960, 540, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, 960, 540), 0, 0);
                System.IO.Directory.CreateDirectory("Screenshots");
                System.IO.File.WriteAllBytes("Screenshots/finish_basket.png", tex.EncodeToPNG());
                RenderTexture.active = null;
                Object.Destroy(cam.gameObject);
                Object.Destroy(rt);
                Object.Destroy(tex);
                step = 3;
            }
            break;
        case 3:
            balloon.RequestHit(p1, Vector3.up * 0.2f); // off its station, so it flies freely
            at = now;
            step = 4;
            break;
        case 4:
            if (now - at > 0.15f)
            {
                var c = cases[index];
                Vector3 start = ring + c.offset;
                netTransform.Teleport(start, Quaternion.identity);
                body.position = start;
                body.linearVelocity = c.velocity;
                at = now;
                step = 5;
            }
            break;
        case 5:
            bool finished = state.State == GameJam.Core.RunState.Finished;
            if (finished || now - at > 2.5f)
            {
                var c = cases[index];
                report(c.name, finished == c.shouldFinish,
                    $"finished={finished} expected={c.shouldFinish} balloon={balloon.transform.position - ring} lastScore={state.LastScore}");
                index++;
                if (finished || index >= cases.Length)
                {
                    if (index < cases.Length) report("remaining cases skipped", false, "the run ended early");
                    Application.onBeforeRender -= tick;
                    Debug.Log("[Finish] DONE");
                    step = 9;
                }
                else
                {
                    step = 3;
                }
            }
            break;
    }
};
Application.onBeforeRender += tick;
return "running: read [Finish] lines in ~12 s";
