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
using Game.Spatial; // Needed for GridManager
using Game.Proximity; // Needed for ProximityManager
using Game.Navigation; // Needed for PathSO (although not directly used here, good practice if states reference it), PathTransitionDetails
using Game.Prescriptions; // Needed for PrescriptionOrder, PrescriptionManager
using Systems.Interaction; // Needed for IInteractable, InteractionManager // <-- MODIFIED using directive
using Game.Interaction; // Needed for ObtainPrescription


namespace Game.NPC
{
     /// <summary>
     /// The central component on an NPC GameObject that runs the data-driven state machine.
     /// Executes the logic defined in NpcStateSO assets and manages state transitions.
     /// Creates and provides the NpcStateContext to executing states.
     /// Can represent a transient customer or an active True Identity NPC.
     /// Added temporary fields for transient prescription data and populates PrescriptionManager in context.
     /// MODIFIED: Removed caching for MultiInteractableManager. Kept ObtainPrescription caching.
     /// </summary>
     [RequireComponent(typeof(NpcMovementHandler))] // Ensure handlers are attached
     [RequireComponent(typeof(NpcAnimationHandler))]
     [RequireComponent(typeof(CustomerShopper))] // Assuming CustomerShopper is a core handler for customer types
     [RequireComponent(typeof(Handlers.NpcEventHandler))] // Required event handler
     [RequireComponent(typeof(Handlers.NpcInterruptionHandler))] // Require the interruption handler
     [RequireComponent(typeof(Game.NPC.Handlers.NpcQueueHandler))] // Required the Queue handler
     [RequireComponent(typeof(Game.NPC.Handlers.NpcPathFollowingHandler))] // Require the Path Following handler
     // Removed: RequireComponent(typeof(MultiInteractableManager))] // MultiInteractableManager is optional, don't require it
     // Removed: RequireComponent(typeof(ObtainPrescription))] // ObtainPrescription is optional, don't require it
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

          // Reference to the Path Following handler
          private NpcPathFollowingHandler npcPathFollowingHandler;
          public NpcPathFollowingHandler PathFollowingHandler { get { return npcPathFollowingHandler; } private set { npcPathFollowingHandler = value; } }

          // --- NEW: Cached Interaction Components --- // <-- NEW FIELDS
          // REMOVED: private MultiInteractableManager multiInteractableManager; // No longer cached here
          // REMOVED: public MultiInteractableManager MultiInteractableManager { get { return multiInteractableManager; } private set { multiInteractableManager = value; } }

          private ObtainPrescription obtainPrescription; // Keep this
          public ObtainPrescription ObtainPrescription { get { return obtainPrescription; } private set { obtainPrescription = value; } } // Keep this
          // --- END NEW ---


          // Reference to the TiNpcManager
          private TiNpcManager tiNpcManager;
          // Reference to the PrescriptionManager
          private PrescriptionManager prescriptionManager;


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

          // --- Transient Prescription Fields ---
          [Header("Transient Prescription Data")]
          [Tooltip("True if this transient NPC has been assigned a pending prescription order.")]
          internal bool hasPendingPrescriptionTransient;

          [Tooltip("The prescription order assigned to this transient NPC.")]
          public PrescriptionOrder assignedOrderTransient;


          // --- Grid Position Tracking for Active NPCs ---
          private Vector3 lastGridPosition;
          private GridManager gridManager; // Need GridManager reference to get cell size
          private float timeSinceLastGridUpdate = 0f; // Separate timer for grid updates
          private const float GridUpdateCheckInterval = 0.5f; // Check grid position every 0.5 seconds

          // --- Interpolation Fields for Rigidbody Movement ---
          private Vector3 visualPositionStart;
          private Vector3 visualPositionEnd;
          private Quaternion visualRotationStart;
          private Quaternion visualRotationEnd;
          private float interpolationTimer;
          private float interpolationDuration;
          private bool isInterpolatingPosition; // Separate flag for position
          private bool isInterpolatingRotation; // Separate flag for rotation

          // --- Temporary storage for active path progress when interrupted ---
          // These fields are used by NpcInterruptionHandler to save/restore path state.
          // PathStateSO.OnEnter also reads these if wasInterruptedFromPathState is true.
          internal string interruptedPathID;
          internal int interruptedWaypointIndex = -1;
          internal bool interruptedFollowReverse = false;
          internal bool wasInterruptedFromPathState = false;

