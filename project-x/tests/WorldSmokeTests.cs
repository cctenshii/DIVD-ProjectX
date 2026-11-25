using Godot;
using System;
using System.Threading.Tasks;
using ProjectX;

public partial class WorldSmokeTests : Node
{
	private BossArena _world;
	private Player _player;
	private Boss _boss;
	private BossRoom _room;
	private BossHUD _hud;
	private ArenaTerrain _terrain;
	private int _checks;
	private void Check(bool value, string label)
	{
		if (!value) throw new Exception(label);
		_checks++; GD.Print("PASS: " + label);
	}
	private async Task Frames(int count = 3)
	{ for (int i = 0; i < count; i++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame); }
	private void Tick(double delta = .016) => _world._PhysicsProcess(delta);
	private void Key(Key code) => _world._Input(new InputEventKey { PhysicalKeycode = code, Pressed = true });
	private async Task Activate()
	{
		_player.GlobalPosition = new(8000, 2200); Tick(); await Frames();
		Check(_world.EncounterStarted && _room.Closed && !_boss.Dormant, "Room locks and boss activates inside radius");
	}
	private async Task Hack(Node2D target)
	{
		Tick(4);
		// Approach from a clear side; larger cover can block a fixed left-hand offset.
		bool found = false;
		for (int i = 0; i < 8; i++)
		{
			var position = target.GlobalPosition + Vector2.Left.Rotated(i * Mathf.Tau / 8) * 90;
			if (!_terrain.IsClear(position, 18) || !_boss.HasClearSight(position) && target == _boss) continue;
			if (target is Minion minion && !minion.HasSight(position)) continue;
			_player.GlobalPosition = position; found = true; break;
		}
		Check(found, "Target has a safe hack approach around larger cover");
		await Frames();
		Check(_world.TryStartHack(), "Nearby target can be hacked");
		if (_hud.Instructions.Active) _hud.Instructions.Advance();
		Tick();
		Check(_world.State == BossArena.ArenaState.Hacking && _player.Locked && _boss.Locked, "Hack freezes combat");
		_hud.Hack.CancelSilently();
		_hud.Hack.EmitSignal(MiniPuzzleHost.SignalName.Finished, true);
		Tick(); await Frames();
	}
	private void CheckTerrain(ArenaTerrain terrain, Vector2 player, Vector2 boss)
	{
		Check(terrain.Connected && terrain.Footprints.Count >= 8, "Larger cover retains a connected, populated arena");
		foreach (var rect in terrain.Footprints)
		{
			Check(terrain.Bounds.Encloses(rect) && !rect.Grow(18).HasPoint(player) && !rect.Grow(52).HasPoint(boss), "Cover stays in bounds and leaves actors clear");
			float longest = Mathf.Max(rect.Size.X, rect.Size.Y);
			Check(longest >= terrain.ObstacleSizeRange.X * .55f && longest <= terrain.ObstacleSizeRange.Y * 2.2f, "Cover uses varied but bounded sizes");
		}
		// Random layout: not every piece may sit on the same row or column.
		var xs = new System.Collections.Generic.HashSet<int>(); var ys = new System.Collections.Generic.HashSet<int>();
		foreach (var rect in terrain.Footprints) { xs.Add((int)(rect.GetCenter().X / 40)); ys.Add((int)(rect.GetCenter().Y / 40)); }
		Check(xs.Count >= terrain.Footprints.Count / 2 && ys.Count >= 4, "Cover is scattered instead of a straight grid");
		var occupied = new System.Collections.Generic.List<Vector2>();
		for (int i = 0; i < 5; i++)
		{
			var spawn = terrain.FindSpawn(player, boss, occupied); occupied.Add(spawn);
			Check(terrain.IsClear(spawn, 60), "Five safe minion positions remain available");
		}
	}
	public override async void _Ready()
	{
		try
		{
			_world = GD.Load<PackedScene>("res://scenes/World.tscn").Instantiate<BossArena>();
			AddChild(_world);
			_world.SetPhysicsProcess(false);
			_player = _world.GetNode<Player>("Player"); _player.SetPhysicsProcess(false);
			_boss = _world.GetNode<Boss>("Boss"); _boss.SetPhysicsProcess(false);
			_room = _world.GetNode<BossRoom>("BossRoom"); _hud = _world.GetNode<BossHUD>("HUD");
			_terrain = _world.GetNode<ArenaTerrain>("Terrain");
            // Normal travel from market finishes the startup bake before reaching the boss.
            await Frames(55); Tick();
			Check(_player.GlobalPosition == new Vector2(3552, 3520), "Player starts in the market");
            Check(GetNode<Control>("/root/QuestInteraction/QuestUI").Visible, "Quest HUD is available in the market");
			Check(((AtlasTexture)_player.GetNode<AnimatedSprite2D>("Body").SpriteFrames.GetFrameTexture("idle_down", 0)).Atlas.ResourcePath.EndsWith("all-position-V2.png"), "Player uses requested spritesheet");
			// Dash: Shift + direction, defined in the project Input Map; no damage blink; a lock cancels it.
			Check(InputMap.HasAction("dash") && InputMap.HasAction("left") && InputMap.HasAction("up"), "Dash and movement live in the project Input Map");
			Vector2 market = _player.GlobalPosition; const double step = 1.0 / 60;
			Check(!_player.TryDash(Vector2.Zero), "Shift without a direction does not dash");
			Input.ActionPress("right");
			Check(_player.TryDash(Vector2.Right) && !_player.TryDash(Vector2.Right), "Dash starts once, then recharges");
			_player._PhysicsProcess(step);
			Check(_player.Velocity.X > 700 && !_player.DashReady, "Shift + direction dashes");
			Check(_player.GetNode<AnimatedSprite2D>("Body").Modulate == Colors.White, "Dash does not blink like damage");
			_player.Locked = true; _player._PhysicsProcess(step); _player.Locked = false; _player._PhysicsProcess(step);
			Check(_player.Velocity.X > 0 && _player.Velocity.X < 400, "A dialogue lock cancels a running dash");
			Input.ActionRelease("right"); _player.ResetPlayer(market);
			Check(_boss.GlobalPosition == new Vector2(8256, 1920) && _boss.Dormant && _boss.Locked, "Boss starts dormant in upper-right room");
			Check(!_boss.Present && !_boss.Visible, "Boss is hidden until the cat gives the main quest");
			var boundary = _world.GetNode<MapBoundary>("MapBoundary");
			Check(boundary.SolidColliderCount > 20, "Buildings, platforms and walls have colliders");
			var space = _player.GetWorld2D().DirectSpaceState;
			bool Solid(Vector2 p) => space.IntersectPoint(new PhysicsPointQueryParameters2D { Position = p, CollisionMask = 1 }).Count > 0;
			Check(Solid(new(3300, 2950)) && Solid(new(4900, 4100)) && Solid(new(7000, 1500)), "Cyber-cafe, raised wall and boss-room glass block the player");
			foreach (var name in new[] { "PlayerSpawn", "CatNpc", "RobotNpc", "YellowCat", "RedCat", "PurpleCat" })
				Check(!Solid(_world.GetNode<Node2D>(name).GlobalPosition), $"{name} stands on walkable floor");
			Check(!_room.Closed && _terrain.Generation == 0 && !_world.TryStartHack(), "Exploration has no gate, terrain hazards or hacks");
			Key(Godot.Key.Escape); Tick(); Check(_player.Locked, "Exploration pauses");
			Key(Godot.Key.Escape); Tick(); Check(_world.State == BossArena.ArenaState.Exploring && !_player.Locked && _boss.Locked, "Resume keeps boss dormant");
            _player.GlobalPosition = new(8000, 2200); Tick();
            Check(!_world.EncounterStarted, "Boss waits until main quest is accepted");
            var journal = _world.GetNode<QuestJournal>("QuestJournal");
            journal.TalkTo(_world.GetNode<QuestNpc>("CatNpc"));
            _player.GlobalPosition = new(3552, 3520); journal.SelectQuest(1); Tick();
            Check(_boss.Present && _boss.Visible, "Talking to the cat makes the boss appear");
            journal.SelectQuest(0);
			_player.GlobalPosition = new(8256, 900); Tick();
			Check(!_world.EncounterStarted, "Outside-room proximity cannot activate boss");
			_player.GlobalPosition = new(7504, 2624); Tick();
			Check(!_world.EncounterStarted, "Entrance requires three tiles of clearance");
			_player.GlobalPosition = new(6350, 2300); Tick();
			Check(!_world.EncounterStarted, "Being inside room but outside radius is insufficient");
			await Activate();
            GetNode<QuestInteraction>("/root/QuestInteraction")._UnhandledInput(new InputEventKey { Keycode = Godot.Key.Tab, Pressed = true });
			Check(_terrain.Connected && _terrain.Generation == 1, "Encounter terrain is connected");
			CheckTerrain(_terrain, _player.GlobalPosition, _boss.GlobalPosition);
			var gateRay = PhysicsRayQueryParameters2D.Create(new(7504, 2600), new(7504, 2800), 1);
			Check(_player.GetWorld2D().DirectSpaceState.IntersectRay(gateRay).Count > 0, "Closed entrance physically blocks passage");
			Key(Godot.Key.Escape); Tick(); Key(Godot.Key.Escape); Tick();
			Check(_world.State == BossArena.ArenaState.Combat && _room.Closed, "Combat pause preserves gate");
			_player.TakeDamage(100, _boss.GlobalPosition, true); Tick(); await Frames();
			Check(_world.RoundOver && !_room.Closed, "Death ends encounter and releases gate");
			_world.StartRound(); await Frames();
			Check(_player.Health == 100 && _boss.SuccessfulHacks == 0 && !_world.EncounterStarted && _terrain.Generation == 0, "Retry resets exploration and boss");
			await Activate();
			for (int stage = 1; stage <= 3; stage++)
			{
				for (int i = 0; i < Boss.HacksPerPhase; i++) await Hack(_boss);
				if (stage == 3) break;
				Tick(2);
                // Inspect spawn positions before any minion has walked toward the player.
                foreach (var actor in _world.GetNode("Minions").GetChildren()) actor.SetPhysicsProcess(false);
                await Frames();
				int expected = stage == 1 ? 3 : 5;
				Check(_world.Intermission && _world.MinionsRemaining == expected && _boss.PhaseFrozen, "Phase change creates correct minion wave");
				Check(_terrain.Generation == stage + 1 && _terrain.Connected, "Phase rebuild stays connected in boss room");
				CheckTerrain(_terrain, _player.GlobalPosition, _boss.GlobalPosition);
				foreach (var child in _world.GetNode("Minions").GetChildren())
				{
					var minion = (Minion)child; minion.SetPhysicsProcess(false);
					Check(_room.WorldBounds.HasPoint(minion.GlobalPosition) && _terrain.IsClear(minion.GlobalPosition, 60), "Minion spawns safely inside room");
					await Hack(minion);
				}
				Tick(2); await Frames();
				Check(!_world.Intermission && !_boss.PhaseFrozen && _room.Closed, "Clearing wave resumes boss while gate stays closed");
				_boss.EmitSignal(Boss.SignalName.LaserRequested, _boss.GlobalPosition + Vector2.Left * 80, Vector2.Left, 440f, 12);
				await Frames();
				Check(_world.GetNode("Lasers").GetChildCount() > 0, "Lasers survive at world-map coordinates");
			}
			Check(_world.GetNode<QuestJournal>("QuestJournal").MainCompleted && _world.State == BossArena.ArenaState.Exploring && !_boss.IsAlive && !_room.Closed && _boss.SuccessfulHacks == 9, "Nine hacks (3 phases x 3) complete main quest and return to exploration");
            Check(!_boss.Present, "Defeated boss disappears after the main quest");
            Check(_terrain.Footprints.Count == 0, "Victory clears combat cover so exploration routes remain accurate");
            journal.SelectQuest(1); await Frames(55);
            Check(!GetNode<QuestInteraction>("/root/QuestInteraction").Busy && !_player.Locked, "Tab during combat cannot queue a journal after victory");
            Check(journal.Objective() == _world.GetNode<QuestNpc>("RobotNpc") && journal.Navigator.Guide(_player.GlobalPosition, journal.Objective()) != null,
                "Main-first completion leaves a return route to start side quest");
            journal.TalkTo(_world.GetNode<QuestNpc>("RobotNpc"));
            Check(journal.MainCompleted && journal.SideAccepted && !journal.SideCompleted, "Side quest can start after completing main quest");
			_world.ReturnToMenu(); _world.StartRound(); await Frames();
			Check(_boss.Dormant && _world.MinionsRemaining == 0 && !_room.Closed, "Menu and restart clean up encounter");
			// Check the compact standalone arena as well as multiple regenerated world layouts.
			var compact = new ArenaTerrain { BuildWalls = false, DrawFloor = false };
			AddChild(compact);
			for (int stage = 1; stage <= 3; stage++)
			{
				compact.Generate(stage, new(320, 550), new(1500, 550));
				CheckTerrain(compact, new(320, 550), new(1500, 550));
				_terrain.Generate(stage, new(8000, 2200), _room.BossSpawn);
				CheckTerrain(_terrain, new(8000, 2200), _room.BossSpawn);
			}
            // Reproduce many layouts, including the compact arena that previously
            // lost too much cover when its four rows blocked navigation clearance.
            var rng = (RandomNumberGenerator)typeof(ArenaTerrain).GetField("_rng", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(compact);
            for (ulong seed = 1; seed <= 20; seed++)
            {
                rng.Seed = seed;
                for (int stage = 1; stage <= 3; stage++)
                {
                    compact.Generate(stage, new(320, 550), new(1500, 550));
                    Check(compact.Connected && compact.Footprints.Count >= 8, $"Compact terrain remains populated and connected: seed {seed}, stage {stage}");
                }
            }
            compact.QueueFree();
			GD.Print($"WORLD TESTS PASSED: {_checks}"); GetTree().Quit();
		}
		catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
	}
}
