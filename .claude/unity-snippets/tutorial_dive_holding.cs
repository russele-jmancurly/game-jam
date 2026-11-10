// Play mode, a scene with a mole hole (lobby or tutorial). Checks that a player who dives into the hole while holding an
// item lets go of it first, while still in the session (an item whose holder has left would stay "held" for the others):
// puts the nearest plain item in the right hand through the real grip (LocalRig.SetGrip), moves the player over the
// hole, and logs the item's holder each frame until the scene changes.
// Read: script eval --code 'return string.Join("\n", (System.Collections.Generic.List<string>)System.AppDomain.CurrentDomain.GetData("tutorialLog"));'
var rig = GameJam.Player.LocalRig.Instance;
if (rig == null) { return "no LocalRig"; }
GameJam.Gameplay.MoleHole hole = null;
foreach (var h in UnityEngine.Object.FindObjectsByType<GameJam.Gameplay.MoleHole>(UnityEngine.FindObjectsSortMode.None)) { hole = h; }
if (hole == null) { return "no active mole hole"; }
var defField = typeof(GameJam.Net.NetworkItem).GetField("_definition", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
GameJam.Net.NetworkItem item = null;
float best = float.MaxValue;
foreach (var candidate in UnityEngine.Object.FindObjectsByType<GameJam.Net.NetworkItem>(UnityEngine.FindObjectsSortMode.None))
{
    var def = (GameJam.Core.ItemDefinition)defField.GetValue(candidate);
    if (def == null || def.Effect != GameJam.Core.ItemEffect.None || candidate.IsSpent || candidate.IsHeld) { continue; }
    float d = (candidate.transform.position - rig.Head.position).sqrMagnitude;
    if (d < best) { best = d; item = candidate; }
}
if (item == null) { return "no item to hold"; }

var log = new System.Collections.Generic.List<string>();
System.AppDomain.CurrentDomain.SetData("tutorialLog", log);
System.Collections.IEnumerator Run()
{
    var hand = rig.RightHandFollower;
    var slapHand = hand.GetComponent<GameJam.Player.SlapHand>();
    // PC test input feeds the grip every frame (G not held = let go), which would undo the scripted grab.
    var desktop = rig.GetComponent<GameJam.Player.DesktopRigInput>();
    if (desktop != null) { desktop.enabled = false; }
    // A grab happens on the press, so let go and press again until the item (teleported to the hand) is in reach.
    for (int i = 0; i < 30 && !rig.IsHolding(slapHand); i++)
    {
        rig.SetGrip(true, false);
        item.GetComponent<Fusion.NetworkTransform>().Teleport(hand.position, UnityEngine.Quaternion.identity);
        item.transform.position = hand.position;
        yield return new UnityEngine.WaitForFixedUpdate();
        yield return null;
        rig.SetGrip(true, true);
        yield return null;
    }
    log.Add("holding through the grip: " + rig.IsHolding(slapHand) + " item=" + item.name + " holder=" + item.Holder);
    if (!rig.IsHolding(slapHand)) { log.Add("could not grab: test not run"); yield break; }
    rig.RespawnAt(hole.transform.position + UnityEngine.Vector3.up * 0.05f);
    for (int i = 0; i < 40; i++)
    {
        yield return null;
        if (item == null) { log.Add("frame " + i + ": item gone (scene changed)"); yield break; }
        if (i < 3 || i % 10 == 0) { log.Add("frame " + i + ": travelling=" + GameJam.Gameplay.SceneTravel.IsTravelling + " rig holding=" + rig.IsHolding(slapHand) + " item.IsHeld=" + item.IsHeld + " holder=" + item.Holder); }
    }
}
rig.StartCoroutine(Run());
return "started";