          // --- Temporary storage for path data when transitioning to generic PathState ---
          // These fields are set by the caller (e.g., another state, TiNpcManager)
          // just before requesting a transition to PathState.FollowPath.
          // The generic PathStateSO.OnEnter reads these fields.
          internal PathSO tempPathSO;
          internal int tempStartIndex;
          internal bool tempFollowReverse;

          private void Awake()
          {
               // --- Get Handler References ---
               MovementHandler = GetComponent<NpcMovementHandler>();
               AnimationHandler = GetComponent<NpcAnimationHandler>();
               Shopper = GetComponent<CustomerShopper>();
               interruptionHandler = GetComponent<NpcInterruptionHandler>();
               queueHandler = GetComponent<NpcQueueHandler>();
               npcPathFollowingHandler = GetComponent<NpcPathFollowingHandler>();

               // --- NEW: Get Interaction Component References --- // <-- NEW LOGIC
               // REMOVED: multiInteractableManager = GetComponent<MultiInteractableManager>(); // No longer get this
               obtainPrescription = GetComponent<ObtainPrescription>(); // Keep this

               // Note: These components are not required, so we don't need null checks to disable the runner here.
               // REMOVED: if (multiInteractableManager == null) Debug.LogWarning($"NpcStateMachineRunner on {gameObject.name}: MultiInteractableManager component not found. Interaction switching may not work correctly.", this);
               if (obtainPrescription == null) Debug.LogWarning($"NpcStateMachineRunner on {gameObject.name}: ObtainPrescription component not found. Prescription interaction may not work correctly.", this);
               // --- END NEW LOGIC ---


               if (MovementHandler == null || AnimationHandler == null || Shopper == null || interruptionHandler == null || QueueHandler == null || PathFollowingHandler == null)
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
               // Handlers and cached components are set here. Managers are set later when available/in Start/Update/TransitionToState.
               _stateContext = new NpcStateContext
               {
                    MovementHandler = MovementHandler,
                    AnimationHandler = AnimationHandler,
                    Shopper = Shopper,
                    NpcObject = this.gameObject,
                    Runner = this,
                    InterruptionHandler = interruptionHandler, // InterruptionHandler is available in Awake
                    QueueHandler = queueHandler,
                    PathFollowingHandler = npcPathFollowingHandler,
                    // REMOVED: MultiInteractableManager = multiInteractableManager, // No longer populate this
                    ObtainPrescription = obtainPrescription // <-- NEW: Inject cached reference
                    // Managers (Customer, TI, Prescription) and TiData will be populated in Start/Update/TransitionToState
               };

               _hasReachedCurrentDestination = true;

               // --- Initialize interpolation fields ---
               visualPositionStart = transform.position;
               visualPositionEnd = transform.position;
               visualRotationStart = transform.rotation;
               visualRotationEnd = transform.rotation;
               interpolationTimer = 0f;
               interpolationDuration = 0f;
               isInterpolatingPosition = false;
               isInterpolatingRotation = false;

               // --- Initialize temporary path fields ---
               tempPathSO = null; // <-- Initialize
               tempStartIndex = 0; // <-- Initialize
               tempFollowReverse = false; // <-- Initialize

               // --- Initialize transient prescription fields ---
               hasPendingPrescriptionTransient = false;
               assignedOrderTransient = new PrescriptionOrder(); // Initialize with default struct values

               Debug.Log($"{gameObject.name}: NpcStateMachineRunner Awake completed.");
          }

          private void Start() // <-- Use Start for Manager singletons
          {
               // Get references to managers
               Manager = CustomerManagement.CustomerManager.Instance;
               if (Manager == null) Debug.LogError($"NpcStateMachineRunner ({gameObject.name}): CustomerManager instance not found!", this);

               tiNpcManager = TiNpcManager.Instance;
               if (tiNpcManager == null) Debug.LogError($"NpcStateMachineRunner ({gameObject.name}): TiNpcManager instance not found!", this);

               prescriptionManager = PrescriptionManager.Instance;
               if (prescriptionManager == null) Debug.LogError($"NpcStateMachineRunner ({gameObject.name}): PrescriptionManager instance not found!", this);


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
               }

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
                    _stateContext.Manager = Manager;
                    _stateContext.TiNpcManager = tiNpcManager;
                    _stateContext.PrescriptionManager = prescriptionManager;
                    _stateContext.CurrentTargetLocation = CurrentTargetLocation;
                    _stateContext.Runner = this;
                    _stateContext.InterruptionHandler = interruptionHandler;
                    _stateContext.QueueHandler = QueueHandler;
                    _stateContext.PathFollowingHandler = PathFollowingHandler;
                    _stateContext.TiData = TiData;
                    // REMOVED: _stateContext.MultiInteractableManager = MultiInteractableManager; // No longer populate this
                    _stateContext.ObtainPrescription = ObtainPrescription; // <-- NEW: Populate cached reference
                    // Need to populate DeltaTime here too if OnExit logic uses it, but typically it doesn't.
                    // _stateContext.DeltaTime = Time.deltaTime; // Or some default? Let's assume OnExit doesn't need it.
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
               // Check grid position update (always called, but timer inside limits frequency)
               CheckGridPositionUpdate();

