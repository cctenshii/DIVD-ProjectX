extends CharacterBody2D


const SPEED = 300.0
@onready var animated_sprite_2d: AnimatedSprite2D = $AnimatedSprite2D

var  last_direction: Vector2 = Vector2.RIGHT

func _physics_process(delta: float) -> void:
	process_movement()
	move_and_slide()
	
	
	
func process_movement() -> void:
		# Get the input direction and handle the movement/deceleration.
	# As good practice, you should replace UI actions with custom gameplay actions.
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
	
