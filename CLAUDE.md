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

## 1b. Drive Blender with the Blender CLI, never the Blender MCP server

- All Blender work goes through `tools/blender/blender.sh <script.py> [args]` (headless Blender 5.2 LTS).
  Do not call Blender MCP tools (`mcp__blender__*`); `.claude/settings.json` denies them in this project.
- Load the `blender-cli` skill first. Use its scripts: `inspect.py` (what's in a model), `render_preview.py`
  (see it), `export_unity_fbx.py` (Unity-ready FBX into `Assets/GameJam/Art/`); write new bpy scripts in
  `tools/blender/scripts/` on top of `blender_common.py`.
- Headless Blender never touches a file the user has open in Blender; ask them to save and close it first.

## 2. Which skill to load

| Task | Skill |
|---|---|
| Any Editor operation (scene, prefab, component, play mode, console) | `unity-cli` + `unity-cli-loop` |
| Any Blender / 3D-model work (inspect, preview, fix, export FBX) | `blender-cli` |
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
`Photon/` untouched (vendor code). The one project edit inside `Photon/` is the `AssembliesToWeave`
entry for `GameJam.Net` in `NetworkProjectConfig.fusion`.

```
Assets/GameJam/
  Scripts/Core/        GameJam.Core.asmdef      config SOs, enums, scoring, CourseClock, CourseLayout,
                                                interfaces (ISlappable, IGrabbable, IRunStatus...) + GameServices
  Scripts/Gameplay/    GameJam.Gameplay.asmdef  BalloonPhysics (plain C#), course hazards, placeholder FX
  Scripts/Player/      GameJam.Player.asmdef    LocalRig, SlapHand, HandGrip, hazards sensor, desktop test
                                                input, Locomotion/ (GorillaPlus port of GorillaLocomotion, MIT)
  Scripts/Net/         GameJam.Net.asmdef       every NetworkBehaviour: GameState, NetworkBalloon,
                                                NetworkItem, NetworkAvatar, NetworkBootstrap (Fusion-woven)
  Scripts/Editor/      GameJam.Editor.asmdef    PrototypeSceneBuilder + BackyardCourseBuilder (open yard)
                                                + placeholder prefab, lighting and dressing builders
  Config/              tuning assets: Balloon/Slap/Run/PlayerRig/KnockbackConfig, Items/Item_*.asset
  Prefabs/Net/         generated network prefabs (do not hand-edit; change the builder)
  Scenes/Prototype.unity  generated placeholder prototype (Scene/Game.unity is the untouched original)
  Materials/Placeholder/  Audio/
```

- Assembly dependencies only point one way: Core ← Gameplay ← Player ← Net ← Editor. Gameplay and
  Player never reference Fusion; they reach networked objects through the Core interfaces.
- Any new assembly containing a `NetworkBehaviour` must be added to `AssembliesToWeave` in
  `Assets/Photon/Fusion/Resources/NetworkProjectConfig.fusion`, or Fusion won't weave it.
- C# style: `GameJam.<Assembly>` namespaces, `[SerializeField] private _camelCase` fields, PascalCase
  members, `sealed` components, `Configure(...)` methods for builder wiring, braces on every `if`.
- Rebuild the placeholder prototype with
  `unity command script eval ... -- --code 'return GameJam.Editor.PrototypeSceneBuilder.BuildFromCli();'`
  (menu: Game Jam > Build Prototype Scene). It keeps existing config assets.
- Photon: Fusion 2.1.4 only. Never import PUN 2 or the Asset Store Photon Voice 2 package: it breaks
  Fusion's Photon Realtime (see the `fusion-networked` skill). Without a Fusion App ID the game runs
  offline in Fusion's Single mode.
- Look: the art bible is `Scripts/Core/ArtPalette.cs` (style, palette, mood in its summary); every environment
  colour comes from it. `EnvironmentLightingBuilder` (sun, trilight ambient, procedural sky, fog, global Volume
  without bloom since the Quest URP asset has HDR off; post-processing renders in PC mode only unless
  `PlayerRigConfig.PostProcessingInHeadset`) and `EnvironmentDressingBuilder` (collider-free, static-batched set
  dressing, including the Drive food props in `Art/Props/Food` with `T_ColorAtlas_v1`) run inside the prototype
  builder. Dressing never gets colliders, so it can't change gameplay.
- Map: an open backyard (GDD: Levels), not a linear course. `BackyardCourseBuilder` holds the zone layout constants
  (pool, washing line, tree, playground, BBQ, porch), the six `HoopCheckpoint`s (any order; checkpoint k = beside hoop
  k-1), sized from measured reach (`HoopRingRadius`, `HoopAboveFloor`, `HoopAbovePerch`: eyes ~0.65 m up, standing reach
  ~1.25 m, a good jump ~2.3 m; re-run `.claude/unity-snippets/hoop_reach_test.cs` after moving one), `BalloonGround` surfaces
  (balloon pops on touch, players walk freely) and every mover. Passing objects are
  `MovingObstacle` in `Path` mode on the shared `CourseClock` (no network cost); check new routes don't clip set pieces.
- Movement: `Scripts/Player/Locomotion/` is a port of GorillaPlus (github.com/baggZ-idk/GorillaPlus), keep `Player.cs`
  close to upstream and list every project change in that folder's README. Feel tuning (jump, slide, arm length, tap
  volume, haptics) is in `PlayerRigConfig` and applied live by `LocalRig`. Slippery/bouncy set pieces: a `Surface`
  component, or a PhysicsMaterial with static friction < 0.3 / some bounciness. Tap and slide sounds are generated by
  `GeneratedAudio` into `Audio/Generated`. Don't take GorillaPlus's networking, voice or PlayFab code.
