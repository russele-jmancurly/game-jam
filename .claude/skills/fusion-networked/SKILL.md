---
name: fusion-networked
description: Patterns for networking a Don't Let the Balloon Drop feature with Photon Fusion 2 Shared Mode and Photon Voice 2 (authority on slap, last-hitter rule, ragdoll RPCs, master-client game state). Use for any NetworkObject, NetworkBehaviour, RPC, authority or voice work.
---

# Networking with Fusion 2 Shared Mode

The GDD section "Multiplayer and voice with Photon Fusion" is the spec; read it first. Check the
installed Fusion version's API (open its source under `Assets/Photon/`) before relying on a member
name below, and fix this skill if it has drifted.

## Rules

- Shared Mode: each client has state authority over its own objects. No host, no server-only logic.
- Split every feature into local logic (plain MonoBehaviour, testable solo) and a thin
  `NetworkBehaviour` that syncs results. Gorilla locomotion stays fully local.
- Sync results, not inputs: `[Networked]` properties for state, RPCs for one-off events.
- Only the state-authority client writes `[Networked]` properties; check `Object.HasStateAuthority`.
- Simulation in `FixedUpdateNetwork`; visuals and interpolation in `Render`. Initialise in `Spawned`.

## Patterns from the GDD

| Thing | Pattern |
|---|---|
| Balloon | On a local slap: `Object.RequestStateAuthority()`, apply the impulse locally, write `LastHitter` (a `PlayerRef`) and `Condition`. Validate the hit against `LastHitter` on the authority; same player twice = pop. If two hits race, the first one the authority applies wins. Keep the balloon slow so authority hand-offs hide corrections. |
| Items | Request authority on grab; write `Thrower` on release so a thrown-item hit counts as that player's hit. Hazard-knocked items carry `PlayerRef.None`. |
| Ragdoll | `[Rpc(RpcSources.All, RpcTargets.StateAuthority)]` on the hit player's avatar carrying direction and force; each client simulates the flop locally. The local player gets straight knockback plus vignette only (Comfort rules). |
| Game state (timer, points, leg result, pop reset) | One `GameState` NetworkObject owned by the master client (`Runner.IsSharedModeMasterClient`). On master leave, the new master requests authority. Test this on Day 1. |
| Voice | `FusionVoiceClient` beside the `NetworkRunner`; `VoiceNetworkObject`, `Recorder`, `Speaker` on the avatar prefab. Request Quest microphone permission before enabling the Recorder. |

## Wiring in the Editor

Prefab setup (NetworkObject, Network Rigidbody/Transform, registering prefabs, runner bootstrap in
the scene) goes through `unity-cli-loop` §2 as one snippet. Use `unity command --query fusion` or
`--query network` to see whether Fusion exposes Editor commands; otherwise use its C# Editor API in the snippet.

## Testing

- Solo: local logic path in Play mode via `unity-cli-loop` §3.
- Two clients on one PC: check for Multiplayer Play Mode (`unity command --query mppm`) and target a
  clone with `--instance <name>`; otherwise ask the user for a build plus Editor session.
- Report what was only tested solo. Authority hand-off, voice and master-client loss need a real multi-player test.
