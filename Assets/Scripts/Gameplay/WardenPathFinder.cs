using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Weighted A* used by the Warden.
///
/// Normal floor has a movement cost of 1.
///
/// Solid terrain is also searchable, but at a higher configurable cost
/// because the Warden must excavate it before travelling through it.
///
/// PERFORMANCE:
///
/// The original version used a List as the A* open set. Finding the
/// cheapest node therefore required scanning the entire List on every
/// expansion.
///
/// This version uses a binary min-heap instead. The lowest-cost node can
/// be removed in O(log n) time rather than repeatedly performing a
/// linear scan.
///
/// Search collections are also reused between calls to substantially
/// reduce garbage collection during pursuit.
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


    // ============================================================
    // REUSABLE SEARCH DATA
    // ============================================================

    /*
     * These collections are deliberately reused.
     *
     * There is only one physical Warden in the current game and all
     * pathfinding occurs on Unity's main thread, so a shared search
     * workspace is appropriate here.
     */
    private static readonly MinHeap openSet =
        new MinHeap(
            8192
        );


    private static readonly Dictionary<Vector2Int, byte> closedSet =
        new Dictionary<Vector2Int, byte>(
            8192
        );


    private static readonly Dictionary<Vector2Int, float> gScore =
        new Dictionary<Vector2Int, float>(
            8192
        );


    private static readonly Dictionary<Vector2Int, Vector2Int> cameFrom =
        new Dictionary<Vector2Int, Vector2Int>(
            8192
        );


    /*
     * WardenController only reads the returned path during the current
     * pursuit step.
     *
     * Reusing this List avoids allocating a fresh result List on every
     * Warden movement decision.
     */
    private static readonly List<Vector2Int> pathBuffer =
        new List<Vector2Int>(
            512
        );


    private static int insertionOrder;


    // ------------------------------------------------------------
    // OPTIONAL DIAGNOSTICS
    // ------------------------------------------------------------

    public static int LastExpandedNodeCount
    {
        get;
        private set;
    }


    public static int LastPathLength
    {
        get;
        private set;
    }


    // ============================================================
    // PUBLIC SEARCH
    // ============================================================

    public static List<Vector2Int> FindPath(
        DungeonGrid grid,
        Vector2Int start,
        Vector2Int goal,
        float solidTerrainCost,
        int searchMargin)
    {
        ResetSearchWorkspace();


        if (grid == null)
        {
            return pathBuffer;
        }


        if (start ==
            goal)
        {
            pathBuffer.Add(
                start
            );


            LastPathLength =
                1;


            return pathBuffer;
        }


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
            return pathBuffer;
        }


        gScore[start] =
            0f;


        float startingHeuristic =
            CalculateHeuristic(
                start,
                goal
            );


        openSet.Push(
            new OpenNode(
                start,
                0f,
                startingHeuristic,
                insertionOrder++
            )
        );


        while (openSet.Count > 0)
        {
            OpenNode openNode =
                openSet.Pop();


            Vector2Int current =
                openNode.Cell;


            /*
             * A better route to this cell may have been inserted into
             * the heap after this entry.
             *
             * In that situation the older entry is stale and can simply
             * be ignored.
             */
            float currentBestG;


            if (!gScore.TryGetValue(
                    current,
                    out currentBestG))
            {
                continue;
            }


            if (openNode.GScore >
                currentBestG +
                0.0001f)
            {
                continue;
            }


            if (closedSet.ContainsKey(
                    current))
            {
                continue;
            }


            // ----------------------------------------------------
            // GOAL REACHED
            // ----------------------------------------------------

            if (current ==
                goal)
            {
                ReconstructPath(
                    start,
                    goal
                );


                LastPathLength =
                    pathBuffer.Count;


                return pathBuffer;
            }


            closedSet[current] =
                1;


            LastExpandedNodeCount++;


            // ----------------------------------------------------
            // EXPAND CARDINAL NEIGHBOURS
            // ----------------------------------------------------

            foreach (Vector2Int direction in
                     directions)
            {
                Vector2Int neighbour =
                    current +
                    direction;


                if (neighbour.x <
                        minX ||
                    neighbour.x >
                        maxX ||
                    neighbour.y <
                        minY ||
                    neighbour.y >
                        maxY)
                {
                    continue;
                }


                if (closedSet.ContainsKey(
                        neighbour))
                {
                    continue;
                }


                /*
                 * Existing dungeon floor is inexpensive.
                 *
                 * Solid terrain is expensive but still considered
                 * because the Warden is capable of excavating it.
                 */
                float movementCost =
                    grid.IsWalkable(
                        neighbour)
                        ? 1f
                        : solidTerrainCost;


                float tentativeG =
                    currentBestG +
                    movementCost;


                float existingG;


                if (gScore.TryGetValue(
                        neighbour,
                        out existingG) &&
                    existingG <=
                        tentativeG)
                {
                    continue;
                }


                /*
                 * This is either the first route to the cell or a
                 * cheaper route than the one previously discovered.
                 */
                cameFrom[neighbour] =
                    current;


                gScore[neighbour] =
                    tentativeG;


                float heuristic =
                    CalculateHeuristic(
                        neighbour,
                        goal
                    );


                float estimatedTotal =
                    tentativeG +
                    heuristic;


                /*
                 * We allow another copy of a cell to enter the heap
                 * when a better path is discovered.
                 *
                 * This avoids needing an expensive search through the
                 * heap to perform decrease-key. Stale entries are
                 * discarded when popped.
                 */
                openSet.Push(
                    new OpenNode(
                        neighbour,
                        tentativeG,
                        estimatedTotal,
                        insertionOrder++
                    )
                );
            }
        }


        /*
         * No route found inside the permitted search region.
         */
        return pathBuffer;
    }


    // ============================================================
    // SEARCH RESET
    // ============================================================

    private static void ResetSearchWorkspace()
    {
        openSet.Clear();

        closedSet.Clear();

        gScore.Clear();

        cameFrom.Clear();

        pathBuffer.Clear();


        insertionOrder =
            0;


        LastExpandedNodeCount =
            0;


        LastPathLength =
            0;
    }


    // ============================================================
    // HEURISTIC
    // ============================================================

    /// <summary>
    /// Manhattan distance is appropriate because movement is restricted
    /// to the four cardinal grid directions.
    ///
    /// Minimum movement cost is 1, so this remains an admissible A*
    /// heuristic even when solid terrain costs considerably more.
    /// </summary>
    private static float CalculateHeuristic(
        Vector2Int cell,
        Vector2Int goal)
    {
        return
            Mathf.Abs(
                goal.x -
                cell.x
            ) +
            Mathf.Abs(
                goal.y -
                cell.y
            );
    }


    // ============================================================
    // SEARCH BOUNDS
    // ============================================================

    /// <summary>
    /// Restricts the weighted search to the generated dungeon area plus
    /// the configured excavation margin.
    ///
    /// The Warden may therefore create shortcuts through nearby solid
    /// terrain without searching indefinitely into empty world space.
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
            if (cell.x <
                minX)
            {
                minX =
                    cell.x;
            }


            if (cell.x >
                maxX)
            {
                maxX =
                    cell.x;
            }


            if (cell.y <
                minY)
            {
                minY =
                    cell.y;
            }


            if (cell.y >
                maxY)
            {
                maxY =
                    cell.y;
            }
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


    // ============================================================
    // PATH RECONSTRUCTION
    // ============================================================

    private static void ReconstructPath(
        Vector2Int start,
        Vector2Int goal)
    {
        pathBuffer.Clear();


        Vector2Int current =
            goal;


        pathBuffer.Add(
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
                /*
                 * This should not occur after a successful A* search,
                 * but returning an empty path is safer than returning an
                 * incomplete route.
                 */
                pathBuffer.Clear();


                return;
            }


            current =
                previous;


            pathBuffer.Add(
                current
            );
        }


        pathBuffer.Reverse();
    }


    // ============================================================
    // BINARY MIN-HEAP
    // ============================================================

    /// <summary>
    /// One A* open-set entry.
    ///
    /// GScore is stored so stale heap entries can be identified.
    ///
    /// InsertionOrder provides deterministic tie-breaking when two nodes
    /// have the same estimated total cost.
    /// </summary>
    private struct OpenNode
    {
        public Vector2Int Cell;

        public float GScore;

        public float FScore;

        public int InsertionOrder;


        public OpenNode(
            Vector2Int cell,
            float gScore,
            float fScore,
            int order)
        {
            Cell =
                cell;


            GScore =
                gScore;


            FScore =
                fScore;


            InsertionOrder =
                order;
        }
    }


    /// <summary>
    /// Small binary minimum heap used as the A* priority queue.
    ///
    /// Push and Pop are O(log n), replacing the full List scan used by
    /// the previous implementation.
    /// </summary>
    private sealed class MinHeap
    {
        private readonly List<OpenNode> items;


        public int Count =>
            items.Count;


        public MinHeap(
            int initialCapacity)
        {
            items =
                new List<OpenNode>(
                    Mathf.Max(
                        16,
                        initialCapacity
                    )
                );
        }


        public void Clear()
        {
            items.Clear();
        }


        public void Push(
            OpenNode node)
        {
            items.Add(
                node
            );


            int index =
                items.Count -
                1;


            /*
             * Bubble upward until the heap property is restored.
             */
            while (index > 0)
            {
                int parentIndex =
                    (index - 1) /
                    2;


                if (!ComesBefore(
                        items[index],
                        items[parentIndex]))
                {
                    break;
                }


                Swap(
                    index,
                    parentIndex
                );


                index =
                    parentIndex;
            }
        }


        public OpenNode Pop()
        {
            OpenNode root =
                items[0];


            int finalIndex =
                items.Count -
                1;


            OpenNode last =
                items[
                    finalIndex
                ];


            items.RemoveAt(
                finalIndex
            );


            if (items.Count == 0)
            {
                return root;
            }


            items[0] =
                last;


            int index =
                0;


            /*
             * Bubble downward until both children have equal or larger
             * priorities.
             */
            while (true)
            {
                int leftChild =
                    index *
                    2 +
                    1;


                if (leftChild >=
                    items.Count)
                {
                    break;
                }


                int rightChild =
                    leftChild +
                    1;


                int bestChild =
                    leftChild;


                if (rightChild <
                        items.Count &&
                    ComesBefore(
                        items[rightChild],
                        items[leftChild]))
                {
                    bestChild =
                        rightChild;
                }


                if (!ComesBefore(
                        items[bestChild],
                        items[index]))
                {
                    break;
                }


                Swap(
                    index,
                    bestChild
                );


                index =
                    bestChild;
            }


            return root;
        }


        private static bool ComesBefore(
            OpenNode first,
            OpenNode second)
        {
            const float epsilon =
                0.0001f;


            if (first.FScore <
                second.FScore -
                epsilon)
            {
                return true;
            }


            if (first.FScore >
                second.FScore +
                epsilon)
            {
                return false;
            }


            /*
             * Preserve deterministic ordering for equal A* costs.
             */
            return
                first.InsertionOrder <
                second.InsertionOrder;
        }


        private void Swap(
            int firstIndex,
            int secondIndex)
        {
            OpenNode temporary =
                items[firstIndex];


            items[firstIndex] =
                items[secondIndex];


            items[secondIndex] =
                temporary;
        }
    }
}