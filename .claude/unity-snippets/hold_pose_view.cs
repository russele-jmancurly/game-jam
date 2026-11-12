// Play mode (PC test mode, Prototype). Puts one item in your right hand and frames it with two DontSave cameras, to
// check how it sits in the paw. Replace ITEM with the item's asset word (e.g. Watermelon) before running.
//   capture game_view ... -- --source camera --camera HoldCamSide --output-path Screenshots/hold_<item>_side.png
//   capture game_view ... -- --source camera --camera HoldCamFront --output-path Screenshots/hold_<item>_front.png
// Returns where the item's centre and its nearest surface are relative to the paw (glove centre), in metres.
using UnityEngine;
string word = "ITEM";
var rig = GameJam.Player.LocalRig.Instance;
if (rig == null) return "no LocalRig";
rig.GetComponent<GameJam.Player.DesktopRigInput>().enabled = false;
rig.SetGrip(true, false);
GameJam.Net.NetworkItem item = null;
foreach (var it in Object.FindObjectsByType<GameJam.Net.NetworkItem>(FindObjectsSortMode.None))
    if (it.Definition.name.Contains(word) && it.HasStateAuthority && !it.IsSpent) item = it;
if (item == null) return "no item " + word;
Transform hand = rig.RightHandFollower;
// Hold the controller out in front of the chest, like a player looking at what they hold.
Vector3 fwd = rig.Head.forward; fwd.y = 0f; fwd.Normalize();
rig.RightController.position = rig.Head.position + fwd * 0.45f + rig.Head.right * 0.15f - Vector3.up * 0.25f;
rig.RightController.rotation = Quaternion.LookRotation(fwd, Vector3.up);
hand.position = rig.RightController.position;
item.GetComponent<Rigidbody>().position = hand.position; item.transform.position = hand.position;
Physics.SyncTransforms();
rig.SetGrip(true, true);
item.transform.SetPositionAndRotation(hand.TransformPoint(item.Definition.HoldOffset), hand.rotation * Quaternion.Euler(item.Definition.HoldEuler));

var avatar = GameJam.Net.NetworkAvatar.Local;
Transform glove = null;
if (avatar != null)
    foreach (Transform t in avatar.GetComponentsInChildren<Transform>(true)) if (t.name == "GloveCenter.R") glove = t;
Vector3 paw = glove != null ? glove.position : hand.position;

Camera Cam(string name)
{
    var go = GameObject.Find(name);
    if (go == null) { go = new GameObject(name) { hideFlags = HideFlags.DontSave }; var c = go.AddComponent<Camera>(); c.nearClipPlane = 0.01f; c.fieldOfView = 40f; c.targetDisplay = 7; }
    return go.GetComponent<Camera>();
}
Vector3 mid = (paw + item.transform.position) * 0.5f;
Camera side = Cam("HoldCamSide");
side.transform.position = mid - hand.right * 0.9f + Vector3.up * 0.15f; side.transform.LookAt(mid);
Camera front = Cam("HoldCamFront");
front.transform.position = mid + hand.up * 0.9f + hand.forward * 0.05f; front.transform.LookAt(mid, hand.forward);

Collider col = item.GetComponent<Collider>();
Vector3 nearest = col.ClosestPoint(paw);
return word + ": held " + item.IsHeldBy(hand) + ", hold offset " + item.Definition.HoldOffset.ToString("F2") + " euler " + item.Definition.HoldEuler
    + ", bounds " + col.bounds.size.ToString("F2") + ", item centre in hand space " + hand.InverseTransformPoint(item.transform.position).ToString("F2")
    + ", paw (glove centre) in hand space " + hand.InverseTransformPoint(paw).ToString("F3")
    + ", gap paw to item surface " + Vector3.Distance(nearest, paw).ToString("F3");
