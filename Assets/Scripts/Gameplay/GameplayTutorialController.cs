using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Controls one-time gameplay tutorials, contextual prompts and short
/// camera focus sequences.
/// </summary>
public class GameplayTutorialController : MonoBehaviour
{
    [Header("References")]

    [SerializeField]
    private DungeonGenerator dungeonGenerator;

    [SerializeField]
    private ResonancePuzzleManager resonancePuzzleManager;

    [SerializeField]
    private DungeonCameraController cameraController;

    [SerializeField]
    private PlayerController playerController;

    [SerializeField]
    private PlayerShaperController shaperController;

    [SerializeField]
    private PlayerPulseController pulseController;

    [SerializeField]
    private PlayerVisionController playerVisionController;

    [SerializeField]
    private WardenManager wardenManager;

    [SerializeField]
    private RunStatsManager runStatsManager;


    [Header("Run Introduction")]

    [Tooltip(
        "Delay after the first floor is ready before the objective introduction begins."
    )]
    [Min(0f)]
    [SerializeField]
    private float runIntroductionStartDelay =
        0.45f;

    [Tooltip(
        "How long the first key remains framed before moving to the hatch."
    )]
    [Min(0f)]
    [SerializeField]
    private float runIntroductionSigilHoldDuration =
        1.25f;

    [Tooltip(
        "How long the hatch remains framed before returning to the player."
    )]
    [Min(0f)]
    [SerializeField]
    private float runIntroductionHatchHoldDuration =
        1.25f;

    [Tooltip(
        "Temporary fog reveal radius around each objective shown by the introduction."
    )]
    [Min(0)]
    [SerializeField]
    private int runIntroductionRevealRadius =
        2;


    [Header("Easy Walkthrough")]

    [Tooltip(
        "How long the basic controls walkthrough remains visible on Easy."
    )]
    [Min(0f)]
    [SerializeField]
    private float easyWalkthroughHoldDuration =
        7.5f;

    [Tooltip(
        "Key used to skip the Easy controls walkthrough."
    )]
    [SerializeField]
    private KeyCode easyWalkthroughSkipKey =
        KeyCode.Space;


    [Header("Puzzle Replay Tutorial")]

    [Tooltip(
        "How long the camera remains on the replay panel before returning to the player."
    )]
    [Min(0f)]
    [SerializeField]
    private float puzzleReplayFocusHoldDuration =
        1.10f;


    [Header("Digger Tutorial")]

    [Tooltip(
        "Maximum Manhattan distance searched for a suitable Digger demonstration."
    )]
    [Min(1)]
    [SerializeField]
    private int diggerTargetSearchRadius =
        8;

    [Tooltip(
        "Delay between attempts when no suitable demonstration wall is available."
    )]
    [Min(0.05f)]
    [SerializeField]
    private float diggerTargetRetryDelay =
        0.40f;

    [Tooltip(
        "Time used to grow each ghost tunnel cell."
    )]
    [Min(0.02f)]
    [SerializeField]
    private float diggerGhostCellDuration =
        0.16f;

    [Tooltip(
        "How long the complete ghost tunnel remains visible."
    )]
    [Min(0f)]
    [SerializeField]
    private float diggerGhostHoldDuration =
        0.85f;

    [Range(0.2f, 1f)]
    [SerializeField]
    private float diggerGhostCellScale =
        0.84f;

    [SerializeField]
    private float diggerGhostZ =
        -2.60f;

    [SerializeField]
    private Color diggerGhostColour =
        new Color(
            0.80f,
            0.35f,
            1.00f,
            0.38f
        );


    [Header("Pulse Tutorial")]

    [Tooltip(
        "How long the tutorial stun remains visible on the selected enemy."
    )]
    [Min(0.1f)]
    [SerializeField]
    private float pulsePreviewStunDuration =
        2.0f;

    [Tooltip(
        "Time taken for the visual Pulse flash to expand."
    )]
    [Min(0.05f)]
    [SerializeField]
    private float pulsePreviewExpandDuration =
        0.30f;

    [SerializeField]
    private float pulsePreviewZ =
        -2.50f;

    [SerializeField]
    private Color pulsePreviewColour =
        new Color(
            0.75f,
            0.90f,
            1.00f,
            0.28f
        );

    [SerializeField]
    private Color pulseTutorialStunColour =
        new Color(
            0.45f,
            0.72f,
            1.00f,
            1.00f
        );


    private int observedGenerationVersion =
        -1;

    private bool runIntroductionLearned;

    private bool runIntroductionPending;

    private bool runIntroductionRunning;

    private int runIntroductionGenerationVersion =
        -1;

    private float runIntroductionReadyTime;

    private bool puzzleReplayTutorialLearned;

    private bool puzzleReplayTutorialStartedThisFloor;

    private bool puzzleReplayCameraShownThisFloor;

    private bool diggerTutorialLearned;

    private bool diggerTutorialPending;

    private bool diggerTutorialRunning;

    private float nextDiggerTargetSearchTime;

    private Coroutine activeTutorialCoroutine;

    private bool restorePlayerControllerAfterTutorial;

