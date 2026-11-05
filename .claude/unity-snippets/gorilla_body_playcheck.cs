// Play-mode check for the gorilla body: IK error per hand, which parts show for the local player, and a review camera
// in front of the player (body shown temporarily so it can be captured; call again with restore to hide it).
var avatar = GameJam.Net.NetworkAvatar.Local;
if (avatar == null) { return "no local avatar"; }
var rig = avatar.GetComponentInChildren<GameJam.Gameplay.GorillaBodyRig>(true);
var so = new UnityEditor.SerializedObject(rig);
System.Func<string, UnityEngine.Transform> t = n => (UnityEngine.Transform)so.FindProperty(n).objectReferenceValue;
float errL = UnityEngine.Vector3.Distance(t("_left.GloveCenter").position, t("_left.Target").position);
float errR = UnityEngine.Vector3.Distance(t("_right.GloveCenter").position, t("_right.Target").position);
float shoulderToHandL = UnityEngine.Vector3.Distance(t("_left.UpperArm").position, t("_left.Target").position);
float shoulderToHandR = UnityEngine.Vector3.Distance(t("_right.UpperArm").position, t("_right.Target").position);
var localRig = GameJam.Player.LocalRig.Instance;
var gloveSphere = localRig.LeftHandFollower.Find("Glove").GetComponent<UnityEngine.Renderer>().enabled;
string parts = "body=" + rig.BodyRenderer.enabled + " arms=" + rig.ArmsRenderer.enabled + " rigGloveSphere=" + gloveSphere;

rig.BodyRenderer.enabled = true; // temporary, for the capture
var head = localRig.Head;
var yaw = UnityEngine.Quaternion.Euler(0f, head.eulerAngles.y, 0f);
var camGo = UnityEngine.GameObject.Find("ReviewCam_Front"); if (camGo == null) { camGo = new UnityEngine.GameObject("ReviewCam_Front"); }
var cam = camGo.GetComponent<UnityEngine.Camera>(); if (cam == null) { cam = camGo.AddComponent<UnityEngine.Camera>(); }
cam.nearClipPlane = 0.02f; cam.fieldOfView = 55f; cam.depth = -50; cam.targetDisplay = 7;
camGo.transform.position = head.position + yaw * new UnityEngine.Vector3(0.5f, 0.05f, 1.5f);
camGo.transform.LookAt(head.position + UnityEngine.Vector3.down * 0.3f);
return string.Format("errL={0:F3} errR={1:F3} shoulderToHand L={2:F2} R={3:F2} {4} handL={5} handR={6} head={7}",
  errL, errR, shoulderToHandL, shoulderToHandR, parts, t("_left.Target").position.ToString("F2"), t("_right.Target").position.ToString("F2"), head.position.ToString("F2"));
