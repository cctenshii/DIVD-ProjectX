using Godot;

public partial class ProjectXLowFidelityPlayer : CharacterBody2D
{
    [Export]
    public float MoveSpeed { get; set; } = 220f;

    private Label _status;

    public override void _Ready()
    {
        MotionMode = MotionModeEnum.Floating;
        ProcessMode = ProcessModeEnum.Always;
        SetPhysicsProcess(true);

        // Alleen tijdelijke, unieke test-acties; project.godot blijft ongewijzigd.
        AddTestAction("px_lf_left", Key.A, Key.Left);
        AddTestAction("px_lf_right", Key.D, Key.Right);
        AddTestAction("px_lf_up", Key.W, Key.Up);
        AddTestAction("px_lf_down", Key.S, Key.Down);

        _status = GetNodeOrNull<Label>("../HUD/InputStatus");
        if (_status != null)
            _status.Text = "C# ACTIEF - klik in het gamevenster en gebruik WASD of de pijltjes.";

        GD.Print("[ProjectX LowFidelity] C# controller actief; WASD en pijltjes geregistreerd.");
    }

    private static void AddTestAction(string name, Key letter, Key arrow)
    {
        if (!InputMap.HasAction(name))
            InputMap.AddAction(name);

        foreach (Key key in new[] { letter, arrow })
        {
            var inputEvent = new InputEventKey { PhysicalKeycode = key };
            if (!InputMap.ActionHasEvent(name, inputEvent))
                InputMap.ActionAddEvent(name, inputEvent);
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        Vector2 direction = Input.GetVector(
            "px_lf_left", "px_lf_right", "px_lf_up", "px_lf_down");

        Velocity = direction * MoveSpeed;
        MoveAndSlide();

        if (_status != null)
        {
            _status.Text = $"C# ACTIEF | Input: ({direction.X:0.00}, {direction.Y:0.00})\n"
                + $"Positie: ({Position.X:0}, {Position.Y:0}) | Botsingen: {GetSlideCollisionCount()}";
        }
    }
}
