using Godot;
using ProjectX;

public partial class Enemy : QuestInteractable
{
	// Original three-hit balance is preserved: a hit now means a solved puzzle.
	[Export(PropertyHint.Range, "1,10,1")] public int HitsToDefeat = 3;
	[Export] public Doors[] UnlockDoorsOnDefeat = System.Array.Empty<Doors>();

	[ExportGroup("Breach Dialogue")]
	[Export] public string BreachTitle = "INTRUDER BREACH";
	[Export(PropertyHint.MultilineText)] public string BreachMessage = "ANNIHILATION PROCESS INITIATED.\nBYPASS ENEMY SECURITY TO SURVIVE.";
	[Export] public float DialogueDuration = 1.8f;

	public int SuccessfulHacks { get; private set; }
	public override bool Completed => IsDefeated;
	public bool IsDefeated { get; private set; }
	public override string Prompt => $"HACKING ENEMY  /  {SuccessfulHacks}/{Mathf.Max(1, HitsToDefeat)}";

	private CanvasLayer _dialogueLayer;
	private double _dialogueTimer;
	private bool _showingDialogue;
	private QuestInteraction _pendingInteraction;

	public override void _Ready()
	{
		base._Ready();
		BodyEntered += OnEnemyBodyEntered;
	}

	public override void _PhysicsProcess(double delta)
	{
		if (!_showingDialogue) return;

		_dialogueTimer -= delta;
		if (_dialogueTimer <= 0)
		{
			ConfirmBreachAndStartHack();
		}
	}

	public override void _UnhandledInput(InputEvent input)
	{
		if (!_showingDialogue) return;

		if (input is InputEventKey key && key.Pressed && !key.Echo &&
			(key.Keycode == Key.Enter || key.Keycode == Key.KpEnter || input.IsActionPressed("interact")))
		{
			ConfirmBreachAndStartHack();
			GetViewport().SetInputAsHandled();
		}
	}

	private void OnEnemyBodyEntered(Node2D body)
	{
		if (IsDefeated || _showingDialogue || !body.IsInGroup("Player")) return;

		var interaction = GetQuestInteraction();
		if (interaction != null && !interaction.Busy && CanInteract(interaction.Player))
		{
			ShowBreachDialogue(interaction);
		}
	}

	public override void Interact(QuestInteraction interaction)
	{
		if (IsDefeated || _showingDialogue) return;
		if (!interaction.Busy && CanInteract(interaction.Player))
		{
			ShowBreachDialogue(interaction);
		}
	}

	private void ShowBreachDialogue(QuestInteraction interaction)
	{
		_pendingInteraction = interaction;
		_showingDialogue = true;
		_dialogueTimer = Mathf.Max(0.2f, DialogueDuration);

		// Lock player movement while the breach warning is on screen
		SetPlayerLocked(interaction.Player, true);

		// Build the retro dialogue overlay directly from Enemy.cs
		_dialogueLayer = new CanvasLayer { Layer = 35 };
		var root = new Control
		{
			Name = "EnemyBreachUI",
			Size = new Vector2(1920, 1080),
			MouseFilter = Control.MouseFilterEnum.Stop,
			Theme = RetroUI.MakeTheme()
		};
		_dialogueLayer.AddChild(root);

		// Subtle dark tint behind the alert box
		root.AddChild(new ColorRect
		{
			Size = root.Size,
			Color = new Color(0.02f, 0.04f, 0.07f, 0.72f),
			MouseFilter = Control.MouseFilterEnum.Ignore
		});

		var panel = RetroUI.Panel(root, new Rect2(440, 320, 1040, 390));
		RetroUI.Label(panel, BreachTitle, new Rect2(48, 36, 944, 64), 46, new Color("ff4d6d"));

		string formattedMessage = $"{BreachMessage}\nSECURITY LAYERS: {Mathf.Max(1, HitsToDefeat)}";
		var bodyLabel = RetroUI.Label(panel, formattedMessage, new Rect2(48, 120, 944, 140), 28, new Color("55ead4"));
		bodyLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;

		RetroUI.Button(panel, "ENGAGE  /  ENTER", new Rect2(48, 276, 944, 72), ConfirmBreachAndStartHack);

		GetTree().Root.AddChild(_dialogueLayer);
	}

	private void ConfirmBreachAndStartHack()
	{
		if (!_showingDialogue) return;
		_showingDialogue = false;

		if (IsInstanceValid(_dialogueLayer))
		{
			_dialogueLayer.QueueFree();
			_dialogueLayer = null;
		}

		if (IsInstanceValid(_pendingInteraction) && !IsDefeated && CanInteract(_pendingInteraction.Player))
		{
			// StartHack will keep the player locked and open the hack puzzle
			_pendingInteraction.StartHack(this);
		}
		else if (IsInstanceValid(_pendingInteraction))
		{
			SetPlayerLocked(_pendingInteraction.Player, false);
		}

		_pendingInteraction = null;
	}

	// Called only after the active puzzle resolves successfully, never on raw E input.
	public void CompleteHack()
	{
		if (IsDefeated) return;
		SuccessfulHacks++;
		if (SuccessfulHacks < Mathf.Max(1, HitsToDefeat)) return;
		IsDefeated = true;
		foreach (var door in UnlockDoorsOnDefeat)
			if (IsInstanceValid(door)) door.Activate(this);
		Hide();
		SetDeferred(Area2D.PropertyName.Monitoring, false);
		// Keep the small node alive until scene exit so result UI and progress remain valid.
	}

	public override void _ExitTree()
	{
		if (IsInstanceValid(_dialogueLayer))
		{
			_dialogueLayer.QueueFree();
		}
		base._ExitTree();
	}

	private void SetPlayerLocked(Node2D player, bool locked)
	{
		if (!IsInstanceValid(player)) return;
		if (player is Player combatPlayer) combatPlayer.Locked = locked;
		else player.Set("locked", locked);
		if (player is CharacterBody2D body) body.Velocity = Vector2.Zero;
	}

	private QuestInteraction GetQuestInteraction()
	{
		return GetTree().Root.GetNodeOrNull<QuestInteraction>("QuestInteraction")
			?? FindQuestInteractionRecursive(GetTree().Root);
	}

	private static QuestInteraction FindQuestInteractionRecursive(Node root)
	{
		if (root is QuestInteraction qi) return qi;
		foreach (Node child in root.GetChildren())
		{
			var found = FindQuestInteractionRecursive(child);
			if (found != null) return found;
		}
		return null;
	}
}
