using Godot;

namespace ProjectXBossArenaPrototype;

// Entering the room alone never starts combat: depth and distance must both match.
public partial class PXBossRoom : Node2D
{
	[Export] public Vector2 RoomSize { get; set; } = new(2976, 1696);
	[Export] public float EntryDepth { get; set; } = 96f;
	[Export] public float ActivationRadius { get; set; } = 1200f;
	public bool Closed { get; private set; }
	public Rect2 WorldBounds => new(GlobalPosition, RoomSize);
	public Vector2 BossSpawn => GetNode<Marker2D>("BossSpawn").GlobalPosition;
	public bool ShouldActivate(Vector2 player, Vector2 boss) =>
		WorldBounds.Grow(-EntryDepth).HasPoint(player) && player.DistanceTo(boss) <= ActivationRadius;
	public void SetClosed(bool closed)
	{
		Closed = closed;
		GetNode<CollisionShape2D>("EntranceGate/CollisionShape2D").SetDeferred(CollisionShape2D.PropertyName.Disabled, !closed);
		GetNode<Node2D>("EntranceGate/Visual").Visible = closed;
	}
}
