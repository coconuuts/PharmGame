// --- Updated NpcStateMachineRunner.cs ---
using UnityEngine;
using System.Collections.Generic;
using System.Collections;
using Game.NPC.Handlers;
using Game.NPC.States; // Needed for State SO references and Context
using CustomerManagement;
using Game.Events;
using Systems.Inventory; // Needed for ItemDetails (for GetItemsToBuy)
using System.Linq; // Needed for First()
using Game.NPC.Types;
using System;

namespace Game.NPC
{
    /// <summary>
    /// The central component on an NPC GameObject that runs the data-driven state machine.
    /// Executes the logic defined in NpcStateSO assets and manages state transitions.
    /// Creates and provides the NpcStateContext to executing states.
    /// </summary>
    public class NpcStateMachineRunner : MonoBehaviour
    {
        // --- References to Handler Components (Accessed by State SOs via the Context) ---
        // These are automatically found in Awake
        public NpcMovementHandler MovementHandler { get; private set; }
        public NpcAnimationHandler AnimationHandler { get; private set; }
        public CustomerShopper Shopper { get; private set; }

        // --- External References (Provided by CustomerManager or found) ---
        [HideInInspector] public CustomerManager Manager { get; private set; } // Provided by Manager
        public CashRegisterInteractable CachedCashRegister { get; internal set; } // Cached (e.g., by Waiting state)

        // --- State Management ---
        private NpcStateSO currentState;
        private NpcStateSO previousState;
        // TODO: Add a Stack<NpcStateSO> for interruption handling (Phase 5)
        private Coroutine activeStateCoroutine;

        // --- Master Dictionary of all available states for THIS NPC ---
        private Dictionary<Enum, NpcStateSO> availableStates;

        // --- NPC Type Definitions (Phase 4 - Assigned in Inspector) ---
        [Header("NPC Type Definitions")]
        [Tooltip("Assign the type definitions that define this NPC's states (e.g., General, Customer). Order matters for overrides.")]
        [SerializeField] private List<NpcTypeDefinitionSO> npcTypes;

        [Tooltip("The Enum key for the primary starting state for this NPC type (e.g., CustomerState.LookingToShop).")]
        [SerializeField] private string primaryStartingStateEnumKey; // Store as string for inspector
        [Tooltip("The Type name of the Enum key for the primary starting state (e.g., Game.NPC.CustomerState).")]
        [SerializeField] private string primaryStartingStateEnumType; // Store as string for inspector


        // --- Internal Data/State Needed by SOs (Can be accessed via Context now) ---
        // Kept here for the Runner to manage/set, but states access via Context.
        public BrowseLocation? CurrentTargetLocation { get; internal set; } = null;
        public int AssignedQueueSpotIndex { get; internal set; } = -1;

        // --- State Context (Reusable structure) ---
        private NpcStateContext _stateContext; // Cache the context to avoid recreating every frame


