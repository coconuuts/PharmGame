using UnityEngine;
using System; // Needed for System.Enum
using System.Collections;
// Ensure you have Game.NPC namespace accessible for CustomerState and GeneralState if needed in derived SOs
using Game.Proximity; // <-- NEW: Needed for ProximityZone in derived states if they check it

namespace Game.NPC.States
{
    /// <summary>
    /// Abstract base class for all NPC State Scriptable Objects.
    /// Defines the lifecycle methods called by the NpcStateMachineRunner.
    /// Uses NpcStateContext to provide access to handlers and data.
    /// States are identified by a generic System.Enum key.
    /// </summary>
    public abstract class NpcStateSO : ScriptableObject
    {
        [Header("Base State Settings")]
        [Tooltip("Optional: A unique identifier for this state (redundant if HandledState is used as ID).")]
        [SerializeField] private string stateID = ""; // This field becomes less critical if HandledState is the primary ID

        [Tooltip("Indicates if this state can be interrupted (e.g., by combat or interaction).")]
        [SerializeField] private bool isInterruptible = true;

        [Tooltip("Indicates if the Runner should check for movement arrival (IsAtDestination) and call OnReachedDestination while in this state.")]
        [SerializeField] private bool checkMovementArrival = false; // <-- NEW FIELD for Runner's Update logic

        // Public properties for settings
        public string StateID => stateID;
        public virtual bool IsInterruptible => isInterruptible;
        public bool CheckMovementArrival => checkMovementArrival;


        /// <summary>
        /// Defines the System.Enum value that uniquely identifies this state.
        /// Must be implemented by derived classes. The specific enum type (CustomerState, GeneralState, etc.)
        /// determines the category, and the value is the specific state within that category.
        /// </summary>
        public abstract System.Enum HandledState { get; }


        /// <summary>
        /// Called when the state machine enters this state. Use for setup.
        /// </summary>
        public virtual void OnEnter(NpcStateContext context)
        {
             // Add logging robustness using HandledState properties
             string enumTypeName = HandledState?.GetType().Name ?? "NULL_TYPE";
             string enumValueName = HandledState?.ToString() ?? "NULL_VALUE";
             Debug.Log($"{context.NpcObject.name}: Entering State: {name} ({GetType().Name}) (Enum: {enumTypeName}.{enumValueName})", context.NpcObject);

             // Ensure NavMeshAgent is enabled via the handler when entering most states
             // States like Death, ReturningToPool should override and disable.
             if (context.MovementHandler?.Agent != null)
             {
                  context.MovementHandler.Agent.enabled = true;
             }
        }

        /// <summary>
        /// Called every frame while the state machine is in this state. Use for continuous logic.
        /// Note: This method is affected by the Runner's update throttling, UNLESS the NPC is in an interrupted state.
        /// </summary>
        public virtual void OnUpdate(NpcStateContext context) // <-- Updated comment
        {
            // Default implementation does nothing
        }

         /// <summary>
         /// Called by the state machine runner when the NPC reaches its current movement destination.
         /// Implement in derived states that involve movement towards a point and have CheckMovementArrival set to true.
         /// </summary>
         public virtual void OnReachedDestination(NpcStateContext context)
         {
              // Add logging robustness using HandledState properties
              string enumTypeName = HandledState?.GetType().Name ?? "NULL_TYPE";
              string enumValueName = HandledState?.ToString() ?? "NULL_VALUE";
              Debug.Log($"{context.NpcObject.name}: Reached destination in state {name} ({enumTypeName}.{enumValueName}), but OnReachedDestination is not overridden or performs no action.", context.NpcObject);
              // Default behavior might be to transition to Idle if this isn't handled?
              // Or simply stop movement (handled by Runner already before calling this).
         }


         /// <summary>
         /// Called when the state machine exits this state. Use for cleanup.
         /// </summary>
         public virtual void OnExit(NpcStateContext context)
        {
             // Add logging robustness using HandledState properties
             string enumTypeName = HandledState?.GetType().Name ?? "NULL_TYPE";
             string enumValueName = HandledState?.ToString() ?? "NULL_VALUE";
            Debug.Log($"{context.NpcObject.name}: Exiting State: {name} ({GetType().Name}) (Enum: {enumTypeName}.{enumValueName})", context.NpcObject);

            // Stop any movement or rotation when exiting a state by default
            context.MovementHandler?.StopMoving();
            context.MovementHandler?.StopRotation();
        }
    }
}