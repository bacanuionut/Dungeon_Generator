using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Shared grid visibility calculations used by enemies, player vision
/// and graphical torch cones.
///
/// Keeping these calculations in one place prevents the visual
/// representation of visibility from disagreeing with gameplay logic.
/// </summary>
public static class DungeonVisibilityUtility
{
    /// <summary>
    /// Tests whether a target is inside a true forward-facing cone.
    ///
    /// VisionRange represents forward depth rather than radial distance.
    /// This produces a more natural torch-shaped field of view with a
    /// flat far edge.
    /// </summary>
    public static bool IsCellVisibleInCone(
        DungeonGrid grid,
        Vector2Int originCell,
        Vector2Int targetCell,
        Vector2Int facingDirection,
        float visionRange,
        float visionAngle)
    {
        if (grid == null ||
            facingDirection == Vector2Int.zero)
        {
            return false;
        }


        if (originCell == targetCell)
            return true;


        Vector2 forward =
            new Vector2(
                facingDirection.x,
                facingDirection.y
            ).normalized;


        Vector2 right =
            new Vector2(
                -forward.y,
                forward.x
            );


        Vector2 toTarget =
            new Vector2(
                targetCell.x - originCell.x,
                targetCell.y - originCell.y
            );


        // How far in front of the enemy is the target?
        float forwardDistance =
            Vector2.Dot(
                toTarget,
                forward
            );


        // Anything behind the enemy or beyond the forward range
        // cannot be seen.
        if (forwardDistance < 0f ||
            forwardDistance > visionRange)
        {
            return false;
        }


        // Measure how far sideways the target lies relative to the
        // enemy's facing direction.
        float lateralDistance =
            Mathf.Abs(
                Vector2.Dot(
                    toTarget,
                    right
                )
            );


        float halfAngleRadians =
            visionAngle *
            0.5f *
            Mathf.Deg2Rad;


        float maximumLateralDistance =
            Mathf.Tan(
                halfAngleRadians
            ) *
            forwardDistance;


        if (lateralDistance >
            maximumLateralDistance)
        {
            return false;
        }


        return HasLineOfSight(
            grid,
            originCell,
            targetCell
        );
    }


    /// <summary>
    /// Returns every walkable cell currently visible inside a
    /// directional cone.
    /// </summary>
    public static List<Vector2Int> GetVisibleWalkableCellsInCone(
        DungeonGrid grid,
        Vector2Int originCell,
        Vector2Int facingDirection,
        float visionRange,
        float visionAngle)
    {
        List<Vector2Int> visible =
            new List<Vector2Int>();


        if (grid == null)
            return visible;


        int radius =
            Mathf.CeilToInt(
                visionRange
            );


        for (int x = originCell.x - radius;
             x <= originCell.x + radius;
             x++)
        {
            for (int y = originCell.y - radius;
                 y <= originCell.y + radius;
                 y++)
            {
                Vector2Int cell =
                    new Vector2Int(
                        x,
                        y
                    );


                if (!grid.IsWalkable(cell))
                    continue;


                if (IsCellVisibleInCone(
                        grid,
                        originCell,
                        cell,
                        facingDirection,
                        visionRange,
                        visionAngle))
                {
                    visible.Add(
                        cell
                    );
                }
            }
        }


        return visible;
    }


    /// <summary>
    /// Performs the same conservative grid visibility test used by
    /// the enemy corner-occlusion fix.
    /// </summary>
    public static bool HasLineOfSight(
        DungeonGrid grid,
        Vector2Int startCell,
        Vector2Int targetCell)
    {
        if (grid == null)
            return false;


        if (startCell == targetCell)
            return true;


        Vector2 start =
            new Vector2(
                startCell.x + 0.5f,
                startCell.y + 0.5f
            );


        Vector2 end =
            new Vector2(
                targetCell.x + 0.5f,
                targetCell.y + 0.5f
            );


        Vector2 difference =
            end - start;


        float distance =
            difference.magnitude;


        if (distance <= 0.001f)
            return true;


        Vector2 rayEnd =
            RaycastToObstacle(
                grid,
                start,
                difference.normalized,
                distance
            );


        float travelled =
            Vector2.Distance(
                start,
                rayEnd
            );


        return travelled >=
               distance - 0.02f;
    }


