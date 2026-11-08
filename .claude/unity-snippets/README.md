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
- `predicted_hit_check.cs` (`[PredictTest]`): fakes network latency offline (env `PREDICT_LATENCY`) and checks the
  slapper's predicted hit: visuals move on the first frame, no jump at the hand-over, end on the real balloon.
  Note: `LOCOTEST_ONLY=slap` in the gorilla suite always fails (PC input stays off without the WASD scenario).
