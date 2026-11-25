using Godot;
using System.Threading.Tasks;
using ProjectX;

public partial class QuestEntrance : Area2D
{
	[Export]
	public Marker2D DestinationMarker { get; set; }

	// Adjust how fast the fade-out and fade-in happen (in seconds)
	[Export]
	public float FadeDuration { get; set; } = 0.4f;

	private static bool _isTeleporting = false;
	private static double _lastTeleportTime = 0;
	private const double TeleportCooldown = 0.5;

	public override void _Ready()
	{
		BodyEntered += OnBodyEntered;
	}

	private async void OnBodyEntered(Node2D body)
	{
		if (!body.IsInGroup("Player") || _isTeleporting)
		{
			return;
		}

		double currentTime = Time.GetTicksMsec() / 1000.0;
		if (currentTime - _lastTeleportTime < TeleportCooldown)
		{
			return;
		}

		if (DestinationMarker == null)
		{
			GD.PrintErr($"[QuestEntrance:{Name}] FAILED: DestinationMarker is not assigned!");
			return;
		}

		_isTeleporting = true;
		try
		{
			await TeleportWithFade(body);
		}
		finally
		{
			_lastTeleportTime = Time.GetTicksMsec() / 1000.0;
			_isTeleporting = false;
		}
	}

	private async Task TeleportWithFade(Node2D player)
	{
		// Lock player movement immediately upon entering the trigger
		SetPlayerLocked(player, true);

		CanvasLayer canvasLayer = null;
		try
		{
			// 1. Create a high-layer CanvasLayer so the black screen covers everything
			canvasLayer = new CanvasLayer { Layer = 100 };
			var fadeRect = new ColorRect
			{
				Color = new Color(0, 0, 0, 0), // Start completely transparent
				MouseFilter = Control.MouseFilterEnum.Ignore
			};
			fadeRect.SetAnchorsPreset(Control.LayoutPreset.FullRect);

			canvasLayer.AddChild(fadeRect);
			GetTree().Root.AddChild(canvasLayer);

			// 2. Fade to black
			var tweenOut = CreateTween();
			tweenOut.TweenProperty(fadeRect, "color:a", 1.0f, FadeDuration);
			await ToSignal(tweenOut, Tween.SignalName.Finished);

			// 3. Teleport the player while the screen is completely black
			if (IsInstanceValid(player) && IsInstanceValid(DestinationMarker))
			{
				player.GlobalPosition = DestinationMarker.GlobalPosition;

				// Snap Camera2D immediately so it doesn't glide across the black screen
				var camera = player.GetNodeOrNull<Camera2D>("Camera2D") ?? GetViewport().GetCamera2D();
				camera?.ResetSmoothing();
			}

			// Brief pause on black so the camera/tiles settle cleanly
			await ToSignal(GetTree().CreateTimer(0.15f), SceneTreeTimer.SignalName.Timeout);

			// 4. Fade back in to reveal the new area
			var tweenIn = CreateTween();
			tweenIn.TweenProperty(fadeRect, "color:a", 0.0f, FadeDuration);
			await ToSignal(tweenIn, Tween.SignalName.Finished);
		}
		finally
		{
			// 5. Unlock the player and clean up the temporary overlay
			SetPlayerLocked(player, false);

			if (IsInstanceValid(canvasLayer))
			{
				canvasLayer.QueueFree();
			}
		}
	}

	private static void SetPlayerLocked(Node2D player, bool locked)
	{
		if (!IsInstanceValid(player)) return;

		if (player is Player combatPlayer)
		{
			combatPlayer.Locked = locked;
		}
		else
		{
			player.Set("locked", locked);
		}

		if (player is CharacterBody2D body)
		{
			body.Velocity = Vector2.Zero;
		}
	}
}
