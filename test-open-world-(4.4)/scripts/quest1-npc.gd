extends Area2D

# 1. We added your chat_box variable here
@export var chat_box: CanvasLayer 
@export var npc_quest_id: int = 0 
var player_in_range: bool = false

func _ready() -> void:
	# Connecting signals in code exactly like your test NPC to prevent editor bugs
	body_entered.connect(_on_body_entered)
	body_exited.connect(_on_body_exited)

func _process(_delta: float) -> void:
	# 2. Changed "ui_accept" to "interact" so it uses your Q button
	if player_in_range and Input.is_action_just_pressed("interact"):
		
		
		if QuestManager.quest_step == npc_quest_id:
			start_dialogue()
			QuestManager.advance_quest()
		else:
			
			if chat_box:
				chat_box.show_dialogue("I have nothing to say to you right now.")

func start_dialogue():
	
	if chat_box:
		if npc_quest_id == 0:
			chat_box.show_dialogue("Please help us! Go talk to the guard over there.")
		elif npc_quest_id == 1:
			chat_box.show_dialogue("The boss door is unlocked. Good luck!")
	else:
		print("ERROR: Chat box is not assigned in the Inspector!")

func _on_body_entered(body: Node2D) -> void:
	if body.is_in_group("Player"):
		player_in_range = true

func _on_body_exited(body: Node2D) -> void:
	if body.is_in_group("Player"):
		player_in_range = false
		if chat_box:
			chat_box.hide_dialogue()
