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
        return TryFindPathInternal(
            generator,
            originCell,
            facingDirection,
            minimumSolidCells,
            maximumSolidCells,
            maximumSideDeviation,
            false,
            out result
        );
    }


    /// <summary>
    /// Searches for a Digger route that ends in a different room rather
    /// than terminating at a corridor cell.
    /// </summary>
    public static bool TryFindRoomPath(
        DungeonGenerator generator,
        Vector2Int originCell,
        Vector2Int facingDirection,
        int minimumSolidCells,
        int maximumSolidCells,
        int maximumSideDeviation,
        out ShaperPathResult result)
    {
        return TryFindPathInternal(
            generator,
            originCell,
            facingDirection,
            minimumSolidCells,
            maximumSolidCells,
            maximumSideDeviation,
            true,
            out result
        );
    }


    private static bool TryFindPathInternal(
        DungeonGenerator generator,
        Vector2Int originCell,
        Vector2Int facingDirection,
        int minimumSolidCells,
        int maximumSolidCells,
        int maximumSideDeviation,
        bool roomDestinationOnly,
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


        /*
         * A solid environmental prop may sit directly in front of the wall.
         *
         * The prop still occupies geometric floor, so IsWalkable() is true,
         * but it should not hide an otherwise valid Shaper wall target.
         * Skip a very small number of consecutive breakable prop cells, then
         * require the next cell to be genuine solid dungeon terrain.
         */
        const int maximumBreakablePropsBeforeWall = 2;

        int skippedPropCells = 0;

        while (grid.IsNavigationBlocked(
                   firstWallCell) &&
               skippedPropCells <
                   maximumBreakablePropsBeforeWall)
        {
            firstWallCell +=
                facingDirection;

            skippedPropCells++;
        }


        // The Shaper still starts by breaking genuine dungeon terrain.
        // Ordinary open floor in front of the player remains an invalid use.
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


        /*
         * Breakable props in front of the wall are part of the obstruction
         * the Shaper is being asked to remove.
         *
         * The search itself starts at genuine dungeon terrain, so without
         * this adjustment a prop + thin wall could be rejected because the
         * wall portion alone did not satisfy minimumSolidCells.
         */
        int effectiveMinimumSolidCells =
            Mathf.Max(
                1,
                minimumSolidCells -
                    skippedPropCells
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
                effectiveMinimumSolidCells)
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
                            sourceRegion,
                            roomDestinationOnly))
                    {
                        continue;
                    }


                    if (!IsTargetBroadlyAhead(
                            originCell,
                            possibleTarget,
                            facingDirection,
                            maximumSolidCells +
                                skippedPropCells,
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
                        maximumSolidCells +
                            skippedPropCells,
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
            if (skippedPropCells > 0)
            {
                UnityEngine.Debug.Log(
                    "SHAPER TARGET REJECTED AFTER PROP - " +
                    $"Facing {facingDirection}, " +
                    $"props skipped: {skippedPropCells}, " +
                    $"first wall cell: {firstWallCell}. " +
                    "No valid generated room/corridor was found within the search envelope."
                );
            }

            return false;
        }


        List<Vector2Int> guide =
            ReconstructPath(
                firstWallCell,
                bestSolidEnd,
                cameFrom
            );


        if (guide.Count <
                effectiveMinimumSolidCells ||
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
    /// Builds only the physical dungeon region the player is currently
    /// standing in.
    ///
    /// For a room this includes:
    /// - the original BSP room
    /// - all organic CA-grown cells connected to that room
    ///
    /// Other rooms remain valid Shaper targets even when they already have
    /// a normal corridor connection to the current room.
    ///
    /// For a corridor, only that particular corridor is excluded so the
    /// Shaper does not immediately rediscover the corridor it started from.
    /// </summary>
    private static HashSet<Vector2Int> BuildSourceRegion(
        DungeonGenerator generator,
        Vector2Int originCell)
    {
        HashSet<Vector2Int> source =
            new HashSet<Vector2Int>();


        if (generator == null ||
            generator.Grid == null)
        {
            return source;
        }


        /*
         * Room.Contains() covers the original BSP rectangle only. Organic
         * room cells are included so the source region matches the playable
         * shape after cellular-automata processing.
         */
        foreach (Room room in
                 generator.Rooms)
        {
            if (room == null)
                continue;


            HashSet<Vector2Int> roomRegion =
                BuildCompleteRoomRegion(
                    generator,
                    room
                );


            if (!roomRegion.Contains(
                    originCell))
            {
                continue;
            }


            /*
             * Exclude the complete source room. Other rooms remain valid
             * destinations even when a normal corridor already connects them.
             */
            source.UnionWith(
                roomRegion
            );


            return source;
        }


        /*
         * If the player is not inside a room, check whether they are
         * standing inside one of the generated corridors.
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


                bool containsPlayer =
                    false;


                foreach (Vector2Int cell in
                         corridor.Cells)
                {
                    if (cell ==
                        originCell)
                    {
                        containsPlayer =
                            true;

                        break;
                    }
                }


                if (!containsPlayer)
                    continue;


                /*
                 * Exclude only the corridor the player is currently
                 * standing in.
                 */
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


        // A dynamic Digger cell is treated as its own source region.
        source.Add(
            originCell
        );


        return source;
    }

    /// <summary>
    /// Builds the complete physical footprint of one room.
    ///
    /// It starts with the original BSP rectangle and then flood-fills
    /// through organic room cells produced by the CA post-processing stage.
    ///
    /// This allows gameplay systems to recognise that irregular room
    /// extensions still belong to the same logical Room object.
    /// </summary>
    private static HashSet<Vector2Int> BuildCompleteRoomRegion(
        DungeonGenerator generator,
        Room room)
    {
        HashSet<Vector2Int> region =
            new HashSet<Vector2Int>();


        if (generator == null ||
            generator.Grid == null ||
            room == null)
        {
            return region;
        }


        DungeonGrid grid =
            generator.Grid;


        Queue<Vector2Int> frontier =
            new Queue<Vector2Int>();


        // Original BSP room cells.

        for (int x = room.Bounds.xMin;
             x < room.Bounds.xMax;
             x++)
        {
            for (int y = room.Bounds.yMin;
                 y < room.Bounds.yMax;
                 y++)
            {
                Vector2Int cell =
                    new Vector2Int(
                        x,
                        y
                    );


                if (!grid.IsWalkable(
                        cell))
                {
                    continue;
                }


                if (region.Add(
                        cell))
                {
                    frontier.Enqueue(
                        cell
                    );
                }
            }
        }


        // Organic room cells connected to the original room.

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


                if (region.Contains(
                        neighbour))
                {
                    continue;
                }


                /*
                 * Only propagate through cells specifically created by
                 * organic room shaping.
                 *
                 * This prevents the flood-fill escaping into ordinary
                 * corridors and eventually covering the whole dungeon.
                 */
                if (!grid.IsOrganicRoomCell(
                        neighbour))
                {
                    continue;
                }


                region.Add(
                    neighbour
                );


                frontier.Enqueue(
                    neighbour
                );
            }
        }


        return region;
    }

    /// <summary>
    /// Adds one complete room region to a set, including organic
    /// CA-generated floor connected to its original BSP room.
    /// </summary>
    private static void AddRoomRegion(
        DungeonGenerator generator,
        Room room,
        HashSet<Vector2Int> destination)
    {
        if (generator == null ||
            generator.Grid == null ||
            room == null ||
            destination == null)
        {
            return;
        }


        DungeonGrid grid =
            generator.Grid;


        Queue<Vector2Int> frontier =
            new Queue<Vector2Int>();


        /*
         * Add the original BSP room cells.
         */
        for (int x = room.Bounds.xMin;
             x < room.Bounds.xMax;
             x++)
        {
            for (int y = room.Bounds.yMin;
                 y < room.Bounds.yMax;
                 y++)
            {
                Vector2Int cell =
                    new Vector2Int(
                        x,
                        y
                    );


                if (!grid.IsWalkable(
                        cell))
                {
                    continue;
                }


                if (destination.Add(
                        cell))
                {
                    frontier.Enqueue(
                        cell
                    );
                }
            }
        }


        /*
         * Grow outward only through cells classified as organic room
         * floor.
         *
         * This associates the irregular CA boundary with its room without
         * flood-filling ordinary corridors and therefore the entire map.
         */
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


                if (destination.Contains(
                        neighbour))
                {
                    continue;
                }


                if (!grid.IsOrganicRoomCell(
                        neighbour))
                {
                    continue;
                }


                destination.Add(
                    neighbour
                );


                frontier.Enqueue(
                    neighbour
                );
            }
        }
    }

    private static bool CorridorContainsCell(
    CorridorGenerator.Corridor corridor,
    Vector2Int targetCell)
    {
        if (corridor == null ||
            corridor.Cells == null)
        {
            return false;
        }


        foreach (Vector2Int cell in
                 corridor.Cells)
        {
            if (cell ==
                targetCell)
            {
                return true;
            }
        }


        return false;
    }

    /// <summary>
    /// Returns true when any cell of a corridor overlaps or is cardinally
    /// adjacent to the supplied room region.
    /// </summary>
    private static bool CorridorTouchesRegion(
        CorridorGenerator.Corridor corridor,
        HashSet<Vector2Int> region)
    {
        if (corridor == null ||
            corridor.Cells == null ||
            region == null)
        {
            return false;
        }


        foreach (Vector2Int corridorCell in
                 corridor.Cells)
        {
            if (region.Contains(
                    corridorCell))
            {
                return true;
            }


            foreach (Vector2Int direction in
                     directions)
            {
                if (region.Contains(
                        corridorCell +
                        direction))
                {
                    return true;
                }
            }
        }


        return false;
    }

    /// <summary>
    /// Tests whether the original room boundary touches a supplied
    /// corridor region.
    /// </summary>
    private static bool RoomTouchesRegion(
        Room room,
        HashSet<Vector2Int> region)
    {
        if (room == null ||
            region == null)
        {
            return false;
        }


        for (int x = room.Bounds.xMin;
             x < room.Bounds.xMax;
             x++)
        {
            for (int y = room.Bounds.yMin;
                 y < room.Bounds.yMax;
                 y++)
            {
                Vector2Int roomCell =
                    new Vector2Int(
                        x,
                        y
                    );


                if (region.Contains(
                        roomCell))
                {
                    return true;
                }


                foreach (Vector2Int direction in
                         directions)
                {
                    if (region.Contains(
                            roomCell +
                            direction))
                    {
                        return true;
                    }
                }
            }
        }


        return false;
    }

    private static bool IsValidDestination(
        DungeonGrid grid,
        Vector2Int cell,
        HashSet<Vector2Int> sourceRegion,
        bool roomDestinationOnly)
    {
        if (sourceRegion.Contains(
                cell))
        {
            return false;
        }


        if (roomDestinationOnly)
        {
            return
                grid.IsRoomCell(cell) ||
                grid.IsOrganicRoomCell(cell);
        }


        // Dynamic Digger tunnels are not treated as destinations.
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