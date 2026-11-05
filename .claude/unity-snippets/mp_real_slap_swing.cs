var input = UnityEngine.Object.FindFirstObjectByType<GameJam.Player.DesktopRigInput>();
typeof(GameJam.Player.DesktopRigInput).GetField("_slapTimer", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(input, 0.2f);
var b = GameJam.Net.NetworkBalloon.Instance;
return "swing; balloon at " + b.transform.position.ToString("F2") + " last=" + b.LastHitter.PlayerId;
