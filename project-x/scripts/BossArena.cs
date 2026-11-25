using Godot;
using System.Collections.Generic;

namespace ProjectX;

public partial class BossArena : Node2D
{
	// State flow: explore -> proximity trigger -> combat -> hack -> phase/minions -> end.
	// Input/signal callbacks queue requests; physics processing applies transitions once.
	public enum ArenaState { Menu, Intro, Exploring, Combat, Briefing, Hacking, Transition, Paused, Ended }
	[Export] public int FailedHackDamage { get; set; } = 20;
	[Export] public float HackCooldownSeconds { get; set; } = 3f;
	[Export] public float BossStunSeconds { get; set; } = 1.5f;
	[Export] public bool StartFullscreen { get; set; } = true;
	public ArenaState State { get; private set; } = ArenaState.Menu;
	public bool RoundOver => State == ArenaState.Ended;
	public bool BattlePaused => State != ArenaState.Combat;
	public bool Intermission { get; private set; }
	public int MinionsRemaining => _minions.Count;
	public float HackCooldown => _hackCooldown;
	private Player _player;
	private Boss _boss;
	private BossHUD _hud;
	private MiniPuzzleHost _hack;
	private ArenaTerrain _terrain;
	private BossRoom _room;
	private Camera2D _camera;
	private Node _questArrow;
	private ArenaState _stateBeforePause;
	public Rect2 CombatBounds => _terrain.Bounds;
	public bool EncounterStarted { get; private set; }
	private Node2D _lasers, _minionRoot;
	private readonly List<Minion> _minions = new();
	private Minion _hackMinion;
	private bool _startRequested, _menuRequested, _hackRequested, _resumeRequested, _introComplete, _beginPrepared, _endVictory, _endRequested;
	private bool? _hackResult;
	private float _hackCooldown, _transitionTime;
	private int _nextStage, _defeatedMinions;
	private bool _endingIntermission;
	private bool _initialized;
	public bool CanUseFullscreen => !Engine.IsEmbeddedInEditor() && !GetWindow().IsEmbedded() && DisplayServer.GetName() != "headless";

