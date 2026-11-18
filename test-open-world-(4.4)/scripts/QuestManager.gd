extends Node


signal quest_advanced(current_step)

var quest_step: int = 0

func advance_quest():
	quest_step += 1
	emit_signal("quest_advanced", quest_step)
