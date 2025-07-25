// --- START OF FILE TiNpcManager.cs (Modified State Saving) ---

using UnityEngine;
using System.Collections.Generic;
using System.Linq; // Needed for LINQ (Where, FirstOrDefault, ToList) // <-- Added ToList
using Game.NPC; // Needed for NpcStateMachineRunner, GeneralState, CustomerState, PathState enum
using Game.NPC.States; // Needed for State SOs (to check HandledState)
using Game.NPC.BasicStates; // Needed for BasicState enum, BasicPathState enum, BasicNpcStateManager
using Utils.Pooling; // Needed for PoolingManager
using System.Collections; // Needed for Coroutine
using System; // Needed for Enum and Type
using Game.NPC.Types; // Needed for TestState enum (Patrol)
using CustomerManagement; // Needed for CustomerManager, QueueType
using Game.NPC.Handlers;
using Game.Spatial; // Needed for GridManager
using Game.Proximity; // Needed for ProximityManager
using Game.Navigation; // Needed for WaypointManager (needed by BasicPathStateSO), PathSO
using Game.Utilities; // Needed for TimeRange
using Game.NPC.Decisions; // Needed for DecisionOption, SerializableDecisionOptionDictionary
using Game.Prescriptions; // Needed for PrescriptionOrder, PrescriptionManager


namespace Game.NPC.TI // Keep in the TI namespace
{
     /// <summary>
     /// Awaiting description
     /// </summary>
     public class TiNpcManager : MonoBehaviour
     {
          // --- Singleton Instance ---
          public static TiNpcManager Instance { get; private set; }

          [Header("References")]
          [Tooltip("Reference to the CustomerManager instance in the scene.")]
          [SerializeField] private CustomerManagement.CustomerManager customerManager; // Need link for activation (e.g., for TryJoinQueue)
          [Tooltip("Reference to the PoolingManager instance in the scene.")]
          [SerializeField] private PoolingManager poolingManager; // Assign PoolingManager directly
          [Tooltip("Reference to the Player's Transform for simulation logic that might need player position.")] // Updated tooltip
          [SerializeField] private Transform playerTransform;
          // Reference to BasicNpcStateManager (will be obtained in Awake/Start)
          private BasicNpcStateManager basicNpcStateManager;
          // Reference to GridManager (needed for updating position during simulation)
          // This reference will be obtained in Awake/Start
          private GridManager gridManager; // <-- ADDED GridManager reference
                                           // Reference to ProximityManager (will be obtained in Awake/Start)
          private ProximityManager proximityManager;
          // Reference to WaypointManager (will be obtained in Awake/Start)
          private WaypointManager waypointManager; // <-- Public getter
                                                   // Reference to PrescriptionManager (will be obtained in Awake/Start)
          private PrescriptionManager prescriptionManager;
          private CashierManager cashierManager;
          public CashierManager CashierManager => cashierManager;

          // --- Reference to the Simulation Orchestrator ---
          internal TiNpcSimulationManager tiNpcSimulationManager;

          // --- Reference to the State Transition Handler ---
          private TiNpcStateTransitionHandler tiNpcStateTransitionHandler;

          [Header("Dummy Data Loading")]
          [Tooltip("Create some dummy TI NPC data instances here for testing.")]
          [SerializeField] private List<DummyTiNpcDataEntry> dummyNpcData;

          // --- DEBUG: Inactive NPC Visualization Settings ---
          [Header("Debug Visualization (Editor Only)")]
          [Tooltip("Enable drawing gizmos for inactive TI NPCs in the Scene view.")]
          [SerializeField] private bool drawInactiveNpcsGizmos = true;
          [Tooltip("The color of the gizmo sphere for inactive NPCs.")]
          [SerializeField] private Color inactiveGizmoColor = Color.blue;
          [Tooltip("The radius of the gizmo sphere for inactive NPCs.")]
          [SerializeField] private float inactiveGizmoRadius = 0.5f;
          // --- END DEBUG ---


          [System.Serializable]
          private class DummyTiNpcDataEntry // Helper class for creating dummy data in inspector
          {
               [Tooltip("A unique identifier for this dummy TI NPC.")]
               public string id;
               [Tooltip("The specific GameObject prefab to use when activating this dummy TI NPC.")]
               [SerializeField] public GameObject prefab; // <-- ADDED PREFAB FIELD
               [Tooltip("The NPC's designated home position.")]
               public Vector3 homePosition;
               [Tooltip("The NPC's designated home rotation.")]
               public Quaternion homeRotation = Quaternion.identity; // Default rotation

               // --- Day Start Behavior Configuration ---
               [Header("Intended Day Start Behavior")]
               [Tooltip("If true, the NPC will follow a path when its day starts. If false, it will transition to a specific state.")]
               [SerializeField] public bool usePathForDayStart = false;

               [Tooltip("The Enum key for the *Active* state this NPC should transition to when its day starts (e.g., TestState.Patrol, CustomerState.LookingToShop). Only used if 'Use Path For Day Start' is false.")]
               [SerializeField] public string dayStartActiveStateEnumKey;
               [Tooltip("The Type name of the Enum key for the day start state (e.g., Game.NPC.TestState, Game.NPC.CustomerState). Only used if 'Use Path For Day Start' is false.")]
               [SerializeField] public string dayStartActiveStateEnumType;

               [Tooltip("The Path ID if the day start behavior is path following. Only used if 'Use Path For Day Start' is true.")]
               [SerializeField] public string dayStartPathID;
               [Tooltip("The index of the waypoint to start the path from (0-based) if the day start behavior is path following.")]
               [SerializeField] public int dayStartStartIndex = 0;
               [Tooltip("Optional: If true, follow the path in reverse from the start index if the day start behavior is path following.")]
               [SerializeField] public bool dayStartFollowReverse = false;

               // --- NEW: Initial Day Start Control Flag --- // <-- ADDED FIELD
               [Header("Initial State Flags")] // Group with other initial state settings
               [Tooltip("The initial value of the 'Can Start Day' flag for this dummy NPC.")]
               [SerializeField] public bool startWithCanStartDay = true; // Default to true
               // --- END NEW ---


               // --- Schedule Time Ranges for Dummy Data ---
               [Header("Schedule Time Ranges")]
               [Tooltip("The time range during the day when this NPC is allowed to be active or simulated.")]
               [SerializeField] public Game.Utilities.TimeRange startDay = new Game.Utilities.TimeRange(0, 0, 23, 59); // Default: all day
               [Tooltip("The time range during the day when this NPC should begin exiting/returning home.")]
               [SerializeField] public Game.Utilities.TimeRange endDay = new Game.Utilities.TimeRange(22, 0, 5, 0); // Default: 10 PM to 5 AM
                                                                                                                    // --- End Schedule Time Ranges ---

               [Header("Unique Decision Options (by Point ID)")]
               [Tooltip("Unique decision options for this dummy NPC, keyed by Decision Point ID.")]
               [SerializeField] public SerializableDecisionOptionDictionary uniqueDecisionOptions = new SerializableDecisionOptionDictionary();

               // --- Dummy Prescription Data (for testing initial state) ---
               [Header("Dummy Prescription Data")]
               [Tooltip("Set to true if this dummy NPC should start with a pending prescription.")]
               [SerializeField] public bool startWithPendingPrescription = false;
               [Tooltip("The dummy prescription order assigned if starting with pending.")]
               [SerializeField] public PrescriptionOrder initialAssignedOrder = new PrescriptionOrder("Dummy Patient", "Dummy Drug", 1, 7); // Default dummy order
                                                                                                                                            // --- END NEW ---
          }

