using Godot;
using System.Collections.Generic;

namespace ProjectXBossArenaPrototype;

public partial class PXBossArena : Node2D
{
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
	private PXArenaPlayer _player;
	private PXArenaBoss _boss;
	private PXBossHUD _hud;
	private PXHackPanel _hack;
	private PXArenaTerrain _terrain;
	private PXBossRoom _room;
	private Camera2D _camera;
	private ArenaState _stateBeforePause;
	public Rect2 CombatBounds => _terrain.Bounds;
	public bool EncounterStarted { get; private set; }
	private Node2D _lasers, _minionRoot;
	private readonly List<PXArenaMinion> _minions = new();
	private readonly HashSet<int> _seen = new();
	private readonly ConfigFile _preferences = new();
	private const string PreferencesPath = "user://projectx_boss_tutorials.cfg";
	private PXArenaMinion _hackMinion;
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
		_terrain = GetNodeOrNull<PXArenaTerrain>("Terrain"); _player = GetNodeOrNull<PXArenaPlayer>("Player"); _boss = GetNodeOrNull<PXArenaBoss>("Boss");
		_room = GetNodeOrNull<PXBossRoom>("BossRoom"); _camera = GetNodeOrNull<Camera2D>("WorldCamera");
		_hud = GetNodeOrNull<PXBossHUD>("HUD");
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
		if (_preferences.Load(PreferencesPath) == Error.Ok)
			for (int i = 0; i < 8; i++) if (_preferences.GetValue("hacks", i.ToString(), false).AsBool()) _seen.Add(i);
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
		if (State == ArenaState.Exploring && _room.ShouldActivate(_player.GlobalPosition, _boss.GlobalPosition)) BeginEncounter();
		if (_camera != null)
		{
			_camera.GlobalPosition = EncounterStarted ? _room.WorldBounds.GetCenter() : _player.GlobalPosition;
			_camera.Zoom = EncounterStarted ? new Vector2(.48f, .48f) : new Vector2(1.25f, 1.25f);
		}
		RefreshHUD();
	}
	public override void _Input(InputEvent inputEvent)
	{
		if (!_initialized) return;
		if (inputEvent is not InputEventKey key || !key.Pressed || key.Echo) return;
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
		_hack.CancelSilently(); _hack.ResetDeck(); _hud.HideHack(); _hud.HideBanner(); _hud.Instructions.CloseSilently();
		ClearLasers(); ClearMinions();
		_hackRequested = _introComplete = _beginPrepared = _endRequested = false; _hackResult = null;
		_nextStage = _defeatedMinions = 0; _hackCooldown = _transitionTime = 0; _endingIntermission = Intermission = false; _hackMinion = null;
		EncounterStarted = _room == null;
		_player.ResetPlayer(_room == null ? new(320, 550) : GetNode<Marker2D>("PlayerSpawn").GlobalPosition);
		_boss.ResetBoss(_room == null ? new(1500, 550) : _room.BossSpawn);
		_boss.Dormant = !EncounterStarted;
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
		_hud.Message("15 BOSS HACKS  /  3 PHASES  /  2 MINION WAVES");
		_hud.Instructions.ShowPages(
			new[] { "MOVE & DODGE", "OMNI-TOOL", "THREE PHASES", "HACK SAFELY" },
			new[] {
				"WASD = move.\nSHIFT + movement = dash.\nObstacles block the boss and lasers.\nESC = pause. F11 = fullscreen / windowed.",
				"Approach a target and press E with a clear line of sight to start a random hack.\n\nSuccess deals damage. Failing or cancelling costs 20 HP. The tool then recharges for 3 seconds.",
				"Defeat the boss with 15 successful hacks. The terrain changes after every 5 hacks.\n\nWave 1: 3 minions. Wave 2: 5 minions. Each minion takes 1 hack. The boss is invulnerable during these waves.",
				"The arena freezes during instructions and hacks. Your HP remains visible at the top.\n\nA successful boss hack stuns the boss for 1.5 seconds. Use that time to escape!" },
			() => _introComplete = true);
	}
	private void BeginEncounter()
	{
		EncounterStarted = true; _room.SetClosed(true); _boss.Dormant = false;
		_terrain.Generate(1, _player.GlobalPosition, _boss.GlobalPosition);
		State = ArenaState.Combat; SetLocked(false); _player.ProtectAfterHack(); _boss.Stun(1.5f);
		_hud.Message("SECTOR LOCKED  /  APPROACH THE BOSS AND PRESS E TO HACK");
	}
	public void ReturnToMenu()
	{
		State = ArenaState.Menu; _hack.CancelSilently(); _hackResult = null; _beginPrepared = _introComplete = false;
		_nextStage = 0; _endRequested = false; ClearLasers(); SetLocked(true); _hud.ShowMenu();
		if (_room != null) { _room.SetClosed(false); _boss.Dormant = true; }
	}
	private PXArenaMinion NearestMinion()
	{
		PXArenaMinion nearest = null; float best = float.MaxValue;
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
		int kind = (int)_hack.CurrentKind;
		if (!_seen.Contains(kind))
			_hud.Instructions.ShowPages(new[] { _hack.InstructionTitle }, new[] { _hack.InstructionBody }, () => {
				_seen.Add(kind); _preferences.SetValue("hacks", kind.ToString(), true); _preferences.Save(PreferencesPath); _beginPrepared = true;
			});
		else BeginPreparedHack();
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
			{ _boss.CompleteHack(); _boss.Stun(BossStunSeconds); _hud.Message($"HACK SUCCESSFUL  {_boss.SuccessfulHacks}/15  /  BOSS STUNNED FOR 1.5s"); }
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
				var minion = new PXArenaMinion { Position = spawn, Player = _player, Terrain = _terrain, Number = i + 1 };
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
		foreach (Node node in _lasers.GetChildren()) if (node is PXArenaLaser laser) laser.Frozen = locked;
	}
	private void SpawnLaser(Vector2 origin, Vector2 direction, float speed, int damage)
	{
		if (BattlePaused || Intermission || _boss.PhaseFrozen) return;
		_lasers.AddChild(new PXArenaLaser { Position = origin, Direction = direction, Speed = speed, Damage = damage, Arena = this });
	}
	private void ClearLasers()
	{ foreach (Node child in _lasers.GetChildren()) { if (child is PXArenaLaser l) l.Frozen = true; _lasers.RemoveChild(child); child.QueueFree(); } }
	private void ClearMinions()
	{ foreach (var minion in _minions) { _minionRoot.RemoveChild(minion); minion.QueueFree(); } _minions.Clear(); }
	public void NotifyLaserHit(int damage) => _hud.Message($"LASER  -{damage} HP  /  DASH OR FIND COVER");
	private void EndRound(bool victory)
	{
		State = ArenaState.Ended; _nextStage = 0; _hack.CancelSilently(); ClearLasers(); SetLocked(true);
		_boss.SetHackAvailable(false); _hud.End(victory, _boss.SuccessfulHacks, _defeatedMinions);
		_room?.SetClosed(false);
	}
	private void RefreshHUD()
	{
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
