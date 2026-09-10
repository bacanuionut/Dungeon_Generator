using System.Collections;
using System.Diagnostics;
using UnityEngine;

/// <summary>
/// Controls the main dungeon camera modes.
///
/// Normal gameplay follows the player at a close zoom.
/// Overview mode frames the complete generated dungeon.
/// Tutorial focus can temporarily pan to a generated world target,
/// then return immediately to the player.
/// </summary>
public class DungeonCameraController : MonoBehaviour
{
    [Header("References")]

    [SerializeField]
    private DungeonGenerator dungeonGenerator;

    [SerializeField]
    private PlayerController playerController;

    [SerializeField]
    private Camera targetCamera;


    [Header("Gameplay View")]

    [Tooltip("Orthographic size used while following the player.")]
    [SerializeField]
    private float gameplayCameraSize = 7f;

    [Tooltip("How quickly the camera follows the player.")]
    [SerializeField]
    private float followSpeed = 8f;


    [Header("Overview View")]

    [Tooltip("Key used to toggle the complete dungeon overview.")]
    [SerializeField]
    private KeyCode overviewKey = KeyCode.Tab;

    [Tooltip("Extra space shown around the dungeon in overview mode.")]
    [SerializeField]
    private float overviewPadding = 3f;

    [Tooltip("Speed used when changing between gameplay and overview zoom.")]
    [SerializeField]
    private float zoomSpeed = 6f;


    [Header("Tutorial Focus")]

    [Tooltip("Time taken to pan from the player to a tutorial target.")]
    [Min(0.10f)]
    [SerializeField]
    private float tutorialPanDuration = 0.75f;


    private bool overviewMode;

    private Vector3 overviewCentre;

    private float overviewCameraSize;

    private bool tutorialFocusActive;

    private Coroutine tutorialFocusCoroutine;


    public bool OverviewMode =>
        overviewMode;

    public bool TutorialFocusActive =>
        tutorialFocusActive;

    public float TutorialPanDuration =>
        tutorialPanDuration;


    private void Awake()
    {
        if (targetCamera == null)
        {
            targetCamera =
                GetComponent<Camera>();
        }
    }


    private void Update()
    {
        if (targetCamera == null ||
            dungeonGenerator == null ||
            playerController == null)
        {
            return;
        }


        /*
         * A tutorial focus sequence owns the camera while active.
         * Normal follow and Tab overview input are ignored until it ends.
         */
        if (tutorialFocusActive)
        {
            return;
        }


        if (Input.GetKeyDown(overviewKey))
        {
            ToggleOverview();
        }


        if (overviewMode)
        {
            UpdateOverviewCamera();
        }
        else
        {
            UpdateGameplayCamera();
        }
    }


    /// <summary>
    /// Follows the player while preserving the camera's Z position.
    /// </summary>
    private void UpdateGameplayCamera()
    {
        Vector3 targetPosition =
            playerController.transform.position;


        targetPosition.z =
            transform.position.z;


        transform.position =
            Vector3.Lerp(
                transform.position,
                targetPosition,
                followSpeed * Time.deltaTime
            );


        targetCamera.orthographicSize =
            Mathf.Lerp(
                targetCamera.orthographicSize,
                gameplayCameraSize,
                zoomSpeed * Time.deltaTime
            );
    }


    private void ToggleOverview()
    {
        if (tutorialFocusActive)
        {
            return;
        }


        overviewMode =
            !overviewMode;


        if (overviewMode)
        {
            CalculateOverviewView();

            UnityEngine.Debug.Log(
                "CAMERA MODE: OVERVIEW"
            );
        }
        else
        {
            UnityEngine.Debug.Log(
                "CAMERA MODE: GAMEPLAY"
            );
        }
    }


    /// <summary>
    /// Calculates a camera position and size which contains every
    /// walkable dungeon cell.
    /// </summary>
    private void CalculateOverviewView()
    {
        DungeonGrid grid =
            dungeonGenerator.Grid;


        if (grid == null ||
            grid.FloorCellCount == 0)
        {
            return;
        }


        int minimumX =
            int.MaxValue;

        int maximumX =
            int.MinValue;

        int minimumY =
            int.MaxValue;

        int maximumY =
            int.MinValue;


        foreach (Vector2Int cell in grid.FloorCells)
        {
            minimumX =
                Mathf.Min(
                    minimumX,
                    cell.x
                );

            maximumX =
                Mathf.Max(
                    maximumX,
                    cell.x
                );

            minimumY =
                Mathf.Min(
                    minimumY,
                    cell.y
                );

            maximumY =
                Mathf.Max(
                    maximumY,
                    cell.y
                );
        }


        float dungeonWidth =
            maximumX - minimumX + 1f;

        float dungeonHeight =
            maximumY - minimumY + 1f;


        overviewCentre =
            new Vector3(
                minimumX + dungeonWidth * 0.5f,
                minimumY + dungeonHeight * 0.5f,
                transform.position.z
            );


        float requiredVerticalSize =
            dungeonHeight * 0.5f;


        float requiredHorizontalSize =
            dungeonWidth /
            (2f * targetCamera.aspect);


        overviewCameraSize =
            Mathf.Max(
                requiredVerticalSize,
                requiredHorizontalSize
            ) +
            overviewPadding;
    }


