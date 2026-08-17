using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Represents one rectangular section of the dungeon in the BSP tree.
///
/// A BSPNode can either:
/// 1. Be a leaf node containing one final dungeon partition, or
/// 2. Have two child nodes created by splitting the partition.
///
/// Keeping the partition logic in its own class means DungeonGenerator
/// does not need to know how individual BSP nodes perform their splits.
/// </summary>
public class BSPNode
{
    // The rectangular area represented by this node.
    public RectInt Bounds { get; private set; }

    // Child nodes created when this node is split.
    // A node with no children is considered a leaf node.
    public BSPNode LeftChild { get; private set; }
    public BSPNode RightChild { get; private set; }

    // A room can be assigned to a leaf partition after BSP splitting is complete.
    public Room Room { get; private set; }

    // Useful for debugging and for limiting recursion.
    public int Depth { get; private set; }

    /// <summary>
    /// Creates a BSP node representing a rectangular area.
    /// </summary>
    public BSPNode(RectInt bounds, int depth)
    {
        Bounds = bounds;
        Depth = depth;
    }

    /// <summary>
    /// Returns true when this node has not been divided further.
    /// Rooms will later be generated inside these leaf nodes.
    /// </summary>
    public bool IsLeaf()
    {
        return LeftChild == null && RightChild == null;
    }

    /// <summary>
    /// Attempts to divide this partition into two child partitions.
    ///
    /// The split direction is influenced by the shape of the partition.
    /// Very wide areas prefer a vertical split and very tall areas
    /// prefer a horizontal split. More square areas choose randomly.
    ///
    /// This avoids creating too many long, thin partitions while still
    /// allowing randomness between dungeon generations.
    /// </summary>
    public bool Split(System.Random random, int minPartitionSize)
    {
        // A node should only be split once.
        if (!IsLeaf())
            return false;

        bool splitHorizontally;

        // Bias the split direction according to the aspect ratio.
        // This produces more useful partitions than choosing the
        // direction completely randomly every time.
        if (Bounds.width > Bounds.height * 1.25f)
        {
            splitHorizontally = false;
        }
        else if (Bounds.height > Bounds.width * 1.25f)
        {
            splitHorizontally = true;
        }
        else
        {
            splitHorizontally = random.Next(0, 2) == 0;
        }

        // Determine how much space exists in the selected direction.
        int availableSize = splitHorizontally
            ? Bounds.height
            : Bounds.width;

        // Both resulting partitions must be at least minPartitionSize.
        // If there is not enough room, try splitting in the other direction.
        if (availableSize < minPartitionSize * 2)
        {
            splitHorizontally = !splitHorizontally;

            availableSize = splitHorizontally
                ? Bounds.height
                : Bounds.width;

            // Neither direction can produce two valid partitions.
            if (availableSize < minPartitionSize * 2)
                return false;
        }

        // Choose the split point while guaranteeing that neither child
        // becomes smaller than the configured minimum partition size.
        int splitPosition = random.Next(
            minPartitionSize,
            availableSize - minPartitionSize + 1
        );

        if (splitHorizontally)
        {
            // Bottom partition.
            RectInt firstBounds = new RectInt(
                Bounds.x,
                Bounds.y,
                Bounds.width,
                splitPosition
            );

            // Top partition.
            RectInt secondBounds = new RectInt(
                Bounds.x,
                Bounds.y + splitPosition,
                Bounds.width,
                Bounds.height - splitPosition
            );

            LeftChild = new BSPNode(firstBounds, Depth + 1);
            RightChild = new BSPNode(secondBounds, Depth + 1);
        }
        else
        {
            // Left partition.
            RectInt firstBounds = new RectInt(
                Bounds.x,
                Bounds.y,
                splitPosition,
                Bounds.height
            );

            // Right partition.
            RectInt secondBounds = new RectInt(
                Bounds.x + splitPosition,
                Bounds.y,
                Bounds.width - splitPosition,
                Bounds.height
            );

            LeftChild = new BSPNode(firstBounds, Depth + 1);
            RightChild = new BSPNode(secondBounds, Depth + 1);
        }

        return true;
    }

    /// <summary>
    /// Generates a rectangular room inside this BSP leaf.
    ///
    /// Padding prevents the room from touching the partition boundary.
    /// Room dimensions and position are random, but use the seeded random
    /// number generator supplied by DungeonGenerator.
    /// </summary>
    public Room GenerateRoom(
        System.Random random,
        int minimumRoomSize,
        int padding)
    {
        // Rooms should only be created in final BSP partitions.
        if (!IsLeaf())
            return null;

        // Calculate the space available after leaving padding on both sides.
        int availableWidth = Bounds.width - (padding * 2);
        int availableHeight = Bounds.height - (padding * 2);

        // The partition cannot contain a valid room with the current settings.
        if (availableWidth < minimumRoomSize ||
            availableHeight < minimumRoomSize)
        {
            return null;
        }

        // Randomise the room dimensions.
        // The upper bound of Random.Next is exclusive, hence the +1.
        int roomWidth = random.Next(
            minimumRoomSize,
            availableWidth + 1
        );

        int roomHeight = random.Next(
            minimumRoomSize,
            availableHeight + 1
        );

        // Work out how far the room can move while remaining inside
        // the padded area of the partition.
        int maximumXOffset = availableWidth - roomWidth;
        int maximumYOffset = availableHeight - roomHeight;

        int xOffset = random.Next(0, maximumXOffset + 1);
        int yOffset = random.Next(0, maximumYOffset + 1);

        int roomX = Bounds.x + padding + xOffset;
        int roomY = Bounds.y + padding + yOffset;

        RectInt roomBounds = new RectInt(
            roomX,
            roomY,
            roomWidth,
            roomHeight
        );

        Room = new Room(roomBounds, this);

        return Room;
    }

    /// <summary>
    /// Recursively finds every leaf below this node.
    ///
    /// This means other parts of the generator do not need to understand
    /// the internal structure of the BSP tree just to obtain the final
    /// dungeon partitions.
    /// </summary>
    public void GetLeafNodes(List<BSPNode> leaves)
    {
        if (IsLeaf())
        {
            leaves.Add(this);
            return;
        }

        LeftChild?.GetLeafNodes(leaves);
        RightChild?.GetLeafNodes(leaves);
    }
}