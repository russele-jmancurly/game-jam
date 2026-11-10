// Play or Edit mode, prototype scene: a DontSave review camera (display 8) on the lobby's tutorial hole, from a player's
// eyes at the spawn. Capture: capture game_view -- --source camera --camera ReviewCam_LobbyHole --output-path Screenshots/lobby_hole.png
var old = UnityEngine.GameObject.Find("ReviewCam_LobbyHole");
if (old != null) { UnityEngine.Object.DestroyImmediate(old); }
var go = new UnityEngine.GameObject("ReviewCam_LobbyHole") { hideFlags = UnityEngine.HideFlags.DontSave };
var cam = go.AddComponent<UnityEngine.Camera>();
cam.targetDisplay = 7;
cam.fieldOfView = 80f;
cam.nearClipPlane = 0.05f;
go.transform.position = new UnityEngine.Vector3(0.4f, 1.15f, -8.2f);
go.transform.LookAt(new UnityEngine.Vector3(3.9f, 1.5f, -9.3f));
return "ok";