          // --- Persistent Data Storage ---
          // Use a single dictionary as the source of truth for all TI NPC data
          internal Dictionary<string, TiNpcData> allTiNpcs = new Dictionary<string, TiNpcData>();

          // --- List of NPC Type Definitions ---
          [Header("NPC Type Definitions")]
          [Tooltip("Assign the type definitions that define this NPC's states (e.g., General, Customer, TrueIdentity). Order matters for overrides.")]
          [SerializeField] private List<NpcTypeDefinitionSO> npcTypes;

          // --- Dictionary of available Active states for Runners ---
          // This is still needed by NpcStateMachineRunner.GetStateSO
          private Dictionary<Enum, NpcStateSO> availableStates;

          // --- Public Accessor for WaypointManager ---
          public WaypointManager WaypointManager => waypointManager;


          private void Awake()
          {
               // Implement singleton pattern
               if (Instance == null)
               {
                    Instance = this;
                    // Optional: DontDestroyOnLoad(gameObject); // Consider if this manager should persist
               }
               else
               {
                    Debug.LogWarning("TiNpcManager: Duplicate instance found. Destroying this one.", this);
                    Destroy(gameObject);
                    return;
               }

               // --- Instantiate State Transition Handler ---
               // Create a new GameObject for the state transition handler component
               GameObject stateTransitionHandlerGO = new GameObject("TiNpcStateTransitionHandler");
               stateTransitionHandlerGO.transform.SetParent(this.transform); // Parent it under the manager for organization
               tiNpcStateTransitionHandler = stateTransitionHandlerGO.AddComponent<TiNpcStateTransitionHandler>();

               // --- Load Available Active States ---
               LoadAvailableStates();

               Debug.Log("TiNpcManager: Awake completed.");
          }

          private void Start()
          {
               // Get reference to the PoolingManager if not assigned
               if (poolingManager == null)
               {
                    poolingManager = PoolingManager.Instance;
               }
               if (poolingManager == null)
               {
                    Debug.LogError("TiNpcManager: PoolingManager instance not found or not assigned! TI NPC pooling/activation will not work. Please add a PoolingManager to your scene or assign it.", this);
                    enabled = false;
                    return;
               }

               // Get reference to the CustomerManager if not assigned
               if (customerManager == null)
               {
                    customerManager = CustomerManagement.CustomerManager.Instance;
                    // Log Error, but don't disable, some TI NPCs might not be customers
                    if (customerManager == null) Debug.LogError("TiNpcManager: CustomerManager instance not found or not assigned! TI NPCs cannot activate into customer roles. Assign in Inspector or ensure it's a functioning Singleton.", this);
               }
               // Get reference to BasicNpcStateManager
               basicNpcStateManager = BasicNpcStateManager.Instance;
               if (basicNpcStateManager == null)
               {
                    Debug.LogError("TiNpcManager: BasicNpcStateManager instance not found! Cannot simulate inactive TI NPCs. Ensure BasicNpcStateManager is in the scene.", this);
                    // Do NOT disable manager entirely, just simulation won't work.
               }

               // --- GET GridManager reference ---
               gridManager = GridManager.Instance;
               if (gridManager == null)
               {
                    Debug.LogError("TiNpcManager: GridManager instance not found! Cannot use spatial partitioning. Ensure GridManager is in the scene.", this);
                    // Do NOT disable manager entirely, just simulation/proximity will be inefficient/broken.
               }

               // Get reference to ProximityManager
               proximityManager = ProximityManager.Instance;
               if (proximityManager == null)
               {
                    Debug.LogError("TiNpcManager: ProximityManager instance not found! Cannot manage NPC activation/deactivation based on proximity. Ensure ProximityManager is in the scene.", this);
                    // Do NOT disable manager entirely, but NPCs won't activate/deactivate.
               }

               // --- Get reference to WaypointManager ---
               waypointManager = WaypointManager.Instance;
               if (waypointManager == null)
               {
                    Debug.LogError("TiNpcManager: WaypointManager instance not found! Cannot handle path following for TI NPCs. Ensure WaypointManager is in the scene.", this);
                    // Do NOT disable manager entirely, just path following won't work.
               }

               // --- Get reference to PrescriptionManager ---
               prescriptionManager = PrescriptionManager.Instance;
               if (prescriptionManager == null)
               {
                    Debug.LogError("TiNpcManager: PrescriptionManager instance not found! Cannot handle prescription logic for TI NPCs. Ensure PrescriptionManager is in the scene.", this);
                    // Do NOT disable manager entirely, just prescription flow won't work.
               }


               // Validate Player Transform - Still needed for simulation logic that might use player position
               if (playerTransform == null)
               {
                    // Attempt to find Player by tag if not assigned
                    GameObject playerGO = GameObject.FindGameObjectWithTag("Player"); // Assumes Player has "Player" tag
                    if (playerGO != null) playerTransform = playerGO.transform;
                    else Debug.LogWarning("TiNpcManager: Player Transform not assigned and GameObject with tag 'Player' not found! Simulation logic that depends on player position may fail.", this); // Changed to Warning
               }


               cashierManager = CashierManager.Instance;
               if (cashierManager == null) Debug.LogWarning($"TiNpcManager ({gameObject.name}): CashierManager instance not found! Cashier functionality will not work. Ensure CashierManager is in the scene.", this);

               // --- Initialize State Transition Handler ---
               // Need lists of NpcTypeDefinitionSO and BasicNpcStateSO assets for mapping setup
               if (tiNpcStateTransitionHandler != null)
               {
                   List<BasicNpcStateSO> basicStateAssetsList = basicNpcStateManager?.GetBasicStateAssets();

                   if (basicStateAssetsList == null)
                   {
                       Debug.LogError("TiNpcManager: BasicNpcStateManager.GetBasicStateAssets() returned null! Cannot initialize TiNpcStateTransitionHandler mappings.", this);
                       // Continue, but state transitions might fail later.
                   }

                   tiNpcStateTransitionHandler.Initialize(
                       npcTypes, // Pass the npcTypes list
                       basicStateAssetsList, // Pass the list of basic state assets
                       basicNpcStateManager,
                       customerManager,
                       waypointManager,
                       prescriptionManager,
                       cashierManager,
                       TimeManager.Instance // Pass TimeManager.Instance directly for now
                   );
               } else {
                   Debug.LogError("TiNpcManager: TiNpcStateTransitionHandler is null after instantiation! State transitions will not work.", this);
               }

               // --- Instantiate and Initialize Simulation Orchestrator ---
               // Create a new GameObject for the simulation orchestrator component
               GameObject simulationOrchestratorGO = new GameObject("TiNpcSimulationOrchestrator");
               simulationOrchestratorGO.transform.SetParent(this.transform); // Parent it under the manager for organization
               tiNpcSimulationManager = simulationOrchestratorGO.AddComponent<TiNpcSimulationManager>();

               // Initialize the simulation orchestrator with necessary references
               // NOTE: TimeManager.Instance is accessed directly here, which is acceptable during initialization.
               tiNpcSimulationManager.Initialize(this, basicNpcStateManager, gridManager, proximityManager, waypointManager, TimeManager.Instance, playerTransform);

               // Load Dummy Data
               LoadDummyNpcData();

               Debug.Log($"TiNpcManager: Started. Loaded {allTiNpcs.Count} TI NPCs.");

               Debug.Log($"TiNpcManager: Start completed.");


               // --- Start the simulation via the orchestrator ---
               if (tiNpcSimulationManager != null)
               {
                    // The orchestrator will check its own dependencies before starting the coroutine
                    tiNpcSimulationManager.StartSimulation();
               } else {
                    Debug.LogError("TiNpcManager: TiNpcSimulationManager is null after instantiation! Simulation will not run.", this);
               }
          }

