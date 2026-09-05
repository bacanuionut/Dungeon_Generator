using UnityEngine;

/// <summary>
/// Stores statistics for one complete dungeon run rather than one floor.
///
/// This is intentionally independent from DungeonGenerator because the same
/// statistics layer will be reused by both planned modes:
///
/// STANDARD
/// - fixed number of floors;
/// - seed and difficulty selected before the run;
/// - completion time is the main performance metric.
///
/// SURVIVAL
/// - continues until the run ends;
/// - floors survived is the main performance metric.
/// </summary>
public class RunStatsManager : MonoBehaviour
{
    public enum GameMode
    {
        Standard,
        Survival
    }

    public enum RunDifficulty
    {
        Easy,
        Normal,
        Hard
    }


    [Header("Current Run")]

    [SerializeField]
    private GameMode currentMode =
        GameMode.Standard;

    [SerializeField]
    private RunDifficulty currentDifficulty =
        RunDifficulty.Normal;

    [SerializeField]
    private int baseSeed = 12345;

    [SerializeField]
    private int targetFloors = 5;


    [Header("Temporary Debug Start")]

    [Tooltip(
        "Keeps the current project playable before the final start-menu GUI " +
        "owns run creation. Disable this when the menu is implemented."
    )]
    [SerializeField]
    private bool autoBeginDebugRun = true;

    [SerializeField]
    private DungeonGenerator dungeonGenerator;


    private bool runActive;
    private bool runFinished;
    private bool runCompletedSuccessfully;

    private float runStartRealtime;
    private float finalElapsedSeconds;

    private int floorsCompleted;
    private int coinsCollected;
    private int heartsLost;
    private int pulseChargesUsed;
    private int shaperChargesUsed;
    private int keysCollected;
    private int puzzlesCompleted;


    public GameMode CurrentMode =>
        currentMode;

    public RunDifficulty CurrentDifficulty =>
        currentDifficulty;

    public int BaseSeed =>
        baseSeed;

    public int TargetFloors =>
        targetFloors;

    public bool RunActive =>
        runActive;

    public bool RunFinished =>
        runFinished;

    public bool RunCompletedSuccessfully =>
        runCompletedSuccessfully;

    public int FloorsCompleted =>
        floorsCompleted;

    public int CoinsCollected =>
        coinsCollected;

    public int HeartsLost =>
        heartsLost;

    public int PulseChargesUsed =>
        pulseChargesUsed;

    public int ShaperChargesUsed =>
        shaperChargesUsed;

    public int KeysCollected =>
        keysCollected;

    public int PuzzlesCompleted =>
        puzzlesCompleted;

    public float ElapsedSeconds =>
        runActive
            ? Time.realtimeSinceStartup -
                runStartRealtime
            : finalElapsedSeconds;


    private void Start()
    {
        if (!autoBeginDebugRun)
            return;

        int debugSeed =
            dungeonGenerator != null
                ? dungeonGenerator.CurrentSeed
                : baseSeed;

        BeginRun(
            GameMode.Standard,
            debugSeed,
            currentDifficulty,
            targetFloors
        );
    }


    /// <summary>
    /// Starts a completely new run and resets every accumulated statistic.
    /// The future main-menu GUI should call this method.
    /// </summary>
    public void BeginRun(
        GameMode mode,
        int selectedBaseSeed,
        RunDifficulty difficulty,
        int selectedTargetFloors)
    {
        currentMode =
            mode;

        currentDifficulty =
            difficulty;

        baseSeed =
            selectedBaseSeed;

        targetFloors =
            mode == GameMode.Standard
                ? Mathf.Max(
                    1,
                    selectedTargetFloors
                )
                : 0;

        ResetCounters();

        runStartRealtime =
            Time.realtimeSinceStartup;

        finalElapsedSeconds =
            0f;

        runActive =
            true;

        runFinished =
            false;

        runCompletedSuccessfully =
            false;

        UnityEngine.Debug.Log(
            "========== RUN STARTED ==========\n" +
            $"Mode: {currentMode}\n" +
            $"Difficulty: {currentDifficulty}\n" +
            $"Base seed: {baseSeed}\n" +
            (currentMode == GameMode.Standard
                ? $"Target floors: {targetFloors}\n"
                : "Target floors: Unlimited\n") +
            "================================="
        );
    }


    public void EndRun(
        bool completedSuccessfully)
    {
        if (runFinished)
            return;

        finalElapsedSeconds =
            runActive
                ? Time.realtimeSinceStartup -
                    runStartRealtime
                : finalElapsedSeconds;

        runActive =
            false;

        runFinished =
            true;

        runCompletedSuccessfully =
            completedSuccessfully;

        LogRunSummary();
    }


    public void RecordCoinCollected(
        int amount = 1)
    {
        amount =
            Mathf.Max(
                0,
                amount
            );

        if (amount <= 0)
            return;

        coinsCollected +=
            amount;

        UnityEngine.Debug.Log(
            $"COIN COLLECTED +{amount} - " +
            $"Run total: {coinsCollected}"
        );
    }


    public void RecordHeartLost(
        int amount = 1)
    {
        heartsLost +=
            Mathf.Max(
                0,
                amount
            );
    }


    public void RecordPulseChargeUsed(
        int amount = 1)
    {
        pulseChargesUsed +=
            Mathf.Max(
                0,
                amount
            );
    }


    public void RecordShaperChargeUsed(
        int amount = 1)
    {
        shaperChargesUsed +=
            Mathf.Max(
                0,
                amount
            );
    }


    public void RecordKeyCollected(
        int amount = 1)
    {
        keysCollected +=
            Mathf.Max(
                0,
                amount
            );
    }


    public void RecordPuzzleCompleted(
        int amount = 1)
    {
        puzzlesCompleted +=
            Mathf.Max(
                0,
                amount
            );
    }


    public void RecordFloorCompleted(
        int amount = 1)
    {
        floorsCompleted +=
            Mathf.Max(
                0,
                amount
            );

        UnityEngine.Debug.Log(
            $"RUN PROGRESS - Floors completed: {floorsCompleted}"
        );
    }


    public void LogRunSummary()
    {
        UnityEngine.Debug.Log(
            "========== RUN SUMMARY ==========\n" +
            $"Mode: {currentMode}\n" +
            $"Difficulty: {currentDifficulty}\n" +
            $"Base seed: {baseSeed}\n" +
            $"Floors completed: {floorsCompleted}\n" +
            $"Coins collected: {coinsCollected}\n" +
            $"Hearts lost: {heartsLost}\n" +
            $"Pulse charges used: {pulseChargesUsed}\n" +
            $"Shaper charges used: {shaperChargesUsed}\n" +
            $"Keys collected: {keysCollected}\n" +
            $"Puzzles completed: {puzzlesCompleted}\n" +
            $"Elapsed time: {FormatTime(ElapsedSeconds)}\n" +
            $"Successful completion: {runCompletedSuccessfully}\n" +
            "================================="
        );
    }


    private void ResetCounters()
    {
        floorsCompleted = 0;
        coinsCollected = 0;
        heartsLost = 0;
        pulseChargesUsed = 0;
        shaperChargesUsed = 0;
        keysCollected = 0;
        puzzlesCompleted = 0;
    }


    private string FormatTime(
        float seconds)
    {
        seconds =
            Mathf.Max(
                0f,
                seconds
            );

        int totalSeconds =
            Mathf.FloorToInt(
                seconds
            );

        int minutes =
            totalSeconds /
            60;

        int remainingSeconds =
            totalSeconds %
            60;

        return
            $"{minutes:00}:{remainingSeconds:00}";
    }
}
