namespace Game.NPC
{
    /// <summary>
    /// Defines states common to most or all NPC types (e.g., combat, idle, initialization).
    /// </summary>
    public enum GeneralState
    {
        None,               // Default or unassigned state for this enum type
        Initializing,       // Generic initialization state (entry point from Runner.Initialize)
        ReturningToPool,    // Generic state for returning to pool
        Combat,             // Engaged in combat
        Social,             // Engaged in social interaction (e.g., talking)
        Emoting,            // Performing an emote animation
        Idle,               // Standing still, not actively pursuing a task
        Death,               // NPC is dead
        // Add other general states here as needed
    }
}