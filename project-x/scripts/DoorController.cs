using Godot;

public partial class DoorController : QuestInteractable
{
	[Export] public Doors[] TargetDoors = System.Array.Empty<Doors>();
	[Export] public bool OneTimeUse = true;
	private bool _hasBeenUsed;
	public override bool Completed => _hasBeenUsed && OneTimeUse;
	public override string Prompt => "[ E ] ACTIVATE CONSOLE";

	public override void Interact(QuestInteraction interaction)
	{
		if (!CanInteract(interaction.Player)) return;
		bool changed = false;
		foreach (var door in TargetDoors)
			if (IsInstanceValid(door)) changed |= door.Activate(this);
		if (!changed)
		{
			interaction.Message("CONSOLE ALREADY REGISTERED, OR NO DOOR IS LINKED.");
			return;
		}
		_hasBeenUsed = true;
		Modulate = new Color(.55f, 1f, .8f);
		interaction.Message("CONSOLE ACTIVATED  /  DOOR SIGNAL REGISTERED");
	}
}
