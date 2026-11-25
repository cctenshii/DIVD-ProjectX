# Robot Cowboy Boss - Three Phase Asset Pack

New generated artwork, 2026-11-25. Game code and existing boss scenes are unchanged.

## Visual phases
- Phase 1: armored cowboy, cyan visor/laser energy.
- Phase 2: damaged armor, torn coat, amber visor and overheating orange vents/lasers.
- Phase 3: exposed red reactor, fractured visor/armor, scorched coat, furious final form and crimson lasers.

## Movement atlas (each phase)
4 columns, 8 rows; four frames per animation.
Rows: idle_down, idle_left, idle_right, idle_up, walk_down, walk_left, walk_right, walk_up.

## Attack atlas (each phase)
6 columns, 6 rows.
Rows 1-4: attack_down, attack_left, attack_right, attack_up.
Each row: draw, aim, charge, FIRE, recoil, recover. Zero-based firing frame: 3.
Row 5: ground_shot: brace, aim into ground, charge, SHOOT GROUND, RADIAL BURST, recover.
Zero-based ground-impact frame: 3; radial emission frame: 4.
Row 6: detached effects: short bolt, long bolt, impact spark, impact flash, radial burst, fading radial burst.
Only enable the ground-shot gameplay attack in PHASE 3. Earlier-phase sheets include the same animation layout for compatibility.

## Claude handoff / integration
These are visual assets and SpriteFrames resources; they do not implement damage or projectile behavior.
Keep existing C# boss behavior and GDScript teammate code. Do not modify minipuzzles.
Switch the visual resource when the existing boss Stage changes (stage 1/2/3).
The existing battle has 15 successful hacks, changing phase after 5 and 10.

Suggested animation event flow (zero-based frame indices):
- During a normal attack, face the target and play attack_direction once.
- At attack frame 3, emit one laser projectile from a direction-specific gun-barrel Marker2D.
- Aim with (target_position - muzzle_position).Normalized(); rotate the detached right-facing bolt to this direction.
- During phase-three ground_shot, telegraph the attack, then shoot from the barrel to a GroundImpact marker at frame 3.
- At ground_shot frame 4, emit 8 projectiles from GroundImpact at angles TAU*i/8 (i=0..7).
- Each projectile moves independently; the radial burst animation is only an impact visual.
- Use a once-per-attack emission flag to avoid repeated spawning on repeated frame callbacks.
- After animation_finished, return to idle/walk and retain the existing attack cooldown.
- Do not loop attack or ground_shot animations. Pause attacks during puzzles as before.

## Godot import
Copy this whole folder to res://assets/images/bosses/robot-cowboy-pack-v2/.
Each phase .tres contains both movement and attack animations; the same resource includes detached effects named fx_bolt_short, fx_bolt_long, fx_ground_impact and fx_radial_burst.
Use AnimatedSprite2D with Nearest texture filtering.
PNG dimensions may differ from the requested generation size. regions.json records actual rounded atlas rectangles.
The resource regions follow the actual dimensions rather than assuming a 256px grid.
Movement and attack atlases may have different effective sprite sizes. Match their scale/pivot in Godot before production use.

## Validation limits
Artwork has been visually inspected, PNG dimensions and alpha format checked, atlas rectangles validated.
Generated frames can vary slightly in silhouette, foot baseline and muzzle position. Effects may need edge cleanup.
Animation loops, gun-marker placement and actual gameplay have NOT been tested in Godot.
Review the atlas regions and align frames before treating this as final production animation.
