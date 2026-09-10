using UnityEngine;

/// <summary>
/// Adjusts Warden pressure according to the player's performance on
/// previous floors.
///
/// The system deliberately uses a small bounded adjustment rather than
/// dramatically changing difficulty. This keeps the procedural run
/// predictable while still responding to player performance.
///
/// Floor completion time is used as the initial performance metric:
///
/// - fast completion  -> slightly more Warden pressure
/// - normal completion -> no change
/// - slow completion  -> slightly less Warden pressure
///
/// The result affects the NEXT floor rather than changing difficulty
/// continuously during the current one.
/// </summary>
public class AdaptiveDifficultyDirector : MonoBehaviour
{
    [Header("References")]

    [SerializeField]
    private DungeonRunManager runManager;

    [SerializeField]
    private RunStatsManager runStatsManager;


    [Header("Performance Thresholds")]

    [Tooltip(
        "Completing a floor faster than this is considered strong " +
        "performance."
    )]
    [SerializeField]
    private float fastFloorThreshold = 70f;

    [Tooltip(
        "Taking longer than this is considered struggling."
    )]
    [SerializeField]
    private float slowFloorThreshold = 120f;


    [Header("Difficulty Adjustment")]

    [Tooltip(
        "Amount by which difficulty moves after a clearly fast or " +
        "slow floor."
    )]
    [SerializeField]
    private float adjustmentStep = 0.08f;

    [Tooltip(
        "Lowest difficulty multiplier the director may produce."
    )]
    [SerializeField]
    private float minimumMultiplier = 0.85f;

    [Tooltip(
        "Highest difficulty multiplier the director may produce."
    )]
    [SerializeField]
    private float maximumMultiplier = 1.20f;

    [SerializeField]
    private float startingMultiplier = 1f;


    // ------------------------------------------------------------
    // RUNTIME STATE
    // ------------------------------------------------------------

    private bool initialised;

    private bool finalFloorRecorded;


    private int observedFloor;


    private float floorStartTime;

    private float currentDifficultyMultiplier;

    private float lastFloorCompletionTime;


    public float CurrentDifficultyMultiplier =>
        currentDifficultyMultiplier;


    /// <summary>
    /// Multiplier used by the Warden's abstract cross-floor pursuit.
    ///
    /// 1.0 = normal
    /// 1.1 = 10% faster
    /// 0.9 = 10% slower
    /// </summary>
    public float WardenPursuitMultiplier =>
        GetSelectedWardenPursuitMultiplier() *
        currentDifficultyMultiplier;


    public float LastFloorCompletionTime =>
        lastFloorCompletionTime;


    private void Update()
    {
        ResolveReferences();

        if (runManager == null)
        {
            return;
        }


        if (!initialised)
        {
            TryInitialise();

            return;
        }


        /*
         * Record the final floor once when the complete run ends.
         */
        if (runManager.RunComplete)
        {
            if (!finalFloorRecorded)
            {
                RecordFinalFloor();
            }


            return;
        }


        int currentFloor =
            runManager.CurrentFloor;


        /*
         * A lower floor number indicates that the run has restarted.
         */
        if (currentFloor <
            observedFloor)
        {
            ResetDirector();

            return;
        }


        /*
         * Increasing floor number means the previous floor has just
         * been successfully completed.
         */
        if (currentFloor >
            observedFloor)
        {
            float completionTime =
                Time.time -
                floorStartTime;


            EvaluateFloorPerformance(
                observedFloor,
                completionTime
            );


            observedFloor =
                currentFloor;


            floorStartTime =
                Time.time;
        }
    }


    public void ResetForNewRun()
    {
        ResetDirector();
    }


    private float GetSelectedWardenPursuitMultiplier()
    {
        if (runStatsManager == null)
        {
            return 1f;
        }

        return runStatsManager.WardenPursuitBaseMultiplier;
    }


