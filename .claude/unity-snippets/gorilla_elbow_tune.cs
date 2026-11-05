// Live elbow-hint tuning on the local avatar's GorillaBodyRig (play mode only), then a 3/4 review camera.
var rig = GameJam.Net.NetworkAvatar.Local.GetComponentInChildren<GameJam.Gameplay.GorillaBodyRig>(true);
var so = new UnityEditor.SerializedObject(rig);
so.FindProperty("_elbowHint").vector3Value = new UnityEngine.Vector3(0.45f, -1f, -0.25f);
so.ApplyModifiedPropertiesWithoutUndo();
var head = GameJam.Player.LocalRig.Instance.Head;
var yaw = UnityEngine.Quaternion.Euler(0f, head.eulerAngles.y, 0f);
var camGo = UnityEngine.GameObject.Find("ReviewCam_Front");
camGo.transform.position = head.position + yaw * new UnityEngine.Vector3(1.1f, 0.25f, 1.0f);
camGo.transform.LookAt(head.position + UnityEngine.Vector3.down * 0.3f);
return "ok";
