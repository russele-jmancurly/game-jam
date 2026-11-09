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
- Splash: `Scenes/Splash.unity` (build index 0) shows the Curly Blue logo (`Art/UI/CurlyBlueLogo.png`) on a dark backdrop
  while `Music_Backyard` plays (at `AudioConfig.SplashMusic`, louder than in game; the user didn't want a separate jingle),
  then loads Prototype after 8 s (`StudioSplash` in Gameplay/Flow; it loads the game in the background; the logo is placed once, 5 m
  in front of the player's view, and stays there (the user doesn't want it to follow the head); it waits for the headset to
  report a pose first, since placing it on a fixed timer left it out of sight in the headset). Rebuild with `GameJam.Editor.SplashSceneBuilder.BuildFromCli()` (menu: Game Jam > Build Splash
  Scene). The prototype builder keeps the splash first in the build settings.
- Photon: Fusion 2.1.4 only. Never import PUN 2 or the Asset Store Photon Voice 2 package: it breaks
  Fusion's Photon Realtime (see the `fusion-networked` skill). Without a Fusion App ID the game runs
  offline in Fusion's Single mode.
- Look: the art bible is `Scripts/Core/ArtPalette.cs` (style, palette, mood in its summary); every environment
  colour comes from it. `EnvironmentLightingBuilder` (sun, trilight ambient, procedural sky, fog, global Volume
  without bloom since the Quest URP asset has HDR off; post-processing renders in PC mode only unless
  `PlayerRigConfig.PostProcessingInHeadset`) and `EnvironmentDressingBuilder` (collider-free, static-batched set
  dressing, including the Drive food props in `Art/Props/Food` with `T_ColorAtlas_v1`) run inside the prototype
  builder. Dressing never gets colliders, so it can't change gameplay. No full-screen post-processing on Quest (an extra
  full-screen pass per eye, 1.5-3 ms); the look comes from free scene-side work instead: mown lawn stripes (`T_LawnStripes`),
  a hazier lawn outside the fence, deck planks, pickets shaded darker at the base (baked AO), soft ground-shadow blobs under
  tall set pieces (`EnvironmentDressingBuilder.BuildGroundShadows`; real shadows only reach 14 m), glossy toy materials
  (items via `PlaceholderPrefabBuilder.GlossyToy`, the pillow stays matte), striped crowd stands, and a spinning golden
  ring on every item spot (`BackyardCourseBuilder.ItemSpotRing`).
- Set-piece models: `Art/Set/*.fbx` (tree, paddling pool, grill, picnic table, playground pieces, rotary clothesline, fence
  picket) from `tools/blender/scripts/build_set_pieces.py` (`--check` verifies walkable tops against the colliders).
  `SetPieceModels.Apply` (end of `BackyardCourseBuilder.Build`) places each at its anchor and hides the primitives it
  stands for, which keep their colliders; a missing FBX leaves the primitives. Item models: `Art/Props/Items/<Item>.fbx`
  from `build_item_props.py`, used automatically by `PlaceholderPrefabBuilder` when present.
