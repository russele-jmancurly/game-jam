// Play mode (PC test mode, the instance that owns the items). Measures what a solid effect item (leaf blower, bubble
// wand) does to your body on real contact, and logs "[SolidItemTest] ..." lines:
//   1. the item dropped inside your body (the respawn-on-top / walked-into case): peak body speed
//   2. the item flying into your body at 6 m/s: peak body speed
// Both should stay a bump: no faster than about KnockbackConfig.PlayerPushForce + 1 m/s, and never a knockdown.
// Read the console ~8 s later (look for "[SolidItemTest] DONE").
var rig = GameJam.Player.LocalRig.Instance;
if (rig == null) return "no LocalRig (enter Play mode in Prototype first)";
void Log(string text) => UnityEngine.Debug.Log("[SolidItemTest] " + text);
GameJam.Net.NetworkItem Find(string assetWord)
{
    foreach (var it in UnityEngine.Object.FindObjectsByType<GameJam.Net.NetworkItem>(UnityEngine.FindObjectsSortMode.None))
        if (it.Definition.name.Contains(assetWord) && it.HasStateAuthority && !it.IsHeld && !it.IsSpent) return it;
    return null;
}
System.Collections.IEnumerator Run()
{
    float limit = rig.KnockbackConfig.PlayerPushForce + 1f;
    foreach (string word in new[] { "LeafBlower", "BubbleWand" })
    {
        var item = Find(word);
        if (item == null) { Log("FAIL no " + word); continue; }
        var body = item.GetComponent<UnityEngine.Rigidbody>();
        UnityEngine.Vector3 home = item.transform.position;
        for (int mode = 0; mode < 2; mode++)
        {
            rig.RespawnAtCheckpoint();
            yield return new UnityEngine.WaitForSeconds(1.2f); // land and settle
            UnityEngine.Vector3 center = rig.BodyCenter;
            UnityEngine.Vector3 from = mode == 0 ? center : center + rig.Head.forward * 2f;
            body.position = from;
            item.transform.position = from;
            body.linearVelocity = mode == 0 ? UnityEngine.Vector3.zero : (center - from).normalized * 6f;
            body.angularVelocity = UnityEngine.Vector3.zero;
            UnityEngine.Physics.SyncTransforms();
            float peak = 0f;
            bool knocked = false;
            float until = UnityEngine.Time.time + 1.2f;
            while (UnityEngine.Time.time < until)
            {
                peak = UnityEngine.Mathf.Max(peak, rig.Body.linearVelocity.magnitude);
                knocked |= rig.IsKnockedDown;
                yield return null;
            }
            Log((peak <= limit && !knocked ? "PASS " : "FAIL ") + word + (mode == 0 ? " dropped inside the body" : " thrown into the body at 6 m/s")
                + ": peak body speed " + peak.ToString("F2") + " m/s (limit " + limit.ToString("F1") + "), knocked down " + knocked
                + ", item ended " + (item.transform.position - rig.BodyCenter).magnitude.ToString("F2") + " m away");
        }
        body.linearVelocity = UnityEngine.Vector3.zero;
        body.position = home;
        item.transform.position = home;
    }
    Log("DONE");
}
rig.StartCoroutine(Run());
return "started; read the console for [SolidItemTest] lines in ~8 s";
