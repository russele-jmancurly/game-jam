---
name: blender-cli
description: Drives Blender headless from the command line for Prickly Pop assets (inspect a model, render a preview, export a Unity-ready FBX, run any bpy script). Use for any Blender or 3D-model task instead of the Blender MCP server.
---

# Blender through the CLI

All Blender work goes through `tools/blender/blender.sh`, which runs a Python script in headless Blender
(`--background --factory-startup --python-exit-code 1`). Never call Blender MCP tools (`mcp__blender__*`);
the project blocks them in `.claude/settings.json`.

- Blender: 5.2.3 LTS at `C:/Program Files/Blender Foundation/Blender 5.2/blender.exe` (4.4 is also installed).
  The wrapper takes `$BLENDER_EXE`, then `blender` on PATH, then the newest install under Program Files, then the
  newest one Windows lists under installed programs (any drive or folder, e.g. `D:/New Folder/blender.exe`, Steam).
  A run takes ~2–5 s.
- Every script prints its JSON result between `@@RESULT_BEGIN@@` and `@@RESULT_END@@`. Blender also logs to stdout,
  so extract the block: `... 2>&1 | sed -n '/@@RESULT_BEGIN@@/,/@@RESULT_END@@/p' | sed '1d;$d'`.
  Failures print `{"success": false, "error": ...}` and exit 1.

## Ready-made scripts (`tools/blender/scripts/`)

| Task | Command |
|---|---|
| What's in a model (objects, triangles, size, materials, UVs, textures, bones, animations) | `tools/blender/blender.sh tools/blender/scripts/inspect.py <file.blend\|.fbx\|.obj\|.glb>` |
| See a model (replaces the MCP "look") | `tools/blender/blender.sh tools/blender/scripts/render_preview.py <file> Screenshots/<name>.png [--size 768] [--angle 35]`, then Read the PNG |
| Game-ready character (drop parts, bake rig pose, upright, triangle budget) | `tools/blender/blender.sh tools/blender/scripts/prepare_character.py <in.blend\|.fbx> Assets/GameJam/Characters/<Name>/<Name>_Game.fbx [--max-tris 6000] [--exclude GloveL,GloveR] [--rest-pose] [--sculpt-level 1] [--udim-atlas 2048]` (sculpts: drop multires first; UDIM tiles: merge into one atlas) |
| Rig + animate a prepared show character in place (humanoid skeleton, auto weights, Idle/Walk/WindUp/Slap/Wave) | `tools/blender/blender.sh tools/blender/scripts/rig_character.py Assets/GameJam/Characters/<Name>/<Name>_Game.fbx Assets/GameJam/Characters/<Name>/<Name>_Game.fbx [--plan baby]` (input must be unrigged; --plan for non-adult proportions) |
| Squished shoulders / skin through a shirt on a rigged character (fix in place, then rebuild the prototype) | `tools/blender/blender.sh tools/blender/scripts/fix_shoulders.py <x>_Game.fbx <x>_Game.fbx [--hide-under SkinMat:ClothMat]` (run once per rig: it moves weight, so don't stack runs) |
| Add the Kick clip to a rigged giant (in place, replaces an old Kick; then rebuild the prototype) | `tools/blender/blender.sh tools/blender/scripts/add_kick_clip.py <x>_Game.fbx <x>_Game.fbx [--leg R]` |
| The MC's clips on a rigged character (Cheer, Shadowbox; before fix_shoulders) | `tools/blender/blender.sh tools/blender/scripts/add_mc_clips.py <x>_Game.fbx <x>_Game.fbx [--glove L]` |
| Critter audience models + palette texture (frog, snail, squirrel, ladybug, songbird, mouse) | `tools/blender/blender.sh tools/blender/scripts/build_critters.py [out_dir] [--only Frog]` -> `Assets/GameJam/Art/Critters/` (toy-mesh helpers shared with `build_item_props.py` live in `toy_mesh.py`) |
| Lobby how-to icons (item, throw, giant knocked down) | `tools/blender/blender.sh tools/blender/scripts/build_lobby_icons.py` -> `Assets/GameJam/Art/UI/Icon_*.png` |
| See a rig's clips (frames as PNGs) | `tools/blender/blender.sh tools/blender/scripts/render_pose.py <file.fbx> Screenshots/<name> Walk:8 Slap:7 [--angle 90] [--size 400]` |
| Player paw close-ups, open and curled into a fist (same curl rule as GorillaBodyRig) | `tools/blender/blender.sh tools/blender/scripts/render_paw.py Assets/GameJam/Art/Characters/PorcupineBody.fbx Screenshots/paw [--curl 0,0.5,1]` |
| Close-up of a character's face (eyes, glasses, hair) | `tools/blender/blender.sh tools/blender/scripts/render_face.py <file.fbx> Screenshots/<name> [--pose Clip:frame]` |
| Unity-ready FBX | `tools/blender/blender.sh tools/blender/scripts/export_unity_fbx.py <in.blend> Assets/GameJam/Art/<...>.fbx [--objects A,B] [--no-anim]` |
| Anything else | Write a script in `tools/blender/scripts/` (or the scratchpad for one-offs) that imports `blender_common` (`load`, `script_args`, `emit`, `fail`, `world_bounds`, `set_engine`) and run it with the wrapper |

## Rules for scripts

- Start with `sys.path.append(os.path.dirname(os.path.abspath(__file__)))` to import `blender_common`; read arguments
  with `script_args()` (everything after `--`).
- Look shader nodes up by type (`n.type == "BSDF_PRINCIPLED"`), never by name; read enum identifiers instead of
  hard-coding them (`set_engine` already does this for render engines, whose names change between versions).
- Never save over a source `.blend` unless asked; write new files. Exported FBX for Unity goes under
  `Assets/GameJam/Art/`, then Unity imports it (refresh through `unity-cli-loop`).
- Unity export settings (in `export_unity_fbx.py`): metres, `axis_forward="-Z"`, `axis_up="Y"`,
  `apply_scale_options="FBX_SCALE_ALL"`, `bake_space_transform=True`, no leaf bones. Imported models arrive at their
  own scale; read `boundsSize` from `inspect.py` before placing them (the Drive food props are ~0.2 m).
- Quest budget: check `totalTriangles` before importing; keep props in the low thousands, characters <= 6000.
- Rotation: always bake rotation/scale into the mesh before exporting (`transform_apply`). Source FBX files are often
  Y-up, and Blender's `bake_space_transform` leaves a 90 degree object rotation that makes Unity show models lying down.
  Check after import: the model root rotation is 0 and Y is the tallest bounds axis.
- Shape keys block applying modifiers; `prepare_character.py` strips them (the game doesn't use them).

## What the MCP did that the CLI doesn't

- **Asset libraries** (Poly Haven, Sketchfab, Poly Pizza) and **text-to-3D generation**: not available headless.
  Downloading from those sites needs the user's OK per file; prefer the studio's own Drive assets.
- **Driving the user's open Blender window live**: the CLI runs a separate headless Blender and never touches what the
  user has open. To change a file the user is editing, ask them to save and close it first.

## Verify

Run `inspect.py` on any file you produce, and `render_preview.py` when the visual result is the question.
Report the triangle count and size.
