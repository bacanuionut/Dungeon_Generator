using UnityEngine;

/// <summary>
/// Maintains the Warden's pursuit across the complete dungeon run.
///
/// Previous dungeon floors are not kept loaded after the player
/// descends. While the Warden is on an earlier floor, its progress is
/// therefore represented abstractly.
///
/// Once the Warden reaches the player's current floor it becomes a
/// physical WardenController and uses weighted A* plus excavation.
///
/// Descending does not reset the Warden. The player simply gains one
/// additional floor of separation because the Warden remains where it
/// was.
/// </summary>
public class WardenManager : MonoBehaviour
{
    [Header("References")]

    [SerializeField]
    private DungeonGenerator dungeonGenerator;

    [SerializeField]
    private DungeonRunManager runManager;

    [SerializeField]
    private RunStatsManager runStatsManager;

    [SerializeField]
    private PlayerController playerController;

    [SerializeField]
    private DungeonTerrainModifier terrainModifier;

    [SerializeField]
    private AdaptiveDifficultyDirector difficultyDirector;

    [Header("Cross-Floor Pursuit")]

    [Tooltip(
        "Approximate time required for the abstract Warden to progress " +
        "through one complete floor while it is behind the player."
    )]
    [SerializeField]
    private float secondsPerFloor = 45f;

    [Tooltip(
        "Extra breathing room at the beginning of a run before the " +
        "Warden begins making abstract progress."
    )]
    [SerializeField]
    private float initialHeadStartSeconds = 20f;


    [Tooltip(
        "Small delay between the Warden reaching the player's floor " +
        "and physically entering through that floor's start room."
    )]
    [SerializeField]
    private float physicalArrivalDelay = 2f;


    [Header("Starting Separation")]

    [Tooltip(
        "The Warden begins above Floor 1. A value of 1 means the " +
        "player starts one complete floor ahead."
    )]
    [SerializeField]
    private int startingFloorsBehind = 1;


    // ------------------------------------------------------------
    // RUN-LEVEL PURSUIT STATE
    // ------------------------------------------------------------

    /*
     * Floor 0 represents the area above the first playable dungeon
     * floor.
     *
     * Example:
     *
     * Player Floor = 1
     * Warden Floor = 0
     *
     * Separation = 1 floor.
     */
    private int wardenFloor;


    /*
     * Progress through the Warden's CURRENT abstract floor.
     *
     * 0 = just began following that floor
     * 1 = completed it and advances to the next floor
     */
    private float pursuitProgress;

    private float pursuitStartTime;

    private int previousPlayerFloor;

    private int observedGenerationVersion = -1;


    private bool runInitialised;

    private bool runFinished;

    private bool previousRunActive;


    // ------------------------------------------------------------
    // PHYSICAL ARRIVAL
    // ------------------------------------------------------------

    private bool physicalSpawnScheduled;

    private float physicalSpawnTime;


    private WardenController activeWarden;


    // ------------------------------------------------------------
    // PUBLIC STATE FOR HUD / EVALUATION
    // ------------------------------------------------------------

    public WardenController ActiveWarden =>
        activeWarden;


    public int WardenFloor =>
        wardenFloor;


    public float PursuitProgress =>
        pursuitProgress;


    public bool IsPhysicalWardenPresent =>
        activeWarden != null;


    public bool IsWardenArriving =>
        physicalSpawnScheduled;


    /// <summary>
    /// Number of complete dungeon floors separating the player and
    /// Warden.
    ///
    /// Zero means the Warden has reached the player's current floor.
    /// </summary>
    public int FloorsBehind
    {
        get
        {
            if (runManager == null)
                return 0;


            return Mathf.Max(
                0,
                runManager.CurrentFloor -
                wardenFloor
            );
        }
    }


    /// <summary>
    /// Approximate seconds before the Warden completes its current
    /// abstract floor.
    ///
    /// Mainly useful for runtime diagnostics and evaluation.
    /// </summary>
    public float SecondsUntilNextFloor
    {
        get
        {
            if (secondsPerFloor <= 0f)
                return 0f;


            float pursuitMultiplier =
                difficultyDirector != null
                    ? Mathf.Max(
                        0.01f,
                        difficultyDirector
                            .WardenPursuitMultiplier
                    )
                    : 1f;


            float effectiveSecondsPerFloor =
                secondsPerFloor /
                pursuitMultiplier;


            float headStartRemaining =
                Mathf.Max(
                    0f,
                    pursuitStartTime -
                    Time.time
                );


            float pursuitRemaining =
                Mathf.Max(
                    0f,
                    (1f - pursuitProgress) *
                    effectiveSecondsPerFloor
                );


            return
                headStartRemaining +
                pursuitRemaining;
        }
    }

