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

    [Header("Player Health")]

    [Tooltip("Health restored whenever a new dungeon is started.")]
    [SerializeField]
    private int maximumHealth = 5;

    private int currentHealth;


    public int CurrentHealth => currentHealth;

    public int MaximumHealth => maximumHealth;

    public bool IsAlive => currentHealth > 0;

    [Header("Movement")]

    [Tooltip("Minimum delay between grid movement steps.")]
    [SerializeField]
    private float movementDelay = 0.12f;

    // Current logical position in the dungeon grid.
    private Vector2Int gridPosition;

    /// <summary>
    /// Current player position in dungeon-grid coordinates.
    /// Enemy AI uses this rather than converting world positions back
    /// into grid coordinates.
    /// </summary>
    public Vector2Int GridPosition => gridPosition;

    // Prevents movement input from firing every rendered frame.
    private float nextMovementTime;

    // Player becomes active only after being placed into
    // a successfully generated dungeon.
    private bool initialised;

    // Number of successful grid movements made since the
    // current dungeon was generated.
    private int movementCount;

    public int MovementCount => movementCount;


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
        currentHealth = maximumHealth;
        movementCount = 0;

        UpdateWorldPosition();

        UnityEngine.Debug.Log(
            $"Player spawned at grid position {gridPosition}."
        );

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

        movementCount++;

        CheckForExit();
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

    /// <summary>
    /// Checks whether the player's current grid position is the
    /// generated exit position.
    /// </summary>
    private void CheckForExit()
    {
        if (dungeonGenerator == null)
        {
            return;
        }

        Vector2Int exitPosition =
            dungeonGenerator.GetExitPosition();

        if (gridPosition == exitPosition)
        {
            dungeonGenerator.CompleteDungeon(
                movementCount
            );
        }
    }

    /// <summary>
    /// Applies damage to the player.
    ///
    /// Enemy AI calls this when an enemy is adjacent and its
    /// attack cooldown has expired.
    /// </summary>
    public void TakeDamage(int damage)
    {
        if (!initialised || currentHealth <= 0)
            return;

        if (damage <= 0)
            return;

        currentHealth =
            Mathf.Max(
                0,
                currentHealth - damage
            );

        UnityEngine.Debug.Log(
            $"PLAYER HIT - Damage: {damage}. " +
            $"Health: {currentHealth}/{maximumHealth}"
        );


        if (currentHealth <= 0)
        {
            initialised = false;

            UnityEngine.Debug.Log(
                "========== GAME OVER ==========\n" +
                "Player health reached zero.\n" +
                "================================"
            );
        }
    }
}