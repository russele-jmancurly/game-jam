// Play mode. Tests the giant knockdown through the real throw path: puts the first giant on open lawn (east of the start
// stage), grabs the nearest throwable item, releases it at the giant's chest at 8 m/s, and points ReviewCam_Giant at it.
// Then installs a recorder (EditorApplication.update) that logs mood, ragdoll, stars and body collider every 0.25 s for 7 s.
// Read the log with: script eval --code 'return string.Join("\n", (System.Collections.Generic.List<string>)System.AppDomain.CurrentDomain.GetData("knockLog"));'
// Capture: capture game_view -- --source camera --camera ReviewCam_Giant --output-path Screenshots/giant_down.png
var m = GameJam.Net.MischiefMaker.Instance;
var giantPos = new UnityEngine.Vector3(14f, 0f, 2f);
var nt = m.GetComponent<Fusion.NetworkTransform>();
nt.Teleport(giantPos, UnityEngine.Quaternion.LookRotation(UnityEngine.Vector3.left));
m.transform.SetPositionAndRotation(giantPos, UnityEngine.Quaternion.LookRotation(UnityEngine.Vector3.left));

// Review camera: side-on, a few metres off.
var camGo = UnityEngine.GameObject.Find("ReviewCam_Giant");
if (camGo == null)
{
    camGo = new UnityEngine.GameObject("ReviewCam_Giant") { hideFlags = UnityEngine.HideFlags.DontSave };
    var cam = camGo.AddComponent<UnityEngine.Camera>();
    cam.targetDisplay = 7;
    camGo.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>().renderPostProcessing = true;
}
camGo.transform.position = giantPos + new UnityEngine.Vector3(-3f, 3.5f, -9f);
camGo.transform.LookAt(giantPos + new UnityEngine.Vector3(0f, 1.6f, 0f));
camGo.GetComponent<UnityEngine.Camera>().fieldOfView = 60f;

// The nearest throwable item (not an effect item: those vanish instead of being thrown).
GameJam.Net.NetworkItem item = null;
float best = float.MaxValue;
foreach (var candidate in UnityEngine.Object.FindObjectsByType<GameJam.Net.NetworkItem>(UnityEngine.FindObjectsSortMode.None))
{
    var defField = typeof(GameJam.Net.NetworkItem).GetField("_definition", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
    var def = (GameJam.Core.ItemDefinition)defField.GetValue(candidate);
    if (def == null || def.Effect != GameJam.Core.ItemEffect.None || candidate.IsSpent) continue;
    float d = (candidate.transform.position - giantPos).sqrMagnitude;
    if (d < best) { best = d; item = candidate; }
}
if (item == null) return "no throwable item found";

var start = giantPos + new UnityEngine.Vector3(-2.2f, 2f, 0f);   // in front of the giant (it faces -x), chest height
var hand = new UnityEngine.GameObject("TestHand") { hideFlags = UnityEngine.HideFlags.DontSave }.transform;
hand.position = start;
var itemNt = item.GetComponent<Fusion.NetworkTransform>();
itemNt.Teleport(start, UnityEngine.Quaternion.identity);
item.transform.position = start;
item.Grab(hand, null);
item.Release(new UnityEngine.Vector3(8f, 0.5f, 0f));            // at the giant
UnityEngine.Object.Destroy(hand.gameObject, 1f);

var domain = System.AppDomain.CurrentDomain;
if (domain.GetData("knockRecorder") is UnityEditor.EditorApplication.CallbackFunction old) UnityEditor.EditorApplication.update -= old;
var log = new System.Collections.Generic.List<string>();
domain.SetData("knockLog", log);
float t0 = UnityEngine.Time.time, next = 0f;
UnityEditor.EditorApplication.CallbackFunction rec = null;
rec = () =>
{
    if (!UnityEngine.Application.isPlaying || m == null) { UnityEditor.EditorApplication.update -= rec; return; }
    float t = UnityEngine.Time.time - t0;
    if (t < next) return;
    next += 0.25f;
    var ragdoll = m.GetComponentInChildren<GameJam.Gameplay.GiantRagdoll>(true);
    var stars = m.GetComponentInChildren<GameJam.Gameplay.DizzyStars>(true);
    var hips = ragdoll != null ? ragdoll.HipsGroundPoint(0f) : UnityEngine.Vector3.zero;
    string headY = ragdoll != null && ragdoll.Head != null ? ragdoll.Head.position.y.ToString("F2") : "-";
    string tilt = "-";
    foreach (var bone in m.GetComponentsInChildren<UnityEngine.Transform>(true))
    {
        if (bone.name != "Head") continue;
        var chest = bone.parent != null ? bone.parent.parent : null; // Head > Neck > Chest
        if (chest != null) tilt = UnityEngine.Vector3.Angle(bone.up, chest.up).ToString("F0");
    }
    log.Add($"t={t:F2} headTilt={tilt} mood={m.Mood} ragdollDown={(ragdoll != null && ragdoll.IsDown)} valid={(ragdoll != null && ragdoll.IsValid)} headY={headY} hips=({hips.x:F1},{hips.z:F1}) root=({m.transform.position.x:F1},{m.transform.position.z:F1}) stars={(stars != null && stars.gameObject.activeSelf)} body={m.GetComponent<UnityEngine.Collider>().enabled} item=({item.transform.position.x:F1},{item.transform.position.y:F1})");
    if (t > 7f) UnityEditor.EditorApplication.update -= rec;
};
domain.SetData("knockRecorder", rec);
UnityEditor.EditorApplication.update += rec;
return $"threw {item.name} at {m.DisplayName} from {start}";
