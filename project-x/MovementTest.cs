using Godot;

public partial class MovementTest : Node2D
{
	private CharacterBody2D _player = null!;
	private Node2D _movingObject = null!;
	private Vector2 _movingObjectStart;
	private float _elapsed;

	public override void _Ready()
	{
		_player = GetNode<CharacterBody2D>("Player");
		_movingObject = GetNode<Node2D>("MovingObject");
		_movingObjectStart = _movingObject.Position;
	}

	public override void _PhysicsProcess(double delta)
	{
		Vector2 direction = Input.GetVector("ui_left", "ui_right", "ui_up", "ui_down");
		_player.Velocity = direction * 250f;
		_player.MoveAndSlide();

		_elapsed += (float)delta;
		_movingObject.Position = _movingObjectStart
			+ Vector2.Right * Mathf.Sin(_elapsed * 2f) * 120f;
	}
}
