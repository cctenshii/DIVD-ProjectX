using Godot;
using System.Collections.Generic;
using ProjectXBossArenaPrototype;

// E -> nearest eligible target -> puzzle -> result -> Continue -> exploration.
// C# owns quest state; the existing GDScript player exposes only its movement lock.
public partial class QuestInteraction : CanvasLayer
{
    public enum InteractionState { Exploring, Hacking, Result, Journal }
    public InteractionState State { get; private set; }
    public bool Busy => State != InteractionState.Exploring;
    public QuestJournal Journal { get; private set; }
    private Control _journalPanel;
    private Label _mainStatus, _sideStatus;
    private bool _toggleJournal;
    public Node2D Player { get; private set; }
    public PXMiniPuzzleHost Hack { get; private set; }
    public Node2D NavigationTarget { get; private set; }
    public QuestInteractable NearbyTarget { get; private set; }
    private readonly List<QuestInteractable> _targets = new();
    private readonly List<Doors> _doors = new();
    private Enemy _enemy;
    private Control _root, _overlay, _result;
    private Label _prompt, _objective, _message, _resultTitle, _resultBody;
    private bool _interactRequested, _continueRequested;
    private bool? _hackResult;
    private double _refreshRemaining;

    public override void _Ready()
    {
        Layer = 30;
        _root = new Control { Name = "QuestUI", Size = new(1920, 1080), MouseFilter = Control.MouseFilterEnum.Ignore, Theme = PXRetroUI.MakeTheme() };
        AddChild(_root);
        var status = PXRetroUI.Panel(_root, new(32, 920, 1856, 128));
        status.MouseFilter = Control.MouseFilterEnum.Ignore;
        _message = PXRetroUI.Label(status, "WASD / ARROWS = MOVE   /   E = INTERACT", new(24, 12, 1808, 40), 26);
        _prompt = PXRetroUI.Label(status, "", new(24, 66, 1808, 40), 28, new Color("55ead4"));
        var objectivePanel = PXRetroUI.Panel(_root, new(32, 24, 1200, 80));
        objectivePanel.MouseFilter = Control.MouseFilterEnum.Ignore;
        _objective = PXRetroUI.Label(objectivePanel, "", new(24, 12, 1152, 52), 26);
        _overlay = new Control { Name = "PuzzleOverlay", Size = _root.Size, MouseFilter = Control.MouseFilterEnum.Stop };
        _root.AddChild(_overlay);
        _overlay.AddChild(new ColorRect { Size = _root.Size, Color = new Color(.02f, .04f, .07f, .88f), MouseFilter = Control.MouseFilterEnum.Ignore });
        Hack = new PXMiniPuzzleHost { Position = new(160, 100) }; _overlay.AddChild(Hack);
        Hack.Finished += success => _hackResult = success;
        _overlay.Hide();
        _result = PXRetroUI.Panel(_root, new(420, 300, 1080, 480));
        _resultTitle = PXRetroUI.Label(_result, "", new(48, 40, 984, 70), 48);
        _resultBody = PXRetroUI.Label(_result, "", new(48, 140, 984, 160), 30, new Color("55ead4"));
        _resultBody.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        PXRetroUI.Button(_result, "CONTINUE  /  ENTER", new(48, 340, 984, 84), () => _continueRequested = true);
        _result.Hide();
        PXRetroUI.Button(_root, "QUESTS / TAB", new(1568, 24, 320, 64), () => _toggleJournal = true);
        _journalPanel = PXRetroUI.Panel(_root, new(320, 180, 1280, 680));
        PXRetroUI.Label(_journalPanel, "QUEST JOURNAL", new(48, 28, 1184, 70), 48);
        PXRetroUI.Label(_journalPanel, "Track one quest. The arrow follows its route; both quests keep their progress.", new(48, 106, 1184, 48), 24);
        _mainStatus = PXRetroUI.Label(_journalPanel, "", new(48, 194, 1140, 70), 30);
        PXRetroUI.Button(_journalPanel, "TRACK MAIN QUEST", new(48, 278, 550, 70), () => SelectQuest(0));
        _sideStatus = PXRetroUI.Label(_journalPanel, "", new(48, 390, 1140, 70), 30);
        PXRetroUI.Button(_journalPanel, "TRACK SIDE QUEST", new(48, 474, 550, 70), () => SelectQuest(1));
        PXRetroUI.Button(_journalPanel, "CLOSE / TAB", new(800, 570, 420, 64), () => _toggleJournal = true);
        _journalPanel.Hide(); _root.Hide();
    }

