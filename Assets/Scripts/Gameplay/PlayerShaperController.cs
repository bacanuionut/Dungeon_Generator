using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Controls dynamic player-directed Shaper use.
///
/// Any solid wall immediately in front of the player is tested. If a
/// different existing room or corridor can be reached through the
/// solid space broadly ahead, that wall becomes shapeable.
/// </summary>
public class PlayerShaperController : MonoBehaviour
{
    [Header("References")]

    [SerializeField]
    private DungeonGenerator dungeonGenerator;

    [SerializeField]
    private PlayerController playerController;

    [SerializeField]
    private DungeonTerrainModifier terrainModifier;

    [SerializeField]
    private RunStatsManager runStatsManager;

    [SerializeField]
    private GameplayTutorialController gameplayTutorialController;

    [Header("Inventory")]

    [SerializeField]
    private int startingCharges = 1;

    [Tooltip("Maximum number of Shaper charges the player may carry.")]
    [SerializeField]
    private int maximumCharges = 3;

    [Header("Controls")]

    [SerializeField]
    private KeyCode shaperKey =
        KeyCode.F;


    [Header("Dynamic Search")]

    [Tooltip(
        "Minimum number of solid cells the Shaper should cross. " +
        "This prevents trivial openings being treated as major uses."
    )]
    [SerializeField]
    private int minimumSolidCells = 2;

    [Tooltip(
        "Maximum number of solid guide cells that one Shaper use " +
        "may cross."
    )]
    [SerializeField]
    private int maximumSolidCells = 16;

    [Tooltip(
        "Maximum sideways deviation from the player's facing line. " +
        "This allows nearby offset rooms to be reached without the " +
        "search turning somewhere unrelated."
    )]
    [SerializeField]
    private int maximumSideDeviation = 5;


    [Header("Wall Highlight")]

    [SerializeField]
    private Color validWallColour =
        new Color(
            0.45f,
            0.75f,
            1f,
            0.45f
        );

    [SerializeField]
    private float highlightScale = 0.94f;


    private int remainingCharges;


    private ShaperPathResult currentTarget;


    private Vector2Int previousPlayerCell;

    private Vector2Int previousFacing;

    private int previousGenerationVersion = -1;


    private bool targetStateRecorded;


    private GameObject wallHighlight;

    private Material highlightMaterial;


    public int RemainingCharges =>
        remainingCharges;

    public KeyCode DiggerKey =>
        shaperKey;

    public event System.Action<int> ChargesChanged;


    public bool HasValidTarget =>
        currentTarget != null;


    private void Start()
    {
        if (gameplayTutorialController == null)
        {
            gameplayTutorialController =
                FindObjectOfType<GameplayTutorialController>();
        }

        ResetInventoryForNewRun();

        CreateWallHighlight();

        targetStateRecorded =
            false;
    }


    public void ResetInventoryForNewRun()
    {
        if (runStatsManager == null)
        {
            runStatsManager =
                FindObjectOfType<RunStatsManager>();
        }

        int targetStartingCharges =
            runStatsManager != null &&
            runStatsManager.RunActive
                ? runStatsManager.StartingDiggerCharges
                : startingCharges;

        remainingCharges =
            Mathf.Clamp(
                targetStartingCharges,
                0,
                Mathf.Max(
                    0,
                    maximumCharges
                )
            );

        ChargesChanged?.Invoke(
            remainingCharges
        );

        targetStateRecorded =
            false;

        UnityEngine.Debug.Log(
            $"SHAPER CHARGES: {remainingCharges}"
        );
    }


    private void Update()
    {
        if (dungeonGenerator == null ||
            dungeonGenerator.Grid == null ||
            playerController == null ||
            terrainModifier == null ||
            !playerController.IsAlive)
        {
            HideWallHighlight();
            return;
        }


        Vector2Int playerCell =
            playerController.GridPosition;


        Vector2Int facing =
            playerController.FacingDirection;


        if (!targetStateRecorded ||
            playerCell !=
                previousPlayerCell ||
            facing !=
                previousFacing ||
            previousGenerationVersion !=
                dungeonGenerator.GenerationVersion)
        {
            RefreshTarget();


            previousPlayerCell =
                playerCell;

            previousFacing =
                facing;

            previousGenerationVersion =
                dungeonGenerator.GenerationVersion;


            targetStateRecorded =
                true;
        }


        if (Input.GetKeyDown(
                shaperKey))
        {
            TryUseShaper();
        }
    }


