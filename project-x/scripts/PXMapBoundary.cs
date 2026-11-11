using Godot;
using System.Collections.Generic;

namespace ProjectXBossArenaPrototype;

// Merge exposed floor edges into walls; editing the painted floor updates collision too.
public partial class PXMapBoundary : Node2D
{
	[Export] public NodePath GroundPath { get; set; } = new("../MainMap/groundbase");
	public override void _Ready()
	{
		var floor = GetNode<TileMapLayer>(GroundPath);
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
}
