using Godot;

public partial class TutorialWorld : Node2D
{
	[Export] public string MainGameScenePath { get; set; } = "res://scenes/World.tscn";
	[Export] public float MoveSpeed { get; set; } = 220.0f;
	[Export] public int PromptFontSize { get; set; } = 36;
	[Export] public int ButtonFontSize { get; set; } = 40;

	private CharacterBody2D? _player;
	private Label _promptLabel = null!;
	private Button _startGameButton = null!;

	public override void _Ready()
	{
		_player = GetNodeOrNull<CharacterBody2D>("Player");
		_promptLabel = GetNode<Label>("UILayer/PanelContainer/MarginContainer/VBoxContainer/PromptLabel");
		_startGameButton = GetNode<Button>("UILayer/PanelContainer/MarginContainer/VBoxContainer/StartGameButton");

		// Center text and button nicely
		_promptLabel.HorizontalAlignment = HorizontalAlignment.Center;
		_promptLabel.AddThemeFontSizeOverride("font_size", PromptFontSize);

		_startGameButton.AddThemeFontSizeOverride("font_size", ButtonFontSize);
		_startGameButton.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;

		// Style button states (normal, hover, pressed, focus)
		var normalStyle = new StyleBoxFlat();
		normalStyle.BgColor = new Color(0.1f, 0.1f, 0.0f);
		normalStyle.CornerRadiusTopLeft = 8;
		normalStyle.CornerRadiusTopRight = 8;
		normalStyle.CornerRadiusBottomLeft = 8;
		normalStyle.CornerRadiusBottomRight = 8;
		normalStyle.ContentMarginLeft = 24;
		normalStyle.ContentMarginRight = 24;
		normalStyle.ContentMarginTop = 10;
		normalStyle.ContentMarginBottom = 10;

		var hoverStyle = (StyleBoxFlat)normalStyle.Duplicate();
		hoverStyle.BgColor = new Color(0.7f, 0.56f, 0.1f);

		var pressedStyle = (StyleBoxFlat)normalStyle.Duplicate();
		pressedStyle.BgColor = new Color(0.4f, 0.34f, 0.1f);

		_startGameButton.AddThemeStyleboxOverride("normal", normalStyle);
		_startGameButton.AddThemeStyleboxOverride("hover", hoverStyle);
		_startGameButton.AddThemeStyleboxOverride("pressed", pressedStyle);
		_startGameButton.AddThemeStyleboxOverride("focus", normalStyle);

		_startGameButton.Pressed += OnStartGamePressed;

		_promptLabel.Text =
			"Move with WASD to explore! Dash with LSHIFT!\n" +
			"Puzzles use Q / W / E / R (rhythm) and mouse/slider controls.";
	}

	public override void _PhysicsProcess(double delta)
	{
		if (_player == null)
		{
			return;
		}

		Vector2 moveInput = GetWasdInput();
		if (moveInput != Vector2.Zero)
		{
			_player.Velocity = moveInput.Normalized() * MoveSpeed;
			_player.MoveAndSlide();
		}
		else
		{
			_player.Velocity = Vector2.Zero;
			_player.MoveAndSlide();
		}
	}

	private static Vector2 GetWasdInput()
	{
		float x = 0.0f;
		float y = 0.0f;

		if (Input.IsKeyPressed(Key.A) || Input.IsActionPressed("ui_left"))
			x -= 1.0f;
		if (Input.IsKeyPressed(Key.D) || Input.IsActionPressed("ui_right"))
			x += 1.0f;
		if (Input.IsKeyPressed(Key.W) || Input.IsActionPressed("ui_up"))
			y -= 1.0f;
		if (Input.IsKeyPressed(Key.S) || Input.IsActionPressed("ui_down"))
			y += 1.0f;

		return new Vector2(x, y);
	}

	private void OnStartGamePressed()
	{
		if (!string.IsNullOrEmpty(MainGameScenePath) && ResourceLoader.Exists(MainGameScenePath))
		{
			GetTree().ChangeSceneToFile(MainGameScenePath);
		}
	}
}
