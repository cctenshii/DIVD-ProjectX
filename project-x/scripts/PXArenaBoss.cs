using Godot;

namespace ProjectXBossArenaPrototype;

public partial class PXArenaBoss : CharacterBody2D
{
	public enum BrainState { Chasing, Searching, Wandering }
	[Signal] public delegate void HealthChangedEventHandler(int current, int maximum);
	[Signal] public delegate void ContactHitEventHandler(int damage);
	[Signal] public delegate void StageChangedEventHandler(int stage);
	[Signal] public delegate void DiedEventHandler();
	[Signal] public delegate void LaserRequestedEventHandler(Vector2 origin, Vector2 direction, float speed, int damage);

	public const int RequiredHacks = 15;
	public const int DamagePerHack = 40;
	public int MaxHealth => RequiredHacks * DamagePerHack;
	public int Health => (RequiredHacks - SuccessfulHacks) * DamagePerHack;
	public int SuccessfulHacks { get; private set; }
	public int Stage => Mathf.Min(3, SuccessfulHacks / 5 + 1);
	public bool IsAlive => SuccessfulHacks < RequiredHacks;
	public bool Locked { get; set; }
	public bool CanSeePlayer { get; private set; }
	public BrainState State { get; private set; } = BrainState.Wandering;
	public string Intent => !IsAlive ? "VERSLAGEN" : Locked ? "HACK ACTIEF" :
		_windup > 0 ? (_radialAttack ? "RINGAANVAL!" : "LASERS!") :
		State == BrainState.Chasing ? "ACHTERVOLGT" : State == BrainState.Searching ? "ZOEKT..." : "PATROUILLE";
	[Export] public float HackRange { get; set; } = 150f;
	[Export] public float BaseChaseSpeed { get; set; } = 120f;

	private readonly RandomNumberGenerator _random = new();
	private PXArenaPlayer _player;
	private Area2D _contact;
	private Polygon2D _body;
	private Label _prompt;
	private Label _name;
	private Vector2 _direction = Vector2.Left;
	private Vector2 _aim = Vector2.Left;
	private float _searchTimer, _wanderTimer, _flash, _lookAngle, _avoidTime;
	private Vector2 _avoidDirection;
	private float _attackCooldown = 1.5f, _windup, _recovery, _secondWave;
	private float _ringAngle;
	private int _attackNumber;
	private bool _canHack, _radialAttack;

	public override void _Ready()
	{
		MotionMode = MotionModeEnum.Floating;
		_random.Randomize();
		_contact = GetNode<Area2D>("ContactArea");
		_body = GetNode<Polygon2D>("Body");
		_prompt = GetNode<Label>("HackPrompt");
		_name = GetNode<Label>("Name");
		// The old timer node stays compatible with the original scene.
		GetNode<Timer>("WanderTimer").Stop();
		ResetBoss(GlobalPosition);
	}

	public void SetTarget(PXArenaPlayer player) => _player = player;

	public bool HasClearSight(Vector2 target)
	{
		var query = PhysicsRayQueryParameters2D.Create(GlobalPosition, target, 1);
		return GetWorld2D().DirectSpaceState.IntersectRay(query).Count == 0;
	}

	public override void _PhysicsProcess(double delta)
	{
		float dt = (float)delta;
		_flash = Mathf.Max(0, _flash - dt);
		_body.Color = !IsAlive ? new Color("657379") : Stage switch
		{
			1 => new Color("bd625b"), 2 => new Color("c38437"), _ => new Color("9651a6")
		};
		_body.Modulate = _flash > 0 ? new Color(1.8f, 1.8f, 1.8f) : Colors.White;
		_name.Text = Intent;
		QueueRedraw();
		if (Locked || !IsAlive || _player == null || !_player.IsAlive)
		{
			Velocity = Vector2.Zero;
			return;
		}

		CanSeePlayer = HasClearSight(_player.GlobalPosition);
		_avoidTime = Mathf.Max(0, _avoidTime - dt);
		if (_avoidTime > 0)
		{
			State = BrainState.Wandering;
			_direction = _avoidDirection;
			_lookAngle += dt * 2;
		}
		else if (CanSeePlayer)
		{
			State = BrainState.Chasing;
			_direction = GlobalPosition.DirectionTo(_player.GlobalPosition);
			_lookAngle = _direction.Angle();
			_searchTimer = 2.2f;
		}
		else if (State == BrainState.Chasing)
		{
			State = BrainState.Searching;
			_searchTimer = 2.2f;
		}
		else if (State == BrainState.Searching)
		{
			_lookAngle += dt * 2.6f;
			_searchTimer -= dt;
			if (_searchTimer <= 0) { State = BrainState.Wandering; PickWanderDirection(); }
		}
		else
		{
			_wanderTimer -= dt;
			if (_wanderTimer <= 0) PickWanderDirection();
			_lookAngle = _direction.Angle();
		}

		TickAttacks(dt);
		float speed = BaseChaseSpeed + (Stage - 1) * 23f;
		bool standing = State == BrainState.Searching || _windup > 0 || _recovery > 0;
		Velocity = standing ? Vector2.Zero : _direction * (State == BrainState.Chasing ? speed : 65f);
		MoveAndSlide();
		if (!standing && GetSlideCollisionCount() > 0)
		{
			var hit = GetSlideCollision(0);
			if (hit.GetCollider() is StaticBody2D)
			{
				// Do not keep pushing through cover. Turn and search instead.
				State = BrainState.Searching;
				_searchTimer = 1.1f;
				Vector2 tangent = hit.GetNormal().Orthogonal();
				if (tangent.Dot(_direction) < 0) tangent = -tangent;
				_avoidDirection = (tangent + hit.GetNormal() * 0.35f).Normalized();
				_avoidTime = 0.8f;
			}
		}
		foreach (Node2D body in _contact.GetOverlappingBodies())
		{
			int damage = 12 + Stage * 3;
			if (body is PXArenaPlayer player && !player.Locked && player.TakeDamage(damage, GlobalPosition))
				EmitSignal(SignalName.ContactHit, damage);
		}
	}

