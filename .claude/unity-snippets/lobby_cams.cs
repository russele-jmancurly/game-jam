// Lobby review cameras (Play mode, or Edit mode): DontSave cameras on display 8 for `capture game_view --source camera`.
//   LobbyCam_Eye: a player's eye at the spawn, looking at the red button (sign, arrow, glass walls)
//   LobbyCam_Out: outside the stage's south-west corner, looking in (how clear the walls are)
// Run again with env LOBBYCAMS_CLEAR=1 to delete them.
using UnityEngine;

foreach (string name in new[] { "LobbyCam_Eye", "LobbyCam_Out" })
{
    var old = GameObject.Find(name);
    if (old != null) Object.DestroyImmediate(old);
}
if (System.Environment.GetEnvironmentVariable("LOBBYCAMS_CLEAR") == "1") return "cleared";

System.Action<string, Vector3, Vector3> cam = (name, position, target) =>
{
    var go = new GameObject(name) { hideFlags = HideFlags.DontSave };
    var c = go.AddComponent<Camera>();
    c.fieldOfView = 70f;
    c.nearClipPlane = 0.03f;
    c.targetDisplay = 7;
    go.transform.position = position;
    go.transform.LookAt(target);
};
cam("LobbyCam_Eye", new Vector3(0.6f, 1.15f, -8f), new Vector3(4f, 1.1f, -3.5f));
cam("LobbyCam_Out", new Vector3(-10f, 2.6f, -15f), new Vector3(0f, 1.5f, -6f));
return "LobbyCam_Eye, LobbyCam_Out placed";
