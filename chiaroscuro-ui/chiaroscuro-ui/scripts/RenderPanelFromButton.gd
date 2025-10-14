extends Button

@export var target_panel: PanelContainer    # Drag any PanelContainer here
@export var auto_find_nearest: bool = true  # If not set, try to find the nearest PanelContainer up the tree

func _ready() -> void:
	pressed.connect(_on_pressed)

	if target_panel == null and auto_find_nearest:
		target_panel = _find_nearest_panel()

	if target_panel == null:
		push_warning("No PanelContainer assigned. Set 'target_panel' or enable 'auto_find_nearest' and place the Button under a PanelContainer.")

func _on_pressed() -> void:
	if target_panel:
		target_panel.visible = not target_panel.visible

func _find_nearest_panel() -> PanelContainer:
	var n := get_parent()
	while n:
		if n is PanelContainer:
			return n
		n = n.get_parent()
	return null
