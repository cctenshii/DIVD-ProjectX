using Godot;

public partial class DoorController : Area2D
{
	// Assign 1 or more doors in the Inspector (e.g., Room 2 Door + Final Reward Door)
	[Export] public Doors[] TargetDoors; 
	[Export] public bool OneTimeUse = true;

	private bool _playerInRange = false;
	private bool _hasBeenUsed = false;

	public override void _Ready()
	{
		BodyEntered += OnBodyEntered;
		BodyExited += OnBodyExited;
	}

	public override void _Process(double delta)
	{
		if (_playerInRange && !_hasBeenUsed && Input.IsActionJustPressed("interact"))
		{
			TriggerController();
		}
	}

	private void TriggerController()
	{
		_hasBeenUsed = OneTimeUse;
		GD.Print("Controller activated!");

		if (TargetDoors != null && TargetDoors.Length > 0)
		{
			foreach (Doors door in TargetDoors)
			{
				if (door != null)
				{
					door.Activate();
				}
			}
		}
		else
		{
			GD.PrintErr("No TargetDoors assigned to this controller!");
		}
	}

	private void OnBodyEntered(Node2D body)
	{
		if (body.IsInGroup("Player"))
		{
			_playerInRange = true;
			GD.Print("Press Q to interact");
		}
	}

	private void OnBodyExited(Node2D body)
	{
		if (body.IsInGroup("Player"))
		{
			_playerInRange = false;
		}
	}
}
