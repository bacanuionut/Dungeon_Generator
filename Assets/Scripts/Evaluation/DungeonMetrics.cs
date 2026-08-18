using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

/// <summary>
/// Calculates quantitative measurements from a generated dungeon.
///
/// This class does not generate or modify the dungeon. It only analyses
/// an already generated result. Keeping metrics separate from generation
/// makes it easier to evaluate different dungeon configurations later.
/// </summary>
public static class DungeonMetrics
{
    /// <summary>
    /// Stores the measurements calculated for one generated dungeon.
    /// </summary>
    public class Result
    {
        public int Seed;

        public int RoomCount;
        public int ConnectionCount;
        public int CorridorCount;

        public int FloorCellCount;
        public int RoomCellCount;
        public int CorridorCellCount;

        public float AverageRoomArea;
        public int SmallestRoomArea;
        public int LargestRoomArea;

        public int TotalCorridorLength;
        public float AverageCorridorLength;

        public int StartToExitGraphDistance;
        public int ShortestPlayablePathLength;
    }


    /// <summary>
    /// Calculates all currently supported metrics for one dungeon.
    /// </summary>
    public static Result Calculate(
        int seed,
        List<Room> rooms,
        DungeonGraph graph,
        List<CorridorGenerator.Corridor> corridors,
        DungeonGrid grid,
        Vector2Int startPosition,
        Vector2Int exitPosition,
        int startToExitGraphDistance)
    {
        Result result = new Result();

        result.Seed = seed;

        // -------------------------
        // Basic generation counts
        // -------------------------

        result.RoomCount =
            rooms != null ? rooms.Count : 0;

        result.ConnectionCount =
            graph != null ? graph.Connections.Count : 0;

        result.CorridorCount =
            corridors != null ? corridors.Count : 0;

        if (grid != null)
        {
            result.FloorCellCount =
                grid.FloorCellCount;

            result.RoomCellCount =
                grid.RoomCellCount;

            result.CorridorCellCount =
                grid.CorridorCellCount;
        }


        // -------------------------
        // Room measurements
        // -------------------------

        CalculateRoomMetrics(
            rooms,
            result
        );


        // -------------------------
        // Corridor measurements
        // -------------------------

        CalculateCorridorMetrics(
            corridors,
            result
        );


        // -------------------------
        // Gameplay measurements
        // -------------------------

        result.StartToExitGraphDistance =
            startToExitGraphDistance;

        result.ShortestPlayablePathLength =
            CalculateShortestPathLength(
                grid,
                startPosition,
                exitPosition
            );

        return result;
    }


    /// <summary>
    /// Calculates average, minimum and maximum room area.
    /// </summary>
    private static void CalculateRoomMetrics(
        List<Room> rooms,
        Result result)
    {
        if (rooms == null || rooms.Count == 0)
        {
            result.AverageRoomArea = 0f;
            result.SmallestRoomArea = 0;
            result.LargestRoomArea = 0;

            return;
        }

        int totalArea = 0;

        int smallestArea = int.MaxValue;
        int largestArea = int.MinValue;

        foreach (Room room in rooms)
        {
            if (room == null)
            {
                continue;
            }

            int area =
                room.Bounds.width *
                room.Bounds.height;

            totalArea += area;

            smallestArea =
                Mathf.Min(
                    smallestArea,
                    area
                );

            largestArea =
                Mathf.Max(
                    largestArea,
                    area
                );
        }

        result.AverageRoomArea =
            (float)totalArea / rooms.Count;

        result.SmallestRoomArea =
            smallestArea;

        result.LargestRoomArea =
            largestArea;
    }


    /// <summary>
    /// Calculates total and average physical corridor length.
    ///
    /// Corridor length is measured using the number of grid cells
    /// stored by each generated corridor.
    /// </summary>
    private static void CalculateCorridorMetrics(
        List<CorridorGenerator.Corridor> corridors,
        Result result)
    {
        if (corridors == null || corridors.Count == 0)
        {
            result.TotalCorridorLength = 0;
            result.AverageCorridorLength = 0f;

            return;
        }

        int totalLength = 0;

        foreach (CorridorGenerator.Corridor corridor
                 in corridors)
        {
            if (corridor == null)
            {
                continue;
            }

            totalLength +=
                corridor.Length;
        }

        result.TotalCorridorLength =
            totalLength;

        result.AverageCorridorLength =
            (float)totalLength /
            corridors.Count;
    }


    /// <summary>
    /// Calculates the shortest walkable grid path from the exact
    /// player spawn coordinate to the exact exit coordinate.
    ///
    /// Breadth-first search guarantees the shortest path when every
    /// movement between neighbouring grid cells has equal cost.
    ///
    /// Returns -1 if no path exists.
    /// </summary>
    private static int CalculateShortestPathLength(
        DungeonGrid grid,
        Vector2Int start,
        Vector2Int destination)
    {
        if (grid == null)
        {
            return -1;
        }

        if (!grid.IsWalkable(start) ||
            !grid.IsWalkable(destination))
        {
            return -1;
        }

        Queue<Vector2Int> queue =
            new Queue<Vector2Int>();

        Dictionary<Vector2Int, int> distances =
            new Dictionary<Vector2Int, int>();

        Vector2Int[] directions =
        {
            Vector2Int.up,
            Vector2Int.down,
            Vector2Int.left,
            Vector2Int.right
        };

        queue.Enqueue(start);
        distances[start] = 0;

        while (queue.Count > 0)
        {
            Vector2Int current =
                queue.Dequeue();

            int currentDistance =
                distances[current];

            if (current == destination)
            {
                return currentDistance;
            }

            foreach (Vector2Int direction
                     in directions)
            {
                Vector2Int neighbour =
                    current + direction;

                if (!grid.IsWalkable(neighbour))
                {
                    continue;
                }

                if (distances.ContainsKey(neighbour))
                {
                    continue;
                }

                distances[neighbour] =
                    currentDistance + 1;

                queue.Enqueue(neighbour);
            }
        }

        return -1;
    }


    /// <summary>
    /// Produces a readable Console summary of one metrics result.
    /// </summary>
    public static void LogResult(Result result)
    {
        if (result == null)
        {
            return;
        }

        UnityEngine.Debug.Log(
            "DUNGEON METRICS\n" +
            $"Seed: {result.Seed}\n" +
            $"Rooms: {result.RoomCount}\n" +
            $"Connections: {result.ConnectionCount}\n" +
            $"Corridors: {result.CorridorCount}\n" +
            $"Floor cells: {result.FloorCellCount}\n" +
            $"Room cells: {result.RoomCellCount}\n" +
            $"Corridor cells: {result.CorridorCellCount}\n" +
            $"Average room area: {result.AverageRoomArea:F2}\n" +
            $"Smallest room area: {result.SmallestRoomArea}\n" +
            $"Largest room area: {result.LargestRoomArea}\n" +
            $"Total corridor length: {result.TotalCorridorLength}\n" +
            $"Average corridor length: {result.AverageCorridorLength:F2}\n" +
            $"Start-to-exit graph distance: {result.StartToExitGraphDistance}\n" +
            $"Shortest playable path: {result.ShortestPlayablePathLength}"
        );
    }
}