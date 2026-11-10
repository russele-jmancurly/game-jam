C# method bodies run in the open Editor with `unity command script eval_file`.
They live outside Assets/ so Unity never compiles them. Keep each one idempotent and returning a short summary string.
`_catalog.json` is a per-session cache of `unity command --detail full --format json` (safe to delete).

Locomotion regression tests (Play mode, PC mode, no headset; each logs PASS/FAIL lines and DONE):
- `gorilla_test_suite.cs` (`[LocoTest]`): idle, run, jump, climb, bounce, ice, snap turn, respawn, WASD, slap. Start it once the game is up (the balloon must exist).
- `hand_stuck_test.cs` (`[StuckTest]`): plants hands on the lawn, stage, pool floor, pool rim seams and the pool corners (floor/wall edge, the square floor's step outside the rim, the diving-board slot), lifts them off; the glove must follow. `STUCKTEST_ONLY=<name part>` runs some sites.
- `stick_walk_test.cs` (`[WalkTest]`): left-stick walking on the lawn, with a hand resting on the ground, and into a wall.
- `paw_grip_check.cs`: places a close-up camera `PawCam` on your right paw and reads grip, drawn curl and the networked
  `RightGrip`; env `PAWCHECK_G=1/0` presses/releases G. Capture with `capture game_view -- --source camera --camera PawCam`.
- `lobby_check.cs` (`[LobbyTest]`): lobby walls and sign up with the balloon hidden; walking and 7.5 m/s jumps never leave
  the stage; the red button drops the walls and shows the balloon on station; the run makes it live.
- `lobby_cams.cs`: review cameras `LobbyCam_Eye` (spawn view of the button) and `LobbyCam_Out` (the glass box from outside).
- `knockdown_check.cs` (`[KnockTest]`): with knockdowns off, a hazard knock and a networked knockback are short shoves, no ragdoll.
- `audio_loop_check.cs`: peak/RMS of every generated clip and whether each loop's seam is clean (no click).
- `light_probe.cs`: capture camera `LightCam` with post-processing on/off and a light scale, to compare the headset look
  (no HDR, no tonemapping) with the PC look; measure the PNGs' brightness to pick `PlayerRigConfig.HeadsetLightScale`.
- `turn_indicator_check.cs` (`[TurnTest]`): the "not your turn" icon shows after your hit and hides after another player's
  (turns the solo pass-rule exemption off for the test and restores it).
- `finish_check.cs` (`[Finish]`): clears every hoop, then throws the balloon at the finish basket in from the side and
  front under the rim and skimming over it (none may finish), then drops it through the ring (must finish). Writes
  `Screenshots/finish_basket.png`.
- `save_rally_check.cs` (`[SaveRally]`): starts a run; a hit on a falling balloon just above the stage is a SAVE, a high
  one isn't; ten alternating stand-in hitters reach the rally milestones; a bounce off the stage breaks the rally (sad
  trombone). Writes `Screenshots/save_popup.png` and `Screenshots/rally.png` from the player's head.
- `predicted_hit_check.cs` (`[PredictTest]`): fakes network latency offline (env `PREDICT_LATENCY`) and checks the
  slapper's predicted hit: visuals move on the first frame, no jump at the hand-over, end on the real balloon.
  Note: `LOCOTEST_ONLY=slap` in the gorilla suite always fails (PC input stays off without the WASD scenario).
- `time_up_check.cs` (`[TimeUpTest]`): shortens the run to 5 s (restored), checks the whistle and TIME'S UP banner, then that
  everyone is back in the lobby with the items on their spots; places `BannerCam` for a screenshot.
- `surface_check.cs` (Edit or Play mode): the rig's hand-tap surfaces (grass, wood, metal, plastic), whether each kicks up
  dirt, its clips and the materials sorted into it (by `PrototypeSceneBuilder.SurfaceKinds` keywords).
- `fx_check.cs` (Play mode): fires two lawn taps and a wood and a metal tap through `HandTapEffects` (only the lawn ones may
  add clods), a confetti burst and a balloon pop through `BurstEffects`; slows time to 1 % and pauses the dirt mid-flight
  for captures from `FxCam_Dirt` / `FxCam_Burst`. Run again with `STEP = "restore"` to set time back.
- `tutorial_dive.cs` (Play mode): puts the player over the scene's active mole hole so the real trip starts (lobby ->
  Tunnel -> Tutorial, or back). `tutorial_status.cs`: scene, session mode, practice flag, balloon, items, giants, fade,
  holes and the tutorial section. `tutorial_lessons_check.cs` (tutorial scene): plays the five sections with the real
  balloon, items and giant and logs gates, checkpoint and goals (read the `tutorialLog` AppDomain data after ~45 s).
  `tutorial_cams.cs` (tutorial scene) and `tutorial_hole_cam.cs` (prototype) place review cameras. `_run.sh <snippet>`
  runs a snippet and prints only its result.
- `tutorial_fx_record.cs` (Play mode): a recorder that survives scene loads; run it, drive the trip, then read the
  `tutorialFx` AppDomain data: the fade's highest alpha, which trip sounds played, the highest particle count per emitter,
  and how far the billboard labels were off the player (it samples before LateUpdate, so a teleport shows a large
  transient angle). `tutorial_dive_holding.cs`: grabs an item through the real grip (PC input off), dives, and logs that
  the item is let go before the session is left.
- `cactus_pop_check.cs` (Play mode, tutorial scene): the balloon must pop on every part of each cactus (top, body side,
  both arms, pot) and must not pop floating beside it.
