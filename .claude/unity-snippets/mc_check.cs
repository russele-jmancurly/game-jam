// MC check (Play mode, Prototype): Gonzales stands on the headline board, rigged and animated. Places a DontSave camera
// in front of him, renders Screenshots/mc_idle.png, fires a critter cheer (CritterCrowd.CheerAll, what the finish does)
// and checks he plays Cheer (McReactions), rendering Screenshots/mc_cheer.png mid-pump; then forces a Shadowbox and
// renders Screenshots/mc_box.png. Logs "[McTest]" lines, then DONE.
//   unity command script eval --project-path "E:\Project\game-jam" -- --file .claude/unity-snippets/mc_check.cs
using UnityEngine;

var mc = GameObject.Find("MC_Gonzales");
if (mc == null) return "no MC_Gonzales in the scene";
var visual = mc.GetComponent<GameJam.Gameplay.CharacterVisual>();
System.Action<string, bool, string> report = (name, pass, detail) => Debug.Log($"[McTest] {(pass ? "PASS" : "FAIL")} {name}: {detail}");

var renderers = mc.GetComponentsInChildren<Renderer>();
var bounds = renderers.Length > 0 ? renderers[0].bounds : new Bounds(mc.transform.position, Vector3.zero);
foreach (var r in renderers) bounds.Encapsulate(r.bounds);
report("rigged model shows", visual != null && visual.Animation != null && mc.GetComponentInChildren<SkinnedMeshRenderer>() != null,
    $"animator={(visual != null && visual.Animation != null)} skinned={mc.GetComponentInChildren<SkinnedMeshRenderer>() != null} size={bounds.size:F2}");

var camGo = GameObject.Find("ReviewCam_McClose");
if (camGo == null) camGo = new GameObject("ReviewCam_McClose") { hideFlags = HideFlags.DontSave };
var cam = camGo.GetComponent<Camera>();
if (cam == null) cam = camGo.AddComponent<Camera>();
cam.targetDisplay = 7;
cam.fieldOfView = 40f;
Vector3 front = mc.transform.forward; // he faces down the trail (south)
camGo.transform.position = bounds.center + front * (bounds.size.y * 2.2f) + Vector3.up * 0.3f;
camGo.transform.LookAt(bounds.center);

System.Action<string> shoot = file =>
{
    var rt = new RenderTexture(900, 900, 24);
    cam.targetTexture = rt;
    cam.Render();
    RenderTexture.active = rt;
    var tex = new Texture2D(900, 900, TextureFormat.RGB24, false);
    tex.ReadPixels(new Rect(0, 0, 900, 900), 0, 0);
    tex.Apply();
    System.IO.File.WriteAllBytes(file, tex.EncodeToPNG());
    RenderTexture.active = null;
    cam.targetTexture = null;
    Object.Destroy(rt);
    Object.Destroy(tex);
};

shoot("Screenshots/mc_idle.png");
float start = Time.time;
int step = 0;
UnityEngine.Events.UnityAction tick = null;
tick = () =>
{
    float t = Time.time - start;
    var anim = visual.Animation;
    if (step == 0 && t > 0.3f)
    {
        GameJam.Gameplay.CritterCrowd.CheerAll(2);
        step = 1;
    }
    else if (step == 1 && t > 0.6f)
    {
        report("cheers with the critters", anim != null && anim.CurrentAction == GameJam.Gameplay.CharacterAction.Cheer, $"action={(anim != null ? anim.CurrentAction.ToString() : "none")}");
        shoot("Screenshots/mc_cheer.png");
        step = 2;
    }
    else if (step == 2 && t > 2.4f)
    {
        anim.Play(GameJam.Gameplay.CharacterAction.Shadowbox);
        step = 3;
    }
    else if (step == 3 && t > 2.9f)
    {
        report("shadowbox clip plays", anim.CurrentAction == GameJam.Gameplay.CharacterAction.Shadowbox, $"action={anim.CurrentAction}");
        shoot("Screenshots/mc_box.png");
        Application.onBeforeRender -= tick;
        Debug.Log("[McTest] DONE (ReviewCam_McClose is DontSave; delete it before saving)");
    }
};
Application.onBeforeRender += tick;
return "McTest started (~3 s)";
