# Don't Let the Balloon Drop: Game Design Document

Nov 4, 2026 · @Russele

## Overview

On a TV relay race show, a team of 2 to 4 porcupine-suited Jmans must parkour through a moving obstacle course to the finish line before the timer runs out, dodging the environment while slapping a balloon along with them.

|  |  |
| --- | --- |
| Genre | Co-op parkour friendslop, physics comedy |
| Platform | VR (Meta Quest) |
| Players | 2 to 4 online co-op |
| Movement | Gorilla locomotion (arm-swing) |
| Networking | Photon Fusion 2 (Shared Mode) and Photon Voice 2, Unity 6000.3.8f1 |
| Session | Episodes of 3 relay legs, each on a timer |
| Jam | Curly Blue three-day VR game jam |

**How it answers the brief:**

- **Smooth VR movement:** Gorilla locomotion parkour is the heart of the game: leap, swing and climb through obstacle courses while dodging hazards that move.
- **A world you can touch:** Hundreds of grabbable, throwable items are scattered everywhere. Slapping, throwing and bonking are the whole game.
- **Jman personality:** Existing Jman characters in spiky porcupine suits, an overexcited Jman commentator, and ragdolling friends carry the humour.
- **One great moment:** Saving the balloon with a perfect throw, only for it to bounce straight into your friend's spiky head.

## Design pillars

Every feature must serve at least one of these five rules. If it serves none, cut it.

1. **Keep moving or get hit.** The course is alive: things swing, sweep, crush and collapse. Standing still is never safe, so players are always reading the next obstacle and timing their next leap.
2. **Your hands are your feet.** You move and slap with the same hands, so every slap is a moment you are not moving, and every swing is a moment you are not protecting the balloon.
3. **Gloves are the only safe part of you.** Everything else on a porcupine Jman pops balloons. The balloon can only be slapped, never carried, and never by the same player twice in a row.
4. **The enemy is you, your friends and the world.** There are no AI enemies. Failure always comes from clumsiness, friendly fire or the course itself, so every failure is a story the group laughs about.
5. **Chaos is funny, never unfair.** Every obstacle is telegraphed and readable. Getting hit should make players laugh and try again, not feel cheated.

## Fiction and characters

The players are contestants on **Pop or Glory: The Porcupine Relay**, a cheap but wildly popular Jman TV game show. Teams race a balloon through moving obstacle courses, and the show's famous twist is that every contestant must wear a spiky porcupine suit. Only the show's padded gloves are safe to touch the balloon with.

**The team (players):** Existing Jman characters in the show's official porcupine suits. The suit is a spiky vest, back spikes and a hood, built from simple shapes or existing props so no new character art is needed. Every part of the suit pops balloons; only the gloves are safe. Each player can pick a different Jman for identity.

**The commentator:** A Jman sports commentator in a booth above the course who narrates everything over loudspeakers: great saves, near misses, ragdolls and especially pops ("He's slapped it straight into his teammate's spikes!"). They are the main voice of the game's humour.

**The host:** The show's over-the-top Jman host greets the team in the studio between legs, announces the qualifying score, and at the end of an episode either crowns the team champions or eliminates them in front of the audience.

**Tone:** Cartoon slapstick. Nobody gets hurt; Jmans bounce, flop and get back up.

## Core loop and time limit

An episode is three relay legs. Each leg has a hard time limit, and the episode has a qualifying score the team must hit to be crowned champions.

**One leg:**

1. The host announces the course and the balloon for the leg in the studio.
2. The team spawns at the start line (point A) with the balloon floating in front of them.
3. The leg timer starts. The team slaps, throws and bonks the balloon along the route.
4. If the balloon pops, the course resets: the whole team and a fresh balloon go back to the start line, but the timer keeps running. The leg succeeds when the balloon crosses the finish line (point B), and fails only when the timer hits zero.
5. Points are awarded based on balloon condition, time left and rally bonus.

**The episode:** After each leg, the team returns to the studio and sees their points against the qualifying score. After the third leg, the host either crowns them champions or eliminates them. Then a new episode starts.

&#91;embedded content: episode loop · 3 timed legs, 1 qualifying score\]

A pop sends the team back to the start line with the clock still running; only running out of time ends a leg without points.

**Time limits (starting values, tune in playtest):**