    private readonly List<EnemyController> frozenEnemyControllers =
        new List<EnemyController>();

    private readonly List<bool> frozenEnemyControllerStates =
        new List<bool>();

    private readonly List<WardenController> frozenWardenControllers =
        new List<WardenController>();

    private readonly List<bool> frozenWardenControllerStates =
        new List<bool>();

    private bool restoreWardenManagerAfterTutorial;

    private bool hostileActorsFrozen;

    private GameObject diggerPreviewRoot;

    private Material diggerPreviewMaterial;

    private bool pulseTutorialLearned;

    private bool pulseTutorialPending;

    private bool pulseTutorialRunning;

    private EnemyController pulseTutorialEnemy;

    private bool pulseEnemyControllerWasEnabled;

    private ActorSpriteAnimator pulseEnemyAnimator;

    private SpriteRenderer pulseEnemySpriteRenderer;

    private Color pulseEnemyOriginalColour =
        Color.white;

    private bool pulseEnemyColourRecorded;

    private GameObject pulsePreviewObject;

    private Material pulsePreviewMaterial;


    private bool hasPrompt;

    private string promptText =
        string.Empty;

    private bool promptUsesKeycap;

    private string promptKeyLabel =
        string.Empty;

    private bool promptHasWorldTarget;

    private Vector3 promptWorldPosition;


    public bool HasPrompt =>
        hasPrompt;

    public string PromptText =>
        promptText;

    public bool PromptUsesKeycap =>
        promptUsesKeycap;

    public string PromptKeyLabel =>
        promptKeyLabel;

    public bool PromptHasWorldTarget =>
        promptHasWorldTarget;

    public Vector3 PromptWorldPosition =>
        promptWorldPosition;


    private void Start()
    {
        ResolveReferences();
    }


    private void Update()
    {
        ResolveReferences();

        DetectFloorChange();

        if (runIntroductionRunning ||
            diggerTutorialRunning ||
            pulseTutorialRunning)
        {
            return;
        }

        TryStartRunIntroduction();

        if (activeTutorialCoroutine != null)
        {
            return;
        }

        UpdatePuzzleReplayTutorial();

        TryStartPendingDiggerTutorial();

        TryStartPendingPulseTutorial();
    }


    private void DetectFloorChange()
    {
        if (dungeonGenerator == null)
        {
            return;
        }

        if (observedGenerationVersion ==
            dungeonGenerator.GenerationVersion)
        {
            return;
        }

        observedGenerationVersion =
            dungeonGenerator.GenerationVersion;

        if (runIntroductionPending &&
            runIntroductionGenerationVersion < 0)
        {
            runIntroductionGenerationVersion =
                observedGenerationVersion;

            runIntroductionReadyTime =
                Time.unscaledTime +
                runIntroductionStartDelay;
        }
        else if (runIntroductionPending &&
                 observedGenerationVersion !=
                    runIntroductionGenerationVersion)
        {
            runIntroductionPending =
                false;

            runIntroductionLearned =
                true;

            if (playerVisionController != null)
            {
                playerVisionController
                    .ClearTutorialFocusVisibility();
            }
        }

        puzzleReplayTutorialStartedThisFloor =
            false;

        puzzleReplayCameraShownThisFloor =
            false;

        ClearPrompt();
    }


    private void TryStartRunIntroduction()
    {
        if (!runIntroductionPending ||
            runIntroductionLearned ||
            runIntroductionRunning ||
            activeTutorialCoroutine != null ||
            dungeonGenerator == null ||
            dungeonGenerator.Grid == null ||
            dungeonGenerator.ObjectiveManager == null ||
            playerController == null ||
            !playerController.IsAlive)
        {
            return;
        }

        if (observedGenerationVersion !=
                runIntroductionGenerationVersion ||
            Time.unscaledTime <
                runIntroductionReadyTime)
        {
            return;
        }

        FloorObjectiveManager objectiveManager =
            dungeonGenerator.ObjectiveManager;

        if (objectiveManager.ObjectiveCells == null ||
            objectiveManager.ObjectiveCells.Count == 0 ||
            objectiveManager.RequiredSigils <= 0)
        {
            return;
        }

        Vector2Int sigilCell =
            FindNearestObjectiveCell(
                objectiveManager.ObjectiveCells
            );

        Vector2Int hatchCell =
            dungeonGenerator.GetExitPosition();

        runIntroductionPending =
            false;

        activeTutorialCoroutine =
            StartCoroutine(
                PlayRunIntroduction(
                    sigilCell,
                    hatchCell,
                    objectiveManager.RequiredSigils
                )
            );
    }


    private Vector2Int FindNearestObjectiveCell(
        IReadOnlyCollection<Vector2Int> objectiveCells)
    {
        Vector2Int playerCell =
            playerController != null
                ? playerController.GridPosition
                : Vector2Int.zero;

        Vector2Int selected =
            Vector2Int.zero;

        int bestDistance =
            int.MaxValue;

        bool found =
            false;

        foreach (Vector2Int cell in
                 objectiveCells)
        {
            int distance =
                Mathf.Abs(
                    cell.x -
                    playerCell.x
                ) +
                Mathf.Abs(
                    cell.y -
                    playerCell.y
                );

            if (!found ||
                distance <
                    bestDistance)
            {
                selected =
                    cell;

                bestDistance =
                    distance;

                found =
                    true;
            }
        }

        return selected;
    }