               // Animation speed update should happen every frame for visual smoothness
               UpdateAnimationSpeed(); // Call animation update helper

               // --- Handle Visual Interpolation for Rigidbody Movement ---
               // This runs every frame regardless of tick frequency
               if (PathFollowingHandler != null && PathFollowingHandler.IsFollowingPath) // Only interpolate if path following is active
               {
                   if (isInterpolatingPosition)
                   {
                       interpolationTimer += Time.deltaTime;
                       // Clamp timer to duration to avoid overshooting
                       float t = Mathf.Clamp01(interpolationTimer / interpolationDuration);

                       // Apply interpolated position
                       transform.position = Vector3.Lerp(visualPositionStart, visualPositionEnd, t);

                       // Stop position interpolation once duration is reached
                       if (interpolationTimer >= interpolationDuration)
                       {
                           isInterpolatingPosition = false;
                       }
                   }

                   if (isInterpolatingRotation)
                   {
                       // Use the same timer for rotation interpolation
                       float t = Mathf.Clamp01(interpolationTimer / interpolationDuration);

                       // Apply interpolated rotation
                       transform.rotation = Quaternion.Slerp(visualRotationStart, visualRotationEnd, t);

                       // Stop rotation interpolation once duration is reached
                       if (interpolationTimer >= interpolationDuration)
                       {
                           isInterpolatingRotation = false;
                       }
                   }

                   // If both position and rotation interpolation are done, reset the flag
                   if (!isInterpolatingPosition && !isInterpolatingRotation)
                   {
                       // Reset timer and duration here if both are finished
                       interpolationTimer = 0f;
                       interpolationDuration = 0f;
                   }
               }

