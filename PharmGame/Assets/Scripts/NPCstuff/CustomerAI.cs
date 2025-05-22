using UnityEngine;
using UnityEngine.AI; // Required for NavMeshAgent
using System.Collections.Generic;
using Utils.Pooling; // Required for PoolingManager
using CustomerManagement; // Required for CustomerManager and BrowseLocation
using System.Collections; // Required for Coroutines
using Systems.Inventory; // Required for Inventory and ItemDetails
using Random = UnityEngine.Random; // Specify UnityEngine.Random
using System.Linq; // Required for LINQ methods like Where, ToList, Sum
using Game.Events;
using Game.NPC.Handlers;

namespace Game.NPC // Your NPC namespace
{
    /// <summary>
    /// Defines the possible states for a customer NPC.
    /// </summary>
    public enum CustomerState
    {
        Inactive,            // In the pool, not active in the scene
        Initializing,        // Brief state after activation, before entering store
        Entering,            // Moving from spawn point into the store
        Browse,              // Moving between/simulating Browse at shelves
        MovingToRegister,    // Moving towards the cash register
        WaitingAtRegister,   // Waiting for the player at the register
        Queue,
        SecondaryQueue,
        TransactionActive,   // Player is scanning items (minigame)
        Exiting,             // Moving towards an exit point
        ReturningToPool      // Signalling completion and waiting to be returned
    }

    /// <summary>
    /// Manages the behavior and movement of a customer NPC by delegating
    /// state-specific logic to separate components.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))] // Ensure the GameObject has a NavMeshAgent
    [RequireComponent(typeof(CustomerShopper))] // Ensure the GameObject has a CustomerShopper
    [RequireComponent(typeof(NpcMovementHandler))] // Require the new handlers
    [RequireComponent(typeof(NpcAnimationHandler))]
    public class CustomerAI : MonoBehaviour
    {
        // --- Components ---
        public NpcMovementHandler MovementHandler { get; private set; } // New handler reference
        public NpcAnimationHandler AnimationHandler { get; private set; } // New handler referenc
        public CustomerShopper Shopper { get; private set; }


        // --- State Management ---
        private CustomerState currentState = CustomerState.Inactive;
        public CustomerState CurrentState { get { return currentState; } }
        public CustomerState PreviousState { get; private set; } = CustomerState.Inactive;
        private float stateEntryTime;

        private Dictionary<CustomerState, BaseCustomerStateLogic> stateLogics;
        private BaseCustomerStateLogic currentStateLogic;
        private Coroutine activeStateCoroutine;
        // ------------------------


        // --- References (Provided by CustomerManager during Initialize) ---
        [HideInInspector] public CustomerManager Manager { get; private set; }
        // --- Cached References ---
        // Cache the CashRegisterInteractable reference once found
        // Allow state logics to set this internally
        public CashRegisterInteractable CachedCashRegister { get; internal set; } // <-- Changed private set to internal set


        // --- Internal Data (Managed by AI script) ---
        // currentTargetLocation is still needed by CustomerAI to provide context to state logics
        // Allow state logics to set this internally
        public BrowseLocation? CurrentTargetLocation { get; internal set; } = null; // <-- Changed private set to internal set
        public int AssignedQueueSpotIndex { get; internal set; } = -1;

        private void Awake()
        {
            // --- Get Handler Components ---
            MovementHandler = GetComponent<NpcMovementHandler>();
            if (MovementHandler == null) { Debug.LogError($"CustomerAI ({gameObject.name}): NpcMovementHandler component not found!", this); enabled = false; return; }

            AnimationHandler = GetComponent<NpcAnimationHandler>();
            if (AnimationHandler == null) { Debug.LogError($"CustomerAI ({gameObject.name}): NpcAnimationHandler component not found!", this); enabled = false; return; }
            // ------------------------------

            Shopper = GetComponent<CustomerShopper>();
            if (Shopper == null) { Debug.LogError($"CustomerAI ({gameObject.name}): CustomerShopper component not found!", this); enabled = false; return; }

            // --- Find and Initialize State Logic Components ---
            stateLogics = new Dictionary<CustomerState, BaseCustomerStateLogic>();
            BaseCustomerStateLogic[] allLogics = GetComponents<BaseCustomerStateLogic>();

            if (allLogics.Length == 0)
            {
                Debug.LogError($"CustomerAI ({gameObject.name}): No BaseCustomerStateLogic components found on this GameObject!", this);
                enabled = false;
                return;
            }

            foreach (var logic in allLogics)
            {
                if (stateLogics.ContainsKey(logic.HandledState))
                {
                    Debug.LogWarning($"CustomerAI ({gameObject.name}): Duplicate state logic found for state {logic.HandledState}! Only the first one found will be used.", this);
                    continue;
                }
                stateLogics.Add(logic.HandledState, logic);
                logic.Initialize(this); // Initialize the state logic component, passing itself
            }

            Debug.Log($"CustomerAI ({gameObject.name}): Found and initialized {stateLogics.Count} state logic components in Awake.");
            // ---------------------------------------------------

            // Ensure agent is disabled initially, will be enabled by Initialize/SetState
            if (MovementHandler != null && MovementHandler.Agent != null) MovementHandler.Agent.enabled = false;

            Debug.Log($"CustomerAI ({gameObject.name}): Awake completed.");
        }

