
extends Control

signal time_up

@export var default_duration: float = 60.0

@onready var time_label: Label = get_node_or_null("TimeLabel") as Label
@onready var countdown: Timer = get_node_or_null("Countdown") as Timer

var duration: float = 60.0
var running: bool = false


func _ready() -> void:
	# Check that the required nodes exist.
	if time_label == null:
		push_error(
			"TimerDisplay: TimeLabel was not found. "
			+ "Check the node name in timer_display.tscn."
		)
		return

	if countdown == null:
		push_error(
			"TimerDisplay: Countdown was not found. "
			+ "Check the node name in timer_display.tscn."
		)
		return

	countdown.one_shot = true

	if not countdown.timeout.is_connected(_on_countdown_timeout):
		countdown.timeout.connect(_on_countdown_timeout)

	duration = default_duration
	_update_display(duration)


func start_timer(seconds: float = -1.0) -> void:
	if countdown == null or time_label == null:
		return

	if seconds > 0:
		duration = seconds
	else:
		duration = default_duration

	countdown.stop()
	countdown.wait_time = duration

	_update_display(duration)

	running = true
	countdown.start()


func stop_timer() -> void:
	running = false

	if countdown != null:
		countdown.stop()


func reset_timer() -> void:
	stop_timer()
	_update_display(duration)


func _process(_delta: float) -> void:
	if running and countdown != null:
		_update_display(countdown.time_left)


func _update_display(seconds: float) -> void:
	if time_label == null:
		return

	var total_seconds: int = max(0, int(ceil(seconds)))
	var minutes: int = total_seconds / 60
	var remaining_seconds: int = total_seconds % 60

	time_label.text = "%02d:%02d" % [
		minutes,
		remaining_seconds
	]


func _on_countdown_timeout() -> void:
	running = false
	_update_display(0)
	time_up.emit()