        private void Awake()
        {
            // --- Get Handler References ---
            MovementHandler = GetComponent<NpcMovementHandler>();
            AnimationHandler = GetComponent<NpcAnimationHandler>();
            Shopper = GetComponent<CustomerShopper>();

            if (MovementHandler == null || AnimationHandler == null || Shopper == null)
            {
                Debug.LogError($"NpcStateMachineRunner on {gameObject.name}: Missing required handler components! MovementHandler: {MovementHandler != null}, AnimationHandler: {AnimationHandler != null}, Shopper: {Shopper != null}", this);
                enabled = false;
            }

            // --- Load States from NPC Type Definitions (Phase 4) ---
            availableStates = new Dictionary<Enum, NpcStateSO>();
            if (npcTypes != null)
            {
                foreach (var typeDef in npcTypes)
                {
                    if (typeDef != null)
                    {
                        // GetAllStates handles inheritance and provides a flat dictionary
                        var typeStates = typeDef.GetAllStates();
                        foreach (var pair in typeStates)
                        {
                            // Add/Overwrite states from this type definition
                            // Ensure key and value are valid
                            if (pair.Key != null && pair.Value != null)
                            {
                                // Check if the assigned SO's HandledState enum type matches the key's enum type
                                // This is important for type safety when casting keys later
                                Type keyEnumType = pair.Key.GetType();
                                Type soHandledEnumType = pair.Value.HandledState.GetType();
                                if (keyEnumType != soHandledEnumType)
                                {
                                     Debug.LogError($"NpcStateMachineRunner ({gameObject.name}): Enum Type mismatch for state '{pair.Key}' in type definition '{typeDef.name}'! Expected '{keyEnumType.Name}', assigned State SO '{pair.Value.name}' has HandledState type '{soHandledEnumType.Name}'. State ignored.", this);
                                     continue; // Skip adding this state if types don't match
                                }

                                availableStates[pair.Key] = pair.Value;
                            }
                             else
                             {
                                Debug.LogWarning($"NpcStateMachineRunner ({gameObject.name}): Skipping null state entry in type definition '{typeDef.name}'.", this);
                             }
                        }
                    }
                    else
                    {
                        Debug.LogWarning($"NpcStateMachineRunner ({gameObject.name}): Null NPC Type Definition assigned in the list!", this);
                    }
                }
            }

            if (availableStates.Count == 0)
            {
                 Debug.LogError($"NpcStateMachineRunner ({gameObject.name}): No states loaded from assigned type definitions! Cannot function.", this);
                 enabled = false;
                 return;
            }
            Debug.Log($"NpcStateMachineRunner ({gameObject.name}): Loaded {availableStates.Count} states from {npcTypes?.Count ?? 0} type definitions.");
            // ----------------------------------------------------

             // Ensure agent is disabled initially (handled by MovementHandler Awake)
            if (MovementHandler?.Agent != null) MovementHandler.Agent.enabled = false;

             // --- Initialize the State Context struct ---
             // Fields that change per-state will be updated on the context instance before passing.
             _stateContext = new NpcStateContext
             {
                 MovementHandler = MovementHandler,
                 AnimationHandler = AnimationHandler,
                 Shopper = Shopper,
                 Manager = null, // Manager is set in Initialize()
                 CachedCashRegister = null, // Cached by states
                 NpcObject = this.gameObject,
                 Runner = this,
                 CurrentTargetLocation = null, // Set by states/runner
                 AssignedQueueSpotIndex = -1, // Set by states/runner
             };
            // -------------------------------------------

            Debug.Log($"{gameObject.name}: NpcStateMachineRunner Awake completed.");
        }

        private void OnEnable()
        {
            // --- Subscribe to Events that trigger state changes ---
            EventManager.Subscribe<NpcImpatientEvent>(HandleNpcImpatient);
            EventManager.Subscribe<ReleaseNpcFromSecondaryQueueEvent>(HandleReleaseFromSecondaryQueue);
            EventManager.Subscribe<NpcStartedTransactionEvent>(HandleTransactionStarted);
            EventManager.Subscribe<NpcTransactionCompletedEvent>(HandleTransactionCompleted);

            // Subscribe to the temporary Emote trigger event
            EventManager.Subscribe<TriggerNpcEmoteEvent>(HandleTriggerEmote);

            // Subscribe to interruption events (Phase 5)
            // EventManager.Subscribe<NpcAttackedEvent>(HandleNpcAttacked);
            // EventManager.Subscribe<NpcInteractedEvent>(HandleNpcInteracted);

            Debug.Log($"{gameObject.name}: NpcStateMachineRunner subscribed to events.");
        }

