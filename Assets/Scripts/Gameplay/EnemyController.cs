using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

/// <summary>
/// Grid-based enemy AI with directional perception.
///
/// The enemy can patrol, chase, investigate the player's last known
/// position, return to its patrol area and attack.
///
/// Detection requires:
/// 1. The player to be inside the vision range.
/// 2. The player to be inside the enemy's facing cone.
/// 3. A clear line of sight through walkable dungeon cells.
/// </summary>
public class EnemyController : MonoBehaviour
{
    public enum EnemyState
    {
        Patrol,
        Chase,
        Investigate,
        Return,
        Attack,
        Stunned
    }


    [Header("Movement")]

    [Tooltip("Delay between enemy grid movements.")]
    [SerializeField]
    private float movementDelay = 0.35f;


    [Header("Perception")]

    [Tooltip("Maximum distance at which this enemy can see.")]
    [SerializeField]
    private float visionRange = 7f;

    [Tooltip("Total width of the enemy's directional vision cone.")]
    [Range(20f, 180f)]
    [SerializeField]
    private float visionAngle = 70f;


    [Header("Investigation")]

    [Tooltip(
        "How long the enemy waits at the player's last visible " +
        "position before giving up."
    )]
    [SerializeField]
    private float investigationWaitTime = 1.25f;


    [Header("Combat")]

    [Tooltip("Damage caused by one successful attack.")]
    [SerializeField]
    private int attackDamage = 1;

    [Tooltip("Minimum time between attacks.")]
    [SerializeField]
    private float attackCooldown = 1f;

    private Renderer enemyRenderer;

    private Color normalColour;

    private Color stunnedColour =
        new Color(
            0.35f,
            0.65f,
            1f,
            1f
        );

    private float stunEndTime;

    private EnemyState stateBeforeStun;

    private DungeonGenerator dungeonGenerator;

    private PlayerController playerController;

    private Room patrolRoom;


    private Vector2Int gridPosition;

    private Vector2Int patrolTarget;

    private Vector2Int facingDirection =
        Vector2Int.down;


    private Vector2Int lastKnownPlayerPosition;

    private bool hasLastKnownPlayerPosition;

    private bool waitingAtLastKnownPosition;


    private System.Random random;


    private float nextMovementTime;

    private float nextAttackTime;

    private float investigationEndTime;


    private bool initialised;


    private EnemyState currentState;


    public Vector2Int GridPosition =>
        gridPosition;

    public Vector2Int FacingDirection =>
        facingDirection;

    public EnemyState CurrentState =>
        currentState;

    public float VisionRange =>
        visionRange;

    public float VisionAngle =>
        visionAngle;

    public DungeonGrid Grid =>
    dungeonGenerator != null
        ? dungeonGenerator.Grid
        : null;


    /// <summary>
    /// Initialises one procedurally generated enemy.
    /// </summary>
    public void Initialise(
        DungeonGenerator generator,
        PlayerController player,
        Room room,
        Vector2Int startingPosition,
        int enemySeed)
    {
        dungeonGenerator = generator;

        playerController = player;

        patrolRoom = room;

        gridPosition = startingPosition;


        random = new System.Random(enemySeed);


        currentState = EnemyState.Patrol;


        hasLastKnownPlayerPosition = false;

        waitingAtLastKnownPosition = false;

        investigationEndTime = 0f;

        enemyRenderer = GetComponent<Renderer>();

        if (enemyRenderer != null)
        {
            MaterialPropertyBlock properties =
                new MaterialPropertyBlock();

            enemyRenderer.GetPropertyBlock(properties);

            normalColour =
                properties.GetColor("_Color");

            // Fallback in case this enemy was created without
            // a colour property block.
            if (normalColour.a <= 0f)
            {
                normalColour =
                    enemyRenderer.material.color;
            }
        }


        ChooseNewPatrolTarget();


        // Initially face towards the first patrol destination.
        UpdateFacingTowards(patrolTarget);


        UpdateWorldPosition();


        nextMovementTime =
            Time.time + movementDelay;

        nextAttackTime =
            Time.time;


        initialised =
            true;
    }