- Show characters (map entities, not playable): `Config/CharacterRoster.asset` lists Jmancurly (host, on a podium on
  the start stage) and Squoof (commentator, on the booth roof); `BackyardCourseBuilder.BuildShowCrew` places them, and the
  mischief maker picks one of them per run. Game models live at `Assets/GameJam/Characters/<Name>/<Name>_Game.fbx`, made
  from source files with `tools/blender/scripts/prepare_character.py` (static mesh, upright, packed textures written to
  `<Name>_Game.fbm` at <= 1024 px, `--budget Mesh=N` for one dense part like hair; players <= 6000 tris, map
  characters ~10000); the prototype builder links them automatically and shows a placeholder while one is missing.
  Sources go in `ArtSource/<Name>/` with their `Textures/` (gitignored); the user drops large ones into `Assets/GameJam/`,
  then move them out. The Drive connector can't download files over 10 MB: ask the user to fetch those by hand.
- Players are all the same porcupine in natural colours (no per-player tint; name tag in the player colour): the skinned
  body `Art/Characters/PorcupineBody.fbx` (our own mesh, generated by `tools/blender/scripts/build_porcupine_body.py`;
  skeleton layout and proportions follow the team's reference rig `ArtSource/UGPlayerModel (1).blend`, never ship that
  mesh). `GorillaBodyRig` (Gameplay) solves two-bone arm IK to the synced gloves and turns the head; the tan paw mitts are
  the gloves, and you see your own arms (`PlayerRigConfig.ShowOwnArms`). Change the body in the Blender script, then
  rebuild the prototype.
- `MischiefMaker` is the roaming giant (Jmancurly or Squoof per run, master-client owned, telegraphed bonks).
- Layer 8 is `Balloon` (named by `ProjectLayers`, used through `GameLayers.Balloon`).
- Testing without a headset: PC test mode (keyboard and mouse) turns on automatically, or force it with
  `PlayerRigConfig.ForceDesktopMode`. Solo, the pass rule is suspended (`BalloonConfig.AllowSoloDoubleHits`).
  Two players in the Editor: Multiplayer Play Mode (Window > Multiplayer > Multiplayer Play Mode), which
  needs the Fusion App ID.

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
