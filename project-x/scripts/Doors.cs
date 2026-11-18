using Godot;

public partial class Doors : StaticBody2D 
{
	[Export] public int RequiredActivations = 1;
	private int _currentActivations = 0;
	private bool _isOpen = false;

	public void Activate()
	{
		if (_isOpen) return;

		_currentActivations++;
		GD.Print($"Door progress: {_currentActivations}/{RequiredActivations}");

		if (_currentActivations >= RequiredActivations)
		{
			OpenDoor();
		}
	}

	private void OpenDoor()
	{
		_isOpen = true;
		GD.Print("Door Unlocked!");

		Visible = false;
		
		var collision = GetNodeOrNull<CollisionShape2D>("CollisionShape2D");
		if (collision != null)
		{
			collision.SetDeferred(CollisionShape2D.PropertyName.Disabled, true);
		}
	}
}
