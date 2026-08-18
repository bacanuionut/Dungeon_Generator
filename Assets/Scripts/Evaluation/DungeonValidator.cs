using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Contains validation methods for analysing generated dungeons.
///
/// Validation is kept separate from generation so that the generator
/// creates the dungeon while this class checks properties of the result.
/// </summary>
public static class DungeonValidator
{
    /// <summary>
    /// Checks whether every generated room can be reached through
    /// the logical connections in the dungeon graph.
    ///
    /// A breadth-first search (BFS) starts from the first room and
    /// follows graph connections until no unvisited connected rooms remain.
    /// If every room was visited, the graph is fully connected.
    /// </summary>
    public static bool IsFullyConnected(
        List<Room> rooms,
        DungeonGraph graph)
    {
        // An empty dungeon is not considered a valid connected dungeon.
        if (rooms == null || rooms.Count == 0 || graph == null)
        {
            return false;
        }

        HashSet<Room> visited = new HashSet<Room>();
        Queue<Room> queue = new Queue<Room>();

        // Start the traversal from the first generated room.
        Room startRoom = rooms[0];

        visited.Add(startRoom);
        queue.Enqueue(startRoom);

        while (queue.Count > 0)
        {
            Room currentRoom = queue.Dequeue();

            // Find every graph edge involving the current room.
            foreach (DungeonGraph.RoomConnection connection
                     in graph.Connections)
            {
                Room neighbour = null;

                if (connection.RoomA == currentRoom)
                {
                    neighbour = connection.RoomB;
                }
                else if (connection.RoomB == currentRoom)
                {
                    neighbour = connection.RoomA;
                }

                // Ignore connections unrelated to the current room
                // and rooms that have already been visited.
                if (neighbour != null && !visited.Contains(neighbour))
                {
                    visited.Add(neighbour);
                    queue.Enqueue(neighbour);
                }
            }
        }

        // Every room must have been reached from the starting room.
        return visited.Count == rooms.Count;
    }

    /// <summary>
    /// Validates the physical corridor paths generated from the dungeon graph.
    ///
    /// Checks that:
    /// 1. One physical corridor exists for every logical graph connection.
    /// 2. Every corridor contains cells.
    /// 3. Each corridor begins inside its first room.
    /// 4. Each corridor ends inside its second room.
    /// 5. Every step in the path moves exactly one grid cell horizontally
    ///    or vertically.
    /// </summary>
    public static bool ValidateCorridors(
        List<CorridorGenerator.Corridor> corridors,
        DungeonGraph graph)
    {
        if (corridors == null || graph == null)
        {
            return false;
        }

        // Every logical connection should have one physical corridor.
        if (corridors.Count != graph.Connections.Count)
        {
            return false;
        }

        foreach (CorridorGenerator.Corridor corridor in corridors)
        {
            if (corridor == null ||
                corridor.Cells == null ||
                corridor.Cells.Count == 0)
            {
                return false;
            }

            Vector2Int firstCell = corridor.Cells[0];
            Vector2Int lastCell =
                corridor.Cells[corridor.Cells.Count - 1];

            // Corridor must start inside the first connected room.
            if (!corridor.RoomA.Contains(firstCell))
            {
                return false;
            }

            // Corridor must finish inside the second connected room.
            if (!corridor.RoomB.Contains(lastCell))
            {
                return false;
            }

            // Check every consecutive pair of cells.
            for (int i = 1; i < corridor.Cells.Count; i++)
            {
                Vector2Int previous = corridor.Cells[i - 1];
                Vector2Int current = corridor.Cells[i];

                int xDifference =
                    Mathf.Abs(current.x - previous.x);

                int yDifference =
                    Mathf.Abs(current.y - previous.y);

                int manhattanDistance =
                    xDifference + yDifference;

                // Exactly 1 means one horizontal or vertical grid step.
                if (manhattanDistance != 1)
                {
                    return false;
                }
            }
        }

        return true;
    }

    /// <summary>
    /// Validates the final unified dungeon grid.
    ///
    /// Every cell belonging to a generated room and every cell belonging
    /// to a generated corridor must exist in the final walkable floor set.
    /// This checks that no generated geometry was lost when the separate
    /// room and corridor data was combined into DungeonGrid.
    /// </summary>
    public static bool ValidateDungeonGrid(
        DungeonGrid grid,
        List<Room> rooms,
        List<CorridorGenerator.Corridor> corridors)
    {
        if (grid == null || rooms == null || corridors == null)
        {
            return false;
        }

        if (grid.FloorCellCount == 0)
        {
            return false;
        }

        // Check every cell belonging to every generated room.
        foreach (Room room in rooms)
        {
            if (room == null)
            {
                return false;
            }

            RectInt bounds = room.Bounds;

            for (int x = bounds.xMin; x < bounds.xMax; x++)
            {
                for (int y = bounds.yMin; y < bounds.yMax; y++)
                {
                    Vector2Int cell = new Vector2Int(x, y);

                    if (!grid.IsWalkable(cell))
                    {
                        return false;
                    }
                }
            }
        }

        // Check every generated corridor cell.
        foreach (CorridorGenerator.Corridor corridor in corridors)
        {
            if (corridor == null || corridor.Cells == null)
            {
                return false;
            }

            foreach (Vector2Int cell in corridor.Cells)
            {
                if (!grid.IsWalkable(cell))
                {
                    return false;
                }
            }
        }

        return true;
    }
}