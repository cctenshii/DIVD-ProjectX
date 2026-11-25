using Godot;

namespace ProjectX;

public partial class Player : CharacterBody2D
{
	#region State and input registration
	[Signal] public delegate void HealthChangedEventHandler(int current, int maximum);
	[Signal] public delegate void DiedEventHandler();

	[Export] public float Speed { get; set; } = 340f;
	[Export] public int MaxHealth { get; set; } = 100;
	[Export] public float InvulnerabilitySeconds { get; set; } = 1f;

	public int Health { get; private set; }
	public bool Locked { get; set; }
	public bool IsAlive => Health > 0;

	public bool DashReady => _dashCooldown <= 0f;
	private float _dashCooldown, _dashTime;
	private Vector2 _dashDirection = Vector2.Right;
	private float _invulnerability;
	// Blink only after damage or a hack; the short dash protection stays invisible.
	private float _blink;
	private Vector2 _knockback;
	private AnimatedSprite2D _body;
	private int _facing;
	private bool _combatGlow;
	private ShaderMaterial _glowMaterial;
	// Minecraft-style "glowing" effect: a white outline that follows the sprite's
	// silhouette. For every transparent pixel, look at the neighbours within
	// `width` texels; if any of them is part of the character, paint it white.
	private const string GlowShader = @"
shader_type canvas_item;
uniform vec4 glow_color : source_color = vec4(1.0, 1.0, 1.0, 1.0);
uniform float width = 2.0;
void fragment() {
	vec4 base = texture(TEXTURE, UV);
	float near = 0.0;
	for (float x = -width; x <= width; x += 1.0)
		for (float y = -width; y <= width; y += 1.0)
			if (x * x + y * y <= width * width + 0.5)
				near = max(near, texture(TEXTURE, UV + vec2(x, y) * TEXTURE_PIXEL_SIZE).a);
	float pulse = 0.8 + 0.2 * sin(TIME * 5.0);
	vec4 outline = vec4(glow_color.rgb, near * (1.0 - base.a) * glow_color.a * pulse);
	// COLOR already holds texture * modulate, so the damage blink keeps working.
	COLOR = base.a > 0.0 ? COLOR : outline;
}";
	/// <summary>White silhouette glow around the player, switched on during the boss fight.</summary>
	public bool CombatGlow
	{
		get => _combatGlow;
		set
		{
			if (_combatGlow == value || _body == null) return;
			_combatGlow = value;
			_glowMaterial ??= new ShaderMaterial { Shader = new Shader { Code = GlowShader } };
			_body.Material = value ? _glowMaterial : null;
		}
	}

	// Input actions (left/right/up/down = WASD + arrows, dash = Shift) are defined in
	// Project Settings > Input Map (project.godot).
	public override void _Ready()
	{
		MotionMode = MotionModeEnum.Floating;
		_body = GetNode<AnimatedSprite2D>("Body");
		GetNode<Label>("Name").AddThemeFontOverride("font", RetroUI.Font);
		GetNode<Label>("Name").AddThemeColorOverride("font_color", RetroUI.Accent);
		ResetPlayer(GlobalPosition);
	}
	#endregion

	#region Movement and presentation

	public override void _PhysicsProcess(double delta)
	{
		// Flow: reject locked input -> update timers -> animate -> dash/walk -> collide.
		// A dialogue or hack cancels a running dash, so it cannot continue afterwards.
		if (Locked || !IsAlive) { Velocity = Vector2.Zero; _dashTime = 0f; _body.Pause(); return; }
		_dashCooldown = Mathf.Max(0f, _dashCooldown - (float)delta);
		_invulnerability = Mathf.Max(0f, _invulnerability - (float)delta);
		_blink = Mathf.Max(0f, _blink - (float)delta);
		_body.Modulate = _blink > 0f && ((int)(_blink * 12f) % 2 == 0)
			? new Color(1f, 1f, 1f, 0.35f) : Colors.White;

		Vector2 direction = Input.GetVector("left", "right", "up", "down");
		UpdateSprite(direction);
		if (direction.LengthSquared() > 0) _dashDirection = direction;
		if (Input.IsActionJustPressed("dash")) TryDash(direction);
		_dashTime = Mathf.Max(0f, _dashTime - (float)delta);
		Velocity = _dashTime > 0 ? _dashDirection * 820f : direction * Speed + _knockback;
		_knockback = _knockback.MoveToward(Vector2.Zero, 850f * (float)delta);
		MoveAndSlide();
	}

	/// <summary>Short burst in the given direction (Shift + movement). Needs a direction and no cooldown.</summary>
	public bool TryDash(Vector2 direction)
	{
		if (Locked || !IsAlive || _dashCooldown > 0f || direction.LengthSquared() == 0f) return false;
		_dashDirection = direction.Normalized();
		_dashTime = 0.15f;
		_dashCooldown = 1.1f;
		_invulnerability = Mathf.Max(_invulnerability, 0.18f);
		return true;
	}

	// Jade's SpriteFrames supply presentation; movement, HP and dash stay in this controller.
	private void UpdateSprite(Vector2 direction)
	{
		bool moving = direction.LengthSquared() > 0;
		if (moving)
		{
			_facing = Mathf.Abs(direction.X) > Mathf.Abs(direction.Y) ? 1 : direction.Y < 0 ? 2 : 0;
			_body.FlipH = _facing == 1 && direction.X > 0;
		}
		string suffix = _facing == 1 ? "left" : _facing == 2 ? "up" : "down";
		_body.Play((moving ? "running_" : "idle_") + suffix);
	}

	#endregion

	#region Damage, protection and reset
	public bool TakeDamage(int amount, Vector2 source, bool ignoreInvulnerability = false)
	{
		if (!IsAlive || amount <= 0 || (!ignoreInvulnerability && _invulnerability > 0f))
			return false;

		Health = Mathf.Max(0, Health - amount);
		_invulnerability = _blink = InvulnerabilitySeconds;
		Vector2 away = GlobalPosition - source;
		_knockback = (away.LengthSquared() > 0.01f ? away.Normalized() : Vector2.Left) * 240f;
		EmitSignal(SignalName.HealthChanged, Health, MaxHealth);
		if (!IsAlive)
		{
			Locked = true;
			EmitSignal(SignalName.Died);
		}
		return true;
	}

	public void ProtectAfterHack(float seconds = 1.5f)
	{
		_invulnerability = Mathf.Max(_invulnerability, seconds);
		_blink = Mathf.Max(_blink, seconds);
		_knockback = Vector2.Zero;
		_dashTime = 0;
	}

	public void ResetPlayer(Vector2 spawn)
	{
		GlobalPosition = spawn;
		Health = MaxHealth;
		Locked = false;
		Velocity = Vector2.Zero;
		_knockback = Vector2.Zero;
		_invulnerability = _blink = 0f;
		_dashCooldown = _dashTime = 0f;
		_facing = 0;
		if (_body != null) { _body.FlipH = false; UpdateSprite(Vector2.Zero); }
		if (_body != null) _body.Modulate = Colors.White;
		EmitSignal(SignalName.HealthChanged, Health, MaxHealth);
	}

	#endregion
}