        private void OnDisable()
        {
            // --- Unsubscribe from Events ---
            EventManager.Unsubscribe<NpcImpatientEvent>(HandleNpcImpatient);
            EventManager.Unsubscribe<ReleaseNpcFromSecondaryQueueEvent>(HandleReleaseFromSecondaryQueue);
            EventManager.Unsubscribe<NpcStartedTransactionEvent>(HandleTransactionStarted);
            EventManager.Unsubscribe<NpcTransactionCompletedEvent>(HandleTransactionCompleted);

            // Unsubscribe from the temporary Emote trigger event
            EventManager.Unsubscribe<TriggerNpcEmoteEvent>(HandleTriggerEmote);

            // Unsubscribe from interruption events
            // EventManager.Unsubscribe<NpcAttackedEvent>(HandleNpcAttacked);
            // EventManager.Unsubscribe<NpcInteractedEvent>(HandleNpcInteracted);

            // Ensure any active state logic coroutine is stopped when disabled
            StopManagedStateCoroutine(activeStateCoroutine);
            activeStateCoroutine = null;

            // Call OnExit for the current state if the object is suddenly disabled while active
            // Need a way to get the CustomerState enum from the NpcStateSO for this check, or rely on SO type/a dedicated ID
            // For now, let's assume the SO has a public getter for its HandledState (as in the old components)
            // or use the GetStateSO map in reverse (less efficient). Let's add a HandledState getter to NpcStateSO.
            if (currentState != null && currentState.HandledState != CustomerState.ReturningToPool) // Assuming NpcStateSO.HandledState exists
            {
                 // Update context fields just before calling OnExit
                 _stateContext.Manager = Manager; // Ensure Manager reference is up-to-date
                 _stateContext.CachedCashRegister = CachedCashRegister;
                 _stateContext.CurrentTargetLocation = CurrentTargetLocation;
                 _stateContext.AssignedQueueSpotIndex = AssignedQueueSpotIndex;

                currentState.OnExit(_stateContext);
                currentState = null;
            }


            Debug.Log($"{gameObject.name}: NpcStateMachineRunner unsubscribed from events.");
        }

        private void Update()
        {
            // Call the OnUpdate method of the current state SO, passing the context
            if (currentState != null)
            {
                 // Update context fields that state might need or could change
                 _stateContext.Manager = Manager; // Should be set in Initialize, but safety
                 _stateContext.CachedCashRegister = CachedCashRegister; // Updated by Waiting state
                 _stateContext.CurrentTargetLocation = CurrentTargetLocation; // Updated by Entering/Browse/Exiting/Queue states
                 _stateContext.AssignedQueueSpotIndex = AssignedQueueSpotIndex; // Updated by Manager/MoveToQueueSpot

                 currentState.OnUpdate(_stateContext); // Pass the context

                 // --- Animation Update (Remains here, uses Handler) ---
                 if (MovementHandler != null && MovementHandler.Agent != null && AnimationHandler != null)
                 {
                      float speed = MovementHandler.Agent.velocity.magnitude;
                      AnimationHandler.SetSpeed(speed);
                 }

                 // REVISED: Put the check in the Runner's Update.
                 if (MovementHandler != null && MovementHandler.Agent != null && MovementHandler.IsAtDestination())
                 {
                     // Check if the current state *cares* about reaching a destination.
                     // Queue states, Entering, MovingToRegister, Exiting care.
                     // Initializing, Browse (waits in coroutine), Waiting, Transaction, Returning don't directly use IsAtDestination in Update.
                     // Let's add an abstract bool property `CheckForDestinationArrival` to NpcStateSO?
                     // Or just check state type directly for now (temporary).
                     CustomerState currentCustomerState = GetStateSO(CustomerState.Queue) == currentState ? CustomerState.Queue :
                                                          (GetStateSO(CustomerState.SecondaryQueue) == currentState ? CustomerState.SecondaryQueue :
                                                           (GetStateSO(CustomerState.Entering) == currentState ? CustomerState.Entering :
                                                            (GetStateSO(CustomerState.MovingToRegister) == currentState ? CustomerState.MovingToRegister :
                                                             (GetStateSO(CustomerState.Exiting) == currentState ? CustomerState.Exiting : CustomerState.Inactive))));

                     if (currentCustomerState == CustomerState.Queue ||
                         currentCustomerState == CustomerState.SecondaryQueue ||
                         currentCustomerState == CustomerState.Entering ||
                         currentCustomerState == CustomerState.MovingToRegister ||
                         currentCustomerState == CustomerState.Exiting)
                     {
                          // Stop movement and call the state SO's OnReachedDestination method
                           MovementHandler.StopMoving(); // Ensure stopped
                           currentState.OnReachedDestination(_stateContext); // Call new virtual method
                     }
                 }
                 // --------------------------------------------------------------
            }
        }


