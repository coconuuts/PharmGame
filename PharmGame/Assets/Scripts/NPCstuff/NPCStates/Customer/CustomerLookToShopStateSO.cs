// --- START OF FILE CustomerLookToShopStateSO.cs ---

// --- CustomerLookToShopStateSO.cs (Renamed from CustomerInitializingStateSO.cs) ---
using UnityEngine;
using System.Collections;
using System;
using CustomerManagement;
using Game.NPC;
using Game.Events;
using Game.NPC.States; // Ensure this is present

namespace Game.NPC.States
{
    /// <summary>
    /// State for a Customer NPC immediately after universal initialization.
    /// Handles the customer-specific decision to check queues or enter the store to browse.
    /// This is the effective 'starting state' for a Customer's behavior flow.
    /// Corresponds to CustomerState.LookingToShop (will be added).
    /// </summary>
    [CreateAssetMenu(fileName = "CustomerLookToShopState", menuName = "NPC/Customer States/Look To Shop", order = 1)] // <-- Updated attribute
    public class CustomerLookToShopStateSO : NpcStateSO // <-- Updated class name
    {
        // Will map to a new enum value
        public override System.Enum HandledState => CustomerState.LookingToShop; // <-- Updated HandledState (requires new enum value)

        public override void OnEnter(NpcStateContext context)
        {
            base.OnEnter(context); // Call base OnEnter (logs entry, enables Agent)

            Debug.Log($"{context.NpcObject.name}: LookToShop state. Transitioning directly to Entering.", context.NpcObject);
            context.TransitionToState(CustomerState.Entering);
        }

        // OnUpdate remains empty or base call
        // OnReachedDestination is not applicable

        public override void OnExit(NpcStateContext context)
        {
            base.OnExit(context); // Call base OnExit (logs exit, stops movement/rotation)
            // Logic from CustomerInitializingLogic.OnExit (currently empty)
        }
    }
}
// --- END OF FILE CustomerLookToShopStateSO.cs ---