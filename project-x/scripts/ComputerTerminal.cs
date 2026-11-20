using Godot;
using ProjectXBossArenaPrototype;

public partial class ComputerTerminal : QuestInteractable
{
	[Export] public float PopupDuration = 2.5f;
	public bool ChipCollected { get; private set; }
	public override bool Completed => ChipCollected;
	public override string Prompt => "[ E ] COLLECT NANO.O CHIP";
	private CanvasLayer _popupLayer;
	private double _popupRemaining;

	public override void _Ready()
	{
		base._Ready();
		_popupLayer = GetNodeOrNull<CanvasLayer>("CanvasLayer");
		if (_popupLayer != null)
		{
			_popupLayer.Hide();
			var panel = _popupLayer.GetNode<PanelContainer>("PanelContainer");
			panel.Theme = PXRetroUI.MakeTheme();
		}
	}
	public override void Interact(QuestInteraction interaction)
	{
		if (!CanInteract(interaction.Player)) return;
		ChipCollected = true;
		_popupRemaining = PopupDuration;
		_popupLayer?.Show();
		interaction.Message("NANO.O CHIP ACQUIRED  /  RETURN IT TO THE ROBOT");
	}
	public override void _Process(double delta)
	{
		if (_popupRemaining <= 0) return;
		_popupRemaining -= delta;
		if (_popupRemaining <= 0) _popupLayer?.Hide();
	}
}