        /// <summary>
        /// Called by the CustomerManager to initialize the NPC state machine.
        /// </summary>
        public void Initialize(CustomerManager manager, Vector3 startPosition)
        {
            this.Manager = manager;
             // Update Manager reference in the cached context
             _stateContext.Manager = this.Manager;

            ResetNPCData();

            if (MovementHandler != null && MovementHandler.Agent != null)
            {
                 if (MovementHandler.Warp(startPosition))
                 {
                     Debug.Log($"NpcStateMachineRunner ({gameObject.name}): Warped to {startPosition} using MovementHandler.");
                 }
                 else
                 {
                     Debug.LogWarning($"NpcStateMachineRunner ({gameObject.name}): Failed to Warp to {startPosition} using MovementHandler. Is the position on the NavMesh? Setting state to ReturningToPool.", this);
                     TransitionToState(GetStateSO(CustomerState.ReturningToPool));
                     return;
                 }
            }
            else
            {
                 Debug.LogError($"NpcStateMachineRunner ({gameObject.name}): MovementHandler or Agent is null during Initialize!", this);
                 TransitionToState(GetStateSO(CustomerState.ReturningToPool));
                 return;
            }

            // Start the state machine in the Initializing state
            TransitionToState(GetStateSO(CustomerState.Initializing));

            Debug.Log($"NpcStateMachineRunner ({gameObject.name}): Initialized at {startPosition}.");
        }

        /// <summary>
        /// Resets NPC-specific data fields managed by the Runner.
        /// Called during Initialize.
        /// </summary>
        private void ResetNPCData()
        {
            CurrentTargetLocation = null;
            CachedCashRegister = null;
            AssignedQueueSpotIndex = -1;

             // Update context fields
             _stateContext.CurrentTargetLocation = CurrentTargetLocation;
             _stateContext.CachedCashRegister = CachedCashRegister;
             _stateContext.AssignedQueueSpotIndex = AssignedQueueSpotIndex;


            Shopper?.Reset();

            Debug.Log($"NpcStateMachineRunner ({gameObject.name}): NPC data reset.");
        }

        /// <summary>
        /// Transitions the state machine to a new state.
        /// </summary>
        /// <param name="nextState">The State Scriptable Object to transition to.</param>
        public void TransitionToState(NpcStateSO nextState)
        {
            if (nextState == null)
            {
                 Debug.LogError($"NpcStateMachineRunner ({gameObject.name}): Attempted to transition to a null state! Attempting fallback to ReturningToPool.", this);
                 NpcStateSO returningState = GetStateSO(CustomerState.ReturningToPool);
                 if (returningState != null && currentState != returningState)
                 {
                     TransitionToState(returningState);
                 }
                 else if (returningState == null)
                 {
                     Debug.LogError($"NpcStateMachineRunner ({gameObject.name}): ReturningToPool State SO is also missing! Cannot transition to a safe state. Publishing NpcReturningToPoolEvent directly.", this);
                     EventManager.Publish(new NpcReturningToPoolEvent(this.gameObject));
                     enabled = false;
                 }
                 return;
            }

            if (currentState == nextState) { return; }

            Debug.Log($"NpcStateMachineRunner ({gameObject.name}): <color=cyan>Transitioning from {(currentState != null ? currentState.name : "NULL")} to {nextState.name}</color>", this);

            previousState = currentState;

            // 1. Perform exit logic on the current state (if any)
            if (currentState != null)
            {
                 StopManagedStateCoroutine(activeStateCoroutine);
                 activeStateCoroutine = null;

                 currentState.OnExit(_stateContext);
            }

            // 2. Update the current state variable
            currentState = nextState;

            // 3. Perform entry logic on the new state
            // --- Call OnEnter passing the context ---
            _stateContext.Manager = Manager;
            _stateContext.CachedCashRegister = CachedCashRegister;
            _stateContext.CurrentTargetLocation = CurrentTargetLocation;
            _stateContext.AssignedQueueSpotIndex = AssignedQueueSpotIndex;
            // Update Runner reference in context (already done in Awake, but safety)
            _stateContext.Runner = this;

            currentState.OnEnter(_stateContext);
        }

