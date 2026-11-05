// Reads the numbers that decide how high players and the balloon can get (for sizing hoops).
var rig = UnityEditor.AssetDatabase.LoadAssetAtPath<GameJam.Core.PlayerRigConfig>("Assets/GameJam/Config/PlayerRigConfig.asset");
var bc = UnityEditor.AssetDatabase.LoadAssetAtPath<GameJam.Core.BalloonConfig>("Assets/GameJam/Config/BalloonConfig.asset");
var sb = new System.Text.StringBuilder();
sb.Append("gravity=").Append(UnityEngine.Physics.gravity.y).Append(" maxJump=").Append(rig.MaxJumpSpeed).Append(" jumpMult=").Append(rig.JumpMultiplier)
  .Append(" velLimit=").Append(rig.VelocityLimit).Append(" maxArm=").Append(rig.MaxArmLength).Append(" bodyDrop=").Append(rig.BodyDrop)
  .Append(" desktopJump=").Append(rig.DesktopJumpSpeed).Append("\nBALLOON ");
foreach (var f in typeof(GameJam.Core.BalloonConfig).GetFields()) { var v = f.GetValue(bc); if (v is float || v is int || v is bool) sb.Append(f.Name).Append('=').Append(v).Append(' '); }
var balloon = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>("Assets/GameJam/Prefabs/Net/Balloon.prefab");
var sc = balloon.GetComponent<UnityEngine.SphereCollider>();
sb.Append("\nballoonRadius=").Append(sc.radius * balloon.transform.localScale.x);
return sb.ToString();