    /// <summary>
    /// Dynamically tests the wall directly in front of the player.
    /// </summary>
    private void RefreshTarget()
    {
        currentTarget =
            null;


        HideWallHighlight();


        if (remainingCharges <= 0 ||
            terrainModifier.IsModifyingTerrain)
        {
            return;
        }


        Vector2Int playerCell =
            playerController.GridPosition;


        Vector2Int facing =
            playerController.FacingDirection;


        if (facing ==
            Vector2Int.zero)
        {
            return;
        }


        Vector2Int frontCell =
            playerCell +
            facing;


        /*
         * Ordinary open floor is not a valid Shaper starting point.
         *
         * A solid environmental prop is different. Props such as tables,
         * sacks, crates and rubble occupy an underlying walkable floor cell,
         * but the navigation layer marks that cell as blocked. In that case
         * the dynamic pathfinder can look through the breakable prop and
         * find the real wall behind it.
         */
        if (dungeonGenerator.Grid.IsWalkable(
                frontCell) &&
            !dungeonGenerator.Grid.IsNavigationBlocked(
                frontCell))
        {
            return;
        }


        ShaperPathResult target;


        if (!DynamicShaperPathfinder.TryFindPath(
                dungeonGenerator,
                playerCell,
                facing,
                minimumSolidCells,
                maximumSolidCells,
                maximumSideDeviation,
                out target))
        {
            return;
        }


        currentTarget =
            target;


        ShowWallHighlight(
            target,
            playerCell,
            facing
        );
    }


    private void TryUseShaper()
    {
        if (remainingCharges <= 0)
        {
            UnityEngine.Debug.Log(
                "NO SHAPER CHARGES REMAINING"
            );

            return;
        }


        if (terrainModifier.IsModifyingTerrain)
        {
            UnityEngine.Debug.Log(
                "SHAPER ALREADY MODIFYING TERRAIN"
            );

            return;
        }


        /*
         * Search again immediately before spending the charge so a
         * cached route can never be used from an old player position.
         */
        RefreshTarget();


        if (currentTarget == null)
        {
            UnityEngine.Debug.Log(
                "NO VALID SHAPER DESTINATION IN THIS DIRECTION"
            );

            return;
        }


        ShaperPathResult selectedTarget =
            currentTarget;


        int operationSeed =
            unchecked(
                dungeonGenerator.CurrentSeed *
                    1597 +
                dungeonGenerator.GenerationVersion *
                    8191 +
                selectedTarget.OriginCell.x *
                    73856093 +
                selectedTarget.OriginCell.y *
                    19349663 +
                playerController.FacingDirection.x *
                    83492791 +
                playerController.FacingDirection.y *
                    297121507
            );


        int chargesBeforeUse =
            remainingCharges;

        remainingCharges =
            Mathf.Max(
                0,
                remainingCharges - 1
            );

        ChargesChanged?.Invoke(remainingCharges);

        bool started =
            terrainModifier.TryCarvePath(
                selectedTarget.GuideCells,
                operationSeed,
                () =>
                {
                    UnityEngine.Debug.Log(
                        "SHAPER CONNECTED TO EXISTING FLOOR - " +
                        $"Target cell {selectedTarget.TargetCell}"
                    );


                    targetStateRecorded =
                        false;
                }
            );


        if (!started)
        {
            remainingCharges =
                chargesBeforeUse;

            ChargesChanged?.Invoke(remainingCharges);

            UnityEngine.Debug.Log(
                "SHAPER COULD NOT START TERRAIN MODIFICATION"
            );

            return;
        }


        if (runStatsManager != null)
        {
            runStatsManager.RecordShaperChargeUsed(
                1
            );
        }

        currentTarget =
            null;


        HideWallHighlight();


        UnityEngine.Debug.Log(
            "========== SHAPER ACTIVATED ==========\n" +
            $"Starting cell: {selectedTarget.OriginCell}\n" +
            $"First wall: {selectedTarget.WallCell}\n" +
            $"Target floor: {selectedTarget.TargetCell}\n" +
            $"Solid guide cells: {selectedTarget.SolidCellCount}\n" +
            $"Remaining charges: {remainingCharges}\n" +
            "======================================"
        );
    }


    /// <summary>
    /// Creates a subtle overlay on the wall rather than permanently
    /// modifying the generated wall's own material.
    /// </summary>
    private void CreateWallHighlight()
    {
        wallHighlight =
            GameObject.CreatePrimitive(
                PrimitiveType.Quad
            );


        wallHighlight.name =
            "Shaper Wall Highlight";


        wallHighlight.transform.SetParent(
            transform,
            false
        );


        wallHighlight.transform.localScale =
            new Vector3(
                highlightScale,
                highlightScale,
                1f
            );


        Collider collider =
            wallHighlight.GetComponent<Collider>();


        if (collider != null)
        {
            Destroy(
                collider
            );
        }


        Renderer renderer =
            wallHighlight.GetComponent<Renderer>();


        Shader shader =
            Shader.Find(
                "Sprites/Default"
            );


        if (shader == null)
        {
            shader =
                Shader.Find(
                    "Unlit/Color"
                );
        }


        if (renderer != null &&
            shader != null)
        {
            highlightMaterial =
                new Material(
                    shader
                );


            highlightMaterial.color =
                validWallColour;


            renderer.sharedMaterial =
                highlightMaterial;
        }


        wallHighlight.SetActive(
            false
        );
    }


