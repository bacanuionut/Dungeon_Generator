using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using UnityEngine;

/// <summary>
/// Assigns gameplay purposes to generated rooms using information
/// from the logical dungeon graph.
///
/// Roles are based on structural properties such as graph depth,
/// dead-end status, main-route membership and room size.
/// </summary>
public static class RoomRoleAssigner
{
    /// <summary>
    /// Analyses the generated graph and assigns one semantic role
    /// to every room.
    /// </summary>
    public static void AssignRoles(
        IReadOnlyList<Room> rooms,
        DungeonGraph graph,
        Room startRoom,
        Room exitRoom)
    {
        if (rooms == null ||
            rooms.Count == 0 ||
            graph == null ||
            startRoom == null ||
            exitRoom == null)
        {
            UnityEngine.Debug.LogError(
                "Room-role assignment failed because dungeon " +
                "structure was incomplete."
            );

            return;
        }


        Dictionary<Room, int> distances =
            DungeonValidator.CalculateRoomDistances(
                startRoom,
                graph
            );


        List<Room> mainPath =
            FindShortestGraphPath(
                startRoom,
                exitRoom,
                graph
            );


        HashSet<Room> mainPathRooms =
            new HashSet<Room>(
                mainPath
            );


        // Start by recording structural graph information and
        // treating ordinary rooms as Combat rooms.
        foreach (Room room in rooms)
        {
            if (room == null)
                continue;


            int distance =
                distances.ContainsKey(room)
                    ? distances[room]
                    : -1;


            room.SetGraphMetadata(
                distance,
                graph.GetDegree(room),
                mainPathRooms.Contains(room)
            );


            room.SetRole(
                RoomRole.Combat
            );
        }


        // These two roles are fixed by the dungeon generator.
        startRoom.SetRole(
            RoomRole.Start
        );

        exitRoom.SetRole(
            RoomRole.Exit
        );


        AssignEliteRoom(
            mainPath,
            startRoom,
            exitRoom
        );


        AssignRestRoom(
            mainPath,
            startRoom,
            exitRoom
        );


        List<Room> sideRooms =
            FindSideRooms(
                rooms,
                mainPathRooms,
                startRoom,
                exitRoom
            );


        AssignRewardRooms(
            rooms.Count,
            sideRooms
        );


        AssignPuzzleRoom(
            sideRooms
        );


        LogRoles(
            rooms,
            mainPath
        );
    }


    /// <summary>
    /// Finds the shortest sequence of logical room connections
    /// between the start and exit rooms.
    /// </summary>
    private static List<Room> FindShortestGraphPath(
        Room start,
        Room destination,
        DungeonGraph graph)
    {
        Queue<Room> frontier =
            new Queue<Room>();

        HashSet<Room> visited =
            new HashSet<Room>();

        Dictionary<Room, Room> cameFrom =
            new Dictionary<Room, Room>();


        frontier.Enqueue(start);
        visited.Add(start);


        bool found = false;


        while (frontier.Count > 0)
        {
            Room current =
                frontier.Dequeue();


            if (current == destination)
            {
                found = true;
                break;
            }


            foreach (Room neighbour in
                     graph.GetNeighbours(current))
            {
                if (neighbour == null ||
                    visited.Contains(neighbour))
                {
                    continue;
                }


                visited.Add(neighbour);

                cameFrom[neighbour] =
                    current;

                frontier.Enqueue(neighbour);
            }
        }


        List<Room> path =
            new List<Room>();


        if (!found)
            return path;


        Room pathRoom =
            destination;

        path.Add(pathRoom);


        while (pathRoom != start)
        {
            if (!cameFrom.ContainsKey(pathRoom))
            {
                path.Clear();
                return path;
            }


            pathRoom =
                cameFrom[pathRoom];

            path.Add(pathRoom);
        }


        path.Reverse();

        return path;
    }


    /// <summary>
    /// Places the Elite encounter immediately before the exit
    /// whenever the graph contains enough rooms on the main route.
    /// </summary>
    private static void AssignEliteRoom(
        List<Room> mainPath,
        Room startRoom,
        Room exitRoom)
    {
        if (mainPath == null ||
            mainPath.Count < 3)
        {
            return;
        }


        Room eliteCandidate =
            mainPath[
                mainPath.Count - 2
            ];


        if (eliteCandidate != startRoom &&
            eliteCandidate != exitRoom)
        {
            eliteCandidate.SetRole(
                RoomRole.Elite
            );
        }
    }


    /// <summary>
    /// On longer main routes, reserves a room before the Elite area
    /// as a Rest room.
    /// </summary>
    private static void AssignRestRoom(
        List<Room> mainPath,
        Room startRoom,
        Room exitRoom)
    {
        // A five-room logical route gives enough space for:
        //
        // Start -> Combat -> Rest -> Elite -> Exit
        if (mainPath == null ||
            mainPath.Count < 5)
        {
            return;
        }


        Room restCandidate =
            mainPath[
                mainPath.Count - 3
            ];


        if (restCandidate == startRoom ||
            restCandidate == exitRoom ||
            restCandidate.Role ==
                RoomRole.Elite)
        {
            return;
        }


        restCandidate.SetRole(
            RoomRole.Rest
        );
    }


