// "Not your turn" icon check (Play mode, PC test mode). Starts a run, turns the solo pass-rule exemption off for the test,
// hits the balloon as the local player (icon must show), then as a stand-in player 2 (another player's hit:
// icon must hide), and restores BalloonConfig.AllowSoloDoubleHits. Places a DontSave camera "TurnCam" matching the
// head for a screenshot while the icon shows. Logs "[TurnTest]" lines, then "[TurnTest] DONE".
//   unity command script eval --project-path "D:\Unity\game-jam" -- --file .claude/unity-snippets/turn_indicator_check.cs
using UnityEngine;

var balloon = GameJam.Net.NetworkBalloon.Instance;
var state = GameJam.Net.GameState.Instance;
var indicator = Object.FindFirstObjectByType<GameJam.Net.TurnIndicator>();
var rig = GameJam.Player.LocalRig.Instance;
if (balloon == null || state == null || indicator == null || rig == null) return "not ready";
System.Action<string, bool, string> report = (name, pass, detail) => Debug.Log($"[TurnTest] {(pass ? "PASS" : "FAIL")} {name}: {detail}");
var config = balloon.Config;
bool soloWas = config.AllowSoloDoubleHits;
config.AllowSoloDoubleHits = false;
var runner = balloon.Runner;
int frame = 0;
UnityEngine.Events.UnityAction tick = null;
tick = () =>
{
    frame++;
    if (frame == 1)
    {
        report("hidden before any hit", !indicator.IsVisible, $"visible={indicator.IsVisible}");
        state.RequestStart();
    }

    if (frame > 1 && frame < 100000 && state.State == GameJam.Core.RunState.Running)
    {
        frame = 100000;
        balloon.RequestHit(runner.LocalPlayer, Vector3.up * 1.5f);
    }

    if (frame == 100090)
    {
        report("your hit: icon shows", indicator.IsVisible && balloon.IsNotLocalPlayersTurn,
            $"visible={indicator.IsVisible} notYourTurn={balloon.IsNotLocalPlayersTurn} lastHitter={balloon.LastHitter}");
        var cam = new GameObject("TurnCam") { hideFlags = HideFlags.DontSave }.AddComponent<Camera>();
        cam.targetDisplay = 7;
        cam.fieldOfView = rig.Head.GetComponent<Camera>().fieldOfView;
        cam.nearClipPlane = 0.02f;
        cam.transform.SetPositionAndRotation(rig.Head.position, rig.Head.rotation);
        Debug.Log("[TurnTest] INFO TurnCam placed: capture it now (icon visible for ~3 s)");
    }

    if (frame == 100600)
    {
        balloon.RequestHit(Fusion.PlayerRef.FromIndex(2), Vector3.up * 1.5f); // another player hits it (a giant's hit is nobody's and keeps the pass)
    }

    if (frame == 100700)
    {
        report("someone else hit it: icon hides", !indicator.IsVisible && !balloon.IsNotLocalPlayersTurn,
            $"visible={indicator.IsVisible} notYourTurn={balloon.IsNotLocalPlayersTurn}");
        config.AllowSoloDoubleHits = soloWas;
        var cam = GameObject.Find("TurnCam");
        if (cam != null) Object.Destroy(cam);
        Application.onBeforeRender -= tick;
        Debug.Log($"[TurnTest] DONE (AllowSoloDoubleHits restored to {config.AllowSoloDoubleHits})");
    }
};
Application.onBeforeRender += tick;
return "TurnTest started";
