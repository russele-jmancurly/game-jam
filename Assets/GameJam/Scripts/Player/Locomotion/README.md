# Gorilla locomotion (GorillaPlus port)

`Player.cs`, `Surface.cs`, `Sounds.cs` and `SurfaceSound.cs` come from GorillaPlus
(https://github.com/baggZ-idk/GorillaPlus, MIT, `LICENSE-GorillaPlus.txt`), an updated April 2022 version of
Another Axiom's GorillaLocomotion (https://github.com/Another-Axiom/GorillaLocomotion, MIT, Copyright (c) 2021
Another-Axiom, `LICENSE-GorillaLocomotion.txt`). Credit Kerestell / Another Axiom in the game's credits
(GorillaPlus credit is optional, per its README).

Not taken from GorillaPlus: its networking, Opus voice and PlayFab (we use Fusion + Photon Voice), and
`GorillaFingers` (our gloves have no finger bones).

## What GorillaPlus adds over the 2021 original

- Moves the player in `LateUpdate`, after tracking; a `FixedUpdate` check stops the head being pushed through walls.
- Hand slides (ice) with their own velocity limit and steering, bouncy surfaces, stuck-hand and head-overlap fixes.
- Hand tap sounds and haptics, slide loop sound and haptics; the body capsule shrinks under low ceilings.

## Project changes (keep `Player.cs` otherwise close to upstream so fixes can be diffed in)

- `Surface` actually works: upstream declares it but never reads it. A `Surface` on a collider (or a parent)
  sets slip and bounce; without one, slip comes from a PhysicsMaterial only if its static friction is below
  `slipperyFrictionThreshold` (0.3), so ordinary physics materials keep the normal grip. Bounce = PhysicsMaterial bounciness.
- Haptics go through `XRHaptics` (one impulse) instead of a coroutine per call: no per-frame garbage while sliding.
- `Sounds` caches its material match (upstream compared `material.name`, a string allocation per tap).
- Snap turn rotates the stuck hand points with the view, so turning can't fling the player.
- A tap re-arms only after the hand lifts 2 cm off the surface it tapped (along the surface normal): a hand resting
  still, or settling and sliding a little, flickers in and out of contact (a motionless hand casts nothing), which
  upstream turned into repeated taps and rumbles.
- Zero frame time no longer produces a NaN velocity.
- The body capsule offset is world space below the head (looking down doesn't swing the body forward).
- Null-safe slide audio sources.
- `Carry(delta)`: moves the player with a ride (the merry-go-round) and shifts the planted hand points and head
  history by the same offset, so planted hands don't pull the body back; `LocalRig.CarryOnRide` calls it.
- `Ride(pivot, degrees)`: turns the whole player about a vertical axis through the ride's centre (view, planted hands
  and hand-speed history included), exactly, without flagging a snap turn, so jumping off still works.

Tuning lives in `Config/PlayerRigConfig.asset` (Locomotion section); `LocalRig` copies it onto `Player` every frame,
so it can be tuned live in Play mode. Slippery or bouncy set pieces: add a `Surface`, or give the collider a
PhysicsMaterial with low friction or some bounciness. The balloon is on its own layer, so it never bounces the player.
- Hands never stay stuck inside geometry: every hand/body cast ignores triggers (avatar hitboxes and effect zones sit on
  Default and used to catch hands), the anti-clip raycast stops the hand a radius short of the surface instead of putting
  its centre on it, and `UnembedHand` moves a hand point found inside a collider back out to the surface the head sees.
  Upstream, a hand inside a collider stayed "planted" for good (every sweep from inside hits at distance 0), worst in the
  pool's rim seams and corners. `.claude/unity-snippets/hand_stuck_test.cs` checks it.
- `Carry` sets the rigidbody to the moved transform (absolute) instead of adding the delta to both: the delta was applied
  twice on frames with a physics step (~20 % too far). `LocalRig.StickWalk` (left thumbstick) moves the player through it.
- The slide hiss fades in and out (`UpdateSlideSound`, volume `slideVolume` from `PlayerRigConfig.SlideVolume`) instead of
  `Play()`/`Stop()`: a sliding hand flickers in and out of contact, and upstream restarted the loop each time (a stutter).
- `HandTapped(point, normal, material)`: raised with each hand tap (after the sound), so effects can react to the surface;
  `HandTapEffects` (dirt on grass) listens. `SurfaceSound` has a `Name` and a `KicksUpDirt` flag, and
  `Sounds.SurfaceFor(material)` returns the matched surface (same cache as the sounds). The builder fills `Surfaces` with
  grass, wood, metal and plastic taps (`GeneratedAudio.TapGrass/TapWood/TapMetal/TapPlastic`, three variants each).
