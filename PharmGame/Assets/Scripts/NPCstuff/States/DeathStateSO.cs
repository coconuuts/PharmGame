// --- FIX IN DeathStateSO.cs ---
using UnityEngine;
using System.Collections;
using Game.NPC;
using Game.NPC.States;

namespace Game.NPC.States
{
    [CreateAssetMenu(fileName = "DeathState", menuName = "NPC/General States/Death", order = 6)]
    public class DeathStateSO : NpcStateSO
    {
        public override CustomerState HandledState => CustomerState.Death;

        public override bool IsInterruptible => false;

         public override void OnEnter(NpcStateContext context)
         {
             Debug.Log($"{context.NpcObject.name}: Entering generic Death state.", context.NpcObject); // DO NOT call base.OnEnter

             // --- Stop all movement and disable agent safely ---
             if (context.MovementHandler != null) // Check if handler exists
             {
                  context.MovementHandler.StopMoving(); // Stop movement and rotation via handler

                  if (context.MovementHandler.Agent != null && context.MovementHandler.Agent.enabled) // Check if Agent exists and is enabled
                  {
                       // Safely disable the agent
                       context.MovementHandler.Agent.enabled = false; // <-- FIX HERE: Direct assignment after checks
                       Debug.Log($"{context.NpcObject.name}: NavMeshAgent disabled in Death state.", context.NpcObject);
                  }
             }
             // -------------------------------------------------

             // TODO: Placeholder logic for death
         }

         public override void OnUpdate(NpcStateContext context)
         {
             // No update logic needed
         }

         // OnReachedDestination is not applicable

         public override void OnExit(NpcStateContext context)
         {
             // DO NOT call base.OnExit
             Debug.Log($"{context.NpcObject.name}: Exiting generic Death state (should not happen).", context.NpcObject);
         }
        // Optional: Coroutine for post-death cleanup
        // ... (CleanupRoutine remains commented out) ...
    }
}