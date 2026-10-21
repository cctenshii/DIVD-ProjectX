using Godot;

namespace ProjectXBossArenaPrototype;

public partial class PXArenaPlayer : CharacterBody2D
{
    [Signal] public delegate void HealthChangedEventHandler(int current, int maximum);
    [Signal] public delegate void DiedEventHandler();

    [Export] public float Speed { get; set; } = 235f;
    [Export] public int MaxHealth { get; set; } = 100;
    [Export] public float InvulnerabilitySeconds { get; set; } = 1f;

    public int Health { get; private set; }
    public bool Locked { get; set; }
    public bool IsAlive => Health > 0;

    private float _invulnerability;
    private Vector2 _knockback;
    private Polygon2D _body;

    public override void _EnterTree()
    {
        RegisterAction("px_boss_left", Key.A, Key.Left);
        RegisterAction("px_boss_right", Key.D, Key.Right);
        RegisterAction("px_boss_up", Key.W, Key.Up);
        RegisterAction("px_boss_down", Key.S, Key.Down);
    }

    public override void _Ready()
    {
        MotionMode = MotionModeEnum.Floating;
        _body = GetNode<Polygon2D>("Body");
        ResetPlayer(GlobalPosition);
    }

    public override void _PhysicsProcess(double delta)
    {
        _invulnerability = Mathf.Max(0f, _invulnerability - (float)delta);
        _body.Modulate = _invulnerability > 0f && ((int)(_invulnerability * 12f) % 2 == 0)
            ? new Color(1f, 1f, 1f, 0.35f) : Colors.White;

        if (Locked || !IsAlive)
        {
            Velocity = Vector2.Zero;
            return;
        }

        Vector2 direction = Input.GetVector("px_boss_left", "px_boss_right", "px_boss_up", "px_boss_down");
        Velocity = direction * Speed + _knockback;
        _knockback = _knockback.MoveToward(Vector2.Zero, 850f * (float)delta);
        MoveAndSlide();
    }

    public bool TakeDamage(int amount, Vector2 source, bool ignoreInvulnerability = false)
    {
        if (!IsAlive || amount <= 0 || (!ignoreInvulnerability && _invulnerability > 0f))
            return false;

        Health = Mathf.Max(0, Health - amount);
        _invulnerability = InvulnerabilitySeconds;
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

    public void ProtectAfterHack()
    {
        _invulnerability = Mathf.Max(_invulnerability, 0.8f);
        _knockback = Vector2.Zero;
    }

    public void ResetPlayer(Vector2 spawn)
    {
        GlobalPosition = spawn;
        Health = MaxHealth;
        Locked = false;
        Velocity = Vector2.Zero;
        _knockback = Vector2.Zero;
        _invulnerability = 0f;
        if (_body != null) _body.Modulate = Colors.White;
        EmitSignal(SignalName.HealthChanged, Health, MaxHealth);
    }

    private static void RegisterAction(string name, params Key[] keys)
    {
        if (!InputMap.HasAction(name)) InputMap.AddAction(name);
        foreach (Key key in keys)
        {
            var inputEvent = new InputEventKey { PhysicalKeycode = key };
            if (!InputMap.ActionHasEvent(name, inputEvent)) InputMap.ActionAddEvent(name, inputEvent);
        }
    }
}
