var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>("Assets/GameJam/Prefabs/Net/PlayerAvatar.prefab");
var rig = prefab.GetComponentInChildren<GameJam.Gameplay.GorillaBodyRig>(true);
if (rig == null) { return "no rig"; }
var so = new UnityEditor.SerializedObject(rig);
var sb = new System.Text.StringBuilder();
foreach (var n in new[] { "_head", "_headTarget", "_left.UpperArm", "_left.Forearm", "_left.Hand", "_left.GloveCenter", "_left.Target", "_right.Target", "_bodyRenderer", "_armsRenderer" }) {
  var p = so.FindProperty(n); sb.Append(n).Append('=').Append(p != null && p.objectReferenceValue ? p.objectReferenceValue.name : "NULL").Append(' ');
}
sb.Append(" bodyFur=").Append(so.FindProperty("_bodyFurSlot").intValue).Append(" armsFur=").Append(so.FindProperty("_armsFurSlot").intValue);
foreach (var r in rig.GetComponentsInChildren<UnityEngine.SkinnedMeshRenderer>(true)) { sb.Append("\n").Append(r.name).Append(": "); foreach (var m in r.sharedMaterials) sb.Append(m ? m.name : "null").Append(','); }
sb.Append("\nrig local pos ").Append(rig.transform.localPosition).Append(" animator ").Append(rig.GetComponent<UnityEngine.Animator>() != null);
var avatar = prefab.GetComponent<GameJam.Net.NetworkAvatar>(); var aso = new UnityEditor.SerializedObject(avatar);
sb.Append(" avatar._bodyRig=").Append(aso.FindProperty("_bodyRig").objectReferenceValue != null);
return sb.ToString();