	private void TickAttacks(float dt)
	{
		_attackCooldown -= dt;
		_recovery = Mathf.Max(0, _recovery - dt);
		if (_secondWave > 0)
		{
			_secondWave -= dt;
			if (_secondWave <= 0) FireRing(_ringAngle + Mathf.Pi / 16f);
		}
		if (_windup > 0)
		{
			_windup -= dt;
			// Targeted lasers cannot follow the player through solid cover.
			if (!_radialAttack && !CanSeePlayer) { _windup = 0; _attackCooldown = 0.8f; return; }
			if (_windup <= 0)
			{
				if (_radialAttack) { FireRing(_ringAngle); _secondWave = 0.42f; }
				else
				{
					int count = Stage == 2 ? 3 : 5;
					for (int i = 0; i < count; i++)
						Fire(_aim.Rotated((i - (count - 1) * 0.5f) * 0.14f), Stage == 2 ? 300f : 340f);
				}
				_recovery = Stage == 2 ? 0.7f : 0.8f;
				_attackCooldown = Stage == 2 ? 2.5f : 1.75f;
			}
		}
		else if (Stage >= 2 && _attackCooldown <= 0 && CanSeePlayer)
		{
			_radialAttack = Stage == 3 && _attackNumber % 2 == 0;
			_attackNumber++;
			_aim = GlobalPosition.DirectionTo(_player.GlobalPosition);
			// Aim a gap towards the player, then offset the second ring.
			_ringAngle = _aim.Angle() + Mathf.Pi / 16f;
			_windup = _radialAttack ? 0.95f : 0.7f;
		}
	}

	private void Fire(Vector2 direction, float speed) =>
		EmitSignal(SignalName.LaserRequested, GlobalPosition + direction * 43f, direction, speed, Stage == 2 ? 12 : 17);

	private void FireRing(float offset)
	{
		for (int i = 0; i < 16; i++) Fire(Vector2.Right.Rotated(offset + i * Mathf.Tau / 16f), 235f);
	}

	public void AfterHack()
	{
		_windup = _secondWave = 0;
		_recovery = 0.45f;
		_attackCooldown = 0.9f;
	}

	public void CompleteHack()
	{
		if (!IsAlive) return;
		int previousStage = Stage;
		SuccessfulHacks++;
		_flash = 0.25f;
		EmitSignal(SignalName.HealthChanged, Health, MaxHealth);
		if (!IsAlive)
		{
			Locked = true;
			SetHackAvailable(false);
			EmitSignal(SignalName.Died);
		}
		else if (Stage != previousStage)
		{
			_attackNumber = 0;
			_windup = _secondWave = 0;
			_attackCooldown = 0.9f;
			EmitSignal(SignalName.StageChanged, Stage);
		}
	}

	public void SetHackAvailable(bool available)
	{
		_canHack = available;
		_prompt.Visible = available;
	}

	public override void _Draw()
	{
		if (!IsAlive) return;
		if (_canHack) DrawArc(Vector2.Zero, HackRange, 0, Mathf.Tau, 70, new Color(0.12f, 0.51f, 0.62f, 0.5f), 2, true);
		DrawLine(Vector2.Zero, Vector2.Right.Rotated(_lookAngle) * 51, new Color("fff0bc"), 3, true);
		if (State == BrainState.Searching && !Locked)
			DrawArc(Vector2.Zero, 61, _lookAngle - 0.55f, _lookAngle + 0.55f, 20, new Color("c79f33"), 4, true);
		if (_windup <= 0 || Locked) return;
		Color warning = _radialAttack ? new Color(0.63f, 0.2f, 0.65f, 0.55f) : new Color(0.8f, 0.42f, 0.05f, 0.65f);
		int rays = _radialAttack ? 16 : Stage == 2 ? 3 : 5;
		for (int i = 0; i < rays; i++)
		{
			Vector2 direction = _radialAttack ? Vector2.Right.Rotated(_ringAngle + i * Mathf.Tau / 16f)
				: _aim.Rotated((i - (rays - 1) * 0.5f) * 0.14f);
			for (int j = 55; j < 225; j += 22) DrawLine(direction * j, direction * (j + 12), warning, 2, true);
		}
		DrawArc(Vector2.Zero, 46, 0, Mathf.Tau, 50, warning, 5, true);
	}

	public void ResetBoss(Vector2 spawn)
	{
		GlobalPosition = spawn;
		SuccessfulHacks = 0;
		Locked = false;
		CanSeePlayer = false;
		State = BrainState.Wandering;
		Velocity = Vector2.Zero;
		_flash = _windup = _recovery = _secondWave = _avoidTime = 0;
		_attackCooldown = 1.5f;
		_attackNumber = 0;
		SetHackAvailable(false);
		PickWanderDirection();
		EmitSignal(SignalName.HealthChanged, Health, MaxHealth);
	}

	private void PickWanderDirection()
	{
		_direction = Vector2.Right.Rotated(_random.RandfRange(0, Mathf.Tau));
		_wanderTimer = _random.RandfRange(0.8f, 1.7f);
	}
}
