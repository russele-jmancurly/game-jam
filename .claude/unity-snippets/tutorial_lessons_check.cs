// Play mode, tutorial scene (reach it with tutorial_dive.cs from the lobby, or open Tutorial.unity and press Play).
// Plays through the five sections with the real objects, as a coroutine on the TutorialGuide, logging each step:
//   gate 1 blocks the way while its lesson is open; 1 walks with the stick (LocalRig.StickWalk) until the lesson is done
//   and on through the opened gate; 2 hits the balloon three times as the local player and carries it through the hoop;
//   3 drops it on the cactus (a pop); 4 grabs the bubble wand and runs its effect until it is used up; 5 throws a beach
//   ball at the giant's chest (a knockdown). After each lesson: the section number, the gate, the checkpoint and where
//   the balloon went. Takes ~40 s.
// Read the log with: script eval --code 'return string.Join("\n", (System.Collections.Generic.List<string>)System.AppDomain.CurrentDomain.GetData("tutorialLog"));'
var guide = UnityEngine.Object.FindFirstObjectByType<GameJam.Net.TutorialGuide>();
if (guide == null) { return "no TutorialGuide: this is not the tutorial scene"; }
var rig = GameJam.Player.LocalRig.Instance;
var balloon = GameJam.Net.NetworkBalloon.Instance;
var state = GameJam.Net.GameState.Instance;
if (rig == null || balloon == null || state == null) { return "rig/balloon/game state not ready yet"; }

var log = new System.Collections.Generic.List<string>();
System.AppDomain.CurrentDomain.SetData("tutorialLog", log);
var gates = UnityEngine.Object.FindObjectsByType<GameJam.Gameplay.SectionGate>(UnityEngine.FindObjectsSortMode.None);
System.Array.Sort(gates, (a, b) => a.transform.position.z.CompareTo(b.transform.position.z));
string Where()
{
    var sb = new System.Text.StringBuilder();
    sb.Append("section=").Append(guide.Section).Append(" checkpoint=").Append(GameJam.Net.GameState.PracticeCheckpoint).Append(" gates=");
    foreach (var g in gates) { sb.Append(g.IsOpen ? "O" : "x"); }
    sb.Append(" balloon=").Append(balloon.transform.position.ToString("F1")).Append(" player z=").Append(rig.Head.position.z.ToString("F1"));
    return sb.ToString();
}
string Goal(int lesson)
{
    foreach (var t in UnityEngine.Object.FindObjectsByType<UnityEngine.TextMesh>(UnityEngine.FindObjectsSortMode.None))
    {
        if (t.name == "Lesson" + (lesson + 1) + (new[] { "_Move", "_Slap", "_Pop", "_Items", "_Giant" })[lesson] + "Goal") { return "'" + t.text + "'"; }
    }
    return "?";
}

