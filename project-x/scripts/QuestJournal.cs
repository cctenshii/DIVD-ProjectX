using Godot;
using ProjectX;

// Quest progress is independent; only SelectedQuest controls the single route arrow.
public partial class QuestJournal : Node
{
    public enum Quest { Main, Side }
    public Quest SelectedQuest { get; private set; } = Quest.Main;
    public bool MainAccepted { get; private set; }
    public bool SideAccepted { get; private set; }
    public bool MainCompleted { get; private set; }
    public bool SideCompleted { get; private set; }
    public bool HasChip => _terminal.ChipCollected && !SideCompleted;
    public QuestNavigator Navigator { get; private set; }
    private QuestNpc _cat, _robot;
    private ComputerTerminal _terminal;
    private Node2D _boss;
    private QuestInteraction _interaction;
    public override void _Ready()
    {
        _cat = GetNode<QuestNpc>("../CatNpc"); _robot = GetNode<QuestNpc>("../RobotNpc");
        _terminal = GetNode<ComputerTerminal>("../ComputerTerminal"); _boss = GetNode<Node2D>("../Boss");
        Navigator = GetNode<QuestNavigator>("../QuestNavigator");
        _interaction = GetNode<QuestInteraction>("/root/QuestInteraction");
        AddToGroup("QuestJournal");
    }
    public void SelectQuest(int quest) { SelectedQuest = (Quest)Mathf.Clamp(quest, 0, 1); Navigator.InvalidateRoute(); }
    public void TalkTo(QuestNpc npc)
    {
        if (npc.StartsMainQuest)
        {
            if (!MainAccepted) { MainAccepted = true; SelectQuest(0); }
            _interaction.Message(MainCompleted ? "CAT: THE GUARDIAN IS DISABLED. THANK YOU." : "CAT: TAKE YOUR OMNI-TOOL TO THE SECURED SECTOR. DISABLE ITS GUARDIAN.");
        }
        else
        {
            if (!SideAccepted) { SideAccepted = true; SelectQuest(1); }
            if (HasChip) { SideCompleted = true; _interaction.Message("ROBOT: NANO.O CHIP RECEIVED. THANK YOU! SIDE QUEST COMPLETE."); }
            else _interaction.Message(SideCompleted ? "ROBOT: YOU ALREADY RETURNED THE CHIP. THANK YOU." : "ROBOT: DISABLE THE SECURITY LOCKS, COLLECT MY NANO.O CHIP AND BRING IT BACK.");
        }
        Navigator.InvalidateRoute();
    }
    public void CompleteMain() { MainCompleted = true; Navigator.InvalidateRoute(); _interaction.Message("GUARDIAN DISABLED / MAIN QUEST COMPLETE. SELECT ANOTHER QUEST WITH TAB."); }
    public string Status(Quest quest) => quest == Quest.Main
        ? MainCompleted ? "Completed" : MainAccepted ? "Disable the guardian" : "Talk to the cat in the market"
        : SideCompleted ? "Completed - chip returned" : !SideAccepted ? "Talk to the robot in the market" : HasChip ? "Return the chip to the robot" : "Open the security doors and collect the chip";
    public Node2D Objective()
    {
        if (SelectedQuest == Quest.Main) return MainCompleted ? null : MainAccepted ? _boss : _cat;
        if (SideCompleted) return null;
        if (!SideAccepted || HasChip) return _robot;
        // Door prerequisites keep objectives on the accessible side of each gate.
        QuestInteractable nearest = null; float distance = float.MaxValue;
        foreach (var node in GetTree().GetNodesInGroup("QuestInteractable"))
        {
            if (node is not QuestInteractable target || target is QuestNpc || !target.Available) continue;
            float d = _interaction.Player.GlobalPosition.DistanceSquaredTo(target.GlobalPosition);
            if (d < distance) { nearest = target; distance = d; }
        }
        return nearest;
    }
}