    private void UpdateOverviewCamera()
    {
        transform.position =
            Vector3.Lerp(
                transform.position,
                overviewCentre,
                followSpeed * Time.deltaTime
            );


        targetCamera.orthographicSize =
            Mathf.Lerp(
                targetCamera.orthographicSize,
                overviewCameraSize,
                zoomSpeed * Time.deltaTime
            );
    }


    /// <summary>
    /// Starts a short tutorial camera focus.
    ///
    /// The camera pans to the supplied generated world position,
    /// waits there briefly, then snaps back to the player.
    ///
    /// The returned Coroutine can be yielded by the tutorial controller.
    /// </summary>
    public Coroutine PlayTutorialFocus(
        Vector3 worldPosition,
        float holdDuration)
    {
        if (targetCamera == null ||
            playerController == null)
        {
            return null;
        }


        if (tutorialFocusCoroutine != null)
        {
            StopCoroutine(
                tutorialFocusCoroutine
            );

            tutorialFocusCoroutine = null;
        }


        tutorialFocusCoroutine =
            StartCoroutine(
                TutorialFocusRoutine(
                    worldPosition,
                    Mathf.Max(0f, holdDuration)
                )
            );


        return tutorialFocusCoroutine;
    }


    private IEnumerator TutorialFocusRoutine(
        Vector3 worldPosition,
        float holdDuration)
    {
        tutorialFocusActive = true;
        overviewMode = false;


        Vector3 startPosition =
            transform.position;

        Vector3 targetPosition =
            new Vector3(
                worldPosition.x,
                worldPosition.y,
                transform.position.z
            );


        float startCameraSize =
            targetCamera.orthographicSize;

        float elapsed =
            0f;


        while (elapsed <
               tutorialPanDuration)
        {
            elapsed +=
                Time.unscaledDeltaTime;


            float progress =
                tutorialPanDuration > 0f
                    ? Mathf.Clamp01(
                        elapsed /
                        tutorialPanDuration
                    )
                    : 1f;


            progress =
                Mathf.SmoothStep(
                    0f,
                    1f,
                    progress
                );


            transform.position =
                Vector3.Lerp(
                    startPosition,
                    targetPosition,
                    progress
                );


            targetCamera.orthographicSize =
                Mathf.Lerp(
                    startCameraSize,
                    gameplayCameraSize,
                    progress
                );


            yield return null;
        }


        transform.position =
            targetPosition;

        targetCamera.orthographicSize =
            gameplayCameraSize;


        if (holdDuration > 0f)
        {
            yield return
                new WaitForSecondsRealtime(
                    holdDuration
                );
        }


        PlaceCameraOnPlayer();


        tutorialFocusActive = false;
        tutorialFocusCoroutine = null;
    }


    /// <summary>
    /// Cancels any active tutorial focus.
    ///
    /// Used when a run/floor changes while a tutorial camera sequence
    /// is still active.
    /// </summary>
    public void CancelTutorialFocus(
        bool snapToPlayer)
    {
        if (tutorialFocusCoroutine != null)
        {
            StopCoroutine(
                tutorialFocusCoroutine
            );

            tutorialFocusCoroutine = null;
        }


        tutorialFocusActive = false;


        if (snapToPlayer)
        {
            PlaceCameraOnPlayer();
        }
    }


    /// <summary>
    /// Immediately places the camera on the player.
    ///
    /// Useful after a new procedural floor has been generated.
    /// </summary>
    public void SnapToPlayer()
    {
        CancelTutorialFocus(
            false
        );

        PlaceCameraOnPlayer();
    }


    private void PlaceCameraOnPlayer()
    {
        if (playerController == null ||
            targetCamera == null)
        {
            return;
        }


        Vector3 position =
            playerController.transform.position;


        position.z =
            transform.position.z;


        transform.position =
            position;


        targetCamera.orthographicSize =
            gameplayCameraSize;


        overviewMode =
            false;
    }
}
