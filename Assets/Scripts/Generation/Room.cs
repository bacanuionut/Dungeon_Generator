using UnityEngine;

/// <summary>
/// Represents one rectangular room within the dungeon.
///
/// Room is deliberately kept as a data-focused class. It stores the
/// position and dimensions of a generated room without being responsible
/// for drawing it or controlling the overall generation process.
/// </summary>
public class Room
{
    /// <summary>
    /// Integer rectangle describing the room's position and size.
    /// </summary>
    public RectInt Bounds { get; private set; }

    /// <summary>
    /// The BSP leaf partition that this room was generated inside.
    /// Keeping this reference is useful for validation and debugging.
    /// </summary>
    public BSPNode ParentPartition { get; private set; }

    public int Width => Bounds.width;
    public int Height => Bounds.height;

    /// <summary>
    /// Approximate centre of the room.
    /// This will later be useful when deciding how rooms should
    /// be connected by corridors.
    /// </summary>
    public Vector2Int Centre
    {
        get
        {
            return new Vector2Int(
                Bounds.x + Bounds.width / 2,
                Bounds.y + Bounds.height / 2
            );
        }
    }

    public Room(RectInt bounds, BSPNode parentPartition)
    {
        Bounds = bounds;
        ParentPartition = parentPartition;
    }

    /// <summary>
    /// Checks whether the complete room remains inside its BSP partition.
    /// This is used by the validation/testing code.
    /// </summary>
    public bool IsInsideParentPartition()
    {
        RectInt partition = ParentPartition.Bounds;

        return Bounds.xMin >= partition.xMin &&
               Bounds.yMin >= partition.yMin &&
               Bounds.xMax <= partition.xMax &&
               Bounds.yMax <= partition.yMax;
    }
}