using Godot;

namespace ProjectXBossArenaPrototype;

public partial class PXArenaLaser : Node2D
{
    public Vector2 Direction { get; set; } = Vector2.Right;
    public float Speed { get; set; } = 300f;
    public int Damage { get; set; } = 12;
    public bool Frozen { get; set; }
    public PXBossArena Arena { get; set; }
    private float _life = 5f;

    public override void _PhysicsProcess(double delta)
    {
        if (Frozen || IsQueuedForDeletion()) return;
        float dt = (float)delta;
        Vector2 next = GlobalPosition + Direction * Speed * dt;
        // Sweep the full travelled segment: fast lasers cannot tunnel through cover.
        var query = PhysicsRayQueryParameters2D.Create(GlobalPosition, next, 3);
        query.HitFromInside = true;
        var hit = GetWorld2D().DirectSpaceState.IntersectRay(query);
        if (hit.Count > 0)
        {
            if (hit["collider"].AsGodotObject() is PXArenaPlayer player && !player.Locked &&
                player.TakeDamage(Damage, GlobalPosition)) Arena?.NotifyLaserHit(Damage);
            QueueFree();
            return;
        }
        GlobalPosition = next;
        _life -= dt;
        if (_life <= 0 || !new Rect2(25, 100, 660, 662).HasPoint(GlobalPosition)) QueueFree();
    }

    public override void _Draw()
    {
        DrawLine(-Direction * 14, Direction * 4, new Color("f06678"), 5, true);
        DrawLine(-Direction * 14, Direction * 4, new Color("fff0c5"), 2, true);
    }
}
