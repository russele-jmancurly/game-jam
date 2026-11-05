# Gorilla locomotion

`Player.cs` and `Surface.cs` come from Another Axiom's GorillaLocomotion
(https://github.com/Another-Axiom/GorillaLocomotion), MIT License, Copyright (c) 2021 Another-Axiom.
See LICENSE-GorillaLocomotion.txt. Credit Kerestell / Another Axiom in the game's credits.

Changes for this project:
- `Rigidbody.velocity` renamed to `Rigidbody.linearVelocity` (Unity 6 API).

`Surface` sets how slippery a collider is (`slipPercentage`): use it for the
wet stepping stones and roof tiles in the course.
