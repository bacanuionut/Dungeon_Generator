using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Generates physical grid-based corridor paths between connected rooms.
///
/// The logical decision about which rooms connect is handled by
/// DungeonGraph. This class is only responsible for turning one of
/// those connections into a physical path.
/// </summary>
public class CorridorGenerator
{
    /// <summary>
    /// Represents one generated corridor.
    ///
    /// Each corridor stores the rooms it connects and every integer
    /// grid position occupied by its path.
    /// </summary>
    public class Corridor
    {
        public Room RoomA { get; private set; }
        public Room RoomB { get; private set; }

        public List<Vector2Int> Cells { get; private set; }

        public Corridor(
            Room roomA,
            Room roomB,
            List<Vector2Int> cells)
        {
            RoomA = roomA;
            RoomB = roomB;
            Cells = cells;
        }

        /// <summary>
        /// Number of grid cells making up this corridor.
        /// Useful later when collecting dungeon metrics.
        /// </summary>
        public int Length => Cells.Count;
    }

    /// <summary>
    /// Generates corridors for every logical edge in the dungeon graph.
    /// </summary>
    public List<Corridor> GenerateCorridors(
        DungeonGraph graph,
        System.Random random)
    {
        List<Corridor> corridors = new List<Corridor>();

        if (graph == null)
        {
            return corridors;
        }

        foreach (DungeonGraph.RoomConnection connection
                 in graph.Connections)
        {
            Corridor corridor = GenerateCorridor(
                connection.RoomA,
                connection.RoomB,
                random
            );

            corridors.Add(corridor);
        }

        return corridors;
    }

    /// <summary>
    /// Creates an orthogonal L-shaped path between two room centres.
    ///
    /// The path can travel horizontally first or vertically first.
    /// This choice is random but deterministic because the same seeded
    /// random number generator used by the dungeon is supplied here.
    /// </summary>
    private Corridor GenerateCorridor(
        Room roomA,
        Room roomB,
        System.Random random)
    {
        Vector2Int start = roomA.Centre;
        Vector2Int end = roomB.Centre;

        List<Vector2Int> cells = new List<Vector2Int>();

        bool horizontalFirst = random.Next(0, 2) == 0;

        if (horizontalFirst)
        {
            // First travel along the X axis.
            AddHorizontalSegment(
                cells,
                start.x,
                end.x,
                start.y
            );

            // Then travel along the Y axis from the corner.
            AddVerticalSegment(
                cells,
                start.y,
                end.y,
                end.x
            );
        }
        else
        {
            // First travel along the Y axis.
            AddVerticalSegment(
                cells,
                start.y,
                end.y,
                start.x
            );

            // Then travel along the X axis from the corner.
            AddHorizontalSegment(
                cells,
                start.x,
                end.x,
                end.y
            );
        }

        // The corner belongs to both segments, so remove duplicate cells
        // while preserving the order in which the path was generated.
        cells = RemoveDuplicateCells(cells);
       
        return new Corridor(
            roomA,
            roomB,
            cells
        );
    }

    /// <summary>
    /// Adds every grid cell along a horizontal line.
    /// Works whether the corridor travels left or right.
    /// </summary>
    private void AddHorizontalSegment(
        List<Vector2Int> cells,
        int startX,
        int endX,
        int y)
    {
        int direction = startX <= endX ? 1 : -1;

        for (
            int x = startX;
            x != endX + direction;
            x += direction)
        {
            cells.Add(new Vector2Int(x, y));
        }
    }

    /// <summary>
    /// Adds every grid cell along a vertical line.
    /// Works whether the corridor travels up or down.
    /// </summary>
    private void AddVerticalSegment(
        List<Vector2Int> cells,
        int startY,
        int endY,
        int x)
    {
        int direction = startY <= endY ? 1 : -1;

        for (
            int y = startY;
            y != endY + direction;
            y += direction)
        {
            cells.Add(new Vector2Int(x, y));
        }
    }

    /// <summary>
    /// Removes cells that occur more than once in a corridor.
    ///
    /// This normally occurs at the corner where the horizontal and
    /// vertical corridor segments meet.
    /// </summary>
    private List<Vector2Int> RemoveDuplicateCells(
        List<Vector2Int> cells)
    {
        HashSet<Vector2Int> seen =
            new HashSet<Vector2Int>();

        List<Vector2Int> result =
            new List<Vector2Int>();

        foreach (Vector2Int cell in cells)
        {
            if (seen.Add(cell))
            {
                result.Add(cell);
            }
        }

        return result;
    }
}