System.Collections.IEnumerator Run()
{
    var defField = typeof(GameJam.Net.NetworkItem).GetField("_definition", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
    var balloonNt = balloon.GetComponent<Fusion.NetworkTransform>();
    log.Add("start: " + Where() + " goal1=" + Goal(0));

    // Gate 1 is shut: walking into it from 1.5 m away gets nowhere.
    rig.RespawnAt(new UnityEngine.Vector3(0f, 0f, 7f));
    yield return null;
    for (float t = 0f; t < 1.5f; t += UnityEngine.Time.deltaTime) { rig.StickWalk(new UnityEngine.Vector2(0f, 1f)); yield return null; }
    log.Add("gate 1 shut: player stopped at z=" + rig.Head.position.z.ToString("F2") + " (gate at 9) open=" + gates[0].IsOpen);
    rig.RespawnAt(new UnityEngine.Vector3(0f, 0f, -2f));
    yield return null;

    // 1. Move: walk north with the stick until the lesson is done, then on through the gate.
    for (float t = 0f; t < 10f && guide.Section == 0; t += UnityEngine.Time.deltaTime) { rig.StickWalk(new UnityEngine.Vector2(0f, 1f)); yield return null; }
    log.Add("1 move done: " + Where() + " goal1=" + Goal(0));
    for (float t = 0f; t < 8f && rig.Head.position.z < 11f; t += UnityEngine.Time.deltaTime) { rig.StickWalk(new UnityEngine.Vector2(0f, 1f)); yield return null; }
    log.Add("1 through gate 1: player z=" + rig.Head.position.z.ToString("F1") + " goal2=" + Goal(1));

    // 2. Slap: three hits as the local player, then carry the balloon through the hoop.
    for (int i = 0; i < 3; i++)
    {
        balloon.RequestHit(balloon.Runner.LocalPlayer, new UnityEngine.Vector3(0f, 2.5f, 0.5f));
        yield return new UnityEngine.WaitForSeconds(0.6f);
    }
    log.Add("2 slaps: goal2=" + Goal(1));
    balloonNt.Teleport(new UnityEngine.Vector3(0f, 2.4f, 19.2f), UnityEngine.Quaternion.identity);
    yield return new UnityEngine.WaitForSeconds(0.2f);
    balloon.RequestHit(balloon.Runner.LocalPlayer, new UnityEngine.Vector3(0f, 0.6f, 3f));
    for (float t = 0f; t < 3f && guide.Section == 1; t += UnityEngine.Time.deltaTime) { yield return null; }
    log.Add("2 hoop done: " + Where() + " goal2=" + Goal(1));
    yield return new UnityEngine.WaitForSeconds(2.2f);
    log.Add("2 -> 3 after the delay: " + Where() + " goal3=" + Goal(2));

    // 3. Pop: put it on the cactus spikes.
    rig.RespawnAt(GameJam.Core.CourseLayout.PlayerSpawn(2, 0));
    balloonNt.Teleport(new UnityEngine.Vector3(3.6f, 2.0f, 31.5f), UnityEngine.Quaternion.identity);
    yield return new UnityEngine.WaitForSeconds(0.2f);
    balloon.RequestHit(Fusion.PlayerRef.None, new UnityEngine.Vector3(0f, -2f, 0f));
    for (float t = 0f; t < 5f && guide.Section == 2; t += UnityEngine.Time.deltaTime) { yield return null; }
    log.Add("3 pop done: " + Where() + " goal3=" + Goal(2));
    yield return new UnityEngine.WaitForSeconds(2.2f);
    log.Add("3 -> 4 after the delay: " + Where() + " goal4=" + Goal(3));

    // 4. Items: grab the bubble wand and run its effect until it is used up.
    rig.RespawnAt(GameJam.Core.CourseLayout.PlayerSpawn(3, 0));
    GameJam.Net.NetworkItem wand = null, ball = null;
    foreach (var item in UnityEngine.Object.FindObjectsByType<GameJam.Net.NetworkItem>(UnityEngine.FindObjectsSortMode.None))
    {
        var def = (GameJam.Core.ItemDefinition)defField.GetValue(item);
        if (def.Effect == GameJam.Core.ItemEffect.Updraft) { wand = item; }
        else if (def.Effect == GameJam.Core.ItemEffect.None && def.DisplayName.Contains("Beach")) { ball = item; }
    }
    if (wand == null) { log.Add("4 items: no bubble wand found"); }
    else
    {
        var hand = rig.RightHandFollower;
        wand.Grab(hand, hand.GetComponent<GameJam.Player.SlapHand>());
        yield return new UnityEngine.WaitForSeconds(0.3f);
        log.Add("4 grab: held=" + wand.IsHeld + " goal4=" + Goal(3));
        wand.Use(true);
        for (float t = 0f; t < 9f && guide.Section == 3; t += UnityEngine.Time.deltaTime) { yield return null; }
        log.Add("4 use done: spent=" + wand.IsSpent + " " + Where() + " goal4=" + Goal(3));
    }
    yield return new UnityEngine.WaitForSeconds(2.2f);
    log.Add("4 -> 5 after the delay: " + Where() + " goal5=" + Goal(4));

    // 5. Giant: throw a beach ball at its chest from 2.5 m.
    rig.RespawnAt(GameJam.Core.CourseLayout.PlayerSpawn(4, 0));
    var giant = GameJam.Net.MischiefMaker.Instance;
    if (giant == null || ball == null) { log.Add("5 giant: missing giant or beach ball"); }
    else
    {
        UnityEngine.Vector3 giantPos = giant.transform.position;
        UnityEngine.Vector3 start = giantPos + new UnityEngine.Vector3(0f, 2f, -2.5f);
        var testHand = new UnityEngine.GameObject("TestHand") { hideFlags = UnityEngine.HideFlags.DontSave }.transform;
        testHand.position = start;
        ball.GetComponent<Fusion.NetworkTransform>().Teleport(start, UnityEngine.Quaternion.identity);
        ball.transform.position = start;
        ball.Grab(testHand, null);
        yield return null;
        ball.Release(new UnityEngine.Vector3(0f, 0.5f, 8f));
        UnityEngine.Object.Destroy(testHand.gameObject, 1f);
        for (float t = 0f; t < 3f && guide.Section == 4; t += UnityEngine.Time.deltaTime) { yield return null; }
        log.Add("5 giant done: mood=" + giant.Mood + " giant at " + giantPos.ToString("F1") + " (den z 54..65) " + Where() + " goal5=" + Goal(4));
    }

    yield return new UnityEngine.WaitForSeconds(0.5f);
    foreach (var t in UnityEngine.Object.FindObjectsByType<UnityEngine.TextMesh>(UnityEngine.FindObjectsSortMode.None))
    {
        if (t.name == "Subtitle" && t.transform.parent != null && t.transform.parent.name == "Label") { log.Add("exit hint: '" + t.text + "'"); }
    }
    log.Add("FINISHED: lessons " + guide.LessonsDone + "/5");
}

guide.StartCoroutine(Run());
return "started; read the log in ~40 s";
