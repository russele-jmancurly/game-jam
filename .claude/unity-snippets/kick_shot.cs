// Play mode, alongside giant_kick_check.cs (run this right after it): renders ReviewCam_Kick to Screenshots/kick_windup.png
// when the giant reaches the end of its wind-up and Screenshots/kick.png halfway through the swing, and ReviewCam_Mc
// (critter_review.cs places it) to Screenshots/mc_play.png at once. Logs "[KickShot]" lines.
using UnityEngine;

var giant = GameJam.Net.MischiefMaker.Instance;
if (giant == null) return "no giant";
System.Action<string, string> shoot = (camName, file) =>
{
    var go = GameObject.Find(camName);
    if (go == null) { Debug.Log("[KickShot] no " + camName); return; }
    var cam = go.GetComponent<Camera>();
    var rt = new RenderTexture(1280, 720, 24);
    cam.targetTexture = rt;
    cam.Render();
    RenderTexture.active = rt;
    var tex = new Texture2D(1280, 720, TextureFormat.RGB24, false);
    tex.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
    tex.Apply();
    System.IO.File.WriteAllBytes(file, tex.EncodeToPNG());
    RenderTexture.active = null;
    cam.targetTexture = null;
    Object.Destroy(rt);
    Object.Destroy(tex);
    Debug.Log("[KickShot] wrote " + file);
};

shoot("ReviewCam_Mc", "Screenshots/mc_play.png");
float windUpSince = -1f, kickSince = -1f;
bool shotWindUp = false, shotKick = false;
UnityEngine.Events.UnityAction tick = null;
tick = () =>
{
    if (giant.Mood == GameJam.Net.RoamerMood.KickWindUp && windUpSince < 0f) windUpSince = Time.time;
    if (giant.Mood == GameJam.Net.RoamerMood.Kick && kickSince < 0f) kickSince = Time.time;
    if (!shotWindUp && windUpSince > 0f && Time.time - windUpSince > 0.6f) { shotWindUp = true; shoot("ReviewCam_Kick", "Screenshots/kick_windup.png"); }
    if (!shotKick && kickSince > 0f && Time.time - kickSince > 0.12f) { shotKick = true; shoot("ReviewCam_Kick", "Screenshots/kick.png"); }
    if (shotKick) { Application.onBeforeRender -= tick; Debug.Log("[KickShot] DONE"); }
};
Application.onBeforeRender += tick;
return "KickShot armed";
