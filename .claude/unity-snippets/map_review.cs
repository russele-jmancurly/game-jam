// Edit or Play mode: renders the yard's layout to Screenshots/map_*.png with DontSave cameras (removed at the end):
// map_top (straight down, the whole yard), map_start (from the stage up the trail) and one shot per gap obstacle:
// map_garden, map_wagon, map_sprinkler, map_hanging, map_wall, map_pinatas (the finish pen from outside), map_pen (looking up it).
//   unity command script eval --project-path "E:\Project\game-jam" -- --file .claude/unity-snippets/map_review.cs
using UnityEngine;

var go = new GameObject("ReviewCam_Map") { hideFlags = HideFlags.DontSave };
var cam = go.AddComponent<Camera>();
cam.targetDisplay = 7;
cam.farClipPlane = 400f;
System.Action<string, int, int> shoot = (file, w, h) =>
{
    var rt = new RenderTexture(w, h, 24);
    cam.targetTexture = rt;
    cam.Render();
    RenderTexture.active = rt;
    var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
    tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
    tex.Apply();
    System.IO.File.WriteAllBytes("Screenshots/" + file + ".png", tex.EncodeToPNG());
    RenderTexture.active = null;
    cam.targetTexture = null;
    Object.DestroyImmediate(rt);
    Object.DestroyImmediate(tex);
};
System.Action<string, Vector3, Vector3, float> view = (file, from, at, fov) =>
{
    cam.orthographic = false;
    cam.fieldOfView = fov;
    go.transform.position = from;
    go.transform.LookAt(at);
    shoot(file, 1100, 700);
};

float hw = GameJam.Editor.BackyardCourseBuilder.HalfWidth, south = GameJam.Editor.BackyardCourseBuilder.SouthEdge, north = GameJam.Editor.BackyardCourseBuilder.NorthEdge;
cam.orthographic = true;
cam.orthographicSize = (north - south) * 0.5f + 4f;
go.transform.position = new Vector3(0f, 120f, (south + north) * 0.5f);
go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
shoot("map_top", 620, 1200);

var hoops = GameJam.Editor.BackyardCourseBuilder.TrailHoops;
var cps = GameJam.Editor.BackyardCourseBuilder.TrailCheckpoints;
var finish = GameJam.Editor.BackyardCourseBuilder.TrailFinish;
view("map_start", new Vector3(0f, 2.2f, -9f), new Vector3(-4f, 2f, 12f), 75f);
System.Func<string, Transform> find = n => { var o = GameObject.Find(n); return o != null ? o.transform : null; };
System.Action<string, Vector3, Vector3> gap = (file, from, to) =>
{
    Vector3 dir = (to - from); dir.y = 0f; dir.Normalize();
    view(file, new Vector3(from.x, 0f, from.z) - dir * 3f + Vector3.up * 2.6f, new Vector3(to.x, 2f, to.z), 70f);
};
gap("map_garden", hoops[0], hoops[1]);
gap("map_wagon", cps[1], GameJam.Editor.BackyardCourseBuilder.PoolCenter + new Vector3(-12f, 0f, 0f));
gap("map_sprinkler", cps[2], hoops[3]);
gap("map_hanging", cps[3], hoops[4]);
gap("map_wall", cps[4], GameJam.Editor.BackyardCourseBuilder.TreeCenter + new Vector3(2.6f, 0f, -1.9f));
view("map_pinatas", new Vector3(finish.x - 9f, 3.2f, finish.z - 24f), new Vector3(finish.x, 2.5f, finish.z - 6f), 70f);
view("map_pen", new Vector3(finish.x, 2f, finish.z - 17f), new Vector3(finish.x, 2.6f, finish.z), 75f);
Object.DestroyImmediate(go);
return "wrote Screenshots/map_top, map_start, map_garden, map_wagon, map_sprinkler, map_hanging, map_wall, map_pinatas";
