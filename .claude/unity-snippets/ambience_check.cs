// Play mode check for the sound/ambience/mirror batch: turns the PC-test player toward the stage mirror, reports the
// mirror camera, the generated sounds wired on each hook, the fan air stream position and the ambient emitters, and
// places review cameras on the box fan (ReviewCam_Fan) and a hoop (ReviewCam_Hoop) for captures.
var sb = new System.Text.StringBuilder();
var input = UnityEngine.Object.FindAnyObjectByType<GameJam.Player.DesktopRigInput>();
if (input != null)
{
    var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
    input.GetType().GetField("_yaw", flags).SetValue(input, -82f);
    input.GetType().GetField("_pitch", flags).SetValue(input, 5f);
    sb.AppendLine("turned PC player toward the mirror");
}

var mirror = UnityEngine.Object.FindAnyObjectByType<GameJam.Gameplay.PlanarMirror>();
var mirrorCam = mirror != null ? mirror.GetComponentInChildren<UnityEngine.Camera>(true) : null;
sb.AppendLine($"mirror at {mirror?.transform.position} camera {(mirrorCam ? (mirrorCam.enabled ? "ON" : "off") : "missing")} rt {mirrorCam?.targetTexture?.width}x{mirrorCam?.targetTexture?.height}");

foreach (var source in UnityEngine.Object.FindObjectsByType<UnityEngine.AudioSource>(UnityEngine.FindObjectsSortMode.None))
{
    if (source.clip != null && !source.clip.name.StartsWith("Balloon_Slap") && !source.clip.name.Contains("Tap") && !source.clip.name.Contains("Slide"))
    {
        sb.AppendLine($"  audio {source.name} clip {source.clip.name} {source.clip.length:F2}s loop {source.loop} playing {source.isPlaying} 3D {source.spatialBlend}");
    }
}

foreach (var ps in UnityEngine.Object.FindObjectsByType<UnityEngine.ParticleSystem>(UnityEngine.FindObjectsSortMode.None))
{
    if (ps.name == "AirStream" || ps.name == "AmbientPollen" || ps.name == "FallingLeaves")
    {
        sb.AppendLine($"  particles {ps.name} at {ps.transform.position} alive {ps.particleCount} playing {ps.isPlaying}");
    }
}

void Place(string name, UnityEngine.Vector3 position, UnityEngine.Vector3 lookAt)
{
    var go = UnityEngine.GameObject.Find(name);
    if (go == null)
    {
        go = new UnityEngine.GameObject(name) { hideFlags = UnityEngine.HideFlags.DontSave };
        go.AddComponent<UnityEngine.Camera>().targetDisplay = 7;
    }

    go.transform.position = position;
    go.transform.LookAt(lookAt);
}

var fan = UnityEngine.GameObject.Find("FanBlades");
if (fan != null)
{
    Place("ReviewCam_Fan", fan.transform.position + new UnityEngine.Vector3(-4f, 5f, -9f), fan.transform.position + new UnityEngine.Vector3(-5f, 0f, 0f));
}

var hoop = UnityEngine.GameObject.Find("Hoop1");
if (hoop != null)
{
    Place("ReviewCam_Hoop", hoop.transform.position + hoop.transform.forward * -5f + UnityEngine.Vector3.up, hoop.transform.position);
}

return sb.ToString();