        /// <summary>
        /// Allows a State SO (via context) to start a coroutine managed by this Runner.
        /// </summary>
        public Coroutine StartManagedStateCoroutine(IEnumerator routine)
        {
            if (routine == null) return null;
            // Coroutines must be started on a MonoBehaviour instance. This Runner is that instance.
            return StartCoroutine(routine);
        }

        /// <summary>
        /// Allows a State SO (via context) to stop a managed coroutine.
        /// </summary>
        public void StopManagedStateCoroutine(Coroutine routine)
        {
            if (routine != null) StopCoroutine(routine);
        }

        /// <summary>
        /// Public getter for the current state SO.
        /// </summary>
        public NpcStateSO GetCurrentState()
        {
            return currentState;
        }

        /// <summary>
        /// Public getter for the previous state SO.
        /// </summary>
        public NpcStateSO GetPreviousState()
        {
             return previousState;
        }


        /// <summary>
        /// Gets a state SO by its Enum key (e.g., CustomerState.Idle, CustomerState.Combat, etc.)
        /// Looks up in the compiled list of available states.
        /// </summary>
        /// <param name="stateEnum">The Enum key of the state to retrieve.</param>
        /// <returns>The NpcStateSO asset, or null if not found.</returns>
        public NpcStateSO GetStateSO(Enum stateEnum) // <-- UPDATE SIGNATURE AND LOGIC
        {
             if (stateEnum == null) return null; // Handle null input

             // Look up in the compiled availableStates dictionary
             if (availableStates != null && availableStates.TryGetValue(stateEnum, out NpcStateSO stateSO))
             {
                  return stateSO;
             }
             // Use stateEnum.GetType().Name and stateEnum.ToString() for logging
             Debug.LogError($"NpcStateMachineRunner ({gameObject.name}): No State SO found in compiled states for Enum '{stateEnum.GetType().Name}.{stateEnum.ToString()}'!", this);
             // Return a safe fallback if the requested state is missing
             // Try returning the generic ReturningToPool state
             NpcStateSO returningStateFallback = null;
             if (availableStates != null)
             {
                  // Need to find the ReturningToPool state using its actual Enum key
                  // For Phase 3, we know it's CustomerState.ReturningToPool
                  availableStates.TryGetValue(CustomerState.ReturningToPool, out returningStateFallback);
             }

             if (returningStateFallback != null && !stateEnum.Equals(CustomerState.ReturningToPool)) // Avoid infinite fallback loop
             {
                  Debug.LogWarning($"NpcStateMachineRunner ({gameObject.name}): Returning '{returningStateFallback.name}' as fallback for missing state '{stateEnum.GetType().Name}.{stateEnum.ToString()}'.", this);
                  return returningStateFallback;
             }

             Debug.LogError($"NpcStateMachineRunner ({gameObject.name}): No fallback state (ReturningToPool) found either! Cannot provide a safe state.", this);
             return null; // Cannot provide a safe state
        }
        