| Leg | Route | Balloons | Time limit |
| --- | --- | --- | --- |
| 1 | Backyard | 1 | 2:30 |
| 2 | Kitchen | 1 | 2:30 |
| 3 | Rooftops to the finish | 2 at once | 3:00 |

**Points per leg:** Base points, plus a bonus for time left, a multiplier for balloon condition (full, leaking, nearly flat), and a rally bonus for long chains of passes between players. A timeout scores nothing. Every pop costs the time it takes to redo the course, which shrinks the time-left bonus.

**Qualifying score:** Set so a clean episode passes comfortably and a messy one only just scrapes through. Failing a leg makes it hard but not impossible, so the episode stays tense until the end.

## Movement and controls

Players move only with gorilla locomotion: push off the ground, walls and objects with their hands to run, climb and leap. There is no joystick movement.

| Action | Input |
| --- | --- |
| Move, climb, leap | Push off surfaces with either hand (gorilla locomotion) |
| Slap the balloon or a friend | Swing an open hand into it; strength comes from hand speed |
| Grab an item | Grip button near an item |
| Throw | Release grip mid-swing |
| Use a tool (leaf blower, bubble wand) | Trigger while holding it |
| Snap turn | Thumbstick left or right (comfort option) |

**Slapping:** Slap force scales with tracked hand velocity, with a cap so wild swings cannot launch the balloon off the map. A gentle tap nudges it; a full swing sends it across a gap. Hits are calculated from hand velocity rather than collider contact alone, so fast swings never pass through the balloon.

**Holding items costs mobility.** A hand holding an item cannot be used to push off. One-handed movement is slower and clumsier, which is a deliberate trade-off: tools are powerful, but they make you a worse climber.

**Gloves:** Only the gloved hands are safe to touch the balloon with. The gloves have a soft, bright look so players always know which part of them is safe.

**Parkour moves:** All built on gorilla locomotion, with the course designed to make each one necessary.

| Move | How it works | Where the course uses it |
| --- | --- | --- |
| Momentum leap | A strong double-arm push launches you across gaps | Roof gaps, broken bridges |
| Wall hop | Push off one wall to reach the opposite one | Narrow alleys, chimney shafts |
| Swing | Grab a bar, rope or washing line and release at the right moment | Over pools, under sweepers |
| Ledge catch | Grab an edge mid-fall and pull up | Collapsing platforms |
| Slide | Slippery surfaces carry your momentum, steer with your hands | Wet tiles, ice, butter spills |
| Bounce | Trampolines and mattresses fling you high | Shortcuts and vertical sections |

**Dodging:** Players dodge with their real bodies: duck under sweeping bars, lean away from swinging objects and hop over rolling ones. This is the heart of the parkour feel and reads brilliantly on the showcase capture.

**Momentum feel:** Keep air control slight but present, so players can make small corrections mid-leap. Landing should feel solid, with a short haptic thud.

## The balloon

The balloon floats down slowly, bounces off soft surfaces, and pops on anything sharp, including every porcupine Jman's body. It can only ever be slapped or hit with items, never held.

**Physics:** Slow, floaty fall with high air drag, so players have time to react. Light squash-and-stretch on every hit. It drifts with wind zones and fans.

**Pass rule:** The same player can never hit the balloon twice in a row. After Player A hits it, the next hit must come from any other teammate; if Player A touches it again first, it pops. This turns every course into a passing game, so at least two players are always needed.

- **What counts as a hit:** A slap, an item that player threw, or a tool they used (leaf blower, umbrella, bubble wand). Players can throw items at any time, but if their thrown item hits the balloon when it isn't their turn, the balloon pops, just like a double slap. An item knocked into the balloon by a hazard counts as nobody's hit.
- **What does not clear it:** Bounces off walls, furniture, bounce pads or the ground. Only another player's hit unlocks the last hitter.
- **Readable at a glance:** The balloon glows in the colour of the last player who hit it, and that player's gloves dim and buzz as a warning if their hand gets close to it.

**What it touches:**

| Contact | Result |
| --- | --- |
| Gloved hand | Slap: pushed in the swing direction; pops if the same player made the last hit |
| Thrown soft item (pillow, beach ball) | Bumped gently upward and away |
| Thrown heavy item (frying pan, watermelon) | Knocked hard; risky but powerful |
| Walls, furniture, floors indoors | Bounces off |
| The ground outdoors | Loses some air; three ground touches and it is nearly flat |
| A Jman's body, light graze | Slow leak: it starts wheezing and sinking faster |
| A Jman's body, hard hit | Pops |
| Sharp hazards (cactus, darts, fan blades, forks) | Pops |
| Squeezed between two hands | Pops (closes the "carry" loophole) |

