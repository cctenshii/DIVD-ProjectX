extends Control

@onready var title_label = $PuzzleUI/VBoxContainer/TitleLabel
@onready var instruction_label = $PuzzleUI/VBoxContainer/InstructionLabel
@onready var status_label = $PuzzleUI/VBoxContainer/StatusLabel
@onready var restart_button = $PuzzleUI/VBoxContainer/RestartButton

@onready var left_buttons = [
	$PuzzleUI/VBoxContainer/MainLayout/LeftPanel/Btn0,
	$PuzzleUI/VBoxContainer/MainLayout/LeftPanel/Btn1,
	$PuzzleUI/VBoxContainer/MainLayout/LeftPanel/Btn2,
	$PuzzleUI/VBoxContainer/MainLayout/LeftPanel/Btn3
]

@onready var right_buttons = [
	$PuzzleUI/VBoxContainer/MainLayout/RightPanel/Btn0,
	$PuzzleUI/VBoxContainer/MainLayout/RightPanel/Btn1,
	$PuzzleUI/VBoxContainer/MainLayout/RightPanel/Btn2,
	$PuzzleUI/VBoxContainer/MainLayout/RightPanel/Btn3
]

@onready var line_canvas = $PuzzleUI/VBoxContainer/MainLayout/LineCanvas

# Symbol identifiers (e.g., 0: Circle, 1: Triangle, 2: Square, 3: Star)
var left_symbols: Array[int] = [0, 1, 2, 3]
var right_symbols: Array[int] = [3, 0, 1, 2] # Shuffled target order

# Connection state tracking
var selected_left_index: int = -1
var connections: Dictionary = {} 
var puzzle_solved: bool = false


func _ready() -> void:
	title_label.text = "04 - Connect Cables"
	instruction_label.text = "Connect the matching symbols."
	restart_button.pressed.connect(_restart_puzzle)

	# Fix: the status text changes length on every click. Without wrapping the
	# label stretched the whole panel, so the puzzle jumped wider and back.
	# Now the width is fixed by the buttons + cable area, and the status text
	# wraps inside a fixed two-line box.
	line_canvas.custom_minimum_size = Vector2(260, 0)
	status_label.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	status_label.custom_minimum_size = Vector2(0, 64)
	status_label.vertical_alignment = VERTICAL_ALIGNMENT_TOP
	
	# Connect button signals
	for i in range(left_buttons.size()):
		left_buttons[i].pressed.connect(_on_left_pressed.bind(i))
		right_buttons[i].pressed.connect(_on_right_pressed.bind(i))
		
	_restart_puzzle()


func _process(_delta: float) -> void:
	# Continuously redraw lines to follow UI positions | STILL NEED TO FIX
	line_canvas.queue_redraw()


func _restart_puzzle() -> void:
	selected_left_index = -1
	connections.clear()
	puzzle_solved = false
	status_label.text = "Click a left terminal, then click its match."
	
	right_symbol_shuffle()
	_update_button_visuals()


func right_symbol_shuffle() -> void:
	right_symbols.shuffle()


func _update_button_visuals() -> void:
	# Map symbol IDs to readable names or icons
	var symbol_names = ["Circle", "Triangle", "Square", "Star"]
	
	for i in range(4):
		left_buttons[i].text = symbol_names[left_symbols[i]]
		right_buttons[i].text = symbol_names[right_symbols[i]]
		
		left_buttons[i].modulate = Color.WHITE
		right_buttons[i].modulate = Color.WHITE


func _on_left_pressed(index: int) -> void:
	if puzzle_solved:
		return
		
	selected_left_index = index
	status_label.text = "Selected left terminal. Now click the matching right terminal."
	
	# Highlight active selection
	for i in range(left_buttons.size()):
		left_buttons[i].modulate = Color(1.0, 1.0, 1.0) if i != index else Color(1.0, 0.85, 0.3)


func _on_right_pressed(index: int) -> void:
	if puzzle_solved or selected_left_index == -1:
		status_label.text = "Select a left terminal first!"
		return
		
	# Check if this right terminal is already connected
	for l_idx in connections:
		if connections[l_idx] == index:
			connections.erase(l_idx) # Remove previous attachment

	# Make the connection
	connections[selected_left_index] = index
	selected_left_index = -1
	
	# Reset left highlights
	for btn in left_buttons:
		btn.modulate = Color.WHITE
		
	status_label.text = "Cable connected! Check your matches."
	
	_check_win_condition()


func _check_win_condition() -> void:
	if connections.size() < 4:
		return

	var all_correct: bool = true
	for l_idx in connections:
		var r_idx = connections[l_idx]
		# Win condition: the symbol ID on the left must match the symbol ID on the right
		if left_symbols[l_idx] != right_symbols[r_idx]:
			all_correct = false
			break

	if all_correct:
		puzzle_solved = true
		status_label.text = "Puzzle Solved! All cables correctly connected."


# Draw cables on the LineCanvas Control node
func _linear_draw_callback() -> void:
	pass # Handled by _draw override below


func _draw() -> void:
	# Fix: drawing on another node only works inside THAT node's draw callback.
	# The cables are now drawn in _on_line_canvas_draw() below.
	pass


func _on_line_canvas_draw() -> void:
	# Draw lines between connected left and right buttons relative to the canvas.
	# Pseudocode: for each connection -> take the inner edge of both buttons ->
	# convert to LineCanvas space (also correct when the puzzle is scaled) -> draw cable.
	var to_canvas: Transform2D = line_canvas.get_global_transform().affine_inverse()
	var cable_color := Color(0.95, 0.15, 0.25) # Red cables (feedback)
	for l_idx in connections:
		var r_idx = connections[l_idx]

		var l_btn: Control = left_buttons[l_idx]
		var r_btn: Control = right_buttons[r_idx]

		# Right edge of the left button and left edge of the right button
		var start_pos = to_canvas * (l_btn.get_global_transform() * Vector2(l_btn.size.x, l_btn.size.y / 2.0))
		var end_pos = to_canvas * (r_btn.get_global_transform() * Vector2(0.0, r_btn.size.y / 2.0))

		# Draw a smooth connection cable line with plugs at both ends
		line_canvas.draw_line(start_pos, end_pos, cable_color, 6.0, true)
		line_canvas.draw_circle(start_pos, 7.0, cable_color)
		line_canvas.draw_circle(end_pos, 7.0, cable_color)
