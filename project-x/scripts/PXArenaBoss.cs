using Godot;

namespace ProjectXBossArenaPrototype;

public partial class PXArenaBoss : CharacterBody2D
{
	[Signal] public delegate void HealthChangedEventHandler(int current, int maximum);
	[Signal] public delegate void ContactHitEventHandler(int damage);
	[Signal] public delegate void DiedEventHandler();

	[Export] public float Speed { get; set; } = 90f;
	[Export] public int MaxHealth { get; set; } = 160;
	[Export] public int ContactDamage { get; set; } = 15;
	[Export] public float HackRange { get; set; } = 140f;

	public int Health { get; private set; }
	public bool Locked { get; set; }
	public bool IsAlive => Health > 0;

	private readonly RandomNumberGenerator _random = new();
	private Area2D _contact;
	private Timer _wanderTimer;
	private Polygon2D _body;
	private Label _prompt;
	private Vector2 _direction;
	private float _flash;
	private bool _canHack;

	public override void _Ready()
	{
		MotionMode = MotionModeEnum.Floating;
		_random.Randomize();
		_contact = GetNode<Area2D>("ContactArea");
		_body = GetNode<Polygon2D>("Body");
		_prompt = GetNode<Label>("HackPrompt");
		_wanderTimer = GetNode<Timer>("WanderTimer");
		_wanderTimer.Timeout += ChooseDirection;
		ResetBoss(GlobalPosition);
	}

	public override void _PhysicsProcess(double delta)
	{
		_flash = Mathf.Max(0f, _flash - (float)delta);
		_body.Modulate = !IsAlive ? new Color(0.45f, 0.45f, 0.45f) :
			_flash > 0f ? new Color(2f, 2f, 2f) : Colors.White;
		_wanderTimer.Paused = Locked || !IsAlive;

		if (Locked || !IsAlive)
		{
			Velocity = Vector2.Zero;
			return;
		}

		Velocity = _direction * Speed;
		MoveAndSlide();
		if (GetSlideCollisionCount() > 0)
		{
			Vector2 normal = GetSlideCollision(0).GetNormal();
			_direction = _direction.Bounce(normal).Normalized();
			_wanderTimer.Start(1.1);
		}

		foreach (Node2D body in _contact.GetOverlappingBodies())
		{
			if (body is PXArenaPlayer player && !player.Locked &&
				player.TakeDamage(ContactDamage, GlobalPosition))
				EmitSignal(SignalName.ContactHit, ContactDamage);
		}
	}

	public void SetHackAvailable(bool available)
	{
		if (_canHack == available) return;
		_canHack = available;
		_prompt.Visible = available;
		QueueRedraw();
	}

	public override void _Draw()
	{
		if (_canHack)
			DrawArc(Vector2.Zero, HackRange, 0f, Mathf.Tau, 80, new Color(0.22f, 0.57f, 0.65f, 0.45f), 2f, true);
	}

	public void TakeDamage(int amount)
	{
		if (!IsAlive || amount <= 0) return;
		Health = Mathf.Max(0, Health - amount);
		_flash = 0.2f;
		EmitSignal(SignalName.HealthChanged, Health, MaxHealth);
		if (!IsAlive)
		{
			Locked = true;
			SetHackAvailable(false);
			EmitSignal(SignalName.Died);
		}
	}

	public void ResetBoss(Vector2 spawn)
	{
		GlobalPosition = spawn;
		Health = MaxHealth;
		Locked = false;
		Velocity = Vector2.Zero;
		_flash = 0f;
		SetHackAvailable(false);
		ChooseDirection();
		_wanderTimer.Paused = false;
		_wanderTimer.Start();
		EmitSignal(SignalName.HealthChanged, Health, MaxHealth);
	}

	private void ChooseDirection()
	{
		_direction = Vector2.Right.Rotated(_random.RandfRange(0f, Mathf.Tau));
		_wanderTimer.WaitTime = _random.RandfRange(1.5f, 3f);
	}
}
