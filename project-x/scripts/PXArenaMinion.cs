using Godot;

namespace ProjectXBossArenaPrototype;

public partial class PXArenaMinion : CharacterBody2D
{
    [Signal] public delegate void ContactHitEventHandler(int damage);
    public bool Locked { get; set; }
    public bool Alive { get; private set; } = true;
    public float Speed { get; set; } = 78;
    public float HackRange => 180;
    public PXArenaPlayer Player { get; set; }
    public PXArenaTerrain Terrain { get; set; }
    public int Number { get; set; }
    private Area2D _contact;
    private float _grace = 1.5f, _repath;
    private bool _highlight;
    private Vector2 _direction;
    public override void _Ready()
    {
        MotionMode = MotionModeEnum.Floating; CollisionLayer = 8; CollisionMask = 15;
        AddChild(new CollisionShape2D { Shape = new CircleShape2D { Radius = 23 } });
        _contact = new Area2D { CollisionLayer = 0, CollisionMask = 2, Monitorable = false };
        _contact.AddChild(new CollisionShape2D { Shape = new CircleShape2D { Radius = 32 } }); AddChild(_contact);
    }
    public override void _PhysicsProcess(double delta)
    {
        if (Locked || !Alive || !Player.IsAlive) { Velocity = Vector2.Zero; return; }
        float dt = (float)delta; _grace = Mathf.Max(0, _grace - dt); _repath -= dt;
        if (_repath <= 0) { _direction = Terrain.DirectionTo(GlobalPosition, Player.GlobalPosition); _repath = .16f; }
        Velocity = _direction * Speed; MoveAndSlide();
        if (_grace > 0) return;
        foreach (var body in _contact.GetOverlappingBodies())
            if (body is PXArenaPlayer p && !p.Locked && p.TakeDamage(10, GlobalPosition)) EmitSignal(SignalName.ContactHit, 10);
    }
    public bool HasSight(Vector2 point) => GetWorld2D().DirectSpaceState.IntersectRay(PhysicsRayQueryParameters2D.Create(GlobalPosition, point, 1)).Count == 0;
    public void SetHighlight(bool value) { _highlight = value; QueueRedraw(); }
    public void GrantGrace() => _grace = 1.5f;
    public void Defeat() { Alive = false; Locked = true; Hide(); CollisionLayer = CollisionMask = 0; QueueFree(); }
    public override void _Draw()
    {
        DrawColoredPolygon(new[] { new Vector2(0, -25), new Vector2(24, 20), new Vector2(-24, 20) }, new Color("f4ce79"));
        DrawCircle(new(0, 5), 7, new Color("283e4f"));
        if (_highlight) DrawArc(Vector2.Zero, 35, 0, Mathf.Tau, 40, PXRetroUI.Accent, 3);
        DrawString(PXRetroUI.Font, new(-24, -36), $"M{Number}", fontSize: 22, modulate: PXRetroUI.Ink);
    }
}