    private void Start()
    {
        if (runStatsManager == null)
        {
            runStatsManager =
                FindObjectOfType<RunStatsManager>();
        }

        previousRunActive =
            runStatsManager != null &&
            runStatsManager.RunActive;
    }

    private void Update()
    {

        if (runStatsManager == null)
        {
            runStatsManager =
                FindObjectOfType<RunStatsManager>();
        }


        if (dungeonGenerator == null ||
            runManager == null ||
            playerController == null ||
            terrainModifier == null)
        {
            return;
        }

        bool runActive =
        runStatsManager.RunActive;


        /*
         * NEW RUN DETECTION
         *
         * The Warden must begin from a completely fresh pursuit state
         * every time a real run starts.
         *
         * This also catches Restart when both the old and new runs are
         * on Floor 1, which floor-number comparison cannot detect.
         */
        if (runActive &&
            !previousRunActive)
        {
            InitialiseRun();

            observedGenerationVersion =
                dungeonGenerator.GenerationVersion;

            previousRunActive =
                true;

            return;
        }


        /*
         * Warden time must NOT progress while sitting on:
         * - the start menu;
         * - the run-end screen;
         * - any other state where there is no active run.
         */
        if (!runActive)
        {
            previousRunActive =
                false;

            return;
        }


        /*
         * A completed run stops Warden pursuit completely.
         */
        if (runManager.RunComplete)
        {
            HandleRunComplete();

            previousRunActive =
                false;

            return;
        }


        /*
         * GenerationVersion changes whenever a new dungeon floor is
         * generated.
         */
        if (dungeonGenerator.Grid != null &&
            observedGenerationVersion !=
                dungeonGenerator.GenerationVersion)
        {
            HandleGeneratedFloor();
        }


        if (!runInitialised ||
            dungeonGenerator.Grid == null)
        {
            return;
        }


        /*
         * The physical Warden handles itself once it exists.
         *
         * Abstract pursuit stops while both actors occupy the same
         * loaded dungeon floor.
         */
        if (activeWarden != null)
        {
            return;
        }


        /*
         * If it has already caught the player, wait for the physical
         * arrival rather than continuing abstract progress.
         */
        if (wardenFloor >=
            runManager.CurrentFloor)
        {
            EnsurePhysicalArrivalScheduled();

            UpdatePhysicalArrival();

            return;
        }


        UpdateAbstractPursuit();


        UpdatePhysicalArrival();
    }


    // ============================================================
    // RUN INITIALISATION
    // ============================================================

    private void InitialiseRun()
    {
        int playerFloor =
            runManager.CurrentFloor;


        /*
         * Normally:
         *
         * Player Floor = 1
         * startingFloorsBehind = 1
         * Warden Floor = 0
         */
        wardenFloor =
            Mathf.Max(
                0,
                playerFloor -
                Mathf.Max(
                    1,
                    startingFloorsBehind
                )
            );


        pursuitProgress =
            0f;

        pursuitStartTime =
            Time.time +
            Mathf.Max(
                0f,
                initialHeadStartSeconds
            );


        previousPlayerFloor =
            playerFloor;


        runInitialised =
            true;


        runFinished =
            false;


        physicalSpawnScheduled =
            false;


        if (activeWarden != null)
        {
            Destroy(
                activeWarden.gameObject
            );


            activeWarden =
                null;
        }


        UnityEngine.Debug.Log(
            "========== WARDEN PURSUIT INITIALISED ==========\n" +
            $"Player floor: {playerFloor}\n" +
            $"Warden floor: {wardenFloor}\n" +
            $"Starting separation: {FloorsBehind}\n" +
            $"Seconds per abstract floor: {secondsPerFloor:0.0}\n" +
            "================================================"
        );
    }


    // ============================================================
    // NEW FLOOR
    // ============================================================