    private IEnumerator PlayRunIntroduction(
        Vector2Int sigilCell,
        Vector2Int hatchCell,
        int requiredSigils)
    {
        runIntroductionRunning =
            true;

        LockPlayerMovement();

        yield return null;

        if (cameraController != null)
        {
            cameraController
                .BeginTutorialSequence();
        }

        Vector3 sigilWorldPosition =
            GridCellToWorld(
                sigilCell,
                -2.1f
            );

        if (playerVisionController != null)
        {
            playerVisionController
                .SetTutorialFocusVisibility(
                    sigilCell,
                    runIntroductionRevealRadius
                );
        }

        ShowPrompt(
            $"COLLECT {requiredSigils} KEYS\nTO UNLOCK THE HATCH",
            false,
            string.Empty,
            true,
            sigilWorldPosition
        );

        if (cameraController != null)
        {
            yield return
                cameraController
                    .PanTutorialSequenceTo(
                        sigilWorldPosition
                    );
        }

        if (runIntroductionSigilHoldDuration > 0f)
        {
            yield return
                new WaitForSecondsRealtime(
                    runIntroductionSigilHoldDuration
                );
        }

        Vector3 hatchWorldPosition =
            GridCellToWorld(
                hatchCell,
                -2f
            );

        if (playerVisionController != null)
        {
            playerVisionController
                .SetTutorialFocusVisibility(
                    hatchCell,
                    runIntroductionRevealRadius
                );
        }

        ShowPrompt(
            "REACH THE HATCH\nTO DESCEND",
            false,
            string.Empty,
            true,
            hatchWorldPosition
        );

        if (cameraController != null)
        {
            yield return
                cameraController
                    .PanTutorialSequenceTo(
                        hatchWorldPosition
                    );
        }

        if (runIntroductionHatchHoldDuration > 0f)
        {
            yield return
                new WaitForSecondsRealtime(
                    runIntroductionHatchHoldDuration
                );
        }

        ClearPrompt();

        if (playerVisionController != null)
        {
            playerVisionController
                .ClearTutorialFocusVisibility();
        }

        if (cameraController != null)
        {
            cameraController
                .EndTutorialSequence(
                    true
                );
        }

        if (runStatsManager != null &&
            runStatsManager.CurrentDifficulty ==
                RunStatsManager.RunDifficulty.Easy)
        {
            string pulseKey =
                pulseController != null
                    ? pulseController.DeployKey.ToString()
                    : "Q";

            string diggerKey =
                shaperController != null
                    ? shaperController.DiggerKey.ToString()
                    : "F";

            string skipKey =
                easyWalkthroughSkipKey.ToString();

            ShowPrompt(
                "USE ARROWS OR W A S D TO MOVE\n" +
                "STAY OUT OF ENEMY VIEW\n" +
                $"PRESS {pulseKey} TO THROW A PULSE AND STUN ENEMIES BRIEFLY\n" +
                $"PRESS {diggerKey} TO USE THE DIGGER AND OPEN A PATH BETWEEN ROOMS\n" +
                "DON'T LET THE WARDEN CATCH YOU\n\n" +
                $"PRESS {skipKey} TO SKIP",
                false,
                string.Empty,
                false,
                Vector3.zero
            );

            float walkthroughEndTime =
                Time.unscaledTime +
                Mathf.Max(0f, easyWalkthroughHoldDuration);

            while (Time.unscaledTime <
                   walkthroughEndTime)
            {
                if (Input.GetKeyDown(
                        easyWalkthroughSkipKey))
                {
                    break;
                }

                yield return null;
            }

            ClearPrompt();
        }

        RestorePlayerMovement();

        runIntroductionLearned =
            true;

        runIntroductionRunning =
            false;

        activeTutorialCoroutine =
            null;

        UnityEngine.Debug.Log(
            "RUN INTRODUCTION COMPLETE - " +
            "Key and hatch objectives shown."
        );
    }


