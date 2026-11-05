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
- **Voice is not installed yet.** Get the Fusion 2.1 Voice add-on from Tools > Fusion > Fusion Hub > Addons
  (the Realtime 5 build), not the Asset Store package.
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

## Gotchas found in this project

- `Runner.IsSharedModeMasterClient` **throws** outside Shared Mode. Check `Runner.GameMode` first.
- `Runner.SimulationTime` (and anything reading `RuntimeConfig`) throws until the first state update. In Single
  mode `PlayerJoined` fires before that, so read network time only from a spawned object (`GameState.Spawned`
  hands `FusionTimeSource` to `CourseClock`).
- `NetworkObjectFlags.MasterClientObject` forces `AllowStateAuthorityOverride` off; the Fusion inspector enforces it.

## Patterns as built

| Thing | Class | Pattern |
|---|---|---|
| Balloon | `NetworkBalloon` | Master Client Object. Hits go to the authority through `RpcApplyHit(hitter, velocity)`; the first hit it receives wins. The authority runs `BalloonPhysics.Step` in `FixedUpdateNetwork` and syncs `Velocity`, `LastHitter`, `Condition`, `Rally`. Same hitter twice = pop (off solo via `BalloonConfig.AllowSoloDoubleHits`). The GDD's alternative, request authority on slap, is untested; switch only if RPC latency feels bad in a playtest. |
| Items | `NetworkItem` | `AllowStateAuthorityOverride`. `RequestStateAuthority()` on grab; writes `Thrower` on release so a thrown hit counts as that player's hit; a throw before authority arrives is queued for 1 s. |
| Ragdoll | `NetworkAvatar.RpcKnockback` (All → All) | The owner gets `LocalRig.ApplyKnockback` (straight shove, noodle hands); everyone else spawns a local `RagdollPuppet`. Local code calls it through `GameServices.LocalKnockback`. |
| Game state | `GameState` | Master Client Object, does not die with its owner; `NetworkBootstrap.PlayerLeft` requests authority as a backup. Owns `State`, `TickTimer`, `Section`, `Pops`, scores, `Headline`. |
| Avatar | `NetworkAvatar` + `AvatarHitbox` | One `NetworkTransform` per head/body/hand child; the owner copies `LocalRig` into them and hides its own renderers. `DestroyWhenStateAuthorityLeaves` on. |
| Voice | not yet | `FusionVoiceClient` beside the `NetworkRunner`; `VoiceNetworkObject`, `Recorder`, `Speaker` on the avatar prefab. Request Quest microphone permission before enabling the Recorder. |

## Wiring in the Editor

The prefabs and scene come from `Assets/GameJam/Scripts/Editor/` (`PrototypeSceneBuilder`,
`PlaceholderPrefabBuilder`). Change the builder rather than the generated prefabs, then rerun it:
`unity command script eval ... -- --code 'return GameJam.Editor.PrototypeSceneBuilder.BuildFromCli();'`.
It regenerates prefabs, materials and `Scenes/Prototype.unity`, but keeps existing config assets.

## Testing

- Solo: with no App ID, Play mode runs offline. Drive the loop with `script eval`, e.g.
  `GameJam.Core.GameServices.Run.RequestStart()`, `NetworkBalloon.Instance.RequestHit(...)`, or moving the
  balloon onto a `Sharp`/`SectionGate`/`FinishLine`, then read `GameState.Instance` fields.
- Solo pass rule: `BalloonConfig.AllowSoloDoubleHits` (on) lets one player hit twice in a row while fewer
  than 2 players are in the session (`NetworkBalloon.IsPassRuleSuspended`); the scoreboard shows
  "SOLO TEST: pass rule off". It switches itself back on when a second player joins, so leave it on.
- Two clients on one PC: Multiplayer Play Mode is installed (`com.unity.multiplayer.playmode` 2.0.2; in Unity 6.3
  the feature is built into the Editor and the package enables it plus Fusion's `FUSION_ENABLE_MPPM` hooks).
  The user ticks Player 2 in Window > Multiplayer > Multiplayer Play Mode; the CLI has no command for that.
  `unity command editor.playmode list_instances` lists clones; target one with `--instance <name>`.
  **MPPM needs the Fusion App ID**: offline Single mode gives every instance its own separate world.
  Virtual players have no headset, so they run in PC test mode.
- Report what was only tested solo. Authority hand-off, voice and master-client loss need a real multi-player test.
