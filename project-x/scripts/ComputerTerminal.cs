using Godot;
using ProjectXBossArenaPrototype;

public partial class ComputerTerminal : QuestInteractable
{
	[Export] public float PopupDuration = 2.5f;
	[Export] public RotationController[] PuzzleControllers; // Assign all 4 controllers here

	[ExportGroup("Locked Dialogue")]
	[Export] public string LockedTitle = "COMPUTER LOCKED";
	[Export(PropertyHint.MultilineText)] public string LockedMessage = "ACCESS DENIED.\nALIGN ALL 4 ROTATION CONTROLLERS TO UNLOCK THE NANO.O CHIP.";
	[Export] public float LockedDialogueDuration = 2.2f;

	public bool IsUnlocked { get; private set; } = false;
	public bool ChipCollected { get; private set; } = false;

	public override bool Completed => ChipCollected;
	public override string Prompt => IsUnlocked ? "[ E ] COLLECT NANO.O CHIP" : "[ E ] COMPUTER LOCKED - ALIGN ALL 4 CONTROLLERS";

	private bool _playerInRange = false;
	private CanvasLayer _popupLayer;

	private CanvasLayer _lockedDialogueLayer;
	private double _lockedDialogueTimer;
	private bool _showingLockedDialogue;
	private Node2D _lockedPlayer;

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
		if (_showingLockedDialogue)
		{
			_lockedDialogueTimer -= delta;
			if (_lockedDialogueTimer <= 0)
			{
				CloseLockedDialogue();
			}
			return;
		}

		if (_playerInRange && !ChipCollected && Input.IsActionJustPressed("interact"))
		{
			if (!IsUnlocked)
			{
				ShowLockedDialogue();
				return;
			}

			CollectChip();
		}
	}

	public override void _UnhandledInput(InputEvent input)
	{
		if (!_showingLockedDialogue) return;

		if (input is InputEventKey key && key.Pressed && !key.Echo &&
			(key.Keycode == Key.Enter || key.Keycode == Key.KpEnter || key.Keycode == Key.Escape || input.IsActionPressed("interact")))
		{
			CloseLockedDialogue();
			GetViewport().SetInputAsHandled();
		}
	}

	// Required by QuestInteractable
	public override void Interact(QuestInteraction interaction)
	{
		if (ChipCollected || _showingLockedDialogue) return;

		if (!IsUnlocked)
		{
			ShowLockedDialogue(interaction?.Player);
			return;
		}

		CollectChip();
	}

	private void ShowLockedDialogue(Node2D player = null)
	{
		if (_showingLockedDialogue) return;
		_showingLockedDialogue = true;
		_lockedDialogueTimer = Mathf.Max(0.3f, LockedDialogueDuration);

		// Find player so we can pause movement briefly while the alert is open
		_lockedPlayer = player ?? FindPlayerNode();
		SetPlayerLocked(_lockedPlayer, true);

		_lockedDialogueLayer = new CanvasLayer { Layer = 35 };
		var root = new Control
		{
			Name = "TerminalLockedUI",
			Size = new Vector2(1920, 1080),
			MouseFilter = Control.MouseFilterEnum.Stop,
			Theme = PXRetroUI.MakeTheme()
		};
		_lockedDialogueLayer.AddChild(root);

		root.AddChild(new ColorRect
		{
			Size = root.Size,
			Color = new Color(0.02f, 0.04f, 0.07f, 0.72f),
			MouseFilter = Control.MouseFilterEnum.Ignore
		});

		var panel = PXRetroUI.Panel(root, new Rect2(440, 320, 1040, 390));
		PXRetroUI.Label(panel, LockedTitle, new Rect2(48, 36, 944, 64), 46, new Color("ff4d6d"));

		// Show live progress of how many controllers are currently aligned
		int alignedCount = GetAlignedControllerCount();
		int totalCount = PuzzleControllers?.Length ?? 0;
		string fullMessage = $"{LockedMessage}\nCONTROLLERS ALIGNED: {alignedCount}/{totalCount}";

		var bodyLabel = PXRetroUI.Label(panel, fullMessage, new Rect2(48, 120, 944, 140), 28, new Color("55ead4"));
		bodyLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;

		PXRetroUI.Button(panel, "ACKNOWLEDGE  /  ENTER", new Rect2(48, 276, 944, 72), CloseLockedDialogue);

		GetTree().Root.AddChild(_lockedDialogueLayer);
	}

	private void CloseLockedDialogue()
	{
		if (!_showingLockedDialogue) return;
		_showingLockedDialogue = false;

		if (IsInstanceValid(_lockedDialogueLayer))
		{
			_lockedDialogueLayer.QueueFree();
			_lockedDialogueLayer = null;
		}

		SetPlayerLocked(_lockedPlayer, false);
		_lockedPlayer = null;
	}

	private int GetAlignedControllerCount()
	{
		if (PuzzleControllers == null) return 0;
		int count = 0;
		foreach (RotationController controller in PuzzleControllers)
		{
			if (controller != null && controller.IsCorrect) count++;
		}
		return count;
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

	public override void _ExitTree()
	{
		if (IsInstanceValid(_lockedDialogueLayer))
		{
			_lockedDialogueLayer.QueueFree();
		}
		base._ExitTree();
	}

	private Node2D FindPlayerNode()
	{
		foreach (Node node in GetTree().GetNodesInGroup("Player"))
		{
			if (node is Node2D n2d) return n2d;
		}
		return null;
	}

	private void SetPlayerLocked(Node2D player, bool locked)
	{
		if (!IsInstanceValid(player)) return;
		if (player is PXArenaPlayer combatPlayer) combatPlayer.Locked = locked;
		else player.Set("locked", locked);
		if (player is CharacterBody2D body) body.Velocity = Vector2.Zero;
	}
}
