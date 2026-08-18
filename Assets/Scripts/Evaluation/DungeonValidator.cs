using System.Collections.Generic;

/// <summary>
/// Contains validation methods for analysing generated dungeons.
///
/// Validation is kept separate from generation so that the generator
/// creates the dungeon while this class checks properties of the result.
/// </summary>
public static class DungeonValidator
{
    /// <summary>
    /// Checks whether every generated room can be reached through
    /// the logical connections in the dungeon graph.
    ///
    /// A breadth-first search (BFS) starts from the first room and
    /// follows graph connections until no unvisited connected rooms remain.
    /// If every room was visited, the graph is fully connected.
    /// </summary>
    public static bool IsFullyConnected(
        List<Room> rooms,
        DungeonGraph graph)
    {
        // An empty dungeon is not considered a valid connected dungeon.
        if (rooms == null || rooms.Count == 0 || graph == null)
        {
            return false;
        }

        HashSet<Room> visited = new HashSet<Room>();
        Queue<Room> queue = new Queue<Room>();

        // Start the traversal from the first generated room.
        Room startRoom = rooms[0];

        visited.Add(startRoom);
        queue.Enqueue(startRoom);

        while (queue.Count > 0)
        {
            Room currentRoom = queue.Dequeue();

            // Find every graph edge involving the current room.
            foreach (DungeonGraph.RoomConnection connection
                     in graph.Connections)
            {
                Room neighbour = null;

                if (connection.RoomA == currentRoom)
                {
                    neighbour = connection.RoomB;
                }
                else if (connection.RoomB == currentRoom)
                {
                    neighbour = connection.RoomA;
                }

                // Ignore connections unrelated to the current room
                // and rooms that have already been visited.
                if (neighbour != null && !visited.Contains(neighbour))
                {
                    visited.Add(neighbour);
                    queue.Enqueue(neighbour);
                }
            }
        }

        // Every room must have been reached from the starting room.
        return visited.Count == rooms.Count;
    }
}