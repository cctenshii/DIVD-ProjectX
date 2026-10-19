
extends Control

@onready var intro = $Intro
@onready var puzzle_container = $PuzzleContainer

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
		"description": "Fill the bars the exact same as the ones above",
		"scene": "res://minipuzzles/puzzles/puzzle_03/puzzle_03.tscn"
	}
]

var selected_puzzle: Dictionary = {}


func _ready() -> void:
	# Connect the intro signals.
	intro.start_requested.connect(_on_start_requested)
	intro.cancel_requested.connect(_on_cancel_requested)

	# Start with the intro visible.
	intro.show()
	puzzle_container.hide()

	# Select a puzzle and update the intro.
	_select_puzzle()


# Choose a puzzle and update its introduction.
func _select_puzzle() -> void:
	if puzzles.is_empty():
		push_error("No puzzles have been added.")
		return

	if randomize_puzzle:
		selected_puzzle = puzzles.pick_random()
	else:
		# For now, always use Puzzle 1.
		selected_puzzle = puzzles[0]

	intro.setup(
		selected_puzzle["title"],
		selected_puzzle["description"]
	)


# Called when the player clicks Start.
func _on_start_requested() -> void:
	var scene_path: String = selected_puzzle["scene"]

	var packed_scene: PackedScene = load(scene_path)

	if packed_scene == null:
		push_error("Could not load puzzle scene: " + scene_path)
		return

	var puzzle_instance = packed_scene.instantiate()

	# Clear any previous puzzle.
	for child in puzzle_container.get_children():
		child.queue_free()

	puzzle_container.add_child(puzzle_instance)

	# Hide the intro and show the selected puzzle.
	intro.hide()
	puzzle_container.show()


# Called when the player clicks Cancel.
func _on_cancel_requested() -> void:
	# Return to the intro screen if a puzzle is running.
	for child in puzzle_container.get_children():
		child.queue_free()

	puzzle_container.hide()
	intro.show()

	# Select and display a puzzle again.
	_select_puzzle()
