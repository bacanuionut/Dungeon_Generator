using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Weighted A* used by the Warden.
///
/// Normal floor is cheap to travel through.
///
/// Solid terrain is also considered traversable by the search, but at
/// a substantially higher cost because the Warden must excavate it
/// before moving through.
///
/// This allows the same search to decide whether following an existing
/// route or breaking a new route is more worthwhile.
/// </summary>
public static class WardenPathfinder
{
    private static readonly Vector2Int[] directions =
    {
        Vector2Int.up,
        Vector2Int.down,
        Vector2Int.left,
        Vector2Int.right
    };


    public static List<Vector2Int> FindPath(
        DungeonGrid grid,
        Vector2Int start,
        Vector2Int goal,
        float solidTerrainCost,
        int searchMargin)
    {
        List<Vector2Int> empty =
            new List<Vector2Int>();


        if (grid == null)
            return empty;


        solidTerrainCost =
            Mathf.Max(
                1.1f,
                solidTerrainCost
            );


        searchMargin =
            Mathf.Max(
                0,
                searchMargin
            );


        int minX;
        int maxX;
        int minY;
        int maxY;


        if (!CalculateSearchBounds(
                grid,
                start,
                goal,
                searchMargin,
                out minX,
                out maxX,
                out minY,
                out maxY))
        {
            return empty;
        }


        List<Vector2Int> open =
            new List<Vector2Int>();


        HashSet<Vector2Int> closed =
            new HashSet<Vector2Int>();


        Dictionary<Vector2Int, float> gScore =
            new Dictionary<Vector2Int, float>();


        Dictionary<Vector2Int, Vector2Int> cameFrom =
            new Dictionary<Vector2Int, Vector2Int>();


        open.Add(
            start
        );


        gScore[start] =
            0f;


        while (open.Count > 0)
        {
            int bestIndex =
                0;


            float bestScore =
                GetEstimatedTotalCost(
                    open[0],
                    goal,
                    gScore
                );


            for (int i = 1;
                 i < open.Count;
                 i++)
            {
                float candidateScore =
                    GetEstimatedTotalCost(
                        open[i],
                        goal,
                        gScore
                    );


                if (candidateScore <
                    bestScore)
                {
                    bestScore =
                        candidateScore;

                    bestIndex =
                        i;
                }
            }


            Vector2Int current =
                open[
                    bestIndex
                ];


            open.RemoveAt(
                bestIndex
            );


            if (current ==
                goal)
            {
                return ReconstructPath(
                    start,
                    goal,
                    cameFrom
                );
            }


            if (closed.Contains(
                    current))
            {
                continue;
            }


            closed.Add(
                current
            );


            foreach (Vector2Int direction in
                     directions)
            {
                Vector2Int neighbour =
                    current +
                    direction;


                if (neighbour.x < minX ||
                    neighbour.x > maxX ||
                    neighbour.y < minY ||
                    neighbour.y > maxY)
                {
                    continue;
                }


                if (closed.Contains(
                        neighbour))
                {
                    continue;
                }


                /*
                 * Existing floor is cheap.
                 *
                 * Solid terrain is expensive but still traversable by
                 * the search because the Warden can excavate it.
                 */
                float movementCost =
                    grid.IsWalkable(
                        neighbour)
                        ? 1f
                        : solidTerrainCost;


                float tentativeScore =
                    gScore[current] +
                    movementCost;


                float existingScore;


                if (gScore.TryGetValue(
                        neighbour,
                        out existingScore) &&
                    existingScore <=
                        tentativeScore)
                {
                    continue;
                }


                cameFrom[neighbour] =
                    current;


                gScore[neighbour] =
                    tentativeScore;


                if (!open.Contains(
                        neighbour))
                {
                    open.Add(
                        neighbour
                    );
                }
            }
        }


        return empty;
    }


    private static float GetEstimatedTotalCost(
        Vector2Int cell,
        Vector2Int goal,
        Dictionary<Vector2Int, float> gScore)
    {
        float travelled =
            gScore[
                cell
            ];


        float heuristic =
            Mathf.Abs(
                goal.x -
                cell.x
            ) +
            Mathf.Abs(
                goal.y -
                cell.y
            );


        return travelled +
               heuristic;
    }


    /// <summary>
    /// Limits excavation to the actual generated dungeon area plus a
    /// small configurable margin.
    ///
    /// Without this restriction an unrestricted solid-terrain search could
    /// theoretically route outside the useful dungeon area.
    /// </summary>
    private static bool CalculateSearchBounds(
        DungeonGrid grid,
        Vector2Int start,
        Vector2Int goal,
        int margin,
        out int minX,
        out int maxX,
        out int minY,
        out int maxY)
    {
        minX =
            int.MaxValue;

        maxX =
            int.MinValue;

        minY =
            int.MaxValue;

        maxY =
            int.MinValue;


        foreach (Vector2Int cell in
                 grid.FloorCells)
        {
            minX =
                Mathf.Min(
                    minX,
                    cell.x
                );


            maxX =
                Mathf.Max(
                    maxX,
                    cell.x
                );


            minY =
                Mathf.Min(
                    minY,
                    cell.y
                );


            maxY =
                Mathf.Max(
                    maxY,
                    cell.y
                );
        }


        if (minX ==
            int.MaxValue)
        {
            return false;
        }


        minX =
            Mathf.Min(
                minX,
                Mathf.Min(
                    start.x,
                    goal.x
                )
            ) -
            margin;


        maxX =
            Mathf.Max(
                maxX,
                Mathf.Max(
                    start.x,
                    goal.x
                )
            ) +
            margin;


        minY =
            Mathf.Min(
                minY,
                Mathf.Min(
                    start.y,
                    goal.y
                )
            ) -
            margin;


        maxY =
            Mathf.Max(
                maxY,
                Mathf.Max(
                    start.y,
                    goal.y
                )
            ) +
            margin;


        return true;
    }


    private static List<Vector2Int> ReconstructPath(
        Vector2Int start,
        Vector2Int goal,
        Dictionary<Vector2Int, Vector2Int> cameFrom)
    {
        List<Vector2Int> path =
            new List<Vector2Int>();


        Vector2Int current =
            goal;


        path.Add(
            current
        );


        while (current !=
               start)
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