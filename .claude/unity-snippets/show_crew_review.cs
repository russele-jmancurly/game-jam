// Play mode: points a review camera at a named map figure (MC_Gonzales) and reports the roster.
var figureName = "MC_Gonzales";
var figure = UnityEngine.GameObject.Find(figureName);
if (figure == null) { return "missing " + figureName; }
var vis = figure.GetComponent<GameJam.Gameplay.CharacterVisual>();
var camGo = UnityEngine.GameObject.Find("ReviewCam_Front");
if (camGo == null) { camGo = new UnityEngine.GameObject("ReviewCam_Front"); }
var cam = camGo.GetComponent<UnityEngine.Camera>(); if (cam == null) { cam = camGo.AddComponent<UnityEngine.Camera>(); }
cam.nearClipPlane = 0.05f; cam.fieldOfView = 40f; cam.depth = -50; cam.targetDisplay = 7;
var bounds = new UnityEngine.Bounds(figure.transform.position, UnityEngine.Vector3.zero);
foreach (var r in figure.GetComponentsInChildren<UnityEngine.Renderer>()) { bounds.Encapsulate(r.bounds); }
camGo.transform.position = bounds.center + figure.transform.forward * (bounds.size.y * 2.2f) + UnityEngine.Vector3.up * 0.2f;
camGo.transform.LookAt(bounds.center);
var roster = UnityEditor.AssetDatabase.LoadAssetAtPath<GameJam.Core.CharacterRoster>("Assets/GameJam/Config/CharacterRoster.asset");
var roamer = UnityEngine.Object.FindFirstObjectByType<GameJam.Net.MischiefMaker>();
return figureName + " shown=" + (vis.Shown ? vis.Shown.DisplayName : "none") + " size=" + bounds.size.ToString("F2") + " roster=" + roster.Count + " roamer=" + (roamer ? roamer.DisplayName : "none");
