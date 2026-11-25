using Godot;
using System.Collections.Generic;

namespace ProjectXBossArenaPrototype;

// Random combat cover for the boss arena. A new layout is generated for every phase.
//
// Pseudocode for Generate(stage):
//   clear old cover
//   repeat a few attempts:
//     scatter random obstacles (shape mix depends on the phase) at random
//     positions, sizes and rotations; reject spots that touch the player,
//     the boss, the arena edge or another obstacle (keep a walkable corridor)
//     bake a navigation grid; drop the newest obstacle until every free cell
//     is reachable again
//   keep the most populated connected layout and build its colliders/visuals
public partial class PXArenaTerrain : Node2D
{
	[Export] public Rect2 Bounds { get; set; } = new(64, 144, 1792, 824);
	[Export] public bool BuildWalls { get; set; } = true;
	[Export] public bool DrawFloor { get; set; } = true;
	/// <summary>Base obstacle size (min, max) in pixels. Individual shapes vary around it.</summary>
	[Export] public Vector2 ObstacleSizeRange { get; set; } = new(70, 130);
	/// <summary>Axis-aligned bounding box of every obstacle (tests and debugging).</summary>
	public readonly List<Rect2> Footprints = new();
	public int Generation { get; private set; }
	public bool Connected { get; private set; }

	private enum Kind { Rock, Pillar, Barrier, Crate, Corner, Shard }
	private sealed class Obstacle
	{
		public Kind Kind;
		public Vector2 Center;
		public float Radius;
		public Rect2 Box;
		public readonly List<Vector2[]> Polygons = new(); // world space, each one convex
	}

	private readonly RandomNumberGenerator _rng = new();
	private readonly AStarGrid2D _grid = new();
	private readonly List<Obstacle> _obstacles = new();
	private Node2D _objects;
	private int _stage = 1;
	private const int Cell = 16;
	private const float NavRadius = 52f;                  // actor clearance used by the grid
	private const float Corridor = NavRadius * 2 + Cell; // free space kept between cover pieces
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

	// ---------------------------------------------------------------- generate
	public void Generate(int stage, Vector2 player, Vector2 boss)
	{
		Clear(false); _stage = Mathf.Clamp(stage, 1, 3);
		// Bigger arenas get more cover, small ones a bit less. Later phases are busier.
		float areaFactor = Mathf.Clamp(Bounds.Area / (2272f * 960f), .75f, 1.5f);
		int wanted = Mathf.RoundToInt((_stage switch { 1 => 12, 2 => 14, _ => 16 }) * areaFactor);

		List<Obstacle> best = null;
		for (int attempt = 0; attempt < 8; attempt++)
		{
			var layout = Scatter(wanted, player, boss);
			SetLayout(layout);
			while (!Connected && _obstacles.Count > 0) { _obstacles.RemoveAt(_obstacles.Count - 1); RebuildFootprints(); BuildGrid(); }
			if (best == null || _obstacles.Count > best.Count) best = new List<Obstacle>(_obstacles);
			if (best.Count >= wanted - 1) break;
		}
		SetLayout(best);
		for (int i = 0; i < _obstacles.Count; i++) MakeObstacle(_obstacles[i], i);
		Generation++; QueueRedraw();
	}

	private List<Obstacle> Scatter(int wanted, Vector2 player, Vector2 boss)
	{
		var placed = new List<Obstacle>();
		// Centres stay a corridor away from the arena edge; pieces may still lean
		// against a wall. Sealed pockets are removed later by the connectivity check.
		Rect2 inner = Bounds.Grow(-Corridor);
		for (int tries = 0; tries < 600 && placed.Count < wanted; tries++)
		{
			var center = new Vector2(_rng.RandfRange(inner.Position.X, inner.End.X), _rng.RandfRange(inner.Position.Y, inner.End.Y));
			var obstacle = Build(PickKind(), center, _rng.RandfRange(0, Mathf.Tau));
			if (!inner.Grow(Corridor - 8).Encloses(obstacle.Box)) continue;
			if (center.DistanceTo(player) < obstacle.Radius + 150 || center.DistanceTo(boss) < obstacle.Radius + 170) continue;
			bool free = true;
			foreach (var other in placed)
				if (center.DistanceTo(other.Center) < obstacle.Radius + other.Radius + Corridor) { free = false; break; }
			if (free) placed.Add(obstacle);
		}
		return placed;
	}

