// Play mode. Installs a recorder (EditorApplication.update, survives scene loads) that watches the tutorial trip's
// sounds and effects and keeps, per item, whether it was ever seen: the screen fade's highest alpha, each watched clip
// playing (Mole_Dive, Tunnel_Rush, Tunnel_Land, Gate_Rumble, Lesson_Done, Win_Fanfare), and the highest live particle
// count of each watched emitter (the hole's and landing's Clods/Dust, HoleDust, TunnelStreaks, the gates' DustPuffs,
// the confetti). Also checks every FaceCamera label really faces the head (worst angle seen, degrees).
// Run it, then drive the trip (tutorial_dive.cs, tutorial_lessons_check.cs), then read:
//   script eval --code 'return (string)System.AppDomain.CurrentDomain.GetData("tutorialFx");'
var domain = System.AppDomain.CurrentDomain;
if (domain.GetData("tutorialFxRecorder") is UnityEditor.EditorApplication.CallbackFunction old) { UnityEditor.EditorApplication.update -= old; }
var clips = new System.Collections.Generic.SortedDictionary<string, bool>();
foreach (var name in new[] { "Mole_Dive", "Tunnel_Rush", "Tunnel_Land", "Gate_Rumble", "Lesson_Done", "Win_Fanfare" }) { clips[name] = false; }
var particles = new System.Collections.Generic.SortedDictionary<string, int>();
float maxFade = 0f, worstLabelAngle = 0f;
int labelsSeen = 0, frame = 0;
UnityEditor.EditorApplication.CallbackFunction rec = null;
rec = () =>
{
    if (!UnityEngine.Application.isPlaying) { UnityEditor.EditorApplication.update -= rec; return; }
    var fade = GameJam.Gameplay.ScreenFade.Instance;
    if (fade != null) { maxFade = UnityEngine.Mathf.Max(maxFade, fade.Alpha); }
    if (frame++ % 2 != 0) { return; }
    foreach (var source in UnityEngine.Object.FindObjectsByType<UnityEngine.AudioSource>(UnityEngine.FindObjectsSortMode.None))
    {
        if (source.isPlaying && source.clip != null && clips.ContainsKey(source.clip.name)) { clips[source.clip.name] = true; }
    }
    foreach (var ps in UnityEngine.Object.FindObjectsByType<UnityEngine.ParticleSystem>(UnityEngine.FindObjectsSortMode.None))
    {
        string n = ps.name;
        if (n != "Clods" && n != "Dust" && n != "HoleDust" && n != "TunnelStreaks" && n != "DustPuffs" && !n.Contains("Confetti")) { continue; }
        string key = n + "<" + (ps.transform.parent != null ? ps.transform.parent.name : "") + (ps.transform.parent != null && ps.transform.parent.parent != null ? "<" + ps.transform.parent.parent.name : "");
        if (key.Contains("HandDirt<GorillaRig")) { continue; } // hand taps, not part of the trip
        particles.TryGetValue(key, out int best);
        particles[key] = System.Math.Max(best, ps.particleCount);
    }
    var cam = UnityEngine.Camera.main;
    if (cam != null)
    {
        var labels = UnityEngine.Object.FindObjectsByType<GameJam.Gameplay.FaceCamera>(UnityEngine.FindObjectsSortMode.None);
        labelsSeen = System.Math.Max(labelsSeen, labels.Length);
        foreach (var label in labels)
        {
            UnityEngine.Vector3 away = label.transform.position - cam.transform.position;
            away.y = 0f;
            if (away.sqrMagnitude > 0.01f) { worstLabelAngle = UnityEngine.Mathf.Max(worstLabelAngle, UnityEngine.Vector3.Angle(label.transform.forward, away)); }
        }
    }
    var sb = new System.Text.StringBuilder();
    sb.Append("fade max alpha ").Append(maxFade.ToString("F2")).Append("\nclips played:");
    foreach (var c in clips) { sb.Append(" ").Append(c.Key).Append("=").Append(c.Value ? "YES" : "no"); }
    sb.Append("\nparticles (max alive):");
    foreach (var p in particles) { sb.Append("\n  ").Append(p.Key).Append(" = ").Append(p.Value); }
    sb.Append("\nbillboard labels: ").Append(labelsSeen).Append(" at most in a scene, worst angle off the player ").Append(worstLabelAngle.ToString("F1")).Append(" deg");
    domain.SetData("tutorialFx", sb.ToString());
};
UnityEditor.EditorApplication.update += rec;
domain.SetData("tutorialFxRecorder", rec);
return "recording";
