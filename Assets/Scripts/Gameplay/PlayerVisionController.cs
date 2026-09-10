using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Controls directional player visibility and exploration.
///
/// Player facing controls the main field of view. A small immediate
/// area around the player remains visible to keep movement readable.
/// </summary>
public class PlayerVisionController : MonoBehaviour
{
    [Header("References")]

    [SerializeField]
    private DungeonGenerator dungeonGenerator;

    [SerializeField]
    private PlayerController playerController;

    [SerializeField]
    private FogOfWarRenderer fogRenderer;

    [Tooltip(
        "Procedural environment source used for static wall-torch lighting. " +
        "If left empty it is resolved from the DungeonGenerator GameObject."
    )]
    [SerializeField]
    private DungeonEnvironmentGenerator environmentGenerator;


    [Header("Vision")]

    [SerializeField]
    private float visionRange = 8f;

    [Range(20f, 180f)]
    [SerializeField]
    private float visionAngle = 90f;

    [Tooltip(
        "Small area immediately around the player that remains " +
        "visible regardless of facing direction."
    )]
    [SerializeField]
    private float immediateVisionRadius = 1.4f;


    private int activeGenerationVersion = -1;


    private Vector2Int previousPlayerPosition;

    private Vector2Int previousFacingDirection;


    private bool previousStateRecorded;


    private readonly HashSet<Vector2Int> visibleFloorCells =
        new HashSet<Vector2Int>();


    private readonly HashSet<Vector2Int> visibleDisplayCells =
        new HashSet<Vector2Int>();


    private readonly HashSet<Vector2Int> exploredDisplayCells =
        new HashSet<Vector2Int>();

    private readonly HashSet<Vector2Int> currentRegionDisplayCells =
        new HashSet<Vector2Int>();

    private readonly HashSet<Vector2Int> tutorialVisibleDisplayCells =
        new HashSet<Vector2Int>();


    private static readonly Vector2Int[] cardinalDirections =
    {
        Vector2Int.up,
        Vector2Int.down,
        Vector2Int.left,
        Vector2Int.right
    };


    private void Update()
    {
        if (dungeonGenerator == null ||
            dungeonGenerator.Grid == null ||
            playerController == null ||
            fogRenderer == null)
        {
            return;
        }


        if (activeGenerationVersion !=
            dungeonGenerator.GenerationVersion)
        {
            InitialiseCurrentFloor();
        }


        Vector2Int currentPosition =
            playerController.GridPosition;


        Vector2Int currentFacing =
            playerController.FacingDirection;


        if (!previousStateRecorded ||
            currentPosition !=
                previousPlayerPosition ||
            currentFacing !=
                previousFacingDirection)
        {
            RefreshVisibility();


            previousPlayerPosition =
                currentPosition;

            previousFacingDirection =
                currentFacing;

            previousStateRecorded =
                true;
        }
    }


    /// <summary>
    /// Resets exploration whenever DungeonGenerator creates a new
    /// procedural floor.
    /// </summary>
    private void InitialiseCurrentFloor()
    {
        activeGenerationVersion =
            dungeonGenerator.GenerationVersion;


        visibleFloorCells.Clear();

        visibleDisplayCells.Clear();

        exploredDisplayCells.Clear();

        currentRegionDisplayCells.Clear();

        tutorialVisibleDisplayCells.Clear();


        if (environmentGenerator == null &&
            dungeonGenerator != null)
        {
            environmentGenerator =
                dungeonGenerator.GetComponent<DungeonEnvironmentGenerator>();
        }


        fogRenderer.BuildForGrid(
            dungeonGenerator.Grid
        );


        previousStateRecorded =
            false;


        UnityEngine.Debug.Log(
            "FOG OF WAR INITIALISED - " +
            $"Seed {dungeonGenerator.CurrentSeed}, " +
            $"generation {activeGenerationVersion}"
        );
    }


