// Headset vs PC brightness probe (Play mode, PC test mode). Places a DontSave capture camera "LightCam" at the spawn looking
// over the yard, then sets post-processing and a light scale for the next capture:
//   env LIGHTPROBE_POST=1|0     post-processing (tonemapping, colour grading) on the camera: 1 = the PC look, 0 = the headset
//   env LIGHTPROBE_SCALE=<f>    sun intensity and trilight ambient colours = authored value x f (1 = as authored)
//   env LIGHTPROBE_VIEW=0|1     0 = from the spawn across the yard, 1 = from the lawn back toward the stage
// The authored values are remembered across runs (env LIGHTPROBE_BASE), so scales never compound.
//   unity command capture game_view ... -- --source camera --camera LightCam --output-path Screenshots/light_x.png
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering.Universal;

string Env(string k, string d) => System.Environment.GetEnvironmentVariable(k) ?? d;
float scale = float.Parse(Env("LIGHTPROBE_SCALE", "1"), System.Globalization.CultureInfo.InvariantCulture);
bool post = Env("LIGHTPROBE_POST", "1") == "1";
int view = int.Parse(Env("LIGHTPROBE_VIEW", "0"));

Light sun = RenderSettings.sun;
string baseKey = Env("LIGHTPROBE_BASE", "");
if (baseKey == "")
{
    // LocalRig may already have boosted the scene (headset); in PC mode its boost is 1, so these are the authored values.
    Color s = RenderSettings.ambientSkyColor, e = RenderSettings.ambientEquatorColor, g = RenderSettings.ambientGroundColor;
    baseKey = string.Join(";", new[] { sun.intensity, s.r, s.g, s.b, e.r, e.g, e.b, g.r, g.g, g.b }
        .Select(v => v.ToString(System.Globalization.CultureInfo.InvariantCulture)));
    System.Environment.SetEnvironmentVariable("LIGHTPROBE_BASE", baseKey);
}
float[] b = baseKey.Split(';').Select(v => float.Parse(v, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
sun.intensity = b[0] * scale;
RenderSettings.ambientSkyColor = new Color(b[1], b[2], b[3]) * scale;
RenderSettings.ambientEquatorColor = new Color(b[4], b[5], b[6]) * scale;
RenderSettings.ambientGroundColor = new Color(b[7], b[8], b[9]) * scale;

var cam = GameObject.Find("LightCam");
if (cam == null)
{
    cam = new GameObject("LightCam") { hideFlags = HideFlags.DontSave };
    var c = cam.AddComponent<Camera>();
    c.fieldOfView = 90f;
    c.nearClipPlane = 0.05f;
    c.targetDisplay = 7;
    cam.AddComponent<UniversalAdditionalCameraData>();
}

if (view == 0) { cam.transform.position = new Vector3(0f, 1.1f, -8f); cam.transform.LookAt(new Vector3(6f, 0.8f, 18f)); }
else { cam.transform.position = new Vector3(-4f, 1.1f, 12f); cam.transform.LookAt(new Vector3(2f, 0.8f, -6f)); }
cam.GetComponent<UniversalAdditionalCameraData>().renderPostProcessing = post;
return $"LightCam view {view}: post={post} scale={scale} sun={sun.intensity:F2} ambientSky={RenderSettings.ambientSkyColor}";
