// --- START OF FILE NpcStateMachineRunner.cs ---

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
using Game.NPC.TI; // Needed for TiNpcData and TiNpcManager

namespace Game.NPC
{
     /// <summary>
     /// The central component on an NPC GameObject that runs the data-driven state machine.
     /// Executes the logic defined in NpcStateSO assets and manages state transitions.
     /// Creates and provides the NpcStateContext to executing states.
     /// Can represent a transient customer or an active True Identity NPC.
     /// </summary>
     [RequireComponent(typeof(NpcMovementHandler))] // Ensure handlers are attached
     [RequireComponent(typeof(NpcAnimationHandler))]
     [RequireComponent(typeof(CustomerShopper))] // Assuming CustomerShopper is a core handler for customer types
     [RequireComponent(typeof(Handlers.NpcEventHandler))] // Required event handler
     [RequireComponent(typeof(Handlers.NpcInterruptionHandler))] // Require the interruption handler
     [RequireComponent(typeof(Game.NPC.Handlers.NpcQueueHandler))] // <-- NEW: Require the Queue handler
     public class NpcStateMachineRunner : MonoBehaviour
     {
          // --- References to Handler Components (Accessed by State SOs via the Context) ---
          public NpcMovementHandler MovementHandler { get; private set; }
          public NpcAnimationHandler AnimationHandler { get; private set; }
          public CustomerShopper Shopper { get; private set; }

          // Reference to the interruption handler (will be used to populate context and reset)
          private NpcInterruptionHandler interruptionHandler;
          // --- Reference to the Queue handler ---
          private NpcQueueHandler queueHandler;
          public NpcQueueHandler QueueHandler { get { return queueHandler; } private set { queueHandler = value; } } // Make public for Context access
          
          // --- Public methods/properties for external access if needed ---
          public NpcStateSO CurrentStateSO => currentState;
          public NpcStateSO PreviousStateSO => previousState;


          // --- External References ---
          [HideInInspector] public CustomerManagement.CustomerManager Manager { get; private set; }
          public CashRegisterInteractable CachedCashRegister { get; internal set; }

          // --- State Management ---
          private NpcStateSO currentState;
          private NpcStateSO previousState;
          private Coroutine activeStateCoroutine; 

          // --- Master Dictionary of all available states for THIS NPC ---
          private Dictionary<Enum, NpcStateSO> availableStates;

          // --- NPC Type Definitions ---
          [Header("NPC Type Definitions")]
          [Tooltip("Assign the type definitions that define this NPC's states (e.g., General, Customer, TrueIdentity). Order matters for overrides.")]
          [SerializeField] private List<NpcTypeDefinitionSO> npcTypes;

          // --- Internal Data/State Needed by SOs (Managed by Runner, Accessed via Context) ---
          public BrowseLocation? CurrentTargetLocation { get; internal set; } = null;

          // --- Movement Arrival Flag ---
          public bool _hasReachedCurrentDestination;

          // --- State Context ---
          private NpcStateContext _stateContext;

          // --- Fallback State Configuration ---
          [Header("Fallback States")]
          [Tooltip("The Enum key for the state to transition to if a requested state is not found (e.g., GeneralState.ReturningToPool).")]
          [SerializeField] private string fallbackReturningStateEnumKey;
          [Tooltip("The Type name of the Enum key for the fallback Returning state (e.g., Game.NPC.GeneralState).")]
          [SerializeField] private string fallbackReturningStateEnumType;

          [Tooltip("The Enum key for the state to transition to if a requested state is not found AND the Returning fallback is not available/appropriate (e.g., GeneralState.Idle).")]
          [SerializeField] private string fallbackIdleStateEnumKey;
          [Tooltip("The Type name of the Enum key for the fallback Idle state (e.g., Game.NPC.GeneralState).")]
          [SerializeField] private string fallbackIdleStateEnumType;

          // --- True Identity (TI) Fields ---
          [Header("True Identity Settings (If applicable)")]
          [Tooltip("Is this Runner instance currently representing a True Identity NPC?")]
          public bool IsTrueIdentityNpc { get; private set; } = false;

          private TiNpcData tiData;

          public TiNpcData TiData
          {
               get => tiData;
               internal set => tiData = value;
          }