**Condition:** Every balloon has three states: full, leaking and nearly flat. Leaking balloons fall faster and make a sad squeaky noise. Condition sets the points multiplier at the finish line.

**Balloon types (unlocked by leg):**

- **Standard:** The tutorial balloon.
- **Heavy water balloon:** Falls faster and splashes nearby players when it pops.
- **Giant balloon:** Twice the size, easy to hit, blocks your view, and almost guaranteed to touch someone's spikes.
- **Double relay:** Two balloons at once, forcing the crew to split up.

## Items

Every route is packed with grabbable junk, so the answer to any problem is always "throw something at it." Most items help with the balloon and are dangerous to friends; a few are traps that only look helpful. All items reuse existing prop art.

**Density:** Aim for an item within arm's reach almost everywhere on the route. Items respawn at their spot a few seconds after being thrown off the map, so the world never runs dry.

| Item | Type | On the balloon | On a friend |
| --- | --- | --- | --- |
| Pillow | Soft throwable | Gentle upward bump | Soft bonk, funny noise |
| Beach ball | Soft throwable | Big bouncy bump | Bounces off their head |
| Rubber duck | Soft throwable | Small bump | Squeak and mini-stun |
| Rubber chicken | Soft throwable | Small bump | Screams on impact |
| Inflatable flamingo | Soft throwable | Wide, gentle push | Smothers their view for a second |
| Frying pan | Heavy throwable | Hard knock, risky | Loud bonk, ragdoll |
| Watermelon | Heavy throwable | Hard knock | Bursts, ragdoll, sticky screen splash |
| Fish | Heavy throwable | Medium knock | Wet slap, ragdoll |
| Bowling ball | Heavy throwable | Massive knock | Big ragdoll; knocks them off walls |
| Traffic cone | Heavy throwable | Medium knock | Sticks on their head |
| Leaf blower | Tool | Steady push at range | Blows them backward |
| Umbrella | Tool | Bat it like a paddle | Bonk, ragdoll |
| Hand fan | Tool | Light, precise push | Hair-flutter, harmless |
| Bubble wand | Tool | Bubbles lift it slowly | Traps them in a bubble briefly |
| Helium canister | Tool | Refills a leaking balloon | Squeaky voice for a while |
| Water pistol | Trap | Makes it heavier and fall faster | Soaks them, harmless |
| Darts | Trap | Pops it instantly | Sticks in their suit |
| Fork | Trap | Pops it instantly | Poke and mini-stun |
| Cake | Chaos | Medium knock, sticks to it and adds weight | Face full of cake |

**Jam scope:** Build about 10 of these first (the soft throwables, frying pan, watermelon, leaf blower, umbrella, darts). Every extra item is cheap once the throw and hit systems work.

## Threats: yourself, your friends, the world

There are no AI enemies. Every lost balloon comes from one of three sources, and the game should make it obvious which one it was.

**Yourself:**

- Your own spikes. Lunge too close and you pop the balloon you were saving.
- Clumsy movement. Pushing off a wall can accidentally slap the balloon, and if you made the last hit, that double hit pops it.
- Holding a tool leaves you one-handed and slow at the worst moment.
- Falling off ledges sends you back to a checkpoint while the balloon keeps sinking.

**Your friends:**

- Their spikes are the most dangerous thing on the route. Crowding around the balloon is how most pops happen.
- Two players slapping at once can cancel each other out or send the balloon somewhere terrible.
- Missed throws hit friends instead, ragdolling them at the worst moment.
- A ragdolling friend can tumble straight into the balloon.

**The environment:**

The course is the main enemy. Most obstacles move on a readable rhythm, and each one threatens both the players and the balloon.