        private void OnEnable() // Subscribe to events when the GameObject is enabled
        {
            // Subscribe to events that can trigger state changes for *this specific NPC*
            // Use a lambda with a check for the specific NPC object
            EventManager.Subscribe<NpcImpatientEvent>(HandleNpcImpatient);
            EventManager.Subscribe<ReleaseNpcFromSecondaryQueueEvent>(HandleReleaseFromSecondaryQueue);
            // Subscribe to future interruption events
            // EventManager.Subscribe<NpcAttackedEvent>(HandleNpcAttacked);
            // EventManager.Subscribe<NpcInteractedEvent>(HandleNpcInteracted);

            Debug.Log($"{gameObject.name}: Subscribed to events in OnEnable.");
        }

        private void OnDisable() // Unsubscribe from events when the GameObject is disabled
        {
            // Unsubscribe from events
            EventManager.Unsubscribe<NpcImpatientEvent>(HandleNpcImpatient);
            EventManager.Unsubscribe<ReleaseNpcFromSecondaryQueueEvent>(HandleReleaseFromSecondaryQueue);
            // Unsubscribe from future interruption events
            // EventManager.Unsubscribe<NpcAttackedEvent>(HandleNpcAttacked);
            // EventManager.Unsubscribe<NpcInteractedEvent>(HandleNpcInteracted);

            Debug.Log($"{gameObject.name}: Unsubscribed from events in OnDisable.");
        }

        // --- Event Handlers ---

        private void HandleNpcImpatient(NpcImpatientEvent eventArgs)
        {
            // Check if this event is for THIS NPC
            if (eventArgs.NpcObject == this.gameObject)
            {
                Debug.Log($"{gameObject.name}: Handling NpcImpatientEvent. Setting state to Exiting.");
                SetState(CustomerState.Exiting); // Transition to the Exiting state
            }
        }

        private void HandleReleaseFromSecondaryQueue(ReleaseNpcFromSecondaryQueueEvent eventArgs)
        {
            // Check if this event is for THIS NPC
            if (eventArgs.NpcObject == this.gameObject)
            {
                Debug.Log($"{gameObject.name}: Handling ReleaseNpcFromSecondaryQueueEvent. Setting state to Entering.");
                // Transition to the Entering state to enter the store
                SetState(CustomerState.Entering);
            }
        }

        // --- Future Interruption Handlers (Placeholder) ---
        /*
        private void HandleNpcAttacked(NpcAttackedEvent eventArgs)
        {
            if (eventArgs.NpcObject == this.gameObject)
            {
                 Debug.Log($"{gameObject.name}: Handling NpcAttackedEvent. Setting state to Combat.");
                 // Need a Combat state logic and SO later
                 // SetState(CustomerState.Combat); // Example
            }
        }

        private void HandleNpcInteracted(NpcInteractedEvent eventArgs)
        {
             if (eventArgs.NpcObject == this.gameObject)
             {
                 Debug.Log($"{gameObject.name}: Handling NpcInteractedEvent. Setting state to Social.");
                 // Need a Social state logic and SO later
                 // SetState(CustomerState.Social); // Example
             }
        }
        */
        // --------------------------------------------------