    private void RefreshVisibility()
    {
        DungeonGrid grid =
            dungeonGenerator.Grid;


        Vector2Int playerCell =
            playerController.GridPosition;


        visibleFloorCells.Clear();

        visibleDisplayCells.Clear();


        List<Vector2Int> coneCells =
            DungeonVisibilityUtility
                .GetVisibleWalkableCellsInCone(
                    grid,
                    playerCell,
                    playerController.FacingDirection,
                    visionRange,
                    visionAngle
                );


        foreach (Vector2Int cell in coneCells)
        {
            visibleFloorCells.Add(
                cell
            );
        }


        AddImmediateVisibility(
            grid,
            playerCell
        );

        RefreshCurrentRegion(
            grid,
            playerCell
        );

        AddEnvironmentalTorchVisibility(
            grid
        );


        /*
         * The player should also be able to see wall faces bordering
         * visible floor.
         *
         * Only non-walkable neighbouring cells are added here.
         * Hidden floor behind walls is NOT revealed.
         */
        foreach (Vector2Int floorCell in
                 visibleFloorCells)
        {
            visibleDisplayCells.Add(
                floorCell
            );


            foreach (Vector2Int direction in
                     cardinalDirections)
            {
                Vector2Int neighbour =
                    floorCell +
                    direction;


                if (!grid.IsWalkable(
                        neighbour))
                {
                    visibleDisplayCells.Add(
                        neighbour
                    );
                }
            }
        }


        exploredDisplayCells.UnionWith(
            visibleDisplayCells
        );

        exploredDisplayCells.UnionWith(
            currentRegionDisplayCells
        );


        visibleDisplayCells.UnionWith(
            tutorialVisibleDisplayCells
        );


        fogRenderer.UpdateFog(
            visibleDisplayCells,
            currentRegionDisplayCells,
            exploredDisplayCells
        );
    }


    /// <summary>
    /// Promotes procedural torch-lit floor cells into the same fully-visible
    /// set used by the player's own light.
    ///
    /// Unexplored rooms are deliberately not revealed from across the map.
    /// A static torch becomes fully bright once its cell belongs to the
    /// player's current region, has already been explored, or is already
    /// visible through the player's directional vision.
    /// </summary>
    private void AddEnvironmentalTorchVisibility(
        DungeonGrid grid)
    {
        if (grid == null ||
            environmentGenerator == null ||
            environmentGenerator.TorchLitFloorCells == null)
        {
            return;
        }

        foreach (Vector2Int cell in
                 environmentGenerator.TorchLitFloorCells)
        {
            if (!grid.IsWalkable(cell))
                continue;

            bool torchRegionKnown =
                visibleFloorCells.Contains(cell) ||
                currentRegionDisplayCells.Contains(cell) ||
                exploredDisplayCells.Contains(cell);

            if (!torchRegionKnown)
                continue;

            visibleFloorCells.Add(cell);
        }
    }


    /// <summary>
    /// Adds a small local visibility radius around the player.
    /// This prevents the directional torch from making immediate
    /// movement unnecessarily awkward.
    /// </summary>
    private void AddImmediateVisibility(
        DungeonGrid grid,
        Vector2Int playerCell)
    {
        int radius =
            Mathf.CeilToInt(
                immediateVisionRadius
            );


        for (int x = playerCell.x - radius;
             x <= playerCell.x + radius;
             x++)
        {
            for (int y = playerCell.y - radius;
                 y <= playerCell.y + radius;
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


                Vector2 difference =
                    new Vector2(
                        cell.x - playerCell.x,
                        cell.y - playerCell.y
                    );


                if (difference.magnitude >
                    immediateVisionRadius)
                {
                    continue;
                }


                if (!DungeonVisibilityUtility.HasLineOfSight(
                        grid,
                        playerCell,
                        cell))
                {
                    continue;
                }


                visibleFloorCells.Add(
                    cell
                );
            }
        }
    }

    /// <summary>
    /// Finds the generated room currently occupied by the player.
    /// Returns null while the player is travelling through a corridor.
    /// </summary>
    private Room FindCurrentRoom(
        Vector2Int playerCell)
    {
        if (dungeonGenerator.Rooms == null)
            return null;


        foreach (Room room in
                 dungeonGenerator.Rooms)
        {
            if (room != null &&
                room.Contains(playerCell))
            {
                return room;
            }
        }


        return null;
    }