	public override void _Ready()
	{
		var window = GetWindow(); window.ContentScaleSize = new(1920, 1080);
		window.ContentScaleMode = Window.ContentScaleModeEnum.CanvasItems; window.ContentScaleAspect = Window.ContentScaleAspectEnum.Keep;
		if (StartFullscreen && CanUseFullscreen) window.Mode = Window.ModeEnum.Fullscreen;
		_terrain = GetNodeOrNull<ArenaTerrain>("Terrain"); _player = GetNodeOrNull<Player>("Player"); _boss = GetNodeOrNull<Boss>("Boss");
		_room = GetNodeOrNull<BossRoom>("BossRoom"); _camera = GetNodeOrNull<Camera2D>("WorldCamera");
		_questArrow = _player?.GetNodeOrNull("QuestArrow");
		_hud = GetNodeOrNull<BossHUD>("HUD");
		if (_terrain == null || _player == null || _boss == null || _hud == null || !_hud.Initialized)
		{ StopWithStartupMessage(); return; }
		_hack = _hud.Hack;
		_lasers = new Node2D { Name = "Lasers" }; AddChild(_lasers);
		_minionRoot = new Node2D { Name = "Minions" }; AddChild(_minionRoot);
		_boss.SetTarget(_player);
		_hud.StartPressed += () => _startRequested = true;
		_hud.RetryPressed += () => _startRequested = true;
		_hud.MenuPressed += () => _menuRequested = true;
		_hud.ResumePressed += () => _resumeRequested = true;
		_hack.Finished += success => _hackResult = success;
		_player.Died += () => { _endVictory = false; _endRequested = true; SetLocked(true); };
		_boss.Died += () => { _endVictory = true; _endRequested = true; SetLocked(true); };
		_boss.StageChanged += stage => _nextStage = stage;
		_boss.LaserRequested += SpawnLaser;
		_boss.ContactHit += damage => _hud.Message($"CONTACT HIT  -{damage} HP");
		ResetWorld(); State = ArenaState.Menu; SetLocked(true); _hud.ShowMenu();
		_initialized = true;
		if (_room != null) StartRound();
	}
	private void StopWithStartupMessage()
	{
		const string message = "Project X could not initialize the arena.\nPlace assets, scenes and scripts directly under res://.\nRebuild C# and open scenes/BossArena.tscn with F6.";
		GD.PushError(message);
		ProcessMode = ProcessModeEnum.Disabled;
		var layer = new CanvasLayer { Layer = 1000 };
		var panel = new ColorRect { Size = new(1920, 1080), Color = new Color("142434") };
		var label = new Label { Text = message, Position = new(100, 250), Size = new(1720, 500), AutowrapMode = TextServer.AutowrapMode.WordSmart };
		label.AddThemeFontSizeOverride("font_size", 36);
		panel.AddChild(label); layer.AddChild(panel); AddChild(layer);
	}
	public override void _PhysicsProcess(double delta)
	{
		if (!_initialized) return;
		float dt = (float)delta;
		if (_menuRequested) { _menuRequested = false; ReturnToMenu(); }
		if (_startRequested) { _startRequested = false; StartRound(); }
		if (_resumeRequested) { _resumeRequested = false; if (State == ArenaState.Paused) { State = _stateBeforePause; _hud.Pause(false); SetLocked(false); } }
		if (_introComplete) { _introComplete = false; if (State == ArenaState.Intro) { State = ArenaState.Combat; SetLocked(false); _player.ProtectAfterHack(); } }
		if (_beginPrepared) { _beginPrepared = false; if (State == ArenaState.Briefing) BeginPreparedHack(); }
		if (_hackResult.HasValue) { bool success = _hackResult.Value; _hackResult = null; FinishHack(success); }
		if (_endRequested) { _endRequested = false; EndRound(_endVictory); }
		if (_nextStage > 0 && !RoundOver) { int stage = _nextStage; _nextStage = 0; BeginIntermission(stage); }
		if (State == ArenaState.Transition)
		{
			_transitionTime -= dt;
			if (_transitionTime <= 0) FinishTransition();
		}
		if (State == ArenaState.Combat) _hackCooldown = Mathf.Max(0, _hackCooldown - dt);
		if (_hackRequested) { _hackRequested = false; TryStartHack(); }
		if (State == ArenaState.Exploring) UpdateBossPresence();
		if (State == ArenaState.Exploring && CanStartMainQuest() && _room.ShouldActivate(_player.GlobalPosition, _boss.GlobalPosition)) BeginEncounter();
		if (_camera != null)
		{
			// Boss fight: follow the player, but clamp the camera to the arena so the
			// void outside the room is never visible (feedback: too far zoomed out).
			bool arena = EncounterStarted && _room != null;
			Rect2 view = CombatBounds.Grow(48);
			_camera.LimitLeft = arena ? (int)view.Position.X : -10000000;
			_camera.LimitTop = arena ? (int)view.Position.Y : -10000000;
			_camera.LimitRight = arena ? (int)view.End.X : 10000000;
			_camera.LimitBottom = arena ? (int)view.End.Y : 10000000;
			float fit = Mathf.Max(1f, Mathf.Max(1920f / view.Size.X, 1080f / view.Size.Y));
			// Smooth follow everywhere: the camera's position smoothing chases the player,
			// limits ease in instead of snapping, and the zoom blends between modes.
			_camera.PositionSmoothingEnabled = true;
			_camera.LimitSmoothed = true;
			_camera.GlobalPosition = _player.GlobalPosition;
			var zoom = arena ? new Vector2(fit, fit) : new Vector2(1.25f, 1.25f);
			_camera.Zoom = _camera.Zoom.Lerp(zoom, 1f - Mathf.Exp(-dt * 4f));
		}
		// White combat glow only while the boss fight is running.
		_player.CombatGlow = EncounterStarted && !RoundOver;
		RefreshHUD();
	}
	public override void _Input(InputEvent inputEvent)
	{
		if (!_initialized) return;
		if (inputEvent is not InputEventKey key || !key.Pressed || key.Echo) return;
		if (GetNodeOrNull<QuestInteraction>("/root/QuestInteraction")?.Busy == true && State == ArenaState.Exploring) return;
		Key code = key.PhysicalKeycode != Key.None ? key.PhysicalKeycode : key.Keycode;
		if (code == Key.F11)
		{
			if (!CanUseFullscreen)
			{
				_hud.Message("FULLSCREEN: RUN OUTSIDE THE EMBEDDED GAME WINDOW.");
				GetViewport().SetInputAsHandled(); return;
			}
			var w = GetWindow(); w.Mode = w.Mode == Window.ModeEnum.Fullscreen ? Window.ModeEnum.Windowed : Window.ModeEnum.Fullscreen;
			if (w.Mode == Window.ModeEnum.Windowed) w.Size = new(1280, 720);
			GetViewport().SetInputAsHandled();
		}
		else if (code == Key.E && State == ArenaState.Combat) { _hackRequested = true; GetViewport().SetInputAsHandled(); }
		else if (code == Key.R && RoundOver) { _startRequested = true; GetViewport().SetInputAsHandled(); }
		else if (code == Key.Escape && (State == ArenaState.Combat || State == ArenaState.Exploring))
		{ _stateBeforePause = State; State = ArenaState.Paused; SetLocked(true); _hud.Pause(true); GetViewport().SetInputAsHandled(); }
		else if (code == Key.Escape && State == ArenaState.Paused) { _resumeRequested = true; GetViewport().SetInputAsHandled(); }
	}
	private void ResetWorld()
	{
		// Reset order: cancel UI -> clear combat actors -> restore spawns -> reopen the room.
		_hack.CancelSilently(); _hack.ResetDeck(); _hud.HideHack(); _hud.HideBanner(); _hud.Instructions.CloseSilently();
		ClearLasers(); ClearMinions();
		_hackRequested = _introComplete = _beginPrepared = _endRequested = false; _hackResult = null;
		_nextStage = _defeatedMinions = 0; _hackCooldown = _transitionTime = 0; _endingIntermission = Intermission = false; _hackMinion = null;
		EncounterStarted = _room == null;
		_questArrow?.Set("navigation_enabled", !EncounterStarted);
		_player.ResetPlayer(_room == null ? new(320, 550) : GetNode<Marker2D>("PlayerSpawn").GlobalPosition);
		_boss.ResetBoss(_room == null ? new(1500, 550) : _room.BossSpawn);
		_boss.Dormant = !EncounterStarted;
		UpdateBossPresence();
		if (_room == null) _terrain.Generate(1, _player.GlobalPosition, _boss.GlobalPosition);
		else { _room.SetClosed(false); _terrain.Clear(); }
		if (_camera != null) { _camera.GlobalPosition = _player.GlobalPosition; _camera.ResetSmoothing(); }
	}
	public void StartRound()
	{
		if (_room != null)
		{
			ResetWorld(); State = ArenaState.Exploring; _hud.HideMenu(); SetLocked(false);
			_hud.Message("OMNI-TOOL ACQUIRED  /  FOLLOW THE ROUTE TO THE UPPER-RIGHT SECTOR");
			return;
		}
		ResetWorld(); State = ArenaState.Intro; SetLocked(true); _hud.HideMenu();
		_hud.Message($"{Boss.RequiredHacks} BOSS HACKS  /  {Boss.Phases} PHASES  /  2 MINION WAVES");
		_hud.Instructions.ShowPages(
			new[] { "MOVE & DODGE", "OMNI-TOOL", "THREE PHASES", "HACK SAFELY" },
			new[] {
				"WASD = move.\nSHIFT + movement = dash.\nObstacles block the boss and lasers.\nESC = pause. F11 = fullscreen / windowed.",
				"Approach a target and press E with a clear line of sight to start a random hack.\n\nSuccess deals damage. Failing or cancelling costs 20 HP. The tool then recharges for 3 seconds.",
				$"Defeat the boss with {Boss.RequiredHacks} successful hacks. The terrain changes after every {Boss.HacksPerPhase} hacks.\n\nWave 1: 3 minions. Wave 2: 5 minions. Each minion takes 1 hack. The boss is invulnerable during these waves.",
				"The arena freezes during instructions and hacks. Your HP remains visible at the top.\n\nA successful boss hack stuns the boss for 1.5 seconds. Use that time to escape!" },
			() => _introComplete = true);
	}
	private void BeginEncounter()
	{
		// Both room depth and boss radius have been checked. Lock once, then enable combat.
		_questArrow?.Set("navigation_enabled", false);
		EncounterStarted = true; _room.SetClosed(true); _boss.Dormant = false; _boss.SetPresent(true);
		GetNodeOrNull<QuestJournal>("QuestJournal")?.SelectQuest(0);
		_terrain.Generate(1, _player.GlobalPosition, _boss.GlobalPosition);
		State = ArenaState.Combat; SetLocked(false); _player.ProtectAfterHack(); _boss.Stun(1.5f);
		_hud.Message("SECTOR LOCKED  /  APPROACH THE BOSS AND PRESS E TO HACK");
	}
	public void ReturnToMenu()
	{
		_questArrow?.Set("navigation_enabled", false);
		State = ArenaState.Menu; _hack.CancelSilently(); _hackResult = null; _beginPrepared = _introComplete = false;
		_nextStage = 0; _endRequested = false; ClearLasers(); SetLocked(true); _hud.ShowMenu();
		if (_room != null) { _room.SetClosed(false); _boss.Dormant = true; }
	}
	private Minion NearestMinion()
	{
		Minion nearest = null; float best = float.MaxValue;
		foreach (var minion in _minions)
		{
			float distance = minion.GlobalPosition.DistanceTo(_player.GlobalPosition);
			if (minion.Alive && distance <= minion.HackRange && distance < best && minion.HasSight(_player.GlobalPosition)) { best = distance; nearest = minion; }
		}
		return nearest;
	}
	public bool TryStartHack()
	{
		if (BattlePaused || _hackCooldown > 0) return false;
		_hackMinion = Intermission ? NearestMinion() : null;
		bool canBoss = !Intermission && !_boss.PhaseFrozen && _boss.IsAlive && _player.GlobalPosition.DistanceTo(_boss.GlobalPosition) <= _boss.HackRange && _boss.HasClearSight(_player.GlobalPosition);
		if (_hackMinion == null && !canBoss) { _hud.Message("APPROACH A HACKABLE TARGET WITH A CLEAR LINE OF SIGHT."); return false; }
		State = ArenaState.Briefing; SetLocked(true); _boss.SetHackAvailable(false);
		_hack.PrepareRandomHack(_boss.Stage);
		// The supplied puzzle flow includes its own instructions and Start button.
		BeginPreparedHack();
		return true;
	}
	private void BeginPreparedHack()
	{
		State = ArenaState.Hacking; _hud.ShowHack(_hackMinion == null ? "BOSS" : $"MINION {_hackMinion.Number}"); _hack.StartPreparedHack();
	}
	private void FinishHack(bool success)
	{
		if (State != ArenaState.Hacking) return;
		_hud.HideHack(); State = ArenaState.Combat;
		if (success)
		{
			if (_hackMinion != null)
			{ _minions.Remove(_hackMinion); _hackMinion.Defeat(); _defeatedMinions++; _hud.Message("MINION DISABLED  /  +1 HACK"); }
			else
			{ _boss.CompleteHack(); _boss.Stun(BossStunSeconds); _hud.Message($"HACK SUCCESSFUL  {_boss.SuccessfulHacks}/{Boss.RequiredHacks}  /  BOSS STUNNED FOR 1.5s"); }
			_player.ProtectAfterHack(BossStunSeconds);
		}
		else
		{
			Vector2 source = _hackMinion != null ? _hackMinion.GlobalPosition : _boss.GlobalPosition;
			_player.TakeDamage(FailedHackDamage, source, true); _boss.AfterFailedHack();
			_hud.Message($"HACK FAILED  -{FailedHackDamage} HP");
			_player.ProtectAfterHack(1f);
		}
		foreach (var minion in _minions) minion.GrantGrace();
		_hackMinion = null; _hackCooldown = HackCooldownSeconds;
		if (_endRequested || _nextStage > 0) { SetLocked(true); return; }
		if (Intermission && _minions.Count == 0)
		{
			_endingIntermission = true; State = ArenaState.Transition; _transitionTime = 1.6f; SetLocked(true);
			_hud.Banner($"PHASE {_boss.Stage}", _boss.Stage == 2 ? "TARGETED LASERS ACTIVATED" : "OVERDRIVE  /  TRIPLE RINGS + SPREAD SHOTS");
		}
		else SetLocked(false);
	}
	private void BeginIntermission(int stage)
	{
		Intermission = true; _boss.PhaseFrozen = true; _endingIntermission = false;
		State = ArenaState.Transition; _transitionTime = 1.5f; ClearLasers(); SetLocked(true);
		_hud.Banner("TERRAIN SHIFT", $"BOSS INVULNERABLE  /  {(stage == 2 ? 3 : 5)} MINIONS INCOMING");
	}
	private void FinishTransition()
	{
		if (_endingIntermission)
		{
			Intermission = false; _boss.PhaseFrozen = false; _boss.Stun(BossStunSeconds);
			_hud.Message($"PHASE {_boss.Stage} ACTIVE  /  FIND COVER");
		}
		else
		{
			_terrain.Generate(_boss.Stage, _player.GlobalPosition, _boss.GlobalPosition);
			int count = _boss.Stage == 2 ? 3 : 5; var occupied = new List<Vector2>();
			for (int i = 0; i < count; i++)
			{
				Vector2 spawn = _terrain.FindSpawn(_player.GlobalPosition, _boss.GlobalPosition, occupied); occupied.Add(spawn);
				var minion = Minion.Scene.Instantiate<Minion>(); minion.Position = spawn; minion.Player = _player; minion.Terrain = _terrain; minion.Number = i + 1;
				minion.ContactHit += damage => _hud.Message($"MINION CONTACT HIT  -{damage} HP");
				_minionRoot.AddChild(minion); _minions.Add(minion);
			}
			_hud.Message($"DISABLE {count} MINIONS  /  1 HACK PER MINION");
		}
		_hud.HideBanner(); State = ArenaState.Combat; SetLocked(false); _player.ProtectAfterHack();
	}
	private void SetLocked(bool locked)
	{
		_player.Locked = locked; _boss.Locked = locked || Intermission || !EncounterStarted;
		if (locked) _player.Velocity = _boss.Velocity = Vector2.Zero;
		foreach (var minion in _minions) { minion.Locked = locked; if (locked) minion.Velocity = Vector2.Zero; }
		foreach (Node node in _lasers.GetChildren()) if (node is Laser laser) laser.Frozen = locked;
	}
	private void SpawnLaser(Vector2 origin, Vector2 direction, float speed, int damage)
	{
		if (BattlePaused || Intermission || _boss.PhaseFrozen) return;
		var laser = Laser.Scene.Instantiate<Laser>(); laser.Position = origin; laser.Direction = direction; laser.Speed = speed; laser.Damage = damage; laser.Arena = this; _lasers.AddChild(laser);
	}
	private void ClearLasers()
	{ foreach (Node child in _lasers.GetChildren()) { if (child is Laser l) l.Frozen = true; _lasers.RemoveChild(child); child.QueueFree(); } }
	private void ClearMinions()
	{ foreach (var minion in _minions) { _minionRoot.RemoveChild(minion); minion.QueueFree(); } _minions.Clear(); }
	public void NotifyLaserHit(int damage) => _hud.Message($"LASER  -{damage} HP  /  DASH OR FIND COVER");
    private bool CanStartMainQuest()
    {
        var journal = GetNodeOrNull<QuestJournal>("QuestJournal");
        var interaction = GetNodeOrNull<QuestInteraction>("/root/QuestInteraction");
        // The fight only exists after talking to the cat (main quest accepted).
        // Tracking the side quest with Tab no longer blocks it.
        return interaction?.Busy != true && (journal == null || journal.MainAccepted && !journal.MainCompleted);
    }
	// Boss is invisible and non-solid until the cat gives the main quest, and
	// disappears again after the main quest is completed.
	private void UpdateBossPresence()
	{
		var journal = GetNodeOrNull<QuestJournal>("QuestJournal");
		bool present = EncounterStarted || journal == null || journal.MainAccepted && !journal.MainCompleted;
		if (_boss.Present == present) return;
		_boss.SetPresent(present);
		if (present && State == ArenaState.Exploring) _hud.Message("GUARDIAN DETECTED  /  FOLLOW THE ARROW TO THE UPPER-RIGHT SECTOR");
	}
	private void EndRound(bool victory)
	{
		_questArrow?.Set("navigation_enabled", false);
        var journal = GetNodeOrNull<QuestJournal>("QuestJournal");
        if (victory && journal != null)
        {
            _nextStage = 0; _hack.CancelSilently(); ClearLasers(); ClearMinions(); _terrain.Clear(); _hud.HideHack(); _hud.HideBanner();
            _room.SetClosed(false); _boss.Dormant = true; EncounterStarted = false;
            State = ArenaState.Exploring; SetLocked(false); _questArrow?.Set("navigation_enabled", true); journal.CompleteMain(); UpdateBossPresence(); return;
        }
		State = ArenaState.Ended; _nextStage = 0; _hack.CancelSilently(); ClearLasers(); SetLocked(true);
		_boss.SetHackAvailable(false); _hud.End(victory, _boss.SuccessfulHacks, _defeatedMinions);
		_room?.SetClosed(false);
	}
	private void RefreshHUD()
	{
		_hud.SetExploring(State == ArenaState.Exploring && GetNodeOrNull<QuestJournal>("QuestJournal") != null);
		bool ready = State == ArenaState.Combat && _hackCooldown <= 0;
		var nearby = Intermission && ready ? NearestMinion() : null;
		bool bossReady = ready && !Intermission && !_boss.PhaseFrozen && _boss.IsAlive && _player.GlobalPosition.DistanceTo(_boss.GlobalPosition) <= _boss.HackRange && _boss.HasClearSight(_player.GlobalPosition);
		_boss.SetHackAvailable(bossReady);
		foreach (var minion in _minions) minion.SetHighlight(minion == nearby);
		string context = BattlePaused ? "ARENA PAUSED" : bossReady || nearby != null ? "[ E ] START HACK" : Intermission ? "APPROACH A MINION  /  BOSS INVULNERABLE" : "APPROACH THE BOSS  /  E = HACK";
		if (State == ArenaState.Exploring) context = "WASD / ARROWS = MOVE   SHIFT = DASH   ESC = PAUSE   GOAL: UPPER RIGHT";
		_hud.UpdateStatus(_player, _boss, Intermission, _minions.Count, _hackCooldown, context);
	}
}
