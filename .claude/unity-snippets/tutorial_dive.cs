// Play mode: puts the local player over the scene's (active) mole hole, the way a hop into it would, so the rig's hazard
// sensor finds it and the trip starts for real (fade, leave the session, Tunnel scene, destination). Check progress with
// tutorial_status.cs. In the lobby the hole leads to the tutorial; in the tutorial it leads back to the yard.
var rig = GameJam.Player.LocalRig.Instance;
if (rig == null) { return "no LocalRig in this scene"; }
GameJam.Gameplay.MoleHole hole = null;
foreach (var h in UnityEngine.Object.FindObjectsByType<GameJam.Gameplay.MoleHole>(UnityEngine.FindObjectsSortMode.None)) { hole = h; }
if (hole == null) { return "no active mole hole (a run is on, or this scene has none)"; }
rig.RespawnAt(hole.transform.position + UnityEngine.Vector3.up * 0.05f);
return "player moved over the hole to " + hole.Destination + " at " + hole.transform.position.ToString("F1") + ", body centre " + rig.BodyCenter.ToString("F2");