    private void Update()
    {
        if (!initialised)
            return;


        if (dungeonGenerator == null ||
            dungeonGenerator.Grid == null ||
            playerController == null)
        {
            return;
        }


        if (!playerController.IsAlive)
            return;

        if (currentState == EnemyState.Stunned)
        {
            UpdateStunned();
            return;
        }

        UpdateState();


        // Attack has its own cooldown and therefore does not use
        // the normal movement timer.
        if (currentState == EnemyState.Attack)
        {
            UpdateAttack();
            return;
        }


        if (Time.time < nextMovementTime)
        {
            return;
        }


        nextMovementTime =
            Time.time +
            movementDelay;


        switch (currentState)
        {
            case EnemyState.Patrol:

                UpdatePatrol();

                break;


            case EnemyState.Chase:

                UpdateChase();

                break;


            case EnemyState.Investigate:

                UpdateInvestigate();

                break;


            case EnemyState.Return:

                UpdateReturn();

                break;
        }
    }


    /// <summary>
    /// Chooses the enemy state according to what the enemy can
    /// currently perceive.
    /// </summary>
    private void UpdateState()
    {
        Vector2Int playerPosition =
            playerController.GridPosition;


        bool playerVisible =
            IsCellVisible(
                playerPosition
            );


        int distanceToPlayer =
            ManhattanDistance(
                gridPosition,
                playerPosition
            );


        EnemyState previousState =
            currentState;


        if (playerVisible)
        {
            // Only visible player positions are remembered.
            // Once visibility is lost the enemy receives no further
            // information about where the player went.
            lastKnownPlayerPosition =
                playerPosition;

            hasLastKnownPlayerPosition =
                true;

            waitingAtLastKnownPosition =
                false;


            if (distanceToPlayer <= 1)
            {
                currentState =
                    EnemyState.Attack;
            }
            else
            {
                currentState =
                    EnemyState.Chase;
            }
        }
        else
        {
            switch (currentState)
            {
                case EnemyState.Chase:
                case EnemyState.Attack:

                    if (hasLastKnownPlayerPosition)
                    {
                        currentState =
                            EnemyState.Investigate;

                        waitingAtLastKnownPosition =
                            false;
                    }
                    else
                    {
                        ChooseReturnOrPatrolState();
                    }

                    break;


                case EnemyState.Investigate:

                    // UpdateInvestigate handles movement to the
                    // remembered player location.
                    break;


                case EnemyState.Return:

                    if (patrolRoom.Contains(
                            gridPosition))
                    {
                        currentState =
                            EnemyState.Patrol;
                    }

                    break;


                case EnemyState.Patrol:

                    // Continue normal patrol.
                    break;
            }
        }


        if (previousState !=
            currentState)
        {
            UnityEngine.Debug.Log(
                $"{name} changed AI state: " +
                $"{previousState} -> {currentState}"
            );


            if (currentState ==
                EnemyState.Investigate &&
                hasLastKnownPlayerPosition)
            {
                UnityEngine.Debug.Log(
                    $"{name} investigating last known position " +
                    $"({lastKnownPlayerPosition.x}, " +
                    $"{lastKnownPlayerPosition.y})"
                );
            }


            if (currentState ==
                EnemyState.Patrol)
            {
                ChooseNewPatrolTarget();
            }
        }
    }


    /// <summary>
    /// Moves toward random destinations inside the enemy's assigned
    /// patrol room.
    /// </summary>
    private void UpdatePatrol()
    {
        if (gridPosition ==
            patrolTarget)
        {
            ChooseNewPatrolTarget();
        }


        Vector2Int nextCell;


        if (TryGetNextPathCell(
                gridPosition,
                patrolTarget,
                true,
                out nextCell))
        {
            MoveTo(
                nextCell
            );
        }
        else
        {
            ChooseNewPatrolTarget();
        }
    }


