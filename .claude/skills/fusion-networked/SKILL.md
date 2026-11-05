---
name: fusion-networked
description: Patterns for networking a Don't Let the Balloon Drop feature with Photon Fusion 2.1 Shared Mode and Photon Voice (authority on slap, last-hitter rule, ragdoll RPCs, master-client game state). Use for any NetworkObject, NetworkBehaviour, RPC, authority or voice work.
---

# Networking with Fusion 2.1 Shared Mode

The GDD section "Multiplayer and voice with Photon Fusion" is the spec; read it first. Check the
installed Fusion version's API before relying on a member name below (grep
`Assets/Photon/Fusion/Assemblies/Fusion.Runtime.xml` for `name="P:Fusion.NetworkRunner.X"`), and fix
this skill if it has drifted.

## Installed SDK (keep it this way)

- **Fusion 2.1.4** (`Assets/Photon/Fusion`, installer: `~/Downloads/photon-fusion-2.1.4-stable-2500.unitypackage`).
  Fusion 2.1 ships its own Photon Realtime 5 as source: `Assets/Photon/PhotonRealtime/Code/Photon.Realtime.asmdef`
  (class `RealtimeClient`) and `Assets/Photon/PhotonLibs/netstandard2.0/*/PhotonClient.dll`.
- **Never import PUN 2 or the Asset Store "Photon Voice 2" (v2.64, Realtime 4).** It overwrites Fusion's
  Realtime with PUN's (`LoadBalancingClient`, `PhotonRealtime.asmdef`), the folder ends up with two
  asmdefs and `Fusion.Runtime.dll` fails with "Unable to resolve reference 'Photon.Realtime'". That
  happened on 2026-11-05; the PUN/Voice 2.64 files are quarantined in `_pending/_quarantine_photon_voice_r4/`.
  Fix if it recurs: move PUN, PhotonChat, PhotonVoice, PhotonRealtime and PhotonLibs out of `Assets/Photon`, then
  restore `PhotonRealtime` and `PhotonLibs` from the Fusion unitypackage.
