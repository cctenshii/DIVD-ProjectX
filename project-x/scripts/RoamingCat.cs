using Godot;
public partial class RoamingCat : CharacterBody2D
{
    [Export] public float Speed = 38;
    [Export] public float RoamRadius = 150;
    private Vector2 _home, _direction;
    private float _turn;
    private QuestInteraction _interaction;
    public override void _Ready() { _home = GlobalPosition; MotionMode = MotionModeEnum.Floating; _interaction = GetNode<QuestInteraction>("/root/QuestInteraction"); }
    public override void _PhysicsProcess(double delta)
    {
        if (_interaction.Busy) { Velocity = Vector2.Zero; return; }
        _turn -= (float)delta;
        if (_turn <= 0 || GetSlideCollisionCount() > 0)
        {
            _direction = GlobalPosition.DistanceTo(_home) > RoamRadius ? GlobalPosition.DirectionTo(_home) : Vector2.Right.Rotated((float)GD.RandRange(0, Mathf.Tau));
            _turn = (float)GD.RandRange(1.2, 3.5);
        }
        Velocity = _direction * Speed; MoveAndSlide();
    }
}
