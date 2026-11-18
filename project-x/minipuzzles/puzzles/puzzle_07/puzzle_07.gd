extends Control

@onready var title_label = $PuzzleUI/VBoxContainer/TitleLabel
@onready var instruction_label = $PuzzleUI/VBoxContainer/InstructionLabel
@onready var status_label = $PuzzleUI/VBoxContainer/StatusLabel
@onready var restart_button = $PuzzleUI/VBoxContainer/RestartButton
@onready var node_canvas = $PuzzleUI/VBoxContainer/NodeCanvas

var current_target: int = 1
var puzzle_solved: bool = false

# Coordinates for the 5 numbered nodes across the canvas
var node_positions: Array[Vector2] = [
	Vector2(90, 180),
	Vector2(220, 260),
	Vector2(350, 140),
	Vector2(480, 280),
	Vector2(580, 200)
]

var node_radii: float = 32.0


func _ready() -> void:
	title_label.text = "07 - Rhythm Clicking"
	instruction_label.text = "Click the numbered circles in sequential order."
	restart_button.pressed.connect(_restart_puzzle)
	
	_restart_puzzle()


func _restart_puzzle() -> void:
	current_target = 1
	puzzle_solved = false
	status_label.text = "Click circle 1 to begin!"
	node_canvas.queue_redraw()


func _on_node_canvas_gui_input(event: InputEvent) -> void:
	if puzzle_solved:
		return
		
	if event is InputEventMouseButton and event.pressed and event.button_index == MOUSE_BUTTON_LEFT:
		var local_pos = event.position # Local position inside NodeCanvas
		
		# Check if any node was clicked
		for i in range(node_positions.size()):
			var node_num = i + 1
			var distance = local_pos.distance_to(node_positions[i])
			
			if distance <= node_radii:
				if node_num == current_target:
					current_target += 1
					if current_target > 5:
						puzzle_solved = true
						status_label.text = "Puzzle Solved! Sequence completed successfully."
					else:
						status_label.text = "Correct! Now click circle " + str(current_target) + "."
					node_canvas.queue_redraw()
				else:
					status_label.text = "Wrong order! Expected circle " + str(current_target) + "."
					node_canvas.queue_redraw()
				break


func _process(_delta: float) -> void:
	node_canvas.queue_redraw()


# Draw the connecting path and numbered circles on the NodeCanvas node
func _on_node_canvas_draw() -> void:
	# Draw connecting path lines between nodes
	for i in range(node_positions.size() - 1):
		node_canvas.draw_line(node_positions[i], node_positions[i+1], Color(0.4, 0.4, 0.4, 0.8), 4.0, true)
		
	# Draw each node circle and its number
	for i in range(node_positions.size()):
		var node_num = i + 1
		var pos = node_positions[i]
		
		var fill_color = Color(0.95, 0.95, 0.95)
		var border_color = Color(0.15, 0.15, 0.15)
		
		if node_num < current_target:
			fill_color = Color(0.6, 0.9, 0.6) # Green for finished
		elif node_num == current_target:
			fill_color = Color(1.0, 0.85, 0.3) # Highlighted active target
			
		node_canvas.draw_circle(pos, node_radii, fill_color)
		node_canvas.draw_arc(pos, node_radii, 0, TAU, 32, border_color, 3.5, true)
		
		# Render the number text centered inside the circle
		var font = ThemeDB.fallback_font
		var font_size = 22
		var text = str(node_num)
		var text_size = font.get_string_size(text, HORIZONTAL_ALIGNMENT_CENTER, -1, font_size)
		node_canvas.draw_string(font, pos - (text_size / 2.0) + Vector2(0, 8), text, HORIZONTAL_ALIGNMENT_CENTER, -1, font_size, border_color)
