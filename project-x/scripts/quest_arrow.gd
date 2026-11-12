extends Sprite2D

@export var rotation_offset_degrees: float = 90.0
@export var nav_agent: NavigationAgent2D
@export var height_offset: float = -50.0

var npc2_node: Node2D
var boss_node: Node2D
@onready var player_node: Node2D = get_parent()

func _ready() -> void:
	# Detach from player scale/rotation
	set_as_top_level(true)

func _process(_delta: float) -> void:
	var target: Node2D = null

	# 1. Identify Target based on Quest Step
	if QuestManager.quest_step == 1:
		if not is_instance_valid(npc2_node):
			npc2_node = get_tree().get_first_node_in_group("NPC2") as Node2D
		target = npc2_node
	elif QuestManager.quest_step == 2:
		if not is_instance_valid(boss_node):
			boss_node = get_tree().get_first_node_in_group("Boss") as Node2D
		target = boss_node

	# 2. Main Arrow Logic
	if is_instance_valid(target) and is_instance_valid(nav_agent) and is_instance_valid(player_node):
		visible = true
		
		# Position arrow above player's head
		global_position = player_node.global_position + Vector2(0, height_offset)
		
		# Update target position
		if nav_agent.target_position != target.global_position:
			nav_agent.target_position = target.global_position

		var distance_to_target = player_node.global_position.distance_to(target.global_position)
		var current_path = nav_agent.get_current_navigation_path()
		
		# DEFAULT / FALLBACK: Point straight at target direction
		var point_to_look_at = target.global_position
		
		# IF WALKABLE PATH EXISTS & NOT CLOSE ENOUGH -> Use Next Path Corner
		if distance_to_target >= 150.0 and current_path.size() > 1:
			var closest_index = 0
			var min_dist = INF
			
			# Find closest path point to player
			for i in range(current_path.size()):
				var d = player_node.global_position.distance_to(current_path[i])
				if d < min_dist:
					min_dist = d
					closest_index = i
			
			# Target the next point ahead on the path
			var target_index = min(closest_index + 1, current_path.size() - 1)
			point_to_look_at = current_path[target_index]

		# Look at calculated point (either next path corner or direct target)
		look_at(point_to_look_at)
		global_rotation_degrees += rotation_offset_degrees
	else:
		visible = false