               // --- Determine if this Runner should be ticked this frame ---
               // Query managers to decide how to tick
               // Only TI NPCs are managed by ProximityManager and InterruptionHandler for ticking
               if (IsTrueIdentityNpc && TiData != null && ProximityManager.Instance != null && interruptionHandler != null && Manager != null)
               {
                   // Get the current zone from ProximityManager
                   ProximityManager.ProximityZone currentZone = ProximityManager.Instance.GetNpcProximityZone(TiData);
                   // Check if currently interrupted
                   bool isCurrentlyInterrupted = interruptionHandler.IsInterrupted();
                   // Check if currently active customer (implies they are inside the store area)
                   bool isInsideStore = Manager.IsTiNpcInsideStore(TiData); // Assuming CustomerManager has this method

                   // If Near or Interrupted, tick core logic every frame
                    if (currentZone == ProximityManager.ProximityZone.Near || isCurrentlyInterrupted || (IsTrueIdentityNpc && isInsideStore))
                    {
                         ThrottledTick(Time.deltaTime); // Call the core logic with frame delta
                    }
                   // Moderate NPCs will be ticked by ProximityManager's separate routine.
                   // Far NPCs are inactive, this Update doesn't run.
               }
               else if (!IsTrueIdentityNpc)
               {
                    // Transient NPCs always update every frame
                    // Debug.Log($"DEBUG Runner Update ({gameObject.name}): Ticking every frame (Transient)"); // Too noisy
                    ThrottledTick(Time.deltaTime); // Transient NPCs always get full updates
               }
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
          /// This method is called by external managers (like ProximityManager) or the Runner's Update
          /// to tick the Runner's core logic.
          /// </summary>
          /// <param name="deltaTime">The time slice for this tick.</param>
          public void ThrottledTick(float deltaTime)
          {
               // This method now contains the core state and movement logic
               // that was previously in the Update method.

               if (currentState != null)
               {
                    // Populate context with current data before passing to state methods
                    _stateContext.Manager = Manager;
                    _stateContext.TiNpcManager = tiNpcManager;
                    _stateContext.PrescriptionManager = prescriptionManager;
                    _stateContext.CurrentTargetLocation = CurrentTargetLocation;
                    _stateContext.Runner = this;
                    _stateContext.InterruptionHandler = interruptionHandler;
                    _stateContext.QueueHandler = queueHandler;
                    _stateContext.PathFollowingHandler = npcPathFollowingHandler;
                    _stateContext.TiData = TiData;
                    // REMOVED: _stateContext.MultiInteractableManager = MultiInteractableManager; // No longer populate this
                    _stateContext.ObtainPrescription = ObtainPrescription; // <-- NEW: Populate cached reference
                    _stateContext.DeltaTime = deltaTime;


                    // --- Handle PathFollowingHandler Tick Movement ---
                    if (PathFollowingHandler != null && PathFollowingHandler.IsFollowingPath) // Only interpolate if path following is active
                    {
                         // --- Get start position/rotation before tick ---
                         visualPositionStart = transform.position;
                         visualRotationStart = transform.rotation;

                         // Call the handler's tick method, which now returns the calculated end position/rotation
                         MovementTickResult tickResult = PathFollowingHandler.TickMovement(deltaTime); // Use the deltaTime parameter

                         // --- Store end position/rotation and setup interpolation ---
                         visualPositionEnd = tickResult.Position;
                         visualRotationEnd = tickResult.Rotation;
                         interpolationTimer = 0f; // Reset timer
                         interpolationDuration = deltaTime; // Duration is the delta time of this tick
                         isInterpolatingPosition = true; // Flag to start position interpolation
                         isInterpolatingRotation = true; // Flag to start rotation interpolation
                    }
                    else
                    {
                         isInterpolatingPosition = false;
                         isInterpolatingRotation = false;
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

                         // Re-populate context just in case Update loop took time (defensive)
                         _stateContext.Manager = Manager;
                         _stateContext.TiNpcManager = tiNpcManager;
                         _stateContext.PrescriptionManager = prescriptionManager;
                         _stateContext.CurrentTargetLocation = CurrentTargetLocation;
                         _stateContext.Runner = this;
                         _stateContext.InterruptionHandler = interruptionHandler;
                         _stateContext.QueueHandler = queueHandler;
                         _stateContext.PathFollowingHandler = npcPathFollowingHandler;
                         _stateContext.TiData = TiData;
                         // REMOVED: _stateContext.MultiInteractableManager = MultiInteractableManager; // No longer populate this
                         _stateContext.ObtainPrescription = ObtainPrescription; // <-- NEW: Populate cached reference
                         _stateContext.DeltaTime = deltaTime; // <-- Populate DeltaTime again (defensive)


                         currentState.OnReachedDestination(_stateContext);
                    }

                     // Re-populate context just in case Update loop took time (defensive)
                    _stateContext.Manager = Manager;
                    _stateContext.TiNpcManager = tiNpcManager;
                    _stateContext.PrescriptionManager = prescriptionManager;
                    _stateContext.CurrentTargetLocation = CurrentTargetLocation;
                    _stateContext.Runner = this;
                    _stateContext.InterruptionHandler = interruptionHandler;
                    _stateContext.QueueHandler = queueHandler;
                    _stateContext.PathFollowingHandler = npcPathFollowingHandler;
                    _stateContext.TiData = TiData;
                    // REMOVED: _stateContext.MultiInteractableManager = MultiInteractableManager; // No longer populate this
                    _stateContext.ObtainPrescription = ObtainPrescription; // <-- NEW: Populate cached reference
                    _stateContext.DeltaTime = deltaTime; // <-- Populate DeltaTime again (defensive)


                    // --- Call OnUpdate with the provided deltaTime ---
                    currentState.OnUpdate(_stateContext); // <-- This is the main state logic
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

               this.Manager = manager; // Set Manager reference
               // Other manager references (TI, Prescription) are obtained in Start

               ResetRunnerTransientData(); // Resets handlers and temporary path fields

               Debug.Log($"[DEBUG {gameObject.name}] Runner Initialize: Calling queueHandler?.Initialize(). queueHandler is null: {queueHandler == null}", this);
               queueHandler?.Initialize(this.Manager); // Initialize QueueHandler with CustomerManager
               // PathFollowingHandler doesn't need Initialize with Manager currently


               if (MovementHandler != null && MovementHandler.Agent != null)
               {
                    // Ensure agent is enabled for the initial warp
                    MovementHandler.EnableAgent(); // <-- Ensure Agent is enabled for Warp

                    if (MovementHandler.Warp(startPosition))
                    {
                         Debug.Log($"NpcStateMachineRunner ({gameObject.name}): Warped to {startPosition} using MovementHandler.");
                         // After warp, ensure interpolation is off
                         isInterpolatingPosition = false;
                         isInterpolatingRotation = false;
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
          /// Called by the TiNpcManager to activate a TRUE IDENTITY NPC.
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

               // --- Set TI identity flags and data link ---
               IsTrueIdentityNpc = true;
               TiData = tiData;
               this.Manager = customerManager;

               Debug.Log($"DEBUG Runner Activate ({gameObject.name}): IsTrueIdentityNpc={IsTrueIdentityNpc}, TiData is null={ (TiData == null) }, TiData ID={TiData?.Id}");

               queueHandler?.Initialize(this.Manager); // Re-initialize QueueHandler with CustomerManager

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
                         // After warp, ensure interpolation is off
                         isInterpolatingPosition = false;
                         isInterpolatingRotation = false;
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

                    // Populate TiData and TiNpcManager in context BEFORE TransitionToState
                    _stateContext.TiData = TiData;
                    _stateContext.TiNpcManager = tiNpcManager;
                    _stateContext.PrescriptionManager = prescriptionManager;
                    // REMOVED: _stateContext.MultiInteractableManager = MultiInteractableManager; // No longer populate this
                    _stateContext.ObtainPrescription = ObtainPrescription; // <-- NEW: Populate cached reference


                    TransitionToState(startingState);
               }
               else
               {
                    // This implies GetPrimaryStartingStateSO also failed after override lookup.
                    Debug.LogError($"NpcStateMachineRunner ({gameObject.name}): No valid starting state found after override and primary lookup for TI NPC '{tiData.Id}'. Cannot find any valid starting state. Transitioning to ReturningToPool (for pooling).", this);
                    // Populate TiData and TiNpcManager in context BEFORE TransitionToState
                    _stateContext.TiData = TiData;
                    _stateContext.TiNpcManager = tiNpcManager;
                    _stateContext.PrescriptionManager = prescriptionManager;
                    // REMOVED: _stateContext.MultiInteractableManager = MultiInteractableManager; // No longer populate this
                    _stateContext.ObtainPrescription = ObtainPrescription; // <-- NEW: Populate cached reference
                    TransitionToState(GetStateSO(GeneralState.ReturningToPool));
               }

               gameObject.SetActive(true);
               enabled = true;

               // Initialize timers here. ProximityManager will set the initial mode via SetUpdateMode
               timeSinceLastGridUpdate = 0f;


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
               timeSinceLastGridUpdate = 0f;
               visualPositionStart = transform.position; // Reset to current visual position
               visualPositionEnd = transform.position;
               visualRotationStart = transform.rotation; // Reset to current visual rotation
               visualRotationEnd = transform.rotation;
               interpolationTimer = 0f;
               interpolationDuration = 0f;
               isInterpolatingPosition = false;
               isInterpolatingRotation = false;
               interruptedPathID = null;
               interruptedWaypointIndex = -1;
               interruptedFollowReverse = false;
               wasInterruptedFromPathState = false;

               // --- Reset temporary path fields ---
               tempPathSO = null;
               tempStartIndex = 0;
               tempFollowReverse = false;

               // --- Reset transient prescription fields ---
               hasPendingPrescriptionTransient = false;
               assignedOrderTransient = new PrescriptionOrder(); // Reset to default struct values

               if (!IsTrueIdentityNpc)
               {
                    Shopper?.Reset();
               }

               interruptionHandler?.Reset();
               queueHandler?.Reset();
               npcPathFollowingHandler?.Reset();

               // --- NEW: Reset Interaction Component states --- // <-- NEW LOGIC
               obtainPrescription?.ResetInteraction(); // Call the ResetInteraction method
               // Note: MultiInteractableManager state is handled by the State SOs in OnEnter/OnExit
               // --- END NEW LOGIC ---


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
                    _stateContext.TiNpcManager = tiNpcManager;
                    _stateContext.PrescriptionManager = prescriptionManager;
                    _stateContext.CurrentTargetLocation = CurrentTargetLocation;
                    _stateContext.Runner = this;
                    _stateContext.InterruptionHandler = interruptionHandler;
                    _stateContext.QueueHandler = queueHandler;
                    _stateContext.PathFollowingHandler = npcPathFollowingHandler;
                    _stateContext.TiData = TiData;
                    // REMOVED: _stateContext.MultiInteractableManager = MultiInteractableManager; // No longer populate this
                    _stateContext.ObtainPrescription = ObtainPrescription; // <-- NEW: Populate cached reference


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
               _stateContext.TiNpcManager = tiNpcManager;
               _stateContext.PrescriptionManager = prescriptionManager;
               _stateContext.CurrentTargetLocation = CurrentTargetLocation;
               _stateContext.Runner = this;
               _stateContext.InterruptionHandler = interruptionHandler;
               _stateContext.QueueHandler = queueHandler;
               _stateContext.PathFollowingHandler = npcPathFollowingHandler;
               _stateContext.TiData = TiData;
               // REMOVED: _stateContext.MultiInteractableManager = MultiInteractableManager; // No longer populate this
               _stateContext.ObtainPrescription = ObtainPrescription; // <-- NEW: Populate cached reference
               // DeltaTime is not typically needed in OnEnter, but populate defensively if needed
               // _stateContext.DeltaTime = Time.deltaTime; // Or some default?

               // Call OnEnter for the new state
               currentState.OnEnter(_stateContext);

               // --- Reset interpolation state on state transition ---
               // Any ongoing interpolation should stop when the state changes.
               visualPositionStart = transform.position; // Snap visual to current actual position
               visualPositionEnd = transform.position;
               visualRotationStart = transform.rotation; // Snap visual to current actual rotation
               visualRotationEnd = transform.rotation;
               interpolationTimer = 0f;
               interpolationDuration = 0f;
               isInterpolatingPosition = false;
               isInterpolatingRotation = false;
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
          /// Prepares the Runner with path data for the *next* transition,
          /// assuming the next state will be the generic PathState.FollowPath.
          /// This data is read by PathStateSO.OnEnter when starting a new path.
          /// </summary>
          /// <param name="path">The PathSO to follow.</param>
          /// <param name="startIndex">The index of the waypoint to start from.</param>
          /// <param name="reverse">Whether to follow the path in reverse.</param>
          public void PreparePathTransition(PathSO path, int startIndex, bool reverse)
          {
               if (path == null)
               {
                    Debug.LogError($"NpcStateMachineRunner ({gameObject.name}): PreparePathTransition called with a null PathSO!", this);
                    // Clear existing temp data on error
                    tempPathSO = null;
                    tempStartIndex = 0;
                    tempFollowReverse = false;
                    return;
               }
               if (startIndex < 0 || startIndex >= path.WaypointCount)
               {
                    Debug.LogError($"NpcStateMachineRunner ({gameObject.name}): PreparePathTransition called with invalid startIndex {startIndex} for path '{path.PathID}' (WaypointCount: {path.WaypointCount})!", this);
                     // Clear existing temp data on error
                    tempPathSO = null;
                    tempStartIndex = 0;
                    tempFollowReverse = false;
                    return;
               }

               tempPathSO = path;
               tempStartIndex = startIndex;
               tempFollowReverse = reverse;
               // Debug.Log($"NpcStateMachineRunner ({gameObject.name}): Prepared path transition data for path '{path.PathID}', start index {startIndex}, reverse {reverse}.", this); // Too noisy
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
          /// Also attempts to load state from TiData first (if not already handled by Activate override).
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
                         Debug.LogWarning($"NpcStateMachineRunner ({gameObject.name}): No primary start state configured and hardcoded ReturningToPool fallback not available either! Cannot start NPC.", this);
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

          // --- Helper method for animation speed update (moved from Update) ---
          private void UpdateAnimationSpeed()
          {
               if (MovementHandler != null && AnimationHandler != null)
               {
                    // Only update animation speed if NavMeshAgent is enabled (standard movement)
                    if (MovementHandler.IsAgentEnabled)
                    {
                         float speed = MovementHandler.Agent.velocity.magnitude;
                         AnimationHandler.SetSpeed(speed);
                    }
                    // Add logic here if PathFollowingHandler also needs animation speed control
                    // else if (PathFollowingHandler != null && PathFollowingHandler.IsFollowingPath)
                    // {
                    //     AnimationHandler.SetSpeed(PathFollowingHandler.pathFollowingSpeed); // Assuming speed is accessible
                    // }
               }
          }
     }
}