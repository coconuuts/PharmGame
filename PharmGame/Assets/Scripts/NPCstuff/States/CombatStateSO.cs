// --- CombatStateSO.cs ---
using UnityEngine;
using System.Collections; // Needed for IEnumerator
using Game.NPC; // Needed for CustomerState enum
using Game.NPC.States; // Needed for NpcStateSO and NpcStateContext

namespace Game.NPC.States
{
    /// <summary>
    /// A generic state for an NPC engaged in combat.
    /// Its specific logic will depend on the combat system.
    /// Corresponds to CustomerState.Combat.
    /// </summary>
    [CreateAssetMenu(fileName = "CombatState", menuName = "NPC/General States/Combat", order = 5)] // Placed under General States
    public class CombatStateSO : NpcStateSO
    {
        public override CustomerState HandledState => CustomerState.Combat; // <-- Use Combat enum

        // Combat state is typically NOT interruptible by other events like interaction or emoting
        // You might want to override the IsInterruptible property:
        // public override bool IsInterruptible => false; // Example override


        public override void OnEnter(NpcStateContext context)
        {
            base.OnEnter(context); // Call base OnEnter (logs entry, enables Agent)
            Debug.Log($"{context.NpcObject.name}: Entering generic Combat state.", context.NpcObject);

            // Stop movement immediately upon entering combat
            context.MovementHandler?.StopMoving();

            // TODO: Placeholder logic for starting combat behavior
            // - Find/Engage target (likely based on the event that triggered combat)
            // - Play combat stance/idle animation
            // - Start combat routine (attacking, moving, etc.)

            // Example: Play a combat idle animation (assuming it exists)
            // context.PlayAnimation("CombatIdle");

             // Example: Start a coroutine for combat logic (will be implemented in Phase 5/Combat system)
             // context.StartCoroutine(CombatRoutine(context));
        }

        public override void OnUpdate(NpcStateContext context)
        {
            base.OnUpdate(context); // Call base OnUpdate (empty)

            // TODO: Placeholder logic for continuous combat behavior
            // - Check target status
            // - Update attack cooldowns
            // - Check conditions to exit combat (e.g., target defeated, out of range, timer)
            // - Trigger transitions out of combat state (e.g., to Death, Idle, ReturningToPool)
        }

        // OnReachedDestination is not applicable to this state unless combat involves specific movement points

        public override void OnExit(NpcStateContext context)
        {
            base.OnExit(context); // Call base OnExit (logs exit, stops movement/rotation - though combat might manage its own movement)
             Debug.Log($"{context.NpcObject.name}: Exiting generic Combat state.", context.NpcObject);
            // TODO: Placeholder logic for exiting combat
            // - Stop combat routine
            // - Transition to post-combat state (e.g., Idle, resume previous state if interrupted)

             // Example: Stop combat animation
             // context.PlayAnimation("Idle"); // Return to idle
        }

        // Optional: Coroutine for complex combat sequences
        /*
        private IEnumerator CombatRoutine(NpcStateContext context)
        {
            Debug.Log($"{context.NpcObject.name}: CombatRoutine started in {name}.", context.NpcObject);
            // Implement combat sequence here (attacking, moving between cover, etc.)
            while (true) // Loop while in combat
            {
                // Check for exit conditions and call context.TransitionToState(...)
                yield return null;
            }
        }
        */
    }
}