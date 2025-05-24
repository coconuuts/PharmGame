// --- Updated CombatStateSO.cs (Full Placeholder Logic) ---
using UnityEngine;
using System.Collections;
using System;
using Game.NPC;
using Game.NPC.States;
using Game.Events;
using Random = UnityEngine.Random;

namespace Game.NPC.States
{
    [CreateAssetMenu(fileName = "CombatState", menuName = "NPC/General States/Combat", order = 5)]
    public class CombatStateSO : NpcStateSO
    {
        public override System.Enum HandledState => GeneralState.Combat;

        public override bool IsInterruptible => false; // Combat state is typically NOT interruptible

        [Header("Combat Simulation Settings")]
        [Tooltip("Minimum duration for the simulated combat state.")]
        [SerializeField] private float minCombatDuration = 3.0f;
        [Tooltip("Maximum duration for the simulated combat state.")]
        [SerializeField] private float maxCombatDuration = 8.0f;

        // Could add animation names, attack logic references, etc. here

        private Coroutine combatRoutine;

        public override void OnEnter(NpcStateContext context)
        {
            string enumTypeName = HandledState?.GetType().Name ?? "NULL_TYPE";
            string enumValueName = HandledState?.ToString() ?? "NULL_VALUE";
            Debug.Log($"{context.NpcObject.name}: Entering State: {name} ({GetType().Name}) (Enum: {enumTypeName}.{enumValueName})", context.NpcObject);


            // Stop movement and rotation
            context.MovementHandler?.StopMoving();
            context.MovementHandler?.StopRotation();
             // Agent enabling might be needed by a combat system, or disabled for root motion.
             // Let's re-enable it here if needed for movement within combat.
             // Base OnEnter enables, but if we skipped it, ensure it's on.
             if (context.MovementHandler?.Agent != null && !context.MovementHandler.Agent.enabled)
             {
                 context.MovementHandler.Agent.enabled = true;
             }


            // Play combat stance/idle animation
            // context.PlayAnimation("CombatIdle"); // Example

            // Start a coroutine for combat simulation logic and completion
            combatRoutine = context.StartCoroutine(CombatRoutine(context));
        }

        public override void OnUpdate(NpcStateContext context)
        {
            // Continuous combat logic (e.g., look at target)
             // Example: Look at the interactor who triggered combat (if available in context)
             // if (context.InteractorObject != null)
             // {
             //     Vector3 directionToTarget = (context.InteractorObject.transform.position - context.NpcObject.transform.position).normalized;
             //      if (directionToTarget.sqrMagnitude > 0.001f)
             //      {
             //           Quaternion targetRotation = Quaternion.LookRotation(directionToTarget);
             //           // Use MovementHandler's instant rotation or start new rotation coroutine if not moving
             //            if (context.MovementHandler != null) context.MovementHandler.Agent.transform.rotation = targetRotation; // Instant rotate example
             //      }
             // }
        }

        // OnReachedDestination is not applicable

        public override void OnExit(NpcStateContext context)
        {
             string enumTypeName = HandledState?.GetType().Name ?? "NULL_TYPE";
             string enumValueName = HandledState?.ToString() ?? "NULL_VALUE";
            Debug.Log($"{context.NpcObject.name}: Exiting State: {name} ({GetType().Name}) (Enum: {enumTypeName}.{enumValueName})", context.NpcObject);


            // Cleanup logic for exiting combat
            // Ensure movement is stopped (Base OnExit does this, but if we don't call base...)
             context.MovementHandler?.StopMoving();
             context.MovementHandler?.StopRotation();

             // Transition to idle animation or locomotion base state
            // context.PlayAnimation("Idle"); // Return to idle
        }

        // Coroutine method to simulate combat duration and signal completion
        private IEnumerator CombatRoutine(NpcStateContext context)
        {
            Debug.Log($"{context.NpcObject.name}: CombatRoutine started in {name}.", context.NpcObject);

            float combatDuration = Random.Range(minCombatDuration, maxCombatDuration);
            Debug.Log($"{context.NpcObject.name}: Simulating combat for {combatDuration:F2} seconds.", context.NpcObject);

            yield return new WaitForSeconds(combatDuration);

            Debug.Log($"{context.NpcObject.name}: Combat simulation finished. Publishing NpcCombatEndedEvent.", context.NpcObject);

            context.PublishEvent(new NpcCombatEndedEvent(context.NpcObject));
        }
    }
}