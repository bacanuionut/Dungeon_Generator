using System.Collections;
using UnityEngine;

public class DungeonRunManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private DungeonGenerator dungeonGenerator;
    [SerializeField] private RunStatsManager runStatsManager;
    [SerializeField] private GameplayTutorialController gameplayTutorialController;
    [SerializeField] private PlayerPulseController playerPulseController;
    [SerializeField] private PlayerShaperController playerShaperController;
    [SerializeField] private AdaptiveDifficultyDirector adaptiveDifficultyDirector;
    [SerializeField] private PlaytestTelemetryManager playtestTelemetryManager;

    [Header("Floor Transition")]
    [Min(0f)]
    [SerializeField] private float floorTransitionDelay = 0.75f;

    [Header("Live Progression - Debug")]
    [SerializeField] private int currentFloor = 1;
    [SerializeField] private bool transitioning;

    public int CurrentFloor => currentFloor;
    public int TotalFloors =>
        runStatsManager != null ? runStatsManager.TargetFloors : 0;
    public int BaseRunSeed =>
        runStatsManager != null ? runStatsManager.BaseSeed : 0;
    public bool IsSurvival =>
        runStatsManager != null &&
        runStatsManager.CurrentMode == RunStatsManager.GameMode.Survival;
    public bool RunComplete =>
        runStatsManager != null && runStatsManager.RunFinished;

    private void Start()
    {
        ResolveReferences();

        if (runStatsManager == null)
        {
            UnityEngine.Debug.LogError(
                "DungeonRunManager could not start because RunStatsManager was not assigned."
            );
            return;
        }

        if (runStatsManager.AutoBeginDebugRun)
        {
            StartConfiguredRun();
        }
    }

    public void StartNewRun(
        RunStatsManager.GameMode mode,
        int selectedBaseSeed,
        RunStatsManager.RunDifficulty difficulty,
        int selectedTargetFloors,
        bool showRunIntroduction = true)
    {
        ResolveReferences();

        if (dungeonGenerator == null || runStatsManager == null)
        {
            UnityEngine.Debug.LogError(
                "A new run could not start because DungeonRunManager is missing DungeonGenerator or RunStatsManager."
            );
            return;
        }

        StopAllCoroutines();
        transitioning = false;

        runStatsManager.BeginRun(
            mode,
            selectedBaseSeed,
            difficulty,
            selectedTargetFloors
        );

        StartConfiguredRun(
            showRunIntroduction
        );
    }

    private void StartConfiguredRun(
        bool showRunIntroduction = true)
    {
        ResolveReferences();

        if (dungeonGenerator == null || runStatsManager == null)
            return;

        gameplayTutorialController?.ResetTutorialsForNewRun(
            showRunIntroduction
        );

        currentFloor = 1;
        transitioning = false;

        adaptiveDifficultyDirector?.ResetForNewRun();
        playerPulseController?.ResetInventoryForNewRun();
        playerShaperController?.ResetInventoryForNewRun();

        playtestTelemetryManager?.BeginNewRunTracking();

        int firstFloorSeed =
            CalculateFloorSeed(currentFloor);

        UnityEngine.Debug.Log(
            "========== FIRST FLOOR ==========\n" +
            $"Mode: {runStatsManager.CurrentMode}\n" +
            $"Floor: {BuildFloorProgressText()}\n" +
            $"Seed: {firstFloorSeed}\n" +
            "Generating new dungeon...\n" +
            "================================="
        );

        playtestTelemetryManager?.BeginFloorGeneration(
            currentFloor,
            firstFloorSeed
        );

        dungeonGenerator.GenerateRunFloor(
            firstFloorSeed,
            currentFloor
        );

        playtestTelemetryManager?.FinishFloorGeneration();
    }

    public void CompleteCurrentFloor()
    {
        ResolveReferences();

        if (transitioning ||
            runStatsManager == null ||
            !runStatsManager.RunActive ||
            runStatsManager.RunFinished)
        {
            return;
        }

        playtestTelemetryManager?.CompleteCurrentFloor(
            true
        );

        runStatsManager.RecordFloorCompleted(1);

        UnityEngine.Debug.Log(
            "========== FLOOR COMPLETE ==========\n" +
            $"Mode: {runStatsManager.CurrentMode}\n" +
            $"Floor: {BuildFloorProgressText()}\n" +
            $"Seed: {dungeonGenerator.CurrentSeed}\n" +
            $"Run floors completed: {runStatsManager.FloorsCompleted}\n" +
            "===================================="
        );

        if (runStatsManager.CurrentMode == RunStatsManager.GameMode.Standard &&
            currentFloor >= runStatsManager.TargetFloors)
        {
            CompleteStandardRun();
            return;
        }

        StartCoroutine(DescendToNextFloor());
    }

    private IEnumerator DescendToNextFloor()
    {
        transitioning = true;

        UnityEngine.Debug.Log(
            $"DESCENDING FROM FLOOR {currentFloor}..."
        );

        yield return new WaitForSeconds(
            floorTransitionDelay
        );

        if (runStatsManager == null ||
            runStatsManager.RunFinished ||
            !runStatsManager.RunActive)
        {
            transitioning = false;
            yield break;
        }

        currentFloor++;

        int nextFloorSeed =
            CalculateFloorSeed(currentFloor);

        UnityEngine.Debug.Log(
            "========== NEW FLOOR ==========\n" +
            $"Mode: {runStatsManager.CurrentMode}\n" +
            $"Floor: {BuildFloorProgressText()}\n" +
            $"Seed: {nextFloorSeed}\n" +
            "Generating new dungeon...\n" +
            "==============================="
        );

        playtestTelemetryManager?.BeginFloorGeneration(
            currentFloor,
            nextFloorSeed
        );

        dungeonGenerator.GenerateRunFloor(
            nextFloorSeed,
            currentFloor
        );

        playtestTelemetryManager?.FinishFloorGeneration();

        transitioning = false;
    }

    private int CalculateFloorSeed(int floorNumber)
    {
        int baseSeed =
            runStatsManager != null
                ? runStatsManager.BaseSeed
                : 0;

        return unchecked(
            baseSeed +
            (floorNumber - 1) * 1009
        );
    }

    private void CompleteStandardRun()
    {
        transitioning = false;

        if (runStatsManager != null)
        {
            runStatsManager.EndRun(true);
        }

        UnityEngine.Debug.Log(
            "=====================================\n" +
            "              RUN COMPLETE\n" +
            "=====================================\n" +
            "Mode: Standard\n" +
            $"Floors completed: {currentFloor}\n" +
            $"Base run seed: {BaseRunSeed}\n" +
            "The player escaped the dungeon.\n" +
            "====================================="
        );
    }

    private string BuildFloorProgressText()
    {
        if (runStatsManager == null)
            return currentFloor.ToString();

        if (runStatsManager.CurrentMode == RunStatsManager.GameMode.Survival)
            return $"{currentFloor} (Unlimited)";

        return $"{currentFloor}/{runStatsManager.TargetFloors}";
    }

    private void ResolveReferences()
    {
        if (runStatsManager == null)
        {
            runStatsManager =
                FindObjectOfType<RunStatsManager>();
        }

        if (dungeonGenerator == null)
        {
            dungeonGenerator =
                FindObjectOfType<DungeonGenerator>();
        }

        if (gameplayTutorialController == null)
        {
            gameplayTutorialController =
                FindObjectOfType<GameplayTutorialController>();
        }

        if (playerPulseController == null)
        {
            playerPulseController =
                FindObjectOfType<PlayerPulseController>();
        }

        if (playerShaperController == null)
        {
            playerShaperController =
                FindObjectOfType<PlayerShaperController>();
        }

        if (adaptiveDifficultyDirector == null)
        {
            adaptiveDifficultyDirector =
                FindObjectOfType<AdaptiveDifficultyDirector>();
        }

        if (playtestTelemetryManager == null)
        {
            playtestTelemetryManager =
                FindObjectOfType<PlaytestTelemetryManager>();
        }
    }
}