    private void HandleGeneratedFloor()
    {
        observedGenerationVersion =
            dungeonGenerator.GenerationVersion;


        int currentPlayerFloor =
            runManager.CurrentFloor;


        /*
         * First floor of the run.
         */
        if (!runInitialised)
        {
            InitialiseRun();

            return;
        }


        /*
         * A floor number going backwards means a new run has started.
         *
         * This provides a safe reset if a restart returns the
         * player from a later floor to Floor 1.
         */
        if (currentPlayerFloor <
            previousPlayerFloor)
        {
            InitialiseRun();

            return;
        }


        /*
         * The old physical Warden belonged to the previous generated
         * floor.
         *
         * If the player descended while the Warden was present, its
         * run-level floor number remains unchanged. The Unity object
         * itself is removed because that old floor no longer exists.
         */
        if (activeWarden != null)
        {
            Destroy(
                activeWarden.gameObject
            );


            activeWarden =
                null;
        }


        physicalSpawnScheduled =
            false;


        if (currentPlayerFloor >
            previousPlayerFloor)
        {
            int floorsDescended =
                currentPlayerFloor -
                previousPlayerFloor;


            UnityEngine.Debug.Log(
                "========== PLAYER DESCENDED ==========\n" +
                $"Previous floor: {previousPlayerFloor}\n" +
                $"Current floor: {currentPlayerFloor}\n" +
                $"Floors descended: {floorsDescended}\n" +
                $"Warden remains on floor: {wardenFloor}\n" +
                $"New separation: {Mathf.Max(0, currentPlayerFloor - wardenFloor)}\n" +
                "======================================"
            );
        }


        previousPlayerFloor =
            currentPlayerFloor;


        /*
         * This normally occurs if the Warden was physically present on
         * the previous floor and the player has just descended.
         *
         * The player has gained one floor of distance, so abstract
         * pursuit resumes naturally.
         */
        if (wardenFloor <
            currentPlayerFloor)
        {
            return;
        }


        EnsurePhysicalArrivalScheduled();
    }


    // ============================================================
    // ABSTRACT PURSUIT
    // ============================================================

    private void UpdateAbstractPursuit()
    {

        if (Time.time <
            pursuitStartTime)
        {
            return;
        }

        if (secondsPerFloor <=
            0f)
        {
            return;
        }


        float pursuitMultiplier =
            difficultyDirector != null
                ? difficultyDirector.WardenPursuitMultiplier
                : 1f;


        pursuitProgress +=
            (Time.deltaTime /
             secondsPerFloor) *
            pursuitMultiplier;


        while (pursuitProgress >=
                   1f &&
               wardenFloor <
                   runManager.CurrentFloor)
        {
            pursuitProgress -=
                1f;


            wardenFloor++;


            float currentDifficulty =
                difficultyDirector != null
                    ? difficultyDirector
                        .CurrentDifficultyMultiplier
                    : 1f;


            UnityEngine.Debug.Log(
                "========== WARDEN ADVANCED ==========\n" +
                $"Warden reached floor: {wardenFloor}\n" +
                $"Player floor: {runManager.CurrentFloor}\n" +
                $"Floors behind: {FloorsBehind}\n" +
                $"Adaptive multiplier: {currentDifficulty:0.00}x\n" +
                "====================================="
            );


            /*
             * The Warden has reached the player's loaded floor.
             */
            if (wardenFloor >=
                runManager.CurrentFloor)
            {
                pursuitProgress =
                    0f;


                EnsurePhysicalArrivalScheduled();

                break;
            }
        }
    }


    // ============================================================
    // PHYSICAL ARRIVAL
    // ============================================================

    private void EnsurePhysicalArrivalScheduled()
    {
        if (activeWarden != null ||
            physicalSpawnScheduled ||
            dungeonGenerator.Grid == null)
        {
            return;
        }


        physicalSpawnScheduled =
            true;


        physicalSpawnTime =
            Time.time +
            Mathf.Max(
                0f,
                physicalArrivalDelay
            );


        UnityEngine.Debug.Log(
            "WARDEN REACHED PLAYER FLOOR - " +
            $"Physical arrival in " +
            $"{physicalArrivalDelay:0.0} seconds."
        );
    }


    private void UpdatePhysicalArrival()
    {
        if (!physicalSpawnScheduled)
            return;


        /*
         * The player may descend during the arrival delay.
         *
         * In that case the Warden is once again behind and no physical
         * spawn should occur on the new floor.
         */
        if (wardenFloor <
            runManager.CurrentFloor)
        {
            physicalSpawnScheduled =
                false;

            return;
        }


        if (Time.time <
            physicalSpawnTime)
        {
            return;
        }


        SpawnPhysicalWarden();
    }


