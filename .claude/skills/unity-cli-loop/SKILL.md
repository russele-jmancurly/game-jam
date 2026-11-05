---
name: unity-cli-loop
description: The required loop for every Unity Editor operation in this project (scenes, prefabs, components, play mode, console, tuning) using the Unity CLI and Pipeline package, never Unity MCP. Load with the unity-cli skill before the first Editor call.
---

# Unity CLI loop for game-jam

The general CLI reference is the `unity-cli` skill; this is the project's fixed routine on top of it.
`$P` below means `--project-path "E:\Project\game-jam"`. Add `--caller plugin --skill unity-cli-loop`
(or the calling skill's name) to every `unity command`.

## 0. Preflight (once per session)

```bash
unity --version && unity self-update --check --format json   # tell the user if an update exists; don't self-update unasked
unity status $P --format json                                 # want state "ready"
unity command --detail full --format json $P > .claude/unity-snippets/_catalog.json   # cache the command list for this session
```

- `STATUS_SAFE_MODE`: read `safeMode.compileErrors`, fix the C#, then re-check. Fixing code is correct here.
- No instances: ask the user whether Unity is open before doing anything else. Never edit scene YAML instead.
- `STATUS_PIPELINE_LOAD_PENDING`: ask the user to click into the Unity window, then `unity status --until-ready $P`.
- Look things up in `_catalog.json` (or `unity command --query <term>`) instead of re-listing commands.

## 1. Code change loop

1. Edit `.cs` files with the file tools (Read/Edit). Keep each change inside one asmdef where possible.
2. `unity recompile $P --format json`. On errors: fix and repeat. Use `--strict` before a commit.
3. `unity command console get $P -- --level error --tail 30` for runtime/import errors.

Never use `unity build` to check compilation. Don't use `script create` when the file tools can write the file.

If `unity recompile` fails with `COMMAND_FAILED` "HTTP 400" (seen with CLI 1.0.0-beta.16 + Pipeline
0.10.0-exp.1), compile through the Editor instead:
`unity command script eval $P -- --code 'UnityEditor.AssetDatabase.Refresh(); return "ok";'`, then
`unity status --until-ready $P`. The domain reload drops the connection for a few seconds, so an
immediate next call can fail with `EDITOR_NOT_RUNNING`: wait with `--until-ready` and retry once. Confirm the
new code loaded with a `script eval` that reflects on a type or field you just added, and read
`groundTruth.compilationFailed` from `console get`.

## 2. Scene / prefab change loop (batched)

For anything more than two operations, write one snippet instead of many calls:

1. Write `.claude/unity-snippets/<task>.cs` as a method body using `UnityEngine`/`UnityEditor` APIs.
   Make it idempotent (find-or-create by name), use `PrefabUtility` for prefabs, register `Undo`,
   and `return` a short summary string (names, counts) rather than full objects.
2. `unity command script eval_file $P .claude/unity-snippets/<task>.cs` (if the catalog lacks
   `eval_file`, pass the code to `script eval -- --code '...' --timeout 15000`).
3. Verify cheaply: `unity command scene get_hierarchy $P` or a `script eval` that returns the one value you care about.
4. `unity command editor save_all $P`.

Single quick edits can use the built-in commands (`gameobject create`, `component add`,
`gameobject set_transform`, `scene save`) directly. Snippets stay in the repo so they can be re-run.

## 3. Play mode verification

Follow `unity-cli` → `references/playmode-verification-loop.md`. Short form:

```bash
unity command editor status $P                         # already playing = the user's session: read only
unity command editor set_auto_tick $P
unity command editor.playmode set_run_in_background $P
unity command editor.playmode start $P
unity status --until-ready $P --format json
unity command editor set_auto_tick $P                  # again: a domain reload on entering Play resets it
unity command job wait $P -- --condition '{"member":"UnityEngine.Time.frameCount","op":"greaterThan","value":200}' --timeout 30
unity command console get $P -- --level error --tail 50
# only if the visual result is the question:
unity command capture game_view $P -- --source camera --save_path Screenshots/check.png
unity command editor.playmode stop $P
unity command editor.playmode set_run_in_background $P -- --enable false
unity command editor set_auto_tick $P -- --enable false
```

`job wait` operators are `equals`, `notEquals`, `greaterThan`, `lessThan`, `contains` and `changed` (not `gt`).
If `frameCount` stays at 1 to 3, check `EditorApplication.isPaused`: Error Pause stops Play on the first
exception, so read `console get -- --level error` before assuming the Editor is unfocused.

The prototype scene is `Assets/GameJam/Scenes/Prototype.unity` (generated; see `jam-feature` §4). It has a
PC test mode (`DesktopRigInput`), on whenever no headset is active, or always with
`PlayerRigConfig.ForceDesktopMode` (then the camera renders to the Game view, not the headset). Controls:
WASD, Space, hold right mouse to look, left click to slap, hold G to grab, K for a self-knockback, Enter to
start a run; a panel in the top-left lists them (`ShowDesktopControlsHelp`). `LocalRig.UsesHeadset` tells
which mode is active.

To slap with the real glove from a script (exercises the hand sweep, not just the balloon RPC), place the
balloon in front of the right hand, about `head + yaw * (0.2, -0.2, 0.55)`, then set `DesktopRigInput._slapTimer`
to 0.2 by reflection. Wait ~0.6 s between slaps (the cooldown is 0.2 s) and read `NetworkBalloon.Instance.LastHitter`.
A slapped balloon often ends up in the water, which pops it and resets `LastHitter`, so re-place it before every slap.
`capture game_view` returned nothing in Play mode on 2026-11-05; OnGUI panels never show in camera captures anyway.

VR note: there is no headset in this loop. Test gameplay logic with the XR Device Simulator or a
debug script, and tell the user what still needs a real headset test.

## 4. Live tuning

While playing, change values on the config ScriptableObject, not on scene components:

```bash
unity command script eval $P -- --code 'var c = UnityEditor.AssetDatabase.LoadAssetAtPath<GameJam.Core.BalloonConfig>("Assets/GameJam/Config/BalloonConfig.asset"); c.drag = 2.5f; return c.drag;'
```

Asset changes made in Play mode persist, so report every value you changed and confirm with the
user before keeping it. Save (`editor save_all`) only after approval.

## 5. Tests

`unity test` refuses a project that is open in an Editor. With the Editor open, find the test command
with `unity command --query test`, run it with `--detach`, then `unity job wait <id>`. Run EditMode first.

## 6. When something fails

Read `errors[0].code`. Exit 17 (busy/compiling): wait with `unity status --until-ready`. Exit 18
(modal dialog): `unity command editor dialog` to list it and tell the user. A timeout, a dropped
connection, or an Editor that quits: stop and say what ran and what failed. Don't retry in a loop,
don't switch Editors, don't fall back to Unity MCP or YAML edits.
