extends SceneTree
var host
var results: Array[bool] = []
var checks = 0
func _initialize(): call_deferred("run")
func frames(count = 3):
	for i in range(count):
		await physics_frame
		await process_frame
func check(value, message):
	if not value:
		push_error(message)
		quit(1)
		return false
	checks += 1
	print("PASS: ", message)
	return true
func start(kind):
	host.PreparePuzzle(kind)
	host.StartPreparedHack()
	var intro = host.Flow.get_node("Intro")
	intro.get_node("HowItWorksButton").pressed.emit()
	if not check(intro.get_node("Description").visible and intro.get_node("StartButton").visible, "Native instructions expose Start"): return
	intro.get_node("StartButton").pressed.emit()
	await frames(10)
func run():
	host = load("res://scripts/PXMiniPuzzleHost.cs").new()
	root.add_child(host)
	host.connect("Finished", func(success): results.append(success))
	for kind in range(8):
		var before = results.size()
		await start(kind)
		var puzzle = host.Puzzle
		if not check(is_instance_valid(puzzle) and puzzle.get_script().resource_path.begins_with("res://minipuzzles/"), "Native source loaded for puzzle %d" % (kind+1)): return
		if not check(host.get_global_rect().encloses(puzzle.get_node("PuzzleUI").get_global_rect()) and host.get_global_rect().encloses(host.Flow.get_node("TimerDisplay").get_global_rect()), "Puzzle controls and timer fit inside host"): return
		match kind:
			0:
				# Wait for native timers; press the correct button after each preview/flash.
				var watchdog = 0
				while host.Active and watchdog < 1800:
					if is_instance_valid(puzzle) and puzzle.accepting_input and not puzzle.showing_sequence:
						puzzle.buttons[puzzle.sequence[puzzle.player_index]].pressed.emit()
					await frames(1)
					watchdog += 1
			1:
				var watchdog = 0
				while host.Active and watchdog < 1200:
					for note in puzzle.notes.duplicate():
						if abs(note.y_pos - puzzle.target_y_position) < 15:
							var event = InputEventKey.new()
							# Puzzles 2.0: use the puzzle's own lane keys (QWER).
							event.keycode = puzzle.LANE_KEYS[note.lane]
							event.pressed = true
							puzzle._unhandled_input(event)
					await frames(1)
					watchdog += 1
			2:
				for i in range(8): puzzle.player_sliders[i].value = puzzle.target_values[i]
			3:
				for i in range(4):
					puzzle._on_left_pressed(i)
					puzzle._on_right_pressed(puzzle.right_symbols.find(puzzle.left_symbols[i]))
			4:
				if not check(puzzle.speed_slider.size.x >= 420 and puzzle.height_slider.size.x >= 420 and puzzle.phase_slider.size.x >= 420, "Frequency sliders have usable pointer travel"): return
				puzzle.speed_slider.value = puzzle.target_speed
				puzzle.height_slider.value = puzzle.target_height
				puzzle.phase_slider.value = puzzle.target_phase
			5:
				for number in range(1,10): puzzle._on_number_button_pressed(puzzle.numbers.find(number))
			6:
				for pos in puzzle.node_positions:
					var event = InputEventMouseButton.new()
					event.pressed = true
					event.button_index = MOUSE_BUTTON_LEFT
					event.position = pos
					puzzle._on_node_canvas_gui_input(event)
			7:
				puzzle._on_inventory_fuse_pressed(3)
				puzzle._on_slot_pressed(0)
				puzzle._on_inventory_fuse_pressed(7)
				puzzle._on_slot_pressed(1)
		await frames(8)
		if not check(not host.Active and results.size() == before+1 and results.back(), "Native puzzle %d awards exactly one success" % (kind+1)): return
	await start(7)
	host.Flow.get_node("TimerDisplay/Countdown").timeout.emit()
	await frames()
	if not check(not host.Active and not results.back(), "Original countdown timeout fails the external attempt"): return
	await start(0)
	var event = InputEventKey.new()
	event.keycode = KEY_ESCAPE
	event.pressed = true
	host._Input(event)
	await frames(90)
	if not check(not host.Active and not results.back(), "Cancel during native memory coroutine cleans up safely"): return
	print("MINIPUZZLE TESTS PASSED: ", checks)
	host.queue_free()
	await frames()
	quit()
