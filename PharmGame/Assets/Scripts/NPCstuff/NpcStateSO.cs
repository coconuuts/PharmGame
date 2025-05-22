// --- Update NpcStateSO.cs (Add HandledState and OnReachedDestination) ---
using UnityEngine;
using System.Collections;
// Ensure you have CustomerState enum available via a using directive or qualified name
using Game.NPC;

namespace Game.NPC.States
{
    /// <summary>
    /// Abstract base class for all NPC State Scriptable Objects.
    /// Defines the lifecycle methods called by the NpcStateMachineRunner.
    /// Uses NpcStateContext to provide access to handlers and data.
    /// </summary>
    public abstract class NpcStateSO : ScriptableObject
    {
        [Header("Base State Settings")]
        [Tooltip("Optional: A unique identifier for this state.")]
        [SerializeField] private string stateID = "";

        [Tooltip("Indicates if this state can be interrupted (e.g., by combat or interaction).")]
        [SerializeField] private bool isInterruptible = true;

        // Public properties for settings
        public string StateID => stateID;
        public virtual bool IsInterruptible => isInterruptible;

        /// <summary>
        /// Defines the CustomerState enum value this SO represents (for migration phase).
        /// Must be implemented by derived classes.
        /// </summary>
        public abstract CustomerState HandledState { get; } // <-- ADD THIS ABSTRACT PROPERTY


        /// <summary>
        /// Called when the state machine enters this state. Use for setup.
        /// </summary>
        public virtual void OnEnter(NpcStateContext context)
        {
             Debug.Log($"{context.NpcObject.name}: Entering State: {name} ({GetType().Name}) (Enum: {HandledState})", context.NpcObject); // Add enum to log

             // Ensure NavMeshAgent is enabled via the handler when entering most states
             if (context.MovementHandler?.Agent != null)
             {
                  context.MovementHandler.Agent.enabled = true;
             }
        }

        /// <summary>
        /// Called every frame while the state machine is in this state. Use for continuous logic.
        /// </summary>
        public virtual void OnUpdate(NpcStateContext context)
        {
            // Default implementation does nothing
        }

         /// <summary>
         /// Called by the state machine runner when the NPC reaches its current movement destination.
         /// Implement in derived states that involve movement towards a point.
         /// </summary>
         /// <param name="context">The context providing access to handlers and data.</param>
         public virtual void OnReachedDestination(NpcStateContext context) // <-- ADD THIS VIRTUAL METHOD
         {
              // Default implementation does nothing
              Debug.Log($"{context.NpcObject.name}: Reached destination in state {name} ({HandledState}), but OnReachedDestination is not overridden or performs no action.", context.NpcObject);
         }


         /// <summary>
         /// Called when the state machine exits this state. Use for cleanup.
         /// </summary>
         public virtual void OnExit(NpcStateContext context)
        {
            Debug.Log($"{context.NpcObject.name}: Exiting State: {name} ({GetType().Name}) (Enum: {HandledState})", context.NpcObject); // Add enum to log

            // Stop any movement or rotation when exiting a state by default
            context.MovementHandler?.StopMoving();
            context.MovementHandler?.StopRotation();
        }
    }
}