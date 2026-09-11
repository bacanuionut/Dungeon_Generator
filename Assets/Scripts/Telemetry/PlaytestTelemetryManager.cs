using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;

/// <summary>
/// Collects anonymous playtest measurements and submits one Google Forms
/// response for each completed or failed floor in a WebGL build.
/// </summary>
public class PlaytestTelemetryManager : MonoBehaviour
{
    private const string TesterIdPlayerPrefsKey =
        "CM3070_Playtest_TesterID";

    private const string GameplayFormId =
        "1FAIpQLScqB1ukXtMROtcBIEB4ZebmFnXdlgQ1P7TrhE23NguTFPi3-g";

    private const string FeedbackFormId =
        "1FAIpQLScxuUq2FoxfQGuhM2FZxWdpLHQUEVP1n1TZ86atN1eBE8Ocyg";

    private const string GameplayFormResponseUrl =
        "https://docs.google.com/forms/d/e/" +
        GameplayFormId +
        "/formResponse";

    private const string FeedbackFormViewUrl =
        "https://docs.google.com/forms/d/e/" +
        FeedbackFormId +
        "/viewform?usp=pp_url";


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
    private WardenManager wardenManager;


    [Header("Build")]

    [Tooltip(
        "Optional label stored with telemetry. " +
        "Leave blank to use the version from Player Settings."
    )]
    [SerializeField]
    private string buildVersionOverride = "";


    [Header("WebGL Bridge Test")]

    [Tooltip(
        "Keep this disabled for real playtests. It sends one fixed test row " +
        "when the WebGL build starts."
    )]
    [SerializeField]
    private bool sendTestSubmissionOnStart = false;

    [Min(0f)]
    [SerializeField]
    private float testSubmissionDelay = 2f;


    [Header("Runtime Identity")]

    [SerializeField]
    private string testerId;

    [SerializeField]
    private string sessionId;

    [SerializeField]
    private int runNumber;


    [Header("Current Floor - Debug")]

    [SerializeField]
    private bool floorTrackingActive;

    [SerializeField]
    private bool floorSubmitted;

    [SerializeField]
    private int trackedFloorNumber;

    [SerializeField]
    private int trackedFloorSeed;

    [SerializeField]
    private float generationTimeMs;

    [SerializeField]
    private int roomsVisited;

    [SerializeField]
    private int enemiesStunned;

    [SerializeField]
    private int enemyDetections;

    [SerializeField]
    private bool wardenEncountered;


    private float generationStartRealtime;
    private float floorStartRealtime;

    private bool generationSuccess;
    private int generatedRoomCount;
    private int generatedFloorCellCount;
    private int generatedCaCellCount;
    private int generatedShortestPath;
    private int generatedGraphDistance;

    private int startingCoins;
    private int startingKeys;
    private int startingPulseUses;
    private int startingDiggerUses;
    private int startingHeartsLost;
    private int startingPuzzlesCompleted;

    private Vector2Int lastTrackedPlayerCell;
    private bool hasTrackedPlayerCell;

    private readonly HashSet<Room>
        visitedRooms =
            new HashSet<Room>();

    private readonly Dictionary<int, EnemyController.EnemyState>
        previousEnemyStates =
            new Dictionary<int, EnemyController.EnemyState>();


    public string TesterId =>
        testerId;

    public string SessionId =>
        sessionId;

    public int RunNumber =>
        runNumber;

    public string BuildVersion
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(
                    buildVersionOverride))
            {
                return buildVersionOverride.Trim();
            }

            return UnityEngine.Application.version;
        }
    }


#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern void SubmitGoogleForm(
        string formUrl,
        string formBody);

    
