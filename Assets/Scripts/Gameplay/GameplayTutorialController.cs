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
        0.10f;

    [Tooltip(
        "How long the complete ghost tunnel remains visible."
    )]
    [Min(0f)]
    [SerializeField]
    private float diggerGhostHoldDuration =
        0.55f;

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


    private int observedGenerationVersion =
        -1;

    private bool puzzleReplayTutorialLearned;

    private bool puzzleReplayTutorialStartedThisFloor;

    private bool puzzleReplayCameraShownThisFloor;

    private bool diggerTutorialLearned;

    private bool diggerTutorialPending;

    private bool diggerTutorialRunning;

    private float nextDiggerTargetSearchTime;

    private Coroutine activeTutorialCoroutine;

    private bool restorePlayerControllerAfterTutorial;

    private GameObject diggerPreviewRoot;

    private Material diggerPreviewMaterial;


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

        if (diggerTutorialRunning)
        {
            return;
        }

        UpdatePuzzleReplayTutorial();

        TryStartPendingDiggerTutorial();
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

        puzzleReplayTutorialStartedThisFloor =
            false;

        puzzleReplayCameraShownThisFloor =
            false;

        ClearPrompt();
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
        yield return null;

        LockPlayerMovement();

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

        ShowPrompt(
            "USE DIGGER\nTO CARVE THROUGH WALLS",
            true,
            "F",
            true,
            wallWorldPosition
        );

        yield return null;

        LockPlayerMovement();

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
    }


    /// <summary>
    /// Resets tutorial state at the start of a run.
    /// </summary>
    public void ResetTutorialsForNewRun()
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

        ClearPrompt();

        UnityEngine.Debug.Log(
            "GAMEPLAY TUTORIALS RESET - New run."
        );
    }


    private void OnDisable()
    {
        RestorePlayerMovement();

        ClearDiggerPreview();

        if (cameraController != null &&
            cameraController.TutorialFocusActive)
        {
            cameraController.CancelTutorialFocus(
                true
            );
        }

        diggerTutorialRunning =
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
    }
}
