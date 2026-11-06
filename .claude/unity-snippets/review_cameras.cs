// Creates (or moves) temporary review cameras for before/after captures. HideFlags.DontSave keeps them
// out of the saved scene. Capture with: capture game_view -- --source camera --camera ReviewCam_<Name> --output-path ...
void Place(string name, UnityEngine.Vector3 position, UnityEngine.Vector3 lookAt, float fov)
{
    var go = UnityEngine.GameObject.Find(name);
    if (go == null)
    {
        go = new UnityEngine.GameObject(name) { hideFlags = UnityEngine.HideFlags.DontSave };
        var cam = go.AddComponent<UnityEngine.Camera>();
        cam.targetDisplay = 7; // display 8: never drawn in the Game view; captured on demand by name
        var data = go.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
        data.renderPostProcessing = true;
    }

    go.transform.position = position;
    go.transform.LookAt(lookAt);
    var camera = go.GetComponent<UnityEngine.Camera>();
    camera.fieldOfView = fov;
    camera.nearClipPlane = 0.05f;
    camera.farClipPlane = 400f;
}

// Trail-shaped backyard (BackyardCourseBuilder): start stage south, hoop trail north to the finish pad (-6, 38), pool and
// playground west, washing line, tree and BBQ east, headline board and house behind the finish.
Place("ReviewCam_Start", new UnityEngine.Vector3(1.5f, 2.2f, -10f), new UnityEngine.Vector3(-2f, 2.5f, 20f), 70f);
Place("ReviewCam_Eye", new UnityEngine.Vector3(0f, 1.05f, -8f), new UnityEngine.Vector3(0f, 1.6f, 20f), 80f);   // a player's eyes on the stage
Place("ReviewCam_Overview", new UnityEngine.Vector3(0f, 36f, -32f), new UnityEngine.Vector3(-2f, 0f, 22f), 60f);
Place("ReviewCam_Roof", new UnityEngine.Vector3(-4f, 13f, 52f), new UnityEngine.Vector3(-2f, 2f, 15f), 70f); // Squoof's view down the trail
Place("ReviewCam_West", new UnityEngine.Vector3(6f, 6f, 20f), new UnityEngine.Vector3(-22f, 2f, 22f), 75f);
Place("ReviewCam_East", new UnityEngine.Vector3(-8f, 6f, 20f), new UnityEngine.Vector3(18f, 2f, 22f), 75f);
return "review cameras placed";
