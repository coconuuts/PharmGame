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
using Game.Spatial; // Needed for GridManager
using Game.Proximity; // Needed for ProximityManager
using Game.Navigation; // Needed for PathSO (although not directly used here, good practice if states reference it)

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
     [RequireComponent(typeof(Game.NPC.Handlers.NpcQueueHandler))] // Required the Queue handler
     [RequireComponent(typeof(Game.NPC.Handlers.NpcPathFollowingHandler))] // <-- NEW: Require the Path Following handler
     public class NpcStateMachineRunner : MonoBehaviour
     {
          // --- References to Handler Components (Accessed by State SOs via the Context) ---
          public NpcMovementHandler MovementHandler { get; private set; }
          public NpcAnimationHandler AnimationHandler { get; private set; }
          public CustomerShopper Shopper { get; private set; }

          // Reference to the interruption handler
          private NpcInterruptionHandler interruptionHandler;
          // Reference to the Queue handler
          private NpcQueueHandler queueHandler;
          public NpcQueueHandler QueueHandler { get { return queueHandler; } private set { queueHandler = value; } }

          // --- NEW: Reference to the Path Following handler ---
          private NpcPathFollowingHandler npcPathFollowingHandler;
          public NpcPathFollowingHandler PathFollowingHandler { get { return npcPathFollowingHandler; } private set { npcPathFollowingHandler = value; } }
          // --- END NEW ---


          // Reference to the TiNpcManager
          private TiNpcManager tiNpcManager;

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

          // --- Update Throttling Fields ---
          private float timeSinceLastUpdate = 0f;
          private float currentUpdateInterval = 0f; // 0 means update every frame (Full)
          private float normalUpdateInterval = 0f; // Stores the interval set by ProximityManager
          private const float FullUpdateInterval = 0f; // Update every frame
          [Tooltip("Update rate (in Hz) for NPCs in the Moderate proximity zone.")]
          [SerializeField] private float moderateUpdateRateHz = 8f; // Example: 8 updates per second
          private float moderateUpdateInterval => 1.0f / moderateUpdateRateHz;

          // --- Grid Position Tracking for Active NPCs ---
          private Vector3 lastGridPosition;
          private GridManager gridManager; // Need GridManager reference to get cell size
          private float timeSinceLastGridUpdate = 0f; // Separate timer for grid updates
          private const float GridUpdateCheckInterval = 0.5f; // Check grid position every 0.5 seconds


          private void Awake()
          {
               // --- Get Handler References ---
               MovementHandler = GetComponent<NpcMovementHandler>();
               AnimationHandler = GetComponent<NpcAnimationHandler>();
               Shopper = GetComponent<CustomerShopper>();
               interruptionHandler = GetComponent<NpcInterruptionHandler>();
               queueHandler = GetComponent<NpcQueueHandler>();
               npcPathFollowingHandler = GetComponent<NpcPathFollowingHandler>(); // <-- NEW: Get Path Following handler

               if (MovementHandler == null || AnimationHandler == null || Shopper == null || interruptionHandler == null || QueueHandler == null || PathFollowingHandler == null) // <-- Add PathFollowingHandler null check
               {
                    Debug.LogError($"NpcStateMachineRunner on {gameObject.name}: Missing required handler components! MovementHandler: {MovementHandler != null}, AnimationHandler: {AnimationHandler != null}, Shopper: {Shopper != null}, InterruptionHandler: {interruptionHandler != null}, QueueHandler: {QueueHandler != null}, PathFollowingHandler: {PathFollowingHandler != null}", this);
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
               // QueueHandler and PathFollowingHandler will be set in Update/TransitionToState before context is passed.
               _stateContext = new NpcStateContext
               {
                    MovementHandler = MovementHandler,
                    AnimationHandler = AnimationHandler,
                    Shopper = Shopper,
                    NpcObject = this.gameObject,
                    Runner = this,
                    QueueHandler = queueHandler, // <-- Set QueueHandler
                    PathFollowingHandler = npcPathFollowingHandler // <-- NEW: Set PathFollowingHandler
               };

               _hasReachedCurrentDestination = true;

               Debug.Log($"{gameObject.name}: NpcStateMachineRunner Awake completed.");
          }

          private void Start() // <-- Use Start for Manager singletons
          {
               // Get reference to TiNpcManager
               tiNpcManager = TiNpcManager.Instance;
               if (tiNpcManager == null)
               {
                    Debug.LogError($"NpcStateMachineRunner ({gameObject.name}): TiNpcManager instance not found! Cannot notify position changes for TI NPCs.", this);
               }

               // Get reference to GridManager
               gridManager = GridManager.Instance;
               if (gridManager == null)
               {
                    Debug.LogError($"NpcStateMachineRunner ({gameObject.name}): GridManager instance not found! Cannot track grid position for active NPCs.", this);
               }

               // Initialize lastGridPosition if this is a TI NPC being activated
               if (IsTrueIdentityNpc && TiData != null && gridManager != null)
               {
                   // Use the position loaded from TiData during Activate()
                   lastGridPosition = transform.position; // Initialize with current position after warp
                   // Note: The initial AddItem to grid is done by TiNpcManager.LoadDummyNpcData or ActivateTiNpc
               }

               // Initialize throttling state - ProximityManager will set the actual mode after activation
               timeSinceLastUpdate = 0f;
               currentUpdateInterval = FullUpdateInterval; // Default to full update initially
               normalUpdateInterval = FullUpdateInterval; // Default normal is also full update
               timeSinceLastGridUpdate = 0f;
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
                    _stateContext.PathFollowingHandler = PathFollowingHandler; // <-- NEW: Populate PathFollowingHandler
                    _stateContext.TiData = TiData; // <-- NEW: Populate TiData
                    currentState.OnExit(_stateContext);
               }
               else if (currentState != null && IsTrueIdentityNpc)
               {
                    // For TI NPCs, OnExit is called by TransitionToState(ReturningToPool) just before Deactivate().
                    // If OnDisable happens BEFORE that transition (e.g., scene unloaded), we should *not* call OnExit here.
                    currentState = null; // Clear currentState on unexpected disable for TI
               }

               // Reset transient data managed by the Runner, which includes resetting handlers
               ResetRunnerTransientData();
               Debug.Log($"{gameObject.name}: NpcStateMachineRunner OnDisable completed. State machine cleanup done.");
          }


          private void Update()
          {
               // --- Handle Update Throttling ---
               // Check if we should skip the main state update this frame based on the *current* interval
               if (currentUpdateInterval > FullUpdateInterval) // If throttled (including temporary overrides)
               {
                   timeSinceLastUpdate += Time.deltaTime;
                   if (timeSinceLastUpdate < currentUpdateInterval)
                   {
                       // Skip main state update logic this frame
                       CheckGridPositionUpdate(); // Still check grid position periodically regardless of state throttling
                       return; // Skip the rest of Update
                   }
                   // If we are here, it means the throttling timer has elapsed or we are in FullUpdateInterval mode.
                   // Use the accumulated timeSinceLastUpdate as the deltaTime for the tick.
                   float tickDeltaTime = timeSinceLastUpdate;
                   timeSinceLastUpdate = 0f; // Reset timer after using accumulated time
                   // --- END NEW ---

                   // --- Main State Update Logic (Runs if not throttled, or if throttling timer elapsed) ---

                   // Check grid position update (always called, but timer inside limits frequency)
                   CheckGridPositionUpdate();


                   if (currentState != null)
                   {
                        // Populate context with current data before passing to state methods
                        _stateContext.Manager = Manager; // Populate Manager
                        _stateContext.CurrentTargetLocation = CurrentTargetLocation;
                        _stateContext.Runner = this;
                        _stateContext.InterruptionHandler = interruptionHandler;
                        _stateContext.QueueHandler = QueueHandler;
                        _stateContext.PathFollowingHandler = PathFollowingHandler; // <-- NEW: Populate PathFollowingHandler
                        _stateContext.TiData = TiData; // <-- NEW: Populate TiData

                        // --- NEW: Call PathFollowingHandler TickMovement if following a path ---
                        if (PathFollowingHandler != null && PathFollowingHandler.IsFollowingPath)
                        {
                             // Call the handler's tick method with the potentially throttled deltaTime
                             PathFollowingHandler.TickMovement(tickDeltaTime); // Use the accumulated deltaTime
                        }
                        // --- END NEW ---


                        if (MovementHandler != null && MovementHandler.Agent != null && AnimationHandler != null)
                        {
                             // Only update animation speed if NavMeshAgent is enabled (standard movement)
                             // Path following handler will manage animation speed if needed during path movement
                             if (MovementHandler.IsAgentEnabled) // Use the new helper property
                             {
                                  float speed = MovementHandler.Agent.velocity.magnitude;
                                  AnimationHandler.SetSpeed(speed);
                             }
                             // Note: Animation speed during path following needs to be handled by the PathFollowingHandler or the state itself.
                        }

                        // --- Check for NavMesh Arrival (only if Agent is enabled and not following path) ---
                        // The Runner's _hasReachedCurrentDestination flag is only relevant for NavMesh movement.
                        // Path following arrival is handled by the PathFollowingHandler setting HasReachedEndOfPath.
                        if (MovementHandler != null && MovementHandler.IsAgentEnabled && !PathFollowingHandler.IsFollowingPath &&
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
                             _stateContext.PathFollowingHandler = PathFollowingHandler; // <-- NEW: Populate PathFollowingHandler
                             _stateContext.TiData = TiData; // <-- NEW: Populate TiData

                             currentState.OnReachedDestination(_stateContext);
                        }
                        // --- End NavMesh Arrival Check ---


                         // Re-populate context just in case Update loop took time
                        _stateContext.Manager = Manager; // Populate Manager
                        _stateContext.CurrentTargetLocation = CurrentTargetLocation;
                        _stateContext.Runner = this;
                        _stateContext.InterruptionHandler = interruptionHandler;
                        _stateContext.QueueHandler = QueueHandler;
                        _stateContext.PathFollowingHandler = PathFollowingHandler; // <-- NEW: Populate PathFollowingHandler
                        _stateContext.TiData = TiData; // <-- NEW: Populate TiData

                        // --- Call OnUpdate with the potentially throttled deltaTime ---
                        currentState.OnUpdate(_stateContext); // <-- This is the main state logic that gets throttled
                        // --- END Call OnUpdate ---
                   }
               }
               else // currentUpdateInterval is FullUpdateInterval (0)
               {
                   // Run every frame, deltaTime is Time.deltaTime
                   float tickDeltaTime = Time.deltaTime;
                   timeSinceLastUpdate = 0f; // Reset timer (not strictly needed for 0 interval, but clean)

                   // Check grid position update (always called)
                   CheckGridPositionUpdate();

                   if (currentState != null)
                   {
                        // Populate context with current data before passing to state methods
                        _stateContext.Manager = Manager; // Populate Manager
                        _stateContext.CurrentTargetLocation = CurrentTargetLocation;
                        _stateContext.Runner = this;
                        _stateContext.InterruptionHandler = interruptionHandler;
                        _stateContext.QueueHandler = QueueHandler;
                        _stateContext.PathFollowingHandler = PathFollowingHandler; // <-- NEW: Populate PathFollowingHandler
                        _stateContext.TiData = TiData; // <-- NEW: Populate TiData

                        // --- NEW: Call PathFollowingHandler TickMovement if following a path ---
                        if (PathFollowingHandler != null && PathFollowingHandler.IsFollowingPath)
                        {
                             // Call the handler's tick method with Time.deltaTime
                             PathFollowingHandler.TickMovement(tickDeltaTime); // Use Time.deltaTime
                        }
                        // --- END NEW ---

                        if (MovementHandler != null && MovementHandler.Agent != null && AnimationHandler != null)
                        {
                             // Only update animation speed if NavMeshAgent is enabled (standard movement)
                             if (MovementHandler.IsAgentEnabled) // Use the new helper property
                             {
                                  float speed = MovementHandler.Agent.velocity.magnitude;
                                  AnimationHandler.SetSpeed(speed);
                             }
                        }

                        // --- Check for NavMesh Arrival (only if Agent is enabled and not following path) ---
                        if (MovementHandler != null && MovementHandler.IsAgentEnabled && !PathFollowingHandler.IsFollowingPath &&
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
                             _stateContext.PathFollowingHandler = PathFollowingHandler; // <-- NEW: Populate PathFollowingHandler
                             _stateContext.TiData = TiData; // <-- NEW: Populate TiData

                             currentState.OnReachedDestination(_stateContext);
                        }
                        // --- End NavMesh Arrival Check ---

                         // Re-populate context just in case Update loop took time
                        _stateContext.Manager = Manager; // Populate Manager
                        _stateContext.CurrentTargetLocation = CurrentTargetLocation;
                        _stateContext.Runner = this;
                        _stateContext.InterruptionHandler = interruptionHandler;
                        _stateContext.QueueHandler = QueueHandler;
                        _stateContext.PathFollowingHandler = PathFollowingHandler; // <-- NEW: Populate PathFollowingHandler
                        _stateContext.TiData = TiData; // <-- NEW: Populate TiData

                        // --- Call OnUpdate with Time.deltaTime ---
                        currentState.OnUpdate(_stateContext); // <-- This is the main state logic
                        // --- END Call OnUpdate ---
                   }
               }
               // --- END Main State Update Logic ---
          }

          /// <summary>
          /// Helper method to check and notify GridManager of position changes for active TI NPCs.
          /// Runs periodically based on GridUpdateCheckInterval.
          /// </summary>
          private void CheckGridPositionUpdate()
          {
               if (IsTrueIdentityNpc && TiData != null && tiNpcManager != null && gridManager != null)
               {
                   timeSinceLastGridUpdate += Time.deltaTime;
                   if (timeSinceLastGridUpdate >= GridUpdateCheckInterval)
                   {
                       timeSinceLastGridUpdate -= GridUpdateCheckInterval;

                       // Only notify if the NPC has moved enough to potentially change grid cells
                       if ((transform.position - lastGridPosition).sqrMagnitude >= (gridManager.cellSize * gridManager.cellSize))
                       {
                           tiNpcManager.NotifyActiveNpcPositionChanged(TiData, lastGridPosition, transform.position);
                           lastGridPosition = transform.position; // Update last tracked position
                       }
                   }
               }
          }


          /// <summary>
          /// Called by the ProximityManager to set the standard update mode based on proximity zone.
          /// This normal mode is temporarily overridden during interruptions.
          /// </summary>
          /// <param name="zone">The current proximity zone of the NPC.</param>
          public void SetUpdateMode(ProximityManager.ProximityZone zone)
          {
              switch(zone)
              {
                  case ProximityManager.ProximityZone.Near:
                      normalUpdateInterval = FullUpdateInterval; // Update every frame
                      // Debug.Log($"{gameObject.name}: Set Normal Update Mode: Near (Full)"); // Too noisy
                      break;
                  case ProximityManager.ProximityZone.Moderate:
                      normalUpdateInterval = moderateUpdateInterval; // Throttled update
                      // Debug.Log($"{gameObject.name}: Set Normal Update Mode: Moderate ({moderateUpdateRateHz} Hz)"); // Too noisy
                      break;
                  case ProximityManager.ProximityZone.Far:
                      // GameObject should be inactive, this method shouldn't be called for Far NPCs
                      // If called, reset to default full update
                      normalUpdateInterval = FullUpdateInterval;
                      // Debug.LogWarning($"{gameObject.name}: SetUpdateMode called for Far zone, which should be inactive.", this);
                      break;
              }
              // Apply the new normal interval immediately if not interrupted
              if (!interruptionHandler.IsInterrupted()) // Check interruption handler flag
              {
                   currentUpdateInterval = normalUpdateInterval;
                   timeSinceLastUpdate = 0f; // Reset timer on mode change
              }
          }

          /// <summary>
          /// Called by the InterruptionHandler when an interruption state begins.
          /// Forces the Runner to update every frame regardless of proximity.
          /// </summary>
          public void EnterInterruptionMode()
          {
               Debug.Log($"{gameObject.name}: Entering Interruption Mode (Force Full Update).");
               currentUpdateInterval = FullUpdateInterval; // Override to full update
               timeSinceLastUpdate = 0f; // Reset timer
          }

          /// <summary>
          /// Called by the InterruptionHandler when an interruption state ends.
          /// Restores the update mode to the one dictated by proximity.
          /// </summary>
          public void ExitInterruptionMode()
          {
              Debug.Log($"{gameObject.name}: Exiting Interruption Mode. Restoring normal update mode.");
               // Restore to the normal update interval based on proximity zone
               currentUpdateInterval = normalUpdateInterval;
               timeSinceLastUpdate = 0f; // Reset timer
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

               ResetRunnerTransientData(); // Resets handlers

               // Transient NPCs are always Near/Full update mode initially
               SetUpdateMode(ProximityManager.ProximityZone.Near); // Set initial mode for transient

               queueHandler?.Initialize(this.Manager);
               // PathFollowingHandler doesn't need Initialize with Manager currently

               if (MovementHandler != null && MovementHandler.Agent != null)
               {
                    // Ensure agent is enabled for the initial warp
                    MovementHandler.EnableAgent(); // <-- Ensure Agent is enabled for Warp

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
          /// <param name="tiData">The persistent data for this TI NPC.</param>
          /// <param name="customerManager">Reference to the CustomerManager singleton.</param>
          /// <param name="overrideStartingState">Optional: An active state enum to transition to immediately instead of determining via GetPrimaryStartingStateSO.</param>
          public void Activate(TiNpcData tiData, CustomerManagement.CustomerManager customerManager, Enum overrideStartingState = null)
          {
               if (tiData == null)
               {
                    Debug.LogError($"NpcStateMachineRunner ({gameObject.name}): Activate called with null TiNpcData! Self-disabling.", this);
                    enabled = false;
                    return;
               }

               Debug.Log($"NpcStateMachineRunner ({gameObject.name}): Activating TI NPC '{tiData.Id}'. Loading state and position.", this);

               // --- Set TI identity flags and data link (DO NOT REMOVE THESE) ---
               IsTrueIdentityNpc = true; // <-- This flag is set TRUE here
               TiData = tiData; // <-- Data link is set here
               this.Manager = customerManager; // <-- Manager link is set here
               // --- END Set TI identity ---

               Debug.Log($"DEBUG Runner Activate ({gameObject.name}): IsTrueIdentityNpc={IsTrueIdentityNpc}, TiData is null={ (TiData == null) }, TiData ID={TiData?.Id}");

               queueHandler?.Initialize(this.Manager); // Re-initialize QueueHandler with Manager
               // PathFollowingHandler doesn't need Initialize with Manager currently


               // Apply persistent position and rotation from TiData
               if (MovementHandler != null && MovementHandler.Agent != null)
               {
                    // Ensure agent is enabled for the initial warp
                    MovementHandler.EnableAgent(); // <-- Ensure Agent is enabled for Warp

                    if (MovementHandler.Warp(tiData.CurrentWorldPosition))
                    {
                         Debug.Log($"NpcStateMachineRunner ({gameObject.name}): Warped to {tiData.CurrentWorldPosition} using MovementHandler from TiData.");
                         transform.rotation = tiData.CurrentWorldRotation;
                         // --- Initialize lastGridPosition after warp ---
                         if (gridManager != null)
                         {
                              lastGridPosition = transform.position;
                              // Note: TiNpcManager.ActivateTiNpc should have already updated the grid with this position
                         }
                         // --- END NEW ---
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

               // --- Determine the state to transition to ---
               NpcStateSO startingState = null;

               if (overrideStartingState != null) // Check if an override state was provided
               {
                    Debug.Log($"NpcStateMachineRunner ({gameObject.name}): Using override starting state '{overrideStartingState.GetType().Name}.{overrideStartingState.ToString()}' provided by TiNpcManager.");
                    startingState = GetStateSO(overrideStartingState); // Get the state SO using the override enum
               }

               if (startingState == null) // If no override, or override state SO wasn't found
               {
                   // Fallback to the default logic: check TypeDef primary state
                   Debug.Log($"NpcStateMachineRunner ({gameObject.name}): No valid override state provided/found. Determining primary starting state from TypeDefs.");
                   // Note: GetPrimaryStartingStateSO no longer attempts to load state from TiData itself in this flow.
                   // TiNpcManager Activate handles reading TiData state and providing it as an override.
                   startingState = GetPrimaryStartingStateSO();
               }


               if (startingState != null)
               {
                    Debug.Log($"NpcStateMachineRunner ({gameObject.name}): Transitioning TI NPC '{tiData.Id}' to starting state '{startingState.name}'.");

                    // Populate TiData in context BEFORE TransitionToState
                    _stateContext.TiData = TiData; // <-- NEW: Populate TiData in context

                    TransitionToState(startingState);
               }
               else
               {
                    // This implies GetPrimaryStartingStateSO also failed after override lookup.
                    Debug.LogError($"NpcStateMachineRunner ({gameObject.name}): No valid starting state found after override and primary lookup for TI NPC '{tiData.Id}'. Cannot find any valid starting state. Transitioning to ReturningToPool (for pooling).", this);
                    // Populate TiData in context BEFORE TransitionToState
                    _stateContext.TiData = TiData; // <-- NEW: Populate TiData in context
                    TransitionToState(GetStateSO(GeneralState.ReturningToPool));
               }

               gameObject.SetActive(true);
               enabled = true;

               // Mark data as active (should be done by the activation trigger logic in TiNpcManager)
               // tiData.IsActiveGameObject = true; // TiNpcManager should manage this when calling Activate

               // Initialize timers here. ProximityManager will set the initial mode via SetUpdateMode
               timeSinceLastUpdate = 0f;
               currentUpdateInterval = FullUpdateInterval; // Default to full update until ProximityManager sets mode
               normalUpdateInterval = FullUpdateInterval; // Default normal is also full update
               timeSinceLastGridUpdate = 0f;


               Debug.Log($"NpcStateMachineRunner ({gameObject.name}): TI NPC '{tiData.Id}' Activated.");

               // --- REMOVED: The RestorePathProgress call is now handled INSIDE PathStateSO.OnEnter ---
               // if (startingActiveStateEnum != null && startingActiveStateEnum.Equals(PathState.FollowPath))
               // {
               //      // Check if the data indicates they were mid-path simulation
               //      if (tiData.isFollowingPathBasic && !string.IsNullOrWhiteSpace(tiData.simulatedPathID) && tiData.simulatedWaypointIndex != -1)
               //      {
               //           Debug.Log($"PROXIMITY {tiData.Id}: Activating into PathState. Restoring path progress on handler: PathID='{tiData.simulatedPathID}', Index={tiData.simulatedWaypointIndex}, Reverse={tiData.simulatedFollowReverse}.", npcObject);
               //           // Get the PathSO asset
               //           PathSO pathSOToRestore = WaypointManager.Instance?.GetPath(tiData.simulatedPathID); // Use WaypointManager.Instance
               //           if (pathSOToRestore != null)
               //           {
               //                // Call a new method on the PathFollowingHandler to restore state (Substep 4.5)
               //                PathFollowingHandler?.RestorePathProgress(pathSOToRestore, tiData.simulatedWaypointIndex, tiData.simulatedFollowReverse);
               //           } else {
               //                Debug.LogError($"PROXIMITY {tiData.Id}: PathSO '{tiData.simulatedPathID}' not found via WaypointManager during PathState activation restore! Cannot restore path progress. NPC will likely start path from beginning.", npcObject);
               //                // The PathState.OnEnter will handle starting from the beginning (index 0) as a fallback.
               //           }
               //      } else {
               //           // Not mid-path simulation, PathState.OnEnter will handle starting from the beginning (index 0)
               //           Debug.Log($"PROXIMITY {tiData.Id}: Activating into PathState, but not mid-path simulation. PathState.OnEnter will handle starting from beginning.", npcObject);
               //      }
               // }
               // --- END REMOVED ---
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

               Debug.Log($"NpcStateMachineRunner ({gameObject.name}): Deactivating TI NPC '{TiData.Id}'. Saving state and position to TiData.");

               // --- Save necessary data to TiData ---
               TiData.CurrentWorldPosition = transform.position;
               TiData.CurrentWorldRotation = transform.rotation;

               // Note: TiData.CurrentStateEnumKey/Type should have already been set by TiNpcManager
               // *before* calling TransitionToState(ReturningToPool) during deactivation.
               // This ensures the correct BasicState is saved.

               Debug.Log($"NpcStateMachineRunner ({gameObject.name}): State '{TiData.CurrentStateEnumKey}' already set and saved to TiData by TiNpcManager for simulation.");

               Debug.Log($"DEBUG Runner Deactivate ({gameObject.name}): IsTrueIdentityNpc={IsTrueIdentityNpc}, TiData is null={ (TiData == null) }");

               // Reset other Runner's transient fields and handlers (Manager reference is kept)
               ResetRunnerTransientData(); // Ensures handlers are reset
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
               lastGridPosition = Vector3.zero; // Reset grid tracking position
               timeSinceLastUpdate = 0f; // Reset throttling timers
               currentUpdateInterval = FullUpdateInterval;
               normalUpdateInterval = FullUpdateInterval; // Reset normal interval too
               timeSinceLastGridUpdate = 0f;


               if (!IsTrueIdentityNpc)
               {
                    Shopper?.Reset();
               }

               interruptionHandler?.Reset();
               queueHandler?.Reset();
               npcPathFollowingHandler?.Reset(); // <-- NEW: Reset Path Following handler (assuming it has a Reset method)

               Debug.Log($"{gameObject.name}: NpcStateMachineRunner transient data reset, including handlers.");
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
                    // Debug.Log($"DEBUG Runner TransitionToState ({gameObject.name}): Attempted to transition to current state '{currentState.name}'. Skipping transition logic.", this); // Too noisy
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
                    _stateContext.QueueHandler = QueueHandler;
                    _stateContext.PathFollowingHandler = PathFollowingHandler; // <-- NEW: Populate PathFollowingHandler
                    _stateContext.TiData = TiData; // <-- NEW: Populate TiData

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
               _stateContext.QueueHandler = QueueHandler;
               _stateContext.PathFollowingHandler = PathFollowingHandler; // <-- NEW: Populate PathFollowingHandler
               _stateContext.TiData = TiData; // <-- NEW: Populate TiData


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
          /// For TI NPCs, attempts to load state from TiData first (if not already handled by Activate override).
          /// </summary>
          public NpcStateSO GetPrimaryStartingStateSO()
          {
               // This method is now primarily a fallback if Activate did NOT provide an override state.
               // The logic here for loading state from TiData might need reconsideration
               // if TiData.CurrentStateEnum is expected to be a BasicState now.
               // Let's assume if Activate didn't provide an override, it means
               // either it's a transient NPC, or a TI NPC with no saved state,
               // or a TI NPC with a saved state that couldn't be mapped to Basic.
               // In these cases, we default to the Type Definition's primary start state.

               Debug.Log($"NpcStateMachineRunner ({gameObject.name}): GetPrimaryStartingStateSO called. Checking Type Definition primary state.");


               // Use the Type Definition primary starting state.
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
                    Debug.Log($"NpcStateMachineRunner ({gameObject.name}): Found primary starting state '{startingStateEnum.GetType().Name}.{startingStateEnum.ToString()}' defined in type '{primaryTypeDef?.name ?? "Unknown Type"}'. Looking up state SO.");
                    NpcStateSO startState = GetStateSO(startingStateEnum);

                    if (startState == null)
                    {
                         Debug.LogError($"NpcStateMachineRunner ({gameObject.name}): GetStateSO returned null for primary starting state '{startingStateEnum.GetType().Name}.{startingStateEnum.ToString()}'. Cannot provide a safe start state.");
                    }
                    return startState;
               }
               else
               {
                    Debug.LogError($"NpcStateMachineRunner ({gameObject.name}): No valid primary starting state configured in any assigned type definitions! Cannot start NPC state machine. Attempting ReturningToPool fallback.");
                    NpcStateSO finalFallback = null;
                    // Hardcoded fallback to ReturningToPool if no primary start state is defined
                    if (availableStates != null) availableStates.TryGetValue(GeneralState.ReturningToPool, out finalFallback);

                    if (finalFallback != null)
                    {
                         Debug.LogWarning($"NpcStateMachineRunner ({gameObject.name}): No primary start state configured, attempting hardcoded ReturningToPool as final fallback.");
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

          /// <summary>
          /// Helper method for NpcStateContext to call PathFollowingHandler.RestorePathProgress.
          /// </summary>
          internal bool RestorePathProgress(PathSO path, int waypointIndex, bool reverse)
          {
               if (PathFollowingHandler != null)
               {
                    return PathFollowingHandler.RestorePathProgress(path, waypointIndex, reverse);
               }
               Debug.LogError($"NpcStateMachineRunner ({gameObject.name}): PathFollowingHandler is null! Cannot restore path progress.", this);
               return false;
          }
     }
}