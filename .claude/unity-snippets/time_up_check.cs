// Time-up flow check (Play mode, offline). Shortens the run to 5 s for the test (restored at the end), starts a run, moves
// an item off its spot, and checks: when time runs out the referee whistle plays and the TIME'S UP banner shows (a DontSave
// camera "BannerCam" on the head is placed for a screenshot); after RunConfig.ResultSeconds everyone is back in the lobby,
// the banner is gone and the item is back on its spot. Logs "[TimeUpTest]" lines, then DONE.
//   unity command script eval --project-path "D:\Unity\game-jam" -- --file .claude/unity-snippets/time_up_check.cs
using System.Reflection;
using UnityEngine;

var state = GameJam.Net.GameState.Instance;
var rig = GameJam.Player.LocalRig.Instance;
var banner = Object.FindFirstObjectByType<GameJam.Net.ResultBanner>();
if (state == null || rig == null || banner == null) return "not ready";
System.Action<string, bool, string> report = (name, pass, detail) => Debug.Log($"[TimeUpTest] {(pass ? "PASS" : "FAIL")} {name}: {detail}");
var config = (GameJam.Core.RunConfig)typeof(GameJam.Net.GameState).GetField("_config", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(state);
var whistle = (AudioSource)typeof(GameJam.Net.GameState).GetField("_timeUpAudio", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(state);
float runWas = config.RunSeconds;
config.RunSeconds = 5f;

GameJam.Net.NetworkItem item = null;
foreach (var candidate in Object.FindObjectsByType<GameJam.Net.NetworkItem>(FindObjectsSortMode.None))
{
    if (candidate.transform.position.z > 0f) { item = candidate; break; }
}

var homeProperty = typeof(GameJam.Net.NetworkItem).GetProperty("HomePosition", BindingFlags.NonPublic | BindingFlags.Instance);
Vector3 home = (Vector3)homeProperty.GetValue(item);
bool movedItem = false, sawTimeUp = false, sawWhistle = false, reported = false;
float timeUpAt = 0f;
UnityEngine.Events.UnityAction tick = null;
state.RequestStart();
tick = () =>
{
    if (state.State == GameJam.Core.RunState.Running && !movedItem)
    {
        movedItem = true;
        var networkTransform = item.GetComponent<Fusion.NetworkTransform>();
        networkTransform.Teleport(home + new Vector3(3f, 1f, 3f), Quaternion.identity); // knocked off its spot
    }

    if (state.IsTimeUp && !sawTimeUp)
    {
        sawTimeUp = true;
        timeUpAt = Time.time;
    }

    if (sawTimeUp && whistle != null && whistle.isPlaying) sawWhistle = true;

    if (sawTimeUp && Time.time - timeUpAt > 1f && GameObject.Find("BannerCam") == null && !reported)
    {
        reported = true;
        report("time up: whistle and banner", sawWhistle && banner.IsVisible,
            $"whistle played={sawWhistle} banner visible={banner.IsVisible} to lobby in {state.SecondsToLobby:F1} s");
        var cam = new GameObject("BannerCam") { hideFlags = HideFlags.DontSave }.AddComponent<Camera>();
        cam.targetDisplay = 7;
        cam.fieldOfView = 70f;
        cam.nearClipPlane = 0.02f;
        cam.transform.SetPositionAndRotation(rig.Head.position, rig.Head.rotation);
        Debug.Log("[TimeUpTest] INFO BannerCam placed: capture it now");
    }

    if (sawTimeUp && state.State == GameJam.Core.RunState.Lobby && Time.time - timeUpAt > config.ResultSeconds + 1.5f)
    {
        Vector3 off = item.transform.position - home;
        float offSpot = new Vector2(off.x, off.z).magnitude; // items drop from the spot (0.6 m up) onto the ground
        report("back to the lobby, banner gone, item on its spot", !banner.IsVisible && offSpot < 0.3f,
            $"state={state.State} banner visible={banner.IsVisible} item {offSpot:F2} m from its spot");
        config.RunSeconds = runWas;
        var cam = GameObject.Find("BannerCam");
        if (cam != null) Object.Destroy(cam);
        Application.onBeforeRender -= tick;
        Debug.Log($"[TimeUpTest] DONE (RunSeconds restored to {config.RunSeconds})");
    }
};
Application.onBeforeRender += tick;
return $"TimeUpTest started (item {item.name}, home {home})";
