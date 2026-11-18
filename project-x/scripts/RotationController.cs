using Godot;

public partial class RotationController : Area2D
{
	// 0 = Up, 1 = Right, 2 = Down, 3 = Left
	[Export(PropertyHint.Range, "0,3,1")] public int CurrentDirection = 0;
	[Export(PropertyHint.Range, "0,3,1")] public int TargetDirection = 0;

	[Export] public ComputerTerminal LinkedComputer;

	// Bright golden-yellow glow tint when aligned
	[Export] public Color CorrectColor = new Color(1.5f, 1.4f, 0.3f, 1.0f);
	[Export] public Color DefaultColor = Colors.White;

	private bool _playerInRange = false;
	private Sprite2D _sprite;

	public bool IsCorrect => CurrentDirection == TargetDirection;

	public override void _Ready()
	{
		CollisionMask = 3; // Detects Player on Layer 1 OR Layer 2
		BodyEntered += OnBodyEntered;
		BodyExited += OnBodyExited;

		_sprite = GetNodeOrNull<Sprite2D>("Sprite2D");
		if (_sprite == null)
		{
			GD.PrintErr($"[DEBUG {Name}] Missing child 'Sprite2D'!");
		}

		if (LinkedComputer == null)
		{
			GD.PrintErr($"[DEBUG {Name}] 'LinkedComputer' is NULL (not assigned in Inspector)!");
		}

		UpdateVisuals();
		GD.Print($"[DEBUG {Name}] Ready in scene '{GetTree().CurrentScene?.Name}' | Monitoring={Monitoring}, Mask={CollisionMask}");
	}

	public override void _Process(double delta)
	{
		if (Input.IsActionJustPressed("interact"))
		{
			if (!_playerInRange)
			{
				// Check if player is overlapping physically even if BodyEntered didn't fire
				foreach (Node2D body in GetOverlappingBodies())
				{
					if (body.IsInGroup("Player"))
					{
						GD.PrintErr($"[DEBUG {Name}] Player IS overlapping, but _playerInRange was False! Forcing True.");
						_playerInRange = true;
						break;
					}
				}
			}

			if (!_playerInRange) return; // Still not near this controller

			if (LinkedComputer != null && LinkedComputer.IsUnlocked)
			{
				GD.Print($"[DEBUG {Name}] Blocked: LinkedComputer ({LinkedComputer.Name}) says IsUnlocked = True!");
				return;
			}

			RotateController();
		}
	}

	private void RotateController()
	{
		CurrentDirection = (CurrentDirection + 1) % 4;
		UpdateVisuals();
		GD.Print($"[DEBUG {Name}] Rotated to {CurrentDirection} (Target: {TargetDirection}, IsCorrect: {IsCorrect})");

		if (LinkedComputer != null)
		{
			LinkedComputer.CheckPuzzle();
		}
	}

	private void UpdateVisuals()
	{
		if (_sprite == null) return;

		// Subtract 90 degrees because the base sprite texture points Right at 0 degrees
		_sprite.RotationDegrees = (CurrentDirection * 90f) - 90f;
		_sprite.SelfModulate = IsCorrect ? CorrectColor : DefaultColor;
	}

	private void OnBodyEntered(Node2D body)
	{
		GD.Print($"[DEBUG {Name}] BodyEntered: '{body.Name}' (InGroup('Player'): {body.IsInGroup("Player")})");
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
			GD.Print($"[DEBUG {Name}] Player exited range.");
		}
	}
}
