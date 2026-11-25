# Robot cowboy boss - v1

New AI-generated transparent RGBA spritesheet for Project X (2026-11-25).
PNG size: 887 x 1774. Four columns and eight rows, 32 frames total.
This is character animation artwork, not an environment TileSet.

Rows from top to bottom:
1. idle_down
2. idle_left
3. idle_right
4. idle_up
5. walk_down
6. walk_left
7. walk_right
8. walk_up

Each row has four frames, read left to right. Idle: 4 fps; walk: 8 fps.
The companion .tres contains AtlasTexture regions using rounded cell boundaries
(221/222 pixels), since the generated dimensions are not divisible by the grid.

Godot: keep the PNG at res://assets/images/bosses/robot-cowboy-boss-v1.png.
Load robot-cowboy-boss-v1.tres into AnimatedSprite2D.sprite_frames.
Use Nearest texture filtering. Suggested starting scale: 0.5.
If moving the files, update the texture path in the .tres.

The artwork was visually reviewed, but animation loops have not been played or
aligned pixel-by-pixel in Godot. AI-generated frames can vary slightly in shape
and baseline. Review the loops before using this as final production animation.
Existing boss code and scenes have not been changed.
