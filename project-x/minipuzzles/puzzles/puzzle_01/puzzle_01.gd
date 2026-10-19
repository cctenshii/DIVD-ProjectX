
extends Control

@onready var puzzle_ui = $PuzzleUI
@onready var puzzle_title = $PuzzleUI/PuzzleTitle
@onready var progress_bar = $PuzzleUI/ProgressBar
@onready var status_label = $PuzzleUI/StatusLabel
@onready var restart_button = $PuzzleUI/RestartButton

@onready var buttons = [
	$PuzzleUI/ButtonGrid/Button0,
	$PuzzleUI/ButtonGrid/Button1,
	$PuzzleUI/ButtonGrid/Button2,
	$PuzzleUI/ButtonGrid/Button3
]

var sequence: Array[int] = []
var player_index: int = 0
var round_number: int = 0

var accepting_input: bool = false
var showing_sequence: bool = false
var game_id: int = 0


func _ready() -> void:
	randomize()

	puzzle_title.text = "Memory Puzzle"

	restart_button.pressed.connect(_restart_puzzle)

	for i in range(buttons.size()):
		buttons[i].pressed.connect(
			_on_puzzle_button_pressed.bind(i)
		)

	style_puzzle_buttons()

	_restart_puzzle()


# Start or restart the puzzle
func _restart_puzzle() -> void:
	game_id += 1

	sequence.clear()
	round_number = 0
	player_index = 0
	accepting_input = false
	showing_sequence = false

	for button in buttons:
		button.modulate = Color.WHITE

	_next_round()


# Start the next round
func _next_round() -> void:
	round_number += 1
	player_index = 0
	accepting_input = false
	showing_sequence = false

	status_label.text = "Memorize the sequence!"
	progress_bar.max_value = round_number
	progress_bar.value = 0

	# Add one random step to the sequence
	sequence.append(randi_range(0, buttons.size() - 1))

	var current_game_id = game_id

	await get_tree().create_timer(0.7).timeout

	if not is_inside_tree() or current_game_id != game_id:
		return

	await _show_sequence(current_game_id)


# Show the sequence to the player
func _show_sequence(current_game_id: int) -> void:
	showing_sequence = true
	accepting_input = false

	for button_index in sequence:
		if current_game_id != game_id:
			return

		await _flash_button(button_index, current_game_id)

		if current_game_id != game_id:
			return

		await get_tree().create_timer(0.2).timeout

		if current_game_id != game_id:
			return

	showing_sequence = false
	accepting_input = true

	status_label.text = "Repeat the sequence!"


func _flash_button(index: int, current_game_id: int) -> void:
	if index < 0 or index >= buttons.size():
		return

	var button = buttons[index]

	button.modulate = Color(1.0, 0.85, 0.3)

	await get_tree().create_timer(0.45).timeout

	if is_inside_tree() and current_game_id == game_id:
		button.modulate = Color.WHITE


func _on_puzzle_button_pressed(index: int) -> void:
	if not accepting_input or showing_sequence:
		return

	if index < 0 or index >= buttons.size():
		return

	if player_index >= sequence.size():
		return

	if index == sequence[player_index]:
		accepting_input = false
		player_index += 1

		progress_bar.value = player_index

		var current_game_id = game_id
		await _flash_button(index, current_game_id)

		if not is_inside_tree() or current_game_id != game_id:
			return

		if player_index == sequence.size():
			status_label.text = "Correct! Next round..."

			await get_tree().create_timer(1.0).timeout

			if is_inside_tree() and current_game_id == game_id:
				_next_round()
		else:
			accepting_input = true

	else:
		accepting_input = false
		status_label.text = "Wrong sequence! Restarting..."

		var current_game_id = game_id

		await get_tree().create_timer(1.0).timeout

		if is_inside_tree() and current_game_id == game_id:
			_restart_puzzle()


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
