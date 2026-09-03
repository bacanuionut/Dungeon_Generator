using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Handles progressive runtime modification of the dungeon.
///
/// A hidden guide guarantees that a Shaper tunnel reaches its target.
/// The final tunnel shape is generated once when the Shaper activates.
///
/// Width varies gradually along the route, the two sides vary
/// independently, and occasional bulges / rough edges prevent the
/// result becoming a uniform rectangular tube.
///
/// Once generation has started the complete growth plan is independent
/// of the player's later position or facing direction.
/// </summary>
public class DungeonTerrainModifier : MonoBehaviour
{
    [Header("References")]

    [SerializeField]
    private DungeonGenerator dungeonGenerator;

    [SerializeField]
    private PlayerVisionController playerVisionController;


    [Header("Growth Speed")]

    [Tooltip("Delay between successive forward growth steps.")]
    [SerializeField]
    private float growthStepDelay = 0.08f;

    [Tooltip("Refresh the dungeon after this many forward steps.")]
    [SerializeField]
    private int renderEverySteps = 3;


    [Header("Organic Tunnel Shape")]

    [Tooltip(
        "Minimum distance the tunnel may grow to either side " +
        "of its guaranteed centre path."
    )]
    [SerializeField]
    private int minimumHalfWidth = 0;

    [Tooltip(
        "Normal maximum distance the tunnel may grow to either " +
        "side of its centre path."
    )]
    [SerializeField]
    private int maximumHalfWidth = 2;

    [Tooltip(
        "Chance that either side of the tunnel changes width at " +
        "the next forward growth step."
    )]
    [Range(0f, 1f)]
    [SerializeField]
    private float widthChangeChance = 0.45f;

    [Tooltip(
        "Chance of producing a temporary wider section."
    )]
    [Range(0f, 1f)]
    [SerializeField]
    private float bulgeChance = 0.18f;

    [Tooltip(
        "Extra width allowed during an occasional bulge."
    )]
    [SerializeField]
    private int bulgeExtraWidth = 1;

    [Tooltip(
        "Chance that an outer boundary cell is omitted, creating " +
        "a rougher edge."
    )]
    [Range(0f, 1f)]
    [SerializeField]
    private float edgeRoughness = 0.28f;


    private bool modifyingTerrain;


    public bool IsModifyingTerrain =>
        modifyingTerrain;

    [Header("Warden Excavation")]

    [Tooltip(
    "Time required for the Warden to break one solid growth step."
)]
    [SerializeField]
    private float wardenDigDelay = 0.65f;

    [Tooltip(
        "Chance of creating a small irregular side chip while the Warden " +
        "breaks through solid terrain."
    )]
    [Range(0f, 1f)]
    [SerializeField]
    private float wardenSideChipChance = 0.12f;


    /// <summary>
    /// Generates the complete organic tunnel footprint before visible
    /// carving begins.
    ///
    /// This is important because the operation can then continue even
    /// if the player turns around, moves away or changes rooms.
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


        /*
         * IMPORTANT:
         *
         * The entire tunnel shape is decided NOW.
         *
         * Nothing about later player movement, facing or visibility
         * can change this growth plan.
         */
        List<List<Vector2Int>> growthPlan =
            BuildOrganicGrowthPlan(
                guideCopy,
                operationSeed
            );


        if (growthPlan.Count == 0)
        {
            return false;
        }


        StartCoroutine(
            CarvePathSequence(
                guideCopy,
                growthPlan,
                onComplete
            )
        );


