// --- Updated NpcStateMachineRunner.cs (Including Fixes for OnReachedDestination Loop and CachedRegister Null) ---
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
    [RequireComponent(typeof(NpcMovementHandler))] // Ensure handlers are attached
    [RequireComponent(typeof(NpcAnimationHandler))]
    [RequireComponent(typeof(CustomerShopper))] // Assuming CustomerShopper is a core handler for customer types
    // CustomerAI also requires these, but redundant RequireComponent can be ok
    public class NpcStateMachineRunner : MonoBehaviour
    {
        // --- References to Handler Components (Accessed by State SOs via the Context) ---
        // These are automatically found in Awake
        public NpcMovementHandler MovementHandler { get; private set; }
        public NpcAnimationHandler AnimationHandler { get; private set; }
        public CustomerShopper Shopper { get; private set; } // Assumes CustomerShopper is general enough or this runner is for customers

        // --- External References (Provided by CustomerManager or found) ---
        [HideInInspector] public CustomerManager Manager { get; private set; } // Provided by Manager
        // Store cached references persistently on the Runner instance
        public CashRegisterInteractable CachedCashRegister { get; internal set; } // Cached (e.g., by Waiting state), now on Runner


        // --- State Management ---
        private NpcStateSO currentState;
        private NpcStateSO previousState;
        private Coroutine activeStateCoroutine; // Coroutine started by the current state

        private Stack<NpcStateSO> stateStack = new Stack<NpcStateSO>();

        // --- Master Dictionary of all available states for THIS NPC ---
        private Dictionary<Enum, NpcStateSO> availableStates;

        // --- NPC Type Definitions (Phase 4 - Assigned in Inspector) ---
        [Header("NPC Type Definitions")]
        [Tooltip("Assign the type definitions that define this NPC's states (e.g., General, Customer). Order matters for overrides.")]
        [SerializeField] private List<NpcTypeDefinitionSO> npcTypes;

        // --- Internal Data/State Needed by SOs (Managed by Runner, Accessed via Context) ---
        // Kept here for the Runner to manage/set, but states access via Context properties/methods.
        public BrowseLocation? CurrentTargetLocation { get; internal set; } = null;
        public int AssignedQueueSpotIndex { get; internal set; } = -1; // Used by Queue/SecondaryQueue states
        public GameObject InteractorObject { get; internal set; } // Used by Social/Combat states

        // --- Movement Arrival Flag (Fix for repeated OnReachedDestination) ---
        public bool _hasReachedCurrentDestination; // Tracks if arrival at the current destination has been processed

        // --- State Context (Reusable structure) ---
          private NpcStateContext _stateContext; // Cache the context to avoid recreating every frame

        // --- Fallback State Configuration (Phase 5.5) ---
        [Header("Fallback States")]
        [Tooltip("The Enum key for the state to transition to if a requested state is not found (e.g., GeneralState.ReturningToPool).")]
        [SerializeField] private string fallbackReturningStateEnumKey; // String key for Returning fallback
        [Tooltip("The Type name of the Enum key for the fallback Returning state (e.g., Game.NPC.GeneralState).")]
        [SerializeField] private string fallbackReturningStateEnumType; // Type name for Returning fallback

        [Tooltip("The Enum key for the state to transition to if a requested state is not found AND the Returning fallback is not available/appropriate (e.g., GeneralState.Idle).")]
        [SerializeField] private string fallbackIdleStateEnumKey; // String key for Idle fallback
        [Tooltip("The Type name of the Enum key for the fallback Idle state (e.g., Game.NPC.GeneralState).")]
        [SerializeField] private string fallbackIdleStateEnumType; // Type name for Idle fallback


        private void Awake()
          {
               // --- Get Handler References ---
               MovementHandler = GetComponent<NpcMovementHandler>();
               AnimationHandler = GetComponent<NpcAnimationHandler>();
               Shopper = GetComponent<CustomerShopper>(); // Assuming this runner is specifically for Customers

               if (MovementHandler == null || AnimationHandler == null || Shopper == null)
               {
                    Debug.LogError($"NpcStateMachineRunner on {gameObject.name}: Missing required handler components! MovementHandler: {MovementHandler != null}, AnimationHandler: {AnimationHandler != null}, Shopper: {Shopper != null}", this);
                    enabled = false;
               }

               // --- Load States from NPC Type Definitions ---
               availableStates = new Dictionary<Enum, NpcStateSO>();
               if (npcTypes != null)
               {
                    foreach (var typeDef in npcTypes)
                    {
                         if (typeDef != null)
                         {
                              var typeStates = typeDef.GetAllStates(); // Recursive call to get states from hierarchy
                              foreach (var pair in typeStates)
                              {
                                   if (pair.Key != null && pair.Value != null)
                                   {
                                        // Validate that the State SO's HandledState matches the dictionary key's enum type
                                        Type keyEnumType = pair.Key.GetType();
                                        Type soHandledEnumType = pair.Value.HandledState.GetType();
                                        if (keyEnumType != soHandledEnumType)
                                        {
                                             Debug.LogError($"NpcStateMachineRunner ({gameObject.name}): Enum Type mismatch for state '{pair.Key}' in type definition '{typeDef.name}'! Expected '{keyEnumType.Name}', assigned State SO '{pair.Value.name}' has HandledState type '{soHandledEnumType.Name}'. State ignored.", this);
                                             continue;
                                        }
                                        availableStates[pair.Key] = pair.Value; // Add or Overwrite
                                   }
                                   else
                                   {
                                        Debug.LogWarning($"NpcStateMachineRunner ({gameObject.name}): Skipping null state entry from type definition '{typeDef.name}'.", this);
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
                // Populate with references to handlers and the runner itself
               _stateContext = new NpcStateContext
               {
                    MovementHandler = MovementHandler,
                    AnimationHandler = AnimationHandler,
                    Shopper = Shopper,
                    Manager = null, // Manager is set in Initialize()
                    // CachedCashRegister is now a property reading from Runner.CachedCashRegister,
                    // so no need to set it here in the struct initialization.
                    NpcObject = this.gameObject,
                    Runner = this,
                    CurrentTargetLocation = null, // Set during navigation states
                    AssignedQueueSpotIndex = -1, // Set during queue states
                    InteractorObject = null, // Set during interaction states
               };

               // --- Initialize the movement arrival flag ---
               _hasReachedCurrentDestination = true; // Start as true, no destination set yet

               Debug.Log($"{gameObject.name}: NpcStateMachineRunner Awake completed.");
          }

        private void OnEnable()
        {
            // --- Subscribe to Events that trigger state changes ---
            EventManager.Subscribe<NpcImpatientEvent>(HandleNpcImpatient);
            EventManager.Subscribe<ReleaseNpcFromSecondaryQueueEvent>(HandleReleaseFromSecondaryQueue);
            EventManager.Subscribe<NpcStartedTransactionEvent>(HandleTransactionStarted);
            EventManager.Subscribe<NpcTransactionCompletedEvent>(HandleTransactionCompleted);

             // --- Subscribe to Interruption Trigger Events ---
            EventManager.Subscribe<NpcAttackedEvent>(HandleNpcAttacked);
            EventManager.Subscribe<NpcInteractedEvent>(HandleNpcInteracted);
            EventManager.Subscribe<TriggerNpcEmoteEvent>(HandleTriggerEmote);

            // --- Subscribe to Interruption Completion Events ---
            EventManager.Subscribe<NpcCombatEndedEvent>(HandleCombatEnded);
            EventManager.Subscribe<NpcInteractionEndedEvent>(HandleInteractionEnded);
            EventManager.Subscribe<NpcEmoteEndedEvent>(HandleEmoteEnded);

            Debug.Log($"{gameObject.name}: NpcStateMachineRunner subscribed to events.");
        }

        private void OnDisable()
        {
            // --- Unsubscribe from Events ---
            EventManager.Unsubscribe<NpcImpatientEvent>(HandleNpcImpatient);
            EventManager.Unsubscribe<ReleaseNpcFromSecondaryQueueEvent>(HandleReleaseFromSecondaryQueue);
            EventManager.Unsubscribe<NpcStartedTransactionEvent>(HandleTransactionStarted);
            EventManager.Unsubscribe<NpcTransactionCompletedEvent>(HandleTransactionCompleted);

            // --- Unsubscribe from Interruption Trigger Events ---
            EventManager.Unsubscribe<NpcAttackedEvent>(HandleNpcAttacked);
            EventManager.Unsubscribe<NpcInteractedEvent>(HandleNpcInteracted);
            EventManager.Unsubscribe<TriggerNpcEmoteEvent>(HandleTriggerEmote);

            // --- Unsubscribe from Interruption Completion Events ---
            EventManager.Unsubscribe<NpcCombatEndedEvent>(HandleCombatEnded);
            EventManager.Unsubscribe<NpcInteractionEndedEvent>(HandleInteractionEnded);
            EventManager.Unsubscribe<NpcEmoteEndedEvent>(HandleEmoteEnded);

            // Ensure any active state logic coroutine is stopped when disabled
            StopManagedStateCoroutine(activeStateCoroutine);
            activeStateCoroutine = null;

            // Call OnExit for the current state if the object is suddenly disabled while active
            // Ensure we don't call OnExit if already in ReturningToPool or if currentState is null
            NpcStateSO returningToPoolSO = GetStateSO(GeneralState.ReturningToPool); // Need to look this up
            if (currentState != null && returningToPoolSO != null && !currentState.HandledState.Equals(returningToPoolSO.HandledState))
            {
                // Populate context fields from Runner fields before calling OnExit
                 // Note: CachedCashRegister is now a property, no need to set it in the struct here
                _stateContext.Manager = Manager;
                _stateContext.CurrentTargetLocation = CurrentTargetLocation;
                _stateContext.AssignedQueueSpotIndex = AssignedQueueSpotIndex;
                _stateContext.Runner = this;
                _stateContext.InteractorObject = InteractorObject;

                currentState.OnExit(_stateContext);
                currentState = null;
            }

            // This is important to prevent stale states on the stack if the NPC is pooled/deactivated
            stateStack.Clear();
             // Reset Runner fields as well
             ResetNPCData();

            Debug.Log($"{gameObject.name}: NpcStateMachineRunner unsubscribed from events, cleared state stack, and reset data.");
        }

        private void Update()
        {
            // Call the OnUpdate method of the current state SO, passing the context
            if (currentState != null)
            {
                 // Update context fields that state might need or could change
                 // Note: CachedCashRegister is now a property, no need to set it in the struct here
                 _stateContext.Manager = Manager;
                 _stateContext.CurrentTargetLocation = CurrentTargetLocation;
                 _stateContext.AssignedQueueSpotIndex = AssignedQueueSpotIndex;
                 _stateContext.Runner = this;
                 _stateContext.InteractorObject = InteractorObject; // Update from Runner field


                 // --- Animation Update (Remains here, uses Handler) ---
                 if (MovementHandler != null && MovementHandler.Agent != null && AnimationHandler != null)
                 {
                      // Only update animation speed if the agent is enabled and has speed
                      float speed = (MovementHandler.Agent.enabled) ? MovementHandler.Agent.velocity.magnitude : 0f;
                      AnimationHandler.SetSpeed(speed);
                 }

                 // --- Movement/Arrival Logic (Uses CheckMovementArrival Flag and _hasReachedCurrentDestination Flag) ---
                 // If the current state has CheckMovementArrival true, check for destination arrival.
                 // Ensure IsAtDestination() is reliable (agent enabled, not path pending, velocity low or remaining distance small)
                 // Only call OnReachedDestination if we are at the destination AND we haven't processed this arrival yet.
                 if (currentState.CheckMovementArrival &&
                     MovementHandler != null &&
                     MovementHandler.Agent != null &&
                     MovementHandler.Agent.isActiveAndEnabled && // Ensure agent is active/enabled on the NavMesh
                     !MovementHandler.Agent.pathPending && // Ensure path calculation is done
                     MovementHandler.IsAtDestination() && // Use the handler's check
                     !_hasReachedCurrentDestination) // <-- Crucial flag check
                 {
                      Debug.Log($"{gameObject.name}: Reached destination in state {currentState.name} (detected by Runner). Stopping and calling OnReachedDestination.");
                      MovementHandler.StopMoving(); // Ensure stopped

                      _hasReachedCurrentDestination = true; // <-- Set the flag *immediately* to prevent repeated calls this frame/next

                      // Call the state SO's OnReachedDestination method
                      currentState.OnReachedDestination(_stateContext);
                 }
                 // --------------------------------------------------------------

                 // Always call OnUpdate *after* checking for arrival.
                 // OnUpdate might decide to transition, setting a new destination which resets _hasReachedCurrentDestination.
                 currentState.OnUpdate(_stateContext);
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

            ResetNPCData(); // Clears Runner fields and resets _hasReachedCurrentDestination to true

            // Attempt to warp the NPC to the start position.
            // This is the first 'movement' action, so the arrival flag will be true (correctly)
            // because Warp doesn't typically trigger a destination arrival event in the same way SetDestination does.
            if (MovementHandler != null && MovementHandler.Agent != null)
            {
                 // Warp requires the agent to be enabled temporarily
                 if (MovementHandler.Warp(startPosition))
                 {
                     Debug.Log($"NpcStateMachineRunner ({gameObject.name}): Warped to {startPosition} using MovementHandler.");
                     // After a successful warp, the NPC is instantly "at" this position, but we haven't *commanded*
                     // them to move *to* it using SetDestination. The _hasReachedCurrentDestination flag should remain true.
                     // The *first* SetDestination call *after* Warp is what should set the flag to false.
                 }
                 else
                 {
                     Debug.LogWarning($"NpcStateMachineRunner ({gameObject.name}): Failed to Warp to {startPosition} using MovementHandler. Is the position on the NavMesh? Setting state to ReturningToPool.", this);
                     TransitionToState(GetStateSO(GeneralState.ReturningToPool));
                     return;
                 }
            }
            else
            {
                 Debug.LogError($"NpcStateMachineRunner ({gameObject.name}): MovementHandler or Agent is null during Initialize!", this);
                 TransitionToState(GetStateSO(GeneralState.ReturningToPool));
                 return;
            }

            // Start the state machine in the Initializing state
            TransitionToState(GetStateSO(GeneralState.Initializing));

            Debug.Log($"NpcStateMachineRunner ({gameObject.name}): Initialized at {startPosition}.");
        }

        /// <summary>
        /// Resets NPC-specific data fields managed by the Runner.
        /// Called during Initialize or OnDisable.
        /// </summary>
        private void ResetNPCData()
        {
            CurrentTargetLocation = null;
            CachedCashRegister = null; // Clear the Runner's persistent cached reference
            AssignedQueueSpotIndex = -1;
            InteractorObject = null; // Clear any cached interactor

             // Reset the movement arrival flag
             _hasReachedCurrentDestination = true; // Set to true because no active movement is occurring

             // Update context fields from the reset Runner fields
            _stateContext.CurrentTargetLocation = CurrentTargetLocation;
             // _stateContext.CachedCashRegister = CachedCashRegister; // <-- REMOVED - Context property reads Runner field
             _stateContext.AssignedQueueSpotIndex = AssignedQueueSpotIndex;
             _stateContext.InteractorObject = InteractorObject;


            Shopper?.Reset(); // Reset shopper data (like inventory, impatience counters)

            Debug.Log($"NpcStateMachineRunner ({gameObject.name}): NPC data reset.");
        }

        /// <summary>
        /// Transitions the state machine to a new state.
        /// Handles OnExit, OnEnter, and potentially the state stack (in Phase 5).
        /// </summary>
        /// <param name="nextState">The State Scriptable Object to transition to.</param>
        public void TransitionToState(NpcStateSO nextState)
        {
            if (nextState == null)
            {
                 Debug.LogError($"NpcStateMachineRunner ({gameObject.name}): Attempted to transition to a null state! Attempting fallback.", this);
                 // Find a safe fallback state if the requested state is null
                 NpcStateSO fallbackState = GetStateSO(GeneralState.ReturningToPool) ?? GetStateSO(GeneralState.Idle);

                 if (fallbackState != null && currentState != fallbackState)
                 {
                     Debug.LogWarning($"NpcStateMachineRunner ({gameObject.name}): Transitioning to fallback state '{fallbackState.name}' for missing state.", this);
                     TransitionToState(fallbackState); // Recursive call with fallback
                 }
                 else if (fallbackState == null || currentState == fallbackState) // If fallback is null or we are already in the fallback state (infinite loop potential)
                 {
                     Debug.LogError($"NpcStateMachineRunner ({gameObject.name}): Fallback state is also null or already current! Cannot transition to a safe state. Publishing NpcReturningToPoolEvent directly.", this);
                     // As a last resort, signal returning to pool via event
                     EventManager.Publish(new NpcReturningToPoolEvent(this.gameObject));
                     enabled = false; // Disable the runner
                 }
                 return; // Exit this transition attempt
            }

            if (currentState == nextState)
            {
                // Debug.Log($"NpcStateMachineRunner ({gameObject.name}): Already in state {nextState.name}. Ignoring transition request.");
                return; // No transition needed if already in the target state
            }

            Debug.Log($"NpcStateMachineRunner ({gameObject.name}): <color=cyan>Transitioning from {(currentState != null ? currentState.name : "NULL")} to {nextState.name}</color>", this);

            previousState = currentState; // Store the previous state SO

            // 1. Perform exit logic on the current state (if any)
            if (currentState != null)
            {
                 // Stop any coroutine started by the state that is now exiting
                 StopManagedStateCoroutine(activeStateCoroutine);
                 activeStateCoroutine = null; // Clear the reference

                 // Populate context for OnExit - uses current Runner field values
                 // Note: CachedCashRegister is now a property reading from Runner.CachedCashRegister,
                 // so no need to set it in the struct here.
                 _stateContext.Manager = Manager;
                 _stateContext.CurrentTargetLocation = CurrentTargetLocation;
                 _stateContext.AssignedQueueSpotIndex = AssignedQueueSpotIndex;
                 _stateContext.Runner = this; // Self reference
                 _stateContext.InteractorObject = InteractorObject;


                 currentState.OnExit(_stateContext);
            }

            // 2. Update the current state variable
            currentState = nextState;

            // 3. Perform entry logic on the new state
            // Populate context for OnEnter - ensure it reflects state *after* exit and before new state logic
            // Note: CachedCashRegister is now a property reading from Runner.CachedCashRegister,
            // so no need to set it in the struct here.
            _stateContext.Manager = Manager;
            _stateContext.CurrentTargetLocation = CurrentTargetLocation; // May have been cleared/set in OnExit or will be set in OnEnter
            _stateContext.AssignedQueueSpotIndex = AssignedQueueSpotIndex; // May have been cleared/set in OnExit or will be set in OnEnter
            _stateContext.Runner = this; // Self reference
            _stateContext.InteractorObject = InteractorObject; // May have been cleared/set in OnExit or will be set in OnEnter


            currentState.OnEnter(_stateContext);

            // Note: State SO's OnEnter can start a coroutine via context.StartCoroutine(routine)
        }

        /// <summary>
        /// Allows a State SO (via context) to start a coroutine managed by this Runner.
        /// Stores the reference to ensure only one state coroutine runs at a time.
        /// </summary>
        public Coroutine StartManagedStateCoroutine(IEnumerator routine)
        {
            if (routine == null)
            {
                Debug.LogWarning($"{gameObject.name}: Attempted to start a null coroutine.", this);
                return null;
            }
            // Stop any currently running state coroutine before starting a new one
            StopManagedStateCoroutine(activeStateCoroutine);
            // Coroutines must be started on a MonoBehaviour instance. This Runner is that instance.
            activeStateCoroutine = StartCoroutine(routine);
            // Debug.Log($"{gameObject.name}: Started managed coroutine for state {currentState?.name ?? "NULL"}.");
            return activeStateCoroutine;
        }

        /// <summary>
        /// Allows a State SO (via context) to stop a managed coroutine.
        /// </summary>
        public void StopManagedStateCoroutine(Coroutine routine)
        {
            if (routine != null)
            {
                 StopCoroutine(routine);
                 // Debug.Log($"{gameObject.name}: Stopped managed coroutine.");
            }
            if (activeStateCoroutine == routine) // Clear reference if the stopped routine was the currently active one
            {
                activeStateCoroutine = null;
            }
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
        /// Gets a state SO by its Enum key (e.g., CustomerState.Idle, GeneralState.Combat, etc.)
        /// Looks up in the compiled list of available states. Includes configurable fallbacks.
        /// </summary>
        /// <param name="stateEnum">The Enum key of the state to retrieve.</param>
        /// <returns>The NpcStateSO asset, or null if not found or key is invalid and fallbacks fail.</returns>
        public NpcStateSO GetStateSO(Enum stateEnum) // Accepts generic System.Enum
        {
             if (stateEnum == null)
             {
                 Debug.LogError($"NpcStateMachineRunner ({gameObject.name}): Attempted to get state with a null Enum key!", this);
                 return null;
             }

             // Look up in the compiled availableStates dictionary
             if (availableStates != null && availableStates.TryGetValue(stateEnum, out NpcStateSO stateSO))
             {
                  return stateSO; // Found the state
             }

             // --- Fallback Logic (Uses configurable fallback keys) ---
             Debug.LogError($"NpcStateMachineRunner ({gameObject.name}): State SO not found in compiled states for Enum '{stateEnum.GetType().Name}.{stateEnum.ToString()}'! Attempting fallbacks.", this);

             // 1. Try the ReturningToPool fallback
             NpcStateSO returningStateFallback = null;
             if (!string.IsNullOrEmpty(fallbackReturningStateEnumKey) && !string.IsNullOrEmpty(fallbackReturningStateEnumType))
             {
                 try
                 {
                      Type enumType = Type.GetType(fallbackReturningStateEnumType);
                      if (enumType != null && enumType.IsEnum)
                      {
                           Enum fallbackEnum = (Enum)Enum.Parse(enumType, fallbackReturningStateEnumKey);
                            // Lookup fallback in availableStates
                           if (availableStates.TryGetValue(fallbackEnum, out returningStateFallback))
                           {
                                // Check if the requested state was already this fallback to avoid infinite loops
                                if (!stateEnum.Equals(fallbackEnum))
                                {
                                    Debug.LogWarning($"NpcStateMachineRunner ({gameObject.name}): Returning '{returningStateFallback.name}' as Returning fallback for missing state '{stateEnum.GetType().Name}.{stateEnum.ToString()}'.", this);
                                    return returningStateFallback;
                                }
                                // Else: Requested state was the fallback itself, and it's missing. Continue to next fallback.
                           }
                           // Else: Returning fallback enum key/type is configured but the SO isn't in availableStates. Continue.
                      }
                       else
                       {
                            Debug.LogError($"NpcStateMachineRunner ({gameObject.name}): Configured Returning fallback Enum Type '{fallbackReturningStateEnumType}' is invalid!", this);
                       }
                 }
                 catch (Exception e)
                 {
                      Debug.LogError($"NpcStateMachineRunner ({gameObject.name}): Error parsing Returning fallback config: {e}", this);
                 }
             }
             else
             {
                   // Debug.LogWarning($"NpcStateMachineRunner ({gameObject.name}): Returning fallback state is not configured.");
             }


             // 2. If Returning fallback didn't work, try the Idle fallback
             NpcStateSO idleStateFallback = null;
             if (!string.IsNullOrEmpty(fallbackIdleStateEnumKey) && !string.IsNullOrEmpty(fallbackIdleStateEnumType))
             {
                 try
                 {
                      Type enumType = Type.GetType(fallbackIdleStateEnumType);
                      if (enumType != null && enumType.IsEnum)
                      {
                           Enum fallbackEnum = (Enum)Enum.Parse(enumType, fallbackIdleStateEnumKey);
                           // Lookup fallback in availableStates
                           if (availableStates.TryGetValue(fallbackEnum, out idleStateFallback))
                           {
                                // Check if the requested state was already this fallback to avoid infinite loops
                                if (!stateEnum.Equals(fallbackEnum))
                                {
                                    Debug.LogWarning($"NpcStateMachineRunner ({gameObject.name}): Returning '{idleStateFallback.name}' as Idle fallback for missing state '{stateEnum.GetType().Name}.{stateEnum.ToString()}'.", this);
                                    return idleStateFallback;
                                }
                                // Else: Requested state was the fallback itself, and it's missing. Continue to final null return.
                           }
                           // Else: Idle fallback enum key/type is configured but the SO isn't in availableStates. Continue.
                      }
                       else
                       {
                            Debug.LogError($"NpcStateMachineRunner ({gameObject.name}): Configured Idle fallback Enum Type '{fallbackIdleStateEnumType}' is invalid!", this);
                       }
                 }
                 catch (Exception e)
                 {
                      Debug.LogError($"NpcStateMachineRunner ({gameObject.name}): Error parsing Idle fallback config: {e}", this);
                 }
             }
             else
             {
                   // Debug.LogWarning($"NpcStateMachineRunner ({gameObject.name}): Idle fallback state is not configured.");
             }


             // 3. If all configured fallbacks fail or were the missing state itself
             Debug.LogError($"NpcStateMachineRunner ({gameObject.name}): All configured fallback states (Returning/Idle) failed or are missing. Cannot provide a safe state for missing '{stateEnum.GetType().Name}.{stateEnum.ToString()}'.", this);
             return null; // Cannot provide a safe state
        }

        /// <summary>
        /// Determines the primary starting state for this NPC based on its configured types.
        /// Called by the generic InitializingSO.
        /// </summary>
        public NpcStateSO GetPrimaryStartingStateSO()
        {
             Enum startingStateEnum = null;
             NpcTypeDefinitionSO primaryTypeDef = null;

             if (npcTypes != null)
             {
                  foreach(var typeDef in npcTypes)
                  {
                       if (typeDef != null)
                       {
                            Enum parsedEnum = typeDef.ParsePrimaryStartingStateEnum(); // Use helper in TypeDef
                            if (parsedEnum != null)
                            {
                                 primaryTypeDef = typeDef;
                                 startingStateEnum = parsedEnum;
                                 break; // Use the first valid starting state found in the list
                            }
                       }
                  }
             }

             if (startingStateEnum != null)
             {
                  Debug.Log($"NpcStateMachineRunner ({gameObject.name}): Found primary starting state '{startingStateEnum.GetType().Name}.{startingStateEnum.ToString()}' defined in type '{primaryTypeDef.name}'. Looking up state SO.", this);
                  // Look up the state SO using the generic GetStateSO.
                  // GetStateSO will handle cases where the SO isn't found and return a fallback if possible.
                  NpcStateSO startState = GetStateSO(startingStateEnum);

                  if (startState == null)
                  {
                       // GetStateSO already logged an error if it couldn't find the SO *and* fallbacks failed.
                       Debug.LogError($"NpcStateMachineRunner ({gameObject.name}): GetStateSO returned null for primary starting state '{startingStateEnum.GetType().Name}.{startingStateEnum.ToString()}'. Cannot provide a safe start state.", this);
                  }
                   // Return whatever GetStateSO found (either the intended state, a fallback, or null).
                  return startState;
             }
             else
             {
                  Debug.LogError($"NpcStateMachineRunner ({gameObject.name}): No valid primary starting state configured in any assigned type definitions! Cannot start NPC state machine.", this);
                  // As a final, final fallback, try to get ReturningToPool directly if no primary state is configured at all.
                   NpcStateSO finalFallback = null;
                   if (availableStates != null) availableStates.TryGetValue(GeneralState.ReturningToPool, out finalFallback);

                   if (finalFallback != null)
                   {
                        Debug.LogWarning($"NpcStateMachineRunner ({gameObject.name}): No primary start state configured, attempting hardcoded ReturningToPool as final fallback.", this);
                        return finalFallback;
                   }

                   Debug.LogError($"NpcStateMachineRunner ({gameObject.name}): No primary start state configured and hardcoded ReturningToPool fallback not available either! Cannot start NPC.", this);
                  return null; // Cannot start
             }
        }

        // --- Event Handlers (Called by EventManager subscriptions) ---
        // These handlers transition the state machine based on external events.

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
                  // This NPC was released from the secondary queue, they should now attempt to enter the store again.
                  TransitionToState(GetStateSO(CustomerState.Entering));
             }
        }

        private void HandleTransactionStarted(NpcStartedTransactionEvent eventArgs)
        {
             if (eventArgs.NpcObject == this.gameObject)
             {
                  Debug.Log($"{gameObject.name}: Runner handling NpcStartedTransactionEvent. Transitioning to TransactionActive.");
                  // The register system signals the NPC to enter the transaction state.
                  TransitionToState(GetStateSO(CustomerState.TransactionActive));
             }
        }

        private void HandleTransactionCompleted(NpcTransactionCompletedEvent eventArgs)
        {
             if (eventArgs.NpcObject == this.gameObject)
             {
                  Debug.Log($"{gameObject.name}: Runner handling NpcTransactionCompletedEvent. Transitioning to Exiting.");
                  // The register/minigame system signals transaction is done. NPC leaves.
                  // The Exiting state is responsible for telling the register the spot is free.
                  TransitionToState(GetStateSO(CustomerState.Exiting));
             }
        }

        // --- Interruption Trigger Event Handlers ---
        // These handlers push the current state and transition to an interrupt state if interruptible.

        private void HandleNpcAttacked(NpcAttackedEvent eventArgs)
        {
            if (eventArgs.NpcObject == this.gameObject)
            {
                 Debug.Log($"{gameObject.name}: Runner handling NpcAttackedEvent.");
                 if (currentState != null && currentState.IsInterruptible)
                 {
                      Debug.Log($"{gameObject.name}: Current state '{currentState.name}' is interruptible. Pushing to stack and transitioning to Combat.");
                      stateStack.Push(currentState);
                      // Store the attacker object if needed by the Combat state
                      InteractorObject = eventArgs.AttackerObject; // Store on Runner

                      NpcStateSO combatState = GetStateSO(GeneralState.Combat);
                      if (combatState != null) TransitionToState(combatState);
                      else Debug.LogError($"NpcStateMachineRunner ({gameObject.name}): Combat state SO not found! Cannot transition to combat.", this);
                 }
                 else
                 {
                      Debug.Log($"{gameObject.name}: Current state '{currentState?.name ?? "NULL"}' is NOT interruptible. Ignoring Combat trigger.", this);
                 }
            }
        }

        private void HandleNpcInteracted(NpcInteractedEvent eventArgs)
        {
             if (eventArgs.NpcObject == this.gameObject)
             {
                 Debug.Log($"{gameObject.name}: Runner handling NpcInteractedEvent.");
                 if (currentState != null && currentState.IsInterruptible)
                 {
                      Debug.Log($"{gameObject.name}: Current state '{currentState.name}' is interruptible. Pushing to stack and transitioning to Social.");
                      stateStack.Push(currentState);
                      // Store the interactor object if needed by the Social state
                      InteractorObject = eventArgs.InteractorObject; // Store on Runner

                      NpcStateSO socialState = GetStateSO(GeneralState.Social);
                      if (socialState != null) TransitionToState(socialState);
                      else Debug.LogError($"NpcStateMachineRunner ({gameObject.name}): Social state SO not found! Cannot transition to social.", this);
                 }
                 else
                 {
                      Debug.Log($"{gameObject.name}: Current state '{currentState?.name ?? "NULL"}' is NOT interruptible. Ignoring Social trigger.", this);
                 }
             }
        }

        private void HandleTriggerEmote(TriggerNpcEmoteEvent eventArgs)
        {
             if (eventArgs.NpcObject == this.gameObject)
             {
                 Debug.Log($"{gameObject.name}: Runner handling TriggerNpcEmoteEvent.");
                 if (currentState != null && currentState.IsInterruptible)
                 {
                      Debug.Log($"{gameObject.name}: Current state '{currentState.name}' is interruptible. Pushing to stack and transitioning to Emoting.");
                      stateStack.Push(currentState);
                       // Optional: Store data related to the emote trigger if needed by the Emoting state

                      NpcStateSO emotingState = GetStateSO(GeneralState.Emoting);
                      if (emotingState != null) TransitionToState(emotingState);
                      else Debug.LogError($"NpcStateMachineRunner ({gameObject.name}): Emoting state SO not found! Cannot transition to emoting.", this);
                 }
                 else
                 {
                      Debug.Log($"{gameObject.name}: Current state '{currentState?.name ?? "NULL"}' is NOT interruptible. Ignoring Emote trigger.", this);
                 }
             }
        }


        // --- Interruption Completion Event Handlers ---
        // These handlers pop the state stack and return to the previous state.

        private void HandleCombatEnded(NpcCombatEndedEvent eventArgs)
        {
             if (eventArgs.NpcObject == this.gameObject)
             {
                  Debug.Log($"{gameObject.name}: Runner handling NpcCombatEndedEvent.");
                  // Clear the InteractorObject as combat has ended
                  InteractorObject = null;

                  // Check if the state stack has a state to return to
                  if (stateStack.Count > 0)
                  {
                       NpcStateSO prevState = stateStack.Pop(); // Get the state we were in before the interruption
                       Debug.Log($"{gameObject.name}: State stack not empty. Popping state '{prevState.name}' and transitioning back.");
                       TransitionToState(prevState); // Transition back to the state from the stack
                  }
                   else
                   {
                        Debug.LogWarning($"{gameObject.name}: NpcCombatEndedEvent received but state stack is empty! Transitioning to Idle/Fallback.", this);
                        // Fallback: Transition to Idle or a safe default state if stack is empty (shouldn't happen if interruptions work correctly)
                        NpcStateSO idleState = GetStateSO(GeneralState.Idle);
                        if (idleState != null) TransitionToState(idleState);
                        else TransitionToState(GetStateSO(GeneralState.ReturningToPool)); // Final fallback if Idle is missing
                   }
             }
        }

        private void HandleInteractionEnded(NpcInteractionEndedEvent eventArgs)
        {
             if (eventArgs.NpcObject == this.gameObject)
             {
                 Debug.Log($"{gameObject.name}: Runner handling NpcInteractionEndedEvent.");
                 // Clear the InteractorObject as interaction has ended
                 InteractorObject = null;

                 // Check if the state stack has a state to return to
                if (stateStack.Count > 0)
                {
                    NpcStateSO prevState = stateStack.Pop(); // Get the state we were in before the interruption
                    Debug.Log($"{gameObject.name}: State stack not empty. Popping state '{prevState.name}' and transitioning back.");
                    TransitionToState(prevState); // Transition back to the state from the stack
                }
                else
                {
                    Debug.LogWarning($"{gameObject.name}: NpcInteractionEndedEvent received but state stack is empty! Transitioning to Idle/Fallback.", this);
                    NpcStateSO idleState = GetStateSO(GeneralState.Idle);
                    if (idleState != null) TransitionToState(idleState);
                    else TransitionToState(GetStateSO(GeneralState.ReturningToPool));
                }
             }
        }

        private void HandleEmoteEnded(NpcEmoteEndedEvent eventArgs)
        {
             if (eventArgs.NpcObject == this.gameObject)
             {
                  Debug.Log($"{gameObject.name}: Runner handling NpcEmoteEndedEvent.");
                  // InteractorObject might or might not be set for emotes depending on how they are triggered. Clear it just in case.
                  // InteractorObject = null; // Optional: Clear if emotes are linked to an interactor

                  // Check if the state stack has a state to return to
                  if (stateStack.Count > 0)
                  {
                       NpcStateSO prevState = stateStack.Pop(); // Get the state we were in before the interruption
                       Debug.Log($"{gameObject.name}: State stack not empty. Popping state '{prevState.name}' and transitioning back.");
                       TransitionToState(prevState); // Transition back to the state from the stack
                  }
                   else
                   {
                        Debug.LogWarning($"{gameObject.name}: NpcEmoteEndedEvent received but state stack is empty! Transitioning to Idle/Fallback.", this);
                        NpcStateSO idleState = GetStateSO(GeneralState.Idle);
                       if (idleState != null) TransitionToState(idleState);
                       else TransitionToState(GetStateSO(GeneralState.ReturningToPool));
                   }
             }
        }

        // --- Public methods called by Manager or external systems ---
        // These remain on the Runner as they are entry points for external control.

        public void GoToRegisterFromQueue()
        {
            Debug.Log($"{gameObject.name}: Runner received GoToRegisterFromQueue signal. Transitioning to MovingToRegister.");
            TransitionToState(GetStateSO(CustomerState.MovingToRegister));
        }

         // This method seems redundant now that NpcTransactionCompletedEvent is handled
         // and leads directly to the Exiting state. If external systems *need* to manually
         // trigger the transaction active state, keep it. Otherwise, remove it
         public void StartTransaction()
         {
             Debug.Log($"NpcStateMachineRunner ({gameObject.name}): StartTransaction called. Transitioning to TransactionActive.");
             TransitionToState(GetStateSO(CustomerState.TransactionActive));
         }


         // This method receives the signal *from* the CashRegisterInteractable (or related system)
         // after a transaction is deemed complete. It triggers the transition to Exiting.
         // This is called by CashRegisterInteractable.OnMinigameCompleted (or similar).
         public void OnTransactionCompleted(float paymentReceived)
         {
             Debug.Log($"NpcStateMachineRunner ({gameObject.name}): OnTransactionCompleted called. Transitioning to Exiting.");
             // Payment handling is outside the scope of the state machine runner itself (e.g., in EconomyManager).
             // The eventargs.PaymentReceived exists but isn't used *here*.
             TransitionToState(GetStateSO(CustomerState.Exiting));
         }

        /// <summary>
        /// Called by the CustomerManager when the queue line moves up.
        /// Updates the NPC's target spot and starts movement towards it.
        /// </summary>
        /// <param name="nextSpotTransform">The Transform of the new queue spot.</param>
        /// <param name="newSpotIndex">The index of the new queue spot.</param>
         public void MoveToQueueSpot(Transform nextSpotTransform, int newSpotIndex)
         {
             Debug.Log($"{gameObject.name}: Runner received MoveToQueueSpot signal for spot {newSpotIndex}.");

             // Update the assigned spot index and target location on the Runner
             AssignedQueueSpotIndex = newSpotIndex;
             CurrentTargetLocation = new BrowseLocation { browsePoint = nextSpotTransform, inventory = null };

             // Check if the current state is appropriate for moving in a queue.
             // If not, this call is unexpected and should be ignored or handled with a warning/fallback.
             CustomerState currentStateEnum = CustomerState.Inactive; // Default to inactive
             if (currentState != null && availableStates.ContainsValue(currentState))
             {
                  // Find the Enum key for the current state SO
                  KeyValuePair<Enum, NpcStateSO> currentKVP = availableStates.FirstOrDefault(pair => pair.Value == currentState);
                   if (currentKVP.Key is CustomerState customerEnum)
                   {
                        currentStateEnum = customerEnum;
                   }
                   // Could add checks for other enum types if needed
             }


             if (currentStateEnum == CustomerState.Queue || currentStateEnum == CustomerState.SecondaryQueue)
             {
                  // Tell the Movement Handler to move to the new spot position using the context helper.
                  // The context helper calls MovementHandler.SetDestination AND resets the _hasReachedCurrentDestination flag.
                  if (_stateContext.MoveToDestination(nextSpotTransform.position))
                  {
                       Debug.Log($"{gameObject.name}: Successfully called MoveToDestination for new queue spot {newSpotIndex} ({nextSpotTransform.position}).");
                       // The Runner's Update loop will now detect arrival (when IsAtDestination becomes true AND _hasReachedCurrentDestination is false)
                       // and call the state SO's OnReachedDestination.
                  }
                  else
                  {
                       Debug.LogError($"NpcStateMachineRunner ({gameObject.name}): Failed to set destination for new queue spot {newSpotIndex}! Cannot move up. Transitioning to Exiting.", this);
                       TransitionToState(GetStateSO(CustomerState.Exiting)); // Fallback on movement failure
                  }
             }
             else
             {
                  Debug.LogWarning($"NpcStateMachineRunner ({gameObject.name}): Received MoveToQueueSpot signal but not in a Queue state ({currentStateEnum})! Current State SO: {currentState?.name ?? "NULL"}. Ignoring move command.", this);
             }
         }

         // Public getter for Shopper's items (used by CashRegisterInteractable)
        // This method remains on the Runner for external access by systems like the Register.
        public List<(ItemDetails details, int quantity)> GetItemsToBuy()
        {
            if (Shopper != null)
            {
                return Shopper.GetItemsToBuy();
            }
            Debug.LogError($"NpcStateMachineRunner ({gameObject.name}): Shopper component is null! Cannot get items to buy.", this);
            return new List<(ItemDetails details, int quantity)>();
        }

        // --- Public methods/properties for external access if needed ---
        // These are less common entry points but might be needed by debugging tools or specific systems.
        public NpcStateSO CurrentStateSO => currentState;
        public NpcStateSO PreviousStateSO => previousState;

        // Example if external systems needed direct handler access (use with caution)
        // public NpcMovementHandler PublicMovementHandler => MovementHandler;
    }
}