    private void UpdatePuzzleReplayTutorial()
    {
        if (puzzleReplayTutorialLearned ||
            resonancePuzzleManager == null)
        {
            return;
        }

        if (resonancePuzzleManager.PuzzleSolved)
        {
            ClearPrompt();
            return;
        }

        if (resonancePuzzleManager
                .ReplayPanelActivatedThisFloor)
        {
            puzzleReplayTutorialLearned =
                true;

            ClearPrompt();

            UnityEngine.Debug.Log(
                "PUZZLE TUTORIAL LEARNED - " +
                "Replay panel activated."
            );

            return;
        }

        if (!resonancePuzzleManager.HasReplayPanel)
        {
            ClearPrompt();
            return;
        }

        /*
         * Room.Contains() uses the rectangular room bounds. Once the tutorial
         * starts it remains active on connected organic room cells as well.
         */
        if (!puzzleReplayTutorialStartedThisFloor)
        {
            if (!resonancePuzzleManager
                    .PlayerCurrentlyInsidePuzzleRoom ||
                !resonancePuzzleManager
                    .InitialSequenceShown ||
                resonancePuzzleManager
                    .IsShowingSequence)
            {
                ClearPrompt();
                return;
            }

            puzzleReplayTutorialStartedThisFloor =
                true;

            UnityEngine.Debug.Log(
                "PUZZLE TUTORIAL STARTED - " +
                "Replay panel guidance active."
            );
        }

        ShowPrompt(
            "STEP ON THIS PANEL\nTO REPLAY THE COLOUR CLUE",
            false,
            string.Empty,
            true,
            resonancePuzzleManager
                .ReplayPanelWorldPosition
        );

        if (!puzzleReplayCameraShownThisFloor &&
            activeTutorialCoroutine == null)
        {
            puzzleReplayCameraShownThisFloor =
                true;

            activeTutorialCoroutine =
                StartCoroutine(
                    PlayPuzzleReplayCameraIntroduction()
                );
        }
    }


    private IEnumerator PlayPuzzleReplayCameraIntroduction()
    {
        LockPlayerMovement();

        yield return null;

        if (cameraController != null &&
            resonancePuzzleManager != null &&
            resonancePuzzleManager.HasReplayPanel)
        {
            Coroutine cameraSequence =
                cameraController.PlayTutorialFocus(
                    resonancePuzzleManager
                        .ReplayPanelWorldPosition,
                    puzzleReplayFocusHoldDuration
                );

            if (cameraSequence != null)
            {
                yield return cameraSequence;
            }
        }

        RestorePlayerMovement();

        activeTutorialCoroutine =
            null;
    }


    /// <summary>
    /// Queues the Digger demonstration after a Digger charge is collected.
    /// </summary>
    public void NotifyDiggerCollected()
    {
        if (diggerTutorialLearned ||
            diggerTutorialPending ||
            diggerTutorialRunning)
        {
            return;
        }

        diggerTutorialPending =
            true;

        nextDiggerTargetSearchTime =
            0f;

        UnityEngine.Debug.Log(
            "DIGGER TUTORIAL QUEUED - " +
            "Digger charge collected."
        );
    }


    private void TryStartPendingDiggerTutorial()
    {
        if (!diggerTutorialPending ||
            diggerTutorialLearned ||
            activeTutorialCoroutine != null ||
            shaperController == null ||
            playerController == null ||
            !playerController.IsAlive)
        {
            return;
        }

        if (Time.unscaledTime <
            nextDiggerTargetSearchTime)
        {
            return;
        }

        nextDiggerTargetSearchTime =
            Time.unscaledTime +
            diggerTargetRetryDelay;

        if (!shaperController.TryFindTutorialTarget(
                diggerTargetSearchRadius,
                out ShaperPathResult target))
        {
            return;
        }

        diggerTutorialPending =
            false;

        activeTutorialCoroutine =
            StartCoroutine(
                PlayDiggerTutorial(target)
            );
    }


    private IEnumerator PlayDiggerTutorial(
        ShaperPathResult target)
    {
        if (target == null)
        {
            activeTutorialCoroutine =
                null;

            yield break;
        }

        diggerTutorialRunning =
            true;

        Vector3 wallWorldPosition =
            GridCellToWorld(
                target.WallCell,
                diggerGhostZ
            );

        LockPlayerMovement();

        ShowPrompt(
            "USE DIGGER\nTO CARVE THROUGH WALLS",
            true,
            "F",
            true,
            wallWorldPosition
        );

        yield return null;

        float previewDuration =
            target.GuideCells.Count *
                diggerGhostCellDuration +
            diggerGhostHoldDuration;

        Coroutine cameraSequence =
            null;

        if (cameraController != null)
        {
            cameraSequence =
                cameraController.PlayTutorialFocus(
                    wallWorldPosition,
                    previewDuration + 0.20f
                );

            yield return
                new WaitForSecondsRealtime(
                    cameraController
                        .TutorialPanDuration
                );
        }

        yield return
            PlayDiggerGhostPreview(
                target
            );

        if (cameraSequence != null)
        {
            yield return cameraSequence;
        }

        ClearDiggerPreview();

        ClearPrompt();

        RestorePlayerMovement();

        diggerTutorialLearned =
            true;

        diggerTutorialRunning =
            false;

        activeTutorialCoroutine =
            null;

        UnityEngine.Debug.Log(
            "DIGGER TUTORIAL LEARNED - " +
            "Ghost excavation shown."
        );
    }