        /// <summary>
        /// Determines the primary starting state for this NPC based on its configured types.
        /// Called by the generic InitializingSO.
        /// </summary>
        public NpcStateSO GetPrimaryStartingStateSO()
        {
             // --- Logic using primaryStartingStateEnumKey/Type (Phase 4) ---
             if (string.IsNullOrEmpty(primaryStartingStateEnumKey) || string.IsNullOrEmpty(primaryStartingStateEnumType))
             {
                  Debug.LogError($"NpcStateMachineRunner ({gameObject.name}): Primary Starting State key or type is not configured!", this);
                  // Fallback to Idle if possible? Or Returning? Let's try Idle if available.
                  NpcStateSO idleStateFallback = null;
                  if (availableStates != null) availableStates.TryGetValue(CustomerState.Idle, out idleStateFallback); // Assumes Idle is CustomerState.Idle

                  if (idleStateFallback != null)
                  {                       Debug.LogWarning($"NpcStateMachineRunner ({gameObject.name}): No primary start state configured, returning Idle as fallback.", this);
                       return idleStateFallback;
                  }

                  Debug.LogError($"NpcStateMachineRunner ({gameObject.name}): No primary start state configured and Idle fallback not available!", this);
                  return GetStateSO(CustomerState.ReturningToPool); // Final fallback
             }

             try
             {
                  // Get the Type of the primary start enum
                  Type enumType = Type.GetType(primaryStartingStateEnumType);
                  if (enumType == null || !enumType.IsEnum)
                  {
                       Debug.LogError($"NpcStateMachineRunner ({gameObject.name}): Primary Starting State Enum Type '{primaryStartingStateEnumType}' is invalid!", this);
                       return GetStateSO(CustomerState.ReturningToPool); // Fallback
                  }

                  // Parse the string key into the actual Enum value
                  Enum startEnum = (Enum)Enum.Parse(enumType, primaryStartingStateEnumKey);

                  // Look up the state SO using the generic GetStateSO
                  NpcStateSO startState = GetStateSO(startEnum);

                  if (startState == null)
                  {
                       Debug.LogError($"NpcStateMachineRunner ({gameObject.name}): Primary Starting State SO not found for key '{primaryStartingStateEnumKey}' of type '{primaryStartingStateEnumType}'!", this);
                       return GetStateSO(CustomerState.ReturningToPool); // Fallback
                  }

                  return startState; // Return the found starting state SO
             }
             catch (Exception e)
             {
                  Debug.LogError($"NpcStateMachineRunner ({gameObject.name}): Error parsing Primary Starting State config: {e}", this);
                  return GetStateSO(CustomerState.ReturningToPool); // Fallback
             }
             // --------------------------------------------------------------
        }

        // --- Event Handlers (Called by EventManager subscriptions) ---
        
        // Add a temporary event handler to demonstrate transitioning to the Emoting state (Phase 3)
        // This will be replaced by Phase 5's interruption logic.
        private void HandleTriggerEmote(TriggerNpcEmoteEvent eventArgs) // Need to define TriggerNpcEmoteEvent
        {
             if (eventArgs.NpcObject == this.gameObject)
             {
                  Debug.Log($"{gameObject.name}: Runner handling TriggerNpcEmoteEvent. Transitioning to Emoting.");
                  // Transition to the Emoting state using the SO
                  TransitionToState(GetStateSO(CustomerState.Emoting));
             }
        }

        private void HandleNpcImpatient(NpcImpatientEvent eventArgs)
        {
            if (eventArgs.NpcObject == this.gameObject)
            {
                Debug.Log($"{gameObject.name}: Runner handling NpcImpatientEvent from state {eventArgs.State}. Transitioning to Exiting.");
                TransitionToState(GetStateSO(CustomerState.Exiting));
            }
        }

        private void HandleReleaseFromSecondaryQueue(ReleaseNpcFromSecondaryQueueEvent eventArgs)
        {
             if (eventArgs.NpcObject == this.gameObject)
             {
                  Debug.Log($"{gameObject.name}: Runner handling ReleaseNpcFromSecondaryQueueEvent. Transitioning to Entering.");
                  TransitionToState(GetStateSO(CustomerState.Entering));
             }
        }

        private void HandleTransactionStarted(NpcStartedTransactionEvent eventArgs)
        {
             if (eventArgs.NpcObject == this.gameObject)
             {
                  Debug.Log($"{gameObject.name}: Runner handling NpcStartedTransactionEvent. Transitioning to TransactionActive.");
                  TransitionToState(GetStateSO(CustomerState.TransactionActive));
             }
        }

        private void HandleTransactionCompleted(NpcTransactionCompletedEvent eventArgs)
        {
             if (eventArgs.NpcObject == this.gameObject)
             {
                  Debug.Log($"{gameObject.name}: Runner handling NpcTransactionCompletedEvent. Transitioning to Exiting.");
                  TransitionToState(GetStateSO(CustomerState.Exiting));
             }
        }
        
