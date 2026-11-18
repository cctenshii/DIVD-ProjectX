extends SceneTree

# Run with Godot .NET: --headless --path . --script res://tests/StoryFlowTests.gd

func _initialize():
	call_deferred("verify")

func settle():
	for i in range(4): await process_frame

func advance():
	var event = InputEventKey.new()
	event.keycode = KEY_ENTER
	event.pressed = true
	Input.parse_input_event(event)
	await settle()

func verify():
	change_scene_to_file("res://scenes/IntroFamily.tscn")
	await settle()
	await advance()
	await advance()
	if current_scene.name != "IntroSpaceshipStorage":
		push_error("Family did not transition to storage")
		quit(1)
		return
	await advance()
	await advance()
	if current_scene.name != "World":
		push_error("Storage did not transition to world")
		quit(1)
		return
	print("STORY FLOW PASSED: Family -> Storage -> World")
	quit()