- **Voice: Photon "Voice for Fusion" 2.63.0** as a UPM git package (`Packages/manifest.json`:
  `com.photonengine.voice-fusion` → `https://github.com/Photon-Server/Photon-UPM.git#fusion/v2/voice-for-fusion`,
  the same source the Fusion Hub's Voice add-on uses). It is the Realtime 5 build, ships no PUN and no
  Realtime copy, and lives in `Library/PackageCache` (not committed). It added `PhotonVoice.Fusion` to
  `AssembliesToWeave` on install, but the assembly was compiled before that, so `VoiceNetworkObject` threw
  "has not been weaved" until a clean recompile
  (`CompilationPipeline.RequestScriptCompilation(RequestScriptCompilationOptions.CleanBuildCache)`). Check weaving by
  looking for `NetworkBehaviourWeavedAttribute` by name, not just "has attributes".
- **Voice wiring**: `FusionVoiceClient` on the Network object (follows the Fusion session; offline mode in Single).
  Avatar prefab root: `VoiceNetworkObject`, `Recorder` (48 kHz, `RecordWhenJoined` off, recording off) and
  `AvatarVoice`, which starts recording for the local player only after `MicrophoneAccess` grants the mic
  (Android runtime permission on Quest). Head child `VoiceSpeaker`: 3D `AudioSource` (linear 1.5–25 m) + `Speaker`
  for proximity voice. A Voice App ID (`AppIdVoice`) is needed online; the bootstrap warns when it is missing.
- **App ID**: `Assets/Photon/Fusion/Resources/PhotonAppSettings.asset` > `AppIdFusion` (Tools > Fusion >
  Realtime Settings). The user creates it in the Photon dashboard; never invent one. While it is empty,
  `NetworkBootstrap` starts Fusion in `GameMode.Single` (offline, solo) so the loop is still testable.

## Project wiring

- All `NetworkBehaviour`s live in `Assets/GameJam/Scripts/Net` (`GameJam.Net.asmdef`, `allowUnsafeCode: true`).
  **`GameJam.Net` is listed in `AssembliesToWeave` in `Assets/Photon/Fusion/Resources/NetworkProjectConfig.fusion`.**
  Any new assembly with a `NetworkBehaviour` must be added there, or nothing syncs. Check weaving with
  `script eval`: the type has `NetworkBehaviourWeavedAttribute`.
- Local logic stays out of `Net`: `GameJam.Gameplay` (e.g. `BalloonPhysics`, hazards) and `GameJam.Player`
  (rig, hands) never reference Fusion. They reach the network through `GameJam.Core` interfaces
  (`IRunStatus`, `IKnockbackRelay`, `ISlappable`, `IGrabbable`, `ISpikeSurface`) and `GameServices`, which the
  Net behaviours fill in on `Spawned`.
- After creating or regenerating network prefabs, call `Fusion.Editor.NetworkProjectConfigUtilities.RebuildPrefabTable()`
  (the builder does it). Otherwise `Runner.Spawn` throws "baked with a guid ... failed to be translated into a prefab id".
- RPC methods are PascalCase with an `Rpc` prefix (`RpcApplyHit`) and may be private.

## Rules

- Shared Mode: each client has state authority over its own objects. No host, no server-only logic.
- Split every feature into local logic (plain C#/MonoBehaviour, testable solo) and a thin
  `NetworkBehaviour` that syncs results. Gorilla locomotion stays fully local.
- Sync results, not inputs: `[Networked]` properties for state, RPCs for one-off events.
- Only the state-authority client writes `[Networked]` properties; check `HasStateAuthority`.
- Simulation in `FixedUpdateNetwork`; visuals in `Render`. Initialise in `Spawned`, clean up in `Despawned`.

## Fusion 2.1 features in use

- **Forecast Physics** (`NetworkProjectConfig.PhysicsForecast = true`; per object
  `NetworkTransform.PhysicsSettings.ForecastEnabled`, default true). Networked rigidbodies are extrapolated into each
  client's local time. Rules: physics code in `FixedUpdate` (not `FixedUpdateNetwork`), Rigidbody interpolation
  `Interpolate`, Unity physics simulation mode FixedUpdate, only the state authority toggles `isKinematic` (Fusion syncs
  it; proxies are Fusion's), move objects with `NetworkTransform.Teleport(pos, rot)` (no lerp across the map).
- **Send priority**: each client calls `Object.SetPriority(Runner.LocalPlayer, 1)` on the balloon (every send tick).
  Shared Mode only accepts the local player; skip it in Single mode.
- **Master client reassign**: `AllowMasterClientReassignRequest = true` (threshold 2 s). `GameState` bumps a networked
  `Heartbeat` every tick; other clients call `Runner.RequestMasterClientReassign()` after 2.5 s without change.
- **RPC `TickAligned = false`** for one-off reactions (knockback) so they play on arrival.
- Not used: Input Delay and breakpoints (Host/Server only), large-data RPCs, AOI config, shared tick-rate overrides
  (dashboard/webhook). Release notes: https://doc.photonengine.com/fusion/v2/getting-started/release-notes/whats-new-2-1

## Gotchas found in this project

- `Runner.IsSharedModeMasterClient` **throws** outside Shared Mode. Check `Runner.GameMode` first.
- `Runner.SimulationTime` (and anything reading `RuntimeConfig`) throws until the first state update. In Single
  mode `PlayerJoined` fires before that, so read network time only from a spawned object (`GameState.Spawned`
  hands `FusionTimeSource` to `CourseClock`).
- `NetworkObjectFlags.MasterClientObject` forces `AllowStateAuthorityOverride` off; the Fusion inspector enforces it.

## Patterns as built

| Thing | Class | Pattern |
|---|---|---|
| Balloon | `NetworkBalloon` | Master Client Object, Rigidbody on layer `Balloon` (8) with Forecast Physics (`GravityForecast.None`: its floaty gravity is custom). The authority runs `BalloonPhysics.Step` (rules only: gravity, drag, wind, pads, hazards, spikes, gates, finish) in `FixedUpdate` and writes the Rigidbody velocity; bounces are real physics (`BalloonBounce` material). Hits go to the authority through `RpcApplyHit(hitter, velocity)`; the first hit it receives wins. Same hitter twice = pop (off solo via `BalloonConfig.AllowSoloDoubleHits`). World bounces reset `Rally`; item hits don't. `PhysicsLayerRules` makes `Balloon` ignore `Ignore Raycast` (players' local rig colliders, held items), so all clients simulate the same collisions; slaps, spikes and swung items are overlap checks. |
| Items | `NetworkItem` | `AllowStateAuthorityOverride`, Forecast Physics. `RequestStateAuthority()` on grab; kinematic only while held (authority side); writes `Thrower` on release so a thrown hit counts as that player's hit; a throw before authority arrives is queued for 1 s. Swung (held) items hit by an overlap box in `FixedUpdate`. Off-map items `Teleport` home. |
| Ragdoll | `NetworkAvatar.RpcKnockback` (All → All, `TickAligned = false`) | The owner gets `LocalRig.ApplyKnockback` (straight shove, noodle hands); everyone else spawns a local `RagdollPuppet` that inherits the avatar's body velocity, springs back toward the real body over the last 45% of its life and shrinks away as the avatar reappears. Local code calls it through `GameServices.LocalKnockback`. |
| Game state | `GameState` | Master Client Object, does not die with its owner; `NetworkBootstrap.PlayerLeft` requests authority as a backup, and the heartbeat watchdog requests master reassignment when the master freezes. Owns `State`, `TickTimer`, `HoopMask` (bit per hoop, any order), `LastHoop` (checkpoint for balloon respawn), `Pops`, scores, `Headline`, `Heartbeat`. The finish only counts when every hoop is cleared; a pop respawns only the balloon (beside the last hoop), never the crew. |
| Avatar | `NetworkAvatar` + `AvatarHitbox` | One `NetworkTransform` per head/body/hand child; the owner copies `LocalRig` into them and hides its own renderers. `DestroyWhenStateAuthorityLeaves` on. |
| Characters | (none networked) | Every player is the same porcupine; no character choice is synced. Jmancurly and Squoof are static map figures; only `MischiefMaker.CharacterIndex` (master-owned) picks which one is the giant. |
| Roaming giant | `MischiefMaker` | Master Client Object with NetworkTransform (no Rigidbody). Authority runs a wander/chase/wind-up/bonk state machine in `FixedUpdateNetwork`; the bonk is `NetworkBalloon.RequestHit(PlayerRef.None, ...)` (nobody's hit). `GameState` re-rolls its character at each run start. Implements `IKnockbackSource`, so `RigHazardSensor` knocks players it walks into. |
| Voice | `AvatarVoice` + Photon `VoiceNetworkObject` | See "Voice wiring" above. Online without a Voice App ID, `NetworkBootstrap` sets `FusionVoiceClient.AutoConnectAndJoin = false` (never destroy the client: `VoiceNetworkObject.Spawned` needs it and throws a NullReferenceException) and `NetworkBootstrap.VoiceEnabled` keeps avatars off the microphone. Hearing each other needs both App IDs and two clients (untested). |

## Wiring in the Editor

The prefabs and scene come from `Assets/GameJam/Scripts/Editor/` (`PrototypeSceneBuilder`,
`PlaceholderPrefabBuilder`). Change the builder rather than the generated prefabs, then rerun it:
`unity command script eval ... -- --code 'return GameJam.Editor.PrototypeSceneBuilder.BuildFromCli();'`.
It regenerates prefabs, materials and `Scenes/Prototype.unity`, but keeps existing config assets.

## Testing

- Solo: with no App ID, Play mode runs offline. Drive the loop with `script eval`, e.g.
  `GameJam.Core.GameServices.Run.RequestStart()`, `NetworkBalloon.Instance.RequestHit(...)`, or moving the
  balloon onto a `Sharp`, a hoop or the `FinishLine` with `NetworkTransform.Teleport`, then read `GameState.Instance` fields.
- Solo pass rule: `BalloonConfig.AllowSoloDoubleHits` (on) lets one player hit twice in a row while fewer
  than 2 players are in the session (`NetworkBalloon.IsPassRuleSuspended`); the scoreboard shows
  "SOLO TEST: pass rule off". It switches itself back on when a second player joins, so leave it on.
- Two clients on one PC: Multiplayer Play Mode is installed (`com.unity.multiplayer.playmode` 2.0.2; in Unity 6.3
  the feature is built into the Editor and the package enables it plus Fusion's `FUSION_ENABLE_MPPM` hooks).
  The user ticks Player 2 in Window > Multiplayer > Multiplayer Play Mode; the CLI has no command for that.
  `unity command editor.playmode list_instances` lists clones; target one with `--instance <name>`.
  **MPPM needs the Fusion App ID**: offline Single mode gives every instance its own separate world.
  Virtual players have no headset, so they run in PC test mode.
- **Driving a clone from the CLI**: `--project-path` with the main path doesn't match a clone, and `--instance` didn't
  either; target it with `--pid <pid from list_instances>`. A clone resolves `script eval --file` relative paths against
  its own project folder, so pass absolute paths. Each CLI call takes ~2 s: give timed effects (knockback) >= 5 s.
- **Clone floods "Failed to load NetworkProjectConfigAsset"** (its read-only AssetDatabase never re-imported the
  `.fusion` config because MPPM doesn't sync Fusion's custom dependencies). Temporary recovery, both Editors open and out
  of Play mode: in main, `Refresh(true)` the three static `FusionCustomDependency` fields of
  `Fusion.Editor.NetworkProjectConfigImporter` (`.claude/unity-snippets/mppm_fusion_refresh_main.cs`); in each clone, run
  `FusionMppmRegisterCustomDependencyCommand.Execute()` with the values from main's
  `Temp/FusionMppm/Dependency_Fusion.*.json` (`mppm_fusion_apply_virtual.cs`), then `NetworkProjectConfig.UnloadGlobal()`
  and remove only `NetworkProjectConfigAsset` from `FusionGlobalScriptableObject._typesFailedToLoad`
  (`mppm_fusion_reload_virtual.cs`). Verified to survive a script recompile; not proven to survive an Editor restart.
- **A rebuilt scene does not reach a clone by itself**: after `BuildFromCli`, the clone keeps the old scene (it once ran
  the old hoops as balloon owner). With both out of Play mode, in the clone run `AssetDatabase.Refresh()` and
  `EditorSceneManager.OpenScene(<active scene path>)`, then compare a few object positions with main before playing.
- **2-player test recipe (2026-11-06, all passed)**: both join `BalloonDrop-Dev`, each sees the other's porcupine with
  arms on its hands (`mp_remote_avatar_check.cs`); the non-master starts the run; `RequestHit` from either side reaches the
  balloon owner, a same-player double hit pops; a real glove slap (`mp_real_slap.cs` then `mp_real_slap_swing.cs`)
  registers from owner and guest; `SendKnockback` knocks the owner down while the other side shows a ragdoll.
  Expected noise: "no scene in StartGameArgs.Scene" (by design), clone-only Package Manager / ProjectSettingsWindow logs.
- Report what was only tested solo. Authority hand-off, voice and master-client loss need a real multi-player test.
