// --- Updated CustomerEnteringStateSO.cs ---
using UnityEngine;
using System.Collections;
using CustomerManagement;
using Game.NPC;
using Game.NPC.States;

namespace Game.NPC.States
{
    [CreateAssetMenu(fileName = "CustomerEnteringState", menuName = "NPC/Customer States/Entering", order = 2)]
    public class CustomerEnteringStateSO : NpcStateSO
    {
        public override CustomerState HandledState => CustomerState.Entering;

        public override void OnEnter(NpcStateContext context)
        {
            base.OnEnter(context); // Call base OnEnter (logs entry, enables Agent)
            Debug.Log($"{context.NpcObject.name}: Entering Entering state. Finding first browse location.", context.NpcObject);

            // --- Logic from CustomerEnteringLogic.OnEnter (Migration) ---
            BrowseLocation? firstBrowseLocation = context.GetRandomBrowseLocation(); // Use context helper

            if (firstBrowseLocation.HasValue && firstBrowseLocation.Value.browsePoint != null)
            {
                context.Runner.CurrentTargetLocation = firstBrowseLocation; // Update context/runner

                context.MoveToDestination(firstBrowseLocation.Value.browsePoint.position); // Use context helper

                Debug.Log($"{context.NpcObject.name}: Set destination to first browse point: {firstBrowseLocation.Value.browsePoint.position}.", context.NpcObject);
            }
            else
            {
                Debug.LogWarning($"{context.NpcObject.name}: No Browse locations available for Entering state! Exiting empty-handed.", context.NpcObject);
                context.TransitionToState(CustomerState.Exiting); // Transition via context
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
             context.TransitionToState(CustomerState.Browse); // Transition via context
        }

        public override void OnExit(NpcStateContext context)
        {
            base.OnExit(context); // Call base OnExit (logs exit, stops movement/rotation)
        }
    }
}