extends Area2D


@export var chat_box: CanvasLayer
var is_player_in_range: bool = false

func _ready() -> void:
	
	body_entered.connect(_on_body_entered)
	body_exited.connect(_on_body_exited)

func _process(_delta: float) -> void:
	# Check if Q is pressed at all
	if Input.is_action_just_pressed("interact"):
		print("Q button was pressed!")
		
		
		if is_player_in_range:
			print("Player is in range. Attempting to show chat box...")
			if chat_box:
				chat_box.show_dialogue("Ready to start your adventure?")
				print("Success: Chat box should be visible now.")
			else:
				print("ERROR: Chat box is missing or not assigned in the Inspector!")
		else:
			print("Player is NOT in range.")

func _on_body_entered(body: Node2D) -> void:
	print("Something entered the NPC area: ", body.name)
	if body.is_in_group("Player"):
		print("The player was successfully detected!")
		is_player_in_range = true

func _on_body_exited(body: Node2D) -> void:
	if body.is_in_group("Player"):
		print("The player left the area.")
		is_player_in_range = false
		if chat_box:
			chat_box.hide_dialogue()
