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
unity command job wait $P -- --condition '{"member":"UnityEngine.Time.frameCount","op":"changed"}' --timeout 10
unity command console get $P -- --level error --tail 50
# only if the visual result is the question:
unity command capture game_view $P -- --source camera --save_path Screenshots/check.png
unity command editor.playmode stop $P
unity command editor.playmode set_run_in_background $P -- --enable false
unity command editor set_auto_tick $P -- --enable false
```

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
