# Don't Let the Balloon Drop: Claude working rules

Co-op VR physics game for the Curly Blue three-day jam. Unity 6000.3.8f1, URP, OpenXR (Meta Quest),
XR Interaction Toolkit 3.5, Photon Fusion 2 (Shared Mode) + Photon Voice 2.
The design source of truth is `Assets/GameJam/Don't Let the Balloon Drop Game Design Document.md`.
Read only the section you need (grep its `##` headings); never paste the whole GDD into context.

## 1. Drive Unity with the Unity CLI, never the Unity MCP server

- All Editor work goes through the `unity` CLI and the project's `com.unity.pipeline` package.
  Do not call Unity MCP tools (`mcp__unityMCP__*` or any other Unity MCP server), even when they
  are listed, and do not ask to re-enable them.
- Load the `unity-cli` skill, then the project skill `unity-cli-loop`, before the first Editor call
  of a session. The loop there is the only way to touch scenes, prefabs or assets.
- Always pass `--project-path "E:\Project\game-jam"` (several Editors may be open) and
  `--caller plugin --skill <skill name>` on every `unity command` call.
- Discover command names with `unity command --query <term>`; never guess them or grep `--help`.
- Never hand-edit `.unity`, `.prefab` or `.asset` YAML while an Editor is reachable.
- Once per machine, mirror the Pipeline package's own skill: `unity skill install claude-code --local`.

## 2. Which skill to load

| Task | Skill |
|---|---|
| Any Editor operation (scene, prefab, component, play mode, console) | `unity-cli` + `unity-cli-loop` |
| Building a GDD feature end to end (balloon, slap, item, hazard, timer...) | `jam-feature` |
| Anything networked (NetworkObject, authority, RPC, voice) | `fusion-networked` |
| Collisions/triggers not firing, slap passing through balloon | `unity:physics-3d-collision` |
| Quest frame rate, GC spikes | `anthropic-skills:unity-performance`, `anthropic-skills:csharp-zero-gc` |
| Adding/upgrading packages | `unity:unity-package-management` |
| World-space UI (wrist timer, scoreboard) | `unity:ui` |
| Look and feel, juice, audio polish (Day 3) | `unity:craft-game-floors`, `unity:urp-postprocessing` |
| Audio routing / memory | `unity:audio-setup-mixers`, `unity:optimize-audio` |
| Code review before a commit | `anthropic-skills:code-standards` |

Load a skill only when its task comes up; don't preload them all.

## 3. Project layout

Everything we make lives under `Assets/GameJam/`. Leave `VRTemplateAssets/`, `Samples/` and
`Photon/` untouched (vendor code).

```
Assets/GameJam/
  Scripts/Core/        GameJam.Core.asmdef      timer, scoring, leg flow, config SOs
  Scripts/Gameplay/    GameJam.Gameplay.asmdef  balloon, slap, items, hazards, ragdoll
  Scripts/Player/      GameJam.Player.asmdef    gorilla locomotion, gloves, spikes, haptics
  Scripts/Net/         GameJam.Net.asmdef       Fusion behaviours, runner bootstrap, voice
  Scripts/Editor/      GameJam.Editor.asmdef    custom [CliCommand] tools, editor-only
  Config/              ScriptableObject tuning assets (BalloonConfig, SlapConfig, ...)
  Prefabs/  Scenes/  Materials/  Audio/
```

- Every script folder has an assembly definition, so a change recompiles one small assembly instead
  of all of Assembly-CSharp. Create the asmdef together with the folder.
- One `MonoBehaviour`/`NetworkBehaviour` per file, file named after the class (player builds break otherwise).
- Every tuning number from the GDD (slap force cap, balloon drag, leg time limits, ragdoll time)
  lives in a ScriptableObject in `Config/`, not in code, so it can be tuned live without a recompile.
- Build things as prefabs first, then place prefab instances in scenes.

## 4. Efficiency rules

- Edit `.cs` files with the file tools, then `unity recompile --project-path ... --format json`
  (seconds) to check them. Never run `unity build` just to see if code compiles.
- Batch scene work: write one C# snippet in `.claude/unity-snippets/` and run it with
  `unity command script eval_file`, instead of ten separate `gameobject`/`component` calls.
- Tune in Play mode with `script eval` against the config ScriptableObject; write the final values
  back to the asset once the user approves them.
- Read only what you need: `scene get_hierarchy` for structure, `console get -- --level error --tail 50`
  for errors. Capture screenshots only when the visual result is the question, with `--save_path`.
- Parse `--format json`; branch on the exit code and `errors[0].code`. Never merge stderr into stdout.
- If the Editor is in Safe Mode (compile errors), `unity status` lists the errors: fix the code first.
- If a command times out or the Editor drops, stop and tell the user; don't loop or fall back to YAML edits.

## 5. Jam scope

Day 1 make-or-break: a slap that feels great with friends online. Follow the GDD's must / should /
stretch list and cut order. Don't build should/stretch items while a must-have is unfinished.

## 6. Git

Small commits per feature, message `feat|fix|tune|chore: <what>`. Commit only when the user asks.
Before committing: `unity recompile` passes, console has no new errors, scene saved (`editor save_all`).
