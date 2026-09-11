using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Creates a grid representation of the generated dungeon.
///
/// The BSP system, rooms and corridors describe how the dungeon
/// was generated. DungeonGrid converts those results into a simple
/// collection of walkable floor cells.
///
/// Keeping this representation separate from rendering allows the
/// dungeon data to be used by systems such as:
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

    // Floor created during gameplay rather than during initial generation.
    // Used by both the player's Shaper and the Warden.
    private readonly HashSet<Vector2Int> dynamicFloorCells =
        new HashSet<Vector2Int>();


    // Walkable floor cells occupied by solid environmental props.
    //
    // These are kept separate from floorCells so rendering, wall topology,
    // fog-of-war and structural evaluation still see the original dungeon
    // geometry. Movement/pathfinding can use IsNavigable() when props should
    // act as obstacles.
    private readonly HashSet<Vector2Int> navigationBlockedCells =
        new HashSet<Vector2Int>();

    // Walk-over environmental details do not block movement, but collectibles
    // should not spawn directly on top of them. Keeping these cells separate
    // preserves navigation while giving content placement a clean exclusion map.
    private readonly HashSet<Vector2Int> collectibleExclusionCells =
        new HashSet<Vector2Int>();


    public IReadOnlyCollection<Vector2Int> DynamicFloorCells =>
        dynamicFloorCells;

    public int DynamicFloorCellCount =>
        dynamicFloorCells.Count;


    public IReadOnlyCollection<Vector2Int> NavigationBlockedCells =>
        navigationBlockedCells;

    public int NavigationBlockedCellCount =>
        navigationBlockedCells.Count;

    public IReadOnlyCollection<Vector2Int> CollectibleExclusionCells =>
        collectibleExclusionCells;

    public int CollectibleExclusionCellCount =>
        collectibleExclusionCells.Count;


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
        dynamicFloorCells.Clear();
        navigationBlockedCells.Clear();
        collectibleExclusionCells.Clear();
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
    /// Returns true when a coordinate is part of the generated floor geometry.
    ///
    /// Environmental props do not change this result. Systems that need to
    /// account for solid prop blockers should use IsNavigable().
    /// </summary>
    public bool IsWalkable(Vector2Int position)
    {
        return floorCells.Contains(position);
    }


    /// <summary>
    /// Returns true when a coordinate is floor and is not currently occupied
    /// by a solid environmental prop.
    ///
    /// IsWalkable() deliberately remains the geometric floor test used by
    /// rendering, visibility and structural validation.
    /// </summary>
    public bool IsNavigable(Vector2Int position)
    {
        return floorCells.Contains(position) &&
               !navigationBlockedCells.Contains(position);
    }


    /// <summary>
    /// Returns true when a solid environmental prop currently reserves
    /// this floor cell for navigation.
    /// </summary>
    public bool IsNavigationBlocked(Vector2Int position)
    {
        return navigationBlockedCells.Contains(position);
    }


    /// <summary>
    /// Returns true when a collectible can be placed without occupying or
    /// visually crowding a solid environmental prop.
    ///
    /// clearance 0 checks only the candidate cell. A clearance of 1 also
    /// checks the surrounding eight cells, which prevents pickups from
    /// appearing underneath overhanging crate, table or rubble artwork.
    /// </summary>
    public bool IsClearForCollectible(
        Vector2Int position,
        int clearance = 1)
    {
        if (!IsNavigable(position))
        {
            return false;
        }

        int safeClearance =
            Mathf.Max(
                0,
                clearance
            );

        for (int x = -safeClearance;
             x <= safeClearance;
             x++)
        {
            for (int y = -safeClearance;
                 y <= safeClearance;
                 y++)
            {
                Vector2Int check =
                    position +
                    new Vector2Int(
                        x,
                        y
                    );

                if (navigationBlockedCells.Contains(check) ||
                    collectibleExclusionCells.Contains(check))
                {
                    return false;
                }
            }
        }

        return true;
    }


    /// <summary>
    /// Marks a floor cell as visually occupied by walk-over environmental
    /// decoration. This does not affect movement or pathfinding.
    /// </summary>
    public bool AddCollectibleExclusion(Vector2Int cell)
    {
        if (!floorCells.Contains(cell))
        {
            return false;
        }

        return collectibleExclusionCells.Add(cell);
    }


    /// <summary>
    /// Adds several walk-over detail cells to the collectible exclusion map.
    /// </summary>
    public int AddCollectibleExclusions(IEnumerable<Vector2Int> cells)
    {
        if (cells == null)
        {
            return 0;
        }

        int added = 0;

        foreach (Vector2Int cell in cells)
        {
            if (AddCollectibleExclusion(cell))
            {
                added++;
            }
        }

        return added;
    }


    /// <summary>
    /// Clears only walk-over detail exclusions. Navigation blockers are kept.
    /// </summary>
    public void ClearCollectibleExclusions()
    {
        collectibleExclusionCells.Clear();
    }


    /// <summary>
    /// Reserves one existing floor cell for a solid environmental prop.
    /// </summary>
    public bool AddNavigationBlocker(Vector2Int cell)
    {
        if (!floorCells.Contains(cell))
        {
            return false;
        }

        return navigationBlockedCells.Add(cell);
    }


    /// <summary>
    /// Reserves several existing floor cells for solid environmental props.
    /// Returns how many new cells were added.
    /// </summary>
    public int AddNavigationBlockers(IEnumerable<Vector2Int> cells)
    {
        if (cells == null)
        {
            return 0;
        }

        int added = 0;

        foreach (Vector2Int cell in cells)
        {
            if (AddNavigationBlocker(cell))
            {
                added++;
            }
        }

        return added;
    }


    /// <summary>
    /// Removes one environmental navigation blocker without changing the
    /// underlying floor geometry. Used when the Shaper destroys a prop.
    /// </summary>
    public bool RemoveNavigationBlocker(Vector2Int cell)
    {
        return navigationBlockedCells.Remove(cell);
    }


    /// <summary>
    /// Removes several environmental navigation blockers.
    /// Returns how many blocker cells were removed.
    /// </summary>
    public int RemoveNavigationBlockers(IEnumerable<Vector2Int> cells)
    {
        if (cells == null)
        {
            return 0;
        }

        int removed = 0;

        foreach (Vector2Int cell in cells)
        {
            if (RemoveNavigationBlocker(cell))
            {
                removed++;
            }
        }

        return removed;
    }


    /// <summary>
    /// Clears only environmental navigation blockers without changing
    /// the generated dungeon floor itself.
    /// </summary>
    public void ClearNavigationBlockers()
    {
        navigationBlockedCells.Clear();
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
    /// Returns true when the cell belongs to an original BSP room.
    /// </summary>
    public bool IsRoomCell(
        Vector2Int cell)
    {
        return roomCells.Contains(
            cell
        );
    }


    /// <summary>
    /// Returns true when the cell belongs to an originally generated
    /// corridor.
    /// </summary>
    public bool IsCorridorCell(
        Vector2Int cell)
    {
        return corridorCells.Contains(
            cell
        );
    }


    /// <summary>
    /// Returns true when the cell was created dynamically during play.
    /// </summary>
    public bool IsDynamicFloorCell(
        Vector2Int cell)
    {
        return dynamicFloorCells.Contains(
            cell
        );
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

    /// <summary>
    /// Adds new walkable terrain while the dungeon is being played.
    ///
    /// Dynamic floor is kept separate from the original BSP rooms,
    /// corridors and CA room shaping so runtime terrain changes can be
    /// measured independently.
    /// </summary>
    public bool AddDynamicFloorCell(
        Vector2Int cell)
    {
        if (floorCells.Contains(cell))
            return false;


        dynamicFloorCells.Add(cell);

        floorCells.Add(cell);


        return true;
    }


    /// <summary>
    /// Adds several dynamically created floor cells.
    /// Returns how many genuinely new cells were added.
    /// </summary>
    public int AddDynamicFloorCells(
        IEnumerable<Vector2Int> cells)
    {
        int added = 0;


        foreach (Vector2Int cell in cells)
        {
            if (AddDynamicFloorCell(cell))
            {
                added++;
            }
        }


        return added;
    }
}