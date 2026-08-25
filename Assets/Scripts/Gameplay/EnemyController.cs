using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

/// <summary>
/// Basic grid-based enemy AI.
///
/// Enemies patrol inside their original room. If the player comes
/// within detection range, they switch to a chase state and use
/// breadth-first search to move through the dungeon toward the player.
/// </summary>
public class EnemyController : MonoBehaviour
{
    public enum EnemyState
    {
        Patrol,
        Chase,
        Investigate,
        Return,
        Attack
    }

    [Header("Combat")]

    [Tooltip("Damage dealt by one successful attack.")]
    [SerializeField]
    private int attackDamage = 1;

    [Tooltip("Minimum time between enemy attacks.")]
    [SerializeField]
    private float attackCooldown = 1.0f;

    private float nextAttackTime;

    [Header("Investigation")]

    [Tooltip("How long the enemy waits at the player's last known position before giving up.")]
    [SerializeField]
    private float investigationWaitTime = 1.25f;

    [Header("Movement")]

    [Tooltip("Delay between enemy grid movements.")]
    [SerializeField]
    private float movementDelay = 0.35f;

    [Tooltip("Grid distance at which the enemy begins chasing the player.")]
    [SerializeField]
    private int detectionRange = 6;


    // The most recent grid cell where this enemy actually saw the player.
    private Vector2Int lastKnownPlayerPosition;

    private bool hasLastKnownPlayerPosition;

    private bool waitingAtLastKnownPosition;

    private float investigationEndTime;

    private DungeonGenerator dungeonGenerator;
    private PlayerController playerController;
    private Room patrolRoom;

    private Vector2Int gridPosition;
    private Vector2Int patrolTarget;

    private System.Random random;

    private float nextMovementTime;

    private bool initialised;

    private EnemyState currentState;


    public Vector2Int GridPosition => gridPosition;

    public EnemyState CurrentState => currentState;


    /// <summary>
    /// Called immediately after the enemy is created procedurally.
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

        hasLastKnownPlayerPosition = false;
        waitingAtLastKnownPosition = false;
        investigationEndTime = 0f;

        gridPosition = startingPosition;

        random =
            new System.Random(enemySeed);

        currentState =
            EnemyState.Patrol;

        ChooseNewPatrolTarget();

        UpdateWorldPosition();

        nextMovementTime =
            Time.time + movementDelay;

        nextAttackTime = Time.time;

        initialised = true;
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

        // Stop acting once the player has been defeated.
        if (!playerController.IsAlive)
            return;


        UpdateState();


        // Attacking uses its own cooldown rather than the normal
        // movement delay.
        if (currentState == EnemyState.Attack)
        {
            UpdateAttack();
            return;
        }


        if (Time.time < nextMovementTime)
            return;

        nextMovementTime =
            Time.time + movementDelay;


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
    /// Updates enemy behaviour using distance, line of sight and the
    /// player's last visible grid position.
    /// </summary>
    private void UpdateState()
    {
        int distanceToPlayer =
            ManhattanDistance(
                gridPosition,
                playerController.GridPosition
            );


        bool playerVisible =
            HasLineOfSightToPlayer();


        bool playerDetected =
            distanceToPlayer <= detectionRange &&
            playerVisible;


        EnemyState previousState =
            currentState;


        // If the player is currently visible and inside the detection
        // range, remember exactly where they were seen.
        if (playerDetected)
        {
            lastKnownPlayerPosition =
                playerController.GridPosition;

            hasLastKnownPlayerPosition =
                true;

            waitingAtLastKnownPosition =
                false;


            // Adjacent visible enemies attack.
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

                    // The enemy has just lost the player.
                    // Instead of immediately returning home, travel to
                    // the last place where the player was actually seen.
                    if (hasLastKnownPlayerPosition)
                    {
                        currentState =
                            EnemyState.Investigate;
                    }
                    else
                    {
                        ChooseReturnOrPatrolState();
                    }

                    break;


                case EnemyState.Investigate:

                    // Investigation movement and waiting are handled by
                    // UpdateInvestigate().
                    break;


                case EnemyState.Return:

                    if (patrolRoom.Contains(gridPosition))
                    {
                        currentState =
                            EnemyState.Patrol;
                    }

                    break;


                case EnemyState.Patrol:

                    // Remain on patrol when the player is not visible.
                    break;
            }
        }


