// --- START OF FILE PathState.cs ---

namespace Game.NPC
{
    /// <summary>
    /// Defines states specifically related to following predefined waypoint paths.
    /// </summary>
    public enum PathState
    {
        None,               // Default or unassigned state for this enum type
        FollowPath,         // Generic state for following any path
        // Add specific path states here if needed, e.g.:
        GymToPharmacy,
        // PharmacyToGymPath,
        // PatrolRouteAPath,
    }
}
// --- END OF FILE PathState.cs ---