
extends Control

@onready var intro = $Intro
@onready var puzzle_ui = $PuzzleUI

@onready var description = $Intro/Description
@onready var how_it_works_button = $Intro/HowItWorksButton
@onready var start_button = $Intro/StartButton

@onready var progress_bar = $PuzzleUI/ProgressBar
@onready var status_label = $PuzzleUI/StatusLabel
@onready var restart_button = $PuzzleUI/RestartButton

@onready var buttons = [
	$PuzzleUI/ButtonGrid/Button0,
	$PuzzleUI/ButtonGrid/Button1,
	$PuzzleUI/ButtonGrid/Button2,
	$PuzzleUI/ButtonGrid/Button3
]

# Puzzle data
var sequence: Array[int] = []
var player_index: int = 0
var round_number: int = 0
var accepting_input: bool = false
var showing_sequence: bool = false


func _ready() -> void:

	intro.show()
	puzzle_ui.hide()

	description.hide()
	start_button.hide()

	how_it_works_button.pressed.connect(
		_on_how_it_works_pressed
	)
	start_button.pressed.connect(_start_puzzle)
	restart_button.pressed.connect(_restart_puzzle)

	# Connect puzzle buttons
	for i in range(buttons.size()):
		buttons[i].pressed.connect(
			_on_puzzle_button_pressed.bind(i)
		)
	
	style_puzzle_buttons()


# Show the instructions
func _on_how_it_works_pressed() -> void:
	description.text = (
        "Watch the sequence carefully. "
		+ "Remember the order of the four buttons "
		+ "and repeat it correctly. Each round adds "
		+ "one more step!"
	)

	description.show()
	start_button.show()


# Start a new puzzle
func _start_puzzle() -> void:
	intro.hide()
	puzzle_ui.show()

	sequence.clear()
	round_number = 0

	_next_round()


# New step to game
func _next_round() -> void:
	round_number += 1
	player_index = 0
	accepting_input = false

	status_label.text = "Memorize the sequence!"
	progress_bar.max_value = round_number
	progress_bar.value = 0

	# Random button add
	sequence.append(randi_range(0, 3))

	await get_tree().create_timer(0.7).timeout

	await _show_sequence()


func _show_sequence() -> void:
	showing_sequence = true
	accepting_input = false

	for button_index in sequence:
		await _flash_button(button_index)
		await get_tree().create_timer(0.2).timeout

	showing_sequence = false
	accepting_input = true

	status_label.text = "Repeat the sequence!"


# Highlight a button temporarily
func _flash_button(index: int) -> void:
	var button = buttons[index]

	button.modulate = Color(1.0, 0.85, 0.3)
	await get_tree().create_timer(0.45).timeout

	button.modulate = Color.WHITE


# Check the player's input
func _on_puzzle_button_pressed(index: int) -> void:
	if not accepting_input:
		return

	# Is the selected button correct?
	if index == sequence[player_index]:
		player_index += 1

		progress_bar.value = player_index
		await _flash_button(index)

		# The entire sequence was correct
		if player_index == sequence.size():
			accepting_input = false
			status_label.text = "Correct! Next round..."

			await get_tree().create_timer(1.0).timeout
			_next_round()

	else:
		# Incorrect input
		accepting_input = false
		status_label.text = "Wrong sequence! Try again."

		await get_tree().create_timer(1.0).timeout
		_restart_puzzle()


# Restart the puzzle from the beginning
func _restart_puzzle() -> void:
	sequence.clear()
	round_number = 0
	player_index = 0
	accepting_input = false
	showing_sequence = false

	for button in buttons:
		button.modulate = Color.WHITE

	_next_round()


# Button styling
func style_puzzle_buttons() -> void:
	var colors = [
		Color("#B7B7B7"),
		Color("#929292"),
		Color("#F2F2F2"),
		Color("#747474")
	]

	for i in range(buttons.size()):
		var button = buttons[i]

		button.custom_minimum_size = Vector2(200, 200)
		button.text = ""
		button.add_theme_stylebox_override(
			"normal",
			make_button_style(colors[i])
		)

		button.add_theme_stylebox_override(
			"hover",
			make_button_style(colors[i].lightened(0.15))
		)

		button.add_theme_stylebox_override(
			"pressed",
			make_button_style(Color("#FFD166"))
		)


func make_button_style(color: Color) -> StyleBoxFlat:
	var style = StyleBoxFlat.new()

	style.bg_color = color
	style.set_corner_radius_all(14)
	style.set_content_margin_all(8)

	return style
