// Edit or Play mode: DontSave review cameras on the critter audience and the MC (display 8). Capture each with
//   unity command capture game_view --project-path "E:\Project\game-jam" -- --source camera --camera ReviewCam_<Name> --output-path Screenshots/<name>.png
// ReviewCam_CritterFence (a fence log beside the laundry hoop), ReviewCam_CritterStart (the cheering section behind the
// start, from the stage), ReviewCam_Mc (Gonzales on the headline board, from the finish pad). Delete them before saving.
using UnityEngine;

System.Action<string, Vector3, Vector3, float> cam = (name, from, at, fov) =>
{
    var go = GameObject.Find("ReviewCam_" + name);
    if (go == null) go = new GameObject("ReviewCam_" + name) { hideFlags = HideFlags.DontSave };
    var c = go.GetComponent<Camera>();
    if (c == null) c = go.AddComponent<Camera>(); // Unity's fake null: never ?? on components
    c.targetDisplay = 7;
    c.fieldOfView = fov;
    c.nearClipPlane = 0.05f;
    go.transform.position = from;
    go.transform.LookAt(at);
};

Transform fence = null, south = null;
var crowd = Object.FindFirstObjectByType<GameJam.Gameplay.CritterCrowd>();
if (crowd == null) return "no CritterCrowd in the open scene";
foreach (Transform critter in crowd.transform)
{
    Vector3 p = critter.position;
    if (fence == null && p.x > 20f && Mathf.Abs(p.z - 12f) < 4f) fence = critter;
    if (south == null && p.z < -12f) south = critter;
}

if (fence != null) cam("CritterFence", fence.position + new Vector3(-5.5f, 1.6f, -2.5f), fence.position + Vector3.up * 0.6f, 50f);
if (south != null) cam("CritterStart", new Vector3(0f, 1.9f, -6f), new Vector3(0f, 0.8f, south.position.z), 70f);
var mc = GameObject.Find("MC_Gonzales");
if (mc != null) cam("Mc", mc.transform.position + new Vector3(4f, -4f, -16f), mc.transform.position + Vector3.up * 1.2f, 45f);
return $"cams: fence {(fence != null)}, start {(south != null)}, mc {(mc != null)}; critters {crowd.Count}";
