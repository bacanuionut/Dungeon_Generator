using UnityEngine;

public class RunStatsManager : MonoBehaviour
{
    public enum GameMode { Standard, Survival }
    public enum RunDifficulty { Easy, Normal, Hard }

    [Header("Run Configuration")]
    [SerializeField] private GameMode currentMode = GameMode.Standard;
    [SerializeField] private RunDifficulty currentDifficulty = RunDifficulty.Normal;
    [SerializeField] private int baseSeed = 12345;
    [Min(1)]
    [SerializeField] private int targetFloors = 5;

    [Header("Temporary Debug Start")]
    [SerializeField] private bool autoBeginDebugRun = false;

    [Header("Starting Resources")]
    [SerializeField] private int easyStartingPulseCharges = 3;
    [SerializeField] private int normalStartingPulseCharges = 1;
    [SerializeField] private int hardStartingPulseCharges = 0;
    [SerializeField] private int easyStartingDiggerCharges = 3;
    [SerializeField] private int normalStartingDiggerCharges = 1;
    [SerializeField] private int hardStartingDiggerCharges = 0;

    [Header("Enemy Difficulty")]
    [Tooltip("Delay between normal-enemy grid steps on Easy.")]
    [SerializeField] private float easyEnemyMovementDelay = 0.60f;
    [Tooltip("Delay between normal-enemy grid steps on Normal.")]
    [SerializeField] private float normalEnemyMovementDelay = 0.42f;
    [Tooltip("Delay between normal-enemy grid steps on Hard.")]
    [SerializeField] private float hardEnemyMovementDelay = 0.35f;

    [Header("Pulse Difficulty")]
    [Tooltip("Multiplier applied to normal-enemy Pulse stun duration on Easy.")]
    [SerializeField] private float easyNormalEnemyStunMultiplier = 1.20f;

    [Header("Warden Difficulty")]
    [Tooltip("Delay between physical Warden grid steps on Easy before adaptive adjustment.")]
    [SerializeField] private float easyWardenMovementDelay = 0.62f;
    [Tooltip("Delay between physical Warden grid steps on Normal before adaptive adjustment.")]
    [SerializeField] private float normalWardenMovementDelay = 0.50f;
    [Tooltip("Delay between physical Warden grid steps on Hard before adaptive adjustment.")]
    [SerializeField] private float hardWardenMovementDelay = 0.42f;

    [Tooltip("Base cross-floor Warden pursuit multiplier on Easy.")]
    [SerializeField] private float easyWardenPursuitMultiplier = 0.80f;
    [Tooltip("Base cross-floor Warden pursuit multiplier on Normal.")]
    [SerializeField] private float normalWardenPursuitMultiplier = 1.00f;
    [Tooltip("Base cross-floor Warden pursuit multiplier on Hard.")]
    [SerializeField] private float hardWardenPursuitMultiplier = 1.10f;

    [Header("Live Run Statistics - Debug")]
    [SerializeField] private bool runActive;
    [SerializeField] private bool runFinished;
    [SerializeField] private bool runCompletedSuccessfully;
    private float runStartRealtime;
    [SerializeField] private float finalElapsedSeconds;
    [SerializeField] private int floorsCompleted;
    [SerializeField] private int coinsCollected;
    [SerializeField] private int heartsLost;
    [SerializeField] private int pulseChargesUsed;
    [SerializeField] private int shaperChargesUsed;
    [SerializeField] private int keysCollected;
    [SerializeField] private int puzzlesCompleted;

    public GameMode CurrentMode => currentMode;
    public RunDifficulty CurrentDifficulty => currentDifficulty;
    public int BaseSeed => baseSeed;
    public int TargetFloors =>
        currentMode == GameMode.Standard ? Mathf.Max(1, targetFloors) : 0;
    public bool AutoBeginDebugRun => autoBeginDebugRun;
    public bool RunActive => runActive;
    public bool RunFinished => runFinished;
    public bool RunCompletedSuccessfully => runCompletedSuccessfully;
    public int FloorsCompleted => floorsCompleted;
    public int CoinsCollected => coinsCollected;
    public int HeartsLost => heartsLost;
    public int PulseChargesUsed => pulseChargesUsed;
    public int ShaperChargesUsed => shaperChargesUsed;
    public int KeysCollected => keysCollected;
    public int PuzzlesCompleted => puzzlesCompleted;
    public float ElapsedSeconds =>
        runActive ? Time.realtimeSinceStartup - runStartRealtime : finalElapsedSeconds;

    public int StartingPulseCharges
    {
        get
        {
            switch (currentDifficulty)
            {
                case RunDifficulty.Easy:
                    return Mathf.Max(0, easyStartingPulseCharges);

                case RunDifficulty.Hard:
                    return Mathf.Max(0, hardStartingPulseCharges);

                default:
                    return Mathf.Max(0, normalStartingPulseCharges);
            }
        }
    }

    public int StartingDiggerCharges
    {
        get
        {
            switch (currentDifficulty)
            {
                case RunDifficulty.Easy:
                    return Mathf.Max(0, easyStartingDiggerCharges);

                case RunDifficulty.Hard:
                    return Mathf.Max(0, hardStartingDiggerCharges);

                default:
                    return Mathf.Max(0, normalStartingDiggerCharges);
            }
        }
    }

