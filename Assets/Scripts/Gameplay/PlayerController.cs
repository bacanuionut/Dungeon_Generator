using System.Diagnostics;
using UnityEngine;

/// <summary>
/// Controls grid-based player movement through the generated dungeon.
///
/// Movement is validated against DungeonGrid rather than against
/// rendered GameObjects. This keeps gameplay dependent on the dungeon
/// data rather than its visual representation.
/// </summary>
public class PlayerController : MonoBehaviour
{
    [Header("References")]

    [Tooltip("Generator containing the dungeon the player can move through.")]
    [SerializeField]
    private DungeonGenerator dungeonGenerator;


    [Header("Movement")]

    [Tooltip("Minimum delay between grid movement steps.")]
    [SerializeField]
    private float movementDelay = 0.12f;


    // Current logical position in the dungeon grid.
    private Vector2Int gridPosition;

    // Prevents movement input from firing every rendered frame.
    private float nextMovementTime;

    // Player becomes active only after being placed into
    // a successfully generated dungeon.
    private bool initialised;


    /// <summary>
    /// Places the player at a valid generated starting position.
    /// </summary>
    private void Start()
    {
        // Initialisation is triggered by DungeonGenerator after
        // successful generation rather than relying on Unity's
        // Start execution order.
    }


    /// <summary>
    /// Reads keyboard input and attempts grid movement.
    /// </summary>
    private void Update()
    {
        if (!initialised)
        {
            return;
        }

        if (Time.time < nextMovementTime)
        {
            return;
        }

        Vector2Int direction = ReadMovementInput();

        if (direction == Vector2Int.zero)
        {
            return;
        }

        TryMove(direction);

        nextMovementTime =
            Time.time + movementDelay;
    }


    /// <summary>
    /// Finds a valid spawn coordinate from DungeonGenerator.
    /// </summary>
    public void InitialisePlayer()
    {
        if (dungeonGenerator == null)
        {
            UnityEngine.Debug.LogError(
                "PlayerController has no DungeonGenerator reference."
            );

            return;
        }

        if (dungeonGenerator.Grid == null ||
            dungeonGenerator.Grid.FloorCellCount == 0)
        {
            UnityEngine.Debug.LogError(
                "Player cannot initialise because the dungeon grid is empty."
            );

            return;
        }

        gridPosition =
            dungeonGenerator.GetPlayerSpawnPosition();

        if (!dungeonGenerator.Grid.IsWalkable(gridPosition))
        {
            UnityEngine.Debug.LogError(
                $"Generated player spawn {gridPosition} is not walkable."
            );

            return;
        }

        UpdateWorldPosition();

        initialised = true;

        UnityEngine.Debug.Log(
            $"Player spawned at grid position {gridPosition}."
        );
    }


    /// <summary>
    /// Converts keyboard input into one of the four grid directions.
    ///
    /// WASD and arrow keys are both supported.
    /// Only cardinal movement is allowed.
    /// </summary>
    private Vector2Int ReadMovementInput()
    {
        if (Input.GetKey(KeyCode.W) ||
            Input.GetKey(KeyCode.UpArrow))
        {
            return Vector2Int.up;
        }

        if (Input.GetKey(KeyCode.S) ||
            Input.GetKey(KeyCode.DownArrow))
        {
            return Vector2Int.down;
        }

        if (Input.GetKey(KeyCode.A) ||
            Input.GetKey(KeyCode.LeftArrow))
        {
            return Vector2Int.left;
        }

        if (Input.GetKey(KeyCode.D) ||
            Input.GetKey(KeyCode.RightArrow))
        {
            return Vector2Int.right;
        }

        return Vector2Int.zero;
    }


    /// <summary>
    /// Attempts to move one cell in the requested direction.
    ///
    /// Movement succeeds only if the target coordinate exists
    /// in the final DungeonGrid.
    /// </summary>
    private void TryMove(Vector2Int direction)
    {
        Vector2Int targetPosition =
            gridPosition + direction;

        if (!dungeonGenerator.Grid.IsWalkable(targetPosition))
        {
            return;
        }

        gridPosition = targetPosition;

        UpdateWorldPosition();
    }


    /// <summary>
    /// Converts the logical grid coordinate into the world-space
    /// centre of the corresponding dungeon cell.
    /// </summary>
    private void UpdateWorldPosition()
    {
        transform.position =
            new Vector3(
                gridPosition.x + 0.5f,
                gridPosition.y + 0.5f,
                -1f
            );
    }
}