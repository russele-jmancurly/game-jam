# Prickly Pop: Claude working rules

Prickly Pop (working title "Don't Let the Balloon Drop"; Android package `com.curlyblue.pricklypop`). Co-op VR physics game for the Curly Blue three-day jam. Unity 6000.3.8f1, URP, OpenXR (Meta Quest),
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
  Scenes/Tunnel.unity, Tutorial.unity  generated: the mole tunnel and the tutorial burrow (see Tutorial below)
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
  then loads the Tutorial after 8 s (`StudioSplash` in Gameplay/Flow; it loads the next scene in the background; the logo is placed once, 5 m
  in front of the player's view, and stays there (the user doesn't want it to follow the head); it waits for the headset to
  report a pose first, since placing it on a fixed timer left it out of sight in the headset). Rebuild with `GameJam.Editor.SplashSceneBuilder.BuildFromCli()` (menu: Game Jam > Build Splash
  Scene). The prototype builder keeps the splash first in the build settings.
- Startup flow (the user's): Splash -> Tutorial (one player, offline) -> lobby. A new player learns alone first, and both
  leaving the tutorial (either of its holes; the first room's says "jump in to skip the tutorial") and finishing it
  (`TutorialGuide` sends them down the tunnel 5 s after the last lesson) end in the lobby (Prototype), where the crew meets.
- Tutorial (also reachable again from the lobby): a mole hole in the start stage's south-east corner (`BackyardCourseBuilder.BuildMoleHole`, a part of
  `LobbyOnly`, so it is gone during a run) takes the one player who gets into it (`MoleHole`, found by `RigHazardSensor`)
  Lobby -> `Scenes/Tunnel.unity` -> `Scenes/Tutorial.unity`, and the tutorial's holes take them back the same way.
  `SceneTravel.Go` (Gameplay/Flow) fades the view (`ScreenFade`: a black quad on the head camera, no post-processing), leaves
  the room first (`GameServices.LeaveSession` -> `NetworkBootstrap` shuts the runner down: it outlives a scene load, so
  without that the player stays in the room as a ghost) and loads Tunnel, where `TunnelTransition` rushes soil rings past a
  head-tracked camera (up when falling, down on the way back) while the destination loads; the rig's `TunnelArrival` plays
  the landing thud and soil burst. Coming back, Prototype's bootstrap joins the same room again. The tutorial is a burrow of
  five rooms in a row, one lesson each (move, slap + hoop, what pops it, items, the giant), each closed by a `SectionGate`
  that `TutorialGuide` (Net) opens when the room's goal is met (the user asked for it "by section, not all at once"; read
  as one lesson at a time in the game, to be confirmed); it also moves the balloon and the respawn point on (`GameState.PracticeCheckpoint`). It runs the real prefabs
  offline: `NetworkBootstrap.ConfigureTutorial` forces Single mode, practice rules (`GameState.IsPractice`: balloon live
  from the start, no lobby, clock or result, a cleared hoop reopens after 4 s) and one giant kept in its den
  (`MischiefMaker.ConfigureYard`). Builders: `TunnelSceneBuilder.BuildFromCli()` and `TutorialSceneBuilder.BuildFromCli()`
  (menu: Game Jam > Build Tunnel / Tutorial Scene); rebuild the tutorial after the prototype whenever prefabs or the rig
  change (both regenerate the same prefabs). Layout constants are in `BackyardCourseBuilder.Tutorial.cs`. Sounds:
  `Mole_Dive`, `Tunnel_Rush`, `Tunnel_Land`, `Gate_Rumble`, `Lesson_Done`. Tests (Play mode): `tutorial_dive.cs` puts the
  player in the scene's hole, `tutorial_status.cs` reports the trip, `tutorial_lessons_check.cs` plays all five sections,
  `tutorial_cams.cs` / `tutorial_hole_cam.cs` frame them.
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
  (items via `PlaceholderPrefabBuilder.GlossyToy`, the pillow stays matte), the critter audience, and a spinning golden
  ring on every item spot (`BackyardCourseBuilder.ItemSpotRing`).
- Set-piece models: `Art/Set/*.fbx` (tree, paddling pool, grill, picnic table, playground pieces, rotary clothesline, fence
  picket, and `Cactus`: the potted cactus `BackyardCourseBuilder.CactusPot` uses for anything "spiky" that pops the balloon,
  wrapped in capsule colliders for pot, body and arms; the user rejected the old cylinder-with-a-red-slab placeholder) from `tools/blender/scripts/build_set_pieces.py` (`--check` verifies walkable tops against the colliders).
  `SetPieceModels.Apply` (end of `BackyardCourseBuilder.Build`) places each at its anchor and hides the primitives it
  stands for, which keep their colliders; a missing FBX leaves the primitives. Item models: `Art/Props/Items/<Item>.fbx`
  from `build_item_props.py`, used automatically by `PlaceholderPrefabBuilder` when present.