    private IEnumerator PlayDiggerGhostPreview(
        ShaperPathResult target)
    {
        if (target == null ||
            target.GuideCells == null ||
            target.GuideCells.Count == 0)
        {
            yield break;
        }

        CreateDiggerPreviewMaterial();

        if (diggerPreviewMaterial == null)
        {
            yield break;
        }

        ClearDiggerPreviewRoot();

        diggerPreviewRoot =
            new GameObject(
                "Digger Tutorial Preview"
            );

        foreach (Vector2Int cell in
                 target.GuideCells)
        {
            GameObject ghostCell =
                CreateDiggerGhostCell(
                    cell
                );

            if (ghostCell == null)
            {
                continue;
            }

            float elapsed =
                0f;

            while (elapsed <
                   diggerGhostCellDuration)
            {
                elapsed +=
                    Time.unscaledDeltaTime;

                float progress =
                    diggerGhostCellDuration > 0f
                        ? Mathf.Clamp01(
                            elapsed /
                            diggerGhostCellDuration
                        )
                        : 1f;

                float scale =
                    Mathf.Lerp(
                        0.18f,
                        diggerGhostCellScale,
                        Mathf.SmoothStep(
                            0f,
                            1f,
                            progress
                        )
                    );

                ghostCell.transform.localScale =
                    new Vector3(
                        scale,
                        scale,
                        1f
                    );

                yield return null;
            }

            ghostCell.transform.localScale =
                new Vector3(
                    diggerGhostCellScale,
                    diggerGhostCellScale,
                    1f
                );
        }

        if (diggerGhostHoldDuration > 0f)
        {
            yield return
                new WaitForSecondsRealtime(
                    diggerGhostHoldDuration
                );
        }
    }


    private GameObject CreateDiggerGhostCell(
        Vector2Int cell)
    {
        if (diggerPreviewRoot == null ||
            diggerPreviewMaterial == null)
        {
            return null;
        }

        GameObject ghostCell =
            GameObject.CreatePrimitive(
                PrimitiveType.Quad
            );

        ghostCell.name =
            $"Digger Preview ({cell.x}, {cell.y})";

        ghostCell.transform.SetParent(
            diggerPreviewRoot.transform
        );

        ghostCell.transform.position =
            GridCellToWorld(
                cell,
                diggerGhostZ
            );

        ghostCell.transform.localScale =
            new Vector3(
                0.18f,
                0.18f,
                1f
            );

        Collider collider =
            ghostCell.GetComponent<Collider>();

        if (collider != null)
        {
            Destroy(
                collider
            );
        }

        Renderer renderer =
            ghostCell.GetComponent<Renderer>();

        if (renderer != null)
        {
            renderer.sharedMaterial =
                diggerPreviewMaterial;
        }

        return ghostCell;
    }


    private void CreateDiggerPreviewMaterial()
    {
        if (diggerPreviewMaterial != null)
        {
            diggerPreviewMaterial.color =
                diggerGhostColour;

            return;
        }

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

        if (shader == null)
        {
            return;
        }

        diggerPreviewMaterial =
            new Material(
                shader
            );

        diggerPreviewMaterial.color =
            diggerGhostColour;
    }


    private void ClearDiggerPreview()
    {
        ClearDiggerPreviewRoot();

        if (diggerPreviewMaterial != null)
        {
            Destroy(
                diggerPreviewMaterial
            );

            diggerPreviewMaterial =
                null;
        }
    }


    private void ClearDiggerPreviewRoot()
    {
        if (diggerPreviewRoot != null)
        {
            Destroy(
                diggerPreviewRoot
            );

            diggerPreviewRoot =
                null;
        }
    }



    /// <summary>
    /// Queues the Pulse demonstration after a Pulse charge is collected.
    /// </summary>
    public void NotifyPulseCollected()
    {
        if (pulseTutorialLearned ||
            pulseTutorialPending ||
            pulseTutorialRunning)
        {
            return;
        }

        pulseTutorialPending =
            true;

        UnityEngine.Debug.Log(
            "PULSE TUTORIAL QUEUED - " +
            "Pulse charge collected."
        );
    }


    private void TryStartPendingPulseTutorial()
    {
        if (!pulseTutorialPending ||
            pulseTutorialLearned ||
            activeTutorialCoroutine != null ||
            pulseController == null ||
            playerController == null ||
            !playerController.IsAlive)
        {
            return;
        }

        EnemyController target =
            FindNearestPulseTutorialEnemy();

        if (target == null)
        {
            return;
        }

        pulseTutorialPending =
            false;

        activeTutorialCoroutine =
            StartCoroutine(
                PlayPulseTutorial(
                    target
                )
            );
    }


    private EnemyController FindNearestPulseTutorialEnemy()
    {
        EnemyController[] enemies =
            FindObjectsOfType<EnemyController>();

        if (enemies == null ||
            enemies.Length == 0 ||
            playerController == null)
        {
            return null;
        }

        Vector2Int playerCell =
            playerController.GridPosition;

        EnemyController nearest =
            null;

        int nearestDistance =
            int.MaxValue;

        foreach (EnemyController enemy in
                 enemies)
        {
            if (enemy == null ||
                !enemy.isActiveAndEnabled ||
                enemy.CurrentState ==
                    EnemyController.EnemyState.Stunned)
            {
                continue;
            }

            SpriteRenderer enemySprite =
                enemy.GetComponentInChildren<SpriteRenderer>(
                    true
                );

            if (enemySprite == null)
            {
                continue;
            }

            Vector2Int enemyCell =
                enemy.GridPosition;

            int distance =
                Mathf.Abs(
                    enemyCell.x -
                    playerCell.x
                ) +
                Mathf.Abs(
                    enemyCell.y -
                    playerCell.y
                );

            if (distance <
                nearestDistance)
            {
                nearest =
                    enemy;

                nearestDistance =
                    distance;
            }
        }

        return nearest;
    }


