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

    // Direction the player is currently facing.
    //
    // This is deliberately separate from the player's position because
    // facing will later control torch visibility, Pulse Charge placement
    // and Shaper targeting.
    private Vector2Int facingDirection =
        Vector2Int.down;


    public Vector2Int FacingDirection =>
        facingDirection;

    private GameObject facingIndicator;

    private Material facingIndicatorMaterial;

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

    private int treasuresCollected;

    public int TreasuresCollected => treasuresCollected;

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
        treasuresCollected = 0;

        
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

        CreateFacingIndicator();

        UpdateFacingIndicator();

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

        // Pressing a direction turns the player even when movement in that
        // direction is blocked by a wall.
        if (direction != Vector2Int.zero)
        {
            facingDirection = direction;

            UpdateFacingIndicator();
        }

        Vector2Int targetPosition =
            gridPosition + direction;

        if (!dungeonGenerator.Grid.IsNavigable(targetPosition))
        {
            return;
        }

        gridPosition = targetPosition;        

        UpdateWorldPosition();

        movementCount++;

        CheckForObjective();

        CheckForItem();

        CheckForExit();
    }

    /// <summary>
    /// Checks whether the player has moved onto a generated
    /// floor-objective item.
    /// </summary>
    private void CheckForObjective()
    {
        if (dungeonGenerator == null ||
            dungeonGenerator.ObjectiveManager == null)
        {
            return;
        }


        dungeonGenerator.ObjectiveManager.TryCollectSigil(
            gridPosition
        );
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
    ///
    /// The descent shaft remains locked until all required
    /// Anchor Sigils have been collected.
    /// </summary>
    private void CheckForExit()
    {
        if (dungeonGenerator == null)
        {
            return;
        }

        if (gridPosition !=
            dungeonGenerator.GetExitPosition())
        {
            return;
        }

        FloorObjectiveManager objectiveManager =
            dungeonGenerator.ObjectiveManager;

        if (objectiveManager != null &&
            !objectiveManager.ExitUnlocked)
        {
            UnityEngine.Debug.Log(
                "DESCENT SHAFT LOCKED - " +
                $"Anchor Sigils: " +
                $"{objectiveManager.CollectedSigils}/" +
                $"{objectiveManager.RequiredSigils}"
            );

            return;
        }

        dungeonGenerator.CompleteDungeon(
            movementCount
        );
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

    /// <summary>
    /// Checks whether the player's current grid position contains a
    /// procedurally generated collectible.
    /// </summary>
    private void CheckForItem()
    {
        if (dungeonGenerator == null ||
            dungeonGenerator.ContentGenerator == null)
        {
            return;
        }


        if (dungeonGenerator.ContentGenerator.TryCollectItem(
                gridPosition))
        {
            treasuresCollected++;

            UnityEngine.Debug.Log(
                $"TREASURE COLLECTED - " +
                $"Total: {treasuresCollected}"
            );
        }
    }

    /// <summary>
    /// Creates a small marker showing the direction the player is facing.
    ///
    /// Facing is gameplay data because it will later control torch
    /// visibility, Pulse Charge placement and Shaper targeting.
    /// </summary>
    private void CreateFacingIndicator()
    {
        if (facingIndicator != null)
            return;


        facingIndicator =
            GameObject.CreatePrimitive(
                PrimitiveType.Quad
            );


        facingIndicator.name =
            "Facing Indicator";


        facingIndicator.transform.SetParent(
            transform,
            false
        );


        facingIndicator.transform.localScale =
            new Vector3(
                0.18f,
                0.18f,
                1f
            );


        Collider collider =
            facingIndicator.GetComponent<Collider>();


        if (collider != null)
        {
            Destroy(
                collider
            );
        }


        Shader shader =
            Shader.Find(
                "Sprites/Default"
            );


        if (shader != null)
        {
            facingIndicatorMaterial =
                new Material(
                    shader
                );


            facingIndicatorMaterial.color =
                Color.white;


            Renderer renderer =
                facingIndicator.GetComponent<Renderer>();


            if (renderer != null)
            {
                renderer.sharedMaterial =
                    facingIndicatorMaterial;
            }
        }
    }

    private void UpdateFacingIndicator()
    {
        if (facingIndicator == null)
            return;


        Vector3 offset =
            new Vector3(
                facingDirection.x * 0.32f,
                facingDirection.y * 0.32f,
                -0.05f
            );


        facingIndicator.transform.localPosition =
            offset;
    }
}