- Map: a trail of stations (GDD: Levels). Every hoop hangs at a set piece that is its obstacle, ~14 m apart and getting
  harder: lawn warm-up (2.4 m), 1 m in front of the washing line (2.4, sheets swing into it), over the pool off the
  diving board (4.0), above the merry-go-round with a trampoline beside it (3.1; a ride: `MovingObstacle.CarriesPlayers` turns riders with it
  via `Player.Ride`, never knocks them off, and lets go as soon as they hop; its handle bars have no colliders), over the grill (3.25, flames on a beat,
  a fan blowing across; launch from ~4 m back), above the low tree branch via the mushroom pad (6.15), then the finish pad
  under a swinging pinata. Rule from the playtest: nothing stays that doesn't shape the way to a hoop (the swing set,
  seesaw, slide, sandbox, picnic table, bug zapper, second washing line and yard-wide movers were cut). Stations are
  `BackyardCourseBuilder` centre constants; `TrailHoops` and `TrailCheckpoints` are offsets from them (checkpoint k = open
  lawn on the working side of the approach to the next hoop, so nobody respawns in the pool or on the grill). Yard 52 x 100 m
  (`HalfWidth` 26, `SouthEdge` -16, `NorthEdge` 84) with a lawn strip west for the giant; `RoamerAvoidCircles` is computed
  from the stations, `RoamerWanderPoints` are literal. A cleared hoop shrinks away until the next run. Hoops count only during a run. Lobby: the crew waits on
  the start stage inside clear glass walls (slippery, 5 m, on Default so hands meet them) with a "slap the red button" sign
  and an arrow hovering over the button (`BackyardCourseBuilder.BuildLobbyOnly`, toggled by `LobbyOnly` from the networked
  run state); the balloon is hidden there (`GameState.IsBalloonShown`). Slapping the button drops the walls and shows the
  balloon on its station, frozen through the countdown, live at GO. `lobby_check.cs` tests it, `lobby_cams.cs` frames it;
  `gorilla_test_suite.cs` lowers the walls while it runs. Movers are
  `MovingObstacle` on the shared `CourseClock` (no network cost). After moving anything, in Play mode run
  `.claude/unity-snippets/map_check.cs` (item spawns, giant spots vs its avoid circles, all 4 player slots per checkpoint,
  overlapping set pieces, every collider vs rings > 1.2 m and waiting balloons > 0.35 m; hoop 2's sheet and hoop 5's
  flames come close on purpose) and `hoop_reach_check.cs` (each hoop must pass with real slaps at 4.5-7.5 m/s from where a
  player launches it). `review_cameras.cs` has an overview and a player's-eye view from the start.
- Movement: `Scripts/Player/Locomotion/` is a port of GorillaPlus (github.com/baggZ-idk/GorillaPlus), keep `Player.cs`
  close to upstream and list every project change in that folder's README. Feel tuning (jump, slide, arm length, tap
  volume, haptics) is in `PlayerRigConfig` and applied live by `LocalRig`. While a Touch controller isn't tracked (asleep, set
  down, Quest in hand-tracking mode) `LocalRig` parks its hand by the head, turns hand contacts off and shows "Pick up your
  controllers" on the wrist; when both return it re-seats the hands and lifts the player out of the ground if they sank
  (untracked hands used to stay planted on the floor and drag the body into it). The left thumbstick walks you the way you
  look (`LocalRig.StickWalk`, `PlayerRigConfig` Stick Movement: speed, dead zone, step height; sweeps head and body so walls
  stop you, moves planted hands along through `Player.Carry`). Hands caught inside geometry are pushed back out
  (`Player.UnembedHand`); run `hand_stuck_test.cs`, `stick_walk_test.cs` and `gorilla_test_suite.cs` after locomotion
  changes. `locomotion_record.cs`/`_report.cs`
  record a headset session to diagnose movement. Slippery/bouncy set pieces: a `Surface`
  component, or a PhysicsMaterial with static friction < 0.3 / some bounciness. Tap and slide sounds are generated by
  `GeneratedAudio` into `Audio/Generated`. Don't take GorillaPlus's networking, voice or PlayFab code.
- Show characters (map entities, not playable): `Config/CharacterRoster.asset` lists Jmancurly (host, on a podium on
  the start stage) and Squoof (commentator, on the booth roof); `BackyardCourseBuilder.BuildShowCrew` places them. Each
  character also roams the yard as a giant (one `MischiefMaker` per roster entry). Game models live at `Assets/GameJam/Characters/<Name>/<Name>_Game.fbx`, made
  from source files with `tools/blender/scripts/prepare_character.py` (static mesh, upright, packed textures written to
  `<Name>_Game.fbm` at <= 1024 px, `--budget Mesh=N` for one dense part like hair; players <= 6000 tris, map
  characters ~10000); then `tools/blender/scripts/rig_character.py <Name>_Game.fbx <Name>_Game.fbx` rigs it in place
  (fitted 19-bone humanoid, auto weights, one skinned mesh with one UV channel, clips Idle/Walk/WindUp/Slap/Wave; check
  poses with `render_pose.py` and the face with `render_face.py`; heat weights pinched the shoulders (the deltoid stayed
  on the collarbone bone while the arm swung ~80 degrees down at one joint), so run `fix_shoulders.py <x>_Game.fbx <x>_Game.fbx`
  after rigging: it moves the deltoid to UpperArm and, in every clip, lets the collarbone take 30 % of the arm's swing
  (`--clavicle`), counter-rotating the upper arm so it points where it did
  (`--hide-under <skinMat>:<clothMat>` also deletes skin poking through a shirt; Squoof: `Squoof:outfit_top`), and rebuild the
  prototype after any character re-export, which relinks the clips into the `CharacterDefinition`; parts' differently named UV maps once merged into unread
  extra channels and left Jman with white eyes). `ShowCharacterImport` names and loops the clips; the prototype builder links model and clips into
  `CharacterDefinition`, and `CharacterVisual` plays them through `CharacterAnimator` (Playables, no controller asset).
  A placeholder shows while a model is missing.
  Sources go in `ArtSource/<Name>/` with their `Textures/` (gitignored); the user drops large ones into `Assets/GameJam/`,
  then move them out. The Drive connector can't download files over 10 MB: ask the user to fetch those by hand.
