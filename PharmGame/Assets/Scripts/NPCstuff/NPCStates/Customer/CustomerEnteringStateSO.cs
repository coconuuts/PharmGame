// --- START OF FILE CustomerEnteringStateSO.cs ---

// --- Updated CustomerEnteringStateSO.cs ---
using UnityEngine;
using System.Collections;
using System;
using CustomerManagement;
using Game.NPC;
using Game.Events; // Needed for NpcEnteredStoreEvent
using Game.NPC.States;

namespace Game.NPC.States
{
    [CreateAssetMenu(fileName = "CustomerEnteringState", menuName = "NPC/Customer States/Entering", order = 2)]
    public class CustomerEnteringStateSO : NpcStateSO
    {
        public override System.Enum HandledState => CustomerState.Entering;

        public override void OnEnter(NpcStateContext context)
        {
            base.OnEnter(context); // Call base OnEnter (logs entry, enables Agent)

            // --- NEW: Publish event to signal entry into the store ---
            // This event is listened to by the CustomerManager to update the activeCustomers count.
            Debug.Log($"{context.NpcObject.name}: Publishing NpcEnteredStoreEvent.", context.NpcObject);
            context.PublishEvent(new NpcEnteredStoreEvent(context.NpcObject));
            // --- END NEW ---


            // --- Logic from CustomerEnteringLogic.OnEnter (Migration) ---
            BrowseLocation? firstBrowseLocation = context.GetRandomBrowseLocation(); // Use context helper

            if (firstBrowseLocation.HasValue && firstBrowseLocation.Value.browsePoint != null)
            {
                context.Runner.CurrentTargetLocation = firstBrowseLocation; // Update context/runner

                // Initiate movement using context helper (resets _hasReachedCurrentDestination flag)
                bool moveStarted = context.MoveToDestination(firstBrowseLocation.Value.browsePoint.position);

                 if (!moveStarted) // Add check for move failure from SetDestination
                 {
                      Debug.LogError($"{context.NpcObject.name}: Failed to start movement to first browse point! Is the point on the NavMesh?", context.NpcObject);
                      context.TransitionToState(CustomerState.Exiting); // Fallback on movement failure
                      return; // Exit OnEnter early if movement failed
                 }


                Debug.Log($"{context.NpcObject.name}: Set destination to first browse point: {firstBrowseLocation.Value.browsePoint.position}.", context.NpcObject);
            }
            else
            {
                Debug.LogWarning($"{context.NpcObject.name}: No Browse locations available for Entering state! Exiting empty-handed.", context.NpcObject);
                context.TransitionToState(CustomerState.Exiting); // Transition via context
                 return; // Exit OnEnter early
            }
            // ---------------------------------------------------------------
        }

        public override void OnUpdate(NpcStateContext context)
        {
            base.OnUpdate(context); // Call base OnUpdate (empty)
        }

        public override void OnReachedDestination(NpcStateContext context) // <-- Implement OnReachedDestination
        {
             // This logic was previously in CustomerEnteringLogic.OnUpdate (the 'if (HasReachedDestination())' block)
             Debug.Log($"{context.NpcObject.name}: Reached initial browse destination (detected by Runner). Transitioning to Browse.", context.NpcObject);
             
             // Ensure movement is stopped before transitioning (Runner does this before calling OnReachedDestination, but defensive)
             context.MovementHandler?.StopMoving();

             context.TransitionToState(CustomerState.Browse); // Transition via context
        }

        public override void OnExit(NpcStateContext context)
        {
            base.OnExit(context); // Call base OnExit (logs exit, stops movement/rotation)
            // Logic from CustomerEnteringLogic.OnExit (currently empty)
        }
    }
}