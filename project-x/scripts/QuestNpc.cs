using Godot;
public partial class QuestNpc : QuestInteractable
{
    [Export] public bool StartsMainQuest;
    public override bool Completed => false;
    public override string Prompt => StartsMainQuest ? "[ E ] TALK TO CAT / MAIN QUEST" : "[ E ] TALK TO ROBOT / SIDE QUEST";
    public override void Interact(QuestInteraction interaction)
    {
        if (!CanInteract(interaction.Player)) return;
        interaction.Journal?.TalkTo(this);
    }
}