        // --- Public methods called by Manager or external systems ---
        // These remain on the Runner as they are entry points for external control.

        public void GoToRegisterFromQueue()
        {
            Debug.Log($"{gameObject.name}: Runner received GoToRegisterFromQueue signal. Transitioning to MovingToRegister.");
            TransitionToState(GetStateSO(CustomerState.MovingToRegister));
        }

         public void StartTransaction()
         {
             Debug.Log($"NpcStateMachineRunner ({gameObject.name}): StartTransaction called. Transitioning to TransactionActive.");
             TransitionToState(GetStateSO(CustomerState.TransactionActive));
         }

         public void OnTransactionCompleted(float paymentReceived)
         {
             Debug.Log($"NpcStateMachineRunner ({gameObject.name}): OnTransactionCompleted called. Transitioning to Exiting.");
             // Payment is handled by Register/EconomyManager.
             TransitionToState(GetStateSO(CustomerState.Exiting));
         }
         
         public void MoveToQueueSpot(Transform nextSpotTransform, int newSpotIndex)
         {
             Debug.Log($"{gameObject.name}: Runner received MoveToQueueSpot signal for spot {newSpotIndex}.");

             // Check if the current state is actually a queue state before processing
             // Access current state via context.Runner.GetCurrentState() == this state SO
             // Or use the temporary enum map check
             CustomerState currentStateEnum = GetStateSO(CustomerState.Queue) == currentState ? CustomerState.Queue :
                                            (GetStateSO(CustomerState.SecondaryQueue) == currentState ? CustomerState.SecondaryQueue : CustomerState.Inactive);


             if (currentStateEnum == CustomerState.Queue || currentStateEnum == CustomerState.SecondaryQueue)
             {
                  // Update the assigned spot index and target location on the Runner/Context
                  AssignedQueueSpotIndex = newSpotIndex;
                  CurrentTargetLocation = new BrowseLocation { browsePoint = nextSpotTransform, inventory = null };


                  // Tell the Movement Handler to move to the new spot position
                  if (MovementHandler != null)
                  {
                       Debug.Log($"{gameObject.name}: Setting new queue spot destination to {nextSpotTransform.position} via MovementHandler.");
                       MovementHandler.SetDestination(nextSpotTransform.position);
                       // The Runner's Update loop will now detect arrival and call the state SO's OnReachedDestination.
                  }
                  else
                  {
                       Debug.LogError($"NpcStateMachineRunner ({gameObject.name}): MovementHandler is null! Cannot move to new queue spot.", this);
                       TransitionToState(GetStateSO(CustomerState.Exiting));
                  }
             }
             else
             {
                  Debug.LogWarning($"NpcStateMachineRunner ({gameObject.name}): Received MoveToQueueSpot signal but not in a Queue state ({currentStateEnum})!", this);
             }
         }

         // Public getter for Shopper's items (used by CashRegisterInteractable)
        // This method remains on the Runner for external access
        public List<(ItemDetails details, int quantity)> GetItemsToBuy()
        {
            if (Shopper != null)
            {
                return Shopper.GetItemsToBuy();
            }
            Debug.LogError($"NpcStateMachineRunner ({gameObject.name}): Shopper component is null! Cannot get items to buy.", this);
            return new List<(ItemDetails details, int quantity)>();
        }

        // --- Public methods/properties for external access if needed (can be added back if necessary) ---
        // For now, states access these via the context. External systems call the methods above.
        // If an external system needs to query current state, the Runner needs a public getter for currentState.HandledState or similar.
        // Let's add a public getter for the current *state SO* and the previous *state SO*.
         public NpcStateSO CurrentStateSO => currentState;
         public NpcStateSO PreviousStateSO => previousState;

        // If external systems need access to handlers, expose them here or via context
        // public NpcMovementHandler PublicMovementHandler => MovementHandler; // Example
    }
}