          private void Awake()
          {
               // --- Get Handler References ---
               MovementHandler = GetComponent<NpcMovementHandler>();
               AnimationHandler = GetComponent<NpcAnimationHandler>();
               Shopper = GetComponent<CustomerShopper>();
               interruptionHandler = GetComponent<NpcInterruptionHandler>();
               queueHandler = GetComponent<NpcQueueHandler>();

               if (MovementHandler == null || AnimationHandler == null || Shopper == null || interruptionHandler == null || QueueHandler == null) // <-- Add QueueHandler null check
               {
                    Debug.LogError($"NpcStateMachineRunner on {gameObject.name}: Missing required handler components! MovementHandler: {MovementHandler != null}, AnimationHandler: {AnimationHandler != null}, Shopper: {Shopper != null}, InterruptionHandler: {interruptionHandler != null}, QueueHandler: {QueueHandler != null}", this);
                    enabled = false;
               }

               // --- Load States from NPC Type Definitions ---
               LoadAvailableStates();

               if (availableStates == null || availableStates.Count == 0)
               {
                    Debug.LogError($"NpcStateMachineRunner ({gameObject.name}): No states loaded from assigned type definitions! Cannot function.", this);
                    enabled = false;
                    return;
               }
               Debug.Log($"NpcStateMachineRunner ({gameObject.name}): Loaded {availableStates.Count} states from {npcTypes?.Count ?? 0} type definitions.");

               // Ensure agent is disabled initially (handled by MovementHandler Awake)
               if (MovementHandler?.Agent != null) MovementHandler.Agent.enabled = false;

               // --- Initialize the State Context struct (partially) ---
               // Handlers are set here. Manager and InterruptionHandler are set later when available/in Update/TransitionToState.
               // QueueHandler will be set in Update/TransitionToState before context is passed.
               _stateContext = new NpcStateContext
               {
                    MovementHandler = MovementHandler,
                    AnimationHandler = AnimationHandler,
                    Shopper = Shopper,
                    NpcObject = this.gameObject,
                    Runner = this,
                    QueueHandler = queueHandler
               };

               _hasReachedCurrentDestination = true;

               Debug.Log($"{gameObject.name}: NpcStateMachineRunner Awake completed.");
          }

