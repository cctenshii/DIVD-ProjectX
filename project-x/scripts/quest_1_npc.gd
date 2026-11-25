extends Area2D

# Design-preview interaction only; this does not gate the C# boss encounter.
@export var chat_box: CanvasLayer 
@export var npc_quest_id: int = 0 
var player_in_range: bool = false

func _ready() -> void:
	# Signals belong to this Area2D script, not its CharacterBody2D parent.
	body_entered.connect(_on_body_entered)
	body_exited.connect(_on_body_exited)

func _process(_delta: float) -> void:
	# Progress only when the expected NPC can actually show its dialogue.
	if player_in_range and Input.is_action_just_pressed("interact"):
		
		
		if QuestManager.quest_step == npc_quest_id:
			if start_dialogue():
				QuestManager.advance_quest()
		else:
			
			if chat_box:
				chat_box.show_dialogue("I have nothing to say to you right now.")

func start_dialogue() -> bool:
	
	if chat_box:
		if npc_quest_id == 0:
			chat_box.show_dialogue("Please help us! Go talk to the guard over there.")
		elif npc_quest_id == 1:
			chat_box.show_dialogue("Follow the arrow to the boss sector. Good luck!")
		return true
	else:
		print("ERROR: Chat box is not assigned in the Inspector!")
		return false

func _on_body_entered(body: Node2D) -> void:
	if body.is_in_group("Player"):
		player_in_range = true

func _on_body_exited(body: Node2D) -> void:
	if body.is_in_group("Player"):
		player_in_range = false
		if chat_box:
			chat_box.hide_dialogue()
