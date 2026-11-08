// Paw grip check (Play mode, PC test mode). Step 1 (no args): places a DontSave close-up camera "PawCam" on your own
// right paw and returns its grip state. Then hold G (or queue it: see below) and run again to read the closed state.
//   unity command script eval --project-path "D:\Unity\game-jam" -- --file .claude/unity-snippets/paw_grip_check.cs
//   unity command capture game_view ... -- --source camera --camera PawCam --output-path Screenshots/paw_open.png
// Set env PAWCHECK_G=1 / 0 to press / release G through the Input System before reading.
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

string g = System.Environment.GetEnvironmentVariable("PAWCHECK_G");
if (g == "1") InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState(Key.G));
if (g == "0") InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState());

var avatar = GameJam.Net.NetworkAvatar.Local;
if (avatar == null) return "no local avatar yet";
var body = avatar.GetComponentInChildren<GameJam.Gameplay.GorillaBodyRig>(true);
Transform glove = null, ring3 = null, hand = null;
foreach (Transform t in body.GetComponentsInChildren<Transform>(true))
{
    if (t.name == "GloveCenter.R") glove = t;
    if (t.name == "Ring3.R") ring3 = t;
    if (t.name == "Hand.R") hand = t;
}

var cam = GameObject.Find("PawCam");
if (cam == null)
{
    cam = new GameObject("PawCam") { hideFlags = HideFlags.DontSave };
    var c = cam.AddComponent<Camera>();
    c.nearClipPlane = 0.01f;
    c.fieldOfView = 40f;
    c.targetDisplay = 7;
}

// Look at the paw from the palm side, a little in front and above.
Vector3 palm = body.transform.right * -Mathf.Sign(body.transform.InverseTransformPoint(hand.position).x);
cam.transform.position = glove.position + (palm * 0.25f + body.transform.forward * 0.2f + Vector3.up * 0.12f);
cam.transform.LookAt(glove.position);

var rig = GameJam.Player.LocalRig.Instance;
var grip = typeof(GameJam.Net.NetworkAvatar).GetProperty("RightGrip", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
float tipToPalm = Vector3.Distance(ring3.position, hand.position);
return $"G={(Keyboard.current.gKey.isPressed ? "down" : "up")} rig.GripAmount(R)={rig.GripAmount(true):F2} drawn grip={body.GetGrip(true):F2} " +
       $"networked RightGrip={grip.GetValue(avatar)} ring tip to wrist {tipToPalm:F3} m";
