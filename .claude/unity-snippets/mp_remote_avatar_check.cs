// 2-player test: on this instance, check the REMOTE player's porcupine (visible, arms reaching its synced hands).
var sb = new System.Text.StringBuilder();
foreach (var avatar in UnityEngine.Object.FindObjectsByType<GameJam.Net.NetworkAvatar>(UnityEngine.FindObjectsSortMode.None))
{
    var rig = avatar.GetComponentInChildren<GameJam.Gameplay.GorillaBodyRig>(true);
    var so = new UnityEditor.SerializedObject(rig);
    System.Func<string, UnityEngine.Transform> t = n => (UnityEngine.Transform)so.FindProperty(n).objectReferenceValue;
    float errL = UnityEngine.Vector3.Distance(t("_left.GloveCenter").position, t("_left.Target").position);
    float errR = UnityEngine.Vector3.Distance(t("_right.GloveCenter").position, t("_right.Target").position);
    var tag = avatar.GetComponentInChildren<UnityEngine.TextMesh>(true);
    sb.Append(avatar.HasStateAuthority ? "LOCAL" : "REMOTE").Append(" p").Append(avatar.Object.StateAuthority.PlayerId)
      .Append(" body=").Append(rig.BodyRenderer.enabled).Append(" arms=").Append(rig.ArmsRenderer.enabled)
      .Append(" ikErr=").Append(errL.ToString("F3")).Append('/').Append(errR.ToString("F3"))
      .Append(" tag='").Append(tag != null ? tag.text : "").Append("' head=").Append(t("_headTarget").position.ToString("F1")).Append(" | ");
}
return sb.ToString();
