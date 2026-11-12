extends Sprite2D


@export var rotation_offset_degrees: float = -90.0

var npc2_node: Node2D
var boss_node: Node2D

func _process(_delta: float) -> void:
	var target: Node2D = null

	if QuestManager.quest_step == 1:
		if not npc2_node:
			npc2_node = get_tree().get_first_node_in_group("NPC2") as Node2D
		target = npc2_node
	elif QuestManager.quest_step == 2:
		if not boss_node:
			boss_node = get_tree().get_first_node_in_group("Boss") as Node2D
		target = boss_node

	if target and is_instance_valid(target):
		visible = true
		
		look_at(target.global_position)
		rotation_degrees += rotation_offset_degrees
	else:
		visible = false
