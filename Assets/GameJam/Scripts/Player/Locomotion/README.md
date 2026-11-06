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
