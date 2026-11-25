using Godot;

// Shared interaction contract. The manager chooses ONE nearby target for each E press.
public abstract partial class QuestInteractable : Area2D
{
	[Export] public Doors[] RequiredOpenDoors = System.Array.Empty<Doors>();
	private Node2D _interactionPoint;
	public abstract bool Completed { get; }
	public abstract string Prompt { get; }
	public bool Available
	{
		get
		{
			if (Completed || IsQueuedForDeletion()) return false;
			foreach (var door in RequiredOpenDoors)
				if (IsInstanceValid(door) && !door.IsOpen) return false;
			return true;
		}
	}

	public override void _Ready()
	{
		_interactionPoint = GetNodeOrNull<Node2D>("CollisionShape2D") ?? this;
		AddToGroup("QuestInteractable");
		CollisionLayer = 0;
		CollisionMask = 2;
		GetNode<QuestInteraction>("/root/QuestInteraction").Register(this);
	}

	public override void _ExitTree()
	{
		GetNodeOrNull<QuestInteraction>("/root/QuestInteraction")?.Unregister(this);
	}

	public bool CanInteract(Node2D player)
	{
		if (!Available || !IsInstanceValid(player) || !GetOverlappingBodies().Contains(player)) return false;
		var query = PhysicsRayQueryParameters2D.Create(player.GlobalPosition, _interactionPoint.GlobalPosition, 1);
		if (player is CollisionObject2D body) query.Exclude = new Godot.Collections.Array<Rid> { body.GetRid() };
		return GetWorld2D().DirectSpaceState.IntersectRay(query).Count == 0;
	}

	public abstract void Interact(QuestInteraction interaction);
}