    /// <summary>
    /// Returns rooms that do not belong to the shortest start-to-exit
    /// route. These are particularly useful for optional content.
    /// </summary>
    private static List<Room> FindSideRooms(
        IReadOnlyList<Room> rooms,
        HashSet<Room> mainPath,
        Room startRoom,
        Room exitRoom)
    {
        List<Room> sideRooms =
            new List<Room>();


        foreach (Room room in rooms)
        {
            if (room == null ||
                room == startRoom ||
                room == exitRoom)
            {
                continue;
            }


            if (!mainPath.Contains(room))
            {
                sideRooms.Add(room);
            }
        }


        return sideRooms;
    }


    /// <summary>
    /// Reward rooms favour optional dead ends and deeper graph
    /// positions so exploration away from the main route has value.
    /// </summary>
    private static void AssignRewardRooms(
        int totalRoomCount,
        List<Room> sideRooms)
    {
        if (sideRooms == null ||
            sideRooms.Count == 0)
        {
            return;
        }


        int targetRewardRooms =
            Mathf.Clamp(
                totalRoomCount / 6,
                1,
                2
            );


        for (int rewardIndex = 0;
             rewardIndex < targetRewardRooms;
             rewardIndex++)
        {
            Room bestRoom = null;
            float bestScore =
                float.MinValue;


            foreach (Room room in sideRooms)
            {
                if (room.Role !=
                    RoomRole.Combat)
                {
                    continue;
                }


                float score =
                    room.GraphDistanceFromStart *
                    20f;


                // Dead ends make strong optional reward locations.
                if (room.GraphDegree == 1)
                {
                    score +=
                        100f;
                }


                // Slight preference for larger rooms.
                score +=
                    room.Width *
                    room.Height *
                    0.02f;


                if (score > bestScore)
                {
                    bestScore =
                        score;

                    bestRoom =
                        room;
                }
            }


            if (bestRoom == null)
                break;


            bestRoom.SetRole(
                RoomRole.Reward
            );
        }
    }


    /// <summary>
    /// Selects one remaining side room as a future procedural
    /// puzzle location.
    ///
    /// Larger optional rooms are preferred because they provide more
    /// space for environmental interaction.
    /// </summary>
    private static void AssignPuzzleRoom(
        List<Room> sideRooms)
    {
        if (sideRooms == null ||
            sideRooms.Count == 0)
        {
            return;
        }


        Room bestRoom = null;
        float bestScore =
            float.MinValue;


        foreach (Room room in sideRooms)
        {
            if (room.Role !=
                RoomRole.Combat)
            {
                continue;
            }


            float score =
                room.GraphDistanceFromStart *
                15f;


            // Optional/dead-end locations are preferable.
            if (room.GraphDegree == 1)
            {
                score +=
                    50f;
            }


            // Puzzle rooms benefit from usable floor area.
            score +=
                room.Width *
                room.Height *
                0.05f;


            if (score > bestScore)
            {
                bestScore =
                    score;

                bestRoom =
                    room;
            }
        }


        if (bestRoom != null)
        {
            bestRoom.SetRole(
                RoomRole.Puzzle
            );
        }
    }


    /// <summary>
    /// Writes one compact summary so semantic generation can be
    /// inspected and tested without needing gameplay content yet.
    /// </summary>
    private static void LogRoles(
        IReadOnlyList<Room> rooms,
        List<Room> mainPath)
    {
        int startCount = 0;
        int exitCount = 0;
        int combatCount = 0;
        int rewardCount = 0;
        int puzzleCount = 0;
        int restCount = 0;
        int eliteCount = 0;


        StringBuilder details =
            new StringBuilder();


        foreach (Room room in rooms)
        {
            switch (room.Role)
            {
                case RoomRole.Start:
                    startCount++;
                    break;

                case RoomRole.Exit:
                    exitCount++;
                    break;

                case RoomRole.Combat:
                    combatCount++;
                    break;

                case RoomRole.Reward:
                    rewardCount++;
                    break;

                case RoomRole.Puzzle:
                    puzzleCount++;
                    break;

                case RoomRole.Rest:
                    restCount++;
                    break;

                case RoomRole.Elite:
                    eliteCount++;
                    break;
            }


            details.Append(
                $"{room.Role}: " +
                $"Centre {room.Centre}, " +
                $"Depth {room.GraphDistanceFromStart}, " +
                $"Degree {room.GraphDegree}, " +
                $"Main path: {room.IsOnMainPath}\n"
            );
        }


        UnityEngine.Debug.Log(
            "========== SEMANTIC ROOM ROLES ==========\n" +
            $"Main path rooms: {mainPath.Count}\n" +
            $"Start: {startCount}\n" +
            $"Exit: {exitCount}\n" +
            $"Combat: {combatCount}\n" +
            $"Reward: {rewardCount}\n" +
            $"Puzzle: {puzzleCount}\n" +
            $"Rest: {restCount}\n" +
            $"Elite: {eliteCount}\n\n" +
            details +
            "========================================="
        );
    }
}