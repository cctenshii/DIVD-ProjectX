using Godot;
using System;
using System.Threading.Tasks;
using ProjectXBossArenaPrototype;

public partial class PXWorldSmokeTests : Node
{
	private PXBossArena _world;
	private PXArenaPlayer _player;
	private PXArenaBoss _boss;
	private PXBossRoom _room;
	private PXBossHUD _hud;
	private PXArenaTerrain _terrain;
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
			if (target is PXArenaMinion minion && !minion.HasSight(position)) continue;
			_player.GlobalPosition = position; found = true; break;
		}
		Check(found, "Target has a safe hack approach around larger cover");
		await Frames();
		Check(_world.TryStartHack(), "Nearby target can be hacked");
		if (_hud.Instructions.Active) _hud.Instructions.Advance();
		Tick();
		Check(_world.State == PXBossArena.ArenaState.Hacking && _player.Locked && _boss.Locked, "Hack freezes combat");
		_hud.Hack.CancelSilently();
		_hud.Hack.EmitSignal(PXHackPanel.SignalName.Finished, true);
		Tick(); await Frames();
	}
	private void CheckTerrain(PXArenaTerrain terrain, Vector2 player, Vector2 boss)
	{
		Check(terrain.Connected && terrain.Footprints.Count >= 8, "Larger cover retains a connected, populated arena");
		foreach (var rect in terrain.Footprints)
		{
			Check(terrain.Bounds.Encloses(rect) && !rect.Grow(18).HasPoint(player) && !rect.Grow(52).HasPoint(boss), "Cover stays in bounds and leaves actors clear");
			Check(rect.Size.X >= terrain.ObstacleSizeRange.X && rect.Size.Y >= terrain.ObstacleSizeRange.X, "Cover uses enlarged dimensions");
		}
		var occupied = new System.Collections.Generic.List<Vector2>();
		for (int i = 0; i < 5; i++)
		{
			var spawn = terrain.FindSpawn(player, boss, occupied); occupied.Add(spawn);
			Check(terrain.IsClear(spawn, 60), "Five safe minion positions remain available");
		}
	}
	public override async void _Ready()
	{
		const string preferences = "user://projectx_boss_tutorials.cfg";
		bool hadPreferences = FileAccess.FileExists(preferences);
		string savedPreferences = hadPreferences ? FileAccess.GetFileAsString(preferences) : null;
		try
		{
			_world = GD.Load<PackedScene>("res://scenes/World.tscn").Instantiate<PXBossArena>();
			AddChild(_world);
			_world.SetPhysicsProcess(false);
			_player = _world.GetNode<PXArenaPlayer>("Player"); _player.SetPhysicsProcess(false);
			_boss = _world.GetNode<PXArenaBoss>("Boss"); _boss.SetPhysicsProcess(false);
			_room = _world.GetNode<PXBossRoom>("BossRoom"); _hud = _world.GetNode<PXBossHUD>("HUD");
			_terrain = _world.GetNode<PXArenaTerrain>("Terrain");
			await Frames(); Tick();
			Check(_player.GlobalPosition == new Vector2(3744, 6656), "Player starts at bottom marker");
			Check(_player.GetNode<Sprite2D>("Body").Texture.ResourcePath.EndsWith("all-position-V2.png"), "Player uses requested spritesheet");
			Check(_boss.GlobalPosition == new Vector2(8256, 1344) && _boss.Dormant && _boss.Locked, "Boss starts dormant in upper-right room");
			Check(!_room.Closed && _terrain.Generation == 0 && !_world.TryStartHack(), "Exploration has no gate, terrain hazards or hacks");
			Key(Godot.Key.Escape); Tick(); Check(_player.Locked, "Exploration pauses");
			Key(Godot.Key.Escape); Tick(); Check(_world.State == PXBossArena.ArenaState.Exploring && !_player.Locked && _boss.Locked, "Resume keeps boss dormant");
			_player.GlobalPosition = new(8256, 900); Tick();
			Check(!_world.EncounterStarted, "Outside-room proximity cannot activate boss");
			_player.GlobalPosition = new(7504, 2624); Tick();
			Check(!_world.EncounterStarted, "Entrance requires three tiles of clearance");
			_player.GlobalPosition = new(5800, 2400); Tick();
			Check(!_world.EncounterStarted, "Being inside room but outside radius is insufficient");
			await Activate();
			Check(_terrain.Connected && _terrain.Generation == 1, "Encounter terrain is connected");
			CheckTerrain(_terrain, _player.GlobalPosition, _boss.GlobalPosition);
			var gateRay = PhysicsRayQueryParameters2D.Create(new(7504, 2600), new(7504, 2800), 1);
			Check(_player.GetWorld2D().DirectSpaceState.IntersectRay(gateRay).Count > 0, "Closed entrance physically blocks passage");
			Key(Godot.Key.Escape); Tick(); Key(Godot.Key.Escape); Tick();
			Check(_world.State == PXBossArena.ArenaState.Combat && _room.Closed, "Combat pause preserves gate");
			_player.TakeDamage(100, _boss.GlobalPosition, true); Tick(); await Frames();
			Check(_world.RoundOver && !_room.Closed, "Death ends encounter and releases gate");
			_world.StartRound(); await Frames();
			Check(_player.Health == 100 && _boss.SuccessfulHacks == 0 && !_world.EncounterStarted && _terrain.Generation == 0, "Retry resets exploration and boss");
			await Activate();
			for (int stage = 1; stage <= 3; stage++)
			{
				for (int i = 0; i < 5; i++) await Hack(_boss);
				if (stage == 3) break;
				Tick(2); await Frames();
				int expected = stage == 1 ? 3 : 5;
				Check(_world.Intermission && _world.MinionsRemaining == expected && _boss.PhaseFrozen, "Phase change creates correct minion wave");
				Check(_terrain.Generation == stage + 1 && _terrain.Connected, "Phase rebuild stays connected in boss room");
				CheckTerrain(_terrain, _player.GlobalPosition, _boss.GlobalPosition);
				foreach (var child in _world.GetNode("Minions").GetChildren())
				{
					var minion = (PXArenaMinion)child; minion.SetPhysicsProcess(false);
					Check(_room.WorldBounds.HasPoint(minion.GlobalPosition) && _terrain.IsClear(minion.GlobalPosition, 60), "Minion spawns safely inside room");
					await Hack(minion);
				}
				Tick(2); await Frames();
				Check(!_world.Intermission && !_boss.PhaseFrozen && _room.Closed, "Clearing wave resumes boss while gate stays closed");
				_boss.EmitSignal(PXArenaBoss.SignalName.LaserRequested, _boss.GlobalPosition + Vector2.Left * 80, Vector2.Left, 440f, 12);
				await Frames();
				Check(_world.GetNode("Lasers").GetChildCount() > 0, "Lasers survive at world-map coordinates");
			}
			Check(_world.RoundOver && !_boss.IsAlive && !_room.Closed && _boss.SuccessfulHacks == 15, "Fifteen hacks complete all phases and open gate");
			_world.ReturnToMenu(); _world.StartRound(); await Frames();
			Check(_boss.Dormant && _world.MinionsRemaining == 0 && !_room.Closed, "Menu and restart clean up encounter");
			// Check the compact standalone arena as well as multiple regenerated world layouts.
			var compact = new PXArenaTerrain { BuildWalls = false, DrawFloor = false };
			AddChild(compact);
			for (int stage = 1; stage <= 3; stage++)
			{
				compact.Generate(stage, new(320, 550), new(1500, 550));
				CheckTerrain(compact, new(320, 550), new(1500, 550));
				_terrain.Generate(stage, new(8000, 2200), _room.BossSpawn);
				CheckTerrain(_terrain, new(8000, 2200), _room.BossSpawn);
			}
			compact.QueueFree();
			GD.Print($"WORLD TESTS PASSED: {_checks}"); GetTree().Quit();
		}
		catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
		finally
		{
			if (hadPreferences) { using var file = FileAccess.Open(preferences, FileAccess.ModeFlags.Write); file.StoreString(savedPreferences); }
			else DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(preferences));
		}
	}
}
