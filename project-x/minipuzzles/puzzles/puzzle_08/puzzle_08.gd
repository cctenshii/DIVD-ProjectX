extends Control

@onready var title_label = $PuzzleUI/VBoxContainer/TitleLabel
@onready var instruction_label = $PuzzleUI/VBoxContainer/InstructionLabel
@onready var target_label = $PuzzleUI/VBoxContainer/TargetLabel
@onready var status_label = $PuzzleUI/VBoxContainer/StatusLabel
@onready var restart_button = $PuzzleUI/VBoxContainer/RestartButton

@onready var slot_buttons = [
	$PuzzleUI/VBoxContainer/SlotContainer/Slot0,
	$PuzzleUI/VBoxContainer/SlotContainer/Slot1
]

@onready var inventory_buttons = [
	$PuzzleUI/VBoxContainer/InventoryContainer/FuseBtn2,
	$PuzzleUI/VBoxContainer/InventoryContainer/FuseBtn3,
	$PuzzleUI/VBoxContainer/InventoryContainer/FuseBtn5,
	$PuzzleUI/VBoxContainer/InventoryContainer/FuseBtn7
]

# Puzzle data
var target_energy: int = 10
var available_fuses: Array[int] = [2, 3, 5, 7]
var slot_values: Array = [null, null] # Stores fuse value or null for each slot

var selected_fuse_value: int = -1
var puzzle_solved: bool = false


func _ready() -> void:
	title_label.text = "08 - Fuse Placement"
	instruction_label.text = "Choose fuses to reach the exact target energy."
	restart_button.pressed.connect(_restart_puzzle)
	
	# Connect inventory fuse buttons
	for i in range(inventory_buttons.size()):
		inventory_buttons[i].text = str(available_fuses[i])
		inventory_buttons[i].pressed.connect(_on_inventory_fuse_pressed.bind(available_fuses[i]))
		
	# Connect socket slot buttons
	for i in range(slot_buttons.size()):
		slot_buttons[i].pressed.connect(_on_slot_pressed.bind(i))
		
	_restart_puzzle()


func _restart_puzzle() -> void:
	puzzle_solved = false
	selected_fuse_value = -1
	slot_values = [null, null]
	
	target_energy = 10
	target_label.text = "TARGET: " + str(target_energy)
	
	for slot in slot_buttons:
		slot.text = "[ Empty ]"
		slot.modulate = Color.WHITE
		
	for btn in inventory_buttons:
		btn.modulate = Color.WHITE
		
	_update_status()


func _on_inventory_fuse_pressed(value: int) -> void:
	if puzzle_solved:
		return
	
	selected_fuse_value = value
	status_label.text = "Selected fuse: " + str(value) + ". Click a slot to place it."
	
	# Highlight selected inventory item
	for i in range(inventory_buttons.size()):
		if available_fuses[i] == value:
			inventory_buttons[i].modulate = Color(1.0, 0.85, 0.3)
		else:
			inventory_buttons[i].modulate = Color.WHITE


func _on_slot_pressed(slot_index: int) -> void:
	if puzzle_solved:
		return
		
	if selected_fuse_value == -1:
		# If slot already has a fuse, clear it back out
		if slot_values[slot_index] != null:
			slot_values[slot_index] = null
			slot_buttons[slot_index].text = "[ Empty ]"
			_update_status()
		else:
			status_label.text = "Select a fuse from the bottom inventory first!"
		return
		
	# Place selected fuse into slot
	slot_values[slot_index] = selected_fuse_value
	slot_buttons[slot_index].text = str(selected_fuse_value)
	
	# Reset selection
	selected_fuse_value = -1
	for btn in inventory_buttons:
		btn.modulate = Color.WHITE
		
	_update_status()
	_check_win_condition()


func _update_status() -> void:
	var current_total = 0
	for val in slot_values:
		if val != null:
			current_total += val
			
	status_label.text = "Current Energy: " + str(current_total) + " / " + str(target_energy)


func _check_win_condition() -> void:
	var current_total = 0
	for val in slot_values:
		if val != null:
			current_total += val
			
	# Win condition: sum equals target energy and both slots are filled
	if current_total == target_energy and slot_values[0] != null and slot_values[1] != null:
		puzzle_solved = true
		status_label.text = "Circuit Active! Puzzle Solved!"
		for slot in slot_buttons:
			slot.modulate = Color(0.5, 1.0, 0.5)
