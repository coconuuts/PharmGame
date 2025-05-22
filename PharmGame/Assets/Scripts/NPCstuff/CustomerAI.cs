// --- Updated CustomerAI.cs (Final Streamlining for Phase 4) ---
using UnityEngine;
using UnityEngine.AI; // Still needed because NavMeshAgent is a RequireComponent of MovementHandler
using System; // Needed for Enum if HandledState or other enum lookups were exposed
// Removed unused using directives: System.Collections.Generic, CustomerManagement, Systems.Inventory
using Game.NPC.Handlers; // Needed for handler access
using Game.NPC.States; // Needed for NpcStateMachineRunner
using Game.NPC.Types; // Might be needed if TypeDefinitions are exposed here

namespace Game.NPC // Your NPC namespace
{
    /// <summary>
    /// Defines the possible states for a customer NPC (primarily for CustomerManager/Event compatibility during migration).
    /// Includes placeholder states for general NPC types.
    /// </summary>
    // Keep CustomerState enum here for now, as CustomerManager, events, and State SOs still use it.
    public enum CustomerState
    {
        Inactive,
        Initializing,
        LookingToShop,
        Entering,
        Browse,
        MovingToRegister,
        WaitingAtRegister,
        Queue,
        SecondaryQueue,
        TransactionActive,
        Exiting,
        ReturningToPool,

        // Placeholder states for General NPC types
        Combat,
        Social,
        Emoting,
        Idle,
        Death
    }

    /// <summary>
    /// Represents an individual NPC GameObject in the game world.
    /// Acts as the root MonoBehaviour, holds essential handler components and the state machine runner.
    /// Serves as the primary access point on the GameObject for other systems.
    /// </summary>
    // Require the essential components that define the NPC's capabilities and behavior system
    [RequireComponent(typeof(NpcMovementHandler))]
    [RequireComponent(typeof(NpcAnimationHandler))]
    [RequireComponent(typeof(CustomerShopper))] // Keeping Shopper on the base NPC
    [RequireComponent(typeof(NpcStateMachineRunner))]
    public class CustomerAI : MonoBehaviour
    {
        // --- Essential References (Public Access Points) ---
        // Other systems can get CustomerAI and access these handlers/runner.
        public NpcMovementHandler MovementHandler { get; private set; }
        public NpcAnimationHandler AnimationHandler { get; private set; }
        public CustomerShopper Shopper { get; private set; }
        public NpcStateMachineRunner StateMachineRunner { get; private set; }

        // Other data fields specific to this NPC instance, but not state-logic or handler detail, can live here.
        // E.g., NPC Name, Appearance settings, Dialogue data reference, etc.

        private void Awake()
        {
            // Get references to the essential components on this GameObject
            MovementHandler = GetComponent<NpcMovementHandler>();
            AnimationHandler = GetComponent<NpcAnimationHandler>();
            Shopper = GetComponent<CustomerShopper>();
            StateMachineRunner = GetComponent<NpcStateMachineRunner>();

            // Basic validation (Runner also validates handlers, but redundancy is okay here)
             if (MovementHandler == null || AnimationHandler == null || Shopper == null || StateMachineRunner == null)
             {
                  Debug.LogError($"CustomerAI ({gameObject.name}): Missing one or more essential components in Awake! Self-disabling.", this);
                  enabled = false; // Cannot function
             }

            // The Runner's Awake will handle its own setup, state loading, and handler references.
            // The MovementHandler's Awake will disable the NavMeshAgent initially.
            // The Shopper's Awake will get its Inventory reference.

            Debug.Log($"CustomerAI ({gameObject.name}): Awake completed. Essential components acquired.");
        }
    }
}