        /// <summary>
        /// Initializes the NPC when it's retrieved from the pool.
        /// Should be called by the CustomerManager AFTER the GameObject is active.
        /// </summary>
        /// <param name="manager">The CustomerManager instance managing this NPC.</param>
        /// <param name="startPosition">The initial position for the NPC.</param>
        public void Initialize(CustomerManager manager, Vector3 startPosition)
        {
            this.Manager = manager;
            ResetNPC(); // Clean up previous state (NavMeshAgent is disabled here)

            // Access MovementHandler for Warp/movement setup
            if (MovementHandler != null && MovementHandler.Agent != null)
            {
                // MovementHandler.Agent.enabled = true; // Enable agent for warping/movement - Handled by Warp now

                if (MovementHandler.Warp(startPosition)) // Use handler's Warp method
                {
                    Debug.Log($"CustomerAI ({gameObject.name}): Warped to {startPosition} using MovementHandler.");
                    // NavMeshAgent ResetPath and isStopped are handled by Warp now
                }
                else
                {
                    Debug.LogWarning($"CustomerAI ({gameObject.name}): Failed to Warp to {startPosition} using MovementHandler. Is the position on the NavMesh?", this);
                    // If Warp fails, the agent is likely not on the NavMesh, disable it again
                    // MovementHandler.Agent.enabled = false; // Added defensive disable - Handled by Warp now if success is false
                    SetState(CustomerState.ReturningToPool); // Cannot initialize properly, return
                    return;
                }
            }
            else
            {
                Debug.LogError($"CustomerAI ({gameObject.name}): MovementHandler or Agent is null during Initialize!", this);
                SetState(CustomerState.ReturningToPool);
                return;
            }

            SetState(CustomerState.Initializing); // Start the AI state flow

            Debug.Log($"CustomerAI ({gameObject.name}): Initialized at {startPosition}.");
        }

        /// <summary>
        /// Resets the NPC's state and data when initialized from the pool.
        /// Cleans up properties from prior use.
        /// </summary>
        private void ResetNPC()
        {
            // Clean up AI specific references/data not managed by state logic
            CurrentTargetLocation = null;
            CachedCashRegister = null;
            AssignedQueueSpotIndex = -1; // Reset queue spot index

            // Reset the shopper component
            Shopper?.Reset();

             Debug.Log($"CustomerAI ({gameObject.name}): NPC state reset (AI data and Shopper). NavMeshAgent cleanup is handled by MovementHandler.");
        }

        /// <summary>
        /// Public method to set the NPC's current state.
        /// Delegates state entry, update, coroutine, and exit logic to state-specific components.
        /// </summary>
        /// <param name="newState">The state to transition to.</param>
        public void SetState(CustomerState newState)
        {
            // Prevent transitioning to the same state unnecessarily
            if (currentState == newState && currentStateLogic != null)
            {
                // Optional: Log if trying to re-enter same state
                // Debug.Log($"CustomerAI ({gameObject.name}): Already in state {newState}. Ignoring SetState call.");
                return;
            }

            Debug.Log($"CustomerAI ({gameObject.name}): <color=yellow>Transitioning from {currentState} to {newState}</color>", this); // Highlight state changes

            PreviousState = currentState;

            // 1. Perform exit logic on the current state (if any)
            if (currentStateLogic != null)
            {
                currentStateLogic.OnExit();
                StopManagedCoroutine(activeStateCoroutine); // Stop the coroutine from the previous state
                activeStateCoroutine = null; // Clear the reference
            }

            // 2. Update the current state variable
            currentState = newState;
            stateEntryTime = Time.time; // Update state entry time

            // 3. Find and activate the new state logic
            if (stateLogics != null && stateLogics.TryGetValue(newState, out var nextStateLogic))
            {
                currentStateLogic = nextStateLogic; // Set the new active state logic

                // Perform entry logic on the new state
                currentStateLogic.OnEnter();

                // Start the state coroutine for the new state (if any)
                if (newState != CustomerState.ReturningToPool) // <-- ADD THIS CONDITION
                {
                    activeStateCoroutine = StartManagedCoroutine(currentStateLogic.StateCoroutine());
                }
                else
                {
                    // For ReturningToPool state, no coroutine is needed/possible after OnEnter
                    activeStateCoroutine = null; // Explicitly clear the reference
                }

            }
            else
            {
                // Handle cases where a state logic component is missing
                Debug.LogError($"CustomerAI ({gameObject.name}): No state logic component found in dictionary for state {newState}!", this);
                currentStateLogic = null; // Ensure no invalid logic is referenced

                // Transition to a safe state or return to pool if a critical logic is missing
                if (newState != CustomerState.ReturningToPool) // Avoid infinite loop if returning logic is missing
                {
                    SetState(CustomerState.ReturningToPool);
                }
                else
                {
                    Debug.LogError($"CustomerAI ({gameObject.name}): Cannot transition to ReturningToPool as its logic is missing! Publishing NpcReturningToPoolEvent directly.", this);
                    EventManager.Publish(new NpcReturningToPoolEvent(this.gameObject));
                }
            }

            // Optional: Any general actions that happen on *every* state transition could go here
            // e.g., ensure NavMeshAgent is stopped/started based on whether the new state is movement or not.
            // However, it's often better to handle NavMeshAgent state within the OnEnter/OnUpdate of the movement states.
        }

