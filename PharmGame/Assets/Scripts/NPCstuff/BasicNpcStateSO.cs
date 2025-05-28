// --- START OF FILE BasicNpcStateSO.cs ---

using UnityEngine;
using System; // Needed for System.Enum
using Game.NPC.TI; // Needed for TiNpcData
using Game.NPC.BasicStates; // Needed for BasicState enum

namespace Game.NPC.BasicStates // Place Basic States in their own namespace
{
    /// <summary>
    /// Abstract base class for all Basic Npc State Scriptable Objects.
    /// Defines the simulation lifecycle methods called by the BasicNpcStateManager.
    /// These states operate directly on TiNpcData and do not require a GameObject.
    /// States are identified by a BasicState enum key.
    /// </summary>
    public abstract class BasicNpcStateSO : ScriptableObject
    {
        [Header("Base Basic State Settings")]
        [Tooltip("Indicates if the BasicNpcStateManager should check for a timeout and transition to BasicExitingStore if the timer expires.")]
        [SerializeField] private bool shouldUseTimeout = false;
        [Tooltip("Minimum duration for the simulated inactive state before potentially timing out.")]
        [SerializeField] private float minInactiveTimeout = 10f;
        [Tooltip("Maximum duration for the simulated inactive state before potentially timing out.")]
        [SerializeField] private float maxInactiveTimeout = 20f;


        // Public properties for settings
        public virtual bool ShouldUseTimeout => shouldUseTimeout;
        public virtual float MinInactiveTimeout => minInactiveTimeout;
        public virtual float MaxInactiveTimeout => maxInactiveTimeout;


        /// <summary>
        /// Defines the BasicState enum value that uniquely identifies this state.
        /// Must be implemented by derived classes.
        /// </summary>
        public abstract System.Enum HandledBasicState { get; }


        /// <summary>
        /// Called when the BasicNpcStateManager enters this state for a specific NPC.
        /// Use for setup that applies to the TiNpcData.
        /// </summary>
        public virtual void OnEnter(TiNpcData data, BasicNpcStateManager manager)
        {
            // Add basic logging
            string enumTypeName = HandledBasicState?.GetType().Name ?? "NULL_TYPE";
            string enumValueName = HandledBasicState?.ToString() ?? "NULL_VALUE";
            // Use data.Id for NPC identification in simulation logs
            Debug.Log($"SIM {data.Id}: Entering Basic State: {name} ({GetType().Name}) (Enum: {enumTypeName}.{enumValueName})");

            // Initialize or reset timer if using timeout
            if (shouldUseTimeout)
            {
                 // Ensure the timer is reset when entering a state with a timeout
                 data.simulatedStateTimer = UnityEngine.Random.Range(minInactiveTimeout, maxInactiveTimeout);
                 Debug.Log($"SIM {data.Id}: Basic State '{name}' uses timeout. Initializing timer to {data.simulatedStateTimer:F2}s.");
            } else {
                 // If state doesn't use timeout, ensure the timer is reset/zeroed
                 data.simulatedStateTimer = 0f;
            }

            // Clear target position by default on enter unless overridden by a movement state
            data.simulatedTargetPosition = null;
        }

        /// <summary>
        /// Called by the BasicNpcStateManager for a specific NPC on each simulation tick
        /// while the NPC is in this state. Use for continuous simulation logic.
        /// </summary>
        public abstract void SimulateTick(TiNpcData data, float deltaTime, BasicNpcStateManager manager);

        /// <summary>
        /// Called when the BasicNpcStateManager exits this state for a specific NPC.
        /// Use for cleanup that applies to the TiNpcData.
        /// </summary>
        public virtual void OnExit(TiNpcData data, BasicNpcStateManager manager)
        {
             // Add basic logging
            string enumTypeName = HandledBasicState?.GetType().Name ?? "NULL_TYPE";
            string enumValueName = HandledBasicState?.ToString() ?? "NULL_VALUE";
            Debug.Log($"SIM {data.Id}: Exiting Basic State: {name} ({GetType().Name}) (Enum: {enumTypeName}.{enumValueName})");

             // No default cleanup needed, specific states handle their data
        }
    }
}
// --- END OF FILE BasicNpcStateSO.cs ---