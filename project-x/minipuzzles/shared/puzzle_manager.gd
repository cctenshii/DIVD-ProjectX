
extends Control

@onready var intro = $Intro
@onready var puzzle_container = $PuzzleContainer
@onready var timer_display = $TimerDisplay

var time_limit: float = 60.0
var randomize_puzzle: bool = true

var puzzles: Array[Dictionary] = [
	{
		"title": "Memory Puzzle",
		"description": "Watch the sequence carefully. Remember the order of the four buttons and repeat it correctly. Each round adds one more step!",
		"scene": "res://minipuzzles/puzzles/puzzle_01/puzzle_01.tscn"
	},
	{
		"title": "Rhythm Game",
		"description": "Follow the correct WASD falling tiles.",
		"scene": "res://minipuzzles/puzzles/puzzle_02/puzzle_02.tscn"
	},
	{
		"title": "Equalizer Matching",
		"description": "Fill the bars to match the ones above.",
		"scene": "res://minipuzzles/puzzles/puzzle_03/puzzle_03.tscn"
	}
]

var selected_puzzle: Dictionary = {}


func _ready() -> void:
	# Connect intro signals.
	intro.start_requested.connect(_on_start_requested)
	intro.cancel_requested.connect(_on_cancel_requested)

	# Connect the timer signal.
	timer_display.time_up.connect(_on_time_up)

	# Initially show the intro, not the timer or puzzle.
	timer_display.hide()
	puzzle_container.hide()
	intro.show()

	_select_puzzle()


# Select a puzzle and update the intro.
func _select_puzzle() -> void:
	if puzzles.is_empty():
		push_error("No puzzles have been added.")
		return

	if randomize_puzzle:
		selected_puzzle = puzzles.pick_random()
	else:
		selected_puzzle = puzzles[0]

	intro.setup(
		selected_puzzle["title"],
		selected_puzzle["description"]
	)


# Called when the player clicks Start.
func _on_start_requested() -> void:
	if selected_puzzle.is_empty():
		push_error("No puzzle has been selected.")
		return

	var scene_path: String = selected_puzzle["scene"]
	var packed_scene: PackedScene = load(scene_path)

	if packed_scene == null:
		push_error("Could not load puzzle scene: " + scene_path)
		return

	# Stop any previous timer.
	timer_display.stop_timer()
	timer_display.hide()

	# Remove any previous puzzle.
	for child in puzzle_container.get_children():
		puzzle_container.remove_child(child)
		child.queue_free()

	# Create the selected puzzle.
	var puzzle_instance = packed_scene.instantiate()
	puzzle_container.add_child(puzzle_instance)

	# Show the game.
	intro.hide()
	puzzle_container.show()

	# Start and display the countdown.
	timer_display.show()
	timer_display.start_timer(time_limit)


# Called when the player clicks Cancel.
func _on_cancel_requested() -> void:
	# Stop and hide the timer.
	timer_display.stop_timer()
	timer_display.hide()

	# Remove the current puzzle.
	for child in puzzle_container.get_children():
		puzzle_container.remove_child(child)
		child.queue_free()

	puzzle_container.hide()
	intro.show()

	# Select a puzzle for the next attempt.
	_select_puzzle()


# Called when the countdown reaches zero.
func _on_time_up() -> void:
	# Stop and hide the timer.
	timer_display.stop_timer()
	timer_display.hide()

	# Remove the current puzzle.
	for child in puzzle_container.get_children():
		puzzle_container.remove_child(child)
		child.queue_free()

	puzzle_container.hide()

	# Return to the intro with a retry message.
	intro.show()
	intro.setup(
		"Time's Up!",
		"You ran out of time. Click Start to try again!"
	)
