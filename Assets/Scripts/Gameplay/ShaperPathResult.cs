using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Result of a successful dynamic Shaper search.
///
/// GuideCells are all currently solid cells which must progressively
/// become walkable before TargetCell is reached.
/// </summary>
public class ShaperPathResult
{
    public Vector2Int OriginCell { get; private set; }

    public Vector2Int WallCell { get; private set; }

    public Vector2Int TargetCell { get; private set; }

    public IReadOnlyList<Vector2Int> GuideCells =>
        guideCells;


    private readonly List<Vector2Int> guideCells;


    public int SolidCellCount =>
        guideCells.Count;


    public ShaperPathResult(
        Vector2Int originCell,
        Vector2Int wallCell,
        Vector2Int targetCell,
        List<Vector2Int> cells)
    {
        OriginCell =
            originCell;

        WallCell =
            wallCell;

        TargetCell =
            targetCell;


        guideCells =
            new List<Vector2Int>(
                cells
            );
    }
}