| Obstacle | How you dodge it | If it hits you | If it hits the balloon |
| --- | --- | --- | --- |
| Sweeping bar (spinning clothesline, rotating sprinkler arm) | Duck under or hop over | Knocked off, ragdoll | Swatted away |
| Swinging object (punching bag, wrecking-ball piñata, laundry basket) | Time your leap between swings | Ragdoll | Knocked far |
| Crusher (closing garage door, trash compactor, pressing books) | Wait for the gap, then sprint | Flattened cartoon-style, respawn at checkpoint | Pops |
| Collapsing platform (cardboard boxes, wobbly table) | Keep moving, catch the next ledge | You fall | Falls with it |
| Rolling object (barbecue, tyres, bowling balls) | Hop over or climb aside | Ragdoll | Knocked and may hit the ground |
| Ceiling fan | Stay below the blades | Knocked down | Pops on the blades; downdraft pushes it to the floor |
| Spikes and sharp props (cactus pots, knife rack, darts board) | Avoid | Harmless to you (you are already spiky) | Pops |
| Wind fans and gusts | Lean into it, grab something | Pushed sideways | Blown off course |
| Water jets and sprinklers | Time your crossing | Slippery hands for a few seconds | Heavier, falls faster |
| Bounce pads | Use them or avoid them | Launched, often into friends | Launched high |
| Floor that drops away (pool, lava-orange carpet, open sewer) | Never touch it | Respawn at checkpoint | Pops or is lost |

**Rhythm rule:** Every moving obstacle has a clear wind-up (a creak, a light, a shake) before it moves, and a steady pattern players can learn. Difficulty comes from combining obstacles, not from surprising players.

**Readability rule:** Anything sharp has a consistent look (bright red tips or a warning sheen) so players learn "red points pop balloons" within the first minute.

## Ragdoll and friendly fire

Friendly fire is always on. Every slap and every throw can hit a friend, and heavy hits send them ragdolling. This is the main source of comedy, so it must feel great to watch and never feel awful to receive.

**What causes a ragdoll:**

- Hit by a heavy item (frying pan, watermelon, bowling ball, fish)
- A hard slap from a friend
- Launched by a bounce pad into a wall or another player
- Caught in the blast of a popping balloon
- Hit by a moving hazard

Soft items and light slaps cause a mini-stun instead: a wobble, a noise and a tiny shove.

**How ragdoll works in VR:** Your friends see your Jman body flop, spin and tumble. You do not. Spinning a VR player's camera causes motion sickness, so on your own headset a ragdoll is a short knockback in a straight line, a brief vignette, and floppy "noodle" hands for about 1.5 seconds that cannot push off or slap. Then you snap back to normal.

**Getting knocked off things:** A ragdoll while climbing drops you. Falls are short and land you on the ground nearby, not back at the start, unless you fall off the map.

**Planned funny moments:**

- **The save that wasn't:** A perfect throw saves the balloon, which bounces straight into a friend's spiky head.
- **Domino crew:** One bowling ball takes out two friends on a ledge, who tumble onto the balloon.
- **Revenge slap:** A friend gets ragdolled, recovers and immediately hits the culprit with a fish.
- **Pop blast:** The balloon pops and knocks everyone nearby onto their backs while the commentator groans.
- **Leaf blower friend launch:** A player aims the leaf blower at the balloon and blows their friend off the roof instead.
- **Traffic cone hat:** A thrown cone lands perfectly on a friend's head and stays there for the rest of the leg.
- **Squeaky panic:** A friend accidentally helium-blasts themselves and spends the leg yelling in a chipmunk voice.

**Tuning rule:** A ragdolled player should be back in action within 2 seconds. Long stuns stop being funny for the person on the floor.

## Levels

All three courses are giant studio sets built at toy scale: everyday places blown up so the Jmans are tiny. Furniture becomes cliffs, drawers become ledges and the gaps between objects become leaps. Sets are built by scaling up existing props and environment art, so no new art is needed. Studio lights, camera rigs, the commentator's booth and the audience stands surround every set. Each course takes a clean team about 1.5 to 2 minutes, leaving slack inside the time limit for chaos.

**The floor is out.** On every set the floor is a hazard (water, a "lava" carpet or a drop into the studio pit), so each course is a chain of platforms to climb onto and leap between. The balloon has to cross every gap too: slap it across first, then leap after it before it sinks.

