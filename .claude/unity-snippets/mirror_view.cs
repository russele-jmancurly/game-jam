// Play mode, run twice. First run: makes ReviewCam_Mirror the viewer (MainCamera tag) 2.5 m in front of the stage mirror,
// so the mirror renders for it; capture it with: capture game_view -- --source camera --camera ReviewCam_Mirror.
// Second run: deletes the review camera and gives the MainCamera tag back to the player's Head. Always run the second
// time: a leftover MainCamera-tagged camera hijacks Camera.main (the mirror's viewer) in later sessions.
var existing = UnityEngine.GameObject.Find("ReviewCam_Mirror");
if (existing != null)
{
    UnityEngine.Object.DestroyImmediate(existing);
    var head = UnityEngine.GameObject.Find("Head");
    if (head != null && head.GetComponent<UnityEngine.Camera>() != null)
    {
        head.tag = "MainCamera";
    }

    return "review camera removed, Head is the main camera again";
}

var mirror = UnityEngine.Object.FindAnyObjectByType<GameJam.Gameplay.PlanarMirror>();
UnityEngine.Camera current = UnityEngine.Camera.main;
if (current != null)
{
    current.tag = "Untagged";
}

var go = new UnityEngine.GameObject("ReviewCam_Mirror") { hideFlags = UnityEngine.HideFlags.DontSave };
go.AddComponent<UnityEngine.Camera>().targetDisplay = 7;
go.tag = "MainCamera";
UnityEngine.Vector3 front = -mirror.transform.forward;
go.transform.position = mirror.transform.position + front * 2.5f + mirror.transform.right * 0.6f + UnityEngine.Vector3.down * 0.1f;
go.transform.LookAt(mirror.transform.position);
go.GetComponent<UnityEngine.Camera>().fieldOfView = 60f;
return $"viewer {go.transform.position}; run again to restore";