    /// <summary>
    /// Follows the player's currently visible position.
    /// </summary>
    private void UpdateChase()
    {
        Vector2Int playerPosition =
            playerController.GridPosition;


        if (ManhattanDistance(
                gridPosition,
                playerPosition) <= 1)
        {
            return;
        }


        Vector2Int nextCell;


        if (TryGetNextPathCell(
                gridPosition,
                playerPosition,
                false,
                out nextCell))
        {
            MoveTo(
                nextCell
            );
        }
    }


    /// <summary>
    /// Moves to the last grid cell where the player was actually
    /// visible.
    ///
    /// Reaching a new position can expose the player around a corner,
    /// in which case UpdateState immediately returns to Chase.
    /// </summary>
    private void UpdateInvestigate()
    {
        if (!hasLastKnownPlayerPosition)
        {
            ChooseReturnOrPatrolState();
            return;
        }


        if (gridPosition !=
            lastKnownPlayerPosition)
        {
            Vector2Int nextCell;


            if (TryGetNextPathCell(
                    gridPosition,
                    lastKnownPlayerPosition,
                    false,
                    out nextCell))
            {
                MoveTo(
                    nextCell
                );
            }
            else
            {
                hasLastKnownPlayerPosition =
                    false;

                waitingAtLastKnownPosition =
                    false;

                ChooseReturnOrPatrolState();
            }


            return;
        }


        // The enemy has reached the location where the player
        // disappeared.
        if (!waitingAtLastKnownPosition)
        {
            waitingAtLastKnownPosition =
                true;


            investigationEndTime =
                Time.time +
                investigationWaitTime;


            UnityEngine.Debug.Log(
                $"{name} reached last known player position " +
                $"({lastKnownPlayerPosition.x}, " +
                $"{lastKnownPlayerPosition.y}) and is searching."
            );


            return;
        }


        if (Time.time <
            investigationEndTime)
        {
            return;
        }


        hasLastKnownPlayerPosition =
            false;

        waitingAtLastKnownPosition =
            false;


        EnemyState previousState =
            currentState;


        ChooseReturnOrPatrolState();


        UnityEngine.Debug.Log(
            $"{name} could not reacquire the player. " +
            $"{previousState} -> {currentState}"
        );
    }


    /// <summary>
    /// Returns to the enemy's original room after losing the player.
    /// </summary>
    private void UpdateReturn()
    {
        if (patrolRoom.Contains(
                gridPosition))
        {
            currentState =
                EnemyState.Patrol;

            ChooseNewPatrolTarget();

            return;
        }


        Vector2Int nextCell;


        if (TryGetNextPathCell(
                gridPosition,
                patrolRoom.Centre,
                false,
                out nextCell))
        {
            MoveTo(
                nextCell
            );
        }
    }


    private void UpdateAttack()
    {
        if (!playerController.IsAlive)
            return;


        // Attack requires the player to remain genuinely visible.
        if (!IsCellVisible(
                playerController.GridPosition))
        {
            return;
        }


        if (ManhattanDistance(
                gridPosition,
                playerController.GridPosition) > 1)
        {
            return;
        }


        if (Time.time <
            nextAttackTime)
        {
            return;
        }


        nextAttackTime =
            Time.time +
            attackCooldown;


        playerController.TakeDamage(
            attackDamage
        );


        UnityEngine.Debug.Log(
            $"{name} attacked the player. " +
            $"Damage: {attackDamage}"
        );
    }


    /// <summary>
    /// Returns true only when a target cell is inside this enemy's
    /// directional cone and has an unobstructed line of sight.
    ///
    /// The visual torch cone uses this exact method too.
    /// </summary>
    public bool IsCellVisible(Vector2Int targetCell)
    {
        if (!initialised ||
            dungeonGenerator == null ||
            dungeonGenerator.Grid == null)
        {
            return false;
        }


        return DungeonVisibilityUtility.IsCellVisibleInCone(
            dungeonGenerator.Grid,
            gridPosition,
            targetCell,
            facingDirection,
            visionRange,
            visionAngle
        );
    }


