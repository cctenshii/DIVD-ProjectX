using Godot;

public partial class Enemy : QuestInteractable
{
	// Original three-hit balance is preserved: a hit now means a solved puzzle.
	[Export(PropertyHint.Range, "1,10,1")] public int HitsToDefeat = 3;
	[Export] public Doors[] UnlockDoorsOnDefeat = System.Array.Empty<Doors>();
	public int SuccessfulHacks { get; private set; }
	public override bool Completed => IsDefeated;
	public bool IsDefeated { get; private set; }
	public override string Prompt => $"[ E ] HACK ENEMY  /  {SuccessfulHacks}/{Mathf.Max(1, HitsToDefeat)}";

	public override void Interact(QuestInteraction interaction)
	{
		if (CanInteract(interaction.Player)) interaction.StartHack(this);
	}

	// Called only after the active puzzle resolves successfully, never on raw E input.
	public void CompleteHack()
	{
		if (IsDefeated) return;
		SuccessfulHacks++;
		if (SuccessfulHacks < Mathf.Max(1, HitsToDefeat)) return;
		IsDefeated = true;
		foreach (var door in UnlockDoorsOnDefeat)
			if (IsInstanceValid(door)) door.Activate(this);
		Hide();
		SetDeferred(Area2D.PropertyName.Monitoring, false);
		// Keep the small node alive until scene exit so result UI and progress remain valid.
	}
}
