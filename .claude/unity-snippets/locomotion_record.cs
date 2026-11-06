// Play mode with a headset: records gorilla locomotion for SECONDS s (per frame: real controller and head positions,
// planted hand points, contacts, rig root, rigidbody velocity) so locomotion_report.cs can say why the player can't move.
var loco = GorillaLocomotion.Player.Instance;
var domain = System.AppDomain.CurrentDomain;
if (domain.GetData("locoRecorder") is UnityEditor.EditorApplication.CallbackFunction old) UnityEditor.EditorApplication.update -= old;
var F = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
var fL = typeof(GorillaLocomotion.Player).GetField("leftHandColliding", F);
var fR = typeof(GorillaLocomotion.Player).GetField("rightHandColliding", F);
var rows = new System.Collections.Generic.List<string>();
domain.SetData("locoRows", rows);
float start = UnityEngine.Time.realtimeSinceStartup;
int lastFrame = -1;
UnityEditor.EditorApplication.CallbackFunction tick = null;
tick = () =>
{
    if (!UnityEngine.Application.isPlaying || loco == null) { UnityEditor.EditorApplication.update -= tick; return; }
    if (UnityEngine.Time.frameCount == lastFrame) return;
    lastFrame = UnityEngine.Time.frameCount;
    float t = UnityEngine.Time.realtimeSinceStartup - start;
    var rb = loco.GetComponent<UnityEngine.Rigidbody>();
    string V(UnityEngine.Vector3 v) => v.x.ToString("F3") + "," + v.y.ToString("F3") + "," + v.z.ToString("F3");
    rows.Add(t.ToString("F3") + ";" + V(loco.transform.position) + ";" + V(loco.headCollider.transform.position) + ";" + V(loco.leftController.position) + ";" + V(loco.rightController.position)
        + ";" + V(loco.leftFollower.position) + ";" + V(loco.rightFollower.position) + ";" + fL.GetValue(loco) + ";" + fR.GetValue(loco) + ";" + V(rb.linearVelocity)
        + ";" + loco.locomotionEnabledLayers.value + ";" + loco.disableMovement + ";" + loco.bodyCollider.gameObject.activeSelf);
    if (t > SECONDS) { UnityEditor.EditorApplication.update -= tick; rows.Add("done"); }
};
domain.SetData("locoRecorder", tick);
UnityEditor.EditorApplication.update += tick;
return "recording locomotion for SECONDS s";
