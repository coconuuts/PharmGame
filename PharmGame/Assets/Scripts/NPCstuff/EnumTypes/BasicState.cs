// --- START OF FILE BasicState.cs ---

namespace Game.NPC.BasicStates
{
    /// <summary>
    /// Defines the simplified states used for simulating True Identity (TI) NPCs
    /// when their GameObject is inactive (pooled/out of proximity range).
    /// These states run on data (TiNpcData) via the BasicNpcStateManager.
    /// </summary>
    public enum BasicState
    {
        None,                   // Default state, no specific simulation logic
        BasicPatrol,            // Simplified patrol/wandering logic
        BasicLookToShop,        // Simulates the decision process to try and be a customer
        BasicEnteringStore,     // Simulates movement towards the first store point
        BasicBrowse,            // Simulates browsing at a store location
        BasicWaitForCashier,    // Simulates waiting in a queue or at the register
        BasicExitingStore,       // Simulates movement towards an exit point
        BasicIdleAtHome,
        BasicLookToPrescription,
        BasicWaitForPrescription,
        BasicWaitingAtPrescriptionSpot, // NEW: Simulating waiting at the *prescription claim spot* (corresponds to active WaitingForPrescription)
        BasicWaitingForDeliverySim,
        
        // Add other basic states as needed in the future
    }
}
// --- END OF FILE BasicState.cs ---s