| Course | Climb and jump focus | Key obstacles | Signature moment |
| --- | --- | --- | --- |
| Giant Backyard | Fence climbing, stepping-stone hops, swings | Fence slats to climb, flower pots and stepping stones over a flooded lawn, spinning clothesline, washing-line swing, trampoline up to a treehouse finish | Slapping the balloon across the flooded lawn, then swinging from the washing line after it |
| Giant Kitchen | Vertical climb from floor to shelves | Open drawers as a staircase, stove burners, cereal-box towers, spoon bridge over the sink, closing cupboard doors, butter-slick counter, ceiling fan near the top | Climbing the drawer staircase while a teammate keeps the balloon up above |
| Giant Rooftops | Big leaps, height and wind | Chimney climbs, rooftop gaps, antenna-wire swings, wrecking-ball piñata, collapsing cardboard boxes, wind fans | Two balloons in the air while the team leaps a rooftop gap between piñata swings |

**Set design rules:**

- **Three gap sizes:** easy hops, full-effort leaps, and optional hero leaps that offer a shortcut.
- **Rest platforms** after every hard section, where the team can regroup and line up the balloon.
- **Big checkpoint platforms**, so a missed jump costs a short climb, not the whole course.
- **Readable climbing:** Climbable surfaces share one look (bright grip tape or a ribbed texture), so players always know where they can go next.
- **Climb up, jump across:** Alternate vertical climbing sections with horizontal leaping sections to keep the rhythm varied.

**Route structure:** Each route has two or three checkpoints. A player who falls off the map respawns at the last checkpoint after 3 seconds. The balloon has no checkpoint: if it pops, the whole team resets to the start line with a fresh balloon, and the timer keeps running.

**Studio:** The game show studio between legs, with the host's stage, the scoreboard, a cheering Jman audience and a pile of items to mess around with while waiting. It doubles as a safe place to learn slapping and throwing.

## UI, audio and feedback

All UI lives in the world, not on a flat screen overlay, so players never look away from the balloon to read it.

**In the world:**

- **Wrist watch:** The leg timer, on the player's left glove. It ticks louder in the last 20 seconds.
- **Balloon shadow:** A clear shadow on the ground below the balloon, so players can track it without looking straight up.
- **Rally counter:** A floating number above the balloon that counts consecutive passes between players without the balloon touching anything.
- **Spike warning:** Your suit spikes rattle and the controllers buzz when the balloon gets close to your body.
- **Scoreboard:** In the studio, showing points earned against the qualifying score.

**Audio:**

- A meaty, satisfying slap sound, pitched up for harder slaps
- Rubbery squeaks when the balloon bounces
- A sad wheeze when it starts leaking
- A huge pop followed by the commentator's anguished groan, audible from anywhere on the route
- Bonk, splat and squeak sounds for each item type hitting a friend
- Jman voice barks for ragdolls, saves and near misses

**Haptics:** A strong buzz on each slap, a sharp jolt when you are hit, and a soft pulse when the balloon is near your spikes.

## Multiplayer and voice with Photon Fusion

The game is built in Unity 6000.3.8f1 with Photon Fusion 2 in Shared Mode for 2 to 4 player sessions, and Photon Voice 2 for voice chat. Shared Mode has no dedicated host: each player holds state authority over their own objects, which keeps setup simple for a jam. Gorilla locomotion runs locally on each headset; only results are synced.

| Networked thing | State authority | What syncs |
| --- | --- | --- |
| Jman avatar | Its own player | Head, both hands, body position, held items |
| Balloon | Whoever slapped or hit it last | Position, velocity, condition, last hitter |
| Items | Whoever grabbed or threw it last | Position, rotation, velocity |
| Ragdoll | The hit player | An RPC with knockback direction and force |
| Game state (timer, points, qualifying score, leg result, pop reset) | Shared Mode master client | Networked properties |
| Voice | Each player | A Photon Voice stream from their avatar |

**Balloon authority:** On each hit, the slapping player requests state authority over the balloon, so slaps feel instant for whoever made them. Keep the balloon slow so the small corrections when authority changes hands are barely visible. Occasional jank is acceptable in this genre. The balloon stores its last hitter as a networked property, and every new hit is checked against it; if two hits land at the same moment, the first one received wins.

**Ragdoll sync:** Don't sync every bone. Send a knockback RPC (direction and force) and let each client simulate the flop locally. Ragdolls will look slightly different on each screen, which nobody will notice.

**Master client loss:** In Shared Mode the game-state object belongs to the master client. If that player leaves, another player must take over its state authority so the timer and score carry on. Test this on Day 1.

**Voice chat:** Photon Voice 2 with its Fusion integration, installed from the Addons tab of the Fusion Hub. Add a FusionVoiceClient next to the NetworkRunner, and a VoiceNetworkObject, Recorder and Speaker on the Jman avatar prefab, so each player's voice comes from their avatar in 3D. The team talks over proximity voice; if a player needs a private channel with one other player, use Photon Voice interest groups. On Quest, request microphone permission before enabling the Recorder.

