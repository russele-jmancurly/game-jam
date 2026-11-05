---
name: jam-feature
description: Builds one Don't Let the Balloon Drop gameplay feature end to end (balloon, slap, item, hazard, timer, ragdoll, checkpoint...) from the GDD to a verified, playable prefab. Use when the user asks to add or implement a game feature.
---

# Build a jam feature

Orchestration order for one feature. Don't skip steps; each one is cheap and catches the next step's failure early.

## 1. Scope it (no tools beyond grep)

- `grep -n "^## " "Assets/GameJam/Don't Let the Balloon Drop Game Design Document.md"`, then read only the matching section.
- Check the feature against the GDD scope list: must / should / stretch. If a must-have is unfinished and this is should/stretch, say so and ask before continuing.
- Write a 3 to 6 line plan in chat: scripts (with asmdef), config fields, prefab, how to verify. Ask one question only if a design rule is genuinely unclear.

## 2. Config first

Create or extend a ScriptableObject in `Scripts/Core/Config/` (e.g. `BalloonConfig`, `SlapConfig`,
`ItemConfig`, `HazardConfig`) with every tunable number, defaulting to the GDD's starting values,
with `[Tooltip]` text citing the GDD rule. Create the asset under `Assets/GameJam/Config/` via a snippet
(`ScriptableObject.CreateInstance` + `AssetDatabase.CreateAsset`).

## 3. Code

- Put the script in the asmdef that owns it (see CLAUDE.md §3); reference other assemblies, never `Assembly-CSharp`.
- Gameplay logic in plain C# or a local MonoBehaviour first; networking goes in a separate `NetworkBehaviour` wrapper (load `fusion-networked`). This keeps Day 1 solo testing possible.
- No per-frame allocations (no LINQ, `GetComponent`, `new` lists or string concatenation in Update/FixedUpdate). Cache references in `Awake`.
- Physics in `FixedUpdate`. Slap and hit detection use tracked hand velocity, not collider contact alone (GDD: The balloon / Movement).
- Expose events (`public event Action<...>`) for audio, haptics, commentator and UI to hook later, so polish never edits core logic.
- `unity recompile` after each file group (see `unity-cli-loop` §1).

## 4. Prefab and scene

One idempotent snippet in `.claude/unity-snippets/<feature>.cs` that: builds or updates the prefab in
`Assets/GameJam/Prefabs/`, assigns the config asset, sets layers and collision matrix entries it needs,
and places one instance in the test scene. Run it through `unity-cli-loop` §2, then save.

## 5. Verify

`unity-cli-loop` §3: play, confirm frames advance, read errors, and check the feature with one
`script eval` that returns its state (e.g. balloon velocity after a simulated slap, last-hitter id,
pop count). State plainly what was verified in the Editor and what needs a headset or a second player.

## 6. Hand off

Two or three sentences: what was built, where (files, prefab), the tuning values to try in the
next playtest, and anything left for the headset. Offer a commit; don't commit unasked.
