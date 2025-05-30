// --- START OF FILE BasicPathState.cs ---

namespace Game.NPC.BasicStates
{
    /// <summary>
    /// Defines the simplified states used for simulating True Identity (TI) NPCs
    /// when following predefined waypoint paths off-screen.
    /// </summary>
    public enum BasicPathState
    {
        None,               // Default state, no specific simulation logic
        BasicFollowPath,    // Generic state for simulating following any path
        // Add specific basic path states here if needed, e.g.:
        // BasicGymToPharmacyPath,
        // BasicPharmacyToGymPath,
        // BasicPatrolRouteAPath,
    }
}
// --- END OF FILE BasicPathState.cs ---