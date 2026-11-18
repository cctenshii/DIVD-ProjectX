extends CharacterBody2D


const SPEED = 300.0
# C# quest controller locks movement while its puzzle or result is open.
var locked: bool = false
@onready var animated_sprite_2d: AnimatedSprite2D = $AnimatedSprite2D

var  last_direction: Vector2 = Vector2.RIGHT

func _physics_process(_delta: float) -> void:
	if locked:
		velocity = Vector2.ZERO
		animated_sprite_2d.pause()
		return
	process_movement()
	move_and_slide()
	
	
	
func process_movement() -> void:
	
	var direction := Input.get_vector("left","right","up","down")


	if direction != Vector2.ZERO:
		velocity = direction * SPEED
		last_direction = direction
	else:
		velocity = Vector2.ZERO


	process_animation(last_direction)
 


func  process_animation(direction) -> void:
	if velocity != Vector2.ZERO:
		play_animation("running", direction)
	else:
		play_animation("idle", direction)

func play_animation(prefix: String, dir: Vector2) -> void:
	if dir.x != 0:
		animated_sprite_2d.flip_h = dir.x > 0
		animated_sprite_2d.play(prefix + "_left")
	elif  dir.y < 0:
		animated_sprite_2d.play(prefix + "_up")
	elif  dir.y > 0:
		animated_sprite_2d.play(prefix + "_down")
	