#endif

    // Removed from above because it was causing a duplicate opening of the link
    //[DllImport("__Internal")]
    //private static extern void OpenExternalUrl(
    //    string url);

    private void Awake()
    {
        InitialiseTesterId();
        BeginPlaytestSession();
        ResolveReferences();
    }


    private void Start()
    {
        if (sendTestSubmissionOnStart)
        {
            Invoke(
                nameof(SendControlledTestSubmission),
                Mathf.Max(
                    0f,
                    testSubmissionDelay
                )
            );
        }
    }


    private void Update()
    {
        ResolveReferences();

        if (!floorTrackingActive ||
            floorSubmitted)
        {
            return;
        }

        TrackPlayerRoomVisits();
        TrackEnemyStateChanges();
        TrackWardenEncounter();

        if (runStatsManager != null &&
            runStatsManager.RunFinished)
        {
            SubmitCurrentFloor(
                false
            );
        }
    }


    /// <summary>
    /// Creates one identifier for the current playtest visit.
    /// It remains unchanged while the player starts or restarts runs.
    /// </summary>
    public string BeginPlaytestSession()
    {
        string timestamp =
            DateTime.UtcNow.ToString(
                "yyyyMMdd-HHmmss"
            );

        string suffix =
            Guid.NewGuid()
                .ToString("N")
                .Substring(0, 4)
                .ToUpperInvariant();

        sessionId =
            testerId +
            "-" +
            timestamp +
            "-" +
            suffix;

        runNumber =
            0;

        ResetFloorTracking();

        UnityEngine.Debug.Log(
            "PLAYTEST SESSION STARTED\n" +
            $"Tester ID: {testerId}\n" +
            $"Session ID: {sessionId}\n" +
            $"Build: {BuildVersion}"
        );

        return sessionId;
    }


    /// <summary>
    /// Starts one run inside the current playtest session.
    /// </summary>
    public int BeginNewRunTracking()
    {
        EnsureSessionId();

        runNumber =
            Mathf.Max(
                0,
                runNumber
            ) +
            1;

        ResetFloorTracking();

        UnityEngine.Debug.Log(
            "PLAYTEST RUN STARTED\n" +
            $"Session ID: {sessionId}\n" +
            $"Run number: {runNumber}"
        );

        return runNumber;
    }


    /// <summary>
    /// Starts timing one procedural floor generation.
    /// </summary>
    public void BeginFloorGeneration(
        int floorNumber,
        int floorSeed)
    {
        ResolveReferences();
        EnsureSessionId();

        ResetFloorTracking();

        trackedFloorNumber =
            floorNumber;

        trackedFloorSeed =
            floorSeed;

        generationStartRealtime =
            Time.realtimeSinceStartup;
    }


    /// <summary>
    /// Captures the generated floor before the player can alter it with
    /// runtime terrain modification.
    /// </summary>
    public void FinishFloorGeneration()
    {
        ResolveReferences();

        generationTimeMs =
            Mathf.Max(
                0f,
                (
                    Time.realtimeSinceStartup -
                    generationStartRealtime
                ) *
                1000f
            );

        if (dungeonGenerator == null)
        {
            generationSuccess =
                false;

            floorTrackingActive =
                true;

            floorStartRealtime =
                Time.realtimeSinceStartup;

            return;
        }

        DungeonGrid grid =
            dungeonGenerator.Grid;

        generationSuccess =
            dungeonGenerator.CurrentMetrics != null &&
            grid != null &&
            grid.FloorCellCount > 0;

        generatedRoomCount =
            dungeonGenerator.Rooms != null
                ? dungeonGenerator.Rooms.Count
                : 0;

        generatedFloorCellCount =
            grid != null
                ? grid.FloorCellCount
                : 0;

        generatedCaCellCount =
            grid != null
                ? grid.OrganicRoomCellCount
                : 0;

        generatedGraphDistance =
            dungeonGenerator.StartToExitDistance;

        generatedShortestPath =
            CalculateShortestPathLength();

        CaptureRunCounterBaselines();

        floorStartRealtime =
            Time.realtimeSinceStartup;

        floorTrackingActive =
            true;

        floorSubmitted =
            false;

        hasTrackedPlayerCell =
            false;

        visitedRooms.Clear();
        previousEnemyStates.Clear();

        TrackPlayerRoomVisits();
        InitialiseEnemyStateTracking();
        TrackWardenEncounter();

        UnityEngine.Debug.Log(
            "PLAYTEST FLOOR TRACKING STARTED\n" +
            $"Floor: {trackedFloorNumber}\n" +
            $"Seed: {trackedFloorSeed}\n" +
            $"Generation success: {generationSuccess}\n" +
            $"Generation time: {generationTimeMs:0.00} ms\n" +
            $"Rooms: {generatedRoomCount}\n" +
            $"Floor cells: {generatedFloorCellCount}\n" +
            $"CA cells: {generatedCaCellCount}\n" +
            $"Shortest path: {generatedShortestPath}\n" +
            $"Graph distance: {generatedGraphDistance}"
        );
    }


    /// <summary>
    /// Submits the currently tracked floor once.
    /// </summary>
    public void CompleteCurrentFloor(
        bool completed)
    {
        if (!floorTrackingActive ||
            floorSubmitted)
        {
            return;
        }

        SubmitCurrentFloor(
            completed
        );
    }


    /// <summary>
    /// Builds the feedback questionnaire URL with the anonymous tester
    /// and playtest-session identifiers already filled in.
    /// </summary>
    public string BuildFeedbackFormUrl()
    {
        EnsureSessionId();

        StringBuilder url =
            new StringBuilder(
                FeedbackFormViewUrl
            );

        AppendQueryValue(
            url,
            "entry.67038099",
            testerId
        );

        AppendQueryValue(
            url,
            "entry.354113390",
            sessionId
        );

        return url.ToString();
    }


    /// <summary>
    /// Opens the optional post-play questionnaire.
    /// </summary>
    public void OpenFeedbackForm()
    {
        string url =
            BuildFeedbackFormUrl();

        UnityEngine.Application.OpenURL(
            url
        );


        UnityEngine.Debug.Log(
            "PLAYTEST FEEDBACK FORM OPENED\n" +
            $"Tester ID: {testerId}\n" +
            $"Session ID: {sessionId}\n" +
            $"Runs started this session: {runNumber}"
        );
    }


    public void SendControlledTestSubmission()
    {
        EnsureSessionId();

        StringBuilder body =
            new StringBuilder();

        AddFormValue(body, "entry.1621710672", testerId);
        AddFormValue(body, "entry.192525645", sessionId);
        AddFormValue(body, "entry.255806241", "TEST");
        AddFormValue(body, "entry.935774192", "WEBGL-BRIDGE-TEST");
        AddFormValue(body, "entry.1370550382", "TEST");
        AddFormValue(body, "entry.1611016367", "TEST");
        AddFormValue(body, "entry.1692995498", "12345");
        AddFormValue(body, "entry.1453615702", "1");
        AddFormValue(body, "entry.361207062", "12345");
        AddFormValue(body, "entry.1805512226", "True");
        AddFormValue(body, "entry.603026256", "12.34");
        AddFormValue(body, "entry.486193356", "13");
        AddFormValue(body, "entry.1574444731", "1450");
        AddFormValue(body, "entry.1562404582", "437");
        AddFormValue(body, "entry.1650077166", "106");
        AddFormValue(body, "entry.1781115299", "4");
        AddFormValue(body, "entry.339407958", "99.99");
        AddFormValue(body, "entry.113367914", "123");
        AddFormValue(body, "entry.1609311136", "9");
        AddFormValue(body, "entry.79340218", "3");
        AddFormValue(body, "entry.1357189004", "5");
        AddFormValue(body, "entry.764617458", "2");
        AddFormValue(body, "entry.52792503", "1");
        AddFormValue(body, "entry.1283581128", "4");
        AddFormValue(body, "entry.1042663730", "6");
        AddFormValue(body, "entry.69338496", "2");
        AddFormValue(body, "entry.1810968073", "True");
        AddFormValue(body, "entry.1473181925", "True");
        AddFormValue(body, "entry.1010747731", "True");

        SubmitBody(
            body.ToString(),
            true
        );
    }


    private void SubmitCurrentFloor(
        bool completed)
    {
        ResolveReferences();
        TrackPlayerRoomVisits();
        TrackEnemyStateChanges();
        TrackWardenEncounter();

        float floorTimeSeconds =
            Mathf.Max(
                0f,
                Time.realtimeSinceStartup -
                floorStartRealtime
            );

        int moves =
            playerController != null
                ? playerController.MovementCount
                : 0;

        int keysCollected =
            dungeonGenerator != null &&
            dungeonGenerator.ObjectiveManager != null
                ? dungeonGenerator.ObjectiveManager.CollectedSigils
                : 0;

        int coinsCollected =
            GetRunCounterDifference(
                runStatsManager != null
                    ? runStatsManager.CoinsCollected
                    : 0,
                startingCoins
            );

        int pulseChargesUsed =
            GetRunCounterDifference(
                runStatsManager != null
                    ? runStatsManager.PulseChargesUsed
                    : 0,
                startingPulseUses
            );

        int diggerChargesUsed =
            GetRunCounterDifference(
                runStatsManager != null
                    ? runStatsManager.ShaperChargesUsed
                    : 0,
                startingDiggerUses
            );

        int heartsLost =
            GetRunCounterDifference(
                runStatsManager != null
                    ? runStatsManager.HeartsLost
                    : 0,
                startingHeartsLost
            );

        int puzzlesCompleted =
            GetRunCounterDifference(
                runStatsManager != null
                    ? runStatsManager.PuzzlesCompleted
                    : 0,
                startingPuzzlesCompleted
            );

        StringBuilder body =
            new StringBuilder();

        AddFormValue(
            body,
            "entry.1621710672",
            testerId
        );

        AddFormValue(
            body,
            "entry.192525645",
            sessionId
        );

        AddFormValue(
            body,
            "entry.255806241",
            runNumber.ToString()
        );

        AddFormValue(
            body,
            "entry.935774192",
            BuildVersion
        );

        AddFormValue(
            body,
            "entry.1370550382",
            runStatsManager != null
                ? runStatsManager.CurrentMode.ToString()
                : string.Empty
        );

        AddFormValue(
            body,
            "entry.1611016367",
            runStatsManager != null
                ? runStatsManager.CurrentDifficulty.ToString()
                : string.Empty
        );

        AddFormValue(
            body,
            "entry.1692995498",
            runStatsManager != null
                ? runStatsManager.BaseSeed.ToString()
                : "0"
        );

        AddFormValue(
            body,
            "entry.1453615702",
            trackedFloorNumber.ToString()
        );

        AddFormValue(
            body,
            "entry.361207062",
            trackedFloorSeed.ToString()
        );

        AddFormValue(
            body,
            "entry.1805512226",
            BoolText(
                generationSuccess
            )
        );

        AddFormValue(
            body,
            "entry.603026256",
            generationTimeMs.ToString(
                "0.00",
                CultureInfo.InvariantCulture
            )
        );

        AddFormValue(
            body,
            "entry.486193356",
            generatedRoomCount.ToString()
        );

        AddFormValue(
            body,
            "entry.1574444731",
            generatedFloorCellCount.ToString()
        );

        AddFormValue(
            body,
            "entry.1562404582",
            generatedCaCellCount.ToString()
        );

        AddFormValue(
            body,
            "entry.1650077166",
            generatedShortestPath.ToString()
        );

        AddFormValue(
            body,
            "entry.1781115299",
            generatedGraphDistance.ToString()
        );

        AddFormValue(
            body,
            "entry.339407958",
            floorTimeSeconds.ToString(
                "0.00",
                CultureInfo.InvariantCulture
            )
        );

        AddFormValue(
            body,
            "entry.113367914",
            moves.ToString()
        );

        AddFormValue(
            body,
            "entry.1609311136",
            roomsVisited.ToString()
        );

        AddFormValue(
            body,
            "entry.79340218",
            keysCollected.ToString()
        );

        AddFormValue(
            body,
            "entry.1357189004",
            coinsCollected.ToString()
        );

        AddFormValue(
            body,
            "entry.764617458",
            pulseChargesUsed.ToString()
        );

        AddFormValue(
            body,
            "entry.52792503",
            diggerChargesUsed.ToString()
        );

        AddFormValue(
            body,
            "entry.1283581128",
            enemiesStunned.ToString()
        );

        AddFormValue(
            body,
            "entry.1042663730",
            enemyDetections.ToString()
        );

        AddFormValue(
            body,
            "entry.69338496",
            heartsLost.ToString()
        );

        AddFormValue(
            body,
            "entry.1810968073",
            BoolText(
                wardenEncountered
            )
        );

        AddFormValue(
            body,
            "entry.1473181925",
            BoolText(
                puzzlesCompleted > 0
            )
        );

        AddFormValue(
            body,
            "entry.1010747731",
            BoolText(
                completed
            )
        );

        floorSubmitted =
            true;

        SubmitBody(
            body.ToString(),
            false
        );

        UnityEngine.Debug.Log(
            "========== PLAYTEST FLOOR DATA ==========\n" +
            $"Tester: {testerId}\n" +
            $"Session: {sessionId}\n" +
            $"Run: {runNumber}\n" +
            $"Floor: {trackedFloorNumber}\n" +
            $"Completed: {completed}\n" +
            $"Time: {floorTimeSeconds:0.00}s\n" +
            $"Moves: {moves}\n" +
            $"Rooms visited: {roomsVisited}\n" +
            $"Keys: {keysCollected}\n" +
            $"Coins: {coinsCollected}\n" +
            $"Pulse used: {pulseChargesUsed}\n" +
            $"Digger used: {diggerChargesUsed}\n" +
            $"Enemies stunned: {enemiesStunned}\n" +
            $"Enemy detections: {enemyDetections}\n" +
            $"Hearts lost: {heartsLost}\n" +
            $"Warden encountered: {wardenEncountered}\n" +
            $"Puzzle completed: {puzzlesCompleted > 0}\n" +
            "========================================="
        );
    }


    private void CaptureRunCounterBaselines()
    {
        if (runStatsManager == null)
        {
            startingCoins = 0;
            startingKeys = 0;
            startingPulseUses = 0;
            startingDiggerUses = 0;
            startingHeartsLost = 0;
            startingPuzzlesCompleted = 0;

            return;
        }

        startingCoins =
            runStatsManager.CoinsCollected;

        startingKeys =
            runStatsManager.KeysCollected;

        startingPulseUses =
            runStatsManager.PulseChargesUsed;

        startingDiggerUses =
            runStatsManager.ShaperChargesUsed;

        startingHeartsLost =
            runStatsManager.HeartsLost;

        startingPuzzlesCompleted =
            runStatsManager.PuzzlesCompleted;
    }


    private void TrackPlayerRoomVisits()
    {
        if (playerController == null ||
            dungeonGenerator == null ||
            dungeonGenerator.Rooms == null)
        {
            return;
        }

        Vector2Int playerCell =
            playerController.GridPosition;

        if (hasTrackedPlayerCell &&
            playerCell ==
            lastTrackedPlayerCell)
        {
            return;
        }

        hasTrackedPlayerCell =
            true;

        lastTrackedPlayerCell =
            playerCell;

        for (int i = 0;
             i < dungeonGenerator.Rooms.Count;
             i++)
        {
            Room room =
                dungeonGenerator.Rooms[i];

            if (room != null &&
                room.Contains(
                    playerCell))
            {
                visitedRooms.Add(
                    room
                );

                roomsVisited =
                    visitedRooms.Count;

                break;
            }
        }
    }


    private void InitialiseEnemyStateTracking()
    {
        EnemyController[] enemies =
            FindObjectsOfType<EnemyController>();

        for (int i = 0;
             i < enemies.Length;
             i++)
        {
            EnemyController enemy =
                enemies[i];

            if (enemy == null)
                continue;

            previousEnemyStates[
                enemy.GetInstanceID()
            ] =
                enemy.CurrentState;
        }
    }


    private void TrackEnemyStateChanges()
    {
        EnemyController[] enemies =
            FindObjectsOfType<EnemyController>();

        for (int i = 0;
             i < enemies.Length;
             i++)
        {
            EnemyController enemy =
                enemies[i];

            if (enemy == null)
                continue;

            int id =
                enemy.GetInstanceID();

            EnemyController.EnemyState current =
                enemy.CurrentState;

            EnemyController.EnemyState previous;

            if (!previousEnemyStates.TryGetValue(
                    id,
                    out previous))
            {
                previousEnemyStates[id] =
                    current;

                continue;
            }

            if (current ==
                    EnemyController.EnemyState.Stunned &&
                previous !=
                    EnemyController.EnemyState.Stunned)
            {
                enemiesStunned++;
            }

            bool currentAlert =
                IsEnemyAlertState(
                    current
                );

            bool previousAlert =
                IsEnemyAlertState(
                    previous
                );

            if (currentAlert &&
                !previousAlert)
            {
                enemyDetections++;
            }

            previousEnemyStates[id] =
                current;
        }
    }


    private static bool IsEnemyAlertState(
        EnemyController.EnemyState state)
    {
        return
            state ==
                EnemyController.EnemyState.Chase ||
            state ==
                EnemyController.EnemyState.Attack;
    }


    private void TrackWardenEncounter()
    {
        if (wardenEncountered ||
            wardenManager == null)
        {
            return;
        }

        if (wardenManager.IsWardenArriving ||
            wardenManager.IsPhysicalWardenPresent)
        {
            wardenEncountered =
                true;
        }
    }


    private int CalculateShortestPathLength()
    {
        if (dungeonGenerator == null ||
            dungeonGenerator.Grid == null)
        {
            return -1;
        }

        Vector2Int start =
            dungeonGenerator.GetPlayerSpawnPosition();

        Vector2Int goal =
            dungeonGenerator.GetExitPosition();

        if (start == goal)
            return 0;

        DungeonGrid grid =
            dungeonGenerator.Grid;

        Queue<Vector2Int> frontier =
            new Queue<Vector2Int>();

        Dictionary<Vector2Int, int> distance =
            new Dictionary<Vector2Int, int>();

        frontier.Enqueue(
            start
        );

        distance[start] =
            0;

        Vector2Int[] directions =
        {
            Vector2Int.up,
            Vector2Int.down,
            Vector2Int.left,
            Vector2Int.right
        };

        while (frontier.Count > 0)
        {
            Vector2Int current =
                frontier.Dequeue();

            int currentDistance =
                distance[current];

            for (int i = 0;
                 i < directions.Length;
                 i++)
            {
                Vector2Int next =
                    current +
                    directions[i];

                if (distance.ContainsKey(
                        next) ||
                    !grid.IsWalkable(
                        next))
                {
                    continue;
                }

                int nextDistance =
                    currentDistance +
                    1;

                if (next == goal)
                {
                    return nextDistance;
                }

                distance[next] =
                    nextDistance;

                frontier.Enqueue(
                    next
                );
            }
        }

        return -1;
    }


    private void ResetFloorTracking()
    {
        floorTrackingActive =
            false;

        floorSubmitted =
            false;

        trackedFloorNumber =
            0;

        trackedFloorSeed =
            0;

        generationTimeMs =
            0f;

        generationSuccess =
            false;

        generatedRoomCount =
            0;

        generatedFloorCellCount =
            0;

        generatedCaCellCount =
            0;

        generatedShortestPath =
            -1;

        generatedGraphDistance =
            0;

        roomsVisited =
            0;

        enemiesStunned =
            0;

        enemyDetections =
            0;

        wardenEncountered =
            false;

        hasTrackedPlayerCell =
            false;

        visitedRooms.Clear();
        previousEnemyStates.Clear();
    }


    private void ResolveReferences()
    {
        if (dungeonGenerator == null)
        {
            dungeonGenerator =
                FindObjectOfType<DungeonGenerator>();
        }

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

        if (playerController == null)
        {
            playerController =
                FindObjectOfType<PlayerController>();
        }

        if (wardenManager == null)
        {
            wardenManager =
                FindObjectOfType<WardenManager>();
        }
    }


    private void InitialiseTesterId()
    {
        if (PlayerPrefs.HasKey(
                TesterIdPlayerPrefsKey))
        {
            testerId =
                PlayerPrefs.GetString(
                    TesterIdPlayerPrefsKey
                );

            if (!string.IsNullOrWhiteSpace(
                    testerId))
            {
                return;
            }
        }

        testerId =
            "T-" +
            Guid.NewGuid()
                .ToString("N")
                .Substring(0, 8)
                .ToUpperInvariant();

        PlayerPrefs.SetString(
            TesterIdPlayerPrefsKey,
            testerId
        );

        PlayerPrefs.Save();

        UnityEngine.Debug.Log(
            $"PLAYTEST TESTER ID CREATED: {testerId}"
        );
    }


    private void EnsureSessionId()
    {
        if (string.IsNullOrWhiteSpace(
                sessionId))
        {
            BeginPlaytestSession();
        }
    }


    private void SubmitBody(
        string body,
        bool isTestSubmission)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        SubmitGoogleForm(
            GameplayFormResponseUrl,
            body
        );

        UnityEngine.Debug.Log(
            isTestSubmission
                ? "WEBGL GOOGLE FORMS TEST SUBMISSION SENT"
                : "PLAYTEST TELEMETRY SUBMISSION SENT"
        );
#else
        UnityEngine.Debug.Log(
            "GOOGLE FORMS SUBMISSION PREVIEW - " +
            "real submission only occurs in a WebGL build.\n" +
            $"URL: {GameplayFormResponseUrl}\n" +
            $"Body: {body}"
        );
#endif
    }


    private static int GetRunCounterDifference(
        int currentValue,
        int startingValue)
    {
        return Mathf.Max(
            0,
            currentValue -
            startingValue
        );
    }


    private static string BoolText(
        bool value)
    {
        return value
            ? "True"
            : "False";
    }


    private static void AddFormValue(
        StringBuilder body,
        string entryId,
        string value)
    {
        if (body.Length > 0)
        {
            body.Append("&");
        }

        body.Append(
            Uri.EscapeDataString(
                entryId
            )
        );

        body.Append("=");

        body.Append(
            Uri.EscapeDataString(
                value ?? string.Empty
            )
        );
    }


    private static void AppendQueryValue(
        StringBuilder url,
        string entryId,
        string value)
    {
        url.Append("&");

        url.Append(
            Uri.EscapeDataString(
                entryId
            )
        );

        url.Append("=");

        url.Append(
            Uri.EscapeDataString(
                value ?? string.Empty
            )
        );
    }
}
