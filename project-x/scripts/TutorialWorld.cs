#nullable enable
using Godot;

public partial class TutorialWorld : Node2D
{
	[Export] public string MainGameScenePath { get; set; } = "res://scenes/World.tscn";
	[Export] public float MoveSpeed { get; set; } = 220.0f;
	[Export] public float DashSpeedMultiplier { get; set; } = 2.6f;
	[Export] public float DashDuration { get; set; } = 0.16f;
	[Export] public int PromptFontSize { get; set; } = 36;
	[Export] public int ButtonFontSize { get; set; } = 40;

	private enum TutorialStep
	{
		PressW,
		PressA,
		PressS,
		PressD,
		PressShift,
		Completed
	}

	private CharacterBody2D? _player;
	private Label _promptLabel = null!;
	private Button _startGameButton = null!;

	private TutorialStep _currentStep = TutorialStep.PressW;
	private float _dashTimer = 0.0f;
	private Vector2 _dashDirection = Vector2.Down;
	private Vector2 _lastMoveDirection = Vector2.Down;

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

		UpdateStepPrompt();
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (@event is not InputEventKey keyEvent || !keyEvent.Pressed || keyEvent.Echo)
		{
			return;
		}

		switch (_currentStep)
		{
			case TutorialStep.PressW when keyEvent.Keycode == Key.W:
				_currentStep = TutorialStep.PressA;
				UpdateStepPrompt();
				break;

			case TutorialStep.PressA when keyEvent.Keycode == Key.A:
				_currentStep = TutorialStep.PressS;
				UpdateStepPrompt();
				break;

			case TutorialStep.PressS when keyEvent.Keycode == Key.S:
				_currentStep = TutorialStep.PressD;
				UpdateStepPrompt();
				break;

			case TutorialStep.PressD when keyEvent.Keycode == Key.D:
				_currentStep = TutorialStep.PressShift;
				UpdateStepPrompt();
				break;

			case TutorialStep.PressShift when keyEvent.Keycode == Key.Shift:
				TriggerDash();
				_currentStep = TutorialStep.Completed;
				UpdateStepPrompt();
				break;
		}
	}

	public override void _PhysicsProcess(double delta)
	{
		if (_player == null)
		{
			return;
		}

		// Integration (merge): scenes/Player.tscn is the game's own Player (Player.cs), which already
		// handles WASD/arrows and Shift-dash with the same speed and cooldown as in the World.
		// Moving it here too would apply two movements per frame, so the tutorial only tracks the steps.
		if (_player is ProjectX.Player)
		{
			return;
		}

		float dt = (float)delta;

		// Allow dashing even after completing the step
		if (Input.IsKeyPressed(Key.Shift) && _dashTimer <= 0.0f && _currentStep == TutorialStep.Completed)
		{
			TriggerDash();
		}

		if (_dashTimer > 0.0f)
		{
			_dashTimer -= dt;
			_player.Velocity = _dashDirection * (MoveSpeed * DashSpeedMultiplier);
			_player.MoveAndSlide();
			return;
		}

		Vector2 moveInput = GetWasdInput();
		if (moveInput != Vector2.Zero)
		{
			Vector2 normalized = moveInput.Normalized();
			_lastMoveDirection = normalized;
			_player.Velocity = normalized * MoveSpeed;
			_player.MoveAndSlide();
		}
		else
		{
			_player.Velocity = Vector2.Zero;
			_player.MoveAndSlide();
		}
	}

	private void TriggerDash()
	{
		Vector2 currentInput = GetWasdInput();
		_dashDirection = currentInput != Vector2.Zero ? currentInput.Normalized() : _lastMoveDirection;
		_dashTimer = DashDuration;
	}

	private void UpdateStepPrompt()
	{
		_promptLabel.Text = _currentStep switch
		{
			TutorialStep.PressW =>
				"Step 1/5: Press [W] to move UP!",
			TutorialStep.PressA =>
				"Step 2/5: Press [A] to move LEFT!",
			TutorialStep.PressS =>
				"Step 3/5: Press [S] to move DOWN!",
			TutorialStep.PressD =>
				"Step 4/5: Press [D] to move RIGHT!",
			TutorialStep.PressShift =>
				"Step 5/5: Press [LSHIFT] to DASH!",
			_ =>
				"Nice! You're ready to explore.\n" +
				"Puzzles use Q / W / E / R (rhythm) and mouse/slider controls."
		};
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