    /// <summary>
    /// Finds the particular generated corridor containing the player.
    ///
    /// Individual corridor objects are used rather than flood-filling all
    /// corridor cells, because the full corridor network is connected and
    /// would otherwise reveal far too much of the dungeon.
    /// </summary>
    private CorridorGenerator.Corridor FindCurrentCorridor(
        Vector2Int playerCell)
    {
        if (dungeonGenerator.Corridors == null)
            return null;


        foreach (
            CorridorGenerator.Corridor corridor
            in dungeonGenerator.Corridors)
        {
            if (corridor == null ||
                corridor.Cells == null)
            {
                continue;
            }


            if (corridor.Cells.Contains(
                    playerCell))
            {
                return corridor;
            }
        }


        return null;
    }

    /// <summary>
    /// Reveals the complete walkable area belonging to one room.
    ///
    /// This provides ambient understanding of the current space while the
    /// directional torch still determines what is shown in full colour.
    /// </summary>
    private void AddCurrentRoomVisibility(
        DungeonGrid grid,
        Room room)
    {
        if (room == null)
            return;


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


                currentRegionDisplayCells.Add(
                    cell
                );
            }
        }


        AddRegionBoundaryHints(
            grid
        );
    }

    /// <summary>
    /// Adds organic CA-generated cells which directly connect to the
    /// current room.
    ///
    /// This lets the perceived room follow its post-processed shape rather
    /// than only revealing the original rectangular BSP core.
    /// </summary>
    private void AddConnectedOrganicRoomCells(
        DungeonGrid grid)
    {
        if (grid.OrganicRoomCells == null ||
            grid.OrganicRoomCellCount == 0)
        {
            return;
        }


        Queue<Vector2Int> frontier =
            new Queue<Vector2Int>();


        HashSet<Vector2Int> visited =
            new HashSet<Vector2Int>(
                currentRegionDisplayCells
            );


        foreach (Vector2Int cell in
                 currentRegionDisplayCells)
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
                     cardinalDirections)
            {
                Vector2Int neighbour =
                    current +
                    direction;


                if (visited.Contains(
                        neighbour))
                {
                    continue;
                }


                if (!grid.IsOrganicRoomCell(
                        neighbour))
                {
                    continue;
                }


                visited.Add(
                    neighbour
                );


                currentRegionDisplayCells.Add(
                    neighbour
                );


                frontier.Enqueue(
                    neighbour
                );
            }
        }
    }

    /// <summary>
    /// Reveals only the current straight section of a generated corridor.
    ///
    /// A generated corridor can contain an L-shaped path. Revealing the
    /// complete Corridor object would therefore expose another corridor
    /// section and potentially the distant room at its other end.
    ///
    /// Instead, the player's current straight corridor segment is
    /// revealed. At a bend or doorway only a small one-cell hint is shown.
    /// </summary>
    private void AddCurrentCorridorVisibility(
        DungeonGrid grid,
        CorridorGenerator.Corridor corridor,
        Vector2Int playerCell)
    {
        if (corridor == null ||
            corridor.Cells == null)
        {
            return;
        }


        HashSet<Vector2Int> corridorCells =
            new HashSet<Vector2Int>(
                corridor.Cells
            );


        if (!corridorCells.Contains(
                playerCell))
        {
            return;
        }

        Vector2Int axis =
            DetermineCorridorAxis(
                corridorCells,
                playerCell
            );


        currentRegionDisplayCells.Add(
            playerCell
        );


        Vector2Int firstEnd =
            ExpandCorridorSegment(
                corridorCells,
                playerCell,
                axis
            );


        Vector2Int secondEnd =
            ExpandCorridorSegment(
                corridorCells,
                playerCell,
                -axis
            );

        AddCorridorEndHints(
            grid,
            corridorCells,
            firstEnd
        );


        AddCorridorEndHints(
            grid,
            corridorCells,
            secondEnd
        );
    }

    /// <summary>
    /// Determines which straight axis of a corridor should currently be
    /// treated as active.
    ///
    /// At a bend, player facing is used so looking/turning into the new
    /// section immediately reveals that section.
    /// </summary>
    private Vector2Int DetermineCorridorAxis(
        HashSet<Vector2Int> corridorCells,
        Vector2Int playerCell)
    {
        bool hasLeft =
            corridorCells.Contains(
                playerCell +
                Vector2Int.left
            );


        bool hasRight =
            corridorCells.Contains(
                playerCell +
                Vector2Int.right
            );


        bool hasUp =
            corridorCells.Contains(
                playerCell +
                Vector2Int.up
            );


        bool hasDown =
            corridorCells.Contains(
                playerCell +
                Vector2Int.down
            );


        bool hasHorizontal =
            hasLeft ||
            hasRight;


        bool hasVertical =
            hasUp ||
            hasDown;


        /*
         * Straight corridor.
         */
        if (hasHorizontal &&
            !hasVertical)
        {
            return Vector2Int.right;
        }


        if (hasVertical &&
            !hasHorizontal)
        {
            return Vector2Int.up;
        }


        /*
         * Corner or unusual intersection.
         *
         * Prefer the direction the player is currently facing.
         */
        if (playerController != null)
        {
            Vector2Int facing =
                playerController.FacingDirection;


            if (facing.x != 0 &&
                hasHorizontal)
            {
                return Vector2Int.right;
            }


            if (facing.y != 0 &&
                hasVertical)
            {
                return Vector2Int.up;
            }
        }


        /*
         * Safe fallback for a corner where facing does not help.
         */
        if (hasVertical)
            return Vector2Int.up;


        return Vector2Int.right;
    }

    /// <summary>
    /// Reveals one additional walkable cell immediately outside the
    /// current room or corridor.
    ///
    /// This gives the player a small visual indication of exits and turns
    /// without revealing the adjoining area.
    /// </summary>
    private void AddRegionBoundaryHints(
        DungeonGrid grid)
    {
        List<Vector2Int> regionSnapshot =
            new List<Vector2Int>(
                currentRegionDisplayCells
            );


        foreach (Vector2Int regionCell in
                 regionSnapshot)
        {
            foreach (Vector2Int direction in
                     cardinalDirections)
            {
                Vector2Int neighbour =
                    regionCell +
                    direction;


                if (currentRegionDisplayCells.Contains(
                        neighbour))
                {
                    continue;
                }


                if (!grid.IsWalkable(
                        neighbour))
                {
                    continue;
                }


                currentRegionDisplayCells.Add(
                    neighbour
                );
            }
        }
    }

    /// <summary>
    /// Builds the dim ambient visibility for the player's current
    /// semantic region.
    /// </summary>
    private void RefreshCurrentRegion(
        DungeonGrid grid,
        Vector2Int playerCell)
    {
        currentRegionDisplayCells.Clear();


        Room currentRoom =
            FindCurrentRoom(
                playerCell
            );


        if (currentRoom != null)
        {
            AddCurrentRoomVisibility(
                grid,
                currentRoom
            );


            AddConnectedOrganicRoomCells(
                grid
            );


            AddRegionBoundaryHints(
                grid
            );


            return;
        }


        CorridorGenerator.Corridor currentCorridor =
            FindCurrentCorridor(
                playerCell
            );


        if (currentCorridor != null)
        {
            AddCurrentCorridorVisibility(
                grid,
                currentCorridor,
                playerCell
            );
        }
    }

    /// <summary>
    /// Expands visibility from the player's cell along one straight
    /// corridor direction.
    ///
    /// Expansion stops when the corridor ends or when the path reaches a
    /// turn. The turn cell itself is included.
    /// </summary>
    private Vector2Int ExpandCorridorSegment(
        HashSet<Vector2Int> corridorCells,
        Vector2Int startCell,
        Vector2Int direction)
    {
        Vector2Int current =
            startCell;


        while (true)
        {
            Vector2Int next =
                current +
                direction;


            if (!corridorCells.Contains(
                    next))
            {
                return current;
            }


            currentRegionDisplayCells.Add(
                next
            );


            current =
                next;

            if (HasPerpendicularCorridorConnection(
                    corridorCells,
                    current,
                    direction))
            {
                return current;
            }
        }
    }

    /// <summary>
    /// Returns true when a corridor cell connects to another corridor
    /// cell perpendicular to the current direction of travel.
    /// </summary>
    private bool HasPerpendicularCorridorConnection(
        HashSet<Vector2Int> corridorCells,
        Vector2Int cell,
        Vector2Int travelDirection)
    {
        if (travelDirection.x != 0)
        {
            // Travelling horizontally.
            // A vertical neighbour means this is a corner/junction.
            return
                corridorCells.Contains(
                    cell +
                    Vector2Int.up
                ) ||
                corridorCells.Contains(
                    cell +
                    Vector2Int.down
                );
        }


        // Travelling vertically.
        // A horizontal neighbour means this is a corner/junction.
        return
            corridorCells.Contains(
                cell +
                Vector2Int.left
            ) ||
            corridorCells.Contains(
                cell +
                Vector2Int.right
            );
    }

    /// <summary>
    /// Reveals only cells immediately neighbouring the end of the current
    /// corridor segment.
    ///
    /// This provides a hint of a bend, doorway or adjoining room without
    /// exposing the next complete region.
    /// </summary>
    private void AddCorridorEndHints(
        DungeonGrid grid,
        HashSet<Vector2Int> completeCorridor,
        Vector2Int endCell)
    {
        foreach (Vector2Int direction in
                 cardinalDirections)
        {
            Vector2Int neighbour =
                endCell +
                direction;


            if (currentRegionDisplayCells.Contains(
                    neighbour))
            {
                continue;
            }


            /*
             * If the neighbour belongs to the same generated corridor,
             * it is probably the first cell around an L-shaped bend.
             */
            if (completeCorridor.Contains(
                    neighbour))
            {
                currentRegionDisplayCells.Add(
                    neighbour
                );

                continue;
            }


            /*
             * Otherwise reveal only the immediately adjacent walkable
             * cell. At a corridor endpoint this gives a small glimpse
             * through the doorway into the adjoining room.
             */
            if (grid.IsWalkable(
                    neighbour))
            {
                currentRegionDisplayCells.Add(
                    neighbour
                );
            }
        }
    }

    /// <summary>
    /// Temporarily clears fog around a tutorial target without adding those
    /// cells to the player's explored map.
    /// </summary>
    public void SetTutorialFocusVisibility(
        Vector2Int centre,
        int radius)
    {
        tutorialVisibleDisplayCells.Clear();

        int safeRadius =
            Mathf.Max(
                0,
                radius
            );

        for (int x = -safeRadius;
             x <= safeRadius;
             x++)
        {
            for (int y = -safeRadius;
                 y <= safeRadius;
                 y++)
            {
                if (Mathf.Abs(x) +
                    Mathf.Abs(y) >
                    safeRadius)
                {
                    continue;
                }

                tutorialVisibleDisplayCells.Add(
                    new Vector2Int(
                        centre.x + x,
                        centre.y + y
                    )
                );
            }
        }

        ForceRefreshVisibility();
    }


    /// <summary>
    /// Removes the temporary visibility used by a tutorial camera focus.
    /// </summary>
    public void ClearTutorialFocusVisibility()
    {
        if (tutorialVisibleDisplayCells.Count == 0)
        {
            return;
        }

        tutorialVisibleDisplayCells.Clear();

        ForceRefreshVisibility();
    }


    /// <summary>
    /// Forces player visibility to be recalculated on the next frame.
    ///
    /// Runtime terrain modification uses this because newly created floor
    /// may immediately become visible through the player's torch.
    /// </summary>
    public void ForceRefreshVisibility()
    {
        previousStateRecorded =
            false;
    }
}