    private IEnumerator PlayPulseTutorial(
        EnemyController target)
    {
        if (target == null)
        {
            activeTutorialCoroutine =
                null;

            yield break;
        }

        pulseTutorialRunning =
            true;

        Vector3 targetWorldPosition =
            new Vector3(
                target.GridPosition.x + 0.5f,
                target.GridPosition.y + 0.5f,
                pulsePreviewZ
            );

        string keyLabel =
            pulseController != null
                ? pulseController.DeployKey.ToString()
                : "Q";

        LockPlayerMovement();

        ShowPrompt(
            "USE PULSE\nTO STUN NEARBY ENEMIES",
            true,
            keyLabel,
            true,
            targetWorldPosition
        );

        yield return null;

        PreparePulseTutorialEnemy(
            target
        );

        float previewDuration =
            pulsePreviewExpandDuration +
            pulsePreviewStunDuration +
            0.25f;

        Coroutine cameraSequence =
            null;

        if (cameraController != null)
        {
            cameraSequence =
                cameraController.PlayTutorialFocus(
                    targetWorldPosition,
                    previewDuration
                );

            yield return
                new WaitForSecondsRealtime(
                    cameraController
                        .TutorialPanDuration
                );
        }

        yield return
            PlayPulseVisualPreview(
                targetWorldPosition
            );

        if (cameraSequence != null)
        {
            yield return cameraSequence;
        }

        RestorePulseTutorialEnemy();

        ClearPulsePreview();

        ClearPrompt();

        RestorePlayerMovement();

        pulseTutorialLearned =
            true;

        pulseTutorialRunning =
            false;

        activeTutorialCoroutine =
            null;

        UnityEngine.Debug.Log(
            "PULSE TUTORIAL LEARNED - " +
            "Enemy stun demonstration shown."
        );
    }


    private IEnumerator PlayPulseVisualPreview(
        Vector3 centre)
    {
        CreatePulsePreviewMaterial();

        if (pulsePreviewMaterial != null)
        {
            pulsePreviewObject =
                GameObject.CreatePrimitive(
                    PrimitiveType.Quad
                );

            pulsePreviewObject.name =
                "Pulse Tutorial Preview";

            pulsePreviewObject.transform.position =
                centre;

            pulsePreviewObject.transform.localScale =
                new Vector3(
                    0.20f,
                    0.20f,
                    1f
                );

            Collider collider =
                pulsePreviewObject.GetComponent<Collider>();

            if (collider != null)
            {
                Destroy(
                    collider
                );
            }

            Renderer renderer =
                pulsePreviewObject.GetComponent<Renderer>();

            if (renderer != null)
            {
                renderer.sharedMaterial =
                    pulsePreviewMaterial;
            }

            float elapsed =
                0f;

            float radius =
                pulseController != null
                    ? pulseController.BlastRadius
                    : 3.5f;

            float fullScale =
                Mathf.Max(
                    0.5f,
                    radius * 2f
                );

            while (elapsed <
                   pulsePreviewExpandDuration)
            {
                elapsed +=
                    Time.unscaledDeltaTime;

                float progress =
                    Mathf.Clamp01(
                        elapsed /
                        Mathf.Max(
                            0.01f,
                            pulsePreviewExpandDuration
                        )
                    );

                float scale =
                    Mathf.Lerp(
                        0.20f,
                        fullScale,
                        Mathf.SmoothStep(
                            0f,
                            1f,
                            progress
                        )
                    );

                pulsePreviewObject.transform.localScale =
                    new Vector3(
                        scale,
                        scale,
                        1f
                    );

                yield return null;
            }

            pulsePreviewObject.transform.localScale =
                new Vector3(
                    fullScale,
                    fullScale,
                    1f
                );
        }

        if (pulseEnemyAnimator != null &&
            pulseEnemyAnimator.isActiveAndEnabled)
        {
            pulseEnemyAnimator
                .BeginTutorialStunPreview();
        }
        else
        {
            SetPulseTutorialEnemyTint(
                true
            );
        }

        if (pulsePreviewObject != null)
        {
            Destroy(
                pulsePreviewObject
            );

            pulsePreviewObject =
                null;
        }

        if (pulsePreviewStunDuration > 0f)
        {
            yield return
                new WaitForSecondsRealtime(
                    pulsePreviewStunDuration
                );
        }

        if (pulseEnemyAnimator != null &&
            pulseEnemyAnimator.isActiveAndEnabled)
        {
            pulseEnemyAnimator
                .EndTutorialStunPreview();
        }
        else
        {
            SetPulseTutorialEnemyTint(
                false
            );
        }
    }