    /// <summary>
    /// Casts a continuous ray through the dungeon grid.
    ///
    /// Gameplay visibility stops at the near edge of the first solid
    /// cell. Visual torch rays can optionally continue through that
    /// first blocking cell so the wall itself appears illuminated,
    /// without allowing visibility beyond it.
    /// </summary>
    public static Vector2 RaycastToObstacle(
        DungeonGrid grid,
        Vector2 origin,
        Vector2 direction,
        float maximumDistance,
        bool includeFirstBlockingCell = false)
    {
        if (grid == null ||
            maximumDistance <= 0f)
        {
            return origin;
        }


        if (direction.sqrMagnitude <= 0.0001f)
            return origin;


        direction.Normalize();


        int currentX =
            Mathf.FloorToInt(
                origin.x
            );

        int currentY =
            Mathf.FloorToInt(
                origin.y
            );


        int stepX =
            direction.x > 0f
                ? 1
                : direction.x < 0f
                    ? -1
                    : 0;


        int stepY =
            direction.y > 0f
                ? 1
                : direction.y < 0f
                    ? -1
                    : 0;


        float tDeltaX =
            stepX != 0
                ? Mathf.Abs(
                    1f / direction.x
                )
                : float.PositiveInfinity;


        float tDeltaY =
            stepY != 0
                ? Mathf.Abs(
                    1f / direction.y
                )
                : float.PositiveInfinity;


        float nextBoundaryX =
            stepX > 0
                ? currentX + 1f
                : currentX;


        float nextBoundaryY =
            stepY > 0
                ? currentY + 1f
                : currentY;


        float tMaxX =
            stepX != 0
                ? Mathf.Abs(
                    (nextBoundaryX - origin.x) /
                    direction.x
                )
                : float.PositiveInfinity;


        float tMaxY =
            stepY != 0
                ? Mathf.Abs(
                    (nextBoundaryY - origin.y) /
                    direction.y
                )
                : float.PositiveInfinity;


        const float cornerTolerance =
            0.0001f;

        const float edgeOffset =
            0.01f;


        while (true)
        {
            float nextDistance =
                Mathf.Min(
                    tMaxX,
                    tMaxY
                );


            if (nextDistance >
                maximumDistance)
            {
                return origin +
                       direction *
                       maximumDistance;
            }


            // The ray is travelling exactly through a grid corner.
            if (Mathf.Abs(
                    tMaxX - tMaxY) <
                cornerTolerance)
            {
                Vector2Int horizontalCell =
                    new Vector2Int(
                        currentX + stepX,
                        currentY
                    );


                Vector2Int verticalCell =
                    new Vector2Int(
                        currentX,
                        currentY + stepY
                    );


                bool horizontalBlocked =
                    !grid.IsWalkable(
                        horizontalCell
                    );


                bool verticalBlocked =
                    !grid.IsWalkable(
                        verticalCell
                    );


                if (horizontalBlocked ||
                    verticalBlocked)
                {
                    if (includeFirstBlockingCell)
                    {
                        float exitDistance =
                            float.PositiveInfinity;


                        if (horizontalBlocked)
                        {
                            exitDistance =
                                Mathf.Min(
                                    exitDistance,
                                    CalculateCellExitDistance(
                                        origin,
                                        direction,
                                        horizontalCell
                                    )
                                );
                        }


                        if (verticalBlocked)
                        {
                            exitDistance =
                                Mathf.Min(
                                    exitDistance,
                                    CalculateCellExitDistance(
                                        origin,
                                        direction,
                                        verticalCell
                                    )
                                );
                        }


                        return origin +
                               direction *
                               Mathf.Min(
                                   maximumDistance,
                                   Mathf.Max(
                                       0f,
                                       exitDistance -
                                       edgeOffset
                                   )
                               );
                    }


                    // Gameplay LOS remains conservative and stops before
                    // entering the blocking wall cell.
                    return origin +
                           direction *
                           Mathf.Max(
                               0f,
                               nextDistance -
                               edgeOffset
                           );
                }


                currentX +=
                    stepX;

                currentY +=
                    stepY;


                Vector2Int diagonalCell =
                    new Vector2Int(
                        currentX,
                        currentY
                    );


                if (!grid.IsWalkable(
                        diagonalCell))
                {
                    if (includeFirstBlockingCell)
                    {
                        float exitDistance =
                            CalculateCellExitDistance(
                                origin,
                                direction,
                                diagonalCell
                            );


                        return origin +
                               direction *
                               Mathf.Min(
                                   maximumDistance,
                                   Mathf.Max(
                                       0f,
                                       exitDistance -
                                       edgeOffset
                                   )
                               );
                    }


                    return origin +
                           direction *
                           Mathf.Max(
                               0f,
                               nextDistance -
                               edgeOffset
                           );
                }


                tMaxX +=
                    tDeltaX;

                tMaxY +=
                    tDeltaY;
            }
            else if (tMaxX < tMaxY)
            {
                float entryDistance =
                    tMaxX;


                currentX +=
                    stepX;


                Vector2Int enteredCell =
                    new Vector2Int(
                        currentX,
                        currentY
                    );


                if (!grid.IsWalkable(
                        enteredCell))
                {
                    if (includeFirstBlockingCell)
                    {
                        float exitDistance =
                            CalculateCellExitDistance(
                                origin,
                                direction,
                                enteredCell
                            );


                        return origin +
                               direction *
                               Mathf.Min(
                                   maximumDistance,
                                   Mathf.Max(
                                       0f,
                                       exitDistance -
                                       edgeOffset
                                   )
                               );
                    }


                    return origin +
                           direction *
                           Mathf.Max(
                               0f,
                               entryDistance -
                               edgeOffset
                           );
                }


                tMaxX +=
                    tDeltaX;
            }
            else
            {
                float entryDistance =
                    tMaxY;


                currentY +=
                    stepY;


                Vector2Int enteredCell =
                    new Vector2Int(
                        currentX,
                        currentY
                    );


                if (!grid.IsWalkable(
                        enteredCell))
                {
                    if (includeFirstBlockingCell)
                    {
                        float exitDistance =
                            CalculateCellExitDistance(
                                origin,
                                direction,
                                enteredCell
                            );


                        return origin +
                               direction *
                               Mathf.Min(
                                   maximumDistance,
                                   Mathf.Max(
                                       0f,
                                       exitDistance -
                                       edgeOffset
                                   )
                               );
                    }


                    return origin +
                           direction *
                           Mathf.Max(
                               0f,
                               entryDistance -
                               edgeOffset
                           );
                }


                tMaxY +=
                    tDeltaY;
            }
        }
    }

    /// <summary>
    /// Calculates where a ray exits a particular grid cell.
    ///
    /// This is used only by the visual torch so the first wall tile hit
    /// by the light can be illuminated completely.
    /// </summary>
    private static float CalculateCellExitDistance(
        Vector2 origin,
        Vector2 direction,
        Vector2Int cell)
    {
        float exitX =
            float.PositiveInfinity;

        float exitY =
            float.PositiveInfinity;


        if (direction.x > 0.0001f)
        {
            exitX =
                (cell.x + 1f - origin.x) /
                direction.x;
        }
        else if (direction.x < -0.0001f)
        {
            exitX =
                (cell.x - origin.x) /
                direction.x;
        }


        if (direction.y > 0.0001f)
        {
            exitY =
                (cell.y + 1f - origin.y) /
                direction.y;
        }
        else if (direction.y < -0.0001f)
        {
            exitY =
                (cell.y - origin.y) /
                direction.y;
        }


        return Mathf.Min(
            exitX,
            exitY
        );
    }
}