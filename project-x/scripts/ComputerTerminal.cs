using Godot;

public partial class ComputerTerminal : Area2D
{
	[Export] public float PopupDuration = 2.5f; // How many seconds the popup stays on screen

	private bool _playerInRange = false;
	private bool _chipCollected = false;
	private CanvasLayer _popupLayer;

	public override void _Ready()
	{
		BodyEntered += OnBodyEntered;
		BodyExited += OnBodyExited;

		_popupLayer = GetNodeOrNull<CanvasLayer>("CanvasLayer");
		if (_popupLayer != null)
		{
			_popupLayer.Visible = false;
		}
	}

	public override void _Process(double delta)
	{
		if (_playerInRange && !_chipCollected && Input.IsActionJustPressed("interact"))
		{
			CollectChip();
		}
	}

	private void CollectChip()
	{
		_chipCollected = true;
		GD.Print("Chip Acquired!");

		if (_popupLayer != null)
		{
			_popupLayer.Visible = true;

			// Automatically hide the popup after PopupDuration seconds
			GetTree().CreateTimer(PopupDuration).Timeout += () =>
			{
				if (IsInstanceValid(_popupLayer))
				{
					_popupLayer.Visible = false;
				}
			};
		}
	}

	private void OnBodyEntered(Node2D body)
	{
		if (body.IsInGroup("Player"))
		{
			_playerInRange = true;
			if (!_chipCollected)
			{
				GD.Print("Press Q to collect chip");
			}
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
