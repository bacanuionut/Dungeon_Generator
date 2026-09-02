using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Handles progressive runtime changes to dungeon terrain.
///
/// The player's Shaper supplies a hidden guide path through solid
/// terrain. Cells then become walkable progressively along that guide,
/// with local neighbour rules producing an organic passage boundary.
///
/// The same system will later be reused by the Warden at a much slower
/// growth rate.
/// </summary>
public class DungeonTerrainModifier : MonoBehaviour
{
    [Header("References")]

    [SerializeField]
    private DungeonGenerator dungeonGenerator;

    [SerializeField]
    private PlayerVisionController playerVisionController;


    [Header("Player Shaper Growth")]

    [Tooltip("Delay between individual forward growth steps.")]
    [SerializeField]
    private float growthStepDelay = 0.08f;

    [Tooltip(
        "Base probability that solid cells beside the guide also " +
        "become floor."
    )]
    [Range(0f, 1f)]
    [SerializeField]
    private float sideGrowthChance = 0.32f;

    [Tooltip(
        "Refresh the dungeon renderer after this many guide steps."
    )]
    [SerializeField]
    private int renderEverySteps = 1;


    private bool modifyingTerrain;


    public bool IsModifyingTerrain =>
        modifyingTerrain;


    /// <summary>
    /// Begins progressive growth through a list of guaranteed solid
    /// guide cells.
    /// </summary>
    public bool TryCarvePath(
        IReadOnlyList<Vector2Int> guideCells,
        int operationSeed,
        Action onComplete)
    {
        if (modifyingTerrain ||
            guideCells == null ||
            guideCells.Count == 0 ||
            dungeonGenerator == null ||
            dungeonGenerator.Grid == null)
        {
            return false;
        }


        List<Vector2Int> guideCopy =
            new List<Vector2Int>(
                guideCells
            );


        StartCoroutine(
            CarvePathSequence(
                guideCopy,
                operationSeed,
                onComplete
            )
        );


        return true;
    }


    private IEnumerator CarvePathSequence(
        List<Vector2Int> guideCells,
        int operationSeed,
        Action onComplete)
    {
        modifyingTerrain =
            true;


        DungeonGrid grid =
            dungeonGenerator.Grid;


        System.Random random =
            new System.Random(
                operationSeed
            );


        HashSet<Vector2Int> guideSet =
            new HashSet<Vector2Int>(
                guideCells
            );


        int totalDynamicCellsAdded =
            0;


        UnityEngine.Debug.Log(
            "SHAPER GROWTH STARTED - " +
            $"Guide cells: {guideCells.Count}"
        );


        /*
         * Advance one guide cell at a time.
         *
         * This creates the visible moving growth front that is central
         * to the Shaper mechanic.
         */
        for (int i = 0;
             i < guideCells.Count;
             i++)
        {
            Vector2Int guideCell =
                guideCells[i];


            if (grid.AddDynamicFloorCell(
                    guideCell))
            {
                totalDynamicCellsAdded++;
            }


            /*
             * Local CA-style growth.
             *
             * The guide guarantees the tunnel reaches its target.
             * These surrounding cells determine the irregular shape.
             */
            List<Vector2Int> sideGrowth =
                new List<Vector2Int>();


            foreach (Vector2Int neighbour in
                     GetMooreNeighbours(
                         guideCell))
            {
                if (grid.IsWalkable(
                        neighbour))
                {
                    continue;
                }


                if (!IsAdjacentToGuide(
                        neighbour,
                        guideSet))
                {
                    continue;
                }


                int walkableNeighbours =
                    CountWalkableNeighbours(
                        grid,
                        neighbour
                    );


                if (walkableNeighbours < 1)
                    continue;


                /*
                 * Dense areas are slightly more likely to join the
                 * growing passage, similar to a local CA birth rule.
                 */
                float localChance =
                    sideGrowthChance +
                    Mathf.Clamp(
                        (walkableNeighbours - 1) *
                        0.10f,
                        0f,
                        0.30f
                    );


                if (random.NextDouble() >
                    localChance)
                {
                    continue;
                }


                sideGrowth.Add(
                    neighbour
                );
            }


            totalDynamicCellsAdded +=
                grid.AddDynamicFloorCells(
                    sideGrowth
                );


            if (i %
                    Mathf.Max(
                        1,
                        renderEverySteps) ==
                0)
            {
                RefreshRuntimePresentation();
            }


            yield return new WaitForSeconds(
                growthStepDelay
            );
        }


        /*
         * One final local smoothing pass.
         *
         * Only additions are allowed. The guaranteed guide is never
         * removed, so the new passage cannot lose connectivity.
         */
        List<Vector2Int> smoothingCells =
            new List<Vector2Int>();


        foreach (Vector2Int guideCell in
                 guideCells)
        {
            foreach (Vector2Int neighbour in
                     GetMooreNeighbours(
                         guideCell))
            {
                if (grid.IsWalkable(
                        neighbour))
                {
                    continue;
                }


                if (!IsAdjacentToGuide(
                        neighbour,
                        guideSet))
                {
                    continue;
                }


                if (CountWalkableNeighbours(
                        grid,
                        neighbour) >= 4)
                {
                    smoothingCells.Add(
                        neighbour
                    );
                }
            }
        }


        totalDynamicCellsAdded +=
            grid.AddDynamicFloorCells(
                smoothingCells
            );


        RefreshRuntimePresentation();


        modifyingTerrain =
            false;


        UnityEngine.Debug.Log(
            "========== SHAPER GROWTH COMPLETE ==========\n" +
            $"Guide cells: {guideCells.Count}\n" +
            $"Dynamic cells added: {totalDynamicCellsAdded}\n" +
            $"Total dynamic floor cells: {grid.DynamicFloorCellCount}\n" +
            "============================================"
        );


        if (onComplete != null)
        {
            onComplete();
        }
    }


    private void RefreshRuntimePresentation()
    {
        dungeonGenerator.RefreshDungeonVisuals();


        if (playerVisionController != null)
        {
            playerVisionController
                .ForceRefreshVisibility();
        }
    }


    private IEnumerable<Vector2Int> GetMooreNeighbours(
        Vector2Int cell)
    {
        for (int x = -1;
             x <= 1;
             x++)
        {
            for (int y = -1;
                 y <= 1;
                 y++)
            {
                if (x == 0 &&
                    y == 0)
                {
                    continue;
                }


                yield return
                    new Vector2Int(
                        cell.x + x,
                        cell.y + y
                    );
            }
        }
    }


    private int CountWalkableNeighbours(
        DungeonGrid grid,
        Vector2Int cell)
    {
        int count = 0;


        foreach (Vector2Int neighbour in
                 GetMooreNeighbours(
                     cell))
        {
            if (grid.IsWalkable(
                    neighbour))
            {
                count++;
            }
        }


        return count;
    }


    private bool IsAdjacentToGuide(
        Vector2Int cell,
        HashSet<Vector2Int> guideCells)
    {
        foreach (Vector2Int neighbour in
                 GetMooreNeighbours(
                     cell))
        {
            if (guideCells.Contains(
                    neighbour))
            {
                return true;
            }
        }


        return false;
    }
}