        if (previousState != currentState)
        {
            UnityEngine.Debug.Log(
                $"{name} changed AI state: " +
                $"{previousState} -> {currentState}"
            );


            if (currentState == EnemyState.Investigate &&
                hasLastKnownPlayerPosition)
            {
                waitingAtLastKnownPosition =
                    false;

                UnityEngine.Debug.Log(
                    $"{name} investigating last known player position: " +
                    $"({lastKnownPlayerPosition.x}, " +
                    $"{lastKnownPlayerPosition.y})"
                );
            }


            if (currentState == EnemyState.Patrol)
            {
                ChooseNewPatrolTarget();
            }
        }
    }


    /// <summary>
    /// Moves toward a randomly chosen walkable cell inside the room
    /// where this enemy originally spawned.
    /// </summary>
    private void UpdatePatrol()
    {
        if (gridPosition == patrolTarget)
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
            MoveTo(nextCell);
        }
        else
        {
            ChooseNewPatrolTarget();
        }
    }


    /// <summary>
    /// Uses the same dungeon grid as the player and finds a shortest
    /// path toward the player's current position.
    /// </summary>
    private void UpdateChase()
    {
        Vector2Int playerPosition =
            playerController.GridPosition;


        // Do not move onto the player's cell.
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
            MoveTo(nextCell);
        }
    }


    /// <summary>
    /// Travels to the player's last visible grid position.
    ///
    /// If the player becomes visible again, UpdateState() immediately
    /// switches back to Chase or Attack.
    ///
    /// If the enemy reaches the remembered position and still cannot see
    /// the player, it waits briefly before returning to its patrol room.
    /// </summary>
    private void UpdateInvestigate()
    {
        if (!hasLastKnownPlayerPosition)
        {
            ChooseReturnOrPatrolState();
            return;
        }


        // Still travelling toward the last place where the player was seen.
        if (gridPosition != lastKnownPlayerPosition)
        {
            Vector2Int nextCell;


            if (TryGetNextPathCell(
                    gridPosition,
                    lastKnownPlayerPosition,
                    false,
                    out nextCell))
            {
                MoveTo(nextCell);
            }
            else
            {
                // If the remembered position unexpectedly cannot be
                // reached, abandon the investigation safely.
                hasLastKnownPlayerPosition =
                    false;

                waitingAtLastKnownPosition =
                    false;

                ChooseReturnOrPatrolState();
            }


            return;
        }


        // The enemy has reached the last visible player location.
        if (!waitingAtLastKnownPosition)
        {
            waitingAtLastKnownPosition =
                true;

            investigationEndTime =
                Time.time + investigationWaitTime;


            UnityEngine.Debug.Log(
                $"{name} reached the player's last known position " +
                $"({lastKnownPlayerPosition.x}, " +
                $"{lastKnownPlayerPosition.y}) and is searching."
            );

            return;
        }


        // Wait briefly at the location before deciding the player escaped.
        if (Time.time < investigationEndTime)
            return;


        hasLastKnownPlayerPosition =
            false;

        waitingAtLastKnownPosition =
            false;


        EnemyState previousState =
            currentState;


        ChooseReturnOrPatrolState();


        UnityEngine.Debug.Log(
            $"{name} could not find the player. " +
            $"{previousState} -> {currentState}"
        );
    }

    /// <summary>
    /// Damages the player while the enemy occupies an adjacent grid cell.
    ///
    /// The enemy does not enter the player's cell. Instead it remains
    /// adjacent and attacks according to a cooldown.
    /// </summary>
    private void UpdateAttack()
    {
        if (!playerController.IsAlive)
            return;

        // Do not attack through a wall even if the two grid positions
        // happen to be close together.
        if (!HasLineOfSightToPlayer())
            return;


        int distanceToPlayer =
            ManhattanDistance(
                gridPosition,
                playerController.GridPosition
            );


        // UpdateState will move the AI back into Chase on the next frame
        // if the player has moved away.
        if (distanceToPlayer > 1)
            return;


        if (Time.time < nextAttackTime)
            return;


        nextAttackTime =
            Time.time + attackCooldown;


        playerController.TakeDamage(
            attackDamage
        );


        UnityEngine.Debug.Log(
            $"{name} attacked the player. " +
            $"Damage: {attackDamage}"
        );
    }

    /// <summary>
    /// Returns the enemy to its original patrol room after the player
    /// escapes detection range.
    ///
    /// Unlike normal patrol movement, this path is allowed to travel
    /// through corridors and other dungeon floor cells.
    /// </summary>
    private void UpdateReturn()
    {
        // Once any cell inside the original room is reached,
        // normal patrol behaviour can resume.
        if (patrolRoom.Contains(gridPosition))
        {
            currentState =
                EnemyState.Patrol;

            ChooseNewPatrolTarget();

            UnityEngine.Debug.Log(
                $"{name} returned to its patrol room."
            );

            return;
        }


        Vector2Int returnTarget =
            patrolRoom.Centre;

        Vector2Int nextCell;


        // false is important here.
        //
        // The enemy must be allowed to use corridors while returning
        // instead of being restricted to the patrol room.
        if (TryGetNextPathCell(
                gridPosition,
                returnTarget,
                false,
                out nextCell))
        {
            MoveTo(nextCell);
        }
    }


    /// <summary>
    /// Selects a random walkable cell from the enemy's original room.
    /// </summary>
    private void ChooseNewPatrolTarget()
    {
        if (patrolRoom == null ||
            dungeonGenerator == null ||
            dungeonGenerator.Grid == null)
        {
            return;
        }

        const int maximumAttempts = 30;

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
                new Vector2Int(x, y);

            if (dungeonGenerator.Grid.IsWalkable(candidate) &&
                candidate != playerController.GridPosition)
            {
                patrolTarget = candidate;
                return;
            }
        }

        patrolTarget =
            gridPosition;
    }


    /// <summary>
    /// Calculates one step of a shortest path using breadth-first
    /// search.
    ///
    /// Patrol paths can optionally be restricted to the enemy's
    /// original room. Chase paths can use the entire dungeon.
    /// </summary>
    private bool TryGetNextPathCell(
        Vector2Int start,
        Vector2Int destination,
        bool restrictToPatrolRoom,
        out Vector2Int nextCell)
    {
        nextCell = start;

        if (start == destination)
            return false;


        Queue<Vector2Int> frontier =
            new Queue<Vector2Int>();

        HashSet<Vector2Int> visited =
            new HashSet<Vector2Int>();

        Dictionary<Vector2Int, Vector2Int> cameFrom =
            new Dictionary<Vector2Int, Vector2Int>();


        frontier.Enqueue(start);
        visited.Add(start);


        Vector2Int[] directions =
        {
            Vector2Int.up,
            Vector2Int.down,
            Vector2Int.left,
            Vector2Int.right
        };


        bool destinationFound = false;


        while (frontier.Count > 0)
        {
            Vector2Int current =
                frontier.Dequeue();

            if (current == destination)
            {
                destinationFound = true;
                break;
            }


            foreach (Vector2Int direction in directions)
            {
                Vector2Int neighbour =
                    current + direction;


                if (visited.Contains(neighbour))
                    continue;


                if (!dungeonGenerator.Grid.IsWalkable(neighbour))
                    continue;


                if (restrictToPatrolRoom &&
                    !patrolRoom.Contains(neighbour))
                {
                    continue;
                }


                visited.Add(neighbour);

                cameFrom[neighbour] =
                    current;

                frontier.Enqueue(neighbour);
            }
        }


        if (!destinationFound)
            return false;


        // Work backwards from the destination until the cell directly
        // after the enemy's current position is found.
        Vector2Int pathCell =
            destination;


        while (cameFrom.ContainsKey(pathCell) &&
               cameFrom[pathCell] != start)
        {
            pathCell =
                cameFrom[pathCell];
        }


        if (!cameFrom.ContainsKey(pathCell))
            return false;


        nextCell =
            pathCell;

        return true;
    }


    private void MoveTo(Vector2Int targetCell)
    {
        if (!dungeonGenerator.Grid.IsWalkable(targetCell))
            return;

        // Never move directly onto the player's cell.
        if (targetCell == playerController.GridPosition)
            return;

        gridPosition =
            targetCell;

        UpdateWorldPosition();
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
            Mathf.Abs(a.x - b.x) +
            Mathf.Abs(a.y - b.y);
    }

    /// <summary>
    /// Checks whether the enemy has an unobstructed grid line to the
    /// player.
    ///
    /// Walkable dungeon cells allow vision. Any non-walkable cell between
    /// the enemy and player blocks vision.
    /// </summary>
    private bool HasLineOfSightToPlayer()
    {
        Vector2Int start =
            gridPosition;

        Vector2Int end =
            playerController.GridPosition;


        int x0 = start.x;
        int y0 = start.y;

        int x1 = end.x;
        int y1 = end.y;


        int deltaX =
            Mathf.Abs(x1 - x0);

        int deltaY =
            Mathf.Abs(y1 - y0);

        int stepX =
            x0 < x1 ? 1 : -1;

        int stepY =
            y0 < y1 ? 1 : -1;

        int error =
            deltaX - deltaY;


        while (true)
        {
            Vector2Int current =
                new Vector2Int(x0, y0);


            // Do not test the enemy's own cell or the player's cell.
            if (current != start &&
                current != end &&
                !dungeonGenerator.Grid.IsWalkable(current))
            {
                return false;
            }


            if (x0 == x1 &&
                y0 == y1)
            {
                break;
            }


            int doubleError =
                error * 2;


            if (doubleError > -deltaY)
            {
                error -= deltaY;
                x0 += stepX;
            }


            if (doubleError < deltaX)
            {
                error += deltaX;
                y0 += stepY;
            }
        }


        return true;
    }

    /// <summary>
    /// Chooses what an enemy should do after abandoning an investigation.
    ///
    /// An enemy already inside its original room can immediately resume
    /// patrol. Otherwise it must navigate home first.
    /// </summary>
    private void ChooseReturnOrPatrolState()
    {
        if (patrolRoom.Contains(gridPosition))
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
}