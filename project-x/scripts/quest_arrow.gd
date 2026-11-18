extends Sprite2D


@export var rotation_offset_degrees: float = 90.0
@export var target_path: NodePath
@export var navigation_enabled: bool = true

var npc2_node: Node2D
var boss_node: Node2D

func _process(_delta: float) -> void:
	# Gameplay: point at the real boss. Design demo: retain the NPC quest sequence.
	# Reacquire freed targets after reloads; never dereference a cached freed node.
	if not navigation_enabled:
		hide()
		return
	var target: Node2D = null

	if not target_path.is_empty():
		target = get_node_or_null(target_path) as Node2D
	elif QuestManager.quest_step == 1:
		if not is_instance_valid(npc2_node):
			npc2_node = get_tree().get_first_node_in_group("NPC2") as Node2D
		target = npc2_node
	elif QuestManager.quest_step == 2:
		if not is_instance_valid(boss_node):
			boss_node = get_tree().get_first_node_in_group("Boss") as Node2D
		target = boss_node

	if is_instance_valid(target):
		visible = true
		
		look_at(target.global_position)
		rotation_degrees += rotation_offset_degrees
	else:
		visible = false
