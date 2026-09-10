using System.Collections;
using UnityEngine;

/// <summary>
/// Handles gameplay tutorial prompts and temporary camera focus.
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

        DetectFloorChange();

        UpdatePuzzleReplayTutorial();
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


        // A solved puzzle does not need the replay-panel prompt on this floor.
        if (resonancePuzzleManager.PuzzleSolved)
        {
            ClearPrompt();
            return;
        }


        // Using the replay panel completes this tutorial for the current run.
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


        // Room.Contains() uses the base room bounds, so the prompt stays active
        // after it starts even if the player moves onto connected organic cells.
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
        // Allow the prompt to render before moving the camera.
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


        observedGenerationVersion =
            -1;

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
