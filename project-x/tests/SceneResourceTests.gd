extends SceneTree
# Exercise both script languages, including the teammate's existing NPC dialogue flow.
func _initialize(): call_deferred("run")
func frames(count = 4):
	for i in range(count):
		await physics_frame
		await process_frame
func press_e():
	var event = InputEventKey.new()
	event.keycode = KEY_E
	event.physical_keycode = KEY_E
	event.pressed = true
	Input.parse_input_event(event)
	await frames()
	var release = event.duplicate()
	release.pressed = false
	Input.parse_input_event(release)
	await frames()
func check(condition: bool, message: String) -> bool:
	if not condition:
		push_error(message)
		quit(1)
	return condition
func run():
	var count = 0
	for file in DirAccess.get_files_at("res://scenes"):
		if not file.ends_with(".tscn"): continue
		var scene = load("res://scenes/" + file)
		if not check(scene is PackedScene, file): return
		var node = scene.instantiate()
		if not check(node != null, file): return
		root.add_child(node)
		await frames()
		root.remove_child(node)
		node.queue_free()
		await frames(2)
		count += 1
	var manager = root.get_node("QuestManager")
	manager.quest_step = 0
	var demo = load("res://scenes/NpcDemo.tscn").instantiate()
	root.add_child(demo)
	await frames()
	var player = demo.get_node("Player/Player")
	var first = demo.get_node("Quest-1-NPCS/NPC1/Area2D")
	var second = demo.get_node("Quest-1-NPCS2/NPC1/Area2D")
	player.global_position = first.global_position
	await frames()
	if not check(first.player_in_range, "First NPC detects player collision layer"): return
	await press_e()
	if not check(manager.quest_step == 1 and demo.get_node("ChatBox/Panel").visible, "First NPC dialogue and quest progression"): return
	await frames()
	if not check(player.get_node("QuestArrow").visible, "Original NPC quest arrow remains visible"): return
	player.global_position = second.global_position
	await frames()
	await press_e()
	if not check(manager.quest_step == 2, "Second NPC dialogue and boss marker progression"): return
	root.remove_child(demo)
	demo.queue_free()
	await frames()
	manager.quest_step = 0
	print("SCENE RESOURCE CHECKS PASSED: ", count, " scenes + NPC dialogue/arrow flow")
	quit()
