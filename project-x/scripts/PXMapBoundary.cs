using Godot;
using System.Collections.Generic;

namespace ProjectXBossArenaPrototype;

// Builds the world collision for main_map at startup.
// 1) Exposed floor edges of the ground layer become thin walls (map border).
// 2) Every painted tile on a "solid" layer (buildings, raised platforms, walls,
//    market stalls) becomes part of a merged rectangle collider.
// 3) Tiles on a "walkable" layer (streets) cut through solid layers.
// Editing the painted map automatically updates the collision on the next run.
public partial class PXMapBoundary : Node2D
{
	[Export] public NodePath GroundPath { get; set; } = new("../MainMap/groundbase");
	/// <summary>Other floor layers that need a border too (Jade's separate side-quest area).</summary>
	[Export] public string[] ExtraGroundLayers { get; set; } = { "groundbaseQuest" };
	[Export] public string[] SolidLayers { get; set; } = { "plateu", "decor-poor-area", "building", "bg-buildings" };
	[Export] public string[] WalkableLayers { get; set; } = { "streets" };
	/// <summary>Number of rectangle colliders created for solid layers (used by tests).</summary>
	public int SolidColliderCount { get; private set; }

	public override void _Ready()
	{
		var floor = GetNode<TileMapLayer>(GroundPath);
		BuildFloorEdges(floor);
		foreach (string name in ExtraGroundLayers)
			if (floor.GetParent().GetNodeOrNull<TileMapLayer>(name) is { } extra) BuildFloorEdges(extra);
		BuildSolidLayers(floor.GetParent());
	}

	// --- 1. Map border -------------------------------------------------------
	private void BuildFloorEdges(TileMapLayer floor)
	{
		var cells = new HashSet<Vector2I>(floor.GetUsedCells());
		var edges = new Dictionary<(bool Horizontal, int Line), List<int>>();
		void Edge(bool horizontal, int line, int start)
		{
			var key = (horizontal, line);
			if (!edges.TryGetValue(key, out var spans)) edges[key] = spans = new();
			spans.Add(start);
		}
		foreach (var p in cells)
		{
			if (!cells.Contains(p + Vector2I.Up)) Edge(true, p.Y, p.X);
			if (!cells.Contains(p + Vector2I.Down)) Edge(true, p.Y + 1, p.X);
			if (!cells.Contains(p + Vector2I.Left)) Edge(false, p.X, p.Y);
			if (!cells.Contains(p + Vector2I.Right)) Edge(false, p.X + 1, p.Y);
		}
		Vector2 tile = floor.TileSet.TileSize;
		foreach (var (key, spans) in edges)
		{
			spans.Sort();
			for (int i = 0; i < spans.Count; i++)
			{
				int start = spans[i], end = start + 1;
				while (i + 1 < spans.Count && spans[i + 1] == end) { i++; end++; }
				Vector2 a = key.Horizontal ? new(start * tile.X, key.Line * tile.Y) : new(key.Line * tile.X, start * tile.Y);
				Vector2 b = key.Horizontal ? new(end * tile.X, key.Line * tile.Y) : new(key.Line * tile.X, end * tile.Y);
				var wall = new StaticBody2D { CollisionLayer = 1, CollisionMask = 14 };
				wall.AddChild(new CollisionShape2D { Shape = new SegmentShape2D { A = ToLocal(floor.ToGlobal(a)), B = ToLocal(floor.ToGlobal(b)) } });
				AddChild(wall);
			}
		}
	}

	// --- 2. Buildings, platforms and walls ------------------------------------
	private void BuildSolidLayers(Node map)
	{
		// Pseudocode:
		//   walkable = all cells of the street layers
		//   for each solid layer:
		//     cells = painted cells whose centre is NOT on a street
		//     merge neighbouring cells into as few rectangles as possible
		//     add one StaticBody2D per rectangle on collision layer 1
		var walkable = new List<(TileMapLayer Layer, HashSet<Vector2I> Cells)>();
		foreach (string name in WalkableLayers)
			if (map.GetNodeOrNull<TileMapLayer>(name) is { } street)
				walkable.Add((street, new HashSet<Vector2I>(street.GetUsedCells())));

		foreach (string name in SolidLayers)
		{
			if (map.GetNodeOrNull<TileMapLayer>(name) is not { } layer || layer.TileSet == null) continue;
			Vector2 tile = layer.TileSet.TileSize;
			var solid = new HashSet<Vector2I>();
			foreach (var cell in layer.GetUsedCells())
			{
				Vector2 center = layer.ToGlobal((cell + new Vector2(.5f, .5f)) * tile);
				if (!OnWalkable(walkable, center)) solid.Add(cell);
			}
			foreach (var rect in MergeCells(solid))
			{
				// Convert the cell rectangle to world space (handles layer offset and scale).
				Vector2 a = ToLocal(layer.ToGlobal(rect.Position * tile));
				Vector2 b = ToLocal(layer.ToGlobal(rect.End * tile));
				var body = new StaticBody2D { Name = $"Solid_{name}_{SolidColliderCount}", Position = (a + b) / 2, CollisionLayer = 1, CollisionMask = 14 };
				body.AddChild(new CollisionShape2D { Shape = new RectangleShape2D { Size = (b - a).Abs() } });
				AddChild(body);
				SolidColliderCount++;
			}
		}
	}

	private static bool OnWalkable(List<(TileMapLayer Layer, HashSet<Vector2I> Cells)> walkable, Vector2 world)
	{
		foreach (var (layer, cells) in walkable)
			if (cells.Contains(layer.LocalToMap(layer.ToLocal(world)))) return true;
		return false;
	}

	// Greedy merge: grow each rectangle to the right, then downwards while the
	// whole row below is solid. Turns thousands of tiles into a few hundred boxes.
	private static List<Rect2I> MergeCells(HashSet<Vector2I> cells)
	{
		var result = new List<Rect2I>();
		var used = new HashSet<Vector2I>();
		var ordered = new List<Vector2I>(cells);
		ordered.Sort((p, q) => p.Y != q.Y ? p.Y.CompareTo(q.Y) : p.X.CompareTo(q.X));
		foreach (var start in ordered)
		{
			if (used.Contains(start)) continue;
			int width = 1;
			while (cells.Contains(start + new Vector2I(width, 0)) && !used.Contains(start + new Vector2I(width, 0))) width++;
			int height = 1;
			while (true)
			{
				bool fullRow = true;
				for (int x = 0; x < width && fullRow; x++)
				{
					var next = start + new Vector2I(x, height);
					fullRow = cells.Contains(next) && !used.Contains(next);
				}
				if (!fullRow) break;
				height++;
			}
			for (int y = 0; y < height; y++) for (int x = 0; x < width; x++) used.Add(start + new Vector2I(x, y));
			result.Add(new Rect2I(start, new Vector2I(width, height)));
		}
		return result;
	}
}