    private void ResolveReferences()
    {
        if (runManager == null)
        {
            runManager =
                FindObjectOfType<DungeonRunManager>();
        }

        if (runStatsManager == null)
        {
            runStatsManager =
                FindObjectOfType<RunStatsManager>();
        }
    }


    private void TryInitialise()
    {
        ResolveReferences();

        if (runManager == null ||
            runManager.CurrentFloor <= 0)
        {
            return;
        }


        observedFloor =
            runManager.CurrentFloor;


        floorStartTime =
            Time.time;


        currentDifficultyMultiplier =
            Mathf.Clamp(
                startingMultiplier,
                minimumMultiplier,
                maximumMultiplier
            );


        finalFloorRecorded =
            false;


        initialised =
            true;


        UnityEngine.Debug.Log(
            "========== ADAPTIVE DIFFICULTY INITIALISED ==========\n" +
            $"Selected difficulty: " +
            $"{(runStatsManager != null ? runStatsManager.CurrentDifficulty.ToString() : "Unknown")}\n" +
            $"Selected Warden pursuit: {GetSelectedWardenPursuitMultiplier():0.00}x\n" +
            $"Adaptive multiplier: " +
            $"{currentDifficultyMultiplier:0.00}x\n" +
            $"Fast threshold: {fastFloorThreshold:0}s\n" +
            $"Slow threshold: {slowFloorThreshold:0}s\n" +
            "====================================================="
        );
    }


    private void EvaluateFloorPerformance(
        int completedFloor,
        float completionTime)
    {
        lastFloorCompletionTime =
            completionTime;


        float previousMultiplier =
            currentDifficultyMultiplier;


        string classification;


        if (completionTime <
            fastFloorThreshold)
        {
            classification =
                "FAST";


            currentDifficultyMultiplier +=
                adjustmentStep;
        }
        else if (completionTime >
                 slowFloorThreshold)
        {
            classification =
                "SLOW";


            currentDifficultyMultiplier -=
                adjustmentStep;
        }
        else
        {
            classification =
                "NORMAL";
        }


        currentDifficultyMultiplier =
            Mathf.Clamp(
                currentDifficultyMultiplier,
                minimumMultiplier,
                maximumMultiplier
            );


        UnityEngine.Debug.Log(
            "========== ADAPTIVE DIFFICULTY ==========\n" +
            $"Completed floor: {completedFloor}\n" +
            $"Completion time: {completionTime:0.0}s\n" +
            $"Performance: {classification}\n" +
            $"Previous multiplier: {previousMultiplier:0.00}x\n" +
            $"New multiplier: {currentDifficultyMultiplier:0.00}x\n" +
            "========================================="
        );
    }


    private void RecordFinalFloor()
    {
        finalFloorRecorded =
            true;


        float completionTime =
            Time.time -
            floorStartTime;


        lastFloorCompletionTime =
            completionTime;


        /*
         * There is no subsequent playable floor, so the final floor is
         * recorded for evaluation without changing difficulty.
         */
        string classification;


        if (completionTime <
            fastFloorThreshold)
        {
            classification =
                "FAST";
        }
        else if (completionTime >
                 slowFloorThreshold)
        {
            classification =
                "SLOW";
        }
        else
        {
            classification =
                "NORMAL";
        }


        UnityEngine.Debug.Log(
            "========== FINAL FLOOR PERFORMANCE ==========\n" +
            $"Floor: {observedFloor}\n" +
            $"Completion time: {completionTime:0.0}s\n" +
            $"Performance: {classification}\n" +
            $"Final difficulty multiplier: " +
            $"{currentDifficultyMultiplier:0.00}x\n" +
            "============================================="
        );
    }


    private void ResetDirector()
    {
        initialised =
            false;


        finalFloorRecorded =
            false;


        observedFloor =
            0;


        floorStartTime =
            0f;


        lastFloorCompletionTime =
            0f;


        currentDifficultyMultiplier =
            Mathf.Clamp(
                startingMultiplier,
                minimumMultiplier,
                maximumMultiplier
            );
    }
}