    /// <summary>
    /// Returns the walkable grid cells currently visible to this
    /// enemy.
    ///
    /// Used to render the torch cone and later useful for debugging
    /// perception behaviour.
    /// </summary>
    public List<Vector2Int> GetVisibleCells()
    {
        if (!initialised ||
            dungeonGenerator == null ||
            dungeonGenerator.Grid == null)
        {
            return new List<Vector2Int>();
        }


        return DungeonVisibilityUtility.GetVisibleWalkableCellsInCone(
            dungeonGenerator.Grid,
            gridPosition,
            facingDirection,
            visionRange,
            visionAngle
        );
    }


    private void ChooseNewPatrolTarget()
    {
        if (patrolRoom == null ||
            dungeonGenerator == null ||
            dungeonGenerator.Grid == null)
        {
            return;
        }


        const int maximumAttempts =
            30;


        for (int attempt = 0;
             attempt < maximumAttempts;
             attempt++)
        {
            int x =
                random.Next(
                    patrolRoom.Bounds.xMin,
                    patrolRoom.Bounds.xMax
                );

            int y =
                random.Next(
                    patrolRoom.Bounds.yMin,
                    patrolRoom.Bounds.yMax
                );


            Vector2Int candidate =
                new Vector2Int(
                    x,
                    y
                );


            if (dungeonGenerator.Grid.IsWalkable(candidate) &&
                candidate !=
                    playerController.GridPosition)
            {
                patrolTarget =
                    candidate;

                return;
            }
        }


        patrolTarget =
            gridPosition;
    }


    private void ChooseReturnOrPatrolState()
    {
        if (patrolRoom.Contains(
                gridPosition))
        {
            currentState =
                EnemyState.Patrol;

            ChooseNewPatrolTarget();
        }
        else
        {
            currentState =
                EnemyState.Return;
        }
    }


    /// <summary>
    /// Finds one step of a shortest grid path using breadth-first
    /// search.
    ///
    /// Patrol paths stay inside the assigned room. Chase,
    /// investigation and return paths may use the complete dungeon.
    /// </summary>
    private bool TryGetNextPathCell(
        Vector2Int start,
        Vector2Int destination,
        bool restrictToPatrolRoom,
        out Vector2Int nextCell)
    {
        nextCell =
            start;


        if (start ==
            destination)
        {
            return false;
        }


        Queue<Vector2Int> frontier =
            new Queue<Vector2Int>();

        HashSet<Vector2Int> visited =
            new HashSet<Vector2Int>();

        Dictionary<Vector2Int, Vector2Int> cameFrom =
            new Dictionary<Vector2Int, Vector2Int>();


        frontier.Enqueue(
            start
        );

        visited.Add(
            start
        );


        Vector2Int[] directions =
        {
            Vector2Int.up,
            Vector2Int.down,
            Vector2Int.left,
            Vector2Int.right
        };


        bool destinationFound =
            false;


        while (frontier.Count > 0)
        {
            Vector2Int current =
                frontier.Dequeue();


            if (current ==
                destination)
            {
                destinationFound =
                    true;

                break;
            }


            foreach (Vector2Int direction in
                     directions)
            {
                Vector2Int neighbour =
                    current +
                    direction;


                if (visited.Contains(
                        neighbour))
                {
                    continue;
                }


                if (!dungeonGenerator.Grid.IsWalkable(
                        neighbour))
                {
                    continue;
                }


                if (restrictToPatrolRoom &&
                    !patrolRoom.Contains(
                        neighbour))
                {
                    continue;
                }


                visited.Add(
                    neighbour
                );


                cameFrom[neighbour] =
                    current;


                frontier.Enqueue(
                    neighbour
                );
            }
        }


        if (!destinationFound)
            return false;


        Vector2Int pathCell =
            destination;


        while (cameFrom.ContainsKey(pathCell) &&
               cameFrom[pathCell] !=
                    start)
        {
            pathCell =
                cameFrom[pathCell];
        }


        if (!cameFrom.ContainsKey(
                pathCell))
        {
            return false;
        }


        nextCell =
            pathCell;


        return true;
    }


