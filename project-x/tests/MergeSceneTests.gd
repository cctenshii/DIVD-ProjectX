extends SceneTree

# Smoke-load every scene and exercise the retained design preview independently.
var failures := 0

func _initialize():
	call_deferred("verify")

func check(condition: bool, message: String):
	if not condition:
		failures += 1
		push_error(message)
	else:
		print("PASS: ", message)

func settle():
	for i in range(3): await process_frame

func verify():
	for file in DirAccess.get_files_at("res://scenes"):
		if not file.ends_with(".tscn"): continue
		var scene = load("res://scenes/" + file) as PackedScene
		check(scene != null, "Load " + file)
		if scene == null: continue
		var instance = scene.instantiate()
		root.add_child(instance)
		await settle()
		if file == "test_map.tscn":
			check(instance.get_node("props").get_used_cells_by_id(20, Vector2i(0, 1)).is_empty(), "Removed out-of-bounds bush tile was unused")
		if file == "main.tscn":
			var manager = root.get_node("QuestManager")
			manager.quest_step = 0
			var first = instance.get_node("Quest-1-NPCS/NPC1/Area2D")
			var second = instance.get_node("Quest-1-NPCS2/NPC1/Area2D")
			var arrow = instance.get_node("Player/Player/QuestArrow")
			check(first.start_dialogue(), "Design NPC can display dialogue")
			manager.advance_quest()
			arrow._process(0.0)
			check(arrow.visible and arrow.npc2_node == instance.get_node("Quest-1-NPCS2"), "Design arrow still targets NPC2")
			check(second.start_dialogue(), "Design guard can display dialogue")
			manager.advance_quest()
			arrow._process(0.0)
			check(arrow.boss_node == instance.get_node("Bossfightlocation"), "Design arrow still targets the demo marker")
			# A cached marker must not be dereferenced after being freed.
			instance.get_node("Bossfightlocation").free()
			arrow._process(0.0)
			check(not arrow.visible, "Arrow handles a freed target safely")
			manager.quest_step = 0
		instance.queue_free()
		await settle()
	print("MERGE SCENE TESTS COMPLETE: ", failures, " failures")
	quit(0 if failures == 0 else 1)
