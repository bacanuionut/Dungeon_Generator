using System.Collections;
using UnityEngine;

/// <summary>
/// Coordinates lightweight gameplay tutorials which can point at
/// procedurally generated world objects and temporarily focus the camera.
///
/// The first implemented tutorial is the Resonance replay-panel explanation.
/// Later Digger, Pulse and opening-run demonstrations can reuse this controller.
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


    [Header("Puzzle Replay Tutorial")]

    [Tooltip(
        "How long the camera remains on the replay panel before snapping back."
    )]
    [Min(0f)]
    [SerializeField]
    private float puzzleReplayFocusHoldDuration =
        1.10f;


    private int observedGenerationVersion =
        -1;

    private bool puzzleReplayTutorialLearned;

    private bool puzzleReplayTutorialStartedThisFloor;

    private bool puzzleReplayCameraShownThisFloor;

    private Coroutine activeTutorialCoroutine;

    private bool restorePlayerControllerAfterTutorial;


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

        DetectFloorOrRunChange();

        UpdatePuzzleReplayTutorial();
    }


    private void DetectFloorOrRunChange()
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


        /*
         * Do not infer a new-run boundary from floor depth here.
         * DungeonRunManager will call ResetTutorialsForNewRun()
         * explicitly once we wire that small step after this first test.
         */
    }


    private void UpdatePuzzleReplayTutorial()
    {
        if (puzzleReplayTutorialLearned ||
            resonancePuzzleManager == null)
        {
            return;
        }


        /*
         * The replay tutorial is no longer needed on this floor if the
         * puzzle has already been solved. It is not marked learned here,
         * so a later puzzle room can still teach the mechanic if needed.
         */
        if (resonancePuzzleManager.PuzzleSolved)
        {
            ClearPrompt();
            return;
        }


        /*
         * Actually stepping onto the replay panel completes this tutorial
         * for the rest of the run.
         */
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
         * The tutorial only STARTS after the player has genuinely entered
         * the Puzzle room and watched the automatic clue sequence.
         *
         * Once started, it stays active for the rest of this floor until
         * the replay panel is actually stepped on. This is deliberate:
         * CA-shaped / organic room cells can extend beyond the original
         * rectangular Room bounds, so using Room.Contains every frame could
         * make the prompt disappear while the player is still visually in
         * the same puzzle space.
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
                "Replay panel guidance is now persistent until activation."
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
        /*
         * The prompt is already active before the pan begins.
         * If the panel is off-screen the HUD clamps the prompt to the edge.
         * As the camera moves, the prompt follows the real panel position.
         */
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
    /// Can later be called explicitly by the run manager
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


        puzzleReplayTutorialLearned =
            false;

        puzzleReplayTutorialStartedThisFloor =
            false;

        puzzleReplayCameraShownThisFloor =
            false;

        ClearPrompt();


        UnityEngine.Debug.Log(
            "GAMEPLAY TUTORIALS RESET - New run."
        );
    }


    private void OnDisable()
    {
        RestorePlayerMovement();


        if (cameraController != null &&
            cameraController.TutorialFocusActive)
        {
            cameraController.CancelTutorialFocus(
                true
            );
        }
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
    }
}