    private void SpawnPhysicalWarden()
    {
        physicalSpawnScheduled =
            false;


        if (activeWarden != null ||
            dungeonGenerator.Grid == null)
        {
            return;
        }


        Vector2Int spawnCell;


        if (!TryFindSpawnCell(
                out spawnCell))
        {
            UnityEngine.Debug.LogWarning(
                "WARDEN COULD NOT FIND A VALID ENTRY CELL."
            );


            /*
             * Try again shortly rather than permanently losing the
             * Warden because of one unusual generated floor.
             */
            physicalSpawnScheduled =
                true;


            physicalSpawnTime =
                Time.time +
                1f;


            return;
        }


        GameObject wardenObject =
            GameObject.CreatePrimitive(
                PrimitiveType.Quad
            );


        wardenObject.name =
            "Warden";


        Collider collider =
            wardenObject.GetComponent<Collider>();


        if (collider != null)
        {
            Destroy(
                collider
            );
        }


        activeWarden =
            wardenObject.AddComponent<WardenController>();


        activeWarden.Initialise(
            dungeonGenerator,
            playerController,
            terrainModifier,
            spawnCell
        );

        if (difficultyDirector != null)
        {
            activeWarden.ApplyDifficultyMultiplier(
                difficultyDirector
                    .CurrentDifficultyMultiplier
            );
        }


        UnityEngine.Debug.Log(
            "========== WARDEN PHYSICALLY ARRIVED ==========\n" +
            $"Floor: {runManager.CurrentFloor}\n" +
            $"Entry cell: {spawnCell}\n" +
            "================================================"
        );
    }


    /// <summary>
    /// The Warden always enters through the floor's Start room.
    ///
    /// This gives the pursuit a readable spatial rule: the Warden is
    /// following the same descent route the player previously used.
    /// </summary>
    private bool TryFindSpawnCell(
        out Vector2Int spawnCell)
    {
        spawnCell =
            Vector2Int.zero;


        Room startRoom =
            dungeonGenerator.StartRoom;


        if (startRoom == null)
            return false;


        Vector2Int centre =
            startRoom.Centre;


        if (dungeonGenerator.Grid.IsWalkable(
                centre))
        {
            spawnCell =
                centre;

            return true;
        }


        for (int x =
                 startRoom.Bounds.xMin;
             x <
                 startRoom.Bounds.xMax;
             x++)
        {
            for (int y =
                     startRoom.Bounds.yMin;
                 y <
                     startRoom.Bounds.yMax;
                 y++)
            {
                Vector2Int candidate =
                    new Vector2Int(
                        x,
                        y
                    );


                if (!dungeonGenerator.Grid.IsWalkable(
                        candidate))
                {
                    continue;
                }


                spawnCell =
                    candidate;


                return true;
            }
        }


        return false;
    }


    // ============================================================
    // RUN COMPLETE
    // ============================================================

    private void HandleRunComplete()
    {
        if (runFinished)
            return;


        runFinished =
            true;


        physicalSpawnScheduled =
            false;


        if (activeWarden != null)
        {
            Destroy(
                activeWarden.gameObject
            );


            activeWarden =
                null;
        }


        UnityEngine.Debug.Log(
            "WARDEN PURSUIT ENDED - RUN COMPLETE"
        );
    }


    // ============================================================
    // HUD
    // ============================================================

    /// <summary>
    /// Returns a compact player-facing description of the Warden's
    /// current pursuit state.
    /// </summary>
    public string GetHUDStatus()
    {
        if (!runInitialised)
        {
            return "-";
        }


        if (activeWarden != null)
        {
            if (activeWarden.IsStunned)
            {
                return "STUNNED";
            }


            return "HERE";
        }


        if (physicalSpawnScheduled &&
            wardenFloor >=
                runManager.CurrentFloor)
        {
            return "ARRIVING";
        }

        if (Time.time <
            pursuitStartTime)
        {
            return "DISTANT";
        }

        int behind =
            FloorsBehind;


        if (behind <= 0)
        {
            return "ARRIVING";
        }


        if (behind == 1)
        {
            return "1 FLOOR BEHIND";
        }


        return
            $"{behind} FLOORS BEHIND";
    }
}