using Godot;

public partial class Chapter1 : Node2D
{
	private Vector2 _playerPosition = new(400, 300);
	private float _time;

	public override void _Process(double delta)
	{
		// Beweeg het blauwe blok met de pijltjestoetsen.
		Vector2 direction = Input.GetVector(
			"ui_left", "ui_right", "ui_up", "ui_down"
		);

		_playerPosition += direction * 250f * (float)delta;

		// Laat het rode object vanzelf heen en weer bewegen.
		_time += (float)delta;
		QueueRedraw();
	}

	public override void _Draw()
	{
		DrawRect(new Rect2(0, 0, 900, 600), new Color("#18202e"));

		DrawRect(
			new Rect2(_playerPosition, new Vector2(40, 40)),
			new Color("#35bfff")
		);

		float enemyX = 650f + Mathf.Sin(_time * 2f) * 120f;
		DrawCircle(new Vector2(enemyX, 300), 22f, new Color("#ff5252"));
	}
}
