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

            // --- Publish event to signal entry into the store ---
            // This event is listened to by the CustomerManager to update the activeCustomers count.
            Debug.Log($"{context.NpcObject.name}: Publishing NpcEnteredStoreEvent.", context.NpcObject);
            context.PublishEvent(new NpcEnteredStoreEvent(context.NpcObject));

            // 1. Check if a location was restored from save data (stored in Runner)
            BrowseLocation? targetLocation = context.Runner.CurrentTargetLocation;

            // 2. If NO restored location exists, pick a new random one
            if (targetLocation == null)
            {
                targetLocation = context.GetRandomBrowseLocation();
            }

            // 3. Process the location (whether restored or new)
            if (targetLocation.HasValue && targetLocation.Value.browsePoint != null)
            {
                context.Runner.CurrentTargetLocation = targetLocation; // Ensure it's set/updated

                // Initiate movement 
                bool moveStarted = context.MoveToDestination(targetLocation.Value.browsePoint.position);

                 if (!moveStarted) 
                 {
                      Debug.LogError($"{context.NpcObject.name}: Failed to start movement to browse point!", context.NpcObject);
                      context.TransitionToState(CustomerState.Exiting); 
                      return; 
                 }

                Debug.Log($"{context.NpcObject.name}: Set destination to browse point (Restored: {context.Runner.CurrentTargetLocation != null}).", context.NpcObject);
            }
            else
            {
                Debug.LogWarning($"{context.NpcObject.name}: No Browse locations available! Exiting.", context.NpcObject);
                context.TransitionToState(CustomerState.Exiting); 
                 return; 
            }
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