// Play mode (PC test mode, on the instance that owns the items). Checks two item rules and logs "[HeldPushTest] ..." lines:
//  1. Solid effect items: the leaf blower / bubble wand collide with your own body while loose, not while held or for
//     0.5 s after letting go; a plain item (beach ball) never does.
//  2. Held-item push: with the beach ball in the right hand, bringing the left hand onto it slowly does nothing, hitting
//     it fast pushes the body (KnockbackConfig.HeldItemPushForce).
// Run, wait ~6 s, then read the console: unity command console get -- --tail 30 (look for "[HeldPushTest] DONE").
var rig = GameJam.Player.LocalRig.Instance;
if (rig == null) return "no LocalRig (enter Play mode in Prototype first)";
var desktop = rig.GetComponent<GameJam.Player.DesktopRigInput>();
var locomotion = rig.LocomotionRoot.GetComponent<GorillaLocomotion.Player>();
UnityEngine.Collider bodyCollider = locomotion.bodyCollider;

GameJam.Net.NetworkItem Find(string assetWord)
{
    foreach (var it in UnityEngine.Object.FindObjectsByType<GameJam.Net.NetworkItem>(UnityEngine.FindObjectsSortMode.None))
        if (it.Definition.name.Contains(assetWord) && it.HasStateAuthority && !it.IsHeld && !it.IsSpent) return it;
    return null;
}
bool Ignored(GameJam.Net.NetworkItem it) => UnityEngine.Physics.GetIgnoreCollision(it.GetComponent<UnityEngine.Collider>(), bodyCollider);
void Log(string text) => UnityEngine.Debug.Log("[HeldPushTest] " + text);

System.Collections.IEnumerator Run()
{
    var ball = Find("BeachBall");
    var blower = Find("LeafBlower");
    var wand = Find("BubbleWand");
    if (ball == null || blower == null || wand == null) { Log("FAIL missing item: ball " + (ball != null) + " blower " + (blower != null) + " wand " + (wand != null)); yield break; }
    yield return null;

    // 1. Solid effect items.
    bool layersCollide = !UnityEngine.Physics.GetIgnoreLayerCollision(GameJam.Core.GameLayers.Item, GameJam.Core.GameLayers.IgnoreRaycast);
    Log((layersCollide && !Ignored(blower) && !Ignored(wand) && Ignored(ball) ? "PASS" : "FAIL") + " loose: layers collide " + layersCollide
        + ", blower ignored " + Ignored(blower) + ", wand ignored " + Ignored(wand) + ", ball ignored " + Ignored(ball));
    var stand = new UnityEngine.GameObject("TestHand") { hideFlags = UnityEngine.HideFlags.DontSave }.transform;
    stand.position = blower.transform.position + UnityEngine.Vector3.up * 0.5f;
    blower.Grab(stand, null);
    yield return null; yield return null;
    Log((Ignored(blower) ? "PASS" : "FAIL") + " held blower ignores the body: " + Ignored(blower));
    blower.Release(UnityEngine.Vector3.zero);
    yield return null; yield return null;
    bool justReleased = Ignored(blower);
    yield return new UnityEngine.WaitForSeconds(0.8f);
    Log((justReleased && !Ignored(blower) ? "PASS" : "FAIL") + " let go: ignored at once " + justReleased + ", solid again after 0.8 s " + !Ignored(blower));
    UnityEngine.Object.Destroy(stand.gameObject);

    // 2. Held-item push. Desktop input off, so it doesn't release the grip or move the controllers.
    desktop.enabled = false;
    UnityEngine.Transform right = rig.RightHandFollower;
    ball.GetComponent<UnityEngine.Rigidbody>().position = right.position;
    ball.transform.position = right.position;
    UnityEngine.Physics.SyncTransforms();
    rig.SetGrip(true, true);
    yield return null; yield return null;
    bool holding = ball.IsHeldBy(right);
    Log((holding ? "PASS" : "FAIL") + " ball in the right hand: " + holding);

    UnityEngine.Collider ballCollider = ball.GetComponent<UnityEngine.Collider>();
    UnityEngine.Transform left = rig.LeftController;
    float[] speeds = { 0.2f, 2.5f };
    foreach (float speed in speeds)
    {
        UnityEngine.Vector3 side = -rig.Head.right; side.y = 0f; side.Normalize();
        left.position = ballCollider.bounds.center + side * (ballCollider.bounds.extents.x + 0.3f);
        yield return new UnityEngine.WaitForSeconds(0.5f); // hand settles, body at rest
        float maxBodySpeed = 0f;
        float until = UnityEngine.Time.time + 0.3f / speed + 0.4f;
        while (UnityEngine.Time.time < until)
        {
            left.position = UnityEngine.Vector3.MoveTowards(left.position, ballCollider.bounds.center, speed * UnityEngine.Time.deltaTime);
            maxBodySpeed = UnityEngine.Mathf.Max(maxBodySpeed, rig.Body.linearVelocity.magnitude);
            yield return null;
        }
        bool pushed = maxBodySpeed > rig.KnockbackConfig.HeldItemPushForce * 0.8f;
        bool expected = speed > rig.KnockbackConfig.HeldItemPushMinSpeed;
        Log((pushed == expected ? "PASS" : "FAIL") + " left hand onto the held ball at " + speed + " m/s: max body speed " + maxBodySpeed.ToString("F2")
            + " (push expected: " + expected + "), still holding " + ball.IsHeldBy(right) + ", knocked down " + rig.IsKnockedDown);
    }

    rig.SetGrip(true, false);
    desktop.enabled = true;
    Log("DONE");
}
rig.StartCoroutine(Run());
return "started; read the console for [HeldPushTest] lines in ~6 s";
