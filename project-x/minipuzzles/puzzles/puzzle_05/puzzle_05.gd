extends Control

@onready var title_label = $PuzzleUI/VBoxContainer/TitleLabel
@onready var instruction_label = $PuzzleUI/VBoxContainer/InstructionLabel
@onready var status_label = $PuzzleUI/VBoxContainer/StatusLabel
@onready var restart_button = $PuzzleUI/VBoxContainer/RestartButton

@onready var target_display = $PuzzleUI/VBoxContainer/TargetWaveDisplay
@onready var player_display = $PuzzleUI/VBoxContainer/PlayerWaveDisplay

@onready var speed_slider = $PuzzleUI/VBoxContainer/SliderContainer/SpeedRow/SpeedSlider
@onready var height_slider = $PuzzleUI/VBoxContainer/SliderContainer/HeightRow/HeightSlider
@onready var phase_slider = $PuzzleUI/VBoxContainer/SliderContainer/PhaseRow/PhaseSlider

# Target wave parameters (randomized on start)
var target_speed: float = 2.0
var target_height: float = 40.0
var target_phase: float = 0.0

var puzzle_solved: bool = false
const TOLERANCE: float = 0.15 # Accuracy threshold for a win


func _ready() -> void:
	title_label.text = "Frequency Tuning"
	instruction_label.text = "Match your signal to the target wave."
	restart_button.pressed.connect(_restart_puzzle)
	
	# Connect slider changes to check matching
	speed_slider.value_changed.connect(_on_slider_changed)
	height_slider.value_changed.connect(_on_slider_changed)
	phase_slider.value_changed.connect(_on_slider_changed)
	
	# Configure slider min/max ranges
	_setup_sliders()
	_restart_puzzle()


func _setup_sliders() -> void:
	speed_slider.min_value = 0.5
	speed_slider.max_value = 5.0
	speed_slider.step = 0.05

	height_slider.min_value = 10.0
	height_slider.max_value = 80.0
	height_slider.step = 1.0

	phase_slider.min_value = 0.0
	phase_slider.max_value = TAU # Full circle radians
	phase_slider.step = 0.05


func _restart_puzzle() -> void:
	puzzle_solved = false
	status_label.text = "Adjust the sliders to match the wave pattern!"
	
	# Generate random target parameters (avoiding extremes)
	target_speed = randf_range(1.0, 4.0)
	target_height = randf_range(25.0, 65.0)
	target_phase = randf_range(0.0, TAU)
	
	# Randomize player starting sliders so they don't start solved
	speed_slider.value = randf_range(0.5, 5.0)
	height_slider.value = randf_range(10.0, 80.0)
	phase_slider.value = randf_range(0.0, TAU)
	
	queue_redraw()


func _on_slider_changed(_value: float) -> void:
	if puzzle_solved:
		return
		
	queue_redraw()
	_check_win_condition()


func _check_win_condition() -> void:
	var speed_diff = abs(speed_slider.value - target_speed)
	var height_diff = abs(height_slider.value - target_height)
	var phase_diff = abs(phase_slider.value - target_phase)
	
	# Check if all values are within acceptable tolerance
	if speed_diff < TOLERANCE and height_diff < 5.0 and phase_diff < TOLERANCE:
		puzzle_solved = true
		status_label.text = "Signal Synchronized! Puzzle Solved!"
		speed_slider.editable = false
		height_slider.editable = false
		phase_slider.editable = false


# Continuously draw both wave panels
func _process(_delta: float) -> void:
	target_display.queue_redraw()
	player_display.queue_redraw()


# Draw Target Wave in the TargetDisplay control node
func _on_target_wave_display_draw() -> void:
	_draw_wave(target_display, target_speed, target_height, target_phase, Color(0.2, 0.6, 1.0))


# Draw Player Wave in the PlayerDisplay control node
func _on_player_wave_display_draw() -> void:
	var current_speed = speed_slider.value if speed_slider else 2.0
	var current_height = height_slider.value if height_slider else 40.0
	var current_phase = phase_slider.value if phase_slider else 0.0
	
	var wave_color = Color(0.2, 0.9, 0.4) if puzzle_solved else Color(0.9, 0.4, 0.2)
	_draw_wave(player_display, current_speed, current_height, current_phase, wave_color)


# Helper function to plot sine waves smoothly across a Control node's width
func _draw_wave(canvas: Control, freq: float, amp: float, phase: float, color: Color) -> void:
	var width = canvas.size.x
	var height = canvas.size.y
	var center_y = height / 2.0
	
	# Draw center axis line
	canvas.draw_line(Vector2(0, center_y), Vector2(width, center_y), Color(0.7, 0.7, 0.7, 0.5), 1.0, true)
	
	var points = PackedVector2Array()
	var resolution = 2.0 # Pixels per step
	
	var x = 0.0
	while x <= width:
		# Sine wave formula: y = amplitude * sin(frequency * x + phase) + center
		var normalized_x = (x / width) * TAU * freq
		var y = center_y + sin(normalized_x + phase) * amp
		points.append(Vector2(x, y))
		x += resolution
		
	if points.size() > 1:
		canvas.draw_polyline(points, color, 3.0, true)
