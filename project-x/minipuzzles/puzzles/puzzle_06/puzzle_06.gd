extends Control

@onready var title_label = $PuzzleUI/VBoxContainer/TitleLabel
@onready var instruction_label = $PuzzleUI/VBoxContainer/InstructionLabel
@onready var progress_label = $PuzzleUI/VBoxContainer/ProgressLabel
@onready var status_label = $PuzzleUI/VBoxContainer/StatusLabel
@onready var restart_button = $PuzzleUI/VBoxContainer/RestartButton

@onready var number_buttons = [
	$PuzzleUI/VBoxContainer/NumberGrid/Btn0,
	$PuzzleUI/VBoxContainer/NumberGrid/Btn1,
	$PuzzleUI/VBoxContainer/NumberGrid/Btn2,
	$PuzzleUI/VBoxContainer/NumberGrid/Btn3,
	$PuzzleUI/VBoxContainer/NumberGrid/Btn4,
	$PuzzleUI/VBoxContainer/NumberGrid/Btn5,
	$PuzzleUI/VBoxContainer/NumberGrid/Btn6,
	$PuzzleUI/VBoxContainer/NumberGrid/Btn7,
	$PuzzleUI/VBoxContainer/NumberGrid/Btn8
]

var numbers: Array[int] = [1, 2, 3, 4, 5, 6, 7, 8, 9]
var current_target: int = 1
var puzzle_solved: bool = false


func _ready() -> void:
	title_label.text = "06 - Number Sequence"
	instruction_label.text = "Click the numbers from 1 to 9 in order."
	restart_button.pressed.connect(_restart_puzzle)
	
	for i in range(number_buttons.size()):
		number_buttons[i].pressed.connect(_on_number_button_pressed.bind(i))
		
	_restart_puzzle()


func _restart_puzzle() -> void:
	current_target = 1
	puzzle_solved = false
	status_label.text = "Find and click number 1."
	progress_label.text = "1 / 9"
	
	# Shuffle numbers randomly across the 9 grid buttons
	numbers.shuffle()
	
	for i in range(number_buttons.size()):
		var btn = number_buttons[i]
		btn.text = str(numbers[i])
		btn.disabled = false
		btn.modulate = Color.WHITE


func _on_number_button_pressed(index: int) -> void:
	if puzzle_solved:
		return
		
	var clicked_number = numbers[index]
	var btn = number_buttons[index]
	
	if clicked_number == current_target:
		# Correct number clicked!
		btn.disabled = true
		btn.modulate = Color(0.5, 1.0, 0.5) # Green tint for completed
		
		if current_target == 9:
			puzzle_solved = true
			status_label.text = "Puzzle Solved! All numbers clicked in order."
			progress_label.text = "9 / 9"
		else:
			current_target += 1
			progress_label.text = str(current_target) + " / 9"
			status_label.text = "Correct! Now find number " + str(current_target) + "."
	else:
		# Wrong order clicked
		status_label.text = "Wrong! You need to click " + str(current_target) + "."
		# Brief flash red
		btn.modulate = Color(1.0, 0.4, 0.4)
		await get_tree().create_timer(0.3).timeout
		if is_inside_tree() and not btn.disabled:
			btn.modulate = Color.WHITE
