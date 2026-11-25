using Godot;
using System.Collections.Generic;

namespace ProjectX;

// Adapter only: all puzzle rules, controls and timers remain in minipuzzles/.
// Native flow -> Start -> observe native completion -> emit exactly one result.
public partial class MiniPuzzleHost : Control
{
    [Signal] public delegate void FinishedEventHandler(bool success);
    public bool Active { get; private set; }
    public int CurrentKind { get; private set; }
    public float TimeLimit => 60;
    public string InstructionTitle { get; private set; } = "OMNI-TOOL";
    public string InstructionBody { get; private set; } = "";
    public Control Flow { get; private set; }
    public Node Puzzle => IsInstanceValid(Flow) && Flow.GetNode("PuzzleContainer").GetChildCount() > 0 ? Flow.GetNode("PuzzleContainer").GetChild(0) : null;
    private readonly List<int> _bag = new();
    private int _lastKind = -1;
    private double _observeRemaining;
    private Control _content;
    private Label _goal;
    private ulong _laidOutPuzzle;

    public static PackedScene Scene => GD.Load<PackedScene>("res://scenes/MiniPuzzleHost.tscn");
    public override void _Ready()
    {
        Size = new(1600, 880); MouseFilter = MouseFilterEnum.Stop;
        Theme = RetroUI.MakeTheme();
        RetroUI.Panel(this, new(0, 0, 1600, 880));
        RetroUI.Label(this, "OMNI-TOOL / SECURITY CHALLENGE", new(36, 18, 1120, 52), 30);
        RetroUI.Button(this, "CANCEL / ESC", new(1260, 18, 300, 56), () => Resolve(false));
        _goal = RetroUI.Label(this, "", new(36, 80, 1520, 42), 23, new Color("55ead4"));
        _content = new Control { Position = new(40, 140), Size = new(1520, 700), MouseFilter = MouseFilterEnum.Ignore };
        // Keep the teammate's UI theme and original logical coordinate system.
        _content.Theme = new Theme { DefaultFontSize = RetroUI.Size(20) };
        AddChild(_content); Hide();
    }
    public void PrepareRandomHack(int stage = 1)
    {
        if (_bag.Count == 0)
        {
            for (int i = 0; i < 8; i++) _bag.Add(i);
            for (int i = 7; i > 0; i--) { int j = (int)(GD.Randi() % (uint)(i + 1)); (_bag[i], _bag[j]) = (_bag[j], _bag[i]); }
            if (_bag[^1] == _lastKind) (_bag[0], _bag[^1]) = (_bag[^1], _bag[0]);
        }
        int kind = _bag[^1]; _bag.RemoveAt(_bag.Count - 1); PreparePuzzle(kind);
    }
    public void PreparePuzzle(int kind)
    {
        CancelSilently(); CurrentKind = Mathf.Clamp(kind, 0, 7); _lastKind = CurrentKind;
        Flow = GD.Load<PackedScene>("res://minipuzzles/shared/puzzle_flow.tscn").Instantiate<Control>();
        _content.AddChild(Flow);
        var entry = Flow.Get("puzzles").AsGodotArray()[CurrentKind].AsGodotDictionary();
        Flow.Set("selected_puzzle", entry);
        InstructionTitle = entry["title"].AsString();
        string goal = CurrentKind == 0 ? "Complete 3 memory rounds." : CurrentKind == 1 ? "Reach 500 rhythm points with Q W E R. More than 3 misses: press Restart." : "Solve the puzzle.";
        string nativeText = entry["description"].AsString();
        // Puzzles 2.0 moved the rhythm lanes to QWER; the shared description still says WASD.
        if (CurrentKind == 1) nativeText = nativeText.Replace("WASD", "QWER");
        InstructionBody = nativeText + "\n\n" + goal + " You have 60 seconds. Cancel ends this attempt.";
        Flow.GetNode("Intro").Call("setup", InstructionTitle, InstructionBody);
        Flow.GetNode("Intro").Connect("cancel_requested", Callable.From(() => Resolve(false)));
        Flow.GetNode("TimerDisplay").Connect("time_up", Callable.From(() => Resolve(false)));
        _goal.Text = goal + "  /  60 seconds  /  Restart retries the puzzle; Cancel returns to the game.";
        // Layout overrides affect only this instance, never the source scenes.
        var intro = Flow.GetNode<Control>("Intro");
        intro.GetNode<Label>("Title").Position = new(40, 30);
        var description = intro.GetNode<Label>("Description");
        description.Position = new(40, 100); description.Size = new(1120, 180); description.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        intro.GetNode<Button>("HowItWorksButton").Position = new(40, 310);
        intro.GetNode<Button>("StartButton").Position = new(40, 360);
        intro.GetNode<Button>("CancelButton").Position = new(220, 360);
        var timer = Flow.GetNode<Control>("TimerDisplay");
        timer.SetAnchorsPreset(LayoutPreset.TopLeft);
        timer.Position = new(1300, 0);
        timer.ZIndex = 1;
        Hide();
    }
    public void StartRandomHack(int stage = 1) { PrepareRandomHack(stage); StartPreparedHack(); }
    public void StartPreparedHack() { Active = true; _observeRemaining = 0; Show(); }
    public override void _Process(double delta)
    {
        if (!Active) return;
        _observeRemaining -= delta;
        if (_observeRemaining > 0) return;
        _observeRemaining = .05;
        var puzzle = Puzzle;
        if (!IsInstanceValid(puzzle)) return;
        var panel = puzzle.GetNodeOrNull<Control>("PuzzleUI");
        if (panel != null)
        {
            if (_laidOutPuzzle != puzzle.GetInstanceId())
            {
                _laidOutPuzzle = puzzle.GetInstanceId();
                // Repair cramped native layout on this instance only; rules and signals stay native.
                panel.SetAnchorsPreset(LayoutPreset.TopLeft); panel.Position = Vector2.Zero;
                foreach (var node in puzzle.FindChildren("*", "HSlider", true, false))
                {
                    var slider = (HSlider)node;
                    slider.CustomMinimumSize = new(420, 32);
                    slider.SizeFlagsHorizontal = SizeFlags.ExpandFill;
                }
                if (CurrentKind == 2)
                    foreach (var channel in puzzle.GetNode("PuzzleUI/VBoxContainer/EqualizerContainer").GetChildren())
                        ((Control)channel).CustomMinimumSize = new(60, 0);
            }
            // Containers settle after _Ready; fit and center without changing logical input coordinates.
            float scale = Mathf.Min(1.3f, Mathf.Min(1260 / Mathf.Max(1, panel.Size.X), 680 / Mathf.Max(1, panel.Size.Y)));
            ((Control)puzzle).Scale = new(scale, scale);
            ((Control)puzzle).Position = new((1260 - panel.Size.X * scale) / 2, 0);
        }
        bool solved = CurrentKind switch
        {
            0 => puzzle.Get("round_number").AsInt32() >= 3 && puzzle.Get("player_index").AsInt32() == puzzle.Get("sequence").AsGodotArray().Count,
            // Puzzles 2.0 defines its own target_score (default 500).
            1 => puzzle.Get("score").AsInt32() >= Mathf.Max(1, puzzle.Get("target_score").VariantType == Variant.Type.Nil ? 500 : puzzle.Get("target_score").AsInt32()),
            _ => puzzle.Get("puzzle_solved").AsBool()
        };
        if (solved) Resolve(true);
    }
    public override void _Input(InputEvent input)
    {
        if (Active && input is InputEventKey key && key.Pressed && !key.Echo && (key.Keycode == Key.Escape || key.PhysicalKeycode == Key.Escape))
        { Resolve(false); GetViewport().SetInputAsHandled(); }
    }
    public void CancelSilently()
    {
        Active = false; _laidOutPuzzle = 0; Hide();
        if (IsInstanceValid(Flow))
        {
            Flow.GetNode("TimerDisplay").Call("stop_timer");
            _content.RemoveChild(Flow); Flow.QueueFree(); Flow = null;
        }
    }
    public void ResetDeck() { _bag.Clear(); _lastKind = -1; }
    private void Resolve(bool success)
    {
        if (!Active) return;
        CancelSilently(); EmitSignal(SignalName.Finished, success);
    }
}
