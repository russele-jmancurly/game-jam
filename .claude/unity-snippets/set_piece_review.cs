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

// Close-ups of the modelled set pieces (Art/Set), plus the overview.
Place("ReviewCam_Overview", new UnityEngine.Vector3(0f, 42f, -38f), new UnityEngine.Vector3(0f, 0f, 46f), 60f);
Place("ReviewCam_Tree", new UnityEngine.Vector3(9f, 5f, 27f), new UnityEngine.Vector3(1f, 6f, 40f), 70f);
Place("ReviewCam_Pool", new UnityEngine.Vector3(10f, 7f, 12f), new UnityEngine.Vector3(24f, 1f, 28f), 65f);
Place("ReviewCam_Playground", new UnityEngine.Vector3(-6f, 7f, 56f), new UnityEngine.Vector3(-22f, 2f, 70f), 70f);
Place("ReviewCam_Bbq", new UnityEngine.Vector3(10f, 6f, 55f), new UnityEngine.Vector3(24f, 2f, 70f), 68f);
Place("ReviewCam_Laundry", new UnityEngine.Vector3(-10f, 4f, 12f), new UnityEngine.Vector3(-26f, 2.5f, 27f), 70f);
return "placed";