    private void ShowWallHighlight(
        ShaperPathResult target,
        Vector2Int playerCell,
        Vector2Int facing)
    {
        if (wallHighlight == null ||
            target == null)
        {
            return;
        }


        /*
         * WallCell is already the first genuine dungeon wall selected by
         * DynamicShaperPathfinder. If a table, sack, crate or other solid
         * environmental prop sits between the player and that wall, the
         * pathfinder skips the prop before assigning WallCell.
         *
         * Position the highlight directly from the grid coordinate rather
         * than from a local offset. This keeps it on the actual wall even
         * when a prop visually hugs the wall or spans more than one cell.
         */
        Vector2Int highlightCell =
            target.WallCell;


        wallHighlight.transform.position =
            new Vector3(
                highlightCell.x + 0.5f,
                highlightCell.y + 0.5f,
                transform.position.z + 0.3f
            );


        wallHighlight.SetActive(
            true
        );
    }


    private void HideWallHighlight()
    {
        if (wallHighlight != null)
        {
            wallHighlight.SetActive(
                false
            );
        }
    }


    private void OnDestroy()
    {
        if (highlightMaterial != null)
        {
            Destroy(
                highlightMaterial
            );
        }
    }

    /// <summary>
    /// Adds Shaper ammunition found during exploration.
    ///
    /// Returns the number of charges actually added.
    /// </summary>
    public int AddCharges(
        int amount)
    {
        if (amount <= 0)
            return 0;


        int previous =
            remainingCharges;


        remainingCharges =
            Mathf.Clamp(
                remainingCharges +
                    amount,
                0,
                Mathf.Max(
                    0,
                    maximumCharges
                )
            );


        int added =
            remainingCharges -
            previous;


        if (added > 0)
        {
            ChargesChanged?.Invoke(remainingCharges);

            UnityEngine.Debug.Log(
                $"SHAPER AMMO COLLECTED +{added}. " +
                $"Current charges: {remainingCharges}/{maximumCharges}"
            );

            if (gameplayTutorialController == null)
            {
                gameplayTutorialController =
                    FindObjectOfType<GameplayTutorialController>();
            }

            if (gameplayTutorialController != null)
            {
                gameplayTutorialController.NotifyDiggerCollected();
            }
        }


        return added;
    }


    /// <summary>
    /// Finds the nearest reachable position where a valid Digger connection
    /// can be demonstrated without changing the dungeon grid.
    /// </summary>
    public bool TryFindTutorialTarget(
        int searchRadius,
        out ShaperPathResult target)
    {
        target = null;

        if (dungeonGenerator == null ||
            dungeonGenerator.Grid == null ||
            playerController == null)
        {
            return false;
        }

        int radius =
            Mathf.Max(
                0,
                searchRadius
            );

        Vector2Int startCell =
            playerController.GridPosition;

        Vector2Int[] directions =
        {
            Vector2Int.up,
            Vector2Int.right,
            Vector2Int.down,
            Vector2Int.left
        };

        Queue<Vector2Int> frontier =
            new Queue<Vector2Int>();

        Dictionary<Vector2Int, int> distances =
            new Dictionary<Vector2Int, int>();

        frontier.Enqueue(
            startCell
        );

        distances[startCell] =
            0;

        while (frontier.Count > 0)
        {
            Vector2Int originCell =
                frontier.Dequeue();

            int distance =
                distances[originCell];

            if (TryFindTutorialTargetFromOrigin(
                    originCell,
                    directions,
                    out target))
            {
                return true;
            }

            if (distance >= radius)
            {
                continue;
            }

            foreach (Vector2Int direction in
                     directions)
            {
                Vector2Int neighbour =
                    originCell +
                    direction;

                if (distances.ContainsKey(
                        neighbour))
                {
                    continue;
                }

                if (!dungeonGenerator.Grid.IsNavigable(
                        neighbour))
                {
                    continue;
                }

                distances[neighbour] =
                    distance + 1;

                frontier.Enqueue(
                    neighbour
                );
            }
        }

        return false;
    }


    private bool TryFindTutorialTargetFromOrigin(
        Vector2Int originCell,
        Vector2Int[] directions,
        out ShaperPathResult target)
    {
        target = null;

        if (!dungeonGenerator.Grid.IsNavigable(
                originCell))
        {
            return false;
        }

        foreach (Vector2Int direction in
                 directions)
        {
            Vector2Int frontCell =
                originCell +
                direction;

            if (dungeonGenerator.Grid.IsWalkable(
                    frontCell) &&
                !dungeonGenerator.Grid.IsNavigationBlocked(
                    frontCell))
            {
                continue;
            }

            if (DynamicShaperPathfinder.TryFindPath(
                    dungeonGenerator,
                    originCell,
                    direction,
                    minimumSolidCells,
                    maximumSolidCells,
                    maximumSideDeviation,
                    out target))
            {
                return true;
            }
        }

        return false;
    }


    public int MaximumCharges =>
        maximumCharges;
}