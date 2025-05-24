// Inside CustomerMovingToRegisterStateSO.cs

using UnityEngine;
using System.Collections;
using System;
using CustomerManagement;
using Game.NPC;
using Game.NPC.States; // Ensure this is present

namespace Game.NPC.States
{
    [CreateAssetMenu(fileName = "CustomerMovingToRegisterState", menuName = "NPC/Customer States/Moving To Register", order = 4)]
    public class CustomerMovingToRegisterStateSO : NpcStateSO
    {
        public override System.Enum HandledState => CustomerState.MovingToRegister;

        public override void OnEnter(NpcStateContext context)
        {
            base.OnEnter(context);

            Transform registerTarget = context.GetRegisterPoint();

            if (registerTarget != null)
            {
                context.Runner.CurrentTargetLocation = new BrowseLocation { browsePoint = registerTarget, inventory = null };
                Debug.Log($"{context.NpcObject.name}: Set destination to register point: {registerTarget.position}.", context.NpcObject);
                // Initiate movement using the context helper (resets _hasReachedCurrentDestination flag)
                bool moveStarted = context.MoveToDestination(registerTarget.position);

                if (!moveStarted) // Add check for move failure from SetDestination
                {
                    Debug.LogError($"{context.NpcObject.name}: Failed to start movement to register point! Is the point on the NavMesh?", context.NpcObject);
                    context.TransitionToState(CustomerState.Exiting); // Fallback on movement failure
                    return; // Exit OnEnter early if movement failed
                }

                // --- REMOVED: SignalCustomerAtRegister() ---
                // This call is now done in the previous state (Browse) when the decision to go to the register is made because it's free.
                // Or it's done by the CustomerManager when sending someone from the queue.
            }
            else
            {
                Debug.LogWarning($"{context.NpcObject.name}: No register point assigned! Exiting.", context.NpcObject);
                context.TransitionToState(CustomerState.Exiting);
                return; // Exit OnEnter early
            }
        }

        public override void OnUpdate(NpcStateContext context)
        {
            base.OnUpdate(context);
        }

        public override void OnReachedDestination(NpcStateContext context)
        {
            Debug.Log($"{context.NpcObject.name}: Reached register destination (detected by Runner). Transitioning to WaitingAtRegister.", context.NpcObject);

            context.MovementHandler?.StopMoving(); // Ensure stopped at destination

            // --- REMOVED: Signal arrival at the register here ---
            // This call is now done when the NPC decides to go to the register / is sent from queue.

            // Rotate towards the register point's rotation (optional, but common)
            if (context.CurrentTargetLocation.HasValue && context.CurrentTargetLocation.Value.browsePoint != null)
            {
                context.RotateTowardsTarget(context.CurrentTargetLocation.Value.browsePoint.rotation);
            }
            else
            {
                Debug.LogWarning($"{context.NpcObject.name}: No valid target location stored for Register rotation!", context.NpcObject);
            }

            // Transition to the WaitingAtRegister state
            context.TransitionToState(CustomerState.WaitingAtRegister);
        }

        public override void OnExit(NpcStateContext context)
        {
            base.OnExit(context);
        }
    }
}