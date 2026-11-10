// Play mode: where the tutorial trip stands. Scene, session, practice mode, balloon, items, giants, fade, lessons.
// Use with tutorial_dive.cs (puts the player in the scene's mole hole) to test Lobby -> Tunnel -> Tutorial -> Tunnel -> Lobby.
var sb = new System.Text.StringBuilder();
sb.Append("scene=").Append(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
sb.Append(" frame=").Append(UnityEngine.Time.frameCount);
sb.Append(" travelling=").Append(GameJam.Gameplay.SceneTravel.IsTravelling);
sb.Append(" dest=").Append(GameJam.Gameplay.SceneTravel.Destination);
var runners = UnityEngine.Object.FindObjectsByType<Fusion.NetworkRunner>(UnityEngine.FindObjectsSortMode.None);
sb.Append(" runners=").Append(runners.Length);
foreach (var r in runners) { sb.Append("[").Append(r.IsRunning ? r.GameMode.ToString() : "stopped").Append("]"); }
sb.Append(" rig=").Append(GameJam.Player.LocalRig.Instance != null);
if (GameJam.Player.LocalRig.Instance != null) { sb.Append("@").Append(GameJam.Player.LocalRig.Instance.Head.position.ToString("F1")); }
var gs = GameJam.Net.GameState.Instance;
sb.Append(" practice=").Append(GameJam.Net.GameState.IsPractice);
sb.Append(" state=").Append(gs != null ? gs.State.ToString() : "none");
if (gs != null) { sb.Append(" hoops=").Append(gs.HoopsCleared).Append("/").Append(gs.HoopCount).Append(" wrist='").Append(gs.WristText.Replace("\n", " ")).Append("'"); }
var balloon = GameJam.Net.NetworkBalloon.Instance;
sb.Append(" balloon=").Append(balloon != null ? balloon.transform.position.ToString("F1") + " hits=" + balloon.Hits + " pops=" + balloon.PopCount + " stationed=" + balloon.IsStationed : "none");
sb.Append(" items=").Append(UnityEngine.Object.FindObjectsByType<GameJam.Net.NetworkItem>(UnityEngine.FindObjectsSortMode.None).Length);
sb.Append(" giants=").Append(GameJam.Net.MischiefMaker.Active.Count);
foreach (var g in GameJam.Net.MischiefMaker.Active) { if (g != null) sb.Append("[").Append(g.DisplayName).Append(" ").Append(g.Mood).Append(" @").Append(g.transform.position.ToString("F1")).Append("]"); }
var fade = GameJam.Gameplay.ScreenFade.Instance;
sb.Append(" fade=").Append(fade != null ? fade.Alpha.ToString("F2") : "none");
var holes = UnityEngine.Object.FindObjectsByType<GameJam.Gameplay.MoleHole>(UnityEngine.FindObjectsInactive.Include, UnityEngine.FindObjectsSortMode.None);
foreach (var h in holes) { sb.Append(" hole->").Append(h.Destination).Append(h.gameObject.activeInHierarchy ? "(on)" : "(off)").Append("@").Append(h.transform.position.ToString("F1")); }
var guide = UnityEngine.Object.FindFirstObjectByType<GameJam.Net.TutorialGuide>();
if (guide != null)
{
    sb.Append(" lessons=").Append(guide.LessonsDone).Append("/5 [");
    for (int i = 0; i < 5; i++) { sb.Append(guide.IsDone(i) ? "x" : "-"); }
    sb.Append("]");
    foreach (var t in guide.GetComponentsInChildren<UnityEngine.TextMesh>(true)) { }
}
return sb.ToString();