        /// <summary>
        /// Allows a state logic component to start a coroutine managed by the CustomerAI.
        /// </summary>
        public Coroutine StartManagedCoroutine(IEnumerator routine)
        {
            if (routine == null) return null;
            // Ensure the routine actually belongs to a state logic before starting if desired
            // E.g., if (routine.Target is BaseCustomerStateLogic)
            return StartCoroutine(routine);
        }

        /// <summary>
        /// Allows a state logic component to stop a managed coroutine.
        /// </summary>
        public void StopManagedCoroutine(Coroutine routine)
        {
            if (routine != null) StopCoroutine(routine);
        }


        private void Update()
        {
            // Delegate the update logic to the currently active state logic component
            currentStateLogic?.OnUpdate();

            if (MovementHandler != null && MovementHandler.Agent != null && AnimationHandler != null)
            {
                 // Use world speed for animation blending
                 float speed = MovementHandler.Agent.velocity.magnitude;
                 AnimationHandler.SetSpeed(speed); // Call handler's method
            }
        }

        /// <summary>
        /// Helper to check if the NavMeshAgent has reached its current destination.
        /// Accounts for path pending, remaining distance, and stopping.
        /// Kept in CustomerAI for now, used by state logic components via reference.
        /// </summary>
        public bool HasReachedDestination() // Made public for state logic to access
        {
            // Delegate to the MovementHandler
            if (MovementHandler != null)
            {
                 return MovementHandler.IsAtDestination();
            }
            return false; // Cannot check if handler is null
        }

        /// <summary>
        /// Called by the CustomerManager to signal that this customer is next in line
        /// and should proceed to the register.
        /// </summary>
        public void GoToRegisterFromQueue()
        {
            Debug.Log($"{gameObject.name}: Signalled by manager to go to the register from the queue.");
            // Transition to the MovingToRegister state
            SetState(CustomerState.MovingToRegister);

            // Note: The OnExit for the Queue state will handle signaling the spot free.
            // The OnEnter for MovingToRegister will find the register point and set the destination.
        }


        // --- Public methods for external systems to call ---

        /// <summary>
        /// Called by the CashRegister to initiate the transaction minigame.
        /// </summary>
        public void StartTransaction()
        {
            // CashRegisterInteractable gets the items from Shopper.GetItemsToBuy() when player interacts.
            // This method just signals the NPC to enter the transaction state.
            SetState(CustomerState.TransactionActive);
            Debug.Log($"CustomerAI ({gameObject.name}): Transaction started.");
        }

        /// <summary>
        /// Called by the CashRegister/Minigame system when the transaction is completed.
        /// </summary>
        /// <param name="paymentReceived">The amount of money the player received.</param>
        public void OnTransactionCompleted(float paymentReceived)
        {
            Debug.Log($"CustomerAI ({gameObject.name}): Transaction completed. Player received {paymentReceived} money.");
            // Maybe play a happy animation/sound
            SetState(CustomerState.Exiting); // Move to exit
        }

        /// <summary>
        /// Public getter for the list of items the customer intends to buy.
        /// Called by the CashRegister system. Delegates to the CustomerShopper.
        /// </summary>
        /// <returns>A list of (ItemDetails, quantity) pairs.</returns>
        public List<(ItemDetails details, int quantity)> GetItemsToBuy()
        {
            // Delegate the call to the Shopper component
            if (Shopper != null)
            {
                return Shopper.GetItemsToBuy();
            }
            else
            {
                Debug.LogError($"CustomerAI ({gameObject.name}): GetItemsToBuy called but Shopper component is null!", this);
                return new List<(ItemDetails details, int quantity)>(); // Return empty list to avoid null reference
            }
        }
    }
}