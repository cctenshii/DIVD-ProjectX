extends Control

@onready var puzzle_title = $PuzzleUI/VBoxContainer/TitleLabel
@onready var status_label = $PuzzleUI/VBoxContainer/StatusLabel
@onready var restart_button = $PuzzleUI/VBoxContainer/RestartButton
@onready var equalizer_container = $PuzzleUI/VBoxContainer/EqualizerContainer

const NUM_CHANNELS: int = 8
const TOLERANCE: float = 3.0 # How close the player needs to be (in %)

var target_values: Array[float] = []
var player_sliders: Array[VSlider] = []
var player_bars: Array[ProgressBar] = []
var target_bars: Array[ProgressBar] = []

var puzzle_solved: bool = false


func _ready() -> void:
	puzzle_title.text = "Fill the bars the exact same as the ones above"
	restart_button.pressed.connect(_restart_puzzle)
	
	_setup_channel_references()
	_restart_puzzle()


func _setup_channel_references() -> void:
	player_sliders.clear()
	player_bars.clear()
	target_bars.clear()

	# Find all child VBoxContainers representing each column/channel
	var channels = equalizer_container.get_children()
	
	for i in range(channels.size()):
		var channel = channels[i]
		
		# Expecting structure: Channel -> TargetBar, PlayerBar, Slider
		var target_bar = channel.get_node("TargetBar") as ProgressBar
		var player_bar = channel.get_node("PlayerBar") as ProgressBar
		var slider = channel.get_node("Slider") as VSlider
		
		target_bars.append(target_bar)
		player_bars.append(player_bar)
		player_sliders.append(slider)
		
		slider.value_changed.connect(_on_slider_value_changed.bind(i))


func _restart_puzzle() -> void:
	puzzle_solved = false
	status_label.text = "Match all 8 levels!"
	target_values.clear()

	for i in range(player_sliders.size()):
		# Generate a random target level between 10% and 90%
		var random_target = randf_range(10.0, 90.0)
		target_values.append(random_target)
		
		# Update target visual bar
		target_bars[i].value = random_target
		
		# Reset player sliders and bars to 0
		player_sliders[i].value = 0
		player_bars[i].value = 0


func _on_slider_value_changed(value: float, channel_index: int) -> void:
	if puzzle_solved:
		return

	# Update the corresponding player bar
	player_bars[channel_index].value = value
	
	_check_win_condition()


func _check_win_condition() -> void:
	var all_correct: bool = true

	for i in range(target_values.size()):
		var difference = abs(player_sliders[i].value - target_values[i])
		
		if difference > TOLERANCE:
			all_correct = false
			break

	if all_correct:
		puzzle_solved = true
		status_label.text = "Puzzle Solved! Perfect Match!"
