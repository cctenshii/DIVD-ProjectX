using Godot;

namespace ProjectXBossArenaPrototype;

public partial class PXStoryScene : Control
{
	[Export] public bool StorageChapter { get; set; }
	[Export(PropertyHint.File, "*.tscn")] public string NextScene { get; set; } = "res://scenes/IntroSpaceshipStorage.tscn";
	private Label _body, _chapter;
	private Button _next;
	private int _page;
	private bool _leaving;
	private string[] Pages => StorageChapter ? new[] {
		"Years later.\n\nThe ship has become your home. While clearing out the storage room, you move an old crate aside. Beneath it lies something you have never seen before.",
		"An Omni-Tool.\n\nThe device lights up at your touch. Systems, locks and security machines: this tool can hack them all.\n\nA signal points to a secured sector in the upper-right corner of the map."
	} : new[] {
		"The space pirates have arrived.\n\nAs the attack draws closer, your family rushes you aboard a ship. There is no time to explain everything. Only time to say goodbye.",
		"The airlock closes.\n\nThe ship escapes before the pirates can intercept it. Through the window, your old life fades into the distance. An unknown future lies ahead."
	};

	public override void _Ready()
	{
		GetWindow().ContentScaleSize = new(1920, 1080);
		GetWindow().ContentScaleMode = Window.ContentScaleModeEnum.CanvasItems;
		GetWindow().ContentScaleAspect = Window.ContentScaleAspectEnum.Keep;
		SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		Theme = PXRetroUI.MakeTheme();
		PXRetroUI.Label(this, "PROJECT X", new(120, 72, 900, 100), 64, PXRetroUI.Accent);
		_chapter = PXRetroUI.Label(this, "", new(120, 200, 900, 70), 40);
		_body = PXRetroUI.Label(this, "", new(120, 310, 900, 450), 32);
		_body.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		_next = PXRetroUI.Button(this, "", new(120, 850, 760, 80), Advance);
		PXRetroUI.Button(this, "SKIP INTRO", new(1440, 960, 380, 60), () => GoTo("res://scenes/World.tscn"));
		PXRetroUI.Label(this, "ENTER / SPACE = CONTINUE", new(120, 950, 900, 40), 22, PXRetroUI.Muted);
		UpdatePage();
	}
	public override void _UnhandledInput(InputEvent input)
	{
		if (input is InputEventKey key && key.Pressed && !key.Echo &&
			(key.Keycode == Key.Enter || key.Keycode == Key.Space))
		{ Advance(); GetViewport().SetInputAsHandled(); }
	}
	private void Advance()
	{
		if (_leaving) return;
		if (++_page >= Pages.Length) GoTo(NextScene); else UpdatePage();
	}
	private void UpdatePage()
	{
		_chapter.Text = StorageChapter ? "02 / THE STORAGE ROOM" : "01 / THE FAREWELL";
		_body.Text = Pages[_page];
		_next.Text = _page < Pages.Length - 1 ? "CONTINUE" : StorageChapter ? "TAKE THE OMNI-TOOL AND EXPLORE" : "YEARS LATER...";
		QueueRedraw();
	}
	private void GoTo(string path)
	{
		if (_leaving) return;
		_leaving = true;
		GetTree().CallDeferred(SceneTree.MethodName.ChangeSceneToFile, path);
	}
	public override void _Draw()
	{
		DrawRect(new(0, 0, 1920, 1080), new Color("0b1420"));
		for (int i = 0; i < 70; i++) DrawCircle(new((i * 137 + 31) % 1920, (i * 251 + 71) % 1080), i % 3 + 1, new Color(.3f, .5f, .6f, .3f));
		DrawRect(new(1120, 230, 680, 620), new Color("142332"));
		if (StorageChapter)
		{
			for (int y = 0; y < 2; y++) for (int x = 0; x < 3; x++)
			{
				var box = new Rect2(1160 + x * 200, 420 + y * 180, 170, 150);
				DrawRect(box, new Color("344b5c")); DrawRect(box.Grow(-10), new Color("6e8a99"), false, 3);
			}
			DrawArc(new(1470, 360), 68, 0, Mathf.Tau, 48, PXRetroUI.Accent, 5);
			DrawRect(new(1440, 320, 60, 80), PXRetroUI.Accent);
			DrawRect(new(1452, 332, 36, 38), new Color("142332"));
		}
		else
		{
			DrawCircle(new(1300, 390), 95, new Color("43416d"));
			DrawColoredPolygon(new[] { new Vector2(1220, 650), new Vector2(1640, 460), new Vector2(1520, 690), new Vector2(1330, 720) }, new Color("9dafbd"));
			DrawColoredPolygon(new[] { new Vector2(1220, 650), new Vector2(1140, 750), new Vector2(1330, 720) }, PXRetroUI.Accent);
			DrawLine(new(1460, 590), new(1550, 550), new Color("142332"), 16);
		}
	}
}
