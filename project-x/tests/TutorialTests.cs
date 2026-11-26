using Godot;
using System;
using System.Threading.Tasks;
using ProjectX;

// Plays Sissi's tutorial (the project's main scene) with real key events:
// WASD steps, Shift dash, E without a target, camera follow, floor collision and the Start Game target.
public partial class TutorialTests : Node
{
    private int _passed;
    private Node2D _tutorial;
    private Player _player;
    private Label _prompt;

    private void Check(bool value, string label)
    {
        if (!value) throw new Exception(label);
        _passed++; GD.Print("PASS: " + label);
    }
    private async Task Frames(int count = 1)
    { for (int i = 0; i < count; i++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame); }
    private static void Send(Key code, bool pressed)
    {
        Input.ParseInputEvent(new InputEventKey { Keycode = code, PhysicalKeycode = code, Pressed = pressed });
        Input.FlushBufferedEvents();
    }
    // Largest distance covered in a single physics frame during the last Hold (independent of start-up lag).
    private float _maxStep;
    private async Task<Vector2> Hold(Key code, int frames)
    {
        Vector2 start = _player.GlobalPosition, last = start; _maxStep = 0;
        Send(code, true);
        for (int i = 0; i < frames; i++)
        {
            await Frames();
            _maxStep = Mathf.Max(_maxStep, _player.GlobalPosition.DistanceTo(last)); last = _player.GlobalPosition;
        }
        Send(code, false); await Frames(2);
        return _player.GlobalPosition - start;
    }
    private bool OnFloor(Vector2 global)
    {
        var floor = _tutorial.GetNode<TileMapLayer>("MainMap/groundbase");
        return floor.GetCellSourceId(floor.LocalToMap(floor.ToLocal(global))) != -1;
    }

    public override async void _Ready()
    {
        try
        {
            string main = ProjectSettings.GetSetting("application/run/main_scene").AsString();
            var scene = GD.Load<PackedScene>(main);
            Check(scene != null && scene.ResourcePath == "res://scenes/Tutorial.tscn", "Main scene is the tutorial");
            _tutorial = scene.Instantiate<Node2D>(); AddChild(_tutorial);
            await Frames(10);

            _player = _tutorial.GetNodeOrNull<Player>("Player");
            _prompt = _tutorial.GetNode<Label>("UILayer/PanelContainer/MarginContainer/VBoxContainer/PromptLabel");
            Check(_player != null, "Tutorial uses the game's Player scene");
            Check(_tutorial.GetNode<MapBoundary>("MapBoundary").SolidColliderCount > 0 && OnFloor(_player.GlobalPosition), "Player starts on the tutorial floor with map collision");
            Check(GetViewport().GetCamera2D() == _player.GetNode<Camera2D>("Camera2D"), "Tutorial camera follows the player");
            Check(_prompt.Text.StartsWith("Step 1/5"), "Tutorial starts at step 1");

            // One movement only: one physics frame at Player.Speed (340) is ~5.7 px; Sissi's extra
            // movement on top of it (220) would make it ~9.3 px.
            float step = _player.Speed / 60f;
            bool Single() => _maxStep > step * 0.85f && _maxStep < step * 1.15f;
            Vector2 up = await Hold(Key.W, 20);
            Check(up.Y < -step * 5 && Mathf.Abs(up.X) < 2 && Single(), "W moves up once (no double movement)");
            Check(_prompt.Text.StartsWith("Step 2/5"), "W completes step 1");
            Vector2 left = await Hold(Key.A, 20);
            Check(left.X < -step * 5 && Single(), "A moves left");
            Check(_prompt.Text.StartsWith("Step 3/5"), "A completes step 2");
            Vector2 down = await Hold(Key.S, 20);
            Check(down.Y > step * 5 && Single(), "S moves down");
            Check(_prompt.Text.StartsWith("Step 4/5"), "S completes step 3");
            Vector2 right = await Hold(Key.D, 20);
            Check(right.X > step * 5 && Single(), "D moves right");
            Check(_prompt.Text.StartsWith("Step 5/5"), "D completes step 4");

            // Shift + D: dash (0.15 s at 820 px/s) covers clearly more than walking in the same time.
            Send(Key.D, true); await Frames(2);
            Vector2 previous = _player.GlobalPosition; float fastest = 0;
            Send(Key.Shift, true);
            for (int i = 0; i < 9; i++) { await Frames(); fastest = Mathf.Max(fastest, _player.GlobalPosition.X - previous.X); previous = _player.GlobalPosition; }
            Send(Key.Shift, false); Send(Key.D, false); await Frames(2);
            Check(fastest > step * 2f, "Shift + D dashes");
            Check(!_player.DashReady, "Dash goes on cooldown");
            Check(_prompt.Text.StartsWith("Nice!"), "Shift completes the tutorial");
            Check(_player.GetNode<AnimatedSprite2D>("Body").Modulate == Colors.White, "Dash does not blink");

            // E with nothing nearby must not lock or break anything.
            Send(Key.E, true); await Frames(2); Send(Key.E, false); await Frames(2);
            Check(!_player.Locked && !GetNode<QuestInteraction>("/root/QuestInteraction").Busy, "E without a target does nothing");
            Vector2 afterE = await Hold(Key.D, 10);
            Check(afterE.X > 0, "Movement still works after E");

            // Map collision: walking far in every direction never leaves the floor.
            foreach (Key key in new[] { Key.W, Key.A, Key.S, Key.D })
            {
                await Hold(key, 240);
                Check(OnFloor(_player.GlobalPosition), $"Holding {key} keeps the player on the floor");
            }

            var tutorialWorld = (TutorialWorld)_tutorial;
            Check(tutorialWorld.MainGameScenePath == "res://scenes/IntroFamily.tscn" && ResourceLoader.Exists(tutorialWorld.MainGameScenePath), "Start Game opens the intro story first");
            var intro = GD.Load<PackedScene>("res://scenes/IntroFamily.tscn").Instantiate<StoryScene>();
            Check(intro.NextScene == "res://scenes/IntroSpaceshipStorage.tscn", "Intro continues to the spaceship storage, then the World");
            intro.Free();
            Check(_tutorial.GetNode<Button>("UILayer/PanelContainer/MarginContainer/VBoxContainer/StartGameButton").Visible, "Start Game button is visible");

            GD.Print($"TUTORIAL TESTS PASSED: {_passed}");
            GetTree().Quit(0);
        }
        catch (Exception error)
        {
            GD.PushError(error.ToString());
            GetTree().Quit(1);
        }
    }
}
