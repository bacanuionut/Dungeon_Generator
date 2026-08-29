using System.Diagnostics;
using UnityEngine;

/// <summary>
/// Controls the two main dungeon camera modes.
///
/// Normal gameplay follows the player at a close zoom.
/// Overview mode frames the complete generated dungeon.
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


    private bool overviewMode;

    private Vector3 overviewCentre;

    private float overviewCameraSize;


    public bool OverviewMode =>
        overviewMode;


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
    /// Immediately places the camera on the player.
    ///
    /// Useful after a new procedural floor has been generated.
    /// </summary>
    public void SnapToPlayer()
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