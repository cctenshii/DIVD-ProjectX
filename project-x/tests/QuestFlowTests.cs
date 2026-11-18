using Godot;
using System;
using System.Reflection;
using System.Threading.Tasks;
using ProjectXBossArenaPrototype;

// Integration checks use real input dispatch, overlap physics and puzzle controls.
public partial class QuestFlowTests : Node
{
    private int _checks;
    private QuestInteraction _interaction;
    private Node2D _quest;
    private CharacterBody2D _player;
    private Doors _first, _second;
    private void Check(bool condition, string label)
    {
        if (!condition) throw new Exception(label);
        _checks++; GD.Print("PASS: " + label);
    }
    private async Task Frames(int count = 5)
    { for (int i = 0; i < count; i++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame); }
    private async Task Press(Key code)
    {
        Input.ParseInputEvent(new InputEventKey { PhysicalKeycode = code, Keycode = code, Pressed = true });
        await Frames();
        Input.ParseInputEvent(new InputEventKey { PhysicalKeycode = code, Keycode = code, Pressed = false });
        await Frames(2);
    }
    private async Task Approach(Node2D target)
    {
        var point = target.GetNodeOrNull<Node2D>("CollisionShape2D")?.GlobalPosition ?? target.GlobalPosition;
        // Consoles sit on a floor edge; approach their interaction area from inside the room.
        if (target is DoorController) point += Vector2.Down.Rotated(target.GlobalRotation) * 18;
        _player.GlobalPosition = point; await Frames(8);
    }
    private static T Field<T>(object obj, string name) => (T)obj.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(obj);
    private static void SetField(object obj, string name, object value) => obj.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(obj, value);
    private static void Call(object obj, string method, params object[] args) => obj.GetType().GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(obj, args);
    private static void Click(PXHackPanel panel, Vector2 point) => panel._GuiInput(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = true, Position = point });
    private void Solve(PXHackPanel hack)
    {
        // Exercise each original puzzle's success condition, not its Finished signal.
        switch (hack.CurrentKind)
        {
            case PXHackPanel.HackKind.Timing:
                SetField(hack, "_needle", Field<float>(hack, "_targetCenter"));
                hack._Input(new InputEventKey { Keycode = Key.Space, Pressed = true });
                break;
            case PXHackPanel.HackKind.Targets:
                for (int i = 0; i < 3; i++) Click(hack, Field<Vector2>(hack, "_clickTarget"));
                break;
            case PXHackPanel.HackKind.Memory:
                hack._Process(1.6);
                foreach (int number in Field<int[]>(hack, "_sequence"))
                    hack._Input(new InputEventKey { Keycode = Key.Key1 + (number - 1), Pressed = true });
                break;
            case PXHackPanel.HackKind.Wires:
                var wires = Field<int[]>(hack, "_wires");
                for (int i = 0; i < 3; i++) { Click(hack, new(62, 138 + i * 38)); Click(hack, new(298, 138 + Array.IndexOf(wires, i) * 38)); }
                break;
            case PXHackPanel.HackKind.Order:
                var order = Field<int[]>(hack, "_order");
                for (int number = 1; number <= 6; number++) { int i = Array.IndexOf(order, number); Click(hack, new(86 + i % 3 * 94, 149 + i / 3 * 55)); }
                break;
            case PXHackPanel.HackKind.Circuit:
                var circuit = Field<int[]>(hack, "_circuit"); int[] solution = { 1, 2, 0, 3 };
                for (int i = 0; i < 4; i++) while (circuit[i] != solution[i]) Click(hack, new(150 + i % 2 * 54, 148 + i / 2 * 54));
                break;
            case PXHackPanel.HackKind.Fingerprint:
                var target = Field<int[]>(hack, "_fingerTarget"); var current = Field<int[]>(hack, "_fingerCurrent");
                for (int i = 0; i < 3; i++) while (current[i] != target[i]) Click(hack, new(230, 135 + i * 37));
                hack.GetNode<Button>("Submit").EmitSignal(BaseButton.SignalName.Pressed);
                break;
            case PXHackPanel.HackKind.Pressure:
                for (int i = 0; i < 250 && hack.Active; i++)
                {
                    bool hold = Field<float>(hack, "_pressure") < .54f;
                    hack._Input(new InputEventKey { Keycode = Key.Space, Pressed = hold }); hack._Process(.02);
                }
                break;
        }
    }
    private async Task Defeat(Enemy enemy)
    {
        await Approach(enemy);
        for (int i = 1; i <= enemy.HitsToDefeat; i++)
        {
            await Press(Key.E);
            Check(_interaction.State == QuestInteraction.InteractionState.Hacking, "E starts an enemy puzzle");
            Check(_player.Get("locked").AsBool(), "C# puzzle locks the GDScript player");
            var position = _player.GlobalPosition;
            Input.ActionPress("right"); await Frames(); Input.ActionRelease("right");
            Check(_player.GlobalPosition.IsEqualApprox(position), "Movement cannot leak into a puzzle");
            await Press(Key.E);
            Check(enemy.SuccessfulHacks == i - 1, "Repeated E does not damage the enemy");
            Solve(_interaction.Hack); await Frames();
            Check(_interaction.State == QuestInteraction.InteractionState.Result && enemy.SuccessfulHacks == i, "Solved puzzle deals exactly one hit");
            Check(_player.Get("locked").AsBool(), "Result keeps movement locked until Continue");
            await Press(Key.Enter);
            Check(!_interaction.Busy && !_player.Get("locked").AsBool(), "Continue restores GDScript movement");
        }
        Check(enemy.IsDefeated && !enemy.Visible, "Three solved puzzles disable the enemy");
    }
    public override async void _Ready()
    {
        try
        {
            _interaction = GetNode<QuestInteraction>("/root/QuestInteraction");
            _quest = GD.Load<PackedScene>("res://scenes/quest_map_test.tscn").Instantiate<Node2D>(); AddChild(_quest);
            _player = _quest.GetNode<CharacterBody2D>("Player/Player");
            _first = _quest.GetNode<Doors>("Doors"); _second = _quest.GetNode<Doors>("Doors2");
            var console = _quest.GetNode<DoorController>("DoorControllers");
            var console2 = _quest.GetNode<DoorController>("DoorControllers2");
            var console3 = _quest.GetNode<DoorController>("DoorControllers3");
            var enemy = _quest.GetNode<Enemy>("Enemy"); var enemy2 = _quest.GetNode<Enemy>("Enemy2");
            var terminal = _quest.GetNode<ComputerTerminal>("ComputerTerminal");
            await Frames(12);
            Check(_interaction.Player == _player, "Quest manager discovers the original nested GDScript player");
            Check(!_first.IsOpen && !_second.IsOpen, "Both doors start locked");
            var edge = PhysicsRayQueryParameters2D.Create(new(8448, 3580), new(8448, 3400), 1);
            Check(_player.GetWorld2D().DirectSpaceState.IntersectRay(edge).Count > 0, "Floor boundary blocks walking around doors through empty space");
            var doorRay = PhysicsRayQueryParameters2D.Create(new(8800, 3600), new(8910, 3600), 1);
            Check(_player.GetWorld2D().DirectSpaceState.IntersectRay(doorRay).Count > 0, "First closed door blocks its corridor");
            await Press(Key.E); Check(_first.CurrentActivations == 0, "E out of range changes nothing");
            await Approach(enemy); await Press(Key.E); Check(!_interaction.Busy, "Enemies behind the first locked door are unavailable");
            await Approach(terminal); await Press(Key.E); Check(!terminal.ChipCollected, "Reward cannot be collected before door conditions");
            await Approach(console); await Press(Key.Q); Check(!_first.IsOpen, "Old Q binding no longer activates a console");
            await Press(Key.E); Check(_first.IsOpen && _first.CurrentActivations == 1, "E on first console opens first door");
            Check(_first.GetNode<CollisionShape2D>("CollisionShape2D").Disabled, "Opened door also disables collision");
            Check(_player.GetWorld2D().DirectSpaceState.IntersectRay(doorRay).Count == 0, "First corridor is traversable after unlocking");
            await Press(Key.E); Check(_first.CurrentActivations == 1, "Repeated console input is idempotent");
            await Approach(enemy); await Press(Key.E); await Press(Key.Escape);
            Check(_interaction.State == QuestInteraction.InteractionState.Result && enemy.SuccessfulHacks == 0 && _second.CurrentActivations == 0, "Cancel fails without enemy or door progress");
            await Press(Key.Enter);
            await Press(Key.E); _interaction.Hack._Process(20); _interaction.Hack._Process(20); await Frames();
            Check(_interaction.State == QuestInteraction.InteractionState.Result && enemy.SuccessfulHacks == 0, "Timeout can be retried without progression");
            await Press(Key.Enter);
            // Temporarily overlapping targets prove one input cannot activate both.
            var console3Position = console3.GlobalPosition;
            console3.GlobalPosition = console2.GlobalPosition;
            await Approach(console2); await Press(Key.E);
            Check(_second.CurrentActivations == 1, "One E press selects only one overlapping target");
            console3.GlobalPosition = console3Position; await Frames();
            await Defeat(enemy);
            Check(_second.CurrentActivations == 2 && !_second.IsOpen, "First enemy supplies one of four door signals");
            enemy.CompleteHack(); Check(_second.CurrentActivations == 2, "Repeated defeat callback cannot add a second signal");
            await Defeat(enemy2);
            Check(_second.CurrentActivations == 3 && !_second.IsOpen, "Door remains closed with one console missing");
            await Approach(console3); await Press(Key.E);
            Check(_second.IsOpen && _second.CurrentActivations == 4, "Two enemies plus two consoles open final door");
            Check(_second.GetNode<CollisionShape2D>("CollisionShape2D").Disabled, "Final door collision is removed");
            await Approach(terminal); await Press(Key.E);
            Check(terminal.ChipCollected, "E at reward terminal completes quest");
            await Press(Key.E); Check(terminal.ChipCollected && !_interaction.Busy, "Reward collection is single-use");
            // All eight puzzle types still work through their actual rules.
            var panel = _interaction.Hack; int results = 0; bool result = false;
            panel.Finished += success => { results++; result = success; };
            foreach (PXHackPanel.HackKind kind in Enum.GetValues<PXHackPanel.HackKind>())
            {
                Call(panel, "BeginHack", kind, 1); int before = results; Solve(panel);
                Check(!panel.Active && result && results == before + 1, $"Puzzle {kind} resolves successfully exactly once");
            }
            // Scene teardown during a modal must release the manager and movement lock.
            _quest.QueueFree(); await Frames(12);
            Check(!_interaction.HasQuestTargets() && !_interaction.Busy, "Scene teardown clears cached targets");
            _quest = GD.Load<PackedScene>("res://scenes/quest_map_test.tscn").Instantiate<Node2D>(); AddChild(_quest);
            _player = _quest.GetNode<CharacterBody2D>("Player/Player"); await Frames(12);
            await Approach(_quest.GetNode<DoorController>("DoorControllers")); await Press(Key.E);
            await Approach(_quest.GetNode<Enemy>("Enemy")); await Press(Key.E);
            Check(_interaction.Busy, "Reloaded scene starts a fresh puzzle");
            _quest.QueueFree(); await Frames(12);
            Check(!_interaction.Busy && !_interaction.Hack.Active && !_interaction.HasQuestTargets(), "Leaving mid-puzzle cleans up modal state");
            GD.Print($"QUEST TESTS PASSED: {_checks}"); GetTree().Quit();
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
    }
}
