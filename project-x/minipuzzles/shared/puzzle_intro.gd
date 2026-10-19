
extends Control

signal start_requested
signal cancel_requested

@onready var title_label: Label = $Title
@onready var description_label: Label = $Description

@onready var how_it_works_button: Button = $HowItWorksButton
@onready var start_button: Button = $StartButton
@onready var cancel_button: Button = $CancelButton


func _ready() -> void:
	# Make sure the intro scene is visible
	show()

	# Set default text so the scene works when run by itself
	title_label.text = "Memory Puzzle"
	description_label.text = (
		"Watch the sequence carefully. "
		+ "Remember the order of the four buttons "
		+ "and repeat it correctly. Each round adds "
		+ "one more step!"
	)

	# Connect the buttons
	start_button.pressed.connect(_on_start_pressed)
	cancel_button.pressed.connect(_on_cancel_pressed)
	how_it_works_button.pressed.connect(
		_on_how_it_works_pressed
	)

	# Initial visibility
	title_label.show()
	how_it_works_button.show()
	cancel_button.show()

	description_label.hide()
	start_button.hide()


# Set the intro title and description
func setup(title: String, description: String) -> void:
	title_label.text = title
	description_label.text = description


# Show the instructions
func _on_how_it_works_pressed() -> void:
	description_label.show()
	start_button.show()
	how_it_works_button.hide()


# Start the puzzle
func _on_start_pressed() -> void:
	start_requested.emit()


# Cancel the puzzle
func _on_cancel_pressed() -> void:
	cancel_requested.emit()
