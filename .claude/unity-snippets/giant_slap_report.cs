// Summarises giant_slap_record.cs: mood/gesture timeline, the hit, the hand's closest approach to the balloon during the
// swing, and foot sliding while walking (speed of the planted foot vs the body, from the lower foot each frame).
var domain = System.AppDomain.CurrentDomain;
var log = domain.GetData("giantLog") as System.Collections.Generic.List<string>;
if (log == null) return "no recording";
var lines = new System.Collections.Generic.List<string>();
var feet = new System.Collections.Generic.List<float[]>();
var sides = new System.Collections.Generic.List<string>();
foreach (var l in log)
{
    if (l.StartsWith("F "))
    {
        var p = l.Split(' ');
        feet.Add(new[] { float.Parse(p[1]), float.Parse(p[2]), float.Parse(p[3]), float.Parse(p[4]), float.Parse(p[6]), float.Parse(p[7]), p.Length > 8 ? float.Parse(p[8]) : 0f });
        sides.Add(p[5]);
    }
    else lines.Add(l);
}
// Planted-foot slide: consecutive frames with the same lower foot, foot near its lowest, body moving.
var slide = new System.Collections.Generic.List<float>();
var body = new System.Collections.Generic.List<float>();
var localBack = new System.Collections.Generic.List<float>();
float minY = 999f; foreach (var f in feet) minY = System.Math.Min(minY, f[2]);
for (int i = 1; i < feet.Count; i++)
{
    float dt = feet[i][0] - feet[i - 1][0];
    if (dt <= 0.001f || sides[i] != sides[i - 1] || feet[i][2] > minY + 0.06f) continue;
    float bodySpeed = new UnityEngine.Vector2(feet[i][4] - feet[i - 1][4], feet[i][5] - feet[i - 1][5]).magnitude / dt;
    if (bodySpeed < 0.8f) continue;
    slide.Add(new UnityEngine.Vector2(feet[i][1] - feet[i - 1][1], feet[i][3] - feet[i - 1][3]).magnitude / dt);
    body.Add(bodySpeed);
    localBack.Add((feet[i - 1][6] - feet[i][6]) / dt); // + = foot moving backward relative to the body
}
slide.Sort(); body.Sort(); localBack.Sort();
string slideText = slide.Count > 0 ? $"planted foot speed median {slide[slide.Count / 2]:F2} m/s vs body {body[body.Count / 2]:F2} m/s over {slide.Count} frames; planted foot backward speed relative to body {localBack[localBack.Count / 2]:F2} m/s" : "no walking frames";
var shots = domain.GetData("giantShots") as System.Collections.Generic.List<string>;
return string.Join("\n", lines) + "\nclosest hand-balloon during swing: " + domain.GetData("giantMinHand") + "\n" + slideText + "\nshots: " + (shots != null ? string.Join(", ", shots) : "none");