    private void MoveTo(
        Vector2Int targetCell)
    {
        if (!dungeonGenerator.Grid.IsWalkable(
                targetCell))
        {
            return;
        }


        if (targetCell ==
            playerController.GridPosition)
        {
            return;
        }


        UpdateFacingTowards(
            targetCell
        );


        gridPosition =
            targetCell;


        UpdateWorldPosition();
    }


    private void UpdateFacingTowards(
        Vector2Int targetCell)
    {
        Vector2Int difference =
            targetCell -
            gridPosition;


        if (difference == Vector2Int.zero)
            return;


        // All current enemy movement is cardinal, so the movement
        // direction is also the enemy's facing direction.
        if (Mathf.Abs(difference.x) >
            Mathf.Abs(difference.y))
        {
            facingDirection =
                difference.x > 0
                    ? Vector2Int.right
                    : Vector2Int.left;
        }
        else
        {
            facingDirection =
                difference.y > 0
                    ? Vector2Int.up
                    : Vector2Int.down;
        }
    }


    private void UpdateWorldPosition()
    {
        transform.position =
            new Vector3(
                gridPosition.x + 0.5f,
                gridPosition.y + 0.5f,
                -2f
            );
    }


    private int ManhattanDistance(
        Vector2Int a,
        Vector2Int b)
    {
        return
            Mathf.Abs(
                a.x - b.x
            ) +
            Mathf.Abs(
                a.y - b.y
            );
    }

    /// <summary>
    /// Temporarily disables this enemy.
    ///
    /// Stun removes its knowledge of the player's previous position.
    /// When the effect ends, the enemy must perceive the player again
    /// before it can resume a chase.
    /// </summary>
    public void ApplyStun(
        float duration)
    {

        if (!initialised ||
            duration <= 0f)
        {
            return;
        }


        SetEnemyColour(stunnedColour);


        stateBeforeStun =
            currentState;


        currentState =
            EnemyState.Stunned;


        stunEndTime =
            Time.time +
            duration;


        // A Pulse disrupts the enemy's pursuit memory.
        // It will need to see the player again after recovering.
        hasLastKnownPlayerPosition =
            false;

        waitingAtLastKnownPosition =
            false;

        UnityEngine.Debug.Log(
            $"{name} STUNNED for {duration:0.0} seconds."
        );
    }

    /// <summary>
    /// Keeps the enemy inactive until its stun timer expires.
    /// </summary>
    private void UpdateStunned()
    {
        if (Time.time <
            stunEndTime)
        {
            return;
        }


        RecoverFromStun();
    }

    /// <summary>
    /// Returns the enemy to sensible behaviour after a Pulse stun.
    ///
    /// The enemy does not magically remember where the player went.
    /// It can immediately chase only if the player is currently visible.
    /// </summary>
    private void RecoverFromStun()
    {
        Vector2Int playerPosition =
            playerController.GridPosition;


        bool playerVisible =
            IsCellVisible(
                playerPosition
            );


        if (playerVisible)
        {
            lastKnownPlayerPosition =
                playerPosition;

            hasLastKnownPlayerPosition =
                true;


            if (ManhattanDistance(
                    gridPosition,
                    playerPosition) <= 1)
            {
                currentState =
                    EnemyState.Attack;
            }
            else
            {
                currentState =
                    EnemyState.Chase;
            }
        }
        else
        {
            hasLastKnownPlayerPosition =
                false;


            if (patrolRoom != null &&
                patrolRoom.Contains(
                    gridPosition))
            {
                currentState =
                    EnemyState.Patrol;

                ChooseNewPatrolTarget();
            }
            else
            {
                currentState =
                    EnemyState.Return;
            }
        }


        SetEnemyColour(normalColour);


        UnityEngine.Debug.Log(
            $"{name} recovered from stun. " +
            $"New state: {currentState}"
        );
    }

    private void SetEnemyColour(Color colour)
    {
        if (enemyRenderer == null)
            return;

        MaterialPropertyBlock properties =
            new MaterialPropertyBlock();

        enemyRenderer.GetPropertyBlock(properties);

        properties.SetColor(
            "_Color",
            colour
        );

        enemyRenderer.SetPropertyBlock(
            properties
        );
    }
}