- Players are all the same porcupine in natural colours (no per-player tint; name tag in the player colour): the skinned
  body `Art/Characters/PorcupineBody.fbx` (our own mesh, generated by `tools/blender/scripts/build_porcupine_body.py`;
  skeleton layout and proportions follow the team's reference rig `ArtSource/UGPlayerModel (1).blend`, never ship that
  mesh). `GorillaBodyRig` (Gameplay) solves two-bone arm IK to the synced gloves and turns the head; the tan paws are
  the gloves, and you see your own arms (`PlayerRigConfig.ShowOwnArms`). Each paw is a palm, three stubby fingers and a
  thumb on finger bone chains; the grip closes it into a fist (analog grip in the headset, G in PC mode, always closed
  while holding an item; `LocalRig.GripAmount` -> `GorillaBodyRig.SetGrip`, synced as a byte per hand in `NetworkAvatar`).
  Curl angles are on `GorillaBodyRig` and mirrored in `tools/blender/scripts/render_paw.py` (open/closed paw close-ups);
  `paw_grip_check.cs` checks it in Play mode. Change the body in the Blender script, then rebuild the prototype (or only
  the avatar prefab: `PlaceholderPrefabBuilder.BuildAvatar` + `RebuildPrefabTable`).
- Items help with hoops (GDD: Items): `ItemDefinition.HoopAssist` steers an item hit through the nearest open hoop
  (`HoopAim` solves the balloon flight); `ItemDefinition.Effect` starts with a press of the controller trigger (PC test
  mode: F; read as pressure with hysteresis) and runs its full `EffectSeconds` while held, following the aim: bubble wand `Updraft` (rises from the ring), leaf blower
  `HoopGust` (from the nozzle where you aim; homes onto a hoop within 25 degrees of the aim). Thrown items no longer set
  anything off. Effect items are single use: once the effect has run they vanish (`IsSpent`) and come back to their spot
  after `ItemDefinition.UsedUpRespawnSeconds` (30 s). `ItemDefinition.HoldOffset`/`HoldEuler`/`EffectEmitter` (rewritten by `PlaceholderPrefabBuilder.ApplyHoldPose`,
  fitted to each model) make the business end point out of the fist (grip +Z). `item_effect_test.cs` tests the effects.
