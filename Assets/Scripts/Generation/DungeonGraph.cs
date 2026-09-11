using System.Collections.Generic;

/// <summary>
/// Represents the logical connectivity of the generated dungeon.
///
/// Rooms act as graph nodes and RoomConnection objects act as edges.
/// This representation is kept separate from physical corridor
/// generation so connectivity can be analysed independently.
/// </summary>
public class DungeonGraph
{
    /// <summary>
    /// Represents one logical connection between two rooms.
    /// CorridorGenerator converts this connection into an
    /// actual corridor path.
    /// </summary>
    public class RoomConnection
    {
        public Room RoomA { get; private set; }
        public Room RoomB { get; private set; }

        public RoomConnection(Room roomA, Room roomB)
        {
            RoomA = roomA;
            RoomB = roomB;
        }
    }

    private readonly List<RoomConnection> connections =
        new List<RoomConnection>();

    /// <summary>
    /// Read-only access to the graph edges.
    /// Other classes can inspect the connections but cannot replace
    /// the internal collection.
    /// </summary>
    public IReadOnlyList<RoomConnection> Connections => connections;

    /// <summary>
    /// Removes all existing graph connections.
    /// Called before generating a new dungeon.
    /// </summary>
    public void Clear()
    {
        connections.Clear();
    }

    /// <summary>
    /// Adds a connection between two rooms.
    ///
    /// Invalid self-connections and duplicate edges are ignored.
    /// </summary>
    public void AddConnection(Room roomA, Room roomB)
    {
        if (roomA == null || roomB == null)
        {
            return;
        }

        // A room should never connect to itself.
        if (roomA == roomB)
        {
            return;
        }

        // Prevent the same undirected edge being added twice.
        foreach (RoomConnection existing in connections)
        {
            bool sameDirection =
                existing.RoomA == roomA &&
                existing.RoomB == roomB;

            bool oppositeDirection =
                existing.RoomA == roomB &&
                existing.RoomB == roomA;

            if (sameDirection || oppositeDirection)
            {
                return;
            }
        }

        connections.Add(
            new RoomConnection(roomA, roomB)
        );
    }

    /// <summary>
    /// Returns all rooms directly connected to the supplied room.
    /// </summary>
    public List<Room> GetNeighbours(Room room)
    {
        List<Room> neighbours =
            new List<Room>();


        if (room == null)
            return neighbours;


        foreach (RoomConnection connection in connections)
        {
            if (connection.RoomA == room)
            {
                neighbours.Add(
                    connection.RoomB
                );
            }
            else if (connection.RoomB == room)
            {
                neighbours.Add(
                    connection.RoomA
                );
            }
        }


        return neighbours;
    }


    /// <summary>
    /// Returns the number of graph connections belonging to a room.
    /// </summary>
    public int GetDegree(Room room)
    {
        if (room == null)
            return 0;


        int degree = 0;


        foreach (RoomConnection connection in connections)
        {
            if (connection.RoomA == room ||
                connection.RoomB == room)
            {
                degree++;
            }
        }


        return degree;
    }
}