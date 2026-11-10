// Play or Edit mode, tutorial scene: DontSave review cameras on display 8 (capture with
// `capture game_view -- --source camera --camera ReviewCam_<Name> --output-path Screenshots/<name>.png`).
// Names: TutStart (a player's view from the landing spot), TutRoom1 (section 1 from above), TutSlap / TutPop /
// TutItems / TutGiant (a player's view on entering each section), TutGate (a gate up close). Delete them before saving.
foreach (var old in UnityEngine.Object.FindObjectsByType<UnityEngine.Camera>(UnityEngine.FindObjectsSortMode.None))
{
    if (old.name.StartsWith("ReviewCam_Tut")) { UnityEngine.Object.DestroyImmediate(old.gameObject); }
}
var views = new (string name, UnityEngine.Vector3 from, UnityEngine.Vector3 to, float fov)[]
{
    ("TutStart", new UnityEngine.Vector3(0f, 0.75f, -0.5f), new UnityEngine.Vector3(0f, 2.4f, 9f), 90f),
    ("TutRoom1", new UnityEngine.Vector3(10f, 7f, -5f), new UnityEngine.Vector3(-2f, 1f, 6f), 80f),
    ("TutSlap", new UnityEngine.Vector3(0f, 0.75f, 10.5f), new UnityEngine.Vector3(1f, 2.2f, 20f), 90f),
    ("TutPop", new UnityEngine.Vector3(0f, 0.75f, 26.5f), new UnityEngine.Vector3(0f, 1.8f, 36f), 95f),
    ("TutItems", new UnityEngine.Vector3(0f, 0.75f, 38.5f), new UnityEngine.Vector3(0f, 1.8f, 48f), 95f),
    ("TutGiant", new UnityEngine.Vector3(0f, 0.75f, 50.2f), new UnityEngine.Vector3(0f, 2.2f, 60f), 95f),
    ("TutGate", new UnityEngine.Vector3(1.2f, 1f, 4.5f), new UnityEngine.Vector3(0f, 2.4f, 9f), 75f),
};
foreach (var v in views)
{
    var go = new UnityEngine.GameObject("ReviewCam_" + v.name) { hideFlags = UnityEngine.HideFlags.DontSave };
    var cam = go.AddComponent<UnityEngine.Camera>();
    cam.targetDisplay = 7;
    cam.fieldOfView = v.fov;
    cam.nearClipPlane = 0.05f;
    cam.clearFlags = UnityEngine.CameraClearFlags.SolidColor;
    cam.backgroundColor = new UnityEngine.Color(0.07f, 0.045f, 0.03f);
    go.transform.position = v.from;
    go.transform.LookAt(v.to);
}
return "review cameras: " + views.Length;
