using Godot;
using System.Collections.Generic;

public partial class Doors : StaticBody2D
{
	[Export] public int RequiredActivations = 1;
	private readonly HashSet<ulong> _sources = new();
	public int CurrentActivations => _sources.Count;
	public bool IsOpen { get; private set; }

	// One console or defeated enemy contributes once, even if a callback is repeated.
	public bool Activate(Node source)
	{
		if (IsOpen || !IsInstanceValid(source) || !_sources.Add(source.GetInstanceId())) return false;
		if (CurrentActivations >= Mathf.Max(1, RequiredActivations)) OpenDoor();
		return true;
	}

	private void OpenDoor()
	{
		IsOpen = true;
		GetNodeOrNull<QuestInteraction>("/root/QuestInteraction")?.Journal?.Navigator.InvalidateRoute();
		Hide();
		GetNodeOrNull<CollisionShape2D>("CollisionShape2D")?.SetDeferred(CollisionShape2D.PropertyName.Disabled, true);
	}
}