	// Phase themes: 1 = scattered rocks and pillars, 2 = crates and long barriers,
	// 3 = wrecked debris with sharp shards and corner walls.
	private Kind PickKind()
	{
		(Kind Kind, int Weight)[] table = _stage switch
		{
			1 => new[] { (Kind.Rock, 35), (Kind.Pillar, 30), (Kind.Crate, 35) },
			2 => new[] { (Kind.Crate, 25), (Kind.Barrier, 40), (Kind.Pillar, 15), (Kind.Corner, 20) },
			_ => new[] { (Kind.Rock, 20), (Kind.Barrier, 20), (Kind.Corner, 25), (Kind.Shard, 35) },
		};
		int total = 0; foreach (var entry in table) total += entry.Weight;
		int roll = _rng.RandiRange(0, total - 1);
		foreach (var entry in table) { if (roll < entry.Weight) return entry.Kind; roll -= entry.Weight; }
		return table[0].Kind;
	}

	private Obstacle Build(Kind kind, Vector2 center, float angle)
	{
		// Skew sizes towards the small end so the arena does not fill with huge blocks.
		float size = Mathf.Lerp(ObstacleSizeRange.X, ObstacleSizeRange.Y, Mathf.Pow(_rng.Randf(), 1.4f));
		var local = new List<Vector2[]>();
		switch (kind)
		{
			case Kind.Rock:
			{
				int count = _rng.RandiRange(7, 9); var points = new Vector2[count];
				for (int i = 0; i < count; i++)
					points[i] = Vector2.Right.Rotated(Mathf.Tau * i / count + _rng.RandfRange(-.25f, .25f)) * size * .5f * _rng.RandfRange(.72f, 1f);
				local.Add(Geometry2D.ConvexHull(points)[..^1]); // ConvexHull repeats the first point
				break;
			}
			case Kind.Pillar:
			{
				float radius = size * _rng.RandfRange(.3f, .42f); var points = new Vector2[16];
				for (int i = 0; i < 16; i++) points[i] = Vector2.Right.Rotated(Mathf.Tau * i / 16) * radius;
				local.Add(points);
				break;
			}
			case Kind.Barrier:
				local.Add(Box(Vector2.Zero, new(size * _rng.RandfRange(1.4f, 2f), size * _rng.RandfRange(.3f, .4f))));
				break;
			case Kind.Crate:
				local.Add(Box(Vector2.Zero, Vector2.One * size * _rng.RandfRange(.65f, .85f)));
				break;
			case Kind.Corner:
			{
				// L-shape built from two convex arms that share the corner.
				float thick = size * .34f, a = size * 1.25f, b = size * _rng.RandfRange(.8f, 1.05f);
				local.Add(Box(new(a / 2 - thick / 2, 0), new(a, thick)));
				local.Add(Box(new(0, b / 2 - thick / 2), new(thick, b)));
				for (int p = 0; p < local.Count; p++) for (int i = 0; i < local[p].Length; i++) local[p][i] -= new Vector2(a, b) * .25f;
				break;
			}
			default: // Shard
				local.Add(new[] {
					Vector2.Up.Rotated(_rng.RandfRange(-.3f, .3f)) * size * .62f,
					Vector2.Right.Rotated(_rng.RandfRange(.35f, .75f)) * size * .5f,
					Vector2.Left.Rotated(_rng.RandfRange(-.75f, -.35f)) * size * .55f });
				break;
		}
		var obstacle = new Obstacle { Kind = kind, Center = center };
		bool first = true;
		foreach (var polygon in local)
		{
			var world = new Vector2[polygon.Length];
			for (int i = 0; i < polygon.Length; i++)
			{
				world[i] = center + polygon[i].Rotated(angle);
				obstacle.Radius = Mathf.Max(obstacle.Radius, world[i].DistanceTo(center));
				if (first) { obstacle.Box = new Rect2(world[i], Vector2.Zero); first = false; }
				else obstacle.Box = obstacle.Box.Expand(world[i]);
			}
			obstacle.Polygons.Add(world);
		}
		return obstacle;
	}

	private static Vector2[] Box(Vector2 center, Vector2 size)
	{
		Vector2 h = size / 2;
		return new[] { center - h, center + new Vector2(h.X, -h.Y), center + h, center + new Vector2(-h.X, h.Y) };
	}