          private void LoadAvailableStates()
          {
               availableStates = new Dictionary<Enum, NpcStateSO>();
               if (npcTypes != null)
               {
                    foreach (var typeDef in npcTypes)
                    {
                         if (typeDef != null)
                         {
                              var typeStates = typeDef.GetAllStates();
                              foreach (var pair in typeStates)
                              {
                                   if (pair.Key != null && pair.Value != null)
                                   {
                                        Type keyEnumType = pair.Key.GetType();
                                        Type soHandledEnumType = pair.Value.HandledState.GetType();
                                        if (keyEnumType != soHandledEnumType)
                                        {
                                             Debug.LogError($"NpcStateMachineRunner ({gameObject.name}): Enum Type mismatch for state '{pair.Key}' in type definition '{typeDef.name}'! Expected '{keyEnumType.Name}', assigned State SO '{pair.Value.name}' has HandledState type '{soHandledEnumType.Name}'. State ignored.", this);
                                             continue;
                                        }
                                        availableStates[pair.Key] = pair.Value;
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
          }

          private void OnDisable()
          {
               // Stop active state coroutine managed by the Runner
               StopManagedStateCoroutine(activeStateCoroutine);
               activeStateCoroutine = null;

               // Call OnExit for the current state if it exists (only for transient NPCs shutting down)
               // For TI NPCs, OnExit is called by TransitionToState(ReturningToPool) just before Deactivate().
               if (currentState != null && !IsTrueIdentityNpc)
               {
                    // Ensure context is populated before calling OnExit
                    _stateContext.Manager = Manager; // Populate Manager
                    _stateContext.CurrentTargetLocation = CurrentTargetLocation;
                    _stateContext.Runner = this;
                    _stateContext.InterruptionHandler = interruptionHandler;
                    _stateContext.QueueHandler = QueueHandler;
                    currentState.OnExit(_stateContext);
               }
               else if (currentState != null && IsTrueIdentityNpc)
               {
                    // For TI NPCs, OnExit is called by TransitionToState(ReturningToPool) just before Deactivate().
                    // If OnDisable happens BEFORE that transition (e.g., scene unloaded), we should *not* call OnExit here.
                    currentState = null; // Clear currentState on unexpected disable for TI
               }

               // Reset transient data managed by the Runner, which includes resetting the interruption handler AND queue handler
               ResetRunnerTransientData();
               Debug.Log($"{gameObject.name}: NpcStateMachineRunner OnDisable completed. State machine cleanup done.");
          }


          private void Update()
          {
               if (currentState != null)
               {
                    // Populate context with current data before passing to state methods
                    _stateContext.Manager = Manager; // Populate Manager
                    _stateContext.CurrentTargetLocation = CurrentTargetLocation;
                    _stateContext.Runner = this;
                    _stateContext.InterruptionHandler = interruptionHandler;
                    _stateContext.QueueHandler = QueueHandler;

                    if (MovementHandler != null && MovementHandler.Agent != null && AnimationHandler != null)
                    {
                         float speed = (MovementHandler.Agent.enabled) ? MovementHandler.Agent.velocity.magnitude : 0f;
                         AnimationHandler.SetSpeed(speed);
                    }

                    if (currentState.CheckMovementArrival &&
                        MovementHandler != null &&
                        MovementHandler.Agent != null &&
                        MovementHandler.Agent.isActiveAndEnabled &&
                        !MovementHandler.Agent.pathPending &&
                        MovementHandler.IsAtDestination() &&
                        !_hasReachedCurrentDestination)
                    {
                         Debug.Log($"{gameObject.name}: Reached destination in state {currentState.name} (detected by Runner). Stopping and calling OnReachedDestination.");
                         MovementHandler.StopMoving();

                         _hasReachedCurrentDestination = true;

                         // Re-populate context just in case Update loop took time
                         _stateContext.Manager = Manager; // Populate Manager
                         _stateContext.CurrentTargetLocation = CurrentTargetLocation;
                         _stateContext.Runner = this;
                         _stateContext.InterruptionHandler = interruptionHandler;
                         _stateContext.QueueHandler = QueueHandler;

                         currentState.OnReachedDestination(_stateContext);
                    }

                     // Re-populate context just in case Update loop took time
                    _stateContext.Manager = Manager; // Populate Manager
                    _stateContext.CurrentTargetLocation = CurrentTargetLocation;
                    _stateContext.Runner = this;
                    _stateContext.InterruptionHandler = interruptionHandler;
                    _stateContext.QueueHandler = QueueHandler;
                    currentState.OnUpdate(_stateContext);
               }
          }


          /// <summary>
          /// Called by the CustomerManager to initialize a TRANSIENT NPC state machine from the pool.
          /// </summary>
          public void Initialize(CustomerManager manager, Vector3 startPosition)
          {
               if (IsTrueIdentityNpc)
               {
                    Debug.LogError($"NpcStateMachineRunner ({gameObject.name}): Initialize called on a TI NPC! Use Activate() instead. Self-disabling.", this);
                    enabled = false;
                    return;
               }

               this.Manager = manager;
               // Context Manager will be set in Update/TransitionToState before use

               ResetRunnerTransientData(); // Resets interruption handler and queue handler
               queueHandler?.Initialize(this.Manager);

               if (MovementHandler != null && MovementHandler.Agent != null)
               {
                    if (MovementHandler.Warp(startPosition))
                    {
                         Debug.Log($"NpcStateMachineRunner ({gameObject.name}): Warped to {startPosition} using MovementHandler.");
                    }
                    else
                    {
                         Debug.LogWarning($"NpcStateMachineRunner ({gameObject.name}): Failed to Warp to {startPosition} using MovementHandler during transient Initialize! Is the position on the NavMesh? Setting state to ReturningToPool.", this);
                         TransitionToState(GetStateSO(GeneralState.ReturningToPool));
                         return;
                    }
               }
               else
               {
                    Debug.LogError($"NpcStateMachineRunner ({gameObject.name}): MovementHandler or Agent is null during transient Initialize!", this);
                    TransitionToState(GetStateSO(GeneralState.ReturningToPool));
                    return;
               }

               // Start the state machine in the Initializing state for transient NPCs
               TransitionToState(GetStateSO(GeneralState.Initializing));

               Debug.Log($"NpcStateMachineRunner ({gameObject.name}): Transient NPC Initialized at {startPosition}.");
          }

          /// <summary>
          /// PHASE 2: Called by the TiNpcManager to activate a TRUE IDENTITY NPC.
          /// Configures the Runner and GameObject based on the persistent data.
          /// </summary>
          public void Activate(TiNpcData tiData, CustomerManagement.CustomerManager customerManager) // Full namespace for CustomerManager
          {
               if (tiData == null)
               {
                    Debug.LogError($"NpcStateMachineRunner ({gameObject.name}): Activate called with null TiNpcData! Self-disabling.", this);
                    enabled = false;
                    return;
               }

               Debug.Log($"NpcStateMachineRunner ({gameObject.name}): Activating TI NPC '{tiData.Id}'. Loading state and position.", this);

               IsTrueIdentityNpc = true;
               TiData = tiData;
               this.Manager = customerManager;

               // Context Manager will be set in Update/TransitionToState before use
               Debug.Log($"DEBUG Runner Activate ({gameObject.name}): IsTrueIdentityNpc={IsTrueIdentityNpc}, TiData is null={ (TiData == null) }, TiData ID={TiData?.Id}", this);

               ResetRunnerTransientData(); // Resets interruption handler and queue handler
               queueHandler?.Initialize(this.Manager);

               // Apply persistent position and rotation from TiData
               if (MovementHandler != null && MovementHandler.Agent != null)
               {
                    if (MovementHandler.Warp(tiData.CurrentWorldPosition))
                    {
                         Debug.Log($"NpcStateMachineRunner ({gameObject.name}): Warped to {tiData.CurrentWorldPosition} using MovementHandler from TiData.");
                         transform.rotation = tiData.CurrentWorldRotation;
                    }
                    else
                    {
                         Debug.LogWarning($"NpcStateMachineRunner ({gameObject.name}): Failed to Warp to {tiData.CurrentWorldPosition} using MovementHandler during TI Activate! Is the position on the NavMesh? Setting state to ReturningToPool (for pooling).", this);
                         TransitionToState(GetStateSO(GeneralState.ReturningToPool));
                         return;
                    }
               }
               else
               {
                    Debug.LogError($"NpcStateMachineRunner ({gameObject.name}): MovementHandler or Agent is null during TI Activate!", this);
                    TransitionToState(GetStateSO(GeneralState.ReturningToPool));
                    return;
               }

               // Determine the state to transition to based on TiData or TypeDefinition
               // Use GetPrimaryStartingStateSO - it handles loading from TiData state if available
               NpcStateSO startingState = GetPrimaryStartingStateSO();

               if (startingState != null)
               {
                    Debug.Log($"NpcStateMachineRunner ({gameObject.name}): Transitioning TI NPC '{tiData.Id}' to state '{startingState.name}'.", this);
                    TransitionToState(startingState);
               }
               else
               {
                    Debug.LogError($"NpcStateMachineRunner ({gameObject.name}): GetPrimaryStartingStateSO returned null for TI NPC '{tiData.Id}'. Cannot find any valid starting state. Transitioning to ReturningToPool (for pooling).", this);
                    TransitionToState(GetStateSO(GeneralState.ReturningToPool));
               }

               gameObject.SetActive(true);
               enabled = true;

               // Mark data as active (should be done by the activation trigger logic in TiNpcManager)
               // tiData.IsActiveGameObject = true; // TiNpcManager should manage this when calling Activate

               Debug.Log($"NpcStateMachineRunner ({gameObject.name}): TI NPC '{tiData.Id}' Activated.");
          }

          /// <summary>
          /// Called internally by the Runner before it is returned to the pool
          /// (triggered by TransitionToState(ReturningToPool) and Manager handling the event).
          /// Saves current Runner state back to the persistent TiNpcData.
          /// </summary>
          public void Deactivate()
          {
               if (!IsTrueIdentityNpc || TiData == null)
               {
                    Debug.LogWarning($"NpcStateMachineRunner ({gameObject.name}): Deactivate called on a non-TI NPC or one without TiData. Ignoring.", this);
                    return;
               }

               Debug.Log($"NpcStateMachineRunner ({gameObject.name}): Deactivating TI NPC '{TiData.Id}'. Saving state and position to TiData.", this);

               // Save current state back to TiData
               TiData.CurrentWorldPosition = transform.position;
               TiData.CurrentWorldRotation = transform.rotation;

               if (currentState != null && currentState.HandledState != null)
               {
                    TiData.SetCurrentState(currentState.HandledState);
               }
               else
               {
                    Debug.LogWarning($"NpcStateMachineRunner ({gameObject.name}): Current state is null or has null HandledState when deactivating TI NPC '{TiData.Id}'. Clearing state in TiData.", this);
                    TiData.SetCurrentState(null);
               }

               if (currentState != null) // Only attempt to save relevant data if there's a current state
               {
                    // Assume Runner.CurrentDestinationPosition holds the last successful move target position
                    // Save it if the current state is one that typically involves moving to a target
                    if (currentState.HandledState.Equals(TestState.Patrol) || currentState.HandledState.Equals(CustomerState.Exiting) ||
                         currentState.HandledState.Equals(CustomerState.Entering) || currentState.HandledState.Equals(CustomerState.MovingToRegister) ||
                         currentState.HandledState.Equals(CustomerState.Queue) || currentState.HandledState.Equals(CustomerState.SecondaryQueue)) // Added queue states, entering, moving to register as they have destinations
                    {
                         TiData.simulatedTargetPosition = CurrentDestinationPosition; // Save the last target
                         Debug.Log($"NpcStateMachineRunner ({gameObject.name}): Saved simulated target position {TiData.simulatedTargetPosition} to TiData on Deactivate (State: {currentState.HandledState}).", this);
                    }
                    else // For states without a continuous move target, clear the simulated target
                    {
                         TiData.simulatedTargetPosition = null;
                         Debug.Log($"NpcStateMachineRunner ({gameObject.name}): Cleared simulated target position on Deactivate (State: {currentState.HandledState}).", this);
                    }

                    // Saving simulated timer is harder to do generically. Simulation starts timers from 0 for simplicity.
                    TiData.simulatedStateTimer = 0f; // Reset simulation timer on deactivate
                     Debug.Log($"NpcStateMachineRunner ({gameObject.name}): Reset simulated state timer to 0 on Deactivate.", this);

               }
               else
               {
                    // If currentState is null, ensure simulation data is cleared/defaulted
                    TiData.simulatedTargetPosition = null;
                    TiData.simulatedStateTimer = 0f;
                    Debug.Log($"NpcStateMachineRunner ({gameObject.name}): Current state is null on Deactivate. Cleared simulated target and timer.", this);
               }

                    // Clear Runner's link to the persistent data and reset flag
                    TiData.IsActiveGameObject = false; // Mark the data record as inactive *before* pooling

                    Debug.Log($"DEBUG Runner Deactivate ({gameObject.name}): IsTrueIdentityNpc={IsTrueIdentityNpc}, TiData is null={ (TiData == null) }", this);

                    // Reset Runner's transient fields to default (Manager reference is kept)
               ResetRunnerTransientData(); // Ensures interruption handler and queue handler are reset
               }


        /// <summary>
        /// Resets NPC-specific TRANSIENT data fields managed by the Runner.
        /// Called during Initialize or Activate. Preserves TI data link if present.
        /// </summary>
        private void ResetRunnerTransientData()
          {
               CurrentTargetLocation = null;
               CurrentDestinationPosition = null; // Clear the last set destination position
               CachedCashRegister = null;
               _hasReachedCurrentDestination = true;

               if (!IsTrueIdentityNpc)
               {
                    Shopper?.Reset();
               }

               interruptionHandler?.Reset();
               queueHandler?.Reset();

               Debug.Log($"NpcStateMachineRunner ({gameObject.name}): Runner transient data reset, including interruption handler and queue handler."); // Updated log
          }


          /// <summary>
          /// Transitions the state machine to a new state.
          /// Handles OnExit, OnEnter.
          /// Includes fallback logic for missing states.
          /// Note: This method triggers Deactivate() via the pooling flow IF this is a TI NPC
          /// and the state is ReturningToPool.
          /// </summary>
          /// <param name="nextState">The State Scriptable Object to transition to.</param>
          public void TransitionToState(NpcStateSO nextState)
          {
               if (nextState == null)
               {
                    Debug.LogError($"NpcStateMachineRunner ({gameObject.name}): Attempted to transition to a null state! Attempting fallback.", this);
                    NpcStateSO fallbackState = GetStateSO(GeneralState.ReturningToPool);

                    if (fallbackState == null)
                    {
                         Debug.LogError($"NpcStateMachineRunner ({gameObject.name}): ReturningToPool fallback state is null! Attempting Idle fallback.", this);
                         fallbackState = GetStateSO(GeneralState.Idle);
                    }

                    if (fallbackState != null && currentState != fallbackState)
                    {
                         Debug.LogWarning($"NpcStateMachineRunner ({gameObject.name}): Transitioning to fallback state '{fallbackState.name}' for missing state.", this);
                         TransitionToState(fallbackState);
                    }
                    else
                    {
                         Debug.LogError($"NpcStateMachineRunner ({gameObject.name}): Fallback state is also null or already current! Cannot transition to a safe state. Self-disabling.", this);
                         enabled = false;
                    }
                    return;
               }

               if (currentState == nextState)
               {
                    Debug.Log($"DEBUG Runner TransitionToState ({gameObject.name}): Attempted to transition to current state '{currentState.name}'. Skipping transition logic.", this);
                    return;
               }

               Debug.Log($"NpcStateMachineRunner ({gameObject.name}): <color=cyan>Transitioning from {(currentState != null ? currentState.name : "NULL")} to {nextState.name}</color>", this);

               previousState = currentState;

               if (currentState != null)
               {
                    StopManagedStateCoroutine(activeStateCoroutine);
                    activeStateCoroutine = null;

                    // Populate context before calling OnExit
                    _stateContext.Manager = Manager;
                    _stateContext.CurrentTargetLocation = CurrentTargetLocation;
                    _stateContext.Runner = this;
                    _stateContext.InterruptionHandler = interruptionHandler;
                    _stateContext.QueueHandler = queueHandler;

                    currentState.OnExit(_stateContext);
               }

               // Handle TI Deactivation BEFORE changing currentState if going to ReturningToPool
               if (IsTrueIdentityNpc && nextState.HandledState != null && nextState.HandledState.Equals(GeneralState.ReturningToPool))
               {
                    Debug.Log($"NpcStateMachineRunner ({gameObject.name}): TI NPC transitioning to ReturningToPool. Calling Deactivate().", this);
                    Deactivate(); // Call Deactivate BEFORE changing currentState
               }

               // Set the new current state
               currentState = nextState;

               // Populate context before calling OnEnter
               _stateContext.Manager = Manager;
               _stateContext.CurrentTargetLocation = CurrentTargetLocation;
               _stateContext.Runner = this;
               _stateContext.InterruptionHandler = interruptionHandler;
               _stateContext.QueueHandler = queueHandler;


               // Call OnEnter for the new state
               currentState.OnEnter(_stateContext);
          }

          /// <summary>
          /// Overload for TransitionToState that takes System.Enum keys.
          /// </summary>
          public void TransitionToState(string stateEnumKey, string stateEnumType)
          {
               if (string.IsNullOrEmpty(stateEnumKey) || string.IsNullOrEmpty(stateEnumType))
               {
                    Debug.LogError($"NpcStateMachineRunner ({gameObject.name}): Attempted to transition using invalid string Enum key ('{stateEnumKey}') or type ('{stateEnumType}')! Attempting fallback.", this);
                    TransitionToState((NpcStateSO)null);
                    return;
               }

               System.Enum targetEnum = null;
               try
               {
                    Type type = Type.GetType(stateEnumType);
                    if (type != null && type.IsEnum)
                    {
                         targetEnum = (System.Enum)Enum.Parse(type, stateEnumKey);
                    }
                    else
                    {
                         Debug.LogError($"NpcStateMachineRunner ({gameObject.name}): Invalid Enum Type string '{stateEnumType}' provided for transition!", this);
                    }
               }
               catch (Exception e)
               {
                    Debug.LogError($"NpcStateMachineRunner ({gameObject.name}): Error parsing state enum key '{stateEnumKey}' of type '{stateEnumType}' for transition: {e.Message}", this);
               }

               NpcStateSO nextState = GetStateSO(targetEnum);

               TransitionToState(nextState);
          }


          /// <summary>
          /// Allows a State SO (via context) to start a coroutine managed by this Runner.
          /// </summary>
          public Coroutine StartManagedStateCoroutine(IEnumerator routine)
          {
               if (routine == null)
               {
                    Debug.LogWarning($"{gameObject.name}: Attempted to start a null coroutine.", this);
                    return null;
               }
               StopManagedStateCoroutine(activeStateCoroutine); // Stop any existing managed coroutine
               activeStateCoroutine = StartCoroutine(routine); // Start the new coroutine
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
               }
               // If the stopped routine was the currently active one, clear the reference
               if (activeStateCoroutine == routine)
               {
                    activeStateCoroutine = null;
               }
          }

          public NpcStateSO GetCurrentState()
          {
               return currentState;
          }

          public NpcStateSO GetPreviousState()
          {
               return previousState;
          }


          /// <summary>
          /// Gets a state SO by its Enum key. Includes configurable fallbacks.
          /// Also attempts to load state from TiData if IsTrueIdentityNpc is true.
          /// </summary>
          public NpcStateSO GetStateSO(Enum stateEnum)
          {
               if (stateEnum == null)
               {
                    Debug.LogError($"NpcStateMachineRunner ({gameObject.name}): Attempted to get state with a null Enum key!", this);
                    return null;
               }

               if (availableStates != null && availableStates.TryGetValue(stateEnum, out NpcStateSO stateSO))
               {
                    return stateSO;
               }

               Debug.LogError($"NpcStateMachineRunner ({gameObject.name}): State SO not found in compiled states for Enum '{stateEnum.GetType().Name}.{stateEnum.ToString()}'! Attempting fallbacks.", this);

               NpcStateSO returningStateFallback = null;
               if (!string.IsNullOrEmpty(fallbackReturningStateEnumKey) && !string.IsNullOrEmpty(fallbackReturningStateEnumType))
               {
                    try
                    {
                         Type enumType = Type.GetType(fallbackReturningStateEnumType);
                         if (enumType != null && enumType.IsEnum)
                         {
                              Enum fallbackEnum = (Enum)Enum.Parse(enumType, fallbackReturningStateEnumKey);
                              if (availableStates.TryGetValue(fallbackEnum, out returningStateFallback))
                              {
                                   // Only return fallback if it's not the *same* enum key we were trying to get (avoids infinite loop if fallback config is recursive)
                                   if (!stateEnum.Equals(fallbackEnum))
                                   {
                                        Debug.LogWarning($"NpcStateMachineRunner ({gameObject.name}): Returning '{returningStateFallback.name}' as Returning fallback for missing state '{stateEnum.GetType().Name}.{stateEnum.ToString()}'.", this);
                                        return returningStateFallback;
                                   } else {
                                        Debug.LogError($"NpcStateMachineRunner ({gameObject.name}): Configured Returning fallback enum key '{fallbackReturningStateEnumKey}' is the same as the requested key '{stateEnum.GetType().Name}.{stateEnum.ToString()}'! Recursive fallback configuration.", this);
                                   }
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


               NpcStateSO idleStateFallback = null;
               if (!string.IsNullOrEmpty(fallbackIdleStateEnumKey) && !string.IsNullOrEmpty(fallbackIdleStateEnumType))
               {
                    try
                    {
                         Type enumType = Type.GetType(fallbackIdleStateEnumType);
                         if (enumType != null && enumType.IsEnum)
                         {
                              Enum fallbackEnum = (Enum)Enum.Parse(enumType, fallbackIdleStateEnumKey);
                              if (availableStates.TryGetValue(fallbackEnum, out idleStateFallback))
                              {
                                   // Only return fallback if it's not the *same* enum key we were trying to get
                                   if (!stateEnum.Equals(fallbackEnum))
                                   {
                                        Debug.LogWarning($"NpcStateMachineRunner ({gameObject.name}): Returning '{idleStateFallback.name}' as Idle fallback for missing state '{stateEnum.GetType().Name}.{stateEnum.ToString()}'.", this);
                                        return idleStateFallback;
                                   } else {
                                         Debug.LogError($"NpcStateMachineRunner ({gameObject.name}): Configured Idle fallback enum key '{fallbackIdleStateEnumKey}' is the same as the requested key '{stateEnum.GetType().Name}.{stateEnum.ToString()}'! Recursive fallback configuration.", this);
                                    }
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


               Debug.LogError($"NpcStateMachineRunner ({gameObject.name}): All configured fallback states (Returning/Idle) failed or are missing. Cannot provide a safe state for missing '{stateEnum.GetType().Name}.{stateEnum.ToString()}'.", this);
               return null;
          }

          /// <summary>
          /// Determines the primary starting state for this NPC based on its configured types.
          /// For TI NPCs, attempts to load state from TiData first.
          /// </summary>
          public NpcStateSO GetPrimaryStartingStateSO()
          {
               // Check TiData state first for TI NPCs
               if (IsTrueIdentityNpc && TiData != null && TiData.CurrentStateEnum != null)
               {
                    // If TiData has a valid state saved, attempt to transition to that state.
                    // Use GetStateSO to ensure the state is actually available in the loaded TypeDefs
                    NpcStateSO savedState = GetStateSO(TiData.CurrentStateEnum);
                    if (savedState != null)
                    {
                         Debug.Log($"NpcStateMachineRunner ({gameObject.name}): Found valid saved state '{TiData.CurrentStateEnumKey}' in TiData for TI NPC '{TiData.Id}'. Using this as primary start state.", this);
                         return savedState; // Return the state loaded from data
                    }
                    else
                    {
                         Debug.LogWarning($"NpcStateMachineRunner ({gameObject.name}): Saved state '{TiData.CurrentStateEnumKey}' from TiData not found in compiled states for TI NPC '{TiData.Id}'. Falling back to Type Definition primary state.", this);
                         // Continue below to use the Type Definition primary starting state
                    }
               }


               // If not a TI NPC, or TiData/state is invalid/missing, use the Type Definition primary starting state.
               Enum startingStateEnum = null;
               NpcTypeDefinitionSO primaryTypeDef = null;

               if (npcTypes != null)
               {
                    foreach (var typeDef in npcTypes)
                    {
                         if (typeDef != null)
                         {
                              Enum parsedEnum = typeDef.ParsePrimaryStartingStateEnum();
                              if (parsedEnum != null)
                              {
                                   primaryTypeDef = typeDef;
                                   startingStateEnum = parsedEnum;
                                   break;
                              }
                         }
                    }
               }

               if (startingStateEnum != null)
               {
                    Debug.Log($"NpcStateMachineRunner ({gameObject.name}): Found primary starting state '{startingStateEnum.GetType().Name}.{startingStateEnum.ToString()}' defined in type '{primaryTypeDef.name}'. Looking up state SO.", this);
                    NpcStateSO startState = GetStateSO(startingStateEnum);

                    if (startState == null)
                    {
                         Debug.LogError($"NpcStateMachineRunner ({gameObject.name}): GetStateSO returned null for primary starting state '{startingStateEnum.GetType().Name}.{startingStateEnum.ToString()}'. Cannot provide a safe start state.", this);
                    }
                    return startState;
               }
               else
               {
                    Debug.LogError($"NpcStateMachineRunner ({gameObject.name}): No valid primary starting state configured in any assigned type definitions! Cannot start NPC state machine.", this);
                    NpcStateSO finalFallback = null;
                    if (availableStates != null) availableStates.TryGetValue(GeneralState.ReturningToPool, out finalFallback);

                    if (finalFallback != null)
                    {
                         Debug.LogWarning($"NpcStateMachineRunner ({gameObject.name}): No primary start state configured, attempting hardcoded ReturningToPool as final fallback.", this);
                         return finalFallback;
                    }

                    Debug.LogError($"NpcStateMachineRunner ({gameObject.name}): No primary start state configured and hardcoded ReturningToPool fallback not available either! Cannot start NPC.", this);
                    return null;
               }
          }

          // These methods are called by the CashRegisterInteractable OR Runner internal logic
          public void StartTransaction()
          {
               Debug.Log($"NpcStateMachineRunner ({gameObject.name}): StartTransaction called. Transitioning to TransactionActive.");
               TransitionToState(GetStateSO(CustomerState.TransactionActive));
          }

          public void OnTransactionCompleted(float paymentReceived)
          {
               Debug.Log($"NpcStateMachineRunner ({gameObject.name}): OnTransactionCompleted called. Transitioning to Exiting.");
               TransitionToState(GetStateSO(CustomerState.Exiting));
          }


          public List<(ItemDetails details, int quantity)> GetItemsToBuy()
          {
               if (Shopper != null)
               {
                    return Shopper.GetItemsToBuy();
               }
               Debug.LogError($"NpcStateMachineRunner ({gameObject.name}): Shopper component is null! Cannot get items to buy.", this);
               return new List<(ItemDetails details, int quantity)>();
          }

          /// <summary>
          /// The position the NavMeshAgent was last commanded to move to.
          /// Used for saving state for inactive simulation.
          /// </summary>
          public Vector3? CurrentDestinationPosition { get; private set; }
          // Method in NpcStateContext.MoveToDestination sets this field now.

          // Make method internal so NpcStateContext can set it
          internal void SetCurrentDestinationPosition(Vector3? position)
          {
              CurrentDestinationPosition = position;
          }
     }
}

// --- END OF FILE NpcStateMachineRunner.cs ---