var go = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>("Assets/GameJam/Art/Characters/GorillaBody.fbx");
if (go == null) { return "missing"; }
var sb = new System.Text.StringBuilder();
sb.Append("root rot ").Append(go.transform.localRotation.eulerAngles).Append(" scale ").Append(go.transform.localScale).Append('\n');
foreach (var t in go.GetComponentsInChildren<UnityEngine.Transform>(true)) {
  if (t.name.Contains("Index") || t.name.Contains("Middle") || t.name.Contains("Thumb")) continue;
  sb.Append(t.name).Append(" pos ").Append(t.position.ToString("F3")).Append(" rot ").Append(t.rotation.eulerAngles.ToString("F0")).Append('\n');
}
foreach (var r in go.GetComponentsInChildren<UnityEngine.SkinnedMeshRenderer>(true)) {
  sb.Append("SMR ").Append(r.name).Append(" bones ").Append(r.bones.Length).Append(" mats ").Append(r.sharedMaterials.Length).Append(" bounds ").Append(r.localBounds).Append(" root ").Append(r.rootBone ? r.rootBone.name : "null").Append('\n');
}
var animator = go.GetComponent<UnityEngine.Animator>();
sb.Append("animator ").Append(animator != null);
return sb.ToString();
