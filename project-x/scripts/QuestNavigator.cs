using Godot;
using System.Collections.Generic;

// Bake collision-aware floor cells once in small batches. Replan only when needed.
// The arrow follows a reachable waypoint, never a straight line through a wall.
public partial class QuestNavigator : Node2D
{
    private readonly AStarGrid2D _grid = new();
    private readonly HashSet<Vector2I> _floor = new();
    private readonly List<Vector2I> _pending = new();
    private readonly List<Doors> _doors = new();
    private PhysicsShapeQueryParameters2D _query;
    private TileMapLayer _ground;
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
        _ground = GetNode<TileMapLayer>("../MainMap/groundbase");
        foreach (var cell in _ground.GetUsedCells()) { _floor.Add(cell); _pending.Add(cell); }
        _grid.Region = _ground.GetUsedRect(); _grid.CellSize = new(32, 32); _grid.Offset = _ground.GlobalPosition + new Vector2(16, 16);
        _grid.DiagonalMode = AStarGrid2D.DiagonalModeEnum.Never; _grid.Update(); _grid.FillSolidRegion(_grid.Region);
        _query = new PhysicsShapeQueryParameters2D { Shape = new CircleShape2D { Radius = 23 }, CollisionMask = 1, CollideWithAreas = false };
        foreach (var child in GetParent().GetChildren()) if (child is Doors door) _doors.Add(door);
        Waypoint = new Marker2D { Name = "CurrentRouteWaypoint" }; AddChild(Waypoint);
    }
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
                var id = _ground.LocalToMap(_ground.ToLocal(door.GlobalPosition));
                for (int y = -5; y <= 5; y++) for (int x = -3; x <= 3; x++) RefreshCell(id + new Vector2I(x, y));
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
        var id = _ground.LocalToMap(_ground.ToLocal(position));
        for (int radius = 0; radius <= 8; radius++)
            for (int y = -radius; y <= radius; y++) for (int x = -radius; x <= radius; x++)
            {
                var next = id + new Vector2I(x, y);
                if (_grid.IsInBoundsv(next) && !_grid.IsPointSolid(next)) return next;
            }
        return new(-9999, -9999);
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
            var start = FreeCell(player); var end = FreeCell(goal);
            _path = _grid.IsInBoundsv(start) && _grid.IsInBoundsv(end) ? _grid.GetPointPath(start, end) : System.Array.Empty<Vector2>();
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
