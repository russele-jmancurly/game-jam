// Play mode, on the instance that owns items and the balloon: set off an item effect at a spot and put the balloon in it.
// Effects run while a held item's trigger is pressed, so this holds the item in a stand-in hand at the spot and presses it.
// MODE: "wand" = bubble wand on the low tree branch under the tree hoop (6); "blower" = leaf blower 5 m from the laundry
// hoop (2), aimed at it;
// "aim" = an assisted hit from 4 m beside the playground hoop (4), using the same aim the items use.
string mode = "MODE";
var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
GameJam.Net.NetworkItem Find(string displayName)
{
    foreach (var it in UnityEngine.Object.FindObjectsByType<GameJam.Net.NetworkItem>(UnityEngine.FindObjectsSortMode.None))
        if (it.Definition.DisplayName == displayName && it.HasStateAuthority && !it.IsHeld) return it;
    return null;
}
void SetOff(GameJam.Net.NetworkItem item, UnityEngine.Vector3 at, UnityEngine.Vector3 aim)
{
    var hand = new UnityEngine.GameObject("TestHand") { hideFlags = UnityEngine.HideFlags.DontSave }.transform;
    hand.SetPositionAndRotation(at, UnityEngine.Quaternion.LookRotation(aim, UnityEngine.Vector3.up));
    item.Grab(hand, null);
    item.Use(true); // trigger held: the effect runs until released or EffectSeconds pass
}
var balloon = GameJam.Net.NetworkBalloon.Instance;
var tree = GameJam.Editor.BackyardCourseBuilder.TreeCenter;
if (mode == "wand")
{
    var wand = Find("Bubble Wand");
    if (wand == null) return "no bubble wand I own";
    SetOff(wand, tree + new UnityEngine.Vector3(2.8f, 4.4f, -0.3f), UnityEngine.Vector3.forward);
    balloon.ResetTo(tree + new UnityEngine.Vector3(2.8f, 4.75f, 0f));
    return "wand set off on the branch; balloon placed above it";
}
if (mode == "blower")
{
    var blower = Find("Leaf Blower");
    if (blower == null) return "no leaf blower I own";
    var hoop = UnityEngine.GameObject.Find("Hoop2").transform.position;
    var from = new UnityEngine.Vector3(hoop.x, 1.2f, hoop.z - 5f);
    SetOff(blower, from, (hoop - from).normalized);
    balloon.ResetTo(new UnityEngine.Vector3(hoop.x, 1.4f, hoop.z - 4f));
    return "blower set off 5 m from hoop 2 (" + hoop.ToString("F1") + "); balloon 1 m into the gust";
}
var h4 = UnityEngine.GameObject.Find("Hoop4").transform.position;
var start = new UnityEngine.Vector3(h4.x - 4f, 1.0f, h4.z - 1f);
balloon.ResetTo(start);
var aimed = GameJam.Gameplay.HoopAim.HitVelocityThrough(start, h4, balloon.Config);
balloon.RequestHit(UnityEngine.Object.FindFirstObjectByType<Fusion.NetworkRunner>().LocalPlayer, aimed);
return "aimed hit " + aimed.ToString("F2") + " from " + start.ToString("F1") + " at hoop 4 " + h4.ToString("F1");
