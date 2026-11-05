// Play mode, run Running: for hoop HOOP_INDEX, find the solid floor under it, check nothing solid cuts the ring, then put
// the balloon ~1 m above that floor (a gorilla-sized player's shoulder) and give it a MEDIUM slap (3 m/s hand x 0.9).
int index = HOOP_INDEX;
GameJam.Gameplay.HoopCheckpoint hoop = null;
foreach (var h in UnityEngine.Object.FindObjectsByType<GameJam.Gameplay.HoopCheckpoint>(UnityEngine.FindObjectsSortMode.None)) { if (h.name == "Hoop" + (index + 1)) hoop = h; }
var c = hoop.transform.position;
float floor = 0f; string floorName = "none";
foreach (var hit in UnityEngine.Physics.RaycastAll(c, UnityEngine.Vector3.down, 30f, ~0, UnityEngine.QueryTriggerInteraction.Ignore))
{
    if (hit.collider.GetComponentInParent<GameJam.Net.NetworkAvatar>() != null || hit.collider.attachedRigidbody != null) continue;
    if (floorName == "none" || hit.point.y > floor) { floor = hit.point.y; floorName = hit.collider.name; }
}
var cut = new System.Collections.Generic.List<string>();
foreach (var col in UnityEngine.Physics.OverlapSphere(c, 1.0f, ~0, UnityEngine.QueryTriggerInteraction.Ignore))
{
    if (col.attachedRigidbody == null && col.GetComponentInParent<GameJam.Net.NetworkAvatar>() == null) cut.Add(col.name);
}
// Start under the centre, or beside a hot/solid thing (the grill) on the ground in front of it.
var start = new UnityEngine.Vector3(c.x, floor + 1.0f, c.z);
var velocity = new UnityEngine.Vector3(0f, 2.7f, 0f);
if (floorName.StartsWith("Grill")) { start = new UnityEngine.Vector3(c.x, 1.0f, c.z - 1.6f); velocity = new UnityEngine.Vector3(0f, 3.0f, 1.2f); }  // from the lawn beside the grill
if (floorName.StartsWith("Pool")) { start = new UnityEngine.Vector3(c.x - 1.2f, 1.625f + 1.0f, c.z); velocity = new UnityEngine.Vector3(0.6f, 2.7f, 0f); } // from the diving board tip
if (c.x < -45f) { start = new UnityEngine.Vector3(-47.2f, 7.1f, c.z); velocity = new UnityEngine.Vector3(0.6f, 2.7f, 0f); } // a hand at the picket tops
var balloon = GameJam.Net.NetworkBalloon.Instance;
balloon.ResetTo(start);
var runner = UnityEngine.Object.FindFirstObjectByType<Fusion.NetworkRunner>();
balloon.RequestHit(runner.LocalPlayer, velocity);
return "Hoop" + (index + 1) + " centre=" + c.ToString("F2") + " floor=" + floorName + "@" + floor.ToString("F2") + " (centre " + (c.y - floor).ToString("F2") + " above) ringCutBy=[" + string.Join(",", cut) + "] start=" + start.ToString("F2");