    public float EnemyMovementDelay
    {
        get
        {
            switch (currentDifficulty)
            {
                case RunDifficulty.Easy:
                    return Mathf.Max(0.05f, easyEnemyMovementDelay);

                case RunDifficulty.Hard:
                    return Mathf.Max(0.05f, hardEnemyMovementDelay);

                default:
                    return Mathf.Max(0.05f, normalEnemyMovementDelay);
            }
        }
    }

    public bool EnemyUsesFullInvestigationScan =>
        currentDifficulty != RunDifficulty.Easy;

    public float NormalEnemyStunDurationMultiplier =>
        currentDifficulty == RunDifficulty.Easy
            ? Mathf.Max(1f, easyNormalEnemyStunMultiplier)
            : 1f;

    public float WardenMovementDelay
    {
        get
        {
            switch (currentDifficulty)
            {
                case RunDifficulty.Easy:
                    return Mathf.Max(0.05f, easyWardenMovementDelay);

                case RunDifficulty.Hard:
                    return Mathf.Max(0.05f, hardWardenMovementDelay);

                default:
                    return Mathf.Max(0.05f, normalWardenMovementDelay);
            }
        }
    }

    public float WardenPursuitBaseMultiplier
    {
        get
        {
            switch (currentDifficulty)
            {
                case RunDifficulty.Easy:
                    return Mathf.Max(0.1f, easyWardenPursuitMultiplier);

                case RunDifficulty.Hard:
                    return Mathf.Max(0.1f, hardWardenPursuitMultiplier);

                default:
                    return Mathf.Max(0.1f, normalWardenPursuitMultiplier);
            }
        }
    }

    private void Awake()
    {
        if (!autoBeginDebugRun)
            return;

        BeginRun(currentMode, baseSeed, currentDifficulty, targetFloors);
    }

    public void BeginRun(
        GameMode mode,
        int selectedBaseSeed,
        RunDifficulty difficulty,
        int selectedTargetFloors)
    {
        currentMode = mode;
        currentDifficulty = difficulty;
        baseSeed = selectedBaseSeed;

        targetFloors =
            mode == GameMode.Standard
                ? Mathf.Max(1, selectedTargetFloors)
                : 0;

        ResetCounters();

        runStartRealtime = Time.realtimeSinceStartup;
        finalElapsedSeconds = 0f;
        runActive = true;
        runFinished = false;
        runCompletedSuccessfully = false;

        UnityEngine.Debug.Log(
            "========== RUN STARTED ==========\n" +
            $"Mode: {currentMode}\n" +
            $"Difficulty: {currentDifficulty}\n" +
            $"Base seed: {baseSeed}\n" +
            (currentMode == GameMode.Standard
                ? $"Target floors: {targetFloors}\n"
                : "Target floors: Unlimited\n") +
            $"Starting Pulse: {StartingPulseCharges}\n" +
            $"Starting Digger: {StartingDiggerCharges}\n" +
            $"Enemy step delay: {EnemyMovementDelay:0.00}s\n" +
            $"Warden step delay: {WardenMovementDelay:0.00}s\n" +
            "================================="
        );
    }

    public void EndRun(bool completedSuccessfully)
    {
        if (runFinished)
            return;

        finalElapsedSeconds =
            runActive
                ? Time.realtimeSinceStartup - runStartRealtime
                : finalElapsedSeconds;

        runActive = false;
        runFinished = true;
        runCompletedSuccessfully = completedSuccessfully;

        LogRunSummary();
    }

    public void RecordCoinCollected(int amount = 1)
    {
        amount = Mathf.Max(0, amount);
        if (amount <= 0) return;

        coinsCollected += amount;

        UnityEngine.Debug.Log(
            $"COIN COLLECTED +{amount} - Run total: {coinsCollected}"
        );
    }

    public void RecordHeartLost(int amount = 1)
    {
        heartsLost += Mathf.Max(0, amount);
    }

    public void RecordPulseChargeUsed(int amount = 1)
    {
        pulseChargesUsed += Mathf.Max(0, amount);
    }

    public void RecordShaperChargeUsed(int amount = 1)
    {
        shaperChargesUsed += Mathf.Max(0, amount);
    }

    public void RecordKeyCollected(int amount = 1)
    {
        keysCollected += Mathf.Max(0, amount);
    }

    public void RecordPuzzleCompleted(int amount = 1)
    {
        puzzlesCompleted += Mathf.Max(0, amount);
    }

    public void RecordFloorCompleted(int amount = 1)
    {
        floorsCompleted += Mathf.Max(0, amount);

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

    private string FormatTime(float seconds)
    {
        seconds = Mathf.Max(0f, seconds);

        int totalSeconds = Mathf.FloorToInt(seconds);
        int minutes = totalSeconds / 60;
        int remainingSeconds = totalSeconds % 60;

        return $"{minutes:00}:{remainingSeconds:00}";
    }
}
