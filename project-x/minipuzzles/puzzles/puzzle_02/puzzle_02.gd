extends Control

@onready var puzzle_title = $PuzzleUI/VBoxContainer/TitleLabel
@onready var score_label = $PuzzleUI/VBoxContainer/ScoreLabel
@onready var restart_button = $PuzzleUI/VBoxContainer/RestartButton

@onready var lane_nodes = [
	$PuzzleUI/VBoxContainer/LaneContainer/LaneW,
	$PuzzleUI/VBoxContainer/LaneContainer/LaneA,
	$PuzzleUI/VBoxContainer/LaneContainer/LaneS,
	$PuzzleUI/VBoxContainer/LaneContainer/LaneD
]

const LANE_KEYS = [KEY_W, KEY_A, KEY_S, KEY_D]
const LANE_NAMES = ["W", "A", "S", "D"]

var score: int = 0
var game_active: bool = false
var notes: Array = []
var spawn_timer: float = 0.0
var song_time: float = 0.0

# Settings
var fall_speed: float = 300.0
var hit_window: float = 50.0  # Pixel tolerance for a successful hit
var target_y_position: int = 400

func _ready() -> void:
	puzzle_title.text = "WASD Rhythm Challenge"
	restart_button.pressed.connect(_restart_puzzle)
	_restart_puzzle()

func _restart_puzzle() -> void:
	score = 0
	song_time = 0.0
	spawn_timer = 0.0
	game_active = true
	score_label.text = "Score: 0"
	
	for note in notes:
		if is_instance_valid(note.node):
			note.node.queue_free()
	notes.clear()

func _process(delta: float) -> void:
	if not game_active:
		return

	song_time += delta
	spawn_timer += delta

	# Spawn a random note every 1.0 seconds
	if spawn_timer >= 1.0:
		spawn_timer = 0.0
		_spawn_random_note()

	# Move all active notes downward locally inside their lanes
	for i in range(notes.size() - 1, -1, -1):
		var note = notes[i]
		note.y_pos += fall_speed * delta
		
		if is_instance_valid(note.node):
			note.node.position.y = note.y_pos

		# If the note goes past the target window without being hit, miss it
		if note.y_pos > target_y_position + hit_window:
			score = max(0, score - 10)
			score_label.text = "Miss! Score: " + str(score)
			note.node.queue_free()
			notes.remove_at(i)

func _unhandled_input(event: InputEvent) -> void:
	if not game_active:
		return

	if event is InputEventKey and event.pressed and not event.echo:
		var lane_index = -1
		
		# Check which WASD key was pressed
		match event.keycode:
			KEY_W: lane_index = 0
			KEY_A: lane_index = 1
			KEY_S: lane_index = 2
			KEY_D: lane_index = 3

		if lane_index != -1:
			_check_hit(lane_index)

func _spawn_random_note() -> void:
	var lane_index = randi() % 4
	var target_lane = lane_nodes[lane_index]
	
	# Create a visual representation for the note
	var note_node = Panel.new()
	note_node.custom_minimum_size = Vector2(70, 30)
	
	note_node.position = Vector2(10, -40) 
	target_lane.add_child(note_node)

	# Store note data
	var note_data = {
		"lane": lane_index,
		"y_pos": -40.0,
		"node": note_node
	}
	notes.append(note_data)

func _check_hit(lane: int) -> void:
	for i in range(notes.size()):
		var note = notes[i]
		if note.lane == lane:
			var distance = abs(note.y_pos - target_y_position)
			
			# Check if the note is within the acceptable hit window
			if distance <= hit_window:
				score += 100
				score_label.text = "Hit! Score: " + str(score)
				
				note.node.queue_free()
				notes.remove_at(i)
				return
			else:
				# Pressed too early or late for this note
				score = max(0, score - 25)
				score_label.text = "Bad Timing! Score: " + str(score)
				return
