// Play mode, tutorial scene. Checks the cactus (BackyardCourseBuilder.CactusPot, Art/Set/Cactus.fbx) pops the balloon
// wherever it touches it: drops the balloon onto each cactus's top, pushes it into the body's side, each arm and the pot,
// and one control beside it that must NOT pop within a second (floating free, 1.2 m from the cactus, 2.5 m up).
// Read: script eval --code 'return string.Join("\n", (System.Collections.Generic.List<string>)System.AppDomain.CurrentDomain.GetData("tutorialLog"));'
var balloon = GameJam.Net.NetworkBalloon.Instance;
if (balloon == null) { return "no balloon yet"; }
var nt = balloon.GetComponent<Fusion.NetworkTransform>();
var log = new System.Collections.Generic.List<string>();
System.AppDomain.CurrentDomain.SetData("tutorialLog", log);
var host = UnityEngine.Object.FindFirstObjectByType<GameJam.Net.TutorialGuide>();
System.Collections.IEnumerator Run()
{
    foreach (var sharp in UnityEngine.Object.FindObjectsByType<GameJam.Gameplay.Sharp>(UnityEngine.FindObjectsSortMode.None))
    {
        UnityEngine.Transform c = sharp.transform;
        float s = c.localScale.x;
        (string name, UnityEngine.Vector3 from, UnityEngine.Vector3 push, bool shouldPop)[] cases =
        {
            ("top", c.TransformPoint(new UnityEngine.Vector3(0f, 2.3f, 0f)), UnityEngine.Vector3.down * 2f, true),
            ("body side", c.TransformPoint(new UnityEngine.Vector3(0f, 1.1f, -1.1f)), c.forward * 2.5f, true),
            ("west arm", c.TransformPoint(new UnityEngine.Vector3(-1.4f, 1.2f, 0f)), c.right * 2.5f, true),
            ("east arm", c.TransformPoint(new UnityEngine.Vector3(1.4f, 1.0f, 0.04f)), -c.right * 2.5f, true),
            ("pot", c.TransformPoint(new UnityEngine.Vector3(0f, 0.45f, -1.1f)), c.forward * 2.5f + UnityEngine.Vector3.up * 0.6f, true),
            ("control: beside it", c.TransformPoint(new UnityEngine.Vector3(0f, 2.5f, -1.2f)), UnityEngine.Vector3.up * 1.5f, false),
        };
        foreach (var k in cases)
        {
            int before = balloon.PopCount;
            // After a pop the balloon is held at its station, which would pull it straight back: a first nudge frees it.
            balloon.RequestHit(Fusion.PlayerRef.None, UnityEngine.Vector3.up * 0.1f);
            yield return new UnityEngine.WaitForSeconds(0.1f);
            nt.Teleport(k.from, UnityEngine.Quaternion.identity);
            yield return new UnityEngine.WaitForSeconds(0.1f);
            before = balloon.PopCount;
            balloon.RequestHit(Fusion.PlayerRef.None, k.push);
            float t = 0f;
            float limit = k.shouldPop ? 2.5f : 1f;
            for (; t < limit && balloon.PopCount == before; t += UnityEngine.Time.deltaTime) { yield return null; }
            bool popped = balloon.PopCount > before;
            log.Add((popped == k.shouldPop ? "PASS " : "FAIL ") + "cactus scale " + s.ToString("F1") + " " + k.name + ": popped=" + popped + " after " + t.ToString("F2") + " s");
            yield return new UnityEngine.WaitForSeconds(0.3f);
        }
    }
    log.Add("FINISHED");
}
host.StartCoroutine(Run());
return "started";
