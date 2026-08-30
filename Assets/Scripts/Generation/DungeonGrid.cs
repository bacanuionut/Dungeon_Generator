using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Creates a grid representation of the generated dungeon.
///
/// The BSP system, rooms and corridors describe how the dungeon
/// was generated. DungeonGrid converts those results into a simple
/// collection of walkable floor cells.
///
/// Keeping this representation separate from rendering means the
/// dungeon data can later be used by other systems such as:
/// - rendering
/// - player movement
/// - pathfinding
/// - gameplay placement
/// - evaluation
/// </summary>
public class DungeonGrid
{

    // Extra floor cells created around BSP room edges by the
    // cellular-automata post-processing stage.
    private HashSet<Vector2Int> organicRoomCells =
        new HashSet<Vector2Int>();

    // Every coordinate that forms part of the walkable dungeon.
    // HashSet prevents duplicate cells when rooms and corridors overlap.
    private HashSet<Vector2Int> floorCells =
        new HashSet<Vector2Int>();

    // Floor cells that specifically came from rooms.
    private HashSet<Vector2Int> roomCells =
        new HashSet<Vector2Int>();

    // Floor cells that specifically came from corridors.
    private HashSet<Vector2Int> corridorCells =
        new HashSet<Vector2Int>();


    public IReadOnlyCollection<Vector2Int> OrganicRoomCells =>
    organicRoomCells;

    public int OrganicRoomCellCount =>
        organicRoomCells.Count;

    /// <summary>
    /// All walkable cells in the dungeon.
    /// </summary>
    public IReadOnlyCollection<Vector2Int> FloorCells
        => floorCells;


    /// <summary>
    /// Cells belonging to generated rooms.
    /// </summary>
    public IReadOnlyCollection<Vector2Int> RoomCells
        => roomCells;


    /// <summary>
    /// Cells used by generated corridors.
    /// </summary>
    public IReadOnlyCollection<Vector2Int> CorridorCells
        => corridorCells;


    /// <summary>
    /// Number of unique walkable cells in the dungeon.
    /// </summary>
    public int FloorCellCount => floorCells.Count;


    /// <summary>
    /// Number of unique cells occupied by rooms.
    /// </summary>
    public int RoomCellCount => roomCells.Count;


    /// <summary>
    /// Number of unique cells occupied by corridors.
    ///
    /// Some corridor cells may also be inside rooms, so:
    ///
    /// RoomCellCount + CorridorCellCount
    ///
    /// is not necessarily equal to FloorCellCount.
    /// </summary>
    public int CorridorCellCount => corridorCells.Count;


    /// <summary>
    /// Clears any previously generated grid data and rebuilds the
    /// dungeon from the supplied rooms and corridors.
    /// </summary>
    public void Build(
        List<Room> rooms,
        List<CorridorGenerator.Corridor> corridors)
    {
        Clear();

        AddRooms(rooms);
        AddCorridors(corridors);
    }


    /// <summary>
    /// Removes all existing grid data.
    /// </summary>
    public void Clear()
    {
        floorCells.Clear();
        roomCells.Clear();
        corridorCells.Clear();
        organicRoomCells.Clear();
    }


    /// <summary>
    /// Converts every rectangular room into individual grid cells.
    /// </summary>
    private void AddRooms(List<Room> rooms)
    {
        if (rooms == null)
        {
            return;
        }

        foreach (Room room in rooms)
        {
            if (room == null)
            {
                continue;
            }

            RectInt bounds = room.Bounds;

            // RectInt uses xMin/yMin as inclusive boundaries
            // and xMax/yMax as exclusive boundaries.
            for (int x = bounds.xMin; x < bounds.xMax; x++)
            {
                for (int y = bounds.yMin; y < bounds.yMax; y++)
                {
                    Vector2Int cell =
                        new Vector2Int(x, y);

                    roomCells.Add(cell);
                    floorCells.Add(cell);
                }
            }
        }
    }


    /// <summary>
    /// Adds every physical corridor cell to the dungeon grid.
    /// </summary>
    private void AddCorridors(
        List<CorridorGenerator.Corridor> corridors)
    {
        if (corridors == null)
        {
            return;
        }

        foreach (CorridorGenerator.Corridor corridor in corridors)
        {
            if (corridor == null || corridor.Cells == null)
            {
                continue;
            }

            foreach (Vector2Int cell in corridor.Cells)
            {
                corridorCells.Add(cell);
                floorCells.Add(cell);
            }
        }
    }


    /// <summary>
    /// Returns true when a coordinate is part of the walkable dungeon.
    ///
    /// This gives gameplay systems a simple way to ask whether a player,
    /// enemy or other object can occupy a particular grid position.
    /// </summary>
    public bool IsWalkable(Vector2Int position)
    {
        return floorCells.Contains(position);
    }

    /// <summary>
    /// Adds extra floor cells generated around room boundaries.
    ///
    /// These cells become part of the walkable dungeon but are kept
    /// separately from the original rectangular BSP room cells.
    /// </summary>
    public void AddOrganicRoomCells(
        IEnumerable<Vector2Int> cells)
    {
        if (cells == null)
            return;

        foreach (Vector2Int cell in cells)
        {
            // Only count genuinely new floor created by the
            // post-processing stage.
            if (!floorCells.Contains(cell))
            {
                organicRoomCells.Add(cell);
                floorCells.Add(cell);
            }
        }
    }

    /// <summary>
    /// Returns true when the supplied cell was added by the organic
    /// room-shaping post-processing stage.
    /// </summary>
    public bool IsOrganicRoomCell(
        Vector2Int cell)
    {
        return organicRoomCells.Contains(
            cell
        );
    }
}