    private void PreparePulseTutorialEnemy(
        EnemyController target)
    {
        RestorePulseTutorialEnemy();

        pulseTutorialEnemy =
            target;

        if (pulseTutorialEnemy == null)
        {
            return;
        }

        pulseEnemyControllerWasEnabled =
            pulseTutorialEnemy.enabled;

        pulseTutorialEnemy.enabled =
            false;

        pulseEnemyAnimator =
            pulseTutorialEnemy.GetComponent<ActorSpriteAnimator>();

        pulseEnemySpriteRenderer =
            pulseTutorialEnemy.GetComponentInChildren<SpriteRenderer>(
                true
            );

        if (pulseEnemySpriteRenderer != null)
        {
            pulseEnemyOriginalColour =
                pulseEnemySpriteRenderer.color;

            pulseEnemyColourRecorded =
                true;
        }
    }


    private void SetPulseTutorialEnemyTint(
        bool stunned)
    {
        if (pulseEnemySpriteRenderer == null)
        {
            return;
        }

        if (stunned)
        {
            pulseEnemySpriteRenderer.color =
                pulseTutorialStunColour;

            return;
        }

        if (pulseEnemyColourRecorded)
        {
            pulseEnemySpriteRenderer.color =
                pulseEnemyOriginalColour;
        }
        else
        {
            pulseEnemySpriteRenderer.color =
                Color.white;
        }
    }


    private void RestorePulseTutorialEnemy()
    {
        SetPulseTutorialEnemyTint(
            false
        );

        if (pulseEnemyAnimator != null)
        {
            pulseEnemyAnimator
                .EndTutorialStunPreview();
        }

        if (pulseTutorialEnemy != null)
        {
            pulseTutorialEnemy.enabled =
                pulseEnemyControllerWasEnabled;
        }

        pulseTutorialEnemy =
            null;

        pulseEnemyControllerWasEnabled =
            false;

        pulseEnemyAnimator =
            null;

        pulseEnemySpriteRenderer =
            null;

        pulseEnemyColourRecorded =
            false;
    }


    private void CreatePulsePreviewMaterial()
    {
        if (pulsePreviewMaterial != null)
        {
            pulsePreviewMaterial.color =
                pulsePreviewColour;

            return;
        }

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

        if (shader == null)
        {
            return;
        }

        pulsePreviewMaterial =
            new Material(
                shader
            );

        pulsePreviewMaterial.color =
            pulsePreviewColour;
    }


    private void ClearPulsePreview()
    {
        if (pulsePreviewObject != null)
        {
            Destroy(
                pulsePreviewObject
            );

            pulsePreviewObject =
                null;
        }

        if (pulsePreviewMaterial != null)
        {
            Destroy(
                pulsePreviewMaterial
            );

            pulsePreviewMaterial =
                null;
        }
    }


    private Vector3 GridCellToWorld(
        Vector2Int cell,
        float z)
    {
        return new Vector3(
            cell.x + 0.5f,
            cell.y + 0.5f,
            z
        );
    }


    private void ShowPrompt(
        string message,
        bool usesKeycap,
        string keyLabel,
        bool hasWorldTarget,
        Vector3 worldPosition)
    {
        hasPrompt =
            true;

        promptText =
            message;

        promptUsesKeycap =
            usesKeycap;

        promptKeyLabel =
            keyLabel;

        promptHasWorldTarget =
            hasWorldTarget;

        promptWorldPosition =
            worldPosition;
    }


    private void ClearPrompt()
    {
        hasPrompt =
            false;

        promptText =
            string.Empty;

        promptUsesKeycap =
            false;

        promptKeyLabel =
            string.Empty;

        promptHasWorldTarget =
            false;

        promptWorldPosition =
            Vector3.zero;
    }


    private void LockPlayerMovement()
    {
        FreezeHostileActors();

        if (playerController == null)
        {
            return;
        }

        restorePlayerControllerAfterTutorial =
            playerController.enabled;

        if (restorePlayerControllerAfterTutorial)
        {
            playerController.enabled =
                false;
        }
    }


    private void RestorePlayerMovement()
    {
        if (playerController != null &&
            restorePlayerControllerAfterTutorial)
        {
            playerController.enabled =
                true;
        }

        restorePlayerControllerAfterTutorial =
            false;

        RestoreHostileActors();
    }


    private void FreezeHostileActors()
    {
        if (hostileActorsFrozen)
        {
            return;
        }

        hostileActorsFrozen =
            true;

        frozenEnemyControllers.Clear();
        frozenEnemyControllerStates.Clear();

        EnemyController[] enemies =
            FindObjectsOfType<EnemyController>();

        for (int i = 0;
             i < enemies.Length;
             i++)
        {
            EnemyController enemy =
                enemies[i];

            if (enemy == null)
            {
                continue;
            }

            frozenEnemyControllers.Add(
                enemy
            );

            frozenEnemyControllerStates.Add(
                enemy.enabled
            );

            enemy.enabled =
                false;
        }

        frozenWardenControllers.Clear();
        frozenWardenControllerStates.Clear();

        WardenController[] wardens =
            FindObjectsOfType<WardenController>();

        for (int i = 0;
             i < wardens.Length;
             i++)
        {
            WardenController warden =
                wardens[i];

            if (warden == null)
            {
                continue;
            }

            frozenWardenControllers.Add(
                warden
            );

            frozenWardenControllerStates.Add(
                warden.enabled
            );

            warden.enabled =
                false;
        }

        if (wardenManager == null)
        {
            wardenManager =
                FindObjectOfType<WardenManager>();
        }

        restoreWardenManagerAfterTutorial =
            wardenManager != null &&
            wardenManager.enabled;

        if (restoreWardenManagerAfterTutorial)
        {
            wardenManager.enabled =
                false;
        }
    }


