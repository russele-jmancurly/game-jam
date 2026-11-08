// Lobby check (Play mode, PC test mode, offline). Logs "[LobbyTest]" lines, then "[LobbyTest] DONE".
//   1. lobby: clear walls and the start sign are up, the balloon is hidden and can't be hit
//   2. walking (stick walk) and big jumps toward each stage edge never leave the stage
//   3. RequestStart (the red button): walls and sign go, the balloon shows on its station, frozen through the countdown
//   4. once running, the balloon takes hits
//   unity command script eval --project-path "D:\Unity\game-jam" -- --file .claude/unity-snippets/lobby_check.cs
using UnityEngine;

var rig = GameJam.Player.LocalRig.Instance;
var p = Object.FindFirstObjectByType<GorillaLocomotion.Player>();
var desk = rig.GetComponent<GameJam.Player.DesktopRigInput>();
var state = GameJam.Net.GameState.Instance;
var balloon = GameJam.Net.NetworkBalloon.Instance;
var lobby = Object.FindFirstObjectByType<GameJam.Gameplay.LobbyOnly>();
GameObject walls = GameObject.Find("LobbyWalls"), sign = GameObject.Find("LobbyStartSign");
System.Action<string, bool, string> report = (name, pass, detail) => Debug.Log($"[LobbyTest] {(pass ? "PASS" : "FAIL")} {name}: {detail}");
if (state == null || balloon == null || lobby == null || walls == null) return "not ready: state/balloon/LobbyOnly/LobbyWalls missing";

// Stage top area: x -6..6, z -10.5..-1.5 (BackyardCourseBuilder.BuildStartStage).
System.Func<Vector3, bool> onStage = v => v.x > -6.3f && v.x < 6.3f && v.z > -10.8f && v.z < -1.2f;
System.Func<bool> balloonShown = () => balloon.GetComponentInChildren<Renderer>(true).enabled;

report("lobby: walls, sign, hidden balloon",
    state.State == GameJam.Core.RunState.Lobby && walls.activeInHierarchy && sign.activeInHierarchy && !balloonShown(),
    $"state={state.State} walls={walls.activeInHierarchy} sign={sign.activeInHierarchy} balloon shown={balloonShown()}");

desk.enabled = false;
rig.Head.localPosition = new Vector3(0f, 1.2f, 0f);
rig.Head.localRotation = Quaternion.identity;
rig.LeftController.localPosition = new Vector3(-0.25f, 0.85f, 0.3f);
rig.RightController.localPosition = new Vector3(0.25f, 0.85f, 0.3f);

// Escape attempts: for each direction, respawn at the stage centre, face it, walk with the stick for 4 s and jump hard
// at the wall twice on the way.
float[] yaws = { 0f, 90f, 180f, 270f };
int attempt = 0, frame = 0, escapes = 0;
float worst = 0f;
string log = "";
UnityEngine.Events.UnityAction tick = null;
tick = () =>
{
    if (attempt < yaws.Length)
    {
        if (frame == 0)
        {
            rig.Turn(yaws[attempt] - rig.TrackingSpace.eulerAngles.y);
            rig.RespawnAt(new Vector3(0f, 0.4f, -6f));
        }

        frame++;
        if (frame > 30) rig.StickWalk(new Vector2(0f, 1f));
        if (frame == 150 || frame == 230)
        {
            Vector3 v = rig.TrackingSpace.forward * 4f + Vector3.up * 7.5f; // the hardest gorilla launch, straight at the wall
            rig.Body.linearVelocity = v;
        }

        Vector3 head = rig.Head.position;
        if (!onStage(head))
        {
            escapes++;
            log += $" {yaws[attempt]}deg->{head.ToString("F1")}";
            frame = 300;
        }

        worst = Mathf.Max(worst, head.y);
        if (frame >= 300)
        {
            attempt++;
            frame = 0;
        }

        return;
    }

    if (frame == 0)
    {
        report("walls keep the crew on the stage (stick walk + 7.5 m/s jumps, 4 directions)", escapes == 0,
            $"escapes={escapes}{log}, highest head {worst:F1} m");
        rig.Turn(-rig.TrackingSpace.eulerAngles.y);
        rig.RespawnAtCheckpoint();
        state.RequestStart();
    }

    frame++;
    if (frame == 30)
    {
        Vector3 offStation = balloon.transform.position - GameJam.Core.CourseLayout.BalloonSpawn(0);
        report("red button: walls and sign gone, balloon shows on its station during the countdown",
            state.State == GameJam.Core.RunState.Countdown && !walls.activeInHierarchy && !sign.activeInHierarchy && balloonShown() && offStation.magnitude < 0.3f,
            $"state={state.State} walls={walls.activeInHierarchy} sign={sign.activeInHierarchy} balloon shown={balloonShown()} off station {offStation.magnitude:F2} m");
    }

    if (state.State == GameJam.Core.RunState.Running && frame < 100000)
    {
        frame = 100000;
        balloon.RequestHit(Fusion.PlayerRef.None, Vector3.up * 2f);
    }

    if (frame == 100060)
    {
        report("run started: balloon live", !balloon.IsStationed && balloon.Velocity.sqrMagnitude > 0.01f,
            $"state={state.State} stationed={balloon.IsStationed} speed={balloon.Velocity.magnitude:F2} m/s");
        Application.onBeforeRender -= tick;
        desk.enabled = true;
        Debug.Log("[LobbyTest] DONE");
    }
};
Application.onBeforeRender += tick;
return "LobbyTest started";
