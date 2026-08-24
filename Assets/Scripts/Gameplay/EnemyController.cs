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


    [Header("Movement")]

    [Tooltip("Delay between enemy grid movements.")]
    [SerializeField]
    private float movementDelay = 0.35f;

    [Tooltip("Grid distance at which the enemy begins chasing the player.")]
    [SerializeField]
    private int detectionRange = 6;


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

            case EnemyState.Return:
                UpdateReturn();
                break;
        }
    }


    /// <summary>
    /// Selects the current AI behaviour from player distance and the
    /// enemy's position relative to its original patrol room.
    /// </summary>
    private void UpdateState()
    {
        int distanceToPlayer =
            ManhattanDistance(
                gridPosition,
                playerController.GridPosition
            );

        EnemyState previousState =
            currentState;


        // Adjacent enemies attack rather than trying to move onto
        // the player's grid cell.
        if (distanceToPlayer <= 1)
        {
            currentState =
                EnemyState.Attack;
        }

        // A detected player who is not yet adjacent is chased.
        else if (distanceToPlayer <= detectionRange)
        {
            currentState =
                EnemyState.Chase;
        }

        else
        {
            switch (currentState)
            {
                case EnemyState.Chase:
                case EnemyState.Attack:

                    // If the player escapes while the enemy is outside
                    // its original room, return home first.
                    if (patrolRoom.Contains(gridPosition))
                    {
                        currentState =
                            EnemyState.Patrol;
                    }
                    else
                    {
                        currentState =
                            EnemyState.Return;
                    }

                    break;


                case EnemyState.Return:

                    if (patrolRoom.Contains(gridPosition))
                    {
                        currentState =
                            EnemyState.Patrol;
                    }

                    break;


                case EnemyState.Patrol:

                    // Continue patrolling while the player remains
                    // outside detection range.
                    break;
            }
        }


        if (previousState != currentState)
        {
            UnityEngine.Debug.Log(
                $"{name} changed AI state: " +
                $"{previousState} -> {currentState}"
            );


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
    /// Damages the player while the enemy occupies an adjacent grid cell.
    ///
    /// The enemy does not enter the player's cell. Instead it remains
    /// adjacent and attacks according to a cooldown.
    /// </summary>
    private void UpdateAttack()
    {
        if (!playerController.IsAlive)
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
}