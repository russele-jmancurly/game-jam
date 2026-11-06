// Play mode. Makes the roaming giant CHARACTER (Jmancurly / Squoof), puts it on open lawn with the balloon at BALLOON_HEIGHT
// in front of it, and installs a per-frame recorder (EditorApplication.update): mood, gesture, Hand.R and balloon positions,
// foot positions, and side-view PNGs at mid wind-up, at the hit and just after. Read the result with giant_slap_report.cs.
// Run once to reroll (if needed), wait ~0.5 s, run again to place and record.
var m = GameJam.Net.MischiefMaker.Instance;
if (m.DisplayName != "CHARACTER") { m.Reroll(); return "rerolled from " + m.DisplayName + "; run again"; }

var domain = System.AppDomain.CurrentDomain;
if (domain.GetData("giantRecorder") is UnityEditor.EditorApplication.CallbackFunction old) UnityEditor.EditorApplication.update -= old;

var giantPos = new UnityEngine.Vector3(12.5f, 0f, 12f);
var nt = m.GetComponent<Fusion.NetworkTransform>();
nt.Teleport(giantPos, UnityEngine.Quaternion.LookRotation(UnityEngine.Vector3.left));
m.transform.position = giantPos;
GameJam.Net.NetworkBalloon.Instance.ResetTo(new UnityEngine.Vector3(10.2f, BALLOON_HEIGHT, 12f));

var log = new System.Collections.Generic.List<string>();
var shots = new System.Collections.Generic.List<string>();
domain.SetData("giantLog", log);
domain.SetData("giantShots", shots);
float start = UnityEngine.Time.realtimeSinceStartup;
string lastMood = "", lastAction = "";
float windUpStart = -1f, hitAt = -1f;
bool shotWind = false, shotHit = false, shotAfter = false;
UnityEngine.Vector3 lastBalloonVel = UnityEngine.Vector3.zero;
float minHandDist = 999f; string minHandInfo = "";

UnityEngine.Transform Find(UnityEngine.Transform root, string name)
{
    foreach (var t in root.GetComponentsInChildren<UnityEngine.Transform>()) if (t.name == name) return t;
    return null;
}

void Shot(string label)
{
    var cam = new UnityEngine.GameObject("RecorderCam").AddComponent<UnityEngine.Camera>();
    var g = m.transform;
    cam.transform.position = g.position + g.right * -9f + g.forward * 1.5f + UnityEngine.Vector3.up * 2.6f;
    cam.transform.LookAt(g.position + g.forward * 1.2f + UnityEngine.Vector3.up * 2.4f);
    cam.fieldOfView = 50f;
    var rt = new UnityEngine.RenderTexture(640, 480, 24);
    cam.targetTexture = rt;
    cam.Render();
    UnityEngine.RenderTexture.active = rt;
    var tex = new UnityEngine.Texture2D(640, 480, UnityEngine.TextureFormat.RGB24, false);
    tex.ReadPixels(new UnityEngine.Rect(0, 0, 640, 480), 0, 0);
    tex.Apply();
    UnityEngine.RenderTexture.active = null;
    string path = "Screenshots/giant_CHARACTER_" + label + ".png";
    System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
    UnityEngine.Object.DestroyImmediate(tex);
    cam.targetTexture = null;
    UnityEngine.Object.DestroyImmediate(rt);
    UnityEngine.Object.DestroyImmediate(cam.gameObject);
    shots.Add(path);
}

UnityEditor.EditorApplication.CallbackFunction tick = null;
tick = () =>
{
    if (!UnityEngine.Application.isPlaying || m == null) { UnityEditor.EditorApplication.update -= tick; return; }
    float t = UnityEngine.Time.realtimeSinceStartup - start;
    var anim = m.GetComponentInChildren<GameJam.Gameplay.CharacterVisual>().Animation;
    var model = m.GetComponentInChildren<GameJam.Gameplay.CharacterVisual>().transform;
    var hand = Find(model, "Hand.R");
    var footL = Find(model, "Foot.L");
    var footR = Find(model, "Foot.R");
    var balloon = GameJam.Net.NetworkBalloon.Instance;
    var rb = balloon.GetComponent<UnityEngine.Rigidbody>();
    string mood = m.Mood.ToString();
    string action = anim != null ? anim.CurrentAction.ToString() : "static";
    var handPos = hand != null ? hand.position : UnityEngine.Vector3.zero;
    float handDist = (handPos - balloon.transform.position).magnitude;
    if (mood != lastMood || action != lastAction) { log.Add($"{t:F2}s mood={mood} action={action} hand={handPos:F2} balloon={balloon.transform.position:F2}"); lastMood = mood; lastAction = action; }
    if (mood == "WindUp" && windUpStart < 0f) windUpStart = t;
    if (!shotWind && windUpStart >= 0f && t > windUpStart + 0.5f) { Shot("1_windup"); shotWind = true; }
    if ((mood == "Slap" || (mood == "Cooldown" && hitAt < 0f)) && handDist < minHandDist) { minHandDist = handDist; minHandInfo = $"{t:F2}s hand={handPos:F2} balloon={balloon.transform.position:F2}"; }
    var vel = rb != null ? rb.linearVelocity : UnityEngine.Vector3.zero;
    if (hitAt < 0f && windUpStart >= 0f && (vel - lastBalloonVel).magnitude > 2f)
    {
        hitAt = t;
        log.Add($"{t:F2}s HIT balloonVel={vel:F1} handToBalloon={handDist:F2} hand={handPos:F2} balloon={balloon.transform.position:F2}");
        if (!shotHit) { Shot("2_hit"); shotHit = true; }
    }
    if (!shotAfter && hitAt >= 0f && t > hitAt + 0.2f) { Shot("3_follow"); shotAfter = true; }
    lastBalloonVel = vel;
    if (footL != null && footR != null)
    {
        var anyFoot = footL.position.y < footR.position.y ? footL : footR;
        var local = m.transform.InverseTransformPoint(anyFoot.position);
        log.Add($"F {t:F3} {anyFoot.position.x:F3} {anyFoot.position.y:F3} {anyFoot.position.z:F3} {(anyFoot == footL ? "L" : "R")} {m.transform.position.x:F3} {m.transform.position.z:F3} {local.z:F3}");
    }
    domain.SetData("giantMinHand", minHandDist.ToString("F2") + " at " + minHandInfo);
    if (t > 9f) { UnityEditor.EditorApplication.update -= tick; log.Add("done"); }
};
domain.SetData("giantRecorder", tick);
UnityEditor.EditorApplication.update += tick;
return "recording CHARACTER";