	private void SetLayout(List<Obstacle> layout)
	{
		_obstacles.Clear(); _obstacles.AddRange(layout);
		RebuildFootprints(); BuildGrid();
	}

	private void RebuildFootprints()
	{
		Footprints.Clear();
		foreach (var obstacle in _obstacles) Footprints.Add(obstacle.Box);
	}

	public void Clear(bool resetGeneration = true)
	{
		foreach (Node child in _objects.GetChildren()) { _objects.RemoveChild(child); child.QueueFree(); }
		_obstacles.Clear(); Footprints.Clear(); if (resetGeneration) Generation = 0; QueueRedraw();
	}

	// ----------------------------------------------------------------- visuals
	private static readonly Color[] Fill = { new("2f4858"), new("4a3426"), new("3d1f33") };
	private static readonly Color[] Edge = { new("6fb3c8"), new("e08a3c"), new("e0466a") };

	private void MakeObstacle(Obstacle obstacle, int index)
	{
		var body = new StaticBody2D { Position = obstacle.Center, CollisionLayer = 1, CollisionMask = 14 };
		var visual = new Node2D { Name = "Visual" };
		Color fill = Fill[_stage - 1], edge = Edge[_stage - 1];
		// Slight colour variation per piece so the field does not look copy-pasted.
		fill = fill.Lightened(_rng.RandfRange(-.06f, .1f));
		foreach (var world in obstacle.Polygons)
		{
			var points = new Vector2[world.Length];
			for (int i = 0; i < world.Length; i++) points[i] = world[i] - obstacle.Center;
			body.AddChild(new CollisionShape2D { Shape = new ConvexPolygonShape2D { Points = points } });
			visual.AddChild(new Polygon2D { Polygon = points, Color = new Color(0, 0, 0, .35f), Position = new Vector2(6, 8) });
		}
		foreach (var world in obstacle.Polygons)
		{
			var points = new Vector2[world.Length];
			var inner = new Vector2[world.Length];
			Vector2 mid = Vector2.Zero;
			for (int i = 0; i < world.Length; i++) { points[i] = world[i] - obstacle.Center; mid += points[i]; }
			mid /= world.Length;
			for (int i = 0; i < world.Length; i++) inner[i] = mid + (points[i] - mid) * .62f;
			visual.AddChild(new Polygon2D { Polygon = points, Color = fill });
			visual.AddChild(new Polygon2D { Polygon = inner, Color = fill.Lightened(.14f) });
			visual.AddChild(new Line2D { Width = 3, DefaultColor = edge, Closed = true, Points = points });
		}
		if (obstacle.Kind == Kind.Pillar)
			visual.AddChild(new Polygon2D { Polygon = Box(Vector2.Zero, Vector2.One * 10), Color = edge, Rotation = Mathf.Pi / 4 });
		body.AddChild(visual); _objects.AddChild(body);
		// Spawn animation: pieces pop in one after another (visual only, collision is instant).
		visual.Scale = Vector2.One * .15f; visual.Modulate = new Color(1, 1, 1, 0);
		var tween = visual.CreateTween().SetParallel();
		tween.TweenProperty(visual, "scale", Vector2.One, .35f).SetDelay(index * .035f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
		tween.TweenProperty(visual, "modulate:a", 1f, .2f).SetDelay(index * .035f);
	}

	// ------------------------------------------------------------- navigation
	public bool IsClear(Vector2 point, float radius)
	{
		if (!Bounds.Grow(-radius).HasPoint(point)) return false;
		foreach (var obstacle in _obstacles)
		{
			if (!obstacle.Box.Grow(radius).HasPoint(point)) continue;
			foreach (var polygon in obstacle.Polygons)
			{
				if (Geometry2D.IsPointInPolygon(point, polygon)) return false;
				for (int i = 0; i < polygon.Length; i++)
				{
					Vector2 a = polygon[i], b = polygon[(i + 1) % polygon.Length];
					if (point.DistanceTo(Geometry2D.GetClosestPointToSegment(point, a, b)) < radius) return false;
				}
			}
		}
		return true;
	}

	private void BuildGrid()
	{
		int total = 0; Vector2I first = new(-1, -1);
		for (int y = 0; y < Rows; y++) for (int x = 0; x < Cols; x++)
		{
			var id = new Vector2I(x, y); bool solid = !IsClear(_grid.GetPointPosition(id), NavRadius);
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

	// ------------------------------------------------------------------ floor
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
