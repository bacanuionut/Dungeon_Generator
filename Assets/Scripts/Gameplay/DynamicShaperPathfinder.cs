using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Searches through solid dungeon space in the direction the player
/// is facing.
///
/// Unlike normal pathfinding, solid cells are traversable here because
/// they represent terrain which the Shaper may convert into floor.
///
/// The search is deliberately constrained forward so activating the
/// Shaper feels like tunnelling through the wall being faced rather
/// than finding an arbitrary route elsewhere in the dungeon.
/// </summary>
public static class DynamicShaperPathfinder
{
    private static readonly Vector2Int[] directions =
    {
        Vector2Int.up,
        Vector2Int.down,
        Vector2Int.left,
        Vector2Int.right
    };


    public static bool TryFindPath(
        DungeonGenerator generator,
        Vector2Int originCell,
        Vector2Int facingDirection,
        int minimumSolidCells,
        int maximumSolidCells,
        int maximumSideDeviation,
        out ShaperPathResult result)
    {
        result = null;


        if (generator == null ||
            generator.Grid == null ||
            facingDirection == Vector2Int.zero)
        {
            return false;
        }


        DungeonGrid grid =
            generator.Grid;


        Vector2Int firstWallCell =
            originCell +
            facingDirection;


        // The Shaper starts by breaking a wall.
        // If the cell ahead is already floor, this is not a valid use.
        if (grid.IsWalkable(
                firstWallCell))
        {
            return false;
        }


        minimumSolidCells =
            Mathf.Max(
                1,
                minimumSolidCells
            );


        maximumSolidCells =
            Mathf.Max(
                minimumSolidCells,
                maximumSolidCells
            );


        maximumSideDeviation =
            Mathf.Max(
                0,
                maximumSideDeviation
            );


        /*
         * Do not allow the search to simply leave one side of the
         * current room and immediately rediscover another part of the
         * same room.
         */
        HashSet<Vector2Int> sourceRegion =
            BuildSourceRegion(
                generator,
                originCell
            );


        /*
         * This is a small Dijkstra-style search.
         *
         * Forward movement is cheapest.
         * Sideways movement costs slightly more.
         * A small amount of backwards correction is possible but the
         * search can never move behind the player's starting wall.
         */
        List<Vector2Int> open =
            new List<Vector2Int>();


        HashSet<Vector2Int> closed =
            new HashSet<Vector2Int>();


        Dictionary<Vector2Int, float> cost =
            new Dictionary<Vector2Int, float>();


        Dictionary<Vector2Int, int> stepCount =
            new Dictionary<Vector2Int, int>();


        Dictionary<Vector2Int, Vector2Int> cameFrom =
            new Dictionary<Vector2Int, Vector2Int>();


        open.Add(
            firstWallCell
        );


        cost[firstWallCell] =
            1f;


        stepCount[firstWallCell] =
            1;


        bool targetFound =
            false;


        Vector2Int bestSolidEnd =
            Vector2Int.zero;


        Vector2Int bestTarget =
            Vector2Int.zero;


        float bestTargetScore =
            float.PositiveInfinity;


        while (open.Count > 0)
        {
            int bestOpenIndex =
                0;


            float bestOpenCost =
                cost[
                    open[0]
                ];


            for (int i = 1;
                 i < open.Count;
                 i++)
            {
                float candidateCost =
                    cost[
                        open[i]
                    ];


                if (candidateCost <
                    bestOpenCost)
                {
                    bestOpenCost =
                        candidateCost;

                    bestOpenIndex =
                        i;
                }
            }


            Vector2Int current =
                open[
                    bestOpenIndex
                ];


            open.RemoveAt(
                bestOpenIndex
            );


            if (closed.Contains(
                    current))
            {
                continue;
            }


            closed.Add(
                current
            );


            int currentSteps =
                stepCount[
                    current
                ];


            /*
             * Once enough solid terrain has been crossed, check every
             * adjacent cell for an existing room/corridor.
             */
            if (currentSteps >=
                minimumSolidCells)
            {
                foreach (Vector2Int direction in
                         directions)
                {
                    Vector2Int possibleTarget =
                        current +
                        direction;


                    if (!grid.IsWalkable(
                            possibleTarget))
                    {
                        continue;
                    }


                    if (!IsValidDestination(
                            grid,
                            possibleTarget,
                            sourceRegion))
                    {
                        continue;
                    }


                    if (!IsTargetBroadlyAhead(
                            originCell,
                            possibleTarget,
                            facingDirection,
                            maximumSolidCells,
                            maximumSideDeviation))
                    {
                        continue;
                    }


                    float lateralDistance =
                        CalculateLateralDistance(
                            originCell,
                            possibleTarget,
                            facingDirection
                        );


                    float targetScore =
                        cost[current] +
                        lateralDistance *
                        0.75f;


                    if (targetScore <
                        bestTargetScore)
                    {
                        targetFound =
                            true;


                        bestTargetScore =
                            targetScore;


                        bestSolidEnd =
                            current;


                        bestTarget =
                            possibleTarget;
                    }
                }
            }


            if (currentSteps >=
                maximumSolidCells)
            {
                continue;
            }


            foreach (Vector2Int direction in
                     directions)
            {
                Vector2Int neighbour =
                    current +
                    direction;


                // Existing floor is a possible destination, not a
                // traversable search cell.
                if (grid.IsWalkable(
                        neighbour))
                {
                    continue;
                }


                if (!IsInsideSearchEnvelope(
                        originCell,
                        neighbour,
                        facingDirection,
                        maximumSolidCells,
                        maximumSideDeviation))
                {
                    continue;
                }


                int newStepCount =
                    currentSteps +
                    1;


                if (newStepCount >
                    maximumSolidCells)
                {
                    continue;
                }


                float movementCost =
                    CalculateMovementCost(
                        direction,
                        facingDirection
                    );


                float newCost =
                    cost[current] +
                    movementCost;


                float previousCost;


                if (cost.TryGetValue(
                        neighbour,
                        out previousCost) &&
                    previousCost <=
                        newCost)
                {
                    continue;
                }


                cost[neighbour] =
                    newCost;


                stepCount[neighbour] =
                    newStepCount;


                cameFrom[neighbour] =
                    current;


                if (!open.Contains(
                        neighbour))
                {
                    open.Add(
                        neighbour
                    );
                }
            }
        }


        if (!targetFound)
        {
            return false;
        }


        List<Vector2Int> guide =
            ReconstructPath(
                firstWallCell,
                bestSolidEnd,
                cameFrom
            );


        if (guide.Count <
                minimumSolidCells ||
            guide.Count >
                maximumSolidCells)
        {
            return false;
        }


        result =
            new ShaperPathResult(
                originCell,
                firstWallCell,
                bestTarget,
                guide
            );


        return true;
    }


