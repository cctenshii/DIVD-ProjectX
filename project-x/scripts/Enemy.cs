using Godot;

public partial class Enemy : Area2D
{
	[Export] public int HitsToDefeat = 3;
	[Export] public Doors[] UnlockDoorsOnDefeat; // Optional: link doors if defeating this enemy opens a room!

	private int _currentHits = 0;
	private bool _playerInRange = false;
	private bool _isDefeated = false;

	public override void _Ready()
	{
		BodyEntered += OnBodyEntered;
		BodyExited += OnBodyExited;
	}

	public override void _Process(double delta)
	{
		if (_playerInRange && !_isDefeated && Input.IsActionJustPressed("interact"))
		{
			TakeHit();
		}
	}

	private void TakeHit()
	{
		_currentHits++;
		GD.Print($"Enemy hit! ({_currentHits}/{HitsToDefeat})");

		// Quick visual flash so you know the Q press registered
		Modulate = new Color(1, 0.3f, 0.3f); // Flash red
		GetTree().CreateTimer(0.1).Timeout += () =>
		{
			if (IsInstanceValid(this)) Modulate = Colors.White;
		};

		if (_currentHits >= HitsToDefeat)
		{
			DefeatEnemy();
		}
	}

	private void DefeatEnemy()
	{
		_isDefeated = true;
		GD.Print("Enemy defeated!");

		// Optional: if you linked any doors in the Inspector, activate them
		if (UnlockDoorsOnDefeat != null)
		{
			foreach (Doors door in UnlockDoorsOnDefeat)
			{
				if (door != null) door.Activate();
			}
		}

		// Remove the enemy from the scene
		QueueFree();
	}

	private void OnBodyEntered(Node2D body)
	{
		if (body.IsInGroup("Player"))
		{
			_playerInRange = true;
			GD.Print("Press Q to attack enemy!");
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
