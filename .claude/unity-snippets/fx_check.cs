// Play mode: checks the pooled VFX. Fires a hand tap on the lawn and one on wood through HandTapEffects (only the lawn
// one may kick up dirt), a confetti burst and a balloon pop through BurstEffects, and reports particle counts. Slows time
// to 1% so a capture right after still sees the bursts (run with STEP = "restore" afterwards to set time back to 1).
// The dirt is paused 0.12 s into its flight. Cameras: FxCam_Dirt looks at the lawn taps, FxCam_Burst at the confetti and pop.
string STEP = "fire";
if (STEP == "restore") { UnityEngine.Time.timeScale = 1f; foreach (var ps in UnityEngine.Object.FindFirstObjectByType<GameJam.Player.HandTapEffects>().GetComponentsInChildren<UnityEngine.ParticleSystem>()) { ps.Play(); } return "time scale 1"; }
if (!UnityEngine.Application.isPlaying) { return "enter Play mode first"; }
var tap = UnityEngine.Object.FindFirstObjectByType<GameJam.Player.HandTapEffects>();
var bursts = GameJam.Gameplay.BurstEffects.Instance;
if (tap == null || bursts == null) { return $"missing: HandTapEffects={tap != null} BurstEffects={bursts != null}"; }
var sounds = UnityEngine.Object.FindFirstObjectByType<GorillaLocomotion.Sounds>();
UnityEngine.Material Mat(string surface) { foreach (var s in sounds.Surfaces) { if (s.Name == surface) { return s.Materials[0]; } } return null; }
var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
var onTap = typeof(GameJam.Player.HandTapEffects).GetMethod("OnHandTapped", flags);
var clods = (UnityEngine.ParticleSystem)typeof(GameJam.Player.HandTapEffects).GetField("_clods", flags).GetValue(tap);
UnityEngine.Camera Cam(string name, UnityEngine.Vector3 at, UnityEngine.Vector3 look)
{
    var go = UnityEngine.GameObject.Find(name) ?? new UnityEngine.GameObject(name);
    var cam = go.GetComponent<UnityEngine.Camera>();
    if (cam == null) { cam = go.AddComponent<UnityEngine.Camera>(); }
    cam.enabled = false;
    go.transform.SetPositionAndRotation(at, UnityEngine.Quaternion.LookRotation(look - at));
    cam.fieldOfView = 50f;
    return cam;
}
var lawn = new UnityEngine.Vector3(4f, 0f, 6f);
UnityEngine.Physics.Raycast(lawn + UnityEngine.Vector3.up * 5f, UnityEngine.Vector3.down, out var hit, 10f);
lawn = hit.point;
Cam("FxCam_Dirt", lawn + new UnityEngine.Vector3(0f, 0.4f, -0.6f), lawn + UnityEngine.Vector3.up * 0.15f);
UnityEngine.Time.timeScale = 0.01f;
int before = clods.particleCount;
onTap.Invoke(tap, new object[] { lawn, UnityEngine.Vector3.up, Mat("Grass") });
onTap.Invoke(tap, new object[] { lawn + UnityEngine.Vector3.right * 0.3f, UnityEngine.Vector3.up, Mat("Grass") });
int afterGrass = clods.particleCount;
onTap.Invoke(tap, new object[] { lawn + UnityEngine.Vector3.left * 0.3f, UnityEngine.Vector3.up, Mat("Wood") });
onTap.Invoke(tap, new object[] { lawn + UnityEngine.Vector3.left * 0.3f, UnityEngine.Vector3.up, Mat("Metal") });
int afterWood = clods.particleCount;
// Show the dirt mid-flight: advance it 0.12 s and leave it paused for the capture (STEP "restore" resumes it).
foreach (var ps in tap.GetComponentsInChildren<UnityEngine.ParticleSystem>()) { ps.Simulate(0.12f, false, false, false); }

var burstAt = lawn + new UnityEngine.Vector3(0f, 2.2f, 4f);
Cam("FxCam_Burst", burstAt + new UnityEngine.Vector3(0f, -0.4f, -4.5f), burstAt);
GameJam.Gameplay.BurstEffects.Confetti(burstAt + UnityEngine.Vector3.left * 0.9f);
GameJam.Gameplay.BurstEffects.Pop(burstAt + UnityEngine.Vector3.right * 0.9f, GameJam.Core.ArtPalette.CandyBlue);
var systems = bursts.GetComponentsInChildren<UnityEngine.ParticleSystem>();
var counts = new System.Collections.Generic.List<string>();
foreach (var ps in systems) { counts.Add($"{ps.name}={ps.particleCount}"); }
return $"lawn {lawn:F2}: clods {before} -> {afterGrass} after 2 grass taps -> {afterWood} after wood+metal taps (must not grow); bursts {string.Join(" ", counts)}; time scale 0.01";
