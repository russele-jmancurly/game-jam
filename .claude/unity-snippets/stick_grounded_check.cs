// Play mode (PC test mode is fine). Checks that thumbstick walking only works on the ground: holds the stick forward
// (LocalRig.StickWalk, once a frame) for 1 s standing, then launches the rig straight up and holds it again in the air,
// then once more after landing. Run once to start, again after ~5 s for the report: metres moved sideways in each phase
// and how often IsGrounded was true.
var domain = System.AppDomain.CurrentDomain;
if (domain.GetData("stickLog") is System.Collections.Generic.List<string> done && domain.GetData("stickDone") is bool finished && finished)
{
    domain.SetData("stickLog", null);
    domain.SetData("stickDone", null);
    return string.Join("\n", done);
}

var rig = GameJam.Player.LocalRig.Instance;
var body = rig.GetComponent<UnityEngine.Rigidbody>();
var log = new System.Collections.Generic.List<string>();
domain.SetData("stickLog", log);
domain.SetData("stickDone", false);
float start = UnityEngine.Time.time;
int lastFrame = -1;
string phase = "";
UnityEngine.Vector3 phaseStart = UnityEngine.Vector3.zero;
int frames = 0, grounded = 0;
bool launched = false;

void EndPhase(string name)
{
    var p = rig.transform.position;
    float sideways = new UnityEngine.Vector2(p.x - phaseStart.x, p.z - phaseStart.z).magnitude;
    log.Add($"{name}: moved {sideways:F2} m sideways in {frames} frames, grounded {grounded}/{frames}, height now {p.y:F2}");
}

UnityEditor.EditorApplication.CallbackFunction tick = null;
tick = () =>
{
    if (!UnityEngine.Application.isPlaying) { UnityEditor.EditorApplication.update -= tick; return; }
    if (UnityEngine.Time.frameCount == lastFrame) return;
    lastFrame = UnityEngine.Time.frameCount;
    float t = UnityEngine.Time.time - start;
    string now = t < 1f ? "standing" : t < 1.1f ? "launch" : t < 2.2f ? "in the air" : t < 3.5f ? "landing" : t < 4.5f ? "landed" : "done";
    if (now != phase)
    {
        if (phase == "standing" || phase == "in the air" || phase == "landed") EndPhase(phase);
        phase = now;
        phaseStart = rig.transform.position;
        frames = 0; grounded = 0;
        if (now == "done") { domain.SetData("stickDone", true); UnityEditor.EditorApplication.update -= tick; return; }
    }

    if (now == "launch" && !launched)
    {
        launched = true;
        body.linearVelocity = new UnityEngine.Vector3(0f, 6f, 0f); // straight up: any sideways motion in the air is the stick
        return;
    }

    if (now == "standing" || now == "in the air" || now == "landed")
    {
        rig.StickWalk(UnityEngine.Vector2.up);
        frames++;
        if (rig.IsGrounded) grounded++;
    }
};
UnityEditor.EditorApplication.update += tick;
return "running: standing 1 s, launch, air 1.1 s, land, walk 1 s";
