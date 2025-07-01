// --- START OF FILE CashierState.cs ---

namespace Game.NPC
{
    /// <summary>
    /// Defines the specific active states for Cashier NPCs.
    /// </summary>
    public enum CashierState
    {
        // --- Core Cashier States ---
        CashierMovingToCashSpot,    // Moving from arrival point (e.g., end of path from home) to the cash register spot.
        CashierWaitingForCustomer,  // Waiting at the cash spot for a customer to arrive.
        CashierProcessingCheckout,  // Actively processing a customer's transaction.
        CashierGoingHome            // Moving from the cash spot back towards their home path/exit.

        // Add other potential cashier-specific states here if needed later (e.g., taking a break)
    }
}

// --- END OF FILE CashierState.cs ---