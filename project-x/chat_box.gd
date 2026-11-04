extends CanvasLayer

@onready var panel: Panel = $Panel
@onready var label: Label = $Panel/Label

func _ready() -> void:
	# Hide the chat box when the game starts
	panel.visible = false

func show_dialogue(text: String) -> void:
	label.text = text
	panel.visible = true

func hide_dialogue() -> void:
	panel.visible = false
