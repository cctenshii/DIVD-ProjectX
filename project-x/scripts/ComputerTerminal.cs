using Godot;

public partial class ComputerTerminal : QuestInteractable
{
	[Export] public float PopupDuration = 2.5f;
	[Export] public RotationController[] PuzzleControllers; // Assign all 4 controllers here

	public bool IsUnlocked { get; private set; } = false;
	public bool ChipCollected { get; private set; } = false;

	public override bool Completed => ChipCollected;
	public override string Prompt => IsUnlocked ? "Press Q to collect chip" : "Locked - Align all 4 controllers";

	private bool _playerInRange = false;
	private CanvasLayer _popupLayer;

	public override void _Ready()
	{
		base._Ready(); // Registers with QuestInteraction

		BodyEntered += OnBodyEntered;
		BodyExited += OnBodyExited;

		_popupLayer = GetNodeOrNull<CanvasLayer>("CanvasLayer");
		if (_popupLayer != null)
		{
			_popupLayer.Visible = false;
		}

		if (PuzzleControllers == null || PuzzleControllers.Length == 0)
		{
			IsUnlocked = true;
		}
		else
		{
			CheckPuzzle();
		}
	}

	public override void _Process(double delta)
	{
		if (_playerInRange && !ChipCollected && Input.IsActionJustPressed("interact"))
		{
			if (!IsUnlocked)
			{
				GD.Print("Computer is locked! Align all 4 controllers first.");
				return;
			}

			CollectChip();
		}
	}

	// Required by QuestInteractable
	public override void Interact(QuestInteraction interaction)
	{
		if (!IsUnlocked || ChipCollected) return;
		CollectChip();
	}

	public void CheckPuzzle()
	{
		if (IsUnlocked || PuzzleControllers == null) return;

		foreach (RotationController controller in PuzzleControllers)
		{
			if (controller != null && !controller.IsCorrect)
			{
				return;
			}
		}

		IsUnlocked = true;
		GD.Print("Puzzle Solved! Computer Terminal is now UNLOCKED!");
	}

	private void CollectChip()
	{
		ChipCollected = true;
		GD.Print("Chip Acquired!");

		if (_popupLayer != null)
		{
			_popupLayer.Visible = true;

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