          /// <summary>
          /// Loads all available Active NpcStateSO assets based on the assigned NpcTypeDefinitions.
          /// Populates the internal availableStates dictionary used by NpcStateMachineRunner.
          /// RESTORED from previous version.
          /// </summary>
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
                                        // Allow different enum types for now, validation is done in NpcTypeDefinitionSO
                                        // if (keyEnumType != soHandledEnumType)
                                        // {
                                        //      Debug.LogError($"NpcStateMachineRunner ({gameObject.name}): Enum Type mismatch for state '{pair.Key}' in type definition '{typeDef.name}'! Expected '{keyEnumType.Name}', assigned State SO '{pair.Value.name}' has HandledState type '{soHandledEnumType.Name}'. State ignored.", this);
                                        //      continue;
                                        // }
                                        availableStates[pair.Key] = pair.Value;
                                   }
                                   else
                                   {
                                        Debug.LogWarning($"TiNpcManager ({gameObject.name}): Skipping null state entry from type definition '{typeDef.name}'.", this);
                                   }
                              }
                         }
                         else
                         {
                              Debug.LogWarning($"TiNpcManager ({gameObject.name}): Null NPC Type Definition assigned in the list!", this);
                         }
                    }
               }

               if (availableStates == null || availableStates.Count == 0)
               {
                   Debug.LogError($"TiNpcManager ({gameObject.name}): No Active states loaded from assigned type definitions! Cannot function.", this);
                   // Do NOT disable the manager here, it might still manage data/simulation.
                   // The Runner will fail if it can't get states.
               }
               else
               {
                   Debug.Log($"TiNpcManager ({gameObject.name}): Loaded {availableStates.Count} Active states from {npcTypes?.Count ?? 0} type definitions.");
               }
          }

          private void OnEnable()
          {
               // --- Start simulation via orchestrator on enable if it exists ---
               if (tiNpcSimulationManager != null)
               {
                    // The orchestrator will check its own dependencies before starting
                    tiNpcSimulationManager.StartSimulation();
               }
          }

          private void OnDisable()
          {
               // --- Stop simulation via orchestrator on disable if it exists ---
               if (tiNpcSimulationManager != null)
               {
                    // The orchestrator will handle stopping its coroutine
                    // No specific StopSimulation method needed on orchestrator for OnDisable,
                    // as its own OnDisable handles stopping its coroutine.
               }
          }

          private void OnDestroy()
          {
               if (Instance == this)
               {
                    // TODO: In a real game, save all TiNpcData here before clearing.
                    allTiNpcs.Clear();
                    // Clear the grid as well
                    gridManager?.ClearGrid();
                    Instance = null;
                    Debug.Log("TiNpcManager: OnDestroy completed. Data and Grid cleared.");
               }
          }

          // --- Draw gizmos for inactive NPCs ---
          private void OnDrawGizmos()
          {
               if (!drawInactiveNpcsGizmos || allTiNpcs == null || allTiNpcs.Count == 0)
               {
                    return;
               }

               Gizmos.color = inactiveGizmoColor;

               // Iterate over all data and draw gizmos for the inactive ones
               foreach (var tiData in allTiNpcs.Values)
               {
                    // Use the IsActiveGameObject flag to determine if it's inactive
                    if (!tiData.IsActiveGameObject)
                    {
                         // Draw a sphere at the data's stored world position
                         Gizmos.DrawSphere(tiData.CurrentWorldPosition, inactiveGizmoRadius);
                         // Optional: Draw the ID as text for easier identification (Requires UnityEditor and Handles)
                         // try { UnityEditor.Handles.Label(npcPosition + Vector3.up * (zoneGizmoRadius + 0.1f), zone.ToString()); } catch {}

                         // --- Draw line to simulated target position if it exists ---
                         if (tiData.simulatedTargetPosition.HasValue)
                         {
                              Gizmos.color = Color.magenta; // Different color for target line
                              Gizmos.DrawLine(tiData.CurrentWorldPosition, tiData.simulatedTargetPosition.Value);
                              Gizmos.DrawSphere(tiData.simulatedTargetPosition.Value, inactiveGizmoRadius * 0.5f); // Draw a smaller sphere at the target
                              Gizmos.color = inactiveGizmoColor; // Restore color
                         }
                    }
               }
          }

          /// <summary>
          /// Sets up the mapping dictionaries between active and basic states.
          /// REFACTORED: Logic moved to TiNpcStateTransitionHandler.Initialize.
          /// </summary>
          private void SetupStateMappings()
          {
               // This method is empty. The logic is in TiNpcStateTransitionHandler.Initialize.
               // The call to TiNpcStateTransitionHandler.Initialize will be added in Start().
          }


          /// <summary>
          /// Gets the corresponding Basic State enum for a given Active State enum.
          /// Returns BasicState.BasicPatrol if no direct mapping is found (fallback for unmapped active states).
          /// REFACTORED: Delegates to TiNpcStateTransitionHandler.
          /// </summary>
          public Enum GetBasicStateFromActiveState(Enum activeStateEnum)
          {
               // Delegate the call to the state transition handler
               if (tiNpcStateTransitionHandler != null)
               {
                    return tiNpcStateTransitionHandler.GetBasicStateFromActiveState(activeStateEnum);
               } else {
                    Debug.LogError($"TiNpcManager: TiNpcStateTransitionHandler is null! Cannot get basic state from active state '{activeStateEnum?.GetType().Name}.{activeStateEnum?.ToString() ?? "NULL"}'. Falling back to BasicPatrol.", this);
                    return BasicState.BasicPatrol; // Fallback if handler is null
               }
          }

          /// <summary>
          /// Gets the corresponding Active State enum for a given Basic State enum.
          /// Returns GeneralState.Idle if no direct mapping is found (should not happen with correct setup).
          /// REFACTORED: Delegates to TiNpcStateTransitionHandler.
          /// </summary>
          public Enum GetActiveStateFromBasicState(Enum basicStateEnum)
          {
               // Delegate the call to the state transition handler
               if (tiNpcStateTransitionHandler != null)
               {
                    return tiNpcStateTransitionHandler.GetActiveStateFromBasicState(basicStateEnum);
               } else {
                    Debug.LogError($"TiNpcManager: TiNpcStateTransitionHandler is null! Cannot get active state from basic state '{basicStateEnum?.GetType().Name}.{basicStateEnum?.ToString() ?? "NULL"}'. Falling back to GeneralState.Idle.", this);
                    return GeneralState.Idle; // Fallback if handler is null
               }
          }

          /// <summary>
          /// Handles the process of activating a single TI NPC.
          /// Gets a pooled GameObject and calls the Runner's Activate method.
          /// Called by ProximityManager.
          /// MODIFIED: Added specific handling for BasicWaitForCashier, BasicBrowse, BasicPathState, and NEW BasicWaitingAtPrescriptionSpot/BasicWaitingForDeliverySim states.
          /// MODIFIED: Now uses the prefab stored in TiNpcData instead of a shared list.
          /// REFACTORED: The complex state determination and data priming logic is moved to TiNpcStateTransitionHandler in a later phase.
          /// </summary>
          /// <param name="tiData">The persistent data of the NPC to activate.</param>
          public void RequestActivateTiNpc(TiNpcData tiData)
          {
               // Check if it's genuinely inactive before proceeding
               if (tiData == null || tiData.IsActiveGameObject || tiData.NpcGameObject != null)
               {
                    Debug.Log($"PROXIMITY TiNpcManager: Skipping activation attempt for '{tiData?.Id ?? "NULL"}'. Reason: tiData is null ({tiData == null}), IsActiveGameObject={tiData?.IsActiveGameObject}, NpcGameObject is null={(tiData?.NpcGameObject == null)}.");
                    return; // Already active or invalid
               }

               // Get the specific prefab from TiNpcData ---
               GameObject prefabToUse = tiData.Prefab;
               if (prefabToUse == null)
               {
                    Debug.LogError($"PROXIMITY TiNpcManager: Cannot activate TI NPC '{tiData.Id}'. TiNpcData has no Prefab assigned! Skipping activation.", this);
                    // Optionally transition to a state that handles this error or just leave it inactive
                    return; // Cannot activate without a prefab
               }

               // Check if required managers are available before proceeding
               // Need BasicNpcStateManager, CustomerManager, WaypointManager, PrescriptionManager, CashierManager, TimeManager
               if (poolingManager == null || basicNpcStateManager == null || customerManager == null || waypointManager == null || prescriptionManager == null || cashierManager == null || TimeManager.Instance == null || tiNpcStateTransitionHandler == null) // Added TimeManager and handler check
               {
                    Debug.LogError("TiNpcManager: Cannot activate TI NPC. Required manager (PoolingManager, BasicNpcStateManager, CustomerManager, WaypointManager, PrescriptionManager, CashierManager, TimeManager, or TiNpcStateTransitionHandler) is null.", this); // Updated log
                    return;
               }
               DateTime currentTime = TimeManager.Instance.CurrentGameTime; // Get current game time


               // --- Use the specific prefab from TiData to get from the pool ---
               GameObject npcObject = poolingManager.GetPooledObject(prefabToUse);

               if (npcObject != null)
               {
                    NpcStateMachineRunner runner = npcObject.GetComponent<NpcStateMachineRunner>();
                    if (runner != null)
                    {
                         Debug.Log($"PROXIMITY TiNpcManager: Activating TI NPC '{tiData.Id}'. Linking data to GameObject '{npcObject.name}'.");

                         // --- Store GameObject reference and update flags on TiNpcData ---
                         tiData.LinkGameObject(npcObject); // Use helper to set NpcGameObject and isActiveGameObject=true

                         // This ensures events that occurred just before activation are handled,
                         // potentially triggering a state change before the Runner's Activate logic runs.
                         tiData.ProcessPendingSimulatedEvents(basicNpcStateManager);

                         // --- Determine the starting state based on saved data ---
                         // Delegate the complex logic to the State Transition Handler
                         Enum startingActiveStateEnum = tiNpcStateTransitionHandler.DetermineActivationState(tiData, runner, currentTime);

                         // Call the Runner's Activate method with the determined starting state override
                         // If startingActiveStateEnum is null, Runner.Activate will use GetPrimaryStartingStateSO().
                         runner.Activate(tiData, customerManager, startingActiveStateEnum); // <-- Pass determined override state Enum

                         // Corrected syntax for the log message
                         Debug.Log($"PROXIMITY TiNpcManager: Activation initiated for TI NPC '{tiData.Id}' (GameObject '{npcObject.name}'). Runner.Activate called with override state: {(startingActiveStateEnum != null ? startingActiveStateEnum.GetType().Name + "." + startingActiveStateEnum.ToString() : "NULL")}");

                    }
                    else
                    {
                         Debug.LogError($"TiNpcManager: Failed to get a pooled TI NPC GameObject for activation of '{tiData.Id}' using prefab '{prefabToUse.name}'! Pool might be exhausted or prefab is invalid.", this);
                    }
               }
          }

          /// <summary>
          /// Handles the process of deactivating a single TI NPC.
          /// Determines the correct Basic State, saves data, and triggers the pooling flow.
          /// Called by ProximityManager.
          /// Saves the mapped basic state for all active states, including prescription waiting/delivery.
          /// The complex state determination and data saving logic is moved to TiNpcStateTransitionHandler in a later phase.
          /// </summary>
          /// <param name="tiData">The persistent data of the NPC to deactivate.</param>
          /// <param name="runner">The active NpcStateMachineRunner for this NPC.</param>
          public void RequestDeactivateTiNpc(TiNpcData tiData, NpcStateMachineRunner runner)
          {
               if (tiData == null || runner == null || !tiData.IsActiveGameObject || tiData.NpcGameObject != runner.gameObject)
               {
                    Debug.LogWarning($"PROXIMITY TiNpcManager: Skipping deactivation request for '{tiData?.Id ?? "NULL"}'. Reason: tiData is null ({tiData == null}), runner is null ({runner == null}), IsActiveGameObject={tiData?.IsActiveGameObject}, GameObject mismatch.", runner?.gameObject);
                    // Defensive cleanup: If runner exists but link is broken, try to force unlink/pool
                    if (runner != null && runner.IsTrueIdentityNpc && runner.TiData == tiData && tiData != null)
                    {
                         Debug.LogError($"PROXIMITY TiNpcManager: Inconsistent state during deactivation request for '{tiData.Id}'. TiData/Runner link seems okay, but IsActiveGameObject or GameObject mismatch. Forcing cleanup.", runner.gameObject);
                         // Attempt to force the pooling flow without state saving
                         runner.TransitionToState(runner.GetStateSO(GeneralState.ReturningToPool));
                    }
                    return; // Cannot proceed with deactivation logic
               }

               // Check if the NPC is in a state that should prevent deactivation (e.g., Combat, Transaction)
               // This check is duplicated from the old ProximityCheckRoutine, but belongs here
               // as it's part of the *decision* to deactivate, which ProximityManager makes.
               // However, ProximityManager should ideally make this check *before* calling this method.
               // Let's keep it here defensively for now, but note it's better placed in ProximityManager.
               NpcStateSO currentStateSO = runner.GetCurrentState();
               if (currentStateSO != null && !currentStateSO.IsInterruptible)
               {
                    Debug.Log($"PROXIMITY {tiData.Id}: Deactivation request skipped. Current state '{currentStateSO.name}' is not interruptible.");
                    return; // Cannot deactivate right now
               }

               // Check if the state transition handler is available
               if (tiNpcStateTransitionHandler == null || basicNpcStateManager == null) // Need BNSM for OnEnter call
               {
                   Debug.LogError($"TiNpcManager: TiNpcStateTransitionHandler or BasicNpcStateManager is null! Cannot determine deactivation state for '{tiData.Id}'. Forcing cleanup.", runner.gameObject);
                   // Fallback: Destroy the GameObject and unlink the data without attempting to save a simulation state
                   Destroy(runner.gameObject); // Destroy the GameObject
                   tiData.UnlinkGameObject(); // Use helper to clear data link and flags
                                              // Attempt to remove from grid using last known position
                   gridManager?.RemoveItem(tiData, tiData.CurrentWorldPosition);
                   return;
               }


               // --- Determine and Save the Basic State before triggering Pooling ---
               // Delegate the complex logic to the State Transition Handler
               Enum targetBasicState = tiNpcStateTransitionHandler.DetermineDeactivationState(tiData, runner);

               if (targetBasicState != null)
               {
                    // Save the mapped basic state for all active states.
                    // The special handling for WaitingForPrescription/Delivery is now in the MAPPINGS (Phase 1).
                    // The simulation system will now correctly receive BasicWaitingAtPrescriptionSpot or BasicWaitingForDeliverySim.
                    tiData.SetCurrentState(targetBasicState); // Save the mapped basic state
                                                              // Corrected syntax for the log message
                    Debug.Log($"PROXIMITY {tiData.Id}: Active state '{currentStateSO?.GetType().Name}.{currentStateSO?.HandledState.ToString() ?? "NULL"}' maps to Basic State '{targetBasicState.GetType().Name}.{targetBasicState.ToString()}'. Saving this state to TiData for simulation.", runner.gameObject);

                    // --- Trigger Deactivation Flow ---
                    Debug.Log($"PROXIMITY {tiData.Id}: TI NPC ready for deactivation. Triggering TransitionToState(ReturningToPool).", runner.gameObject);
                    // Transition the Runner to the ReturningToPool state.
                    // The Runner.TransitionToState handles calling Runner.Deactivate() *before* entering the state.
                    // Runner.Deactivate() will now save the *Basic State* we just set on tiData, and position/rotation.
                    // HandleTiNpcReturnToPool will be called later by the pooling event, clearing data link/flag and updating grid.
                    runner.TransitionToState(runner.GetStateSO(GeneralState.ReturningToPool));
               }
               else
               {
                    // This shouldn't happen if GetBasicStateFromActiveState has a fallback, but defensive.
                    // Corrected syntax for the log message
                    Debug.LogError($"PROXIMITY {tiData.Id}: Could not determine a Basic State mapping for active state '{currentStateSO?.GetType().Name}.{currentStateSO?.HandledState.ToString() ?? "NULL"}' during deactivation request. Cannot save state for simulation! Forcing cleanup.", runner.gameObject);
                    // Fallback: Destroy the GameObject and unlink the data without attempting to save a simulation state
                    Destroy(runner.gameObject); // Destroy the GameObject
                    tiData.UnlinkGameObject(); // Use helper to clear data link and flags
                                               // Attempt to remove from grid using last known position
                    gridManager?.RemoveItem(tiData, tiData.CurrentWorldPosition);
               }
          }

          /// <summary>
          /// Called by CustomerManager when a TI NPC's GameObject is
          /// ready to be returned to the pool after deactivation.
          /// Handles the final cleanup and data unlinking.
          /// </summary>
          public void HandleTiNpcReturnToPool(GameObject npcObject)
          {
               Debug.Log($"POOL TiNpcManager: HandleTiNpcReturnToPool received GameObject '{npcObject.name}'.");

               if (npcObject == null)
               {
                    Debug.LogWarning("TiNpcManager: Received null GameObject in HandleTiNpcReturnToPool. Ignoring.", this);
                    return;
               }

               NpcStateMachineRunner runner = npcObject.GetComponent<NpcStateMachineRunner>();
               if (runner == null)
               {
                    Debug.LogWarning($"TiNpcManager: Received GameObject '{npcObject.name}' without NpcStateMachineRunner in HandleTiNpcReturnToPool. Cannot process as TI NPC. Attempting to return to pool directly.", npcObject);
                    if (poolingManager != null && npcObject.GetComponent<PooledObjectInfo>() != null) poolingManager.ReturnPooledObject(npcObject);
                    else Destroy(npcObject); // Fallback destroy if not pooled
                    return; // Exit handling
               }

               // Find the TiNpcData associated with this runner.
               // It *should* have been linked via NpcGameObject property.
               TiNpcData deactivatedTiData = allTiNpcs.Values.FirstOrDefault(data => data != null && data.NpcGameObject == npcObject); // Added null check for data

               if (deactivatedTiData != null)
               {
                    Debug.Log($"POOL TiNpcManager: Found TiNpcData for '{deactivatedTiData.Id}' linked to GameObject '{npcObject.name}'. Unlinking data and flags.");

                    // --- Clear the data link and flags ---
                    deactivatedTiData.UnlinkGameObject(); // Use helper to set NpcGameObject=null and isActiveGameObject=false-

                    // --- Update the NPC's position in the grid with its final position before pooling ---
                    // The Runner.Deactivate should have already saved the final position to tiData.CurrentWorldPosition
                    if (gridManager != null)
                    {
                         // Use the position saved in TiData by Runner.Deactivate()
                         // The old position for UpdateItemPosition doesn't strictly matter here
                         // as we are just ensuring it's in the grid at its final position.
                         // Using the GameObject's position just before pooling as the 'old' position is fine.
                         gridManager.UpdateItemPosition(deactivatedTiData, npcObject.transform.position, deactivatedTiData.CurrentWorldPosition);
                         Debug.Log($"POOL TiNpcManager: Updated grid position for '{deactivatedTiData.Id}' to final position {deactivatedTiData.CurrentWorldPosition}.", npcObject);
                    }
                    else
                    {
                         Debug.LogWarning($"POOL TiNpcManager: GridManager is null! Cannot update grid position for '{deactivatedTiData.Id}' on return to pool.", npcObject);
                    }

                    // --- Remove the runner from ProximityManager's active lists ---
                    if (proximityManager != null)
                    {
                         Debug.Log($"POOL TiNpcManager: Removing runner '{runner.gameObject.name}' from ProximityManager active lists.", runner.gameObject);
                         proximityManager.RemoveRunnerFromActiveLists(runner);
                    }
                    else
                    {
                         Debug.LogWarning($"POOL TiNpcManager: ProximityManager is null! Cannot remove runner '{runner.gameObject.name}' from active lists.", runner.gameObject);
                    }
               }
               else
               {
                    // This warning indicates the NpcGameObject -> TiNpcData link was already broken before this handler was called.
                    // This could happen if Runner.Deactivate somehow failed or if the object was pooled via another path.
                    Debug.LogWarning($"POOL TiNpcManager: Could not find TiNpcData linked to returning GameObject '{npcObject.name}' in HandleTiNpcReturnToPool! Data link already lost or inconsistent. Runner.IsTrueIdentityNpc: {runner.IsTrueIdentityNpc}.", npcObject);

                    // Defensive cleanup: If it was a TI NPC (check runner flag), try to find the data by ID if available
                    if (runner.IsTrueIdentityNpc && runner.TiData != null && !string.IsNullOrEmpty(runner.TiData.Id))
                    {
                         // Try to find the data using the ID saved in the runner (if still there)
                         TiNpcData dataById = GetTiNpcData(runner.TiData.Id);
                         if (dataById != null)
                         {
                              Debug.LogError($"POOL TiNpcManager: Found TiNpcData by ID '{dataById.Id}' ({dataById.GetHashCode()}), but GameObject link was missing! Forcing link cleanup on data object. GameObject was likely pooled incorrectly.", npcObject);
                              dataById.UnlinkGameObject(); // Unlink the data object
                                                           // Attempt to remove from grid using the data's last known position
                              gridManager?.RemoveItem(dataById, dataById.CurrentWorldPosition);
                         }
                         else
                         {
                              Debug.LogError($"POOL TiNpcManager: Runner flagged as TI ({runner.IsTrueIdentityNpc}), but TiData link was lost, and TiData ID '{runner.TiData.Id}' lookup failed! Cannot perform data cleanup.", npcObject);
                         }
                    }
                    else if (runner.IsTrueIdentityNpc)
                    {
                         Debug.LogError($"POOL TiNpcManager: Runner flagged as TI ({runner.IsTrueIdentityNpc}), but TiData link was lost and TiData ID was null/empty! Cannot perform data cleanup.", npcObject);
                    }
                    // If runner is not flagged as TI, the CustomerManager should have handled it.
                    // If we somehow get a non-TI here, it's a flow error, but pool it anyway.

                    // --- Attempt to remove the runner from ProximityManager's active lists even if data link is broken ---
                    if (proximityManager != null)
                    {
                         Debug.LogWarning($"POOL TiNpcManager: Attempting to remove runner '{runner.gameObject.name}' from ProximityManager active lists despite data link issue.", runner.gameObject);
                         proximityManager.RemoveRunnerFromActiveLists(runner);
                    }
                    else
                    {
                         Debug.LogWarning($"POOL TiNpcManager: ProximityManager is null! Cannot remove runner '{runner.gameObject.name}' from active lists.", runner.gameObject);
                    }
               }

               Debug.Log($"POOL TiNpcManager: Returning TI NPC GameObject '{npcObject.name}' to pool.");
               if (poolingManager != null)
               {
                    poolingManager.ReturnPooledObject(npcObject);
               }
               else
               {
                    Debug.LogError($"TiNpcManager: PoolingManager is null! Cannot return TI NPC GameObject '{npcObject.name}' to pool. Destroying.", this);
                    Destroy(npcObject);
               }
          }


          /// <summary>
          /// Called by an active NpcStateMachineRunner when its position changes significantly (e.g., enters a new grid cell).
          /// Updates the NPC's position in the GridManager.
          /// REFACTORED: Delegates to TiNpcSimulationManager.
          /// </summary>
          /// <param name="data">The TiNpcData associated with the active NPC.</param>
          /// <param name="oldPosition">The NPC's position before the change.</param>
          /// <param name="newPosition">The NPC's new position.</param>
          public void NotifyActiveNpcPositionChanged(TiNpcData data, Vector3 oldPosition, Vector3 newPosition) // MOVED TO TiNpcSimulationManager
          {
               // Delegate the call to the simulation orchestrator
               if (tiNpcSimulationManager != null)
               {
                    tiNpcSimulationManager.NotifyActiveNpcPositionChanged(data, oldPosition, newPosition);
               } else {
                    Debug.LogError($"TiNpcManager: TiNpcSimulationManager is null! Cannot notify active NPC position change for '{data?.Id ?? "NULL"}'.", data?.NpcGameObject);
               }
          }


          /// <summary>
          /// Gets a specific TI NPC's data by ID.
          /// </summary>
          public TiNpcData GetTiNpcData(string id)
          {
               if (allTiNpcs.TryGetValue(id, out TiNpcData data))
               {
                    return data;
               }
               return null;
          }

          /// <summary>
          /// Gets a list of all currently active TI NPC data records (filtered on the fly).
          /// </summary>
          public List<TiNpcData> GetActiveTiNpcs()
          {
               // Filter the main collection when requested
               return allTiNpcs.Values.Where(data => data != null && data.IsActiveGameObject).ToList(); // Added null check for data
          }

          /// <summary>
          /// Gets a list of all currently inactive TI NPC data records (filtered on the fly).
          /// </summary>
          public List<TiNpcData> GetInactiveTiNpcs()
          {
               // Filter the main collection when requested
               return allTiNpcs.Values.Where(data => data != null && !data.IsActiveGameObject).ToList(); // Added null check for data
          }

          /// <summary>
          /// Returns the total number of TI NPCs managed.
          /// </summary>
          public int GetTotalTiNpcCount()
          {
               return allTiNpcs.Count;
          }

          // --- Get list of all TI NPC IDs ---
          /// <summary>
          /// Gets a list of all unique IDs for the managed TI NPCs.
          /// </summary>
          public List<string> GetTiNpcIds()
          {
               // Return a new list containing all the keys (IDs) from the dictionary
               return allTiNpcs.Keys.ToList();
          }

          /// <summary>
          /// Gets a random point within the defined XZ patrol area bounds (for simulation).
          /// Uses a fixed Y height (e.g., 0) for simplicity as NavMesh sampling is not available.
          /// This method is now only used by LoadDummyNpcData for initial targets.
          /// Patrol simulation logic now uses the version in BasicPatrolStateSO.
          /// </summary>
          private Vector3 GetRandomPointInPatrolAreaSimulated()
          {
               // Keep the logic here for dummy data initialization, but the simulation logic should use the SO's version.
               // These bounds should ideally match the BasicPatrolStateSO's bounds.
               Vector2 simulatedPatrolAreaMin = new Vector2(-10f, -10f); // Hardcoded to match BasicPatrolStateSO for dummy data
               Vector2 simulatedPatrolAreaMax = new Vector2(10f, 10f); // Hardcoded to match BasicPatrolStateSO for dummy data
               float randomX = UnityEngine.Random.Range(simulatedPatrolAreaMin.x, simulatedPatrolAreaMax.x);
               float randomZ = UnityEngine.Random.Range(simulatedPatrolAreaMin.y, simulatedPatrolAreaMax.y); // Note: using y for Z axis in Vector2
               return new Vector3(randomX, 0f, randomZ); // Assume ground is at Y=0 for simulation
          }

          // --- Restored LoadDummyNpcData method ---
          private void LoadDummyNpcData()
          {
               if (dummyNpcData == null || dummyNpcData.Count == 0)
               {
                    Debug.LogWarning("TiNpcManager: No dummy NPC data entries configured to load.", this);
                    return;
               }

               allTiNpcs.Clear();
               gridManager?.ClearGrid(); // Clear grid before loading new data

               if (basicNpcStateManager == null)
               {
                    Debug.LogError("TiNpcManager: BasicNpcStateManager not found. Cannot initialize dummy TI NPC data with proper basic states or targets for simulation.", this);
                    return; // Cannot load dummy data correctly
               }
               if (gridManager == null)
               {
                    Debug.LogError("TiNpcManager: GridManager not found. Cannot add dummy TI NPC data to the grid.", this);
                    // Continue loading data into allTiNpcs, but grid will be empty. Proximity/Simulation will fail.
               }
               // --- Check WaypointManager for dummy path data initialization ---
               if (waypointManager == null)
               {
                    Debug.LogWarning("TiNpcManager: WaypointManager not found. Dummy NPCs configured with BasicPathState will not have their path data initialized correctly.", this);
                    // Continue loading, but path data will be invalid.
               }

               foreach (var entry in dummyNpcData)
               {
                    if (entry == null) // Added null check for entry
                    {
                         Debug.LogWarning("TiNpcManager: Skipping null dummy NPC data entry.", this);
                         continue;
                    }
                    if (string.IsNullOrWhiteSpace(entry.id))
                    {
                         Debug.LogWarning("TiNpcManager: Skipping dummy NPC entry with empty or whitespace ID.", this);
                         continue;
                    }
                    // --- NEW: Check if prefab is assigned in dummy data ---
                    if (entry.prefab == null)
                    {
                         Debug.LogError($"TiNpcManager: Skipping dummy NPC entry with ID '{entry.id}'. No Prefab assigned in dummy data!", this);
                         continue;
                    }

                    if (allTiNpcs.ContainsKey(entry.id))
                    {
                         Debug.LogWarning($"TiNpcManager: Skipping duplicate dummy NPC entry with ID '{entry.id}'.", this);
                         continue;
                    }

                    // --- Pass the prefab from the dummy entry to the TiNpcData constructor ---
                    TiNpcData newNpcData = new TiNpcData(entry.id, entry.homePosition, entry.homeRotation, entry.prefab);

                    // --- Assign schedule time ranges from dummy data ---
                    newNpcData.startDay = entry.startDay;
                    newNpcData.endDay = entry.endDay;

                    // --- Assign the initial canStartDay flag from dummy data --- // <-- NEW ASSIGNMENT
                    newNpcData.canStartDay = entry.startWithCanStartDay;
                    // --- END NEW ---

                    // --- Assign unique decision options from dummy data ---
                    // Directly copy the serialized list from the dummy entry to the TiNpcData
                    newNpcData.uniqueDecisionOptions.entries = new List<SerializableDecisionOptionDictionary.KeyValuePair>(entry.uniqueDecisionOptions.entries);

                    // --- Assign intended day start behavior from dummy data ---
                    // Assign the new toggle and path fields
                    newNpcData.usePathForDayStart = entry.usePathForDayStart;
                    newNpcData.dayStartActiveStateEnumKey = entry.dayStartActiveStateEnumKey;
                    newNpcData.dayStartActiveStateEnumType = entry.dayStartActiveStateEnumType;
                    newNpcData.dayStartPathID = entry.dayStartPathID;
                    newNpcData.dayStartStartIndex = entry.dayStartStartIndex;
                    newNpcData.dayStartFollowReverse = entry.dayStartFollowReverse;

                    // --- Assign initial pending prescription data from dummy data ---
                    newNpcData.pendingPrescription = entry.startWithPendingPrescription;
                    newNpcData.assignedOrder = entry.initialAssignedOrder; // Assign the dummy order struct

                    // --- Determine Initial State based on Schedule ---
                    Enum initialBasicStateEnum;
                    // --- Check TimeManager dependency for initial state determination ---
                    if (TimeManager.Instance == null)
                    {
                         Debug.LogError($"TiNpcManager: TimeManager instance is null! Cannot determine initial state for dummy NPC '{entry.id}' based on schedule. Defaulting to BasicPatrol.", this);
                         initialBasicStateEnum = BasicState.BasicPatrol; // Fallback if TimeManager is missing
                    }
                    else
                    {
                         DateTime currentTime = TimeManager.Instance.CurrentGameTime; // Get current game time

                         // The pendingPrescription flag does NOT determine the initial state here.
                         // The Decision Point override will handle redirection later.

                         // --- Check both schedule AND canStartDay flag for initial state --- // <-- MODIFIED LOGIC
                         if (newNpcData.startDay.IsWithinRange(currentTime) && newNpcData.canStartDay)
                         {
                              // Day has started AND NPC is allowed to start, determine initial state from the intended day start behavior
                              // Use the refined property getter which handles path vs state logic
                              Enum dayStartActiveState = newNpcData.DayStartActiveStateEnum; // <-- Use refined property

                              if (dayStartActiveState != null)
                              {
                                   initialBasicStateEnum = GetBasicStateFromActiveState(dayStartActiveState); // Map to Basic State
                                   Debug.Log($"TiNpcManager: Dummy NPC '{entry.id}' day has started ({newNpcData.startDay}, Current Time: {currentTime:HH:mm}) AND can start day. Initial Basic State is mapped from Day Start Active State: '{initialBasicStateEnum?.GetType().Name}.{initialBasicStateEnum?.ToString() ?? "NULL"}'.", this);
                                   // --- If starting in a BasicPathState, prime the simulation path data from the dayStart fields ---
                                   // This logic uses the DayStart... fields directly from newNpcData.
                                   if (initialBasicStateEnum != null && initialBasicStateEnum.Equals(BasicPathState.BasicFollowPath))
                                   {
                                        // Use the dayStart fields to initialize the simulated path data
                                        newNpcData.simulatedPathID = newNpcData.DayStartPathID; // <-- Use DayStartPathID
                                        newNpcData.simulatedWaypointIndex = newNpcData.DayStartStartIndex; // <-- Use DayStartStartIndex
                                        newNpcData.simulatedFollowReverse = newNpcData.DayStartFollowReverse; // <-- Use DayStartFollowReverse
                                        newNpcData.isFollowingPathBasic = true; // Flag as following a path simulation
                                        Debug.Log($"TiNpcManager: Priming path simulation data for initial BasicPathState: PathID='{newNpcData.simulatedPathID}', Index={newNpcData.simulatedWaypointIndex}, Reverse={newNpcData.simulatedFollowReverse}.", this);

                                        // Set initial position to the start waypoint's position if path data is valid
                                        if (waypointManager != null && !string.IsNullOrWhiteSpace(newNpcData.simulatedPathID) && newNpcData.simulatedWaypointIndex >= 0)
                                        {
                                             PathSO initialPathSO = waypointManager.GetPath(newNpcData.simulatedPathID);
                                             if (initialPathSO != null && newNpcData.simulatedWaypointIndex < initialPathSO.WaypointCount)
                                             {
                                                  string startWaypointID = initialPathSO.GetWaypointID(newNpcData.simulatedWaypointIndex);
                                                  Transform startWaypointTransform = waypointManager.GetWaypointTransform(startWaypointID);
                                                  if (startWaypointTransform != null)
                                                  {
                                                       newNpcData.CurrentWorldPosition = startWaypointTransform.position;
                                                       // Simulate initial rotation towards the next waypoint (duplicate logic from BasicPathStateSO.OnEnter)
                                                       int nextTargetIndex = newNpcData.simulatedFollowReverse ? newNpcData.simulatedWaypointIndex - 1 : newNpcData.simulatedWaypointIndex + 1;
                                                       bool hasNextWaypoint = newNpcData.simulatedFollowReverse ? (nextTargetIndex >= 0) : (nextTargetIndex < initialPathSO.WaypointCount);
                                                       if (hasNextWaypoint)
                                                       {
                                                            string nextTargetWaypointID = initialPathSO.GetWaypointID(nextTargetIndex);
                                                            Transform nextTargetTransform = waypointManager.GetWaypointTransform(nextTargetWaypointID);
                                                            if (nextTargetTransform != null)
                                                            {
                                                                 Vector3 direction = (nextTargetTransform.position - newNpcData.CurrentWorldPosition).normalized;
                                                                 if (direction.sqrMagnitude > 0.001f)
                                                                 {
                                                                      newNpcData.CurrentWorldRotation = Quaternion.LookRotation(direction);
                                                                 }
                                                            }
                                                       }
                                                       Debug.Log($"TiNpcManager: Initialized position for dummy NPC '{entry.id}' to path start waypoint {newNpcData.CurrentWorldPosition}.", this);
                                                  }
                                                  else
                                                  {
                                                       Debug.LogError($"TiNpcManager: Start waypoint '{startWaypointID}' for initial path '{newNpcData.simulatedPathID}' not found during dummy load! Cannot initialize position. Falling back to BasicPatrol.", this);
                                                       initialBasicStateEnum = BasicState.BasicPatrol; // Fallback state
                                                                                                       // Clear invalid path data
                                                       newNpcData.simulatedPathID = null;
                                                       newNpcData.simulatedWaypointIndex = -1;
                                                       newNpcData.simulatedFollowReverse = false;
                                                       newNpcData.isFollowingPathBasic = false;
                                                       newNpcData.CurrentWorldPosition = newNpcData.HomePosition; // Reset position
                                                       newNpcData.CurrentWorldRotation = newNpcData.HomeRotation; // Reset rotation
                                                  }
                                             }
                                             else
                                             {
                                                  Debug.LogError($"TiNpcManager: Initial PathSO '{newNpcData.simulatedPathID}' not found or index {newNpcData.simulatedWaypointIndex} invalid during dummy load! Cannot initialize path data. Falling back to BasicPatrol.", this);
                                                  initialBasicStateEnum = BasicState.BasicPatrol; // Fallback state
                                                                                                  // Clear invalid path data
                                                  newNpcData.simulatedPathID = null;
                                                  newNpcData.simulatedWaypointIndex = -1;
                                                  newNpcData.simulatedFollowReverse = false;
                                                  newNpcData.isFollowingPathBasic = false;
                                             }
                                        }
                                        else
                                        {
                                             Debug.LogWarning($"TiNpcManager: Dummy NPC '{entry.id}' configured with BasicPathState but missing Path ID or WaypointManager is null. Cannot initialize path data. Falling back to BasicPatrol.", this);
                                             initialBasicStateEnum = BasicState.BasicPatrol; // Fallback state
                                        }
                                    }
                                    else
                                    {
                                        // If not starting in a path state, ensure path simulation data is cleared
                                        newNpcData.simulatedPathID = null;
                                        newNpcData.simulatedWaypointIndex = -1;
                                        newNpcData.simulatedFollowReverse = false;
                                        newNpcData.isFollowingPathBasic = false;
                                        // Position and rotation should be set by the state's OnEnter later
                                    }

                                }
                                else
                                {
                                    // Day start state config is invalid, fallback
                                    // This happens if usePathForDayStart is false but key/type are empty, OR
                                    // if usePathForDayStart is true but PathState.FollowPath cannot be parsed.
                                    Debug.LogError($"TiNpcManager: Dummy NPC '{entry.id}' day has started, but Day Start Active State config is null or invalid! Falling back to BasicPatrol. Check Day Start configuration in Dummy Data.", this); // Added more specific error message
                                    initialBasicStateEnum = BasicState.BasicPatrol;
                                    // Ensure path simulation data is cleared on fallback
                                    newNpcData.simulatedPathID = null;
                                    newNpcData.simulatedWaypointIndex = -1;
                                    newNpcData.simulatedFollowReverse = false;
                                    newNpcData.isFollowingPathBasic = false;
                                }
                            }
                            else
                            {
                                // Day has NOT started OR NPC is NOT allowed to start, initial state is BasicIdleAtHome
                                Debug.Log($"TiNpcManager: Dummy NPC '{entry.id}' day has NOT started ({newNpcData.startDay}, Current Time: {currentTime:HH:mm}) OR cannot start day. Initial Basic State is BasicIdleAtHome.", this); // Updated log
                                initialBasicStateEnum = BasicState.BasicIdleAtHome;
                                // Ensure position/rotation is home and simulation data is cleared when starting in BasicIdleAtHome
                                newNpcData.CurrentWorldPosition = newNpcData.HomePosition;
                                newNpcData.CurrentWorldRotation = newNpcData.HomeRotation;
                                newNpcData.simulatedTargetPosition = null;
                                newNpcData.simulatedStateTimer = 0f;
                                newNpcData.simulatedPathID = null;
                                newNpcData.simulatedWaypointIndex = -1;
                                newNpcData.simulatedFollowReverse = false;
                                newNpcData.isFollowingPathBasic = false;
                            }
                        }

                        // Validate the determined initial state exists as a Basic State SO
                        BasicNpcStateSO initialStateSO = basicNpcStateManager.GetBasicStateSO(initialBasicStateEnum);
                        if (initialStateSO == null)
                        {
                            Debug.LogError($"TiNpcManager: Dummy NPC '{entry.id}' determined initial Basic State '{initialBasicStateEnum}', but no BasicStateSO asset found for this state! Skipping NPC load.", this);
                            continue; // Skip loading this NPC if its initial state is invalid
                        }

                        newNpcData.SetCurrentState(initialBasicStateEnum); // Set the initial Basic State

                        // Call the OnEnter for this initial basic state immediately to set up simulation data (timer, target)
                        // This replicates the OnEnter call that would happen if the NPC transitioned into this state.
                        // Note: BasicPathStateSO.OnEnter handles its own initialization based on isFollowingPathBasic flag.
                        // We only need to call OnEnter if the state is NOT BasicIdleAtHome, because BasicIdleAtHome.OnEnter
                        // has already been implicitly handled by setting position/rotation to home and clearing targets above.
                        // Calling it again would just re-log the same thing.
                        // Also, if the initial state is BasicLookToPrescription, its OnEnter will handle setting up simulation data.
                        if (!initialBasicStateEnum.Equals(BasicState.BasicIdleAtHome)) // <-- Only call OnEnter if not BasicIdleAtHome
                        {
                            initialStateSO.OnEnter(newNpcData, basicNpcStateManager); // Pass the data and the manager
                        }

                        // Flags and GameObject link are initialized in the TiNpcData constructor (isActiveGameObject=false, NpcGameObject=null)
                        // isEndingDay is also initialized in the constructor (false)

                        allTiNpcs.Add(newNpcData.Id, newNpcData);

                        // --- Add the newly loaded NPC data to the grid ---
                        if (gridManager != null)
                        {
                            gridManager.AddItem(newNpcData, newNpcData.CurrentWorldPosition);
                            Debug.Log($"TiNpcManager: Loaded dummy NPC '{newNpcData.Id}' using prefab '{newNpcData.Prefab?.name ?? "NULL"}' with initial Basic State '{initialBasicStateEnum}'. Schedule: {newNpcData.startDay} to {newNpcData.endDay}. Can Start Day: {newNpcData.canStartDay}. Unique Options: {newNpcData.uniqueDecisionOptions.entries.Count}. Simulation timer initialized to {newNpcData.simulatedStateTimer:F2}s, Target: {newNpcData.simulatedTargetPosition?.ToString() ?? "NULL"}. Path Data: Following={newNpcData.isFollowingPathBasic}, ID='{newNpcData.simulatedPathID}', Index={newNpcData.simulatedWaypointIndex}, Reverse={newNpcData.simulatedFollowReverse}. Pending Prescription: {newNpcData.pendingPrescription}. Added to grid.", this); // Updated log
                        }
                        else
                        {
                            Debug.Log($"TiNpcManager: Loaded dummy NPC '{newNpcData.Id}' using prefab '{newNpcData.Prefab?.name ?? "NULL"}' with initial Basic State '{initialBasicStateEnum}'. Schedule: {newNpcData.startDay} to {newNpcData.endDay}. Can Start Day: {newNpcData.canStartDay}. Unique Options: {newNpcData.uniqueDecisionOptions.entries.Count}. Simulation timer initialized to {newNpcData.simulatedStateTimer:F2}s, Target: {newNpcData.simulatedTargetPosition?.ToString() ?? "NULL"}. Path Data: Following={newNpcData.isFollowingPathBasic}, ID='{newNpcData.simulatedPathID}', Index={newNpcData.simulatedWaypointIndex}, Reverse={newNpcData.simulatedFollowReverse}. Pending Prescription: {newNpcData.pendingPrescription}. GridManager is null, NOT added to grid.", this); // Updated log
                        }
                    }
                }
            }
        }