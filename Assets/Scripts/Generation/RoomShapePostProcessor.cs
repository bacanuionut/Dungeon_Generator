using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Applies cellular-automata-inspired post-processing around the
/// outside of BSP-generated rooms.
///
/// The original rectangular room is never removed. The algorithm only
/// adds new floor around its edges, preserving the BSP structure and
/// existing connectivity guarantees.
/// </summary>
public static class RoomShapePostProcessor
{
    /// <summary>
    /// Generates organic floor extensions around all BSP rooms.
    /// </summary>
    public static HashSet<Vector2Int> GenerateOrganicRoomCells(
        IReadOnlyList<Room> rooms,
        DungeonGrid grid,
        int dungeonWidth,
        int dungeonHeight,
        System.Random random,
        int growthRadius,
        float initialGrowthChance,
        int smoothingIterations)
    {
        HashSet<Vector2Int> allAddedCells =
            new HashSet<Vector2Int>();


        if (rooms == null ||
            grid == null ||
            random == null ||
            growthRadius <= 0)
        {
            return allAddedCells;
        }


        foreach (Room room in rooms)
        {
            if (room == null ||
                room.ParentPartition == null)
            {
                continue;
            }


            HashSet<Vector2Int> roomAdditions =
                GenerateForRoom(
                    room,
                    grid,
                    dungeonWidth,
                    dungeonHeight,
                    random,
                    growthRadius,
                    initialGrowthChance,
                    smoothingIterations
                );


            foreach (Vector2Int cell in roomAdditions)
            {
                allAddedCells.Add(cell);
            }
        }


        return allAddedCells;
    }


    /// <summary>
    /// Creates and smooths a candidate region surrounding one room.
    /// </summary>
    private static HashSet<Vector2Int> GenerateForRoom(
        Room room,
        DungeonGrid grid,
        int dungeonWidth,
        int dungeonHeight,
        System.Random random,
        int growthRadius,
        float initialGrowthChance,
        int smoothingIterations)
    {
        RectInt roomBounds =
            room.Bounds;

        RectInt partitionBounds =
            room.ParentPartition.Bounds;


        // Grow only within the room's own BSP leaf partition.
        // This prevents the post-processing stage from ignoring
        // the spatial structure established by BSP.
        int minimumX =
            Mathf.Max(
                roomBounds.xMin - growthRadius,
                partitionBounds.xMin,
                0
            );

        int maximumX =
            Mathf.Min(
                roomBounds.xMax + growthRadius,
                partitionBounds.xMax,
                dungeonWidth
            );

        int minimumY =
            Mathf.Max(
                roomBounds.yMin - growthRadius,
                partitionBounds.yMin,
                0
            );

        int maximumY =
            Mathf.Min(
                roomBounds.yMax + growthRadius,
                partitionBounds.yMax,
                dungeonHeight
            );


        HashSet<Vector2Int> candidates =
            new HashSet<Vector2Int>();

        HashSet<Vector2Int> activeCells =
            new HashSet<Vector2Int>();


        // Randomly initialise cells surrounding the guaranteed
        // rectangular room core.
        for (int x = minimumX; x < maximumX; x++)
        {
            for (int y = minimumY; y < maximumY; y++)
            {
                Vector2Int cell =
                    new Vector2Int(x, y);


                // The original BSP room is never modified.
                if (roomBounds.Contains(cell))
                    continue;


                candidates.Add(cell);


                if (random.NextDouble() <
                    initialGrowthChance)
                {
                    activeCells.Add(cell);
                }
            }
        }


        // Apply repeated local neighbourhood rules.
        for (int iteration = 0;
             iteration < smoothingIterations;
             iteration++)
        {
            HashSet<Vector2Int> nextState =
                new HashSet<Vector2Int>();


            foreach (Vector2Int cell in candidates)
            {
                int neighbouringFloor =
                    CountFloorNeighbours(
                        cell,
                        roomBounds,
                        activeCells
                    );


                bool currentlyActive =
                    activeCells.Contains(cell);


                // Dense neighbourhoods become floor.
                if (neighbouringFloor >= 5)
                {
                    nextState.Add(cell);
                }

                // Sparse neighbourhoods become wall.
                else if (neighbouringFloor <= 2)
                {
                    // Deliberately left inactive.
                }

                // At an intermediate neighbour count, retain the
                // previous state rather than forcing a change.
                else if (currentlyActive)
                {
                    nextState.Add(cell);
                }
            }


            activeCells =
                nextState;
        }


        // Remove any isolated patches that are not actually connected
        // to the original BSP room.
        activeCells =
            KeepCellsConnectedToRoom(
                roomBounds,
                activeCells
            );


        HashSet<Vector2Int> newFloorCells =
            new HashSet<Vector2Int>();


        foreach (Vector2Int cell in activeCells)
        {
            // Existing corridor/floor cells should not be counted as
            // newly generated organic room floor.
            if (!grid.IsWalkable(cell))
            {
                newFloorCells.Add(cell);
            }
        }


        return newFloorCells;
    }


    /// <summary>
    /// Counts the eight surrounding cells using a Moore
    /// neighbourhood.
    ///
    /// Original room cells are permanently considered floor.
    /// </summary>
    private static int CountFloorNeighbours(
        Vector2Int cell,
        RectInt roomBounds,
        HashSet<Vector2Int> activeCells)
    {
        int count = 0;


        for (int xOffset = -1;
             xOffset <= 1;
             xOffset++)
        {
            for (int yOffset = -1;
                 yOffset <= 1;
                 yOffset++)
            {
                if (xOffset == 0 &&
                    yOffset == 0)
                {
                    continue;
                }


                Vector2Int neighbour =
                    new Vector2Int(
                        cell.x + xOffset,
                        cell.y + yOffset
                    );


                if (roomBounds.Contains(neighbour) ||
                    activeCells.Contains(neighbour))
                {
                    count++;
                }
            }
        }


        return count;
    }


    /// <summary>
    /// Removes random isolated floor patches and keeps only organic
    /// cells that are connected to the original room.
    /// </summary>
    private static HashSet<Vector2Int> KeepCellsConnectedToRoom(
        RectInt roomBounds,
        HashSet<Vector2Int> activeCells)
    {
        HashSet<Vector2Int> connected =
            new HashSet<Vector2Int>();

        Queue<Vector2Int> frontier =
            new Queue<Vector2Int>();


        Vector2Int[] directions =
        {
            Vector2Int.up,
            Vector2Int.down,
            Vector2Int.left,
            Vector2Int.right
        };


        // Find generated cells directly touching the rectangular room.
        foreach (Vector2Int cell in activeCells)
        {
            foreach (Vector2Int direction in directions)
            {
                if (roomBounds.Contains(
                        cell + direction))
                {
                    connected.Add(cell);
                    frontier.Enqueue(cell);
                    break;
                }
            }
        }


        // Grow outward only through connected generated floor.
        while (frontier.Count > 0)
        {
            Vector2Int current =
                frontier.Dequeue();


            foreach (Vector2Int direction in directions)
            {
                Vector2Int neighbour =
                    current + direction;


                if (!activeCells.Contains(neighbour))
                    continue;


                if (connected.Contains(neighbour))
                    continue;


                connected.Add(neighbour);
                frontier.Enqueue(neighbour);
            }
        }


        return connected;
    }
}