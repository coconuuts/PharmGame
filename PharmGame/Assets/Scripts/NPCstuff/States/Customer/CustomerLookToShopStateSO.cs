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

            // The logic is still the same, it just runs after a separate, generic initialization step.
            context.StartCoroutine(LookToShopRoutine(context)); // <-- Updated coroutine name
        }

        // OnUpdate remains empty or base call
        // OnReachedDestination is not applicable

        public override void OnExit(NpcStateContext context)
        {
            base.OnExit(context); // Call base OnExit (logs exit, stops movement/rotation)
            // Logic from CustomerInitializingLogic.OnExit (currently empty)
        }

        // Coroutine method (Logic from CustomerInitializingLogic.StateCoroutine)
        private IEnumerator LookToShopRoutine(NpcStateContext context) // <-- Updated coroutine name
        {
            Debug.Log($"{context.NpcObject.name}: LookToShopRoutine started in {name}.", context.NpcObject); // <-- Updated log

            // Wait one frame (logic from old StateCoroutine)
            yield return null;
            Debug.Log($"{context.NpcObject.name}: LookToShopRoutine finished processing wait.", context.NpcObject); // <-- Updated log

            // --- Decision logic based on Manager state (Migration) ---
            // This logic remains as it determines the *first customer state* transition.
            if (context.Manager != null && context.Manager.IsMainQueueFull())
            {
                Debug.Log($"{context.NpcObject.name}: Main queue is full. Attempting to join secondary queue.", context.NpcObject);
                Transform assignedSpot;
                int spotIndex;
                CustomerAI customerAIComponent = context.NpcObject.GetComponent<CustomerAI>();
                if (customerAIComponent != null && context.Manager.TryJoinSecondaryQueue(context.Runner, out assignedSpot, out spotIndex)) // Pass context.Runner
                {
                    Debug.Log($"{context.NpcObject.name}: Successfully joined secondary queue at spot {spotIndex}.", context.NpcObject);
                    context.Runner.CurrentTargetLocation = new BrowseLocation { browsePoint = assignedSpot, inventory = null };
                    context.Runner.AssignedQueueSpotIndex = spotIndex;
                    context.TransitionToState(CustomerState.SecondaryQueue); // Transition via context helper
                }
                else
                {
                    Debug.LogWarning($"{context.NpcObject.name}: Main queue and secondary queue are full! Exiting to pool (fallback).", context.NpcObject);
                    context.TransitionToState(GeneralState.ReturningToPool); // Transition via context
                }
            }
            else
            {
                // Main queue is not full, proceed normally to enter the store
                Debug.Log($"{context.NpcObject.name}: Main queue is not full. Transitioning to Entering.", context.NpcObject);
                context.TransitionToState(CustomerState.Entering); // Transition via context
            }
            // ---------------------------------------------------------------

            Debug.Log($"{context.NpcObject.name}: LookToShopRoutine finished.", context.NpcObject); // <-- Updated log
        }
    }
}