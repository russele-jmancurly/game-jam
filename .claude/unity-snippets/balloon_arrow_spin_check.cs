// Play mode, in a scene where the balloon is shown and stationed (Tutorial, or Prototype during a run). Throws a
// beach ball at the top of the balloon, where the finder arrow sits, and logs "[ArrowSpinTest] ..." lines: the balloon
// body and its arrow must stay upright (Rigidbody rotation frozen, arrow held upright by BalloonLocator).
// Read the console ~3 s later (look for "[ArrowSpinTest] DONE").
var balloon = GameJam.Net.NetworkBalloon.Instance;
if (balloon == null) return "no balloon (enter Play mode first)";
var rig = GameJam.Player.LocalRig.Instance;
void Log(string text) => UnityEngine.Debug.Log("[ArrowSpinTest] " + text);
GameJam.Net.NetworkItem ball = null;
foreach (var it in UnityEngine.Object.FindObjectsByType<GameJam.Net.NetworkItem>(UnityEngine.FindObjectsSortMode.None))
    if (it.HasStateAuthority && !it.IsHeld && !it.IsSpent) { ball = it; if (it.Definition.name.Contains("BeachBall")) break; }
if (ball == null) return "no item to throw";
var body = balloon.GetComponent<UnityEngine.Rigidbody>();
UnityEngine.Transform beacon = balloon.transform.Find("View/Beacon");
System.Collections.IEnumerator Run()
{
    Log("constraints " + body.constraints + ", stationed " + balloon.IsStationed + ", beacon active " + (beacon != null && beacon.gameObject.activeInHierarchy));
    var itemBody = ball.GetComponent<UnityEngine.Rigidbody>();
    float radius = balloon.GetComponent<UnityEngine.SphereCollider>().radius;
    UnityEngine.Vector3 top = balloon.transform.position + UnityEngine.Vector3.up * (radius * 0.9f);
    UnityEngine.Vector3 from = top + UnityEngine.Vector3.right * 2f + UnityEngine.Vector3.up * 0.3f;
    itemBody.position = from; ball.transform.position = from;
    itemBody.linearVelocity = (top - from).normalized * 9f;
    itemBody.angularVelocity = UnityEngine.Vector3.zero;
    UnityEngine.Physics.SyncTransforms();
    float worstBody = 0f, worstBeacon = 0f;
    float until = UnityEngine.Time.time + 2f;
    while (UnityEngine.Time.time < until)
    {
        worstBody = UnityEngine.Mathf.Max(worstBody, UnityEngine.Quaternion.Angle(body.rotation, UnityEngine.Quaternion.identity));
        if (beacon != null && beacon.gameObject.activeInHierarchy)
            worstBeacon = UnityEngine.Mathf.Max(worstBeacon, UnityEngine.Quaternion.Angle(beacon.rotation, UnityEngine.Quaternion.identity));
        yield return null;
    }
    Log((worstBody < 0.5f && worstBeacon < 0.5f ? "PASS" : "FAIL") + " after a throw at the top: body tilted up to " + worstBody.ToString("F1")
        + " deg, arrow tilted up to " + worstBeacon.ToString("F1") + " deg");
    Log("DONE");
}
(rig != null ? (UnityEngine.MonoBehaviour)rig : balloon).StartCoroutine(Run());
return "started";