    private void RestoreHostileActors()
    {
        if (!hostileActorsFrozen)
        {
            return;
        }

        for (int i = 0;
             i < frozenEnemyControllers.Count &&
             i < frozenEnemyControllerStates.Count;
             i++)
        {
            EnemyController enemy =
                frozenEnemyControllers[i];

            if (enemy != null)
            {
                enemy.enabled =
                    frozenEnemyControllerStates[i];
            }
        }

        frozenEnemyControllers.Clear();
        frozenEnemyControllerStates.Clear();

        for (int i = 0;
             i < frozenWardenControllers.Count &&
             i < frozenWardenControllerStates.Count;
             i++)
        {
            WardenController warden =
                frozenWardenControllers[i];

            if (warden != null)
            {
                warden.enabled =
                    frozenWardenControllerStates[i];
            }
        }

        frozenWardenControllers.Clear();
        frozenWardenControllerStates.Clear();

        if (wardenManager != null &&
            restoreWardenManagerAfterTutorial)
        {
            wardenManager.enabled =
                true;
        }

        restoreWardenManagerAfterTutorial =
            false;

        hostileActorsFrozen =
            false;
    }


    /// <summary>
    /// Resets per-run tutorial state and optionally queues the opening objective introduction.
    /// </summary>
    public void ResetTutorialsForNewRun(
        bool showRunIntroduction = true)
    {
        if (activeTutorialCoroutine != null)
        {
            StopCoroutine(
                activeTutorialCoroutine
            );

            activeTutorialCoroutine =
                null;
        }

        RestorePlayerMovement();

        if (cameraController != null)
        {
            cameraController.CancelTutorialFocus(
                true
            );
        }

        ClearDiggerPreview();

        observedGenerationVersion =
            -1;

        runIntroductionLearned =
            !showRunIntroduction;

        runIntroductionPending =
            showRunIntroduction;

        runIntroductionRunning =
            false;

        runIntroductionGenerationVersion =
            -1;

        runIntroductionReadyTime =
            0f;

        if (playerVisionController != null)
        {
            playerVisionController
                .ClearTutorialFocusVisibility();
        }

        puzzleReplayTutorialLearned =
            false;

        puzzleReplayTutorialStartedThisFloor =
            false;

        puzzleReplayCameraShownThisFloor =
            false;

        diggerTutorialLearned =
            false;

        diggerTutorialPending =
            false;

        diggerTutorialRunning =
            false;

        nextDiggerTargetSearchTime =
            0f;

        pulseTutorialLearned =
            false;

        pulseTutorialPending =
            false;

        pulseTutorialRunning =
            false;

        RestorePulseTutorialEnemy();

        ClearPulsePreview();

        ClearPrompt();

        UnityEngine.Debug.Log(
            "GAMEPLAY TUTORIALS RESET - New run."
        );
    }


    private void OnDisable()
    {
        RestorePlayerMovement();

        if (playerVisionController != null)
        {
            playerVisionController
                .ClearTutorialFocusVisibility();
        }

        ClearDiggerPreview();

        RestorePulseTutorialEnemy();

        ClearPulsePreview();

        if (cameraController != null &&
            cameraController.TutorialFocusActive)
        {
            cameraController.CancelTutorialFocus(
                true
            );
        }

        runIntroductionRunning =
            false;

        diggerTutorialRunning =
            false;

        pulseTutorialRunning =
            false;
    }


    private void ResolveReferences()
    {
        if (dungeonGenerator == null)
        {
            dungeonGenerator =
                FindObjectOfType<DungeonGenerator>();
        }

        if (resonancePuzzleManager == null)
        {
            resonancePuzzleManager =
                FindObjectOfType<ResonancePuzzleManager>();
        }

        if (cameraController == null)
        {
            cameraController =
                FindObjectOfType<DungeonCameraController>();
        }

        if (playerController == null)
        {
            playerController =
                FindObjectOfType<PlayerController>();
        }

        if (shaperController == null)
        {
            shaperController =
                FindObjectOfType<PlayerShaperController>();
        }

        if (pulseController == null)
        {
            pulseController =
                FindObjectOfType<PlayerPulseController>();
        }

        if (playerVisionController == null)
        {
            playerVisionController =
                FindObjectOfType<PlayerVisionController>();
        }

        if (wardenManager == null)
        {
            wardenManager =
                FindObjectOfType<WardenManager>();
        }

        if (runStatsManager == null)
        {
            runStatsManager =
                FindObjectOfType<RunStatsManager>();
        }
    }
}
