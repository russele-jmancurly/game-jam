// 2-player test from a NON-authority player: put the balloon in front of this player's right glove via the networked
// reset RPC, then trigger the PC-mode slap so the real SlapHand sweep sends the hit to the balloon's owner.
var rig = GameJam.Player.LocalRig.Instance;
var head = rig.Head;
var yaw = UnityEngine.Quaternion.Euler(0f, head.eulerAngles.y, 0f);
var spot = head.position + yaw * new UnityEngine.Vector3(0.2f, -0.2f, 0.55f);
GameJam.Net.NetworkBalloon.Instance.ResetTo(spot);
return "balloon -> " + spot.ToString("F2") + " glove=" + rig.RightHandFollower.position.ToString("F2");