        return true;
    }


    /// <summary>
    /// Generates an irregular but connected tunnel around the guide.
    ///
    /// The left and right widths perform independent bounded random
    /// walks. This means the tunnel can naturally widen and narrow
    /// rather than having the same width for its entire length.
    /// </summary>
    private List<List<Vector2Int>> BuildOrganicGrowthPlan(
        List<Vector2Int> guideCells,
        int operationSeed)
    {
        List<List<Vector2Int>> plan =
            new List<List<Vector2Int>>();


        if (guideCells == null ||
            guideCells.Count == 0)
        {
            return plan;
        }


        System.Random random =
            new System.Random(
                operationSeed
            );


        int safeMinimumWidth =
            Mathf.Max(
                0,
                minimumHalfWidth
            );


        int safeMaximumWidth =
            Mathf.Max(
                safeMinimumWidth,
                maximumHalfWidth
            );


        /*
         * Left and right begin independently.
         *
         * This immediately prevents the passage from always being
         * symmetrical around its guide.
         */
        int leftWidth =
            random.Next(
                safeMinimumWidth,
                safeMaximumWidth + 1
            );


        int rightWidth =
            random.Next(
                safeMinimumWidth,
                safeMaximumWidth + 1
            );


        for (int i = 0;
             i < guideCells.Count;
             i++)
        {
            Vector2Int centre =
                guideCells[i];


            /*
             * Width evolves gradually instead of being randomly
             * regenerated every cell. This produces coherent organic
             * sections rather than visual noise.
             */
            if (i > 0)
            {
                leftWidth =
                    MutateWidth(
                        leftWidth,
                        safeMinimumWidth,
                        safeMaximumWidth,
                        random
                    );


                rightWidth =
                    MutateWidth(
                        rightWidth,
                        safeMinimumWidth,
                        safeMaximumWidth,
                        random
                    );
            }


            int effectiveLeft =
                leftWidth;


            int effectiveRight =
                rightWidth;


            /*
             * Occasional asymmetric bulges.
             *
             * Usually only one side expands, which creates caves /
             * pockets rather than merely making the entire passage
             * uniformly wider.
             */
            if (random.NextDouble() <
                bulgeChance)
            {
                if (random.NextDouble() <
                    0.5)
                {
                    effectiveLeft +=
                        Mathf.Max(
                            0,
                            bulgeExtraWidth
                        );
                }
                else
                {
                    effectiveRight +=
                        Mathf.Max(
                            0,
                            bulgeExtraWidth
                        );
                }
            }


            /*
             * Avoid cutting a huge opening through the first or final
             * wall. The middle of the tunnel is free to vary more.
             */
            if (i == 0 ||
                i ==
                    guideCells.Count - 1)
            {
                effectiveLeft =
                    Mathf.Min(
                        effectiveLeft,
                        1
                    );


                effectiveRight =
                    Mathf.Min(
                        effectiveRight,
                        1
                    );
            }


            Vector2Int tangent =
                DetermineGuideDirection(
                    guideCells,
                    i
                );


            Vector2Int perpendicular =
                new Vector2Int(
                    -tangent.y,
                    tangent.x
                );


            HashSet<Vector2Int> stepCells =
                new HashSet<Vector2Int>();


            /*
             * This cell can NEVER be omitted.
             *
             * It is the connectivity guarantee.
             */
            stepCells.Add(
                centre
            );


            AddOrganicSide(
                stepCells,
                centre,
                perpendicular,
                effectiveLeft,
                random
            );


            AddOrganicSide(
                stepCells,
                centre,
                -perpendicular,
                effectiveRight,
                random
            );


            /*
             * Occasionally make a small irregular pocket beside the
             * current growth front.
             */
            TryAddLocalPocket(
                stepCells,
                centre,
                tangent,
                perpendicular,
                effectiveLeft,
                effectiveRight,
                random
            );


            plan.Add(
                new List<Vector2Int>(
                    stepCells
                )
            );
        }


        return plan;
    }


    /// <summary>
    /// Randomly changes one side's width by at most one cell.
    ///
    /// Because this is a bounded random walk, width changes gradually:
    ///
    /// 1, 1, 2, 2, 2, 1, 0, 1 ...
    ///
    /// rather than:
    ///
    /// 1, 4, 0, 3, 1 ...
    /// </summary>
    private int MutateWidth(
        int currentWidth,
        int minimumWidth,
        int maximumWidth,
        System.Random random)
    {
        if (random.NextDouble() >
            widthChangeChance)
        {
            return currentWidth;
        }


        int change =
            random.Next(
                0,
                2
            ) == 0
                ? -1
                : 1;


        return Mathf.Clamp(
            currentWidth +
            change,
            minimumWidth,
            maximumWidth
        );
    }


    /// <summary>
    /// Adds one irregular side of the tunnel.
    ///
    /// Only the outermost cell is eligible for rough-edge removal.
    /// Inner cells remain filled so detached islands are not created.
    /// </summary>
    private void AddOrganicSide(
        HashSet<Vector2Int> stepCells,
        Vector2Int centre,
        Vector2Int sideDirection,
        int width,
        System.Random random)
    {
        if (width <= 0)
            return;


        for (int distance = 1;
             distance <= width;
             distance++)
        {
            bool outerEdge =
                distance ==
                width;


            /*
             * Occasionally omit only the boundary cell.
             *
             * The centre path remains intact and wider sections do not
             * become internally disconnected.
             */
            if (outerEdge &&
                random.NextDouble() <
                    edgeRoughness)
            {
                continue;
            }


            Vector2Int cell =
                centre +
                sideDirection *
                distance;


            stepCells.Add(
                cell
            );
        }
    }


    /// <summary>
    /// Produces occasional asymmetric pockets around the passage.
    ///
    /// These are deliberately uncommon. Too many would make the tunnel
    /// look noisy instead of naturally irregular.
    /// </summary>
    private void TryAddLocalPocket(
        HashSet<Vector2Int> stepCells,
        Vector2Int centre,
        Vector2Int tangent,
        Vector2Int perpendicular,
        int leftWidth,
        int rightWidth,
        System.Random random)
    {
        if (random.NextDouble() >
            bulgeChance * 0.65f)
        {
            return;
        }


        bool useLeft =
            random.NextDouble() <
            0.5;


        Vector2Int side =
            useLeft
                ? perpendicular
                : -perpendicular;


        int currentWidth =
            useLeft
                ? leftWidth
                : rightWidth;


        int pocketDistance =
            Mathf.Max(
                1,
                currentWidth + 1
            );


        Vector2Int pocketCell =
            centre +
            side *
            pocketDistance;


        /*
         * Shift the pocket slightly forward or backwards so the edge
         * is not a perfectly perpendicular stripe.
         */
        double directionRoll =
            random.NextDouble();


        if (directionRoll <
            0.33)
        {
            pocketCell +=
                tangent;
        }
        else if (directionRoll <
                 0.66)
        {
            pocketCell -=
                tangent;
        }


        stepCells.Add(
            pocketCell
        );
    }


    /// <summary>
    /// Determines the local direction of the hidden guide.
    ///
    /// At a corner we favour the direction the path is about to travel,
    /// allowing the grown shape to naturally flow around bends.
    /// </summary>
    private Vector2Int DetermineGuideDirection(
        List<Vector2Int> guideCells,
        int index)
    {
        if (guideCells.Count == 1)
        {
            return Vector2Int.right;
        }


        if (index <
            guideCells.Count - 1)
        {
            Vector2Int forward =
                guideCells[index + 1] -
                guideCells[index];


            if (forward !=
                Vector2Int.zero)
            {
                return forward;
            }
        }


        if (index > 0)
        {
            Vector2Int previous =
                guideCells[index] -
                guideCells[index - 1];


            if (previous !=
                Vector2Int.zero)
            {
                return previous;
            }
        }


        return Vector2Int.right;
    }


    /// <summary>
    /// Progressively reveals the pre-generated tunnel plan.
    ///
    /// The player may freely move or turn while this runs. Nothing in
    /// this coroutine reads PlayerController or player facing.
    /// </summary>
    private IEnumerator CarvePathSequence(
        List<Vector2Int> guideCells,
        List<List<Vector2Int>> growthPlan,
        Action onComplete)
    {
        modifyingTerrain =
            true;


        DungeonGrid grid =
            dungeonGenerator.Grid;


        int totalDynamicCellsAdded =
            0;


        UnityEngine.Debug.Log(
            "SHAPER GROWTH STARTED - " +
            $"Guide cells: {guideCells.Count}, " +
            $"planned growth steps: {growthPlan.Count}"
        );


        int safeRenderInterval =
            Mathf.Max(
                1,
                renderEverySteps
            );


        WaitForSeconds growthWait =
            growthStepDelay > 0f
                ? new WaitForSeconds(
                    growthStepDelay
                )
                : null;


        for (int i = 0;
             i < growthPlan.Count;
             i++)
        {
            int addedThisStep =
                grid.AddDynamicFloorCells(
                    growthPlan[i]
                );


            totalDynamicCellsAdded +=
                addedThisStep;


            /*
             * The logical terrain can continue growing every step without
             * rebuilding the complete visual representation every step.
             *
             * A visual refresh is performed periodically and always on the
             * final growth step.
             */
            bool finalStep =
                i ==
                growthPlan.Count - 1;


            bool scheduledRefresh =
                (i + 1) %
                safeRenderInterval ==
                0;


            if (scheduledRefresh ||
                finalStep)
            {
                RefreshRuntimePresentation();
            }


            if (growthWait != null)
            {
                yield return growthWait;
            }
            else
            {
                yield return null;
            }
        }


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
        if (dungeonGenerator != null)
        {
            dungeonGenerator
                .RefreshDungeonVisuals();
        }


        if (playerVisionController != null)
        {
            playerVisionController
                .ForceRefreshVisibility();
        }
    }

    /// <summary>
    /// Slowly converts one solid cell into floor for the Warden.
    ///
    /// This uses the same shared runtime terrain system as the player's
    /// Shaper, but the Warden excavates one step at a time rather than
    /// opening an entire passage in one activation.
    /// </summary>
    public bool TryCarveWardenCell(
        Vector2Int targetCell,
        int operationSeed,
        Action onComplete)
    {
        if (modifyingTerrain ||
            dungeonGenerator == null ||
            dungeonGenerator.Grid == null)
        {
            return false;
        }


        if (dungeonGenerator.Grid.IsWalkable(
                targetCell))
        {
            if (onComplete != null)
            {
                onComplete();
            }


            return true;
        }


        StartCoroutine(
            CarveWardenCellSequence(
                targetCell,
                operationSeed,
                onComplete
            )
        );


        return true;
    }

    private IEnumerator CarveWardenCellSequence(
    Vector2Int targetCell,
    int operationSeed,
    Action onComplete)
    {
        modifyingTerrain =
            true;


        DungeonGrid grid =
            dungeonGenerator.Grid;


        UnityEngine.Debug.Log(
            $"WARDEN EXCAVATING - Cell {targetCell}"
        );


        /*
         * Unlike the player's fast growing Shaper, the Warden visibly
         * chips at one section of terrain before it becomes walkable.
         */
        yield return new WaitForSeconds(
            wardenDigDelay
        );


        List<Vector2Int> cellsToOpen =
            new List<Vector2Int>();


        cellsToOpen.Add(
            targetCell
        );


        System.Random random =
            new System.Random(
                operationSeed
            );


        /*
         * A small amount of deterministic local side damage prevents the
         * Warden's excavated route from always becoming a perfectly
         * one-cell-wide artificial line.
         */
        foreach (Vector2Int neighbour in
                 GetWardenMooreNeighbours(
                     targetCell))
        {
            if (grid.IsWalkable(
                    neighbour))
            {
                continue;
            }


            if (CountWardenWalkableNeighbours(
                    grid,
                    neighbour) < 2)
            {
                continue;
            }


            if (random.NextDouble() >
                wardenSideChipChance)
            {
                continue;
            }


            cellsToOpen.Add(
                neighbour
            );
        }


        int added =
            grid.AddDynamicFloorCells(
                cellsToOpen
            );


        RefreshRuntimePresentation();


        modifyingTerrain =
            false;


        UnityEngine.Debug.Log(
            $"WARDEN BROKE THROUGH - Cell {targetCell}. " +
            $"Dynamic cells added: {added}"
        );


        if (onComplete != null)
        {
            onComplete();
        }
    }

    private IEnumerable<Vector2Int> GetWardenMooreNeighbours(
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


                yield return new Vector2Int(
                    cell.x + x,
                    cell.y + y
                );
            }
        }
    }


    private int CountWardenWalkableNeighbours(
        DungeonGrid grid,
        Vector2Int cell)
    {
        int count =
            0;


        foreach (Vector2Int neighbour in
                 GetWardenMooreNeighbours(
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
}