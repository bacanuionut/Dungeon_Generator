/// <summary>
/// Describes the gameplay purpose assigned to a generated room.
///
/// Room roles are derived from the dungeon graph after the BSP
/// structure and start/exit rooms have been selected.
/// </summary>
public enum RoomRole
{
    Unassigned,

    Start,
    Exit,

    Combat,
    Reward,
    Puzzle,
    Rest,
    Elite,

    // Reserved for later Shaper-generated or hidden areas.
    Secret
}