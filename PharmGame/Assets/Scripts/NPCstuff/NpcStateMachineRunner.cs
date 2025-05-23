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
        private Coroutine activeStateCoroutine;

        private Stack<NpcStateSO> stateStack = new Stack<NpcStateSO>();

        // --- Master Dictionary of all available states for THIS NPC ---
        private Dictionary<Enum, NpcStateSO> availableStates;

        // --- NPC Type Definitions (Phase 4 - Assigned in Inspector) ---
        [Header("NPC Type Definitions")]
        [Tooltip("Assign the type definitions that define this NPC's states (e.g., General, Customer). Order matters for overrides.")]
        [SerializeField] private List<NpcTypeDefinitionSO> npcTypes;

        // --- Internal Data/State Needed by SOs (Can be accessed via Context now) ---
        // Kept here for the Runner to manage/set, but states access via Context.
        public BrowseLocation? CurrentTargetLocation { get; internal set; } = null;
        public int AssignedQueueSpotIndex { get; internal set; } = -1;
        public GameObject InteractorObject { get; internal set; }

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
                // Populate from Runner's fields (some are null initially)
               _stateContext = new NpcStateContext
               {
                    MovementHandler = MovementHandler,
                    AnimationHandler = AnimationHandler,
                    Shopper = Shopper,
                    Manager = Manager, // Manager is set in Initialize()
                    CachedCashRegister = CachedCashRegister, // Will be null initially
                    NpcObject = this.gameObject,
                    Runner = this,
                    CurrentTargetLocation = CurrentTargetLocation, // Will be null initially
                    AssignedQueueSpotIndex = AssignedQueueSpotIndex, // Will be -1 initially
                    InteractorObject = InteractorObject, // Will be null initially
               };

               Debug.Log($"{gameObject.name}: NpcStateMachineRunner Awake completed.");
          }

        private void OnEnable()
        {
            // --- Subscribe to Events that trigger state changes ---
            EventManager.Subscribe<NpcImpatientEvent>(HandleNpcImpatient);
            EventManager.Subscribe<ReleaseNpcFromSecondaryQueueEvent>(HandleReleaseFromSecondaryQueue);
            EventManager.Subscribe<NpcStartedTransactionEvent>(HandleTransactionStarted);
            EventManager.Subscribe<NpcTransactionCompletedEvent>(HandleTransactionCompleted);

             // --- Subscribe to Interruption Trigger Events (Phase 5) ---
            EventManager.Subscribe<NpcAttackedEvent>(HandleNpcAttacked);
            EventManager.Subscribe<NpcInteractedEvent>(HandleNpcInteracted);
            EventManager.Subscribe<TriggerNpcEmoteEvent>(HandleTriggerEmote); 

            // --- Subscribe to Interruption Completion Events (Phase 5) ---
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

            // --- Unsubscribe from Interruption Trigger Events (Phase 5) ---
            EventManager.Unsubscribe<NpcAttackedEvent>(HandleNpcAttacked);
            EventManager.Unsubscribe<NpcInteractedEvent>(HandleNpcInteracted);
            EventManager.Unsubscribe<TriggerNpcEmoteEvent>(HandleTriggerEmote);

            // --- Unsubscribe from Interruption Completion Events (Phase 5) ---
            EventManager.Unsubscribe<NpcCombatEndedEvent>(HandleCombatEnded); 
            EventManager.Unsubscribe<NpcInteractionEndedEvent>(HandleInteractionEnded);
            EventManager.Unsubscribe<NpcEmoteEndedEvent>(HandleEmoteEnded);

            // Ensure any active state logic coroutine is stopped when disabled
            StopManagedStateCoroutine(activeStateCoroutine);
            activeStateCoroutine = null;

               // Call OnExit for the current state if the object is suddenly disabled while active
               // Check against CustomerState.ReturningToPool using HandledState
            NpcStateSO returningToPoolSO = GetStateSO(GeneralState.ReturningToPool);
            if (currentState != null && returningToPoolSO != null && !currentState.HandledState.Equals(returningToPoolSO.HandledState))
               {
                    // --- Populate context fields from Runner fields before calling OnExit ---
                    _stateContext.Manager = Manager;
                    _stateContext.CachedCashRegister = CachedCashRegister;
                    _stateContext.CurrentTargetLocation = CurrentTargetLocation;
                    _stateContext.AssignedQueueSpotIndex = AssignedQueueSpotIndex;
                    _stateContext.Runner = this;
                    _stateContext.InteractorObject = InteractorObject; // <-- Populate from Runner field

                    currentState.OnExit(_stateContext);
                    currentState = null;
               }

            // This is important to prevent stale states on the stack if the NPC is pooled/deactivated
            stateStack.Clear();

            Debug.Log($"{gameObject.name}: NpcStateMachineRunner unsubscribed from events and cleared state stack.");
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
                 _stateContext.Runner = this;
                 _stateContext.InteractorObject = InteractorObject;

                 currentState.OnUpdate(_stateContext); // Pass the context

                 // --- Animation Update (Remains here, uses Handler) ---
                 if (MovementHandler != null && MovementHandler.Agent != null && AnimationHandler != null)
                 {
                      float speed = MovementHandler.Agent.velocity.magnitude;
                      AnimationHandler.SetSpeed(speed);
                 }

                 // --- Movement/Arrival Logic (Uses CheckMovementArrival Flag - Phase 5.5) ---
                 // If the current state has CheckMovementArrival set to true, check for destination arrival.
                 if (currentState.CheckMovementArrival) // <-- Use the flag on the state SO
                 {
                      if (MovementHandler != null && MovementHandler.Agent != null && MovementHandler.IsAtDestination())
                      {
                           Debug.Log($"{gameObject.name}: Reached destination in state {currentState.name} (detected by Runner). Stopping and calling OnReachedDestination.");
                           MovementHandler.StopMoving(); // Ensure stopped
                           currentState.OnReachedDestination(_stateContext); // Call state SO's OnReachedDestination method
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
        /// Called during Initialize.
        /// </summary>
        private void ResetNPCData()
        {
            CurrentTargetLocation = null;
            CachedCashRegister = null;
            AssignedQueueSpotIndex = -1;
            // Reset InteractorObject when NPC is pooled/reinitialized
            // InteractorObject = null; // <-- ADD FIELD TO RUNNER IF MANAGED HERE

             // Update context fields
            _stateContext.CurrentTargetLocation = CurrentTargetLocation;
             _stateContext.CachedCashRegister = CachedCashRegister;
             _stateContext.AssignedQueueSpotIndex = AssignedQueueSpotIndex;
             _stateContext.InteractorObject = null;


            Shopper?.Reset();

            Debug.Log($"NpcStateMachineRunner ({gameObject.name}): NPC data reset.");
        }

        /// <summary>
        /// Transitions the state machine to a new state.
        /// This method will be updated in later substeps to handle the stack.
        /// </summary>
        /// <param name="nextState">The State Scriptable Object to transition to.</param>
        public void TransitionToState(NpcStateSO nextState)
        {
            if (nextState == null)
            {
                 Debug.LogError($"NpcStateMachineRunner ({gameObject.name}): Attempted to transition to a null state! Attempting fallback to ReturningToPool.", this);
                 NpcStateSO returningState = GetStateSO(GeneralState.ReturningToPool);
                 if (returningState != null && currentState != returningState)
                 {
                     TransitionToState(returningState);
                 }
                 else if (returningState == null)
                 {
                     Debug.LogError($"NpcStateMachineRunner ({gameObject.name}): ReturningToPool State SO is also null! Cannot transition to a safe state. Publishing NpcReturningToPoolEvent directly.", this);
                     EventManager.Publish(new NpcReturningToPoolEvent(this.gameObject));
                     enabled = false;
                 }
                 return;
            }

            if (currentState == nextState) { return; }

            Debug.Log($"NpcStateMachineRunner ({gameObject.name}): <color=cyan>Transitioning from {(currentState != null ? currentState.name : "NULL")} to {nextState.name}</color>", this);

            previousState = currentState; // Store the previous state SO

            // 1. Perform exit logic on the current state (if any)
            // This part remains the same for now. Interrupt logic will modify this later.
            if (currentState != null)
            {
                 StopManagedStateCoroutine(activeStateCoroutine);
                 activeStateCoroutine = null;

                 _stateContext.Manager = Manager;
                 _stateContext.CachedCashRegister = CachedCashRegister;
                 _stateContext.CurrentTargetLocation = CurrentTargetLocation;
                 _stateContext.AssignedQueueSpotIndex = AssignedQueueSpotIndex;
                 _stateContext.Runner = this;
                 currentState.OnExit(_stateContext);
            }

            // 2. Update the current state variable
            currentState = nextState;

            // 3. Perform entry logic on the new state
            _stateContext.Manager = Manager;
            _stateContext.CachedCashRegister = CachedCashRegister;
            _stateContext.CurrentTargetLocation = CurrentTargetLocation;
            _stateContext.AssignedQueueSpotIndex = AssignedQueueSpotIndex;
            _stateContext.Runner = this;

            currentState.OnEnter(_stateContext);

            // Note: State SO's OnEnter can start a coroutine via context.StartCoroutine(routine)
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
        /// Gets a state SO by its Enum key (e.g., CustomerState.Idle, GeneralState.Combat, etc.)
        /// Looks up in the compiled list of available states. Includes configurable fallbacks.
        /// </summary>
        /// <param name="stateEnum">The Enum key of the state to retrieve.</param>
        /// <returns>The NpcStateSO asset, or null if not found or key is invalid and fallbacks fail.</returns>
        public NpcStateSO GetStateSO(Enum stateEnum) // Accepts generic Enum
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
             // Log that the primary lookup failed
             Debug.LogError($"NpcStateMachineRunner ({gameObject.name}): State SO not found in compiled states for Enum '{stateEnum.GetType().Name}.{stateEnum.ToString()}'!", this);

             // --- Fallback Logic (Uses configurable fallback keys - Phase 5.5) ---

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
                           availableStates.TryGetValue(fallbackEnum, out returningStateFallback);

                            // Check if the requested state is the returning state itself to avoid infinite loop
                           if (returningStateFallback != null && !stateEnum.Equals(fallbackEnum)) // Use .Equals for Enum comparison
                           {
                                Debug.LogWarning($"NpcStateMachineRunner ({gameObject.name}): Returning '{returningStateFallback.name}' as Returning fallback for missing state '{stateEnum.GetType().Name}.{stateEnum.ToString()}'.", this);
                                return returningStateFallback;
                           }
                           else if (returningStateFallback != null && stateEnum.Equals(fallbackEnum))
                           {
                                // The requested state *was* the returning state, but it wasn't found. This is an error.
                                Debug.LogError($"NpcStateMachineRunner ({gameObject.name}): Requested state was Returning fallback itself, but it's not in available states! Cannot provide a safe state.", this);
                                return null; // Cannot fallback to a missing fallback
                           }
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
                  // Log if Returning fallback is not configured
                   Debug.LogWarning($"NpcStateMachineRunner ({gameObject.name}): Returning fallback state is not configured.", this);
             }


             // 2. If Returning fallback didn't work or wasn't the requested state, try the Idle fallback
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
                           availableStates.TryGetValue(fallbackEnum, out idleStateFallback);

                            // Check if the requested state is the idle state itself to avoid infinite loop
                           if (idleStateFallback != null && !stateEnum.Equals(fallbackEnum)) // Use .Equals for Enum comparison
                           {
                                Debug.LogWarning($"NpcStateMachineRunner ({gameObject.name}): Returning '{idleStateFallback.name}' as Idle fallback for missing state '{stateEnum.GetType().Name}.{stateEnum.ToString()}'.", this);
                                return idleStateFallback;
                           }
                           else if (idleStateFallback != null && stateEnum.Equals(fallbackEnum))
                           {
                                // The requested state *was* the idle state, but it wasn't found. This is an error.
                                Debug.LogError($"NpcStateMachineRunner ({gameObject.name}): Requested state was Idle fallback itself, but it's not in available states! Cannot provide a safe state.", this);
                                return null; // Cannot fallback to a missing fallback
                           }
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
                   // Log if Idle fallback is not configured
                   Debug.LogWarning($"NpcStateMachineRunner ({gameObject.name}): Idle fallback state is not configured.", this);
             }


             // 3. If all fallbacks fail or are the requested state that wasn't found
             Debug.LogError($"NpcStateMachineRunner ({gameObject.name}): All fallback states (Returning/Idle) failed or are missing. Cannot provide a safe state for missing '{stateEnum.GetType().Name}.{stateEnum.ToString()}'.", this);
             return null; // Cannot provide a safe state
        }
        
        /// <summary>
        /// Determines the primary starting state for this NPC based on its configured types.
        /// Called by the generic InitializingSO.
        /// </summary>
        public NpcStateSO GetPrimaryStartingStateSO()
        {
             // Looks for the starting state defined in the assigned type definitions.
             // Prioritizes the first type definition in the list that has a valid starting state config.

             Enum startingStateEnum = null;
             NpcTypeDefinitionSO primaryTypeDef = null; // Keep track of which type def defined it

             if (npcTypes != null)
             {
                  foreach(var typeDef in npcTypes)
                  {
                       if (typeDef != null)
                       {
                            Enum parsedEnum = typeDef.ParsePrimaryStartingStateEnum(); // Use helper in TypeDef
                            if (parsedEnum != null)
                            {
                                 // Found a valid starting state defined in this type definition
                                 primaryTypeDef = typeDef;
                                 startingStateEnum = parsedEnum;
                                 break; // Use the first valid starting state found
                            }
                       }
                  }
             }

             if (startingStateEnum != null)
             {
                  Debug.Log($"NpcStateMachineRunner ({gameObject.name}): Found primary starting state '{startingStateEnum.GetType().Name}.{startingStateEnum.ToString()}' defined in type '{primaryTypeDef.name}'. Looking up state SO.", this);
                  // Look up the state SO using the generic GetStateSO
                  NpcStateSO startState = GetStateSO(startingStateEnum); // Use the parsed Enum

                  // GetStateSO will log an error and return a fallback if the SO is not found in availableStates
                  if (startState != null)
                  {
                       // Ensure the found state is not one of the fallbacks if the original wasn't found
                       // GetStateSO handles the fallback return, so if startState is not null here,
                       // it's either the intended state or the Returning fallback.
                       // We should probably check if the returned state is *actually* the intended start state
                       // and not just a fallback.
                       if (startState.HandledState.Equals(startingStateEnum)) // Check if the returned SO's HandledState matches the requested enum
                       {
                           return startState; // Return the found starting state SO
                       }
                       else
                       {
                            // GetStateSO returned a fallback because the *intended* start state wasn't found in availableStates
                            Debug.LogError($"NpcStateMachineRunner ({gameObject.name}): Intended primary starting state SO for '{startingStateEnum.GetType().Name}.{startingStateEnum.ToString()}' was not found in loaded states. Returning GetStateSO's fallback instead.", this);
                            return startState; // Return the fallback provided by GetStateSO
                       }
                  }
                  else
                  {
                       // GetStateSO returned null, meaning even fallbacks failed. Error logged in GetStateSO.
                       Debug.LogError($"NpcStateMachineRunner ({gameObject.name}): GetStateSO returned null for primary starting state '{startingStateEnum.GetType().Name}.{startingStateEnum.ToString()}'. Cannot provide a safe start state.", this);
                       return null; // No safe state found
                  }
             }
             else
             {
                  Debug.LogError($"NpcStateMachineRunner ({gameObject.name}): No valid primary starting state configured in any assigned type definitions! Cannot start NPC state machine.", this);
                  // Log error and attempt to transition to ReturningToPool as a last resort
                  // This case should ideally not happen if the runner is configured reasonably.
                  // Let's try to get the Returning state SO directly as a final, final fallback
                   NpcStateSO finalFallback = null;
                   if (availableStates != null) availableStates.TryGetValue(GeneralState.ReturningToPool, out finalFallback); // Assumes Returning is CustomerState.ReturningToPool

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

        // --- Interruption Trigger Event Handlers (Phase 5) ---

        private void HandleNpcAttacked(NpcAttackedEvent eventArgs)
        {
            if (eventArgs.NpcObject == this.gameObject)
            {
                 Debug.Log($"{gameObject.name}: Runner handling NpcAttackedEvent.");
                 if (currentState != null && currentState.IsInterruptible)
                 {
                      Debug.Log($"{gameObject.name}: Current state '{currentState.name}' is interruptible. Pushing to stack and transitioning to Combat.");
                      stateStack.Push(currentState);
                      // Optional: Store the attacker object in the context/runner
                      // InteractorObject = eventArgs.AttackerObject; // Requires field on Runner

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
                      // --- Store the interactor object in the context/runner ---
                      _stateContext.InteractorObject = eventArgs.InteractorObject; // <-- Set InteractorObject in context
                      // InteractorObject = eventArgs.InteractorObject; // Or set a field on Runner and update context from it

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
                       // E.g., if eventArgs had an EmoteID field: _stateContext.EmoteID = eventArgs.EmoteID;

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


        // --- Interruption Completion Event Handlers (Phase 5) ---

        private void HandleCombatEnded(NpcCombatEndedEvent eventArgs)
        {
             if (eventArgs.NpcObject == this.gameObject)
             {
                  Debug.Log($"{gameObject.name}: Runner handling NpcCombatEndedEvent.");
                  // Check if the state stack has a state to return to
                  if (stateStack.Count > 0)
                  {
                       // Optional: Check if the current state is CombatStateSO for robustness
                       // If currentState.HandledState == CustomerState.Combat
                       NpcStateSO prevState = stateStack.Pop(); // --- POP PREVIOUS STATE FROM STACK ---
                       Debug.Log($"{gameObject.name}: State stack not empty. Popping state '{prevState.name}' and transitioning back.");
                       TransitionToState(prevState); // --- TRANSITION BACK TO PREVIOUS STATE ---
                  }
                   else
                   {
                        Debug.LogWarning($"{gameObject.name}: NpcCombatEndedEvent received but state stack is empty! Transitioning to Idle/Fallback.", this);
                        // Fallback: Transition to Idle or a safe default state if stack is empty
                        NpcStateSO idleState = GetStateSO(GeneralState.Idle);
                        if (idleState != null) TransitionToState(idleState);
                        else TransitionToState(GetStateSO(GeneralState.ReturningToPool)); // Final fallback
                   }
             }
        }

        private void HandleInteractionEnded(NpcInteractionEndedEvent eventArgs)
        {
             if (eventArgs.NpcObject == this.gameObject)
             {
                 Debug.Log($"{gameObject.name}: Runner handling NpcInteractionEndedEvent.");
                 _stateContext.InteractorObject = null;

                 // Check if the state stack has a state to return to
                if (stateStack.Count > 0)
                {
                    NpcStateSO prevState = stateStack.Pop(); // --- POP PREVIOUS STATE FROM STACK ---
                    Debug.Log($"{gameObject.name}: State stack not empty. Popping state '{prevState.name}' and transitioning back.");
                    TransitionToState(prevState); // --- TRANSITION BACK TO PREVIOUS STATE ---
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
                  // Check if the state stack has a state to return to
                  if (stateStack.Count > 0)
                  {
                       NpcStateSO prevState = stateStack.Pop(); // --- POP PREVIOUS STATE FROM STACK ---
                       Debug.Log($"{gameObject.name}: State stack not empty. Popping state '{prevState.name}' and transitioning back.");
                       TransitionToState(prevState); // --- TRANSITION BACK TO PREVIOUS STATE ---
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