    /// <summary>
    /// Builds the region that should NOT count as a destination.
    ///
    /// If the player is in a room, that room and its connected
    /// CA-grown room cells are excluded.
    ///
    /// If the player is in a corridor, the current generated corridor
    /// is excluded.
    /// </summary>
    private static HashSet<Vector2Int> BuildSourceRegion(
        DungeonGenerator generator,
        Vector2Int originCell)
    {
        HashSet<Vector2Int> source =
            new HashSet<Vector2Int>();


        DungeonGrid grid =
            generator.Grid;


        Room currentRoom =
            null;


        foreach (Room room in
                 generator.Rooms)
        {
            if (room != null &&
                room.Contains(
                    originCell))
            {
                currentRoom =
                    room;

                break;
            }
        }


        if (currentRoom != null)
        {
            for (int x =
                     currentRoom.Bounds.xMin;
                 x <
                     currentRoom.Bounds.xMax;
                 x++)
            {
                for (int y =
                         currentRoom.Bounds.yMin;
                     y <
                         currentRoom.Bounds.yMax;
                     y++)
                {
                    Vector2Int cell =
                        new Vector2Int(
                            x,
                            y
                        );


                    if (grid.IsWalkable(
                            cell))
                    {
                        source.Add(
                            cell
                        );
                    }
                }
            }


            /*
             * Include CA-grown cells which are directly connected to
             * this room so the Shaper cannot target its own irregular
             * outer edge as though it were another room.
             */
            Queue<Vector2Int> frontier =
                new Queue<Vector2Int>();


            foreach (Vector2Int cell in
                     source)
            {
                frontier.Enqueue(
                    cell
                );
            }


            while (frontier.Count > 0)
            {
                Vector2Int current =
                    frontier.Dequeue();


                foreach (Vector2Int direction in
                         directions)
                {
                    Vector2Int neighbour =
                        current +
                        direction;


                    if (source.Contains(
                            neighbour))
                    {
                        continue;
                    }


                    if (!grid.IsOrganicRoomCell(
                            neighbour))
                    {
                        continue;
                    }


                    source.Add(
                        neighbour
                    );


                    frontier.Enqueue(
                        neighbour
                    );
                }
            }


            return source;
        }


        /*
         * If not inside a room, check whether this is one of the
         * originally generated corridors.
         */
        if (generator.Corridors != null)
        {
            foreach (
                CorridorGenerator.Corridor corridor
                in generator.Corridors)
            {
                if (corridor == null ||
                    corridor.Cells == null)
                {
                    continue;
                }


                if (!corridor.Cells.Contains(
                        originCell))
                {
                    continue;
                }


                foreach (Vector2Int cell in
                         corridor.Cells)
                {
                    source.Add(
                        cell
                    );
                }


                return source;
            }
        }


        // Fallback for standing inside dynamically created terrain.
        source.Add(
            originCell
        );


        return source;
    }


