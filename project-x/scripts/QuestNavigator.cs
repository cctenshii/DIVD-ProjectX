using Godot;
using System.Collections.Generic;

// Bake collision-aware floor cells once in small batches. Replan only when needed.
// The arrow follows a reachable waypoint, never a straight line through a wall.
//
// The grid is aligned to WORLD space (32 px cells), so it can cover several floor
// layers at once: the main map ("groundbase") and Jade's rotated side-quest area
// ("groundbaseQuest"). The two areas are only connected by QuestEntrance teleports:
//   target unreachable on foot? -> route to the teleport whose destination can reach it.
public partial class QuestNavigator : Node2D
{
    [Export] public string[] FloorLayers { get; set; } = { "groundbase", "groundbaseQuest" };
    private const int CellSize = 32;
    private readonly AStarGrid2D _grid = new();
    private readonly HashSet<Vector2I> _floor = new();
    private readonly List<Vector2I> _pending = new();
    private readonly List<Doors> _doors = new();
    private readonly List<QuestEntrance> _portals = new();
    private PhysicsShapeQueryParameters2D _query;
    private int _cursor;
    private bool _dirty = true, _doorsDirty;
    private int _doorRefreshFrames;
    private Vector2 _lastStart = Vector2.Inf, _lastGoal = Vector2.Inf;
    private Vector2[] _path = System.Array.Empty<Vector2>();
    public bool IsReadyForRoutes => _cursor >= _pending.Count && _pending.Count > 0;
    public bool HasRoute { get; private set; }
    public int RoutePointCount => _path.Length;
    public Marker2D Waypoint { get; private set; }
    private double _repath;

    public override void _Ready()
    {
        var map = GetNode<Node>("../MainMap");
        foreach (string name in FloorLayers)
        {
            if (map.GetNodeOrNull<TileMapLayer>(name) is not { } layer) continue;
            Vector2 tile = layer.TileSet.TileSize;
            foreach (var cell in layer.GetUsedCells())
            {
                var id = ToCell(layer.ToGlobal((cell + new Vector2(.5f, .5f)) * tile));
                if (_floor.Add(id)) _pending.Add(id);
            }
        }
        var region = new Rect2I();
        bool first = true;
        foreach (var id in _floor) { if (first) { region = new Rect2I(id, Vector2I.One); first = false; } else region = region.Expand(id).Expand(id + Vector2I.One); }
        _grid.Region = region; _grid.CellSize = new(CellSize, CellSize); _grid.Offset = new Vector2(CellSize / 2f, CellSize / 2f);
        _grid.DiagonalMode = AStarGrid2D.DiagonalModeEnum.Never; _grid.Update(); _grid.FillSolidRegion(_grid.Region);
        _query = new PhysicsShapeQueryParameters2D { Shape = new CircleShape2D { Radius = 23 }, CollisionMask = 1, CollideWithAreas = false };
        foreach (var child in GetParent().GetChildren())
        {
            if (child is Doors door) _doors.Add(door);
            if (child is QuestEntrance portal) _portals.Add(portal);
        }
        Waypoint = new Marker2D { Name = "CurrentRouteWaypoint" }; AddChild(Waypoint);
    }

    private static Vector2I ToCell(Vector2 world) => new(Mathf.FloorToInt(world.X / CellSize), Mathf.FloorToInt(world.Y / CellSize));

    public override void _PhysicsProcess(double delta)
    {
        // Bound startup work instead of issuing thousands of physics queries in one frame.
        for (int i = 0; i < 768 && _cursor < _pending.Count; i++, _cursor++) RefreshCell(_pending[_cursor]);
        _repath -= delta;
        if (IsReadyForRoutes && _doorsDirty && --_doorRefreshFrames <= 0)
        {
            _doorsDirty = false;
            foreach (var door in _doors)
            {
                var id = ToCell(door.GlobalPosition);
                for (int y = -6; y <= 6; y++) for (int x = -6; x <= 6; x++) RefreshCell(id + new Vector2I(x, y));
            }
            // A cached empty route must be rebuilt after the collision grid changes,
            // even if the player stands still beside the newly opened door.
            _dirty = true;
        }
    }

    private void RefreshCell(Vector2I id)
    {
        if (!_grid.IsInBoundsv(id) || !_floor.Contains(id)) return;
        _query.Transform = new Transform2D(0, _grid.GetPointPosition(id));
        _grid.SetPointSolid(id, GetWorld2D().DirectSpaceState.IntersectShape(_query, 1).Count > 0);
    }

    public void InvalidateRoute()
    {
        _dirty = true; _doorsDirty = true;
        // Doors disable their collider deferred; query the next physics snapshot.
        _doorRefreshFrames = 2;
    }

    private Vector2I FreeCell(Vector2 position)
    {
        var id = ToCell(position);
        for (int radius = 0; radius <= 8; radius++)
            for (int y = -radius; y <= radius; y++) for (int x = -radius; x <= radius; x++)
            {
                var next = id + new Vector2I(x, y);
                if (_grid.IsInBoundsv(next) && !_grid.IsPointSolid(next)) return next;
            }
        return new(-9999, -9999);
    }

    private Vector2[] PathBetween(Vector2 from, Vector2 to)
    {
        var start = FreeCell(from); var end = FreeCell(to);
        return _grid.IsInBoundsv(start) && _grid.IsInBoundsv(end) ? _grid.GetPointPath(start, end) : System.Array.Empty<Vector2>();
    }

    public bool SegmentClear(Vector2 a, Vector2 b)
    {
        var side = a.DirectionTo(b).Orthogonal() * 23;
        foreach (var offset in new[] { Vector2.Zero, side, -side })
            if (GetWorld2D().DirectSpaceState.IntersectRay(PhysicsRayQueryParameters2D.Create(a + offset, b + offset, 1)).Count > 0) return false;
        return true;
    }

    public Node2D Guide(Vector2 player, Node2D target)
    {
        HasRoute = false;
        if (!IsReadyForRoutes || !IsInstanceValid(target)) return null;
        var goal = target is QuestInteractable interaction ? interaction.InteractionPosition : target.GlobalPosition;
        if (_dirty || _repath <= 0 && (player.DistanceSquaredTo(_lastStart) > 4096 || goal.DistanceSquaredTo(_lastGoal) > 256))
        {
            _dirty = false; _repath = .5; _lastStart = player; _lastGoal = goal;
            _path = PathBetween(player, goal);
            // Different area: walk to the teleport whose destination can reach the goal.
            if (_path.Length == 0)
                foreach (var portal in _portals)
                {
                    if (!IsInstanceValid(portal) || !IsInstanceValid(portal.DestinationMarker)) continue;
                    if (PathBetween(portal.DestinationMarker.GlobalPosition, goal).Length == 0) continue;
                    var toPortal = PathBetween(player, portal.GlobalPosition);
                    if (toPortal.Length == 0) continue;
                    _path = toPortal; break;
                }
        }
        if (_path.Length == 0) return null;
        // Choose the furthest currently visible point along this connected path.
        int closest = 0; float distance = float.MaxValue;
        for (int i = 0; i < _path.Length; i++) { float d = player.DistanceSquaredTo(_path[i]); if (d < distance) { closest = i; distance = d; } }
        Vector2? next = null;
        for (int i = closest; i < _path.Length && i < closest + 40; i++)
        {
            if (!SegmentClear(player, _path[i])) break;
            next = _path[i];
        }
        if (next == null) return null;
        Waypoint.GlobalPosition = next.Value; HasRoute = true; return Waypoint;
    }
}