    public void Register(QuestInteractable target)
    {
        if (!_targets.Contains(target)) _targets.Add(target);
        _refreshRemaining = 0;
    }
    public void Unregister(QuestInteractable target)
    {
        _targets.Remove(target);
        if (target == _enemy) ResetInteraction();
        if (_targets.Count == 0)
        {
            _doors.Clear(); Player = null; Journal = null; _root?.Hide();
            NavigationTarget = null; NearbyTarget = null;
            if (_message != null) Message("WASD / ARROWS = MOVE   /   E = INTERACT");
        }
        _refreshRemaining = 0;
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_targets.Count == 0) return;
        if (!IsInstanceValid(Player) || !Player.IsInsideTree())
        {
            ResetInteraction();
            Player = null;
            foreach (var node in GetTree().GetNodesInGroup("Player"))
                if (node is CharacterBody2D body) { Player = body; break; }
        }
        if (!IsInstanceValid(Player)) return;
        if (!IsInstanceValid(Journal)) Journal = GetTree().GetFirstNodeInGroup("QuestJournal") as QuestJournal;
        // The optional World console must not overlay or interrupt the boss HUD.
        if (Player.GetParent() is PXBossArena arena && arena.State != PXBossArena.ArenaState.Exploring)
        {
            _root.Hide(); _interactRequested = _toggleJournal = _continueRequested = false; return;
        }
        if (_toggleJournal)
        {
            _toggleJournal = false;
            if (State == InteractionState.Journal) ResetInteraction();
            else if (!Busy && Journal != null) { State = InteractionState.Journal; SetPlayerLocked(true); _journalPanel.Show(); }
        }
        if (_continueRequested) { _continueRequested = false; if (State == InteractionState.Result) ResetInteraction(); }
        if (_hackResult.HasValue)
        {
            bool success = _hackResult.Value; _hackResult = null;
            FinishHack(success);
        }
        if (_interactRequested)
        {
            _interactRequested = false;
            if (!Busy) { RefreshTargets(); NearbyTarget?.Interact(this); }
        }
        // A six-object quest needs no per-frame scene-tree search or pathfinding.
        _refreshRemaining -= delta;
        if (_refreshRemaining <= 0) { _refreshRemaining = .1; RefreshTargets(); }
    }

    public override void _UnhandledInput(InputEvent input)
    {
        if (_targets.Count == 0 || input is not InputEventKey key || !key.Pressed || key.Echo) return;
        if (IsInstanceValid(Player) && Player.GetParent() is PXBossArena arena && arena.State != PXBossArena.ArenaState.Exploring) return;
        if (key.Keycode == Key.Tab || (key.Keycode == Key.Escape && State == InteractionState.Journal))
        { _toggleJournal = true; GetViewport().SetInputAsHandled(); }
        else if (State == InteractionState.Result && key.Keycode == Key.Enter)
        { _continueRequested = true; GetViewport().SetInputAsHandled(); }
        else if (input.IsActionPressed("interact"))
        {
            if (!Busy) _interactRequested = true;
            GetViewport().SetInputAsHandled();
        }
    }

    private void RefreshTargets()
    {
        NearbyTarget = null; NavigationTarget = null;
        float nearest = float.MaxValue, navigationDistance = float.MaxValue;
        _doors.Clear();
        bool chipCollected = false;
        foreach (var target in _targets)
        {
            if (!IsInstanceValid(target)) continue;
            if (target is ComputerTerminal terminal) chipCollected |= terminal.ChipCollected;
            var linked = target is DoorController console ? console.TargetDoors : target is Enemy enemy ? enemy.UnlockDoorsOnDefeat : System.Array.Empty<Doors>();
            foreach (var door in linked) if (IsInstanceValid(door) && !_doors.Contains(door)) _doors.Add(door);
            if (!target.Available) continue;
            float distance = Player.GlobalPosition.DistanceSquaredTo(target.GlobalPosition);
            if (distance < navigationDistance) { navigationDistance = distance; NavigationTarget = target; }
            if (distance < nearest && target.CanInteract(Player)) { nearest = distance; NearbyTarget = target; }
        }
        _root.Visible = Journal != null || Player is not PXArenaPlayer || Busy || NearbyTarget != null;
        if (Journal != null)
        {
            NavigationTarget = Busy ? null : Journal.Navigator.Guide(Player.GlobalPosition, Journal.Objective());
            _mainStatus.Text = (Journal.SelectedQuest == QuestJournal.Quest.Main ? "[TRACKED] " : "") + "MAIN / " + Journal.Status(QuestJournal.Quest.Main);
            _sideStatus.Text = (Journal.SelectedQuest == QuestJournal.Quest.Side ? "[TRACKED] " : "") + "SIDE / " + Journal.Status(QuestJournal.Quest.Side);
        }
        if (Busy) NavigationTarget = null;
        _prompt.Text = State == InteractionState.Journal ? "SELECT A QUEST TO TRACK / TAB TO CLOSE"
            : Busy ? "MOVEMENT PAUSED DURING THE HACK" : NearbyTarget?.Prompt ?? "FOLLOW THE ARROW TO THE NEXT OBJECTIVE";
        var pending = _doors.Find(door => !door.IsOpen);
        _objective.Text = chipCollected ? "QUEST COMPLETE  /  NANO.O CHIP ACQUIRED" : pending != null
            ? $"DOOR SIGNALS  {pending.CurrentActivations}/{Mathf.Max(1, pending.RequiredActivations)}  /  ACTIVATE CONSOLES AND DISABLE ENEMIES"
            : "DOORS OPEN  /  COLLECT THE NANO.O CHIP";
        if (Journal != null)
            _objective.Text = (Journal.SelectedQuest == QuestJournal.Quest.Main ? "MAIN / " : "SIDE / ") + Journal.Status(Journal.SelectedQuest).ToUpperInvariant();
    }

    public void StartHack(Enemy enemy)
    {
        if (Busy || !enemy.CanInteract(Player)) return;
        _enemy = enemy; State = InteractionState.Hacking;
        SetPlayerLocked(true);
        Hack.StartRandomHack();
        _overlay.Show(); _result.Hide();
        NavigationTarget = null;
    }
    private void FinishHack(bool success)
    {
        if (State != InteractionState.Hacking) return;
        if (!IsInstanceValid(_enemy)) { ResetInteraction(); return; }
        if (success) _enemy.CompleteHack();
        _overlay.Hide(); State = InteractionState.Result;
        _resultTitle.Text = _enemy.IsDefeated ? "ENEMY DISABLED" : success ? "HACK SUCCESSFUL" : "HACK FAILED";
        _resultBody.Text = _enemy.IsDefeated
            ? "Enemy disabled. Its door signal has been registered.\nContinue to the remaining consoles or the chip terminal."
            : success ? $"Security layers disabled: {_enemy.SuccessfulHacks}/{Mathf.Max(1, _enemy.HitsToDefeat)}.\nContinue, then press E near this enemy to hack again."
            : "No security layer was disabled.\nContinue, then press E near the enemy to retry.";
        _result.Show(); _refreshRemaining = 0;
    }
    private void SetPlayerLocked(bool locked)
    {
        if (!IsInstanceValid(Player)) return;
        if (Player is PXArenaPlayer combatPlayer) combatPlayer.Locked = locked;
        else Player.Set("locked", locked);
        if (Player is CharacterBody2D body) body.Velocity = Vector2.Zero;
    }
    private void ResetInteraction()
    {
        Hack?.CancelSilently(); _overlay?.Hide(); _result?.Hide(); _journalPanel?.Hide();
        SetPlayerLocked(false);
        State = InteractionState.Exploring;
        _enemy = null; _hackResult = null;
        _interactRequested = _continueRequested = _toggleJournal = false;
        _refreshRemaining = 0;
    }
    private void SelectQuest(int quest)
    {
        if (Journal == null || State != InteractionState.Journal) return;
        Journal.SelectQuest(quest); ResetInteraction();
    }
    // Narrow bridge for the teammate's GDScript arrow.
    public Node2D GetNavigationTarget() => NavigationTarget;
    public bool HasQuestTargets() => _targets.Count > 0;
    public void Message(string text) => _message.Text = text;
}
