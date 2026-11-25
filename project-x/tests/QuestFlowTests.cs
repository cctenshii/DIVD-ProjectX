using Godot;
using System;
using System.Threading.Tasks;
using ProjectXBossArenaPrototype;

public partial class QuestFlowTests : Node
{
    private int _checks;
    private bool _usedPortal;
    private PXBossArena _world;
    private PXArenaPlayer _player;
    private QuestJournal _journal;
    private QuestInteraction _interaction;
    private void Check(bool condition, string text) { if (!condition) throw new Exception(text); _checks++; GD.Print("PASS: " + text); }
    private async Task Frames(int count = 4)
    { for (int i = 0; i < count; i++) { await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame); await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); } }
    private async Task Press(Key code)
    {
        Input.ParseInputEvent(new InputEventKey { Keycode = code, PhysicalKeycode = code, Pressed = true }); await Frames();
        Input.ParseInputEvent(new InputEventKey { Keycode = code, PhysicalKeycode = code, Pressed = false }); await Frames(2);
    }
    private async Task Approach(QuestInteractable target)
    {
        _player.GlobalPosition = target.InteractionPosition + (target is DoorController ? Vector2.Down.Rotated(target.GlobalRotation) * 18 : Vector2.Zero);
        await Frames(8);
    }
    private async Task CheckRoute(Node2D target, string label)
    {
        // Follow every suggested segment, checking clearance rather than teleporting across walls.
        // This verifies navigation geometry; interaction tests separately use real E input.
        var goal = target is QuestInteractable item ? item.InteractionPosition : target.GlobalPosition;
        _journal.Navigator.InvalidateRoute(); await Frames(3);
        for (int step = 0; step < 100 && _player.GlobalPosition.DistanceTo(goal) > 96; step++)
        {
            var marker = _journal.Navigator.Guide(_player.GlobalPosition, target);
            if (marker == null || !_journal.Navigator.SegmentClear(_player.GlobalPosition, marker.GlobalPosition))
                throw new Exception($"Unsafe or missing route: {label}; player={_player.GlobalPosition}, goal={goal}, waypoint={marker?.GlobalPosition}, points={_journal.Navigator.RoutePointCount}");
            if (_player.GlobalPosition.DistanceTo(marker.GlobalPosition) < 1)
            {
                // Standing on Jade's teleport: wait for the fade-out, jump and fade-in.
                var before = _player.GlobalPosition;
                await Frames(90);
                if (_player.GlobalPosition.DistanceTo(before) < 500) throw new Exception($"Stalled route: {label} at {before}");
                _usedPortal = true; continue;
            }
            _player.GlobalPosition = marker.GlobalPosition; await Frames(2);
        }
        Check(_player.GlobalPosition.DistanceTo(goal) <= 96, "Full safe route: " + label);
    }
    private Button Button(Node root, string prefix)
    {
        if (root is Button button && button.Text.StartsWith(prefix)) return button;
        foreach (var child in root.GetChildren()) { var found = Button(child, prefix); if (found != null) return found; }
        return null;
    }
    private async Task StartFuse()
    {
        _interaction.Hack.PreparePuzzle(7); _interaction.Hack.StartPreparedHack();
        _interaction.Hack.Flow.GetNode("Intro").EmitSignal("start_requested"); await Frames();
    }
    private void SolveFuse(Node puzzle)
    {
        puzzle.Call("_on_inventory_fuse_pressed", 3); puzzle.Call("_on_slot_pressed", 0);
        puzzle.Call("_on_inventory_fuse_pressed", 7); puzzle.Call("_on_slot_pressed", 1);
    }
    private async Task Defeat(Enemy enemy)
    {
        await Approach(enemy);
        for (int i = 1; i <= enemy.HitsToDefeat; i++)
        {
            await Press(Key.E); if (!_interaction.Busy) await Press(Key.Enter);
            Check(_interaction.Busy && _player.Locked, "E starts minipuzzle and locks player");
            await StartFuse(); SolveFuse(_interaction.Hack.Puzzle); await Frames(8);
            Check(enemy.SuccessfulHacks == i && _interaction.State == QuestInteraction.InteractionState.Result, "Native puzzle completion deals one hit");
            await Press(Key.Enter); Check(!_player.Locked, "Continue restores movement");
        }
    }
    public override async void _Ready()
    {
        try
        {
            _world = GD.Load<PackedScene>("res://scenes/World.tscn").Instantiate<PXBossArena>(); AddChild(_world);
            _player = _world.GetNode<PXArenaPlayer>("Player"); _journal = _world.GetNode<QuestJournal>("QuestJournal");
            _interaction = GetNode<QuestInteraction>("/root/QuestInteraction");
            await Frames(55);
            Check(_player.GlobalPosition.DistanceTo(new(3552,3520)) < 5, "Spawn is in the market");
            Check(!_journal.MainAccepted && !_journal.SideAccepted, "Neither quest starts automatically");
            Check(_journal.Navigator.IsReadyForRoutes, "Route grid finishes incremental collision bake");
            Check(_interaction.NavigationTarget != null && _journal.Navigator.HasRoute, "Single arrow has a reachable route to selected NPC");
            var robot = _world.GetNode<QuestNpc>("RobotNpc"); var cat = _world.GetNode<QuestNpc>("CatNpc");
            await Approach(robot); await Press(Key.E);
            Check(_journal.SideAccepted && !_journal.MainAccepted && _journal.SelectedQuest == QuestJournal.Quest.Side, "Robot starts and selects side quest");
            await Press(Key.Tab); Check(_interaction.State == QuestInteraction.InteractionState.Journal && _player.Locked, "Tab opens quest selection and freezes movement");
            Button(_interaction, "TRACK MAIN").EmitSignal(BaseButton.SignalName.Pressed); await Frames();
            Check(_journal.SelectedQuest == QuestJournal.Quest.Main && !_player.Locked, "Main selection changes only tracked quest");
            await Approach(cat); await Press(Key.E);
            Check(_journal.MainAccepted && _journal.SideAccepted, "Cat starts main quest while retaining side progress");
            _journal.SelectQuest(1); await Frames(10);
            var guide = _interaction.NavigationTarget;
            Check(guide != null && _journal.Navigator.RoutePointCount > 20, "Distant side quest uses intermediate route points");
            Check(_journal.Navigator.SegmentClear(_player.GlobalPosition, guide.GlobalPosition), "First guidance segment cannot cross walls");
            await CheckRoute(_world.GetNode<DoorController>("DoorControllers"), "market through the quest teleport to the first console");
            Check(_usedPortal, "Arrow guides to Jade's teleport when the quest area is separate");
            var first = _world.GetNode<Doors>("Doors"); var second = _world.GetNode<Doors>("Doors2");
            var terminal = _world.GetNode<ComputerTerminal>("ComputerTerminal");
            await Approach(terminal); await Press(Key.E); Check(!terminal.ChipCollected, "Locked doors prevent early chip pickup");
            await Approach(_world.GetNode<DoorController>("DoorControllers")); await Press(Key.E);
            Check(first.IsOpen && first.CurrentActivations == 1, "First console opens first door exactly once");
            await CheckRoute(_world.GetNode<Enemy>("Enemy"), "through opened first door to enemy");
            var enemy = _world.GetNode<Enemy>("Enemy"); await Approach(enemy);
            await Press(Key.E); if (!_interaction.Busy) await Press(Key.Enter);
            await StartFuse();
            var puzzle = _interaction.Hack.Puzzle; puzzle.Call("_on_inventory_fuse_pressed",3); puzzle.Call("_on_slot_pressed",0);
            Button(puzzle,"Restart").EmitSignal(BaseButton.SignalName.Pressed); await Frames();
            Check(puzzle.Get("slot_values").AsGodotArray()[0].VariantType == Variant.Type.Nil, "Original restart button retains native reset logic");
            Button(_interaction.Hack,"CANCEL").EmitSignal(BaseButton.SignalName.Pressed); await Frames();
            Check(!_interaction.Hack.Active && enemy.SuccessfulHacks == 0 && _interaction.State == QuestInteraction.InteractionState.Result, "Visible Cancel ends attempt without awarding progress");
            await Press(Key.Enter);
            // Jade's breach warning appears first; Enter/E confirms it and opens the hack.
            await Press(Key.E); if (!_interaction.Busy) await Press(Key.Enter);
            Check(_interaction.State == QuestInteraction.InteractionState.Hacking, "Breach warning leads into the enemy hack");
            await StartFuse(); await Press(Key.Escape);
            Check(enemy.SuccessfulHacks == 0 && !_interaction.Hack.Active, "Escape also cancels an active minipuzzle");
            await Press(Key.Enter);
            await Defeat(enemy); await Defeat(_world.GetNode<Enemy>("Enemy2"));
            await Approach(_world.GetNode<DoorController>("DoorControllers2")); await Press(Key.E);
            Check(enemy.IsDefeated && _world.GetNode<Enemy>("Enemy2").IsDefeated, "Both guards can be disabled");
            Check(!second.IsOpen && second.CurrentActivations == 1, "Final door needs both consoles (Jade's design)");
            await Approach(_world.GetNode<DoorController>("DoorControllers3")); await Press(Key.E);
            Check(second.IsOpen && second.CurrentActivations == 2, "Two consoles unlock final door");
            await CheckRoute(terminal, "through opened final door to chip");
            await Approach(terminal); await Press(Key.E);
            Check(!terminal.ChipCollected && !terminal.IsUnlocked, "Computer stays locked until all rotation controllers are aligned");
            await Press(Key.Enter); Check(!_player.Locked, "Locked-computer warning closes and frees movement");
            foreach (var name in new[] { "RotationController", "RotationController2", "RotationController3", "RotationController4" })
            {
                var controller = _world.GetNode<RotationController>(name);
                _player.GlobalPosition = controller.GlobalPosition; await Frames(6);
                for (int turn = 0; turn < 4 && !controller.IsCorrect; turn++) await Press(Key.E);
                Check(controller.IsCorrect, $"{name} can be rotated into place with E");
            }
            Check(terminal.IsUnlocked, "Aligning all four controllers unlocks the computer");
            await Approach(terminal); await Press(Key.E);
            Check(_journal.HasChip && !_journal.SideCompleted, "Picking up chip does not complete the side quest");
            Check(_journal.Objective() == robot, "Side quest now guides back to the robot");
            await CheckRoute(robot, "chip return to market robot");
            await Approach(robot); await Press(Key.E);
            Check(_journal.SideCompleted && !_journal.HasChip, "Returning chip completes side quest without a reward");
            await Press(Key.E); Check(_journal.SideCompleted && !_journal.HasChip, "Repeated turn-in cannot duplicate progress");
            _journal.SelectQuest(0); await Frames(10);
            Check(_journal.MainAccepted && !_journal.MainCompleted && _journal.Navigator.HasRoute, "Main quest and its route remain available after side completion");
            _world.SetPhysicsProcess(false); // Verify the complete route without triggering combat halfway.
            await CheckRoute(_world.GetNode<Node2D>("Boss"), "market to upper-right guardian");
            _world.QueueFree(); await Frames();
            Check(!_interaction.Busy && !_interaction.HasQuestTargets(), "Scene teardown cleans cached targets and modal state");
            GD.Print($"QUEST TESTS PASSED: {_checks}"); GetTree().Quit();
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
    }
}