- `MischiefMaker` is a roaming giant: `NetworkBootstrap` spawns one per roster character (Jmancurly and Squoof, at
  `BackyardCourseBuilder.RoamerSpawns`), each fixed to its character via `AssignCharacter` (`MischiefMaker.Active` lists them;
  master-client owned). It only picks wander spots it can walk to in a straight line clear of its avoid circles
  (`HasClearPath`; set pieces chain into walls, e.g. pool + diving-board stand + clothesline), reaching far ones in hops, and
  `map_check.cs` verifies every spot is reachable that way. It picks a destination among all spots (far ones and ones
  away from the other giant likelier) and walks there in hops along the shortest route over the spots that see each other
  (`PickWanderTarget`, Dijkstra over `_links`): picking only straight-line spots kept a giant on one side (the user wants
  them roaming the whole yard). `giant_roam_coverage.cs` measures it (Fusion ignores timeScale: sample in real time). If it still makes no progress for 4 s it picks a new spot, or
  gives up a chase for a cooldown: eased walking, idle pauses, then
  WindUp (held over the telegraph) and Slap clips timed so the hand lands on the hit. With an unrigged model it falls back
  to procedural motion and a builder-made `SlapArm`/`SlapHand`. Movement and slap tuning: `CharacterRoster` (`Roamer*`).
  The host and commentator on stage idle and wave now and then.
  A thrown item (not swung or loose; `NetworkItem.HandleContact` -> `RequestKnockDown` RPC to the master) knocks a giant down
  for `RoamerKnockdownSeconds` (3 s, mood `KnockedDown`): every client drops a real ragdoll built at runtime from the rig's
  bones (`GiantRagdoll`: 10 bodies, the head rides on the chest because a hinged head flopped back; get-up resets every bone
  to its standing pose before the Animator runs, since the clips don't drive the neck) with `DizzyStars` over its head. The
  body capsule is off and it bowls nobody over while down; it stands up where its hips landed, then cools down
  (`RoamerBonkCooldown`). Unrigged models tip over instead. `giant_knockdown_check.cs` tests it with a real throw.
- The balloon is stationed (`NetworkBalloon.IsStationed`): after spawning, a reset or a pop it hovers and bobs at its
  `SpawnPoint.BalloonPosition` until the first hit (start: 1.4 m above the stage, hidden until the red button). Feel: a hand slap sends it the way the hand moved, as hard as it
  moved (`SlapConfig.BalloonVelocityScale`, capped by `BalloonConfig.MaxHitSpeed`); gentle taps get extra lift (SlapTapLift),
  hard slaps almost none (SlapHardLift). Linear Drag + low Gravity make it hang and float down (Gravity 0.9 / Drag 1.1 = ~0.82 m/s: 0.64 m/s felt too slow and 1.1 m/s
  too fast; all hoops still pass `hoop_reach_check.cs`, which needs a run going since the balloon ignores hits in the lobby). Keep
  QuadraticDrag at 0: `HoopAim` assumes linear drag. Items and the giant bypass the slap shaping. Hits squash it along the hit direction, flash it white, rumble harder for harder slaps, and play one of three generated
  `Balloon_Slap_*` sounds for everyone. `balloon_smoothness.cs` (per-frame motion), `balloon_slap_flight.cs` (rise/travel of a given slap) and
  `slap_speed_record.cs` (your real slap speeds in a headset) are the tuning tools. Online, a slap by anyone but the
  balloon's owner (the master client) is predicted on the slapper's client (`NetworkBalloon.PredictHit`): the sound and
  squash play at once and the visuals (the prefab's `View` child, never the networked body) fly the same trajectory
  until the owner's hit arrives (networked `HitCount`), then blend onto it over 0.3 s; `RpcApplyHit` is not
  tick-aligned. Before this the slapper waited two network trips to see or hear their own hit.
  `predicted_hit_check.cs` fakes the latency offline and checks the hand-over.
- Turn cue: the balloon always shows the hitter colour (candy pink before anyone hits it, `PlayerPalette.Neutral`); whoever hit it last (pass rule on, during a run) sees a "not
  your turn" icon (balloon + red no-sign, `T_NotYourTurn`) low in the middle of their view, drawn over the arms
  (`TurnIndicator` on the head camera, `PrototypeSceneBuilder.BuildTurnIndicator`); it fades out when someone else hits
  it (a giant's hit is nobody's and keeps it). `turn_indicator_check.cs` tests it.
- Headset lighting: the Quest has no HDR and no tonemapping, so bright colours clip to white; `LocalRig` scales sun and
  ambient by `PlayerRigConfig.HeadsetLightScale` (0.8; the old 1.35 boost washed the build out). `light_probe.cs` compares
  scales from the same view.
- Bounce pads (trampoline, diving-board spring, tree mushroom) launch players only (`BouncePad._balloonFactor` 0): a
  balloon relaunched by a pad hovered over it forever.
- Easy mode: players never die or get knocked down. Water only pops the balloon (players can wade in the pool), and with
  `KnockbackConfig.KnockdownsEnabled` off (default) the giant, moving obstacles, flames, pop blasts and other players only
  give a light shove (`PlayerPushForce`, `LocalRig.Push`): no ragdoll, you keep your hands and items. `LocalRig.Knock` and
  `NetworkAvatar.RpcKnockback` turn every knockdown into that shove; `knockdown_check.cs` tests it. Only falling below
  `KillHeight` (through the map) respawns you.
- Sound and ambience: every sound is synthesized by `GeneratedAudio` into `Audio/Generated` (balloon slaps and pop, hoop
  chime, win fanfare, trampoline boing, bubble and wind loops, background music) at 44.1 kHz, normalised per kind; loops
  are rendered long and crossfaded into their start (seamless), effects PCM, music Vorbis (loops gaplessly). Clips carry a
  version stamp (importer userData, `GeneratedAudio.Version`): bump it to regenerate. Music: `Music_Backyard`, an upbeat
  116 BPM groove (Am F C G: four-on-the-floor kick, clap, swung hats, syncopated bass, pumping pad; no lead, the user
  didn't want the bell-like hook), on the scene's `Music` object at volume 0.15 (0.25 was too loud). Mix: `Config/AudioConfig.asset` (Master, Music,
  Ambience, Effects, fades), applied live by `SoundLevel` on every source; loops start/stop by fading
  (`SoundLevel.FadeIn/FadeOut`), the hand-slide hiss fades in `Player` (it used to restart every frame: stutter). Taps and
  slide: `PlayerRigConfig.TapVolume/SlideVolume`. `audio_loop_check.cs` checks seams and levels;
  `PlaceholderPrefabBuilder.Sound` makes the AudioSources.
  Hooks: `HoopCheckpoint` (chime + confetti when a hoop clears), `GameState` (2D fanfare on a medal finish),
  `NetworkBalloon.RpcPlayPopEffects`, `BouncePad.PlayBounce` (from `RigHazardSensor`), `ItemEffectZone` (loop while
  the effect runs) and the fan's `AirStream`. `AmbientEffectsBuilder` adds yard pollen and falling tree leaves (no
  colliders; hoops have no ambient VFX, by request). VFX are pooled particle systems fired with `Emit` (no Instantiate):
  `BurstEffects` (scene object under Ambience: hoop-clear confetti strips in the candy colours, balloon-pop rubber shreds in
  the hitter's colour plus an air puff; `PopBurstEffect`/`HoopCheckpoint` fall back to cubes without it). Item effects:
  bubbles are `T_Bubble` (clear, shimmering rim, highlight; never rotated) that swell, wobble and pop in sparkles (death
  sub-emitter); the leaf blower adds a `NozzleBlast` of air puffs; wind leaves and falling leaves use `T_Leaf`
  (`PlaceholderPrefabBuilder.LeafColors`). Particle textures come from `GeneratedTextures` (Bubble, Leaf, Shred,
  ConfettiStrip, Clod) through `PlaceholderPrefabBuilder.ParticleMaterial`. Hand taps sound by surface: the builder sorts the
  yard's collider materials by name into grass, wood, metal and plastic (`PrototypeSceneBuilder.SurfaceKinds`, unmatched ones
  keep the default taps) with three generated variants each; on grass (`SurfaceSound.KicksUpDirt`) `HandTapEffects` on the
  rig flicks up clods and grass bits with a dust puff (local only). `surface_check.cs` lists the sorting, `fx_check.cs` fires
  and captures the bursts and dirt. The start stage has a `PlanarMirror` (off-axis render-texture camera, only
  near and in front of it, shows your own hidden body through `GorillaBodyRig.All`). Check them in Play mode with
  `ambience_check.cs` (sounds per hook, emitters, fan stream; review cams) and `mirror_view.cs` (views the mirror; run it a second
  time to restore the Head as main camera). A medal finish plays the fanfare and a generated crowd cheer
  (`GeneratedAudio.CrowdCheer`, `Crowd_Cheer`) for everyone and pops big confetti round each player's own view
  (`WinCelebration`, through `BurstEffects.BigConfetti`; the confetti pool is 900). A finished run (medal or time up) returns to
  the lobby after `RunConfig.ResultSeconds` (10 s): hoops reset, balloon back on station at the start, crew respawned on the stage, every item back
  on its spot (`NetworkItem.ReturnHome`, home spot networked so late joiners agree). A run is 6 minutes (`RunConfig.RunSeconds`
  360; medals Silver 3900 / Gold 4500, raised by 1800 with the longer clock); when time runs out everyone hears a referee
  whistle (`Whistle_TimeUp`, 2D on `GameState`) and sees "TIME'S UP!" with a countdown to the lobby (`ResultBanner` on the
  head camera, drawn on top). `time_up_check.cs` tests the flow. Wind (box fan,
  leaf blower) is drawn by `PlaceholderPrefabBuilder.StyleAsWind`: trail wisps on scrolling noise plus a few leaves.
- Finding the balloon: `BalloonLocator` (on the balloon, local only) shows a bobbing arrow over it (no beam: the user had it removed) and, when
  it is out of your view, an arrow at the edge of your view with its distance, only while it is stationed (start, reset,
  after a pop); the first hit hides everything (user's call: no indicator while it is in play). The balloon has a dark ink
  outline: `Shaders/Outline.shader` (`GameJam/Outline`, inverted hull, stereo-instancing safe) on a child sphere of `Visual`.
- Lobby picture signs (lobby only, both built by `BackyardCourseBuilder.BuildPictureSign`): "How to play" inside the north glass
  wall, straight ahead of the spawn (slap the balloon, through a hoop, clear all 6, fly it to the finish, plus the two pop
  rules), and "Items knock out the giants" inside the east wall (grab an item, throw it, bonk the giant). Icons
  `Art/UI/Icon_*.png` are rendered by `tools/blender/scripts/build_lobby_icons.py` (transparent, cartoon outline); rerun it
  and rebuild the prototype to change them. Course TextMeshes use `Shaders/WorldText.shader` (`GameJam/WorldText`, depth
  tested; `DepthTestCourseText`): the built-in text shader reads its depth test from a GUI global and drew every label
  through walls and signs.
- Layer 8 is `Balloon` (named by `ProjectLayers`, used through `GameLayers.Balloon`).
- Layer 9 is `Item`: loose items are off the Default layer so gorilla hands never grip them, and each item ignores
  collisions with the local rig's solid colliders (`NetworkItem.IgnoreLocalRig`), so items never push you. Only an item
  another player threw or swung shoves you (simple mode); loose ones never do.
- Layer 10 is `Ragdoll` (a knocked-down giant's bones): `PhysicsLayerRules` lets it touch only Default and itself, so a
  falling giant never hits players (Ignore Raycast), the balloon or items.
- Testing without a headset: PC test mode (keyboard and mouse) turns on automatically, or force it with
  `PlayerRigConfig.ForceDesktopMode`. Solo, the pass rule is suspended (`BalloonConfig.AllowSoloDoubleHits`).
  All players join one Photon region (`NetworkBootstrap._fixedRegion`, default `asia`), or they can miss each other.
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
- Quest APK (Editor open, target already Android): `unity command build run ... -- --target Android --output-path
  Builds/Quest/BalloonDrop.apk --scenes '["Assets/GameJam/Scenes/Prototype.unity"]' --confirm`, then poll `build status`
  (~8 min, IL2CPP). `Builds/` and `*.apk` are gitignored. Builds rewrite the URP config prefilters and the preloaded
  assets; commit those. The URP global settings must stay at `m_AssetVersion` 9 (URP 17.3), or the build refuses to start.
- If a command times out or the Editor drops, stop and tell the user; don't loop or fall back to YAML edits.

## 5. Jam scope

Day 1 make-or-break: a slap that feels great with friends online. Follow the GDD's must / should /
stretch list and cut order. Don't build should/stretch items while a must-have is unfinished.

## 6. Git

Small commits per feature, message `feat|fix|tune|chore: <what>`. Commit only when the user asks.
Before committing: `unity recompile` passes, console has no new errors, scene saved (`editor save_all`).