- Map: a trail of stations (GDD: Levels) zigzagging west-east up the yard. Every hoop hangs at a set piece that is its
  obstacle, 18-25 m apart (the user asked for a bigger map with the hoops further apart; they were ~14 m) and getting
  harder: lawn warm-up (2.4 m), 1 m in front of the washing line (2.4, sheets swing into it), over the pool off the
  diving board (4.0), above the merry-go-round with a trampoline beside it (3.1; a ride: `MovingObstacle.CarriesPlayers` turns riders with it
  via `Player.Ride`, never knocks them off, and lets go as soon as they hop; its handle bars have no colliders), over the grill (3.25, flames on a beat,
  a fan blowing across; launch from ~4 m back), above the low tree branch via the mushroom pad (6.15), then the finish basket
  at the end of a fenced pen, past two swinging pinatas: a horizontal ring 2.9 m over the finish pad (out of reach even with a jump) with a solid rim,
  backboard and net; the balloon only finishes by dropping down through it (`FinishLine.Accepts`: moving down, centre inside
  the opening and within 0.25 m under the rim plane), so it can't be steered in from the side any more (playtest).
  `finish_check.cs` tests side, skim and drop entries. Rule from the playtest: nothing stays that doesn't shape the way to a hoop (the swing set,
  seesaw, slide, sandbox, picnic table, bug zapper, second washing line and yard-wide movers were cut). Stations are
  `BackyardCourseBuilder` centre constants; `TrailHoops` and `TrailCheckpoints` are offsets from them (checkpoint k = open
  lawn a few metres past hoop k, so a popped balloon has the whole gap and its obstacle to cross again; they used to sit right
  in front of the next hoop). Yard 52 x 144 m (`HalfWidth` 26, `SouthEdge` -16, `NorthEdge` 128:
  the extra room is all length, because 68 m wide felt too wide for players this small); `RoamerAvoidCircles`,
  `RoamerWanderPoints` (a 9 x 10 m grid minus spots within 2 m of a circle) and `RoamerSpawns` are all computed from the
  layout. The run is 10 minutes (`RunConfig.RunSeconds` 600; the user raised it from 8) for the ~135 m trail.
  Gap obstacles (`BackyardCourseBuilder.Obstacles.cs`, the user wanted the cactus used more and more challenge): every gap
  after the warm-up has one, each using the potted cactus (`CactusPot`, pops on touch) differently: to hoop 2 a cactus
  garden to weave through; to hoop 3 two cacti on a wagon sliding back and forth on rails (`MovingObstacle.ConfigurePingPong`);
  to hoop 4 a sprinkler whose turning head sweeps a gust (`WindVolume` under a spinning mover, which must not knock
  players) toward four cacti just past its reach; to hoop 5 two cactus pots swinging from frames; to hoop 6 a wall of five
  tall cacti to lob it over; and the finish pen (`BuildFinishPen`): the yard's own 7 m pickets down both sides of the last 19 m and across its
  end (a slapped balloon tops out near 6 m, so it can't be lobbed in), open only at the south, with two pinatas across it
  (38 degree swing, the second faster) and a cactus against each fence before the pad. The user found the pinatas could
  simply be walked around when they stood on open lawn; the giants stay out of the pen (avoid circles fill it). Their spots are computed from the hoops and checkpoints and feed the avoid circles. Models (`Art/Set`, from
  `build_set_pieces.py`): `Pinata` (a striped paper donkey; the user pointed out the old pink cube wasn't modelled),
  `SwingFrame`, `Wagon`, `PotHanger`, `SprinklerBase`, `SprinklerHead`; placed by `SetPieceModels.PlaceModel`, primitives
  stay as colliders. `map_review.cs` renders a top view and one shot per obstacle to `Screenshots/map_*.png`. A cleared hoop shrinks away until the next run. Hoops count only during a run. Lobby: the crew waits on
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
  stop you, moves planted hands along through `Player.Carry`), only while grounded (`LocalRig.IsGrounded`: a short sphere cast
  under the body capsule hits a walkable surface, 0.12 s grace for bumps; no steering mid-air; `stick_grounded_check.cs`). Hands caught inside geometry are pushed back out
  (`Player.UnembedHand`); run `hand_stuck_test.cs`, `stick_walk_test.cs` and `gorilla_test_suite.cs` after locomotion
  changes. `locomotion_record.cs`/`_report.cs`
  record a headset session to diagnose movement. Slippery/bouncy set pieces: a `Surface`
  component, or a PhysicsMaterial with static friction < 0.3 / some bounciness. Tap and slide sounds are generated by
  `GeneratedAudio` into `Audio/Generated`. Don't take GorillaPlus's networking, voice or PlayFab code.
- Show characters (map entities, not playable): `Config/CharacterRoster.asset` lists Jmancurly and Squoof in
  `Characters` (each roams the yard as a giant, one `MischiefMaker` per entry; the user cut the host podium and the
  commentator booth) and the MC in `Mc`: Gonzales, on a perch on top of the headline board (`BuildShowCrew`), not a giant,
  so never put him in `Characters`. He is a bald baby in a blue romper with a yellow belt and a red boxing glove on his own
  left hand; source `ArtSource/Gonzales/Gonzales3.blend` (18.8M-tri sculpt in 5 UDIM tiles). Rebuild his model with:
  `prepare_character.py ArtSource/Gonzales/Gonzales3.blend Characters/Gonzales/Gonzales_Game.fbx --max-tris 10000
  --sculpt-level 1 --udim-atlas 2048 --max-texture 2048 --exclude TEEH,TEETH --budget Head_002_001=3600,body=2400,
  clothes=1000,tape2.001=350,glove1=700,glove2=200,glove3=200,socks=500,nails=250,eyes.=400`, then `rig_character.py ...
  --plan baby` (big head, short legs, A-pose, glove left out when finding the arm, no arm weights on torso clothing),
  `add_mc_clips.py ... --glove L` (Cheer, Shadowbox), then `fix_shoulders.py ... --clavicle 0.12` (0.3 crushed his romper
  with his fists up), in that order, each in place. He idles, waves and shadowboxes now and then, and `McReactions`
  makes him punch the air whenever the critters cheer (`CritterCrowd.Cheered`); `mc_check.cs` tests it. (A little skin
  shows through his socks at the toes; body and socks share one material, so `--hide-under` can't separate them.)
  The board text is the MC's lines (`McBoard`, was `CommentatorBoard`). Game models live at `Assets/GameJam/Characters/<Name>/<Name>_Game.fbx`, made
  from source files with `tools/blender/scripts/prepare_character.py` (static mesh, upright, packed textures written to
  `<Name>_Game.fbm` at <= 1024 px, `--budget Mesh=N` for one dense part like hair; players <= 6000 tris, map
  characters ~10000); then `tools/blender/scripts/rig_character.py <Name>_Game.fbx <Name>_Game.fbx` rigs it in place
  (fitted 19-bone humanoid, auto weights, one skinned mesh with one UV channel, clips Idle/Walk/WindUp/Slap/Wave, plus Kick from
  `add_kick_clip.py <x>_Game.fbx <x>_Game.fbx` (in place on a rigged model, like `fix_shoulders.py`; rerun after any re-rig); check
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
  When the balloon isn't in reach during a run (never in the lobby, the countdown or the tutorial), it goes after the
  nearest player within `RoamerKickNoticeDistance` (`ChasePlayer`), plants its foot at `RoamerKickReach` (kept well over
  the body-bump contact), draws its leg back with a "!" (`KickWindUp`, the Kick clip, or WindUp then Slap without one) and
  kicks (`Kick`, `Giant_Kick` sound): a player still within 1.4 x reach is launched (`NetworkAvatar.SendGiantKick`, a
  separate RPC that skips the easy-mode cap) and ragdolled for `RoamerKickKnockdownSeconds`, then it cools down
  (`RoamerKickCooldown`). The balloon always wins over a player. `giant_kick_check.cs` tests lobby, balloon-first and the kick.
  The MC on the headline board idles and waves now and then.
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
  Slow touches never pop it (a playtest bug): a touch slower than `SlapConfig.MinBalloonSlapSpeed` is a nudge
  (`NetworkBalloon.Nudge` -> `RpcNudge`, every `NudgeInterval`): no hit, no turn used, the balloon drifts off the hand at up to
  `NudgeSpeed` and the quills stay disarmed for `NudgeGraceSeconds` (the body is right behind the hand). After a hit,
  `SlapHand` locks that target for both hands until neither touches it and `RehitSeconds` passed (a hand resting in the
  not-yet-moved body on a non-owner client re-hit it: a double-hit pop); the owner also ignores the same hitter twice within
  `RehitSeconds`. Quills (`BalloonPhysics.TouchSpikes`) pop only on closing speed toward them, set (not add) the bounce-off
  speed each step, and leak once per touch. `slow_push_check.cs` tests all three.
- Turn cue: the balloon always shows the hitter colour (candy pink before anyone hits it, `PlayerPalette.Neutral`); whoever hit it last (pass rule on, during a run) sees a "not
  your turn" icon (balloon + red no-sign, `T_NotYourTurn`) low in the middle of their view, drawn over the arms
  (`TurnIndicator` on the head camera, `PrototypeSceneBuilder.BuildTurnIndicator`); it fades out when someone else hits
  it (a giant's hit is nobody's and keeps it). `turn_indicator_check.cs` tests it.
- The audience is the backyard critters (by request; the human crowd stands are gone): `tools/blender/scripts/build_critters.py`
  makes `Art/Critters/{Frog,Snail,Squirrel,Ladybug,Songbird,Mouse}.fbx` (~800 tris each, shared helpers in `toy_mesh.py`)
  and `T_CritterPalette.png` (each face's colour is a palette cell by UV, so all critters share one material).
  `EnvironmentDressingBuilder.BuildCritterAudience` puts 46 of them on logs along both fences (outside `RoamerBounds`) and
  behind the start, no colliders, each in an upright holder (the FBX roots keep their axis rotation). `CritterCrowd` (one
  component, no per-critter scripts) bobs them, turns them to watch the balloon (`LookTarget`, set by `BalloonCallouts`)
  and hops them on `Cheer` (save, rally milestone, cleared hoop) and `CheerAll` (the finish). `critter_review.cs` frames them.
- Save and rally callouts (`BalloonCallouts` on the balloon, local only; tuning in `BalloonConfig` "Save and rally
  callouts"): a player hit on a balloon falling (>= `SaveMinFallSpeed`) within `SaveHeight` of the surface below
  (`BalloonGroundShadow.GroundY`, the pool counts at its water line) is a SAVE: "SAVE!" pops up over it with
  `Balloon_Save` and confetti for everyone (a `save` flag on `RpcHitEffects`; the slapper's predicted hit runs the same
  check, so they see it at once). The rally number grows and changes colour every `RallyMilestoneStep` passes (white,
  yellow, orange, red and throbbing) with the critters' `Critter_Ooh` pitched up per milestone, punches on each pass, and when a
  rally of `RallyBreakMinimum`+ ends on a bounce it droops away grey with `Rally_Break` (sad trombone); a pop, reset or the
  lobby resets it quietly (detected in `Render` by diffing `Rally`, no extra network state). Cues are 2D on the balloon.
  Both texts get a runtime dark drop shadow. `save_rally_check.cs` tests it. Changing `RpcHitEffects` means every headset
  in a playtest needs the new APK.
- Headset lighting: the Quest has no HDR and no tonemapping, so bright colours clip to white; `LocalRig` scales sun and
  ambient by `PlayerRigConfig.HeadsetLightScale` (0.8; the old 1.35 boost washed the build out). `light_probe.cs` compares
  scales from the same view.
- Bounce pads (trampoline, diving-board spring, tree mushroom) launch players only (`BouncePad._balloonFactor` 0): a
  balloon relaunched by a pad hovered over it forever.
- Knockdown rule (the user's, with no switch): players never die, and nothing knocks a player down except a giant's
  kick (see `MischiefMaker`; `NetworkAvatar.SendGiantKick`, the view dims by `KnockbackConfig.KnockdownDim` while you're
  down). Never add another knockdown, and never let players knock each other down. Everything else is a light shove
  (`KnockbackConfig.PlayerPushForce`): walking into a giant, moving obstacles and flames (`RigHazardSensor` ->
  `LocalRig.Shove`), and another player's slap or thrown or swung item (`NetworkAvatar.SendShove`); you keep your hands and
  items. A balloon pop never pushes anyone (the pop blast is gone, by request). Water only pops the balloon (players can
  wade in the pool). `knockdown_check.cs` tests it. Only falling below `KillHeight` (through the map) respawns you.
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
  Hooks: `HoopCheckpoint` (chime + confetti when a hoop clears), `GameState` (2D fanfare on a finish),
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
  time to restore the Head as main camera). A finish plays the fanfare and a generated critter cheer
  (`GeneratedAudio.CritterCheer`, `Critter_Cheer`: chirps, croaks, squeaks) for everyone and pops big confetti round each player's own view
  (`WinCelebration`, through `BurstEffects.BigConfetti`; the confetti pool is 900). A finished run (finish or time up) returns to
  the lobby after `RunConfig.ResultSeconds` (10 s): hoops reset, balloon back on station at the start, crew respawned on the stage, every item back
  on its spot (`NetworkItem.ReturnHome`, home spot networked so late joiners agree). A run is 10 minutes (`RunConfig.RunSeconds`
  600; no medals or grades, by request: any finish is a win, "You made it! N points!"); when time runs out everyone hears a referee
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
  through walls and signs. Floating labels (text that isn't on a sign or a wall: hoop labels, the MC's
  names, the mole holes' titles, the tutorial's WATER / SPIKES / den / welcome labels) get `FaceCamera`
  (`BackyardCourseBuilder.Billboard`): they turn about the vertical axis to face the player (the user asked for every
  non-sign text to billboard). Text on boards, walls and gates stays fixed to its surface.
- Layer 8 is `Balloon` (named by `ProjectLayers`, used through `GameLayers.Balloon`).
- Layer 9 is `Item`: loose items are off the Default layer so gorilla hands never grip them, and plain items ignore
  collisions with the local rig's solid colliders (`NetworkItem.UpdateRigCollision`, run from `Render` so clients that
  don't own the item apply it too), so they never push you. The two items with a trigger effect (bubble wand, leaf blower)
  are the exception, by the user's request: solid against your body while loose or thrown, but not while anyone holds
  them or for 0.5 s after being let go (a held item follows the hand and would ram its holder). Only an item
  another player threw or swung shoves you (lightly; never a knockdown); loose ones never do.
  Held-item push (the user's): hitting the item one hand holds with your other hand (a beach ball in the right hand
  bumped with the left) pushes your own body away from it (`HandGrip.IsBumpedBy` -> `LocalRig.PushOffHeldItems`,
  `IGrabbable.Touches`; a light `Push`, never a knockdown, you keep the item). It needs closing speed, so hands resting
  together don't push. Tuning: `KnockbackConfig` `HeldItemPushForce` / `MinSpeed` / `TouchRadius` / `Cooldown`.
  `held_item_push_check.cs` tests both rules in Play mode; `solid_item_contact_check.cs` measures the bump a solid
  item gives your body (dropped inside it, thrown into it). `knockdown_check.cs` counts frames, so its giant-kick case
  can miss its 1.2 s by a hair above ~190 fps: re-run it before reading that as a bug.
- Layer 10 is `Ragdoll` (a knocked-down giant's bones): `PhysicsLayerRules` lets it touch only Default and itself, so a
  falling giant never hits players (Ignore Raycast), the balloon or items.
- Testing without a headset: PC test mode (keyboard and mouse) turns on automatically, or force it with
  `PlayerRigConfig.ForceDesktopMode`. Solo, the pass rule is suspended (`BalloonConfig.AllowSoloDoubleHits`).
  All players join one Photon region (`NetworkBootstrap._fixedRegion`, default `asia`), or they can miss each other.
  Each build gets its own room: the session name ends in the build's ID (`BalloonDrop-Dev-<buildGUID>`; the Editor and its
  MPPM clones share `-editor`), because a client whose prefabs or networked fields differ from the master's never receives
  its objects and sits on "Connecting..." (that happened on the Quest). Every headset in a playtest needs the same APK;
  untick `NetworkBootstrap._separateBuilds` to put the Editor and a headset in one room on purpose.
  Quest debugging: `adb` is in the Editor's Android SDK (`.../PlaybackEngines/AndroidPlayer/SDK/platform-tools`); run
  `adb logcat -G 16M` first, since the XR runtime floods the default 256 KB buffer and pushes out the `[Net]` lines.
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
  Builds/Quest/PricklyPop.apk --scenes '["Assets/GameJam/Scenes/Splash.unity","Assets/GameJam/Scenes/Prototype.unity","Assets/GameJam/Scenes/Tunnel.unity","Assets/GameJam/Scenes/Tutorial.unity"]' --confirm`
  (the tutorial hole loads Tunnel and Tutorial by name: an APK without them goes black at the hole; last built 2026-11-11 with all four, ~79 MB, 9.5 min), then poll `build status`
  (~8 min, IL2CPP). `Builds/` and `*.apk` are gitignored. Builds rewrite the URP config prefilters and the preloaded
  assets; commit those. The URP global settings must stay at `m_AssetVersion` 9 (URP 17.3), or the build refuses to start.
- If a command times out or the Editor drops, stop and tell the user; don't loop or fall back to YAML edits.

## 5. Jam scope

Day 1 make-or-break: a slap that feels great with friends online. Follow the GDD's must / should /
stretch list and cut order. Don't build should/stretch items while a must-have is unfinished.

## 6. Git

Small commits per feature, message `feat|fix|tune|chore: <what>`. Commit only when the user asks.
Before committing: `unity recompile` passes, console has no new errors, scene saved (`editor save_all`).
