using Godot;
using System.Collections.Generic;

namespace ProjectXBossArenaPrototype;

public partial class PXArenaTerrain : Node2D
{
	[Export] public Rect2 Bounds { get; set; } = new(64, 144, 1792, 824);
	[Export] public bool BuildWalls { get; set; } = true;
	[Export] public bool DrawFloor { get; set; } = true;
	[Export] public Vector2 ObstacleSizeRange { get; set; } = new(120, 156);
	public readonly List<Rect2> Footprints = new();
	public int Generation { get; private set; }
	public bool Connected { get; private set; }
	private readonly RandomNumberGenerator _rng = new();
	private readonly AStarGrid2D _grid = new();
	private Node2D _objects;
	private int _stage = 1;
	private const int Cell = 16;
	private int Cols => (int)(Bounds.Size.X / Cell);
	private int Rows => (int)(Bounds.Size.Y / Cell);

	public override void _Ready()
	{
		_rng.Randomize(); _objects = new Node2D { Name = "Obstacles" }; AddChild(_objects);
		if (BuildWalls)
		{
			var center = Bounds.GetCenter();
			Wall(new(center.X, Bounds.Position.Y - 12), new(Bounds.Size.X + 24, 24));
			Wall(new(center.X, Bounds.End.Y + 12), new(Bounds.Size.X + 24, 24));
			Wall(new(Bounds.Position.X - 12, center.Y), new(24, Bounds.Size.Y));
			Wall(new(Bounds.End.X + 12, center.Y), new(24, Bounds.Size.Y));
		}
		_grid.Region = new Rect2I(0, 0, Cols, Rows); _grid.CellSize = new(Cell, Cell);
		_grid.Offset = Bounds.Position + new Vector2(8, 8);
		_grid.DiagonalMode = AStarGrid2D.DiagonalModeEnum.Never; _grid.Update();
	}
	private void Wall(Vector2 center, Vector2 size)
	{
		var body = new StaticBody2D { Position = center, CollisionLayer = 1, CollisionMask = 14 };
		body.AddChild(new CollisionShape2D { Shape = new RectangleShape2D { Size = size } }); AddChild(body);
	}
	public void Generate(int stage, Vector2 player, Vector2 boss)
	{
		Clear(false); _stage = stage;
		// Jittered, shuffled cells give different layouts while retaining wide corridors.
		var candidates = new List<Rect2>();
		for (int row = 0; row < 4; row++) for (int col = 0; col < 7; col++)
		{
			var center = Bounds.Position + new Vector2((col + .5f) * Bounds.Size.X / 7, (row + .5f) * Bounds.Size.Y / 4) + new Vector2(_rng.RandfRange(-6, 6), _rng.RandfRange(-6, 6));
			var size = new Vector2(_rng.RandfRange(ObstacleSizeRange.X, ObstacleSizeRange.Y),
				_rng.RandfRange(ObstacleSizeRange.X, ObstacleSizeRange.Y));
			var rect = new Rect2(center - size / 2, size);
			if (!rect.Grow(140).HasPoint(player) && !rect.Grow(140).HasPoint(boss)) candidates.Add(rect);
		}
		for (int i = candidates.Count - 1; i > 0; i--)
		{ int j = _rng.RandiRange(0, i); (candidates[i], candidates[j]) = (candidates[j], candidates[i]); }
		int wanted = 16 + (stage - 1) * 2;
		for (int i = 0; i < Mathf.Min(wanted, candidates.Count); i++) Footprints.Add(candidates[i]);
		BuildGrid();
		while (!Connected && Footprints.Count > 0)
		{ Footprints.RemoveAt(Footprints.Count - 1); BuildGrid(); }
		for (int i = 0; i < Footprints.Count; i++) MakeObstacle(Footprints[i], _rng.RandiRange(0, 2));
		Generation++; QueueRedraw();
	}
	public void Clear(bool resetGeneration = true)
	{
		foreach (Node child in _objects.GetChildren()) { _objects.RemoveChild(child); child.QueueFree(); }
		Footprints.Clear(); if (resetGeneration) Generation = 0; QueueRedraw();
	}
	private void MakeObstacle(Rect2 rect, int kind)
	{
		Vector2 half = rect.Size / 2; var body = new StaticBody2D { Position = rect.GetCenter(), CollisionLayer = 1, CollisionMask = 14 };
		Vector2[] points;
		if (kind == 0)
		{
			points = new Vector2[24]; float radius = Mathf.Min(half.X, half.Y);
			for (int i = 0; i < 24; i++) points[i] = Vector2.Right.Rotated(Mathf.Tau * i / 24) * radius;
			body.AddChild(new CollisionShape2D { Shape = new CircleShape2D { Radius = radius } });
		}
		else
		{
			points = kind == 1 ? new[] { -half, new Vector2(half.X, -half.Y), half, new Vector2(-half.X, half.Y) }
				: new[] { new Vector2(0, -half.Y), half, new Vector2(-half.X, half.Y) };
			body.AddChild(new CollisionShape2D { Shape = new ConvexPolygonShape2D { Points = points } });
		}
		body.AddChild(new Polygon2D { Polygon = points, Color = new Color("344b5c") });
		var outline = new Line2D { Width = 3, DefaultColor = new Color("6e8a99"), Closed = true, Points = points };
		body.AddChild(outline); _objects.AddChild(body);
	}
	public bool IsClear(Vector2 point, float radius)
	{
		if (!Bounds.Grow(-radius).HasPoint(point)) return false;
		foreach (var rect in Footprints) if (rect.Grow(radius).HasPoint(point)) return false;
		return true;
	}
	private void BuildGrid()
	{
		int total = 0; Vector2I first = new(-1, -1);
		for (int y = 0; y < Rows; y++) for (int x = 0; x < Cols; x++)
		{
			var id = new Vector2I(x, y); bool solid = !IsClear(_grid.GetPointPosition(id), 52);
			_grid.SetPointSolid(id, solid); if (!solid) { first = id; total++; }
		}
		var visited = new HashSet<Vector2I>(); var queue = new Queue<Vector2I>();
		if (total > 0) { visited.Add(first); queue.Enqueue(first); }
		while (queue.Count > 0)
		{
			var id = queue.Dequeue();
			foreach (var d in new[] { Vector2I.Left, Vector2I.Right, Vector2I.Up, Vector2I.Down })
			{ var n = id + d; if (_grid.IsInBoundsv(n) && !_grid.IsPointSolid(n) && visited.Add(n)) queue.Enqueue(n); }
		}
		Connected = total > 0 && visited.Count == total;
	}
	public Vector2 FindSpawn(Vector2 player, Vector2 boss, List<Vector2> occupied)
	{
		for (int i = 0; i < 4000; i++)
		{
			var p = new Vector2(_rng.RandfRange(Bounds.Position.X + 76, Bounds.End.X - 76), _rng.RandfRange(Bounds.Position.Y + 76, Bounds.End.Y - 76));
			if (!IsClear(p, 60) || p.DistanceTo(player) < 280 || p.DistanceTo(boss) < 150) continue;
			bool free = true; foreach (var other in occupied) free &= p.DistanceTo(other) > 160;
			if (free) return p;
		}
		for (int y = 0; y < Rows; y++) for (int x = 0; x < Cols; x++)
		{
			var p = _grid.GetPointPosition(new(x, y)); if (!IsClear(p, 60) || p.DistanceTo(player) < 200 || p.DistanceTo(boss) < 130) continue;
			bool free = true; foreach (var other in occupied) free &= p.DistanceTo(other) > 90; if (free) return p;
		}
		throw new System.InvalidOperationException("No safe minion spawn available.");
	}
	public Vector2 DirectionTo(Vector2 from, Vector2 target)
	{
		Vector2I a = NearestFree(from), b = NearestFree(target);
		var path = _grid.GetPointPath(a, b);
		return path.Length < 2 ? from.DirectionTo(target) : from.DirectionTo(path[1]);
	}
	private Vector2I NearestFree(Vector2 point)
	{
		var id = new Vector2I(Mathf.Clamp((int)((point.X - Bounds.Position.X) / Cell), 0, Cols - 1), Mathf.Clamp((int)((point.Y - Bounds.Position.Y) / Cell), 0, Rows - 1));
		for (int radius = 0; radius < 12; radius++) for (int y = -radius; y <= radius; y++) for (int x = -radius; x <= radius; x++)
		{ var n = id + new Vector2I(x, y); if (_grid.IsInBoundsv(n) && !_grid.IsPointSolid(n)) return n; }
		return id;
	}
	public override void _Draw()
	{
		if (!DrawFloor)
		{
			if (Generation > 0) DrawRect(Bounds, _stage switch {
				2 => new Color(.8f, .4f, .05f, .22f), 3 => new Color(.65f, .08f, .5f, .28f),
				_ => new Color(.1f, .3f, .45f, .16f) });
			return;
		}
		DrawRect(new(0, 0, 1920, 1080), new Color("0b1420")); DrawRect(Bounds, new Color("142332"));
		for (float x = 64; x < 1856; x += 32) DrawLine(new(x, 144), new(x, 968), new Color("1b2e3e"));
		for (float y = 144; y < 968; y += 32) DrawLine(new(64, y), new(1856, y), new Color("1b2e3e"));
		DrawRect(Bounds, new Color("668790"), false, 4);
		for (int x = 100; x < 1850; x += 120) { DrawRect(new(x, 140, 48, 6), new Color("87ead8")); DrawRect(new(x, 966, 48, 6), new Color("87ead8")); }
	}
}