**Setup:** Create a Fusion App ID and a Voice App ID in the Photon dashboard and set both in Fusion's Realtime Settings. Photon's cloud hosts the sessions, so headsets can join from any network; fix the region during playtests to keep latency low.

## Comfort

The game is fast and chaotic, so comfort rules are non-negotiable: chaos happens to your friends' bodies, never to your own camera.

- **Never rotate the player's view.** Ragdolls, knockbacks and wind push in straight lines only.
- **Short knockbacks with a vignette.** The screen edges darken briefly during any forced movement.
- **Keep the balloon near eye level.** Slow fall speed and route design that keeps it ahead of the crew, not overhead, to avoid neck strain.
- **Ground shadow.** Lets players track the balloon without looking straight up.
- **Snap turn option.** For players who prefer not to turn their bodies.
- **No screen shake.** Use sound and haptics for impact instead.

## Jam scope, schedule and risks

The make-or-break goal is a slap that feels great with friends online by the end of Day 1. Everything else is built on top of that.

**Scope:**

- **Must have:** 2 to 4 players online with voice chat, gorilla locomotion, slap-only balloon with spike pops, about 10 items, ragdoll on heavy hits, one obstacle course with 4 or 5 moving obstacles and a timer, leg result screen.
- **Should have:** All three routes, the qualifying score and studio, slow leaks, rally counter, commentator lines.
- **Stretch:** Balloon types, the full item list, host banter.

**Three-day plan:**

| Day | Goal | Tasks |
| --- | --- | --- |
| Day 1: Discover | Slap a balloon together online | Gorilla locomotion in Unity; Fusion Shared Mode session with synced Jmans and working voice; balloon physics and slap feel; spike pop on body contact; grab and throw one item |
| Day 2: Build | One full playable leg | Backyard course with checkpoints and moving obstacles; timer and result screen; 10 items; ragdoll and friendly fire; first group playtests and tuning |
| Day 3: Refine | Polish and demo | Kitchen and Rooftops routes if time allows; audio and haptics; comfort pass; performance on Quest; record gameplay capture; prepare the pitch |

**Risks:**

| Risk | Why it is hard | Mitigation |
| --- | --- | --- |
| Balloon authority jank | Several players hitting one object at once | Authority on slap, slow balloon, accept some jank |
| Slap feel | Too weak is boring, too strong sends it off the map | Tune on Day 1 before building levels; cap max force |
| Body pops feel unfair | Players cannot see their own body in VR | Spike warning buzz, small head-and-torso hitbox, slow leak on light grazes |
| Ragdoll motion sickness | Moving the player's camera | Ragdoll is visual for others only; straight knockback plus vignette for you |
| Held items colliding with your own body | Gorilla locomotion uses hand colliders | Held items ignore your own body and climbing surfaces |
| Testing needs several headsets | Every multiplayer test needs 2 to 4 people | Book a daily group playtest slot; use an editor test mode for solo work |

**Cut order if time runs short:** Rooftops route, then Kitchen, then the qualifying score (keep single legs), then extra items.

## Showcase plan

The showcase needs a playable build, a short pitch and a 3 to 5 minute gameplay capture. Show the chaos, but make sure the core idea reads in the first 30 seconds.

**Capture outline (about 4 minutes):**

1. **0:00 to 0:30:** The pitch in one line, then the team in the studio slapping items at each other.
2. **0:30 to 1:30:** The Backyard leg: first slaps, a trampoline friend launch, crossing the finish line and the commentator cheering.
3. **1:30 to 3:00:** The Kitchen or Rooftops: a near miss with spikes, a frying pan ragdoll, a balloon popping right before the finish and the whole crew snapping back to the start with the clock still ticking.
4. **3:00 to 4:00:** The scoreboard, the host's verdict, and a short note on what the team learned.

**Capture tips:** Record from a spectator camera as well as a headset, so viewers can see the ragdolls and spikes. Prepare a highlight of the best accidental friend hit from playtests.

**How Claude helped:** Note where Claude was used during the jam, for example brainstorming the concept, tuning slap and balloon physics, setting up Fusion authority and Photon Voice, and debugging. The brief asks teams to share this.
