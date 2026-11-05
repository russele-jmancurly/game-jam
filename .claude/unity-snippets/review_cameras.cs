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

// Open backyard layout (BackyardCourseBuilder): start stage south, pool east, washing line west, tree centre,
// playground north-west, BBQ north-east, porch finish north.
Place("ReviewCam_Start", new UnityEngine.Vector3(1.5f, 2.2f, -10f), new UnityEngine.Vector3(0f, 2.5f, 20f), 70f);
Place("ReviewCam_Overview", new UnityEngine.Vector3(0f, 42f, -38f), new UnityEngine.Vector3(0f, 0f, 46f), 60f);
Place("ReviewCam_Roof", new UnityEngine.Vector3(2f, 9f, 50f), new UnityEngine.Vector3(0f, 3f, 90f), 70f);
Place("ReviewCam_West", new UnityEngine.Vector3(-4f, 6f, 46f), new UnityEngine.Vector3(-26f, 2f, 52f), 75f);
Place("ReviewCam_East", new UnityEngine.Vector3(6f, 6f, 46f), new UnityEngine.Vector3(26f, 2f, 52f), 75f);
return "review cameras placed";