    private static bool IsValidDestination(
        DungeonGrid grid,
        Vector2Int cell,
        HashSet<Vector2Int> sourceRegion)
    {
        if (sourceRegion.Contains(
                cell))
        {
            return false;
        }


        /*
         * Destination must be part of existing generated topology.
         *
         * We currently do not target another dynamic Shaper tunnel
         * because the intended interaction is room/corridor discovery.
         */
        return
            grid.IsRoomCell(cell) ||
            grid.IsCorridorCell(cell) ||
            grid.IsOrganicRoomCell(cell);
    }


    private static bool IsInsideSearchEnvelope(
        Vector2Int origin,
        Vector2Int cell,
        Vector2Int facing,
        int maximumForwardDistance,
        int maximumSideDeviation)
    {
        Vector2Int difference =
            cell -
            origin;


        int forwardDistance =
            difference.x *
                facing.x +
            difference.y *
                facing.y;


        if (forwardDistance < 1 ||
            forwardDistance >
                maximumForwardDistance)
        {
            return false;
        }


        float lateralDistance =
            CalculateLateralDistance(
                origin,
                cell,
                facing
            );


        return lateralDistance <=
               maximumSideDeviation;
    }


    private static bool IsTargetBroadlyAhead(
        Vector2Int origin,
        Vector2Int target,
        Vector2Int facing,
        int maximumForwardDistance,
        int maximumSideDeviation)
    {
        Vector2Int difference =
            target -
            origin;


        int forwardDistance =
            difference.x *
                facing.x +
            difference.y *
                facing.y;


        if (forwardDistance <= 0 ||
            forwardDistance >
                maximumForwardDistance +
                1)
        {
            return false;
        }


        return
            CalculateLateralDistance(
                origin,
                target,
                facing) <=
            maximumSideDeviation +
            1;
    }


    private static float CalculateLateralDistance(
        Vector2Int origin,
        Vector2Int cell,
        Vector2Int facing)
    {
        Vector2Int difference =
            cell -
            origin;


        Vector2Int perpendicular =
            new Vector2Int(
                -facing.y,
                facing.x
            );


        return Mathf.Abs(
            difference.x *
                perpendicular.x +
            difference.y *
                perpendicular.y
        );
    }


    private static float CalculateMovementCost(
        Vector2Int movement,
        Vector2Int facing)
    {
        int alignment =
            movement.x *
                facing.x +
            movement.y *
                facing.y;


        if (alignment > 0)
        {
            // Moving into the direction the player aimed.
            return 1f;
        }


        if (alignment == 0)
        {
            // Allows modest curvature toward a nearby room.
            return 1.35f;
        }


        // A small correction backwards is possible while still inside
        // the overall forward envelope, but is deliberately expensive.
        return 2.25f;
    }


    private static List<Vector2Int> ReconstructPath(
        Vector2Int start,
        Vector2Int end,
        Dictionary<Vector2Int, Vector2Int> cameFrom)
    {
        List<Vector2Int> path =
            new List<Vector2Int>();


        Vector2Int current =
            end;


        path.Add(
            current
        );


        while (current != start)
        {
            Vector2Int previous;


            if (!cameFrom.TryGetValue(
                    current,
                    out previous))
            {
                path.Clear();
                return path;
            }


            current =
                previous;


            path.Add(
                current
            );
        }


        path.Reverse();


        return path;
    }
}