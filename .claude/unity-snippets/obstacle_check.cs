// Gap obstacle check (Play mode, offline, PC test mode). Starts a run, then holds the balloon in each obstacle's path:
//   1) the cactus wagon: on its rails at cactus height -> pops ("Popped on a cactus!") when the wagon comes by;
//   2) a hanging cactus: in the middle of its frame -> pops;
//   3) the sprinkler: 4 m from it -> the sweeping gust pushes it outward (held in place, its speed is read);
//   4) a finish pinata: in its sweep -> swatted (a hit of well over 2 m/s), not popped;
//   5) a garden cactus: lowered onto it -> pops.
// Logs "[ObstacleTest]" lines, then DONE (~45 s: the wagon and the sprinkler take a while to come round).
//   unity command script eval --project-path "E:\Project\game-jam" -- --file .claude/unity-snippets/obstacle_check.cs
using System.Reflection;
using UnityEngine;

var balloon = GameJam.Net.NetworkBalloon.Instance;
var state = GameJam.Net.GameState.Instance;
if (balloon == null || state == null) return "not ready";
System.Action<string, bool, string> report = (name, pass, detail) => Debug.Log($"[ObstacleTest] {(pass ? "PASS" : "FAIL")} {name}: {detail}");
PropertyInfo headlineProp = typeof(GameJam.Net.GameState).GetProperty("Headline", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
System.Func<string> headline = () => headlineProp.GetValue(state)?.ToString();
var body = balloon.GetComponent<Rigidbody>();
System.Func<string, Transform> find = n => { var o = GameObject.Find(n); return o != null ? o.transform : null; };
Transform wagon = find("CactusWagon"), hanging = find("HangingCactus1"), sprinkler = find("SprinklerHead"), pinata = find("FinishPinata1");
if (wagon == null || hanging == null || sprinkler == null || pinata == null) return "an obstacle is missing from the scene";
var garden = (Transform)null;
foreach (var sharp in Object.FindObjectsByType<GameJam.Gameplay.Sharp>(FindObjectsSortMode.None))
{
    if (sharp.transform.parent != null && sharp.transform.parent.name == "Course" && (garden == null || sharp.transform.position.z < garden.position.z)) garden = sharp.transform;
}

// Where each test holds the balloon.
Vector3 wagonSpot = wagon.position + wagon.rotation * new Vector3(0f, 1.6f, 6f);          // the middle of the rails, cactus height
Vector3 hangingSpot = new Vector3(hanging.position.x, hanging.position.y - 3.4f, hanging.position.z); // 1 m above the pot's base
Vector3 sprinklerSpot = sprinkler.position + new Vector3(4f, 1.2f, 0f);
Vector3 pinataSpot = new Vector3(pinata.position.x, pinata.position.y - 3.4f, pinata.position.z);

int step = -1, pops = 0;
float stepStart = 0f, peak = 0f;
string popLine = "";
int lastPops = balloon.PopCount;
System.Action<Vector3> hold = p => { body.position = p; balloon.transform.position = p; };
System.Action<Vector3> place = p =>
{
    balloon.ResetTo(p);
    balloon.RequestHit(Fusion.PlayerRef.None, Vector3.down * 0.6f); // live (cancels the upward assist)
};
System.Action<int, Vector3> begin = (next, p) => { step = next; stepStart = Time.time; pops = 0; peak = 0f; popLine = ""; lastPops = balloon.PopCount; place(p); };

UnityEngine.Events.UnityAction tick = null;
tick = () =>
{
    float t = Time.time - stepStart;
    if (balloon.PopCount != lastPops) { lastPops = balloon.PopCount; pops++; popLine = headline(); }
    switch (step)
    {
        case -1:
            state.RequestStart(); step = 0; stepStart = Time.time;
            break;
        case 0:
            if (state.State == GameJam.Core.RunState.Running && t > 0.5f) begin(1, wagonSpot);
            break;
        case 1: // wagon
            if (pops == 0 && t > 0.4f) { hold(wagonSpot); body.linearVelocity = Vector3.zero; }
            if (pops > 0 || t > 14f) { report("cactus wagon pops it", pops > 0 && popLine.Contains("cactus"), $"popped {pops > 0} \"{popLine}\" after {t:F1} s"); begin(2, hangingSpot); }
            break;
        case 2: // hanging cactus
            if (pops == 0 && t > 0.4f) { hold(hangingSpot); body.linearVelocity = Vector3.zero; }
            if (pops > 0 || t > 6f) { report("hanging cactus pops it", pops > 0 && popLine.Contains("cactus"), $"popped {pops > 0} \"{popLine}\" after {t:F1} s"); begin(3, sprinklerSpot); }
            break;
        case 3: // sprinkler gust: hold the position, keep the sideways velocity the gust gives it
        {
            if (t > 0.4f)
            {
                Vector3 v = body.linearVelocity;
                Vector3 outward = sprinklerSpot - sprinkler.position; outward.y = 0f; outward.Normalize();
                peak = Mathf.Max(peak, Vector3.Dot(v, outward));
                hold(sprinklerSpot);
                body.linearVelocity = new Vector3(v.x, 0f, v.z);
            }
            if (t > 13f) { report("sprinkler gust pushes it outward", peak > 1f && pops == 0, $"peak outward speed {peak:F1} m/s, pops {pops}"); begin(4, pinataSpot); }
            break;
        }
        case 4: // pinata
        {
            if (t > 0.4f)
            {
                peak = Mathf.Max(peak, body.linearVelocity.magnitude);
                if (peak < 2f) { hold(pinataSpot); }
            }
            if (peak > 2f && t > 0.6f || t > 6f)
            {
                report("pinata swats it (no pop)", peak > 2f && pops == 0, $"speed after the hit {peak:F1} m/s, pops {pops}");
                begin(5, garden != null ? garden.position + Vector3.up * 3.5f : pinataSpot);
            }
            break;
        }
        case 5: // garden cactus: let it sink onto the cactus from above
            if (pops > 0 || t > 8f)
            {
                report("garden cactus pops it", pops > 0 && popLine.Contains("cactus"), $"popped {pops > 0} \"{popLine}\" after {t:F1} s");
                Application.onBeforeRender -= tick;
                step = 6;
                Debug.Log("[ObstacleTest] DONE");
            }
            break;
    }
};
Application.onBeforeRender += tick;
return "ObstacleTest started (~45 s)";
