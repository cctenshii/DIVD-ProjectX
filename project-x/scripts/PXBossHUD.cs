using Godot;
using System;

namespace ProjectXBossArenaPrototype;

public partial class PXBossHUD : CanvasLayer
{
	public Action StartPressed, RetryPressed, MenuPressed, ResumePressed;
	public PXHackPanel Hack { get; private set; }
	public PXInstructionPopup Instructions { get; private set; }
	public ProgressBar PlayerBar { get; private set; }
	public Control HackOverlay { get; private set; }
	public bool Initialized { get; private set; }
	private Control _menu, _ending, _pause, _banner, _top;
	private Label _playerText, _bossText, _phase, _dash, _context, _message, _bannerTitle, _bannerBody, _endTitle, _endBody, _hackTarget, _hackCaption;
	private ProgressBar _bossBar;
	private StyleBoxFlat _healthFill;
	private Control Root(string name)
	{
		var root = new Control { Name = name, Size = new(1920, 1080), MouseFilter = Control.MouseFilterEnum.Ignore, Theme = PXRetroUI.MakeTheme() };
		AddChild(root); return root;
	}
	private Control Backdrop(string name)
	{
		var root = Root(name); root.MouseFilter = Control.MouseFilterEnum.Stop;
		root.AddChild(new ColorRect { Size = root.Size, Color = new Color(.02f, .04f, .07f, .88f), MouseFilter = Control.MouseFilterEnum.Ignore });
		return root;
	}
	public override void _Ready()
	{
		Layer = 10;
		var baseUI = Root("Status");
		_message = PXRetroUI.Label(baseUI, "", new(64, 998, 1350, 40), 26);
		_context = PXRetroUI.Label(baseUI, "", new(64, 1042, 1400, 30), 22, PXRetroUI.Muted);
		_dash = PXRetroUI.Label(baseUI, "", new(1480, 1010, 380, 40), 26, PXRetroUI.Accent);
		HackOverlay = Root("HackOverlay"); HackOverlay.MouseFilter = Control.MouseFilterEnum.Stop;
		var frame = PXRetroUI.Panel(HackOverlay, new(160, 90, 1600, 900), new Color(.05f, .1f, .16f, .91f));
		frame.Name = "HackFrame";
		PXRetroUI.Label(frame, "PROJECT X  /  OMNI-TOOL", new(48, 34, 700, 40), 26, PXRetroUI.Accent);
		PXRetroUI.Label(frame, "REALITY\nBREACH", new(48, 135, 690, 200), 88);
		_hackTarget = PXRetroUI.Label(frame, "", new(52, 385, 680, 60), 38, PXRetroUI.Accent);
		_hackCaption = PXRetroUI.Label(frame, "", new(52, 470, 650, 180), 28, PXRetroUI.Muted);
		_hackCaption.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		PXRetroUI.Label(frame, "ARENA BEVROREN\nHP BLIJFT ZICHTBAAR", new(52, 732, 680, 90), 26, PXRetroUI.Accent);
		Hack = GD.Load<PackedScene>("res://scenes/HackPanel.tscn").Instantiate<PXHackPanel>();
		Hack.Position = new(800, 100); Hack.Scale = new(2, 2); frame.AddChild(Hack); HackOverlay.Hide();
		_banner = Root("StageBanner");
		var bannerPanel = PXRetroUI.Panel(_banner, new(480, 390, 960, 260), new Color(.05f, .1f, .16f, .94f));
		_bannerTitle = PXRetroUI.Label(bannerPanel, "", new(40, 38, 880, 70), 52);
		_bannerTitle.HorizontalAlignment = HorizontalAlignment.Center;
		_bannerBody = PXRetroUI.Label(bannerPanel, "", new(40, 126, 880, 80), 28, PXRetroUI.Accent);
		_bannerBody.HorizontalAlignment = HorizontalAlignment.Center; _banner.Hide();
		_menu = Backdrop("MainMenu");
		PXRetroUI.Label(_menu, "PROJECT X", new(340, 160, 1240, 140), 112);
		PXRetroUI.Label(_menu, "OMNI-TOOL  /  BOSS ARENA", new(348, 320, 1200, 70), 42, PXRetroUI.Accent);
		PXRetroUI.Label(_menu, "LOW-FIDELITY TEST  //  3 FASES  //  8 HACKS", new(348, 410, 1200, 50), 28, PXRetroUI.Muted);
		PXRetroUI.Button(_menu, "START", new(350, 540, 480, 86), () => StartPressed?.Invoke());
		PXRetroUI.Button(_menu, "AFSLUITEN", new(350, 650, 480, 76), () => GetTree().Quit());
		PXRetroUI.Label(_menu, "V3.2  /  ROOT FOLDER LAYOUT", new(350, 870, 1200, 44), 24, PXRetroUI.Muted);
		_ending = Backdrop("EndScreen");
		_endTitle = PXRetroUI.Label(_ending, "", new(350, 250, 1400, 100), 76);
		_endBody = PXRetroUI.Label(_ending, "", new(355, 380, 1200, 120), 36, PXRetroUI.Accent);
		PXRetroUI.Button(_ending, "OPNIEUW", new(355, 570, 480, 84), () => RetryPressed?.Invoke());
		PXRetroUI.Button(_ending, "HOOFDMENU", new(875, 570, 480, 84), () => MenuPressed?.Invoke()); _ending.Hide();
		_pause = Backdrop("PauseScreen");
		PXRetroUI.Label(_pause, "GEPAUZEERD", new(530, 290, 1100, 100), 72);
		PXRetroUI.Button(_pause, "VERDER", new(530, 460, 800, 84), () => ResumePressed?.Invoke());
		PXRetroUI.Button(_pause, "HOOFDMENU", new(530, 570, 800, 84), () => MenuPressed?.Invoke()); _pause.Hide();
		Instructions = GD.Load<PackedScene>("res://scenes/InstructionPopup.tscn").Instantiate<PXInstructionPopup>();
		AddChild(Instructions);
		// Last UI child: health stays above every hack and instruction popup.
		_top = Root("PersistentHealth");
		var topBackground = PXRetroUI.Panel(_top, new(40, 12, 1840, 72)); topBackground.MouseFilter = Control.MouseFilterEnum.Ignore;
		_playerText = PXRetroUI.Label(topBackground, "", new(20, 3, 420, 34), 24);
		PlayerBar = PXRetroUI.Bar(topBackground, new(20, 43, 420, 12), PXRetroUI.Accent);
		_healthFill = (StyleBoxFlat)PlayerBar.GetThemeStylebox("fill");
		_bossText = PXRetroUI.Label(topBackground, "", new(500, 3, 740, 34), 24);
		_bossBar = PXRetroUI.Bar(topBackground, new(500, 43, 740, 12), new("df897e"));
		_phase = PXRetroUI.Label(topBackground, "", new(1300, 16, 500, 40), 28, PXRetroUI.Accent);
		Initialized = true;
	}
	public void UpdateStatus(PXArenaPlayer player, PXArenaBoss boss, bool intermission, int minions, float cooldown, string context)
	{
		if (!Initialized || player == null || boss == null) return;
		PlayerBar.MaxValue = player.MaxHealth; PlayerBar.Value = player.Health;
		_healthFill.BgColor = _healthFill.BorderColor = player.Health <= 30 ? new Color("f27777") : PXRetroUI.Accent;
		_playerText.Text = $"SPELER  {player.Health} / {player.MaxHealth} HP";
		_bossBar.MaxValue = boss.MaxHealth; _bossBar.Value = boss.Health;
		_bossText.Text = $"BOSS  {boss.SuccessfulHacks} / 15 HACKS   |   {boss.Intent}";
		_phase.Text = intermission ? $"MINIONS OVER: {minions}" : $"FASE {boss.Stage} / 3";
		_dash.Text = player.DashReady ? "SHIFT  /  DASH GEREED" : "DASH HERLAADT";
		_context.Text = cooldown > 0 ? $"OMNI-TOOL HERLAADT  {cooldown:0.0}s" : context;
	}
	public void Message(string message) => _message.Text = message;
	public void ShowMenu() { _menu.Show(); _ending.Hide(); _pause.Hide(); _top.Hide(); HideHack(); HideBanner(); Instructions.CloseSilently(); }
	public void HideMenu() { _menu.Hide(); _ending.Hide(); _pause.Hide(); _top.Show(); }
	public void ShowHack(string target)
	{
		_hackTarget.Text = "DOELWIT  /  " + target;
		_hackCaption.Text = $"{Hack.InstructionTitle}\n\nSucces: hackschade.\nMislukt: 20 HP schade.\n\nEen geslaagde bosshack geeft\n1,5 seconde ontsnappingstijd.";
		HackOverlay.Show();
	}
	public void HideHack() => HackOverlay.Hide();
	public void Banner(string title, string body) { _bannerTitle.Text = title; _bannerBody.Text = body; _banner.Show(); }
	public void HideBanner() => _banner.Hide();
	public void Pause(bool paused) => _pause.Visible = paused;
	public void End(bool win, int hacks, int minions)
	{
		_endTitle.Text = win ? "BOSS UITGESCHAKELD" : "SIGNAL LOST";
		_endBody.Text = $"{hacks} / 15 BOSSHACKS\n{minions} MINIONS UITGESCHAKELD";
		HideHack(); HideBanner(); Instructions.CloseSilently(); _ending.Show();
	}
}
