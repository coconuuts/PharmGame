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
using Game.Prescriptions; // Needed for PrescriptionManager, PrescriptionOrder


namespace Game.NPC.TI // Keep in the TI namespace
{
     /// <summary>
     /// Manages the persistent data for True Identity (TI) NPCs.
     /// Handles loading, storing, and tracking TI NPC data independent of their GameObjects.
     /// Implements off-screen simulation logic (delegating to BasicNpcStateManager)
     /// and provides methods for ProximityManager to trigger activation/deactivation.
     /// Uses a single collection of data records and filters on the fly.
     /// Now includes mappings for path following states, loads schedule time ranges,
     /// loads unique decision options, configures intended day start behavior,
     /// and handles activation/deactivation logic.
     /// MODIFIED: Added specific activation logic for BasicWaitForCashier, BasicBrowse, BasicPathState, BasicWaitingAtPrescriptionSpot, BasicWaitingForDeliverySim states.
     /// MODIFIED: Saves the mapped basic state for all active states, including prescription waiting/delivery.
     /// MODIFIED: Updated state mappings to include new basic states for prescription waiting/delivery.
     /// MODIFIED: Added public method to get list of all TI NPC IDs.
     /// MODIFIED: Removed the shared tiNpcPrefabs list.
     /// ADDED: Prefab field to DummyTiNpcDataEntry.
     /// MODIFIED: RequestActivateTiNpc now uses the prefab stored in TiNpcData.
     /// MODIFIED: Removed validation for the old shared prefab list.
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


          [Header("Simulation Settings")] // Renamed header slightly
          [Tooltip("The interval (in seconds) between simulation ticks for inactive NPCs.")]
          [SerializeField] private float simulationTickInterval = 0.1f; // Process a batch every 0.1 seconds (10 Hz)
          [Tooltip("The maximum number of inactive NPCs to simulate per tick.")]
          [SerializeField] private int maxNpcsToSimulatePerTick = 10; // Process 10 NPCs per tick


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

          // Internal index for round-robin simulation batching
          private int simulationIndex = 0;

          // Coroutine references
          private Coroutine simulationCoroutine;


          // --- State Mapping Dictionaries ---
          private Dictionary<Enum, Enum> activeToBaseStateMap;
          private Dictionary<Enum, Enum> basicToActiveStateMap;

          // --- Public Accessor for WaypointManager ---
          public WaypointManager WaypointManager => waypointManager; // <-- Public getter


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

               // Setup State Mappings
               SetupStateMappings();

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

               // Load Dummy Data
               LoadDummyNpcData();

               Debug.Log($"TiNpcManager: Started. Loaded {allTiNpcs.Count} TI NPCs.");

               Debug.Log($"TiNpcManager: Start completed.");

               // Start the simulation loop if BasicNpcStateManager and GridManager are available
               if (basicNpcStateManager != null && gridManager != null && playerTransform != null) // Simulation needs player position for query
               {
                    simulationCoroutine = StartCoroutine(SimulateInactiveNpcsRoutine());
                    Debug.Log("TiNpcManager: Simulation coroutine started.");
               }
               else
               {
                    Debug.LogWarning("TiNpcManager: BasicNpcStateManager, GridManager, or Player Transform is null. Cannot start simulation coroutine."); // Updated log
               }

          }

          private void OnEnable()
          {
               // Restart simulation coroutine if manager was disabled and re-enabled AND dependencies exist
               if (simulationCoroutine == null && allTiNpcs.Count > 0 && basicNpcStateManager != null && gridManager != null && playerTransform != null) // Added dependencies
               {
                    Debug.Log("TiNpcManager: Restarting simulation coroutine on OnEnable.");
                    simulationCoroutine = StartCoroutine(SimulateInactiveNpcsRoutine());
               }
          }

          private void OnDisable()
          {
               // Stop simulation coroutine
               if (simulationCoroutine != null)
               {
                    Debug.Log("TiNpcManager: Stopping simulation coroutine on OnDisable.");
                    StopCoroutine(simulationCoroutine);
                    simulationCoroutine = null;
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

          // --- DEBUG: Draw gizmos for inactive NPCs ---
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
          // --- END DEBUG ---

          /// <summary>
          /// Sets up the mapping dictionaries between active and basic states.
          /// </summary>
          private void SetupStateMappings()
          {
               activeToBaseStateMap = new Dictionary<Enum, Enum>();
               basicToActiveStateMap = new Dictionary<Enum, Enum>();

               // Define the mappings based on the plan

               // Active -> Basic mappings
               // General States
               activeToBaseStateMap[GeneralState.Idle] = BasicState.BasicPatrol; // Assume general idle/non-customer states map to patrol
               activeToBaseStateMap[GeneralState.Emoting] = BasicState.BasicPatrol; // Assume these interruptions resume patrol when inactive
               activeToBaseStateMap[GeneralState.Social] = BasicState.BasicPatrol;
               activeToBaseStateMap[GeneralState.Combat] = BasicState.BasicPatrol; // Combatting NPC when inactive -> patrol
                                                                                   // Note: GeneralState.Initializing, GeneralState.ReturningToPool, GeneralState.Death are terminal or transient and don't map to simulation states

               // Test States
               activeToBaseStateMap[TestState.Patrol] = BasicState.BasicPatrol;

               // Customer States
               activeToBaseStateMap[CustomerState.LookingToShop] = BasicState.BasicLookToShop;
               activeToBaseStateMap[CustomerState.Entering] = BasicState.BasicEnteringStore;
               activeToBaseStateMap[CustomerState.Browse] = BasicState.BasicBrowse;
               activeToBaseStateMap[CustomerState.MovingToRegister] = BasicState.BasicWaitForCashier; // Collapse multiple active states to one basic state
               activeToBaseStateMap[CustomerState.WaitingAtRegister] = BasicState.BasicWaitForCashier;
               activeToBaseStateMap[CustomerState.Queue] = BasicState.BasicWaitForCashier;
               activeToBaseStateMap[CustomerState.SecondaryQueue] = BasicState.BasicExitingStore; // Secondary queue maps to Exiting simulation (giving up)
               activeToBaseStateMap[CustomerState.Exiting] = BasicState.BasicExitingStore;
               // CustomerState.Inactive and CustomerState.TransactionActive are not mapped

               activeToBaseStateMap[PathState.FollowPath] = BasicPathState.BasicFollowPath;

               // Cashier States
               activeToBaseStateMap[CashierState.CashierMovingToCashSpot] = BasicState.BasicCashierMovingToCashSpot;
               activeToBaseStateMap[CashierState.CashierWaitingForCustomer] = BasicState.BasicCashierWaitingForCustomer;
               activeToBaseStateMap[CashierState.CashierProcessingCheckout] = BasicState.BasicCashierProcessingCheckout;
               activeToBaseStateMap[CashierState.CashierGoingHome] = BasicPathState.BasicFollowPath;

               // Prescription State Mappings ---
               // These mappings are STILL needed for Transient NPCs and for TI NPCs *before* they hit a Decision Point
               // if they somehow ended up in these states via old logic or specific non-DecisionPoint transitions.
               activeToBaseStateMap[CustomerState.LookToPrescription] = BasicState.BasicLookToPrescription;
               activeToBaseStateMap[CustomerState.PrescriptionEntering] = BasicState.BasicWaitForPrescription; // Map to BasicWaitForPrescription (queue sim)
               activeToBaseStateMap[CustomerState.PrescriptionQueue] = BasicState.BasicWaitForPrescription; // Map to BasicWaitForPrescription (queue sim)
               activeToBaseStateMap[CustomerState.WaitingForPrescription] = BasicState.BasicWaitingAtPrescriptionSpot; // Map active WaitingForPrescription to new basic state
               activeToBaseStateMap[CustomerState.WaitingForDelivery] = BasicState.BasicWaitingForDeliverySim; // Map new basic state back to active WaitingForDelivery

               // Basic -> Active mappings
               basicToActiveStateMap[BasicState.BasicPatrol] = TestState.Patrol;
               basicToActiveStateMap[BasicState.BasicLookToShop] = CustomerState.LookingToShop;
               basicToActiveStateMap[BasicState.BasicEnteringStore] = CustomerState.Entering;
               basicToActiveStateMap[BasicState.BasicWaitForCashier] = CustomerState.Queue; // This is the DEFAULT mapping, can be overridden during activation for specific logic.
               basicToActiveStateMap[BasicState.BasicExitingStore] = CustomerState.Exiting;
               basicToActiveStateMap[BasicState.BasicIdleAtHome] = GeneralState.Idle; // Map to Active Idle as a fallback
               basicToActiveStateMap[BasicPathState.BasicFollowPath] = PathState.FollowPath;
               basicToActiveStateMap[BasicState.BasicLookToPrescription] = CustomerState.LookToPrescription;
               basicToActiveStateMap[BasicState.BasicWaitForPrescription] = CustomerState.PrescriptionQueue; // Map back to the queue state as the default
               basicToActiveStateMap[BasicState.BasicWaitingAtPrescriptionSpot] = CustomerState.WaitingForPrescription; // Map new basic state back to active WaitingForPrescription
               basicToActiveStateMap[BasicState.BasicWaitingForDeliverySim] = CustomerState.WaitingForDelivery; // Map new basic state back to active WaitingForDelivery
               basicToActiveStateMap[BasicState.BasicCashierMovingToCashSpot] = CashierState.CashierMovingToCashSpot;
               basicToActiveStateMap[BasicState.BasicCashierWaitingForCustomer] = CashierState.CashierWaitingForCustomer;
               basicToActiveStateMap[BasicState.BasicCashierProcessingCheckout] = CashierState.CashierProcessingCheckout;

               Debug.Log($"TiNpcManager: State mappings setup. Active->Basic: {activeToBaseStateMap.Count}, Basic->Active: {basicToActiveStateMap.Count}");
          }


          /// <summary>
          /// Gets the corresponding Basic State enum for a given Active State enum.
          /// Returns BasicState.BasicPatrol if no direct mapping is found (fallback for unmapped active states).
          /// </summary>
          public Enum GetBasicStateFromActiveState(Enum activeStateEnum)
          {
               if (activeStateEnum == null)
               {
                    Debug.LogWarning($"TiNpcManager: GetBasicStateFromActiveState called with null activeStateEnum. Falling back to BasicPatrol.");
                    return BasicState.BasicPatrol; // Safe default if input is null
               }

               if (activeToBaseStateMap.TryGetValue(activeStateEnum, out Enum basicStateEnum))
               {
                    return basicStateEnum;
               }

               // Fallback for active states that don't have a defined mapping (e.g., unlisted states, future states)
               // Assume these should default to patrolling when inactive.
               Debug.LogWarning($"TiNpcManager: No Basic State mapping found for Active State '{activeStateEnum.GetType().Name}.{activeStateEnum.ToString() ?? "NULL"}'. Falling back to BasicPatrol.");
               return BasicState.BasicPatrol; // Default fallback for unmapped active states
          }

          /// <summary>
          /// Gets the corresponding Active State enum for a given Basic State enum.
          /// Returns GeneralState.Idle if no direct mapping is found (should not happen with correct setup).
          /// </summary>
          public Enum GetActiveStateFromBasicState(Enum basicStateEnum)
          {
               if (basicStateEnum == null)
               {
                    Debug.LogWarning($"TiNpcManager: GetActiveStateFromBasicState called with null basicStateEnum. Falling back to GeneralState.Idle.");
                    return GeneralState.Idle; // Safe active fallback if input is null
               }

               if (basicToActiveStateMap.TryGetValue(basicStateEnum, out Enum activeStateEnum))
               {
                    return activeStateEnum;
               }

               // Error if a Basic State doesn't have a mapping back to an Active State
               Debug.LogError($"TiNpcManager: No Active State mapping found for Basic State '{basicStateEnum.GetType().Name}.{basicStateEnum.ToString() ?? "NULL"}'! Returning GeneralState.Idle as fallback. Review mappings!");
               return GeneralState.Idle; // Error fallback
          }


          /// <summary>
          /// The low-tick simulation routine. Iterates over inactive NPCs found near the player via the grid,
          /// AND inactive NPCs currently following a path simulation regardless of distance,
          /// delegating simulation logic to the BasicNpcStateManager.
          /// </summary>
          private IEnumerator SimulateInactiveNpcsRoutine()
          {
               while (true)
               {
                    yield return new WaitForSeconds(simulationTickInterval);

                    if (basicNpcStateManager == null)
                    {
                         Debug.LogError("SIM TiNpcManager: BasicNpcStateManager is null! Cannot simulate inactive NPCs.", this);
                         yield return new WaitForSeconds(simulationTickInterval * 5);
                         continue;
                    }
                    if (gridManager == null)
                    {
                         Debug.LogError("SIM TiNpcManager: GridManager is null! Cannot efficiently find inactive NPCs for simulation.", this);
                         yield return new WaitForSeconds(simulationTickInterval * 5); // Cannot simulate efficiently without grid
                         continue;
                    }
                    if (playerTransform == null)
                    {
                         Debug.LogError("SIM TiNpcManager: Player Transform is null! Cannot query grid for simulation batch.", this);
                         yield return new WaitForSeconds(simulationTickInterval * 5);
                         continue;
                    }
                    if (waypointManager == null) // Check WaypointManager dependency for path simulation
                    {
                         // Simulation for non-path states can continue, but path states will fallback internally.
                         // No need to yield longer here, just log the specific issue.
                         Debug.LogWarning("SIM TiNpcManager: WaypointManager not found. Simulation of path following for inactive NPCs may not work correctly.");
                    }
                    // --- Check TimeManager dependency for schedule checks ---
                    if (TimeManager.Instance == null)
                    {
                         Debug.LogError("SIM TiNpcManager: TimeManager instance is null! Cannot perform schedule checks during simulation.", this);
                         yield return new WaitForSeconds(simulationTickInterval * 5);
                         continue; // Cannot proceed
                    }
                    DateTime currentTime = TimeManager.Instance.CurrentGameTime; // Get current game time

                    // --- Build the list of NPCs to simulate this tick ---
                    List<TiNpcData> simulationCandidates = new List<TiNpcData>();
                    HashSet<TiNpcData> addedToBatch = new HashSet<TiNpcData>(); // Use a set to prevent duplicates

                    // 1. Find inactive NPCs near the player using the grid (Standard Proximity Simulation)
                    // Query a radius slightly larger than the farthest zone radius to catch NPCs just outside.
                    float simulationQueryRadius = (proximityManager != null ? proximityManager.farRadius : 30f) + (gridManager != null ? gridManager.cellSize : 5f); // Query slightly beyond far radius
                    Vector3 playerPosition = playerTransform.position;

                    if (gridManager != null) // Ensure gridManager is available for query
                    {
                         List<TiNpcData> nearbyInactiveNpcs = gridManager.QueryItemsInRadius(playerPosition, simulationQueryRadius)
                              .Where(data => !data.IsActiveGameObject) // Filter for inactive only
                              .ToList();

                         foreach (var data in nearbyInactiveNpcs)
                         {
                              if (addedToBatch.Add(data)) // Add to set and list if not already present
                              {
                                   simulationCandidates.Add(data);
                              }
                         }
                    }
                    else
                    {
                         // Fallback for simulation if GridManager is missing (very inefficient, should not happen in final build)
                         // Iterate ALL inactive NPCs and check distance (only if GridManager is null)
                         Debug.LogError("SIM TiNpcManager: GridManager is null, falling back to inefficient distance check for nearby inactive NPCs!");
                         foreach (var data in allTiNpcs.Values)
                         {
                              if (!data.IsActiveGameObject)
                              {
                                   Vector3 npcPosition = data.CurrentWorldPosition; // Use saved data position
                                   float distanceToPlayerSq = (npcPosition - playerPosition).sqrMagnitude;
                                   if (distanceToPlayerSq <= simulationQueryRadius * simulationQueryRadius)
                                   {
                                        if (addedToBatch.Add(data))
                                        {
                                             simulationCandidates.Add(data);
                                        }
                                   }
                              }
                         }
                    }


                    // 2. Find inactive NPCs that MUST be simulated (e.g., path following) regardless of proximity
                    // Iterate over *all* known TI NPCs
                    foreach (var data in allTiNpcs.Values)
                    {
                         // Include any inactive NPC that has a simulated target position set.
                         bool isInactiveSimulatingMovement = !data.IsActiveGameObject && data.simulatedTargetPosition.HasValue;

                         if (isInactiveSimulatingMovement)
                         {
                              // Add to the batch if not already added (e.g., was also nearby)
                              if (addedToBatch.Add(data))
                              {
                                   simulationCandidates.Add(data);
                              }
                         }
                    }
                    // --- Finished building the list of NPCs to simulate ---


                    int totalToSimulate = simulationCandidates.Count;

                    if (totalToSimulate == 0)
                    {
                         // If no candidates, just wait for the next interval
                         yield return new WaitForSeconds(simulationTickInterval * 2); // Wait a bit longer if nothing is happening
                         simulationIndex = 0; // Reset index if list is empty
                         continue;
                    }

                    // Wrap the simulation index if it exceeds the total number of simulation candidates
                    if (simulationIndex >= totalToSimulate)
                    {
                         simulationIndex = 0;
                    }

                    // Get the batch from the combined list of simulation candidates
                    // Ensure we handle wrapping around the end of the list
                    List<TiNpcData> currentBatch;
                    if (simulationIndex + maxNpcsToSimulatePerTick <= totalToSimulate)
                    {
                         currentBatch = simulationCandidates.Skip(simulationIndex).Take(maxNpcsToSimulatePerTick).ToList();
                    }
                    else
                    {
                         int remainingInList = totalToSimulate - simulationIndex;
                         int neededFromStart = maxNpcsToSimulatePerTick - remainingInList;
                         currentBatch = simulationCandidates.Skip(simulationIndex).Take(remainingInList).ToList();
                         currentBatch.AddRange(simulationCandidates.Take(neededFromStart)); // Add from the start to complete the batch
                    }


                    if (currentBatch.Count == 0 && totalToSimulate > 0)
                    {
                         // This case can happen if totalToSimulate is > 0 but < maxNpcsToSimulatePerTick
                         // and simulationIndex is totalToSimulate or more. The batching logic above should handle this correctly now.
                         // If we reach here, it might indicate an issue or just need a reset.
                         Debug.LogWarning($"SIM TiNpcManager: Generated an empty batch ({currentBatch.Count}) despite {totalToSimulate} candidates available! Resetting index.");
                         simulationIndex = 0; // Force reset index
                         continue; // Skip this tick's simulation and wait for the next interval
                    }
                    else if (currentBatch.Count == 0)
                    {
                         // totalToSimulate must be 0 here. Covered by the check above.
                         simulationIndex = 0; // Reset index
                         yield return new WaitForSeconds(simulationTickInterval * 2); // Wait a bit longer
                         continue; // Go to next iteration
                    }


                    // Process the batch
                    int countProcessedThisTick = 0;
                    int batchSize = currentBatch.Count; // Use batch size for index advancement

                    foreach (var npcData in currentBatch) // Iterate over the temporary batch list
                    {
                         // This check is crucial to confirm if an active NPC is mistakenly in this list (should not happen with filtering)
                         if (npcData.IsActiveGameObject)
                         {
                              Debug.LogError($"SIM TiNpcManager: Found ACTIVE NPC '{npcData.Id}' (GameObject '{npcData.NpcGameObject?.name ?? "NULL"}') in the INACTIVE simulation batch! This should not happen. Skipping simulation for this NPC.", npcData.NpcGameObject);
                              countProcessedThisTick++; // Still count this entry as processed in the batch to advance index correctly
                              continue; // Skip simulation logic for this NPC
                         }
                         // Ensure the state is valid for simulation before ticking
                         BasicNpcStateSO currentStateSO = basicNpcStateManager?.GetBasicStateSO(npcData.CurrentStateEnum);
                         if (currentStateSO == null)
                         {
                              // This is the error case we are fixing. The saved state is an Active state enum.
                              Debug.LogError($"SIM {npcData.Id}: Current Basic State SO not found for Enum '{npcData.CurrentStateEnum?.GetType().Name}.{npcData.CurrentStateEnum?.ToString() ?? "NULL"}' during simulation tick! This is likely an Active state enum saved incorrectly. Attempting to map to Basic State and transition.", npcData.NpcGameObject);

                              // Attempt to map the saved Active state enum to a Basic state enum
                              Enum mappedBasicState = GetBasicStateFromActiveState(npcData.CurrentStateEnum);

                              if (mappedBasicState != null)
                              {
                                   Debug.LogWarning($"SIM {npcData.Id}: Successfully mapped saved Active state '{npcData.CurrentStateEnum?.ToString() ?? "NULL"}' to Basic state '{mappedBasicState.ToString()}'. Transitioning to mapped basic state.", npcData.NpcGameObject);
                                   // Transition to the correctly mapped basic state
                                   basicNpcStateManager.TransitionToBasicState(npcData, mappedBasicState);
                                   // The new state's logic will run on the *next* simulation tick for this NPC.
                                   countProcessedThisTick++; // Count this entry
                                   continue; // Skip simulation logic for this NPC this tick
                              }
                              else
                              {
                                   // Mapping failed, fallback to BasicPatrol
                                   Debug.LogError($"SIM {npcData.Id}: Failed to map saved state '{npcData.CurrentStateEnum?.ToString() ?? "NULL"}' to any Basic state. Transitioning to BasicPatrol (fallback).", npcData.NpcGameObject);
                                   basicNpcStateManager.TransitionToBasicState(npcData, BasicState.BasicPatrol); // Use BasicState enum directly
                                                                                                                 // Note: TransitionToBasicState logs errors if BasicPatrol isn't found either.
                                   countProcessedThisTick++; // Count this entry
                                   continue; // Skip simulation logic for this NPC this tick
                              }
                         }

                         // --- Check if StartDay has begun and transition from BasicIdleAtHome ---
                         if (npcData.CurrentStateEnum != null && npcData.CurrentStateEnum.Equals(BasicState.BasicIdleAtHome))
                         {
                              if (npcData.startDay.IsWithinRange(currentTime))
                              {
                                   Debug.Log($"SIM {npcData.Id}: StartDay has begun ({npcData.startDay}, Current Time: {currentTime:HH:mm}). Transitioning from BasicIdleAtHome to day start state.", npcData.NpcGameObject);

                                   // Get the intended Active start state from TiData (uses the refined property)
                                   Enum dayStartActiveState = npcData.DayStartActiveStateEnum;

                                   if (dayStartActiveState != null)
                                   {
                                        // Map the Active state to its Basic equivalent for simulation
                                        Enum dayStartBasicState = GetBasicStateFromActiveState(dayStartActiveState);

                                        if (dayStartBasicState != null)
                                        {
                                             // --- If the day start state is a BasicPathState, prime the simulation path data from the dayStart fields ---
                                             // This logic uses the DayStart... fields directly from TiNpcData.
                                             if (dayStartBasicState.Equals(BasicPathState.BasicFollowPath))
                                             {
                                                  // Use the dayStart fields to initialize the simulated path data
                                                  npcData.simulatedPathID = npcData.DayStartPathID; // <-- Use DayStartPathID
                                                  npcData.simulatedWaypointIndex = npcData.DayStartStartIndex; // <-- Use DayStartStartIndex
                                                  npcData.simulatedFollowReverse = npcData.DayStartFollowReverse; // <-- Use DayStartFollowReverse
                                                  npcData.isFollowingPathBasic = true; // Flag as following a path simulation
                                                  Debug.Log($"SIM {npcData.Id}: Priming path simulation data for BasicPathState transition: PathID='{npcData.simulatedPathID}', Index={npcData.simulatedWaypointIndex}, Reverse={npcData.simulatedFollowReverse}.", npcData.NpcGameObject);
                                             }
                                             else
                                             {
                                                  // If not transitioning to a path state, ensure path simulation data is cleared
                                                  npcData.simulatedPathID = null;
                                                  npcData.simulatedWaypointIndex = -1;
                                                  npcData.simulatedFollowReverse = false;
                                                  npcData.isFollowingPathBasic = false;
                                             }

                                             // Trigger the state transition to the determined basic state
                                             basicNpcStateManager.TransitionToBasicState(npcData, dayStartBasicState);

                                             // Continue to the next NPC in the batch. The new state's logic
                                             // will run on the *next* simulation tick for this NPC.
                                             countProcessedThisTick++;
                                             continue; // Skip the rest of the tick logic for this NPC this frame
                                        }
                                        else
                                        {
                                             Debug.LogError($"SIM {npcData.Id}: Could not map intended Day Start Active State '{dayStartActiveState.GetType().Name}.{dayStartActiveState.ToString() ?? "NULL"}' to a Basic State! Transitioning to BasicPatrol fallback.", npcData.NpcGameObject);
                                             basicNpcStateManager.TransitionToBasicState(npcData, BasicState.BasicPatrol);
                                             countProcessedThisTick++;
                                             continue;
                                        }
                                   }
                                   else
                                   {
                                        // This is the error case the user reported. The DayStartActiveStateEnum property is null.
                                        // This happens if usePathForDayStart is false but key/type are empty, OR
                                        // if usePathForDayStart is true but PathState.FollowPath cannot be parsed.
                                        Debug.LogError($"SIM {npcData.Id}: Intended Day Start Active State is null or invalid! Cannot transition from BasicIdleAtHome. Transitioning to BasicPatrol fallback. Check Day Start configuration in Dummy Data.", npcData.NpcGameObject); // Added more specific error message
                                        basicNpcStateManager.TransitionToBasicState(npcData, BasicState.BasicPatrol);
                                        countProcessedThisTick++;
                                        continue;
                                   }
                              }
                              // If in BasicIdleAtHome but day hasn't started, do nothing and continue to next NPC
                              countProcessedThisTick++; // Still count as processed in batch
                              continue; // Skip the rest of the tick logic for this NPC this frame
                         }

                         // --- DELEGATE SIMULATION TO BASICNPCSTATEMANAGER ---
                         // BasicNpcStateManager will handle calling GridManager.UpdateItemPosition after simulation tick
                         // This only happens if the NPC was NOT in BasicIdleAtHome or transitioned out of it this tick.
                         basicNpcStateManager.SimulateTickForNpc(npcData, simulationTickInterval);
                         // --- END DELEGATION ---

                         countProcessedThisTick++;
                    }

                    // Advance the simulation index by the number of NPCs *actually processed* in this batch.
                    // This must be done *after* iterating the batch.
                    simulationIndex += countProcessedThisTick; // Use the actual count processed

                    // Wrap the simulation index based on the total number of *simulation candidates* found in this tick.
                    //totalToSimulate is the count determined *before* batching.
                    if (totalToSimulate > 0)
                    {
                         simulationIndex %= totalToSimulate;
                    }
                    else
                    {
                         simulationIndex = 0; // Reset if no candidates were found this tick
                    }

                    // Log simulation tick summary (Optional, can be noisy)
                    // Debug.Log($"SIM TiNpcManager: Simulated {countProcessedThisTick} inactive NPCs this tick from {totalToSimulate} candidates. Next batch starts at index {simulationIndex}.");
               }
          }


          /// <summary>
          /// Handles the process of activating a single TI NPC.
          /// Gets a pooled GameObject and calls the Runner's Activate method.
          /// Called by ProximityManager.
          /// MODIFIED: Added specific handling for BasicWaitForCashier, BasicBrowse, BasicPathState, and NEW BasicWaitingAtPrescriptionSpot/BasicWaitingForDeliverySim states.
          /// MODIFIED: Now uses the prefab stored in TiNpcData instead of a shared list.
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

               if (poolingManager == null || customerManager == null || basicNpcStateManager == null || gridManager == null || waypointManager == null || prescriptionManager == null || cashierManager == null)
               {
                    Debug.LogError("TiNpcManager: Cannot activate TI NPC. Required manager (PoolingManager, CustomerManager, BasicNpcStateManager, GridManager, WaypointManager, or PrescriptionManager) is null.", this); // Updated log
                    return;
               }
               // --- Check TimeManager dependency for schedule checks ---
               if (TimeManager.Instance == null)
               {
                    Debug.LogError("PROXIMITY TiNpcManager: TimeManager instance is null! Cannot perform schedule checks during activation.", this);
                    return; // Cannot proceed
               }
               DateTime currentTime = TimeManager.Instance.CurrentGameTime; // Get current game time


               // --- Use the specific prefab from TiData to get from the pool ---
               GameObject npcObject = poolingManager.GetPooledObject(prefabToUse);
               // --- END Use specific prefab ---


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
                         // The saved state should now always be a BasicState enum or null.
                         Enum savedBasicStateEnum = tiData.CurrentStateEnum;
                         Enum startingActiveStateEnum = null; // The active state we will transition to


                         // --- Handle activation based on the saved BasicStateEnum ---

                         if (savedBasicStateEnum != null && savedBasicStateEnum.Equals(BasicState.BasicIdleAtHome))
                         {
                              Debug.Log($"PROXIMITY {tiData.Id}: Saved Basic state is BasicIdleAtHome. Checking schedule for activation.", npcObject);

                              if (tiData.startDay.IsWithinRange(currentTime))
                              {
                                   // Day has started, activate to the intended day start state (uses the refined property)
                                   Debug.Log($"PROXIMITY {tiData.Id}: Day has started ({tiData.startDay}, Current Time: {currentTime:HH:mm}). Activating to intended day start Active state.", npcObject);
                                   startingActiveStateEnum = tiData.DayStartActiveStateEnum;

                                   // --- If activating into a PathState, prime the TiData's simulated path data from the dayStart fields ---
                                   // This logic uses the DayStart... fields directly from TiNpcData.
                                   if (startingActiveStateEnum != null && startingActiveStateEnum.Equals(PathState.FollowPath))
                                   {
                                        // Use the dayStart fields to initialize the simulated path data on TiData
                                        // PathStateSO.OnEnter will read these directly from TiData when IsTrueIdentityNpc is true
                                        tiData.simulatedPathID = tiData.DayStartPathID; // <-- Use DayStartPathID
                                        tiData.simulatedWaypointIndex = tiData.DayStartStartIndex; // <-- Use DayStartStartIndex
                                        tiData.simulatedFollowReverse = tiData.DayStartFollowReverse; // <-- Use DayStartFollowReverse
                                        tiData.isFollowingPathBasic = true; // Flag to tell PathStateSO.OnEnter to restore

                                        Debug.Log($"PROXIMITY {tiData.Id}: Primed TiData for PathState activation from DayStart: PathID='{tiData.simulatedPathID}', Index={tiData.simulatedWaypointIndex}, Reverse={tiData.simulatedFollowReverse}.", npcObject);
                                   }

                                   // Clear all NON-PATH simulation data as active state takes over
                                   tiData.simulatedTargetPosition = null;
                                   tiData.simulatedStateTimer = 0f;
                                   // Note: simulatedPathID, simulatedWaypointIndex, simulatedFollowReverse, isFollowingPathBasic
                                   // are NOT cleared here if activating into a PathState, as they are needed by PathStateSO.OnEnter.
                                   // They ARE cleared if activating into a non-PathState (handled by the else block below).

                              }
                              else
                              {
                                   // Day has NOT started, NPC should remain idle at home even if activated.
                                   Debug.Log($"PROXIMITY {tiData.Id}: Day has NOT started ({tiData.startDay}, Current Time: {currentTime:HH:mm}). Activating to Active Idle state.", npcObject);
                                   startingActiveStateEnum = GeneralState.Idle; // Activate to Active Idle state

                                   // Clear all simulation data as active state takes over
                                   tiData.simulatedTargetPosition = null;
                                   tiData.simulatedStateTimer = 0f;
                                   tiData.simulatedPathID = null;
                                   tiData.simulatedWaypointIndex = -1;
                                   tiData.simulatedFollowReverse = false;
                                   tiData.isFollowingPathBasic = false;
                              }
                         }

                         // --- Handle activation from BasicWaitForCashierState ---
                         else if (savedBasicStateEnum != null && savedBasicStateEnum.Equals(BasicState.BasicWaitForCashier))
                         {
                              Debug.Log($"PROXIMITY {tiData.Id}: Saved Basic state is BasicWaitForCashier (Queue Sim). Checking live queue/register status.", npcObject);

                              // Check register occupancy
                              if (customerManager.IsRegisterOccupied() == false)
                              {
                                   // Register is free, go straight there
                                   Debug.Log($"PROXIMITY {tiData.Id}: Register is free. Activating to MovingToRegister state.", npcObject);
                                   startingActiveStateEnum = CustomerState.MovingToRegister;
                                   // Clear simulation data as active state takes over
                                   tiData.simulatedTargetPosition = null;
                                   tiData.simulatedStateTimer = 0f;
                                   // Clear path simulation data
                                   tiData.simulatedPathID = null;
                                   tiData.simulatedWaypointIndex = -1;
                                   tiData.simulatedFollowReverse = false;
                                   tiData.isFollowingPathBasic = false;
                              }
                              else
                              {
                                   // Register is busy, try to join the main queue
                                   Debug.Log($"PROXIMITY {tiData.Id}: Register is busy. Attempting to join main queue.", npcObject);

                                   // Need the Runner's QueueHandler to configure it *before* the state transition
                                   NpcQueueHandler queueHandler = runner.QueueHandler; // Get handler reference
                                   if (queueHandler != null)
                                   {
                                        Transform assignedSpotTransform;
                                        int assignedSpotIndex;

                                        if (customerManager.TryJoinQueue(runner, out assignedSpotTransform, out assignedSpotIndex))
                                        {
                                             // Successfully joined queue, setup the handler and transition to Queue state
                                             Debug.Log($"PROXIMITY {tiData.Id}: Successfully rejoined main queue at spot {assignedSpotIndex}. Activating to Queue state.", npcObject);
                                             // Use SetupQueueSpot method to configure the handler and runner target
                                             queueHandler.SetupQueueSpot(assignedSpotTransform, assignedSpotIndex, QueueType.Main);
                                             startingActiveStateEnum = CustomerState.Queue;
                                             // Clear simulation data as active state takes over
                                             tiData.simulatedTargetPosition = null; // Clear simulated target
                                             tiData.simulatedStateTimer = 0f; // Reset timer
                                             // Clear path simulation data
                                             tiData.simulatedPathID = null;
                                             tiData.simulatedWaypointIndex = -1;
                                             tiData.simulatedFollowReverse = false;
                                             tiData.isFollowingPathBasic = false;

                                        }
                                        else
                                        {
                                             // Main queue is full, cannot be a customer right now
                                             Debug.Log($"PROXIMITY {tiData.Id}: Main queue is full. Cannot join. Activating to Exiting state.", npcObject);
                                             startingActiveStateEnum = CustomerState.Exiting; // Give up on shopping
                                                                                              // Clear simulation data as active state takes over
                                             tiData.simulatedTargetPosition = null;
                                             tiData.simulatedStateTimer = 0f;
                                             // Clear path simulation data
                                             tiData.simulatedPathID = null;
                                             tiData.simulatedWaypointIndex = -1;
                                             tiData.simulatedFollowReverse = false;
                                             tiData.isFollowingPathBasic = false;
                                        }
                                   }
                                   else
                                   {
                                        // QueueHandler missing - critical error for queue state
                                        Debug.LogError($"PROXIMITY {tiData.Id}: Runner is missing NpcQueueHandler component during BasicWaitForCashier activation! Cannot handle queue logic. Activating to Exiting as fallback.", npcObject);
                                        startingActiveStateEnum = CustomerState.Exiting; // Fallback
                                                                                         // Clear simulation data
                                        tiData.simulatedTargetPosition = null;
                                        tiData.simulatedStateTimer = 0f;
                                        // Clear path simulation data
                                        tiData.simulatedPathID = null;
                                        tiData.simulatedWaypointIndex = -1;
                                        tiData.simulatedFollowReverse = false;
                                        tiData.isFollowingPathBasic = false;
                                   }
                              }
                         }
                         // --- END BasicWaitForCashierState handling ---

                         // --- Handle activation from BasicBrowseState ---
                         else if (savedBasicStateEnum != null && savedBasicStateEnum.Equals(BasicState.BasicBrowse))
                         {
                              Debug.Log($"PROXIMITY {tiData.Id}: Saved Basic state is BasicBrowse. Getting a new browse location from CustomerManager.", npcObject);

                              BrowseLocation? newBrowseLocation = customerManager?.GetRandomBrowseLocation();

                              if (newBrowseLocation.HasValue && newBrowseLocation.Value.browsePoint != null)
                              {
                                   // Set the runner's target location BEFORE transitioning to the state
                                   runner.CurrentTargetLocation = newBrowseLocation; // Set Runner's target field
                                   runner.SetCurrentDestinationPosition(newBrowseLocation.Value.browsePoint.position); // Also set runner's last destination position field
                                   runner._hasReachedCurrentDestination = false; // Mark as needing to move
                                   startingActiveStateEnum = CustomerState.Browse; // Set the target state to active Browse

                                   Debug.Log($"PROXIMITY {tiData.Id}: Successfully got new browse location {newBrowseLocation.Value.browsePoint.name}. Activating to Browse state.", npcObject);

                                   // Clear simulation data as active state takes over
                                   tiData.simulatedTargetPosition = null; // Clear simulated target
                                   tiData.simulatedStateTimer = 0f; // Reset timer
                                   // Clear path simulation data
                                   tiData.simulatedPathID = null;
                                   tiData.simulatedWaypointIndex = -1;
                                   tiData.simulatedFollowReverse = false;
                                   tiData.isFollowingPathBasic = false;
                              }
                              else
                              {
                                   Debug.LogError($"PROXIMITY {tiData.Id}: Could not get a valid browse location from CustomerManager during BasicBrowse activation! Activating to Exiting as fallback.", npcObject);
                                   startingActiveStateEnum = CustomerState.Exiting; // Fallback if cannot get browse location
                                   // Clear simulation data
                                   tiData.simulatedTargetPosition = null;
                                   tiData.simulatedStateTimer = 0f;
                                   // Clear path simulation data
                                   tiData.simulatedPathID = null;
                                   tiData.simulatedWaypointIndex = -1;
                                   tiData.simulatedFollowReverse = false;
                                   tiData.isFollowingPathBasic = false;
                              }
                         }
                         // --- END BasicBrowseState handling ---

                         // --- Handle activation from BasicPathState ---
                         else if (savedBasicStateEnum != null && savedBasicStateEnum.Equals(BasicPathState.BasicFollowPath))
                         {
                              Debug.Log($"PROXIMITY {tiData.Id}: Saved Basic state is BasicFollowPath. Restoring path progress.", npcObject);

                              // Get the corresponding active path state enum
                              // startingActiveStateEnum = GetActiveStateFromBasicState(savedBasicStateEnum); // Should map to PathState.FollowPath // OLD LINE
                              startingActiveStateEnum = PathState.FollowPath; // <-- NEW LINE: Directly assign the correct active state

                              // Check if path data is valid on TiData
                              if (string.IsNullOrWhiteSpace(tiData.simulatedPathID) || tiData.simulatedWaypointIndex < 0 || waypointManager == null)
                              {
                                   Debug.LogError($"PROXIMITY {tiData.Id}: Invalid path simulation data found during BasicPathState activation! PathID: '{tiData.simulatedPathID}', Index: {tiData.simulatedWaypointIndex}. Transitioning to BasicPatrol fallback.", npcObject);
                                   // Fallback to a safe state if path data is bad
                                   startingActiveStateEnum = GetActiveStateFromBasicState(BasicState.BasicPatrol); // Map BasicPatrol to its active counterpart
                                   // Clear path simulation data as it's invalid
                                   tiData.simulatedPathID = null;
                                   tiData.simulatedWaypointIndex = -1;
                                   tiData.simulatedFollowReverse = false;
                                   tiData.isFollowingPathBasic = false;
                                   tiData.simulatedTargetPosition = null; // Clear simulated target
                                   tiData.simulatedStateTimer = 0f; // Reset timer
                              }
                              else
                              {
                                   // Path data seems valid. The generic PathStateSO.OnEnter will read the
                                   // tiData.simulated... fields directly when it detects activation from a saved state.
                                   // We just need to ensure the state mapping is correct and the data is still on tiData.
                                   Debug.Log($"PROXIMITY {tiData.Id}: Path data valid on TiData. PathState.OnEnter will handle restoration from TiData.", npcObject);

                                   // Clear NON-PATH simulation data as active state takes over
                                   tiData.simulatedTargetPosition = null; // Clear simulated target
                                   tiData.simulatedStateTimer = 0f; // Reset timer on activation
                                   // Note: simulatedPathID, simulatedWaypointIndex, simulatedFollowReverse, isFollowingPathBasic
                                   // are NOT cleared here, as they are needed by the PathStateSO.OnEnter.
                                   // They WILL be cleared by the PathFollowingHandler itself when it stops following the path.
                              }
                         }

                         // Handle activation from BasicWaitForPrescriptionState (Queue Sim) ---
                         // This handles NPCs who were waiting in the *prescription queue* simulation.
                         else if (savedBasicStateEnum != null && savedBasicStateEnum.Equals(BasicState.BasicWaitForPrescription))
                         {
                              Debug.Log($"PROXIMITY {tiData.Id}: Saved Basic state is BasicWaitForPrescription (Queue Sim). Attempting to rejoin prescription queue.", npcObject);

                              NpcQueueHandler queueHandler = runner.QueueHandler;
                              if (queueHandler != null)
                              {
                                   Transform assignedSpotTransform;
                                   int assignedSpotIndex;
                                   // Attempt to join the prescription queue
                                   if (prescriptionManager.TryJoinPrescriptionQueue(runner, out assignedSpotTransform, out assignedSpotIndex))
                                   {
                                        // Successfully joined queue, setup the handler and transition to PrescriptionQueue state
                                        Debug.Log($"PROXIMITY {tiData.Id}: Successfully rejoined prescription queue at spot {assignedSpotIndex}. Activating to PrescriptionQueue state.", npcObject);
                                        queueHandler.SetupQueueSpot(assignedSpotTransform, assignedSpotIndex, QueueType.Prescription);
                                        startingActiveStateEnum = CustomerState.PrescriptionQueue;
                                   }
                                   else
                                   {
                                        // Prescription queue is full, cannot rejoin
                                        Debug.Log($"PROXIMITY {tiData.Id}: Prescription queue is full. Cannot rejoin. Activating to Exiting state.", npcObject);
                                        startingActiveStateEnum = CustomerState.Exiting; // Give up
                                   }
                              }
                              else
                              {
                                   Debug.LogError($"PROXIMITY {tiData.Id}: Runner is missing NpcQueueHandler component during BasicWaitForPrescription activation! Cannot handle queue logic. Activating to Exiting.", npcObject);
                                   startingActiveStateEnum = CustomerState.Exiting; // Fallback
                              }
                              // Clear simulation data after queue evaluation
                              tiData.simulatedTargetPosition = null;
                              tiData.simulatedStateTimer = 0f;
                              tiData.simulatedPathID = null;
                              tiData.simulatedWaypointIndex = -1;
                              tiData.simulatedFollowReverse = false;
                              tiData.isFollowingPathBasic = false;
                         }

                         // Handle activation from BasicWaitingAtPrescriptionSpot (Waiting Sim) ---
                         // This handles NPCs who were waiting at the claim spot (WaitingForPrescription) simulation.
                         else if (savedBasicStateEnum != null && savedBasicStateEnum.Equals(BasicState.BasicWaitingAtPrescriptionSpot))
                         {
                              Debug.Log($"PROXIMITY {tiData.Id}: Saved Basic state is BasicWaitingAtPrescriptionSpot (Waiting Sim). Activating to WaitingForPrescription state.", npcObject);
                              // Transition directly back to the active WaitingForPrescription state
                              startingActiveStateEnum = CustomerState.WaitingForPrescription;

                              // Ensure position is set to the claim spot before activation
                              if (prescriptionManager != null && prescriptionManager.GetPrescriptionClaimPoint() != null)
                              {
                                   Vector3 claimSpotPos = prescriptionManager.GetPrescriptionClaimPoint().position;
                                   Quaternion claimSpotRot = prescriptionManager.GetPrescriptionClaimPoint().rotation;
                                   // Warp the NPC to the claim spot position
                                   if (runner.MovementHandler != null && runner.MovementHandler.Agent != null)
                                   {
                                        runner.MovementHandler.EnableAgent(); // Ensure agent is enabled for warp
                                        if (runner.MovementHandler.Warp(claimSpotPos))
                                        {
                                             Debug.Log($"PROXIMITY {tiData.Id}: Warped to claim spot {claimSpotPos} for BasicWaitingAtPrescriptionSpot activation.", npcObject);
                                             runner.transform.rotation = claimSpotRot; // Set rotation
                                        }
                                        else
                                        {
                                             Debug.LogError($"PROXIMITY {tiData.Id}: Failed to warp to claim spot {claimSpotPos} for BasicWaitingAtPrescriptionSpot activation! Is the point on the NavMesh? Activating to Exiting as fallback.", npcObject);
                                             startingActiveStateEnum = CustomerState.Exiting; // Fallback
                                        }
                                   }
                                   else
                                   {
                                        Debug.LogError($"PROXIMITY {tiData.Id}: Runner MovementHandler or Agent is null during BasicWaitingAtPrescriptionSpot activation! Cannot warp. Activating to Exiting as fallback.", npcObject);
                                        startingActiveStateEnum = CustomerState.Exiting; // Fallback
                                   }
                              }
                              else
                              {
                                   Debug.LogError($"PROXIMITY {tiData.Id}: PrescriptionManager or claim point is null during BasicWaitingAtPrescriptionSpot activation! Cannot warp. Activating to Exiting as fallback.", npcObject);
                                   startingActiveStateEnum = CustomerState.Exiting; // Fallback
                              }

                              // Clear simulation data as active state takes over
                              tiData.simulatedTargetPosition = null;
                              tiData.simulatedStateTimer = 0f;
                              tiData.simulatedPathID = null;
                              tiData.simulatedWaypointIndex = -1;
                              tiData.simulatedFollowReverse = false;
                              tiData.isFollowingPathBasic = false;
                         }

                         // Handle activation from BasicWaitingForDeliverySim (Delivery Sim) ---
                         // This handles NPCs who were waiting at the claim spot for delivery (WaitingForDelivery) simulation.
                         else if (savedBasicStateEnum != null && savedBasicStateEnum.Equals(BasicState.BasicWaitingForDeliverySim))
                         {
                              Debug.Log($"PROXIMITY {tiData.Id}: Saved Basic state is BasicWaitingForDeliverySim (Delivery Sim). Activating to WaitingForDelivery state.", npcObject);
                              // Transition directly back to the active WaitingForDelivery state
                              startingActiveStateEnum = CustomerState.WaitingForDelivery;

                              // Ensure position is set to the claim spot before activation
                              if (prescriptionManager != null && prescriptionManager.GetPrescriptionClaimPoint() != null)
                              {
                                   Vector3 claimSpotPos = prescriptionManager.GetPrescriptionClaimPoint().position;
                                   Quaternion claimSpotRot = prescriptionManager.GetPrescriptionClaimPoint().rotation;
                                   // Warp the NPC to the claim spot position
                                   if (runner.MovementHandler != null && runner.MovementHandler.Agent != null)
                                   {
                                        runner.MovementHandler.EnableAgent(); // Ensure agent is enabled for warp
                                        if (runner.MovementHandler.Warp(claimSpotPos))
                                        {
                                             Debug.Log($"PROXIMITY {tiData.Id}: Warped to claim spot {claimSpotPos} for BasicWaitingForDeliverySim activation.", npcObject);
                                             runner.transform.rotation = claimSpotRot; // Set rotation
                                        }
                                        else
                                        {
                                             Debug.LogError($"PROXIMITY {tiData.Id}: Failed to warp to claim spot {claimSpotPos} for BasicWaitingForDeliverySim activation! Is the point on the NavMesh? Activating to Exiting as fallback.", npcObject);
                                             startingActiveStateEnum = CustomerState.Exiting; // Fallback
                                        }
                                   }
                                   else
                                   {
                                        Debug.LogError($"PROXIMITY {tiData.Id}: Runner MovementHandler or Agent is null during BasicWaitingForDeliverySim activation! Cannot warp. Activating to Exiting as fallback.", npcObject);
                                        startingActiveStateEnum = CustomerState.Exiting; // Fallback
                                   }
                              }
                              else
                              {
                                   Debug.LogError($"PROXIMITY {tiData.Id}: PrescriptionManager or claim point is null during BasicWaitingForDeliverySim activation! Cannot warp. Activating to Exiting as fallback.", npcObject);
                                   startingActiveStateEnum = CustomerState.Exiting; // Fallback
                              }

                              // Clear simulation data as active state takes over
                              tiData.simulatedTargetPosition = null;
                              tiData.simulatedStateTimer = 0f;
                              tiData.simulatedPathID = null;
                              tiData.simulatedWaypointIndex = -1;
                              tiData.simulatedFollowReverse = false;
                              tiData.isFollowingPathBasic = false;
                         }

                         // Handle activation from Basic Cashier States at Register ---
                         else if (savedBasicStateEnum != null &&
                                 (savedBasicStateEnum.Equals(BasicState.BasicCashierMovingToCashSpot) ||
                                  savedBasicStateEnum.Equals(BasicState.BasicCashierWaitingForCustomer) ||
                                  savedBasicStateEnum.Equals(BasicState.BasicCashierProcessingCheckout)))
                         {
                              Debug.Log($"PROXIMITY {tiData.Id}: Saved Basic state is a Cashier state at the register ({savedBasicStateEnum.ToString()}). Activating to corresponding Active state.", npcObject);

                              // Map the Basic state to the corresponding Active State
                              startingActiveStateEnum = GetActiveStateFromBasicState(savedBasicStateEnum); // This will map to CashierMovingToCashSpot or CashierWaitingForCustomer

                              // Set the Runner's target location to the Cashier Spot *before* the state transition
                              Transform cashierSpot = cashierManager?.GetCashierSpot();
                              if (cashierSpot != null)
                              {
                                   runner.CurrentTargetLocation = new BrowseLocation { browsePoint = cashierSpot, inventory = null };
                                   runner.SetCurrentDestinationPosition(cashierSpot.position);
                                   // If activating into MovingToCashSpot, _hasReachedCurrentDestination should be false (handled by SetCurrentDestinationPosition)
                                   // If activating into WaitingForCustomer or ProcessingCheckout, they are already at the spot,
                                   // so _hasReachedCurrentDestination should be true. SetCurrentDestinationPosition handles this.
                                   Debug.Log($"PROXIMITY {tiData.Id}: Set Runner target location to Cashier Spot {cashierSpot.position}.", npcObject);

                                   // If activating into WaitingForCustomer or ProcessingCheckout, warp them directly to the spot
                                   if (savedBasicStateEnum.Equals(BasicState.BasicCashierWaitingForCustomer) || savedBasicStateEnum.Equals(BasicState.BasicCashierProcessingCheckout))
                                   {
                                        if (runner.MovementHandler != null && runner.MovementHandler.Agent != null)
                                        {
                                             runner.MovementHandler.EnableAgent(); // Ensure agent is enabled for warp
                                             if (runner.MovementHandler.Warp(cashierSpot.position))
                                             {
                                                  Debug.Log($"PROXIMITY {tiData.Id}: Warped to Cashier spot {cashierSpot.position} for activation into waiting/processing state.", npcObject);
                                                  runner.transform.rotation = cashierSpot.rotation; // Set rotation
                                             }
                                             else
                                             {
                                                  Debug.LogError($"PROXIMITY {tiData.Id}: Failed to warp to Cashier spot {cashierSpot.position} for activation! Is the point on the NavMesh? Activating to Idle as fallback.", npcObject);
                                                  startingActiveStateEnum = GeneralState.Idle; // Fallback
                                             }
                                        }
                                        else
                                        {
                                             Debug.LogError($"PROXIMITY {tiData.Id}: Runner MovementHandler or Agent is null during Cashier activation! Cannot warp. Activating to Idle as fallback.", npcObject);
                                             startingActiveStateEnum = GeneralState.Idle; // Fallback
                                        }
                                   }
                              }
                              else
                              {
                                   Debug.LogError($"PROXIMITY {tiData.Id}: CashierManager or Cashier Spot is null! Cannot set Runner target location. Activating to Idle as fallback.", npcObject);
                                   startingActiveStateEnum = GeneralState.Idle; // Fallback
                              }

                              // Clear simulation data as active state takes over
                              tiData.simulatedTargetPosition = null;
                              tiData.simulatedStateTimer = 0f;
                              tiData.simulatedPathID = null;
                              tiData.simulatedWaypointIndex = -1;
                              tiData.simulatedFollowReverse = false;
                              tiData.isFollowingPathBasic = false;
                         }


                         // --- Handle activation from any other saved BasicState ---
                         // This covers BasicPatrol, BasicLookToShop, BasicEnteringStore, BasicExitingStore, BasicIdleAtHome, BasicPathState
                         else if (savedBasicStateEnum != null) // Check if there was *any* saved Basic state
                         {
                              // Map the Basic state to the corresponding Active State
                              startingActiveStateEnum = GetActiveStateFromBasicState(savedBasicStateEnum);
                              Debug.Log($"PROXIMITY {tiData.Id}: Saved Basic state '{savedBasicStateEnum.GetType().Name}.{savedBasicStateEnum.ToString() ?? "NULL"}' maps to Active State '{startingActiveStateEnum?.GetType().Name}.{startingActiveStateEnum?.ToString() ?? "NULL"}'. Activating to this state.", npcObject);
                              // Reset simulation data when transitioning from simulation to active
                              tiData.simulatedTargetPosition = null; // Clear simulated target
                              tiData.simulatedStateTimer = 0f; // Reset timer on activation
                              // Clear path simulation data (unless it's BasicPathState, handled above)
                              if (!savedBasicStateEnum.Equals(BasicPathState.BasicFollowPath))
                              {
                                   tiData.simulatedPathID = null;
                                   tiData.simulatedWaypointIndex = -1;
                                   tiData.simulatedFollowReverse = false;
                                   tiData.isFollowingPathBasic = false;
                              }
                         }
                         else // No saved state at all (should default to primary type start state)
                         {
                              // If no state was saved, or mapping failed, startingActiveStateEnum remains null.
                              // The Runner will then fall back to its GetPrimaryStartingStateSO logic (which checks TypeDefs).
                              Debug.Log($"PROXIMITY {tiData.Id}: No valid saved Basic state found or mapped. Runner will determine primary starting state from TypeDefs.", npcObject);
                              // Ensure simulation data is clean if no saved state existed
                              tiData.simulatedTargetPosition = null;
                              tiData.simulatedStateTimer = 0f;
                              // Clear path simulation data
                              tiData.simulatedPathID = null;
                              tiData.simulatedWaypointIndex = -1;
                              tiData.simulatedFollowReverse = false;
                              tiData.isFollowingPathBasic = false;
                         }
                         // --- END Handle activation based on saved BasicStateEnum ---


                         // Call the Runner's Activate method with the determined starting state override
                         // If startingActiveStateEnum is null, Runner.Activate will use GetPrimaryStartingStateSO().
                         runner.Activate(tiData, customerManager, startingActiveStateEnum); // <-- Pass determined override state Enum

                         // Corrected syntax for the log message
                         Debug.Log($"PROXIMITY TiNpcManager: Activation initiated for TI NPC '{tiData.Id}' (GameObject '{npcObject.name}'). Runner.Activate called with override state: {(startingActiveStateEnum != null ? startingActiveStateEnum.GetType().Name + "." + startingActiveStateEnum.ToString() : "NULL")}");

                    }
                    else
                    {
                         // --- MODIFIED: Log which prefab failed to pool ---
                         Debug.LogError($"TiNpcManager: Failed to get a pooled TI NPC GameObject for activation of '{tiData.Id}' using prefab '{prefabToUse.name}'! Pool might be exhausted or prefab is invalid.", this);
                         // --- END MODIFIED ---
                    }
               }
          }

          /// <summary>
          /// Handles the process of deactivating a single TI NPC.
          /// Determines the correct Basic State, saves data, and triggers the pooling flow.
          /// Called by ProximityManager.
          /// MODIFIED: Saves the mapped basic state for all active states, including prescription waiting/delivery.
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


               // --- Determine and Save the Basic State before triggering Pooling ---
               Enum currentActiveState = currentStateSO?.HandledState; // Get the enum key of the current active state
               Enum targetBasicState = GetBasicStateFromActiveState(currentActiveState); // Map to the corresponding basic state

               if (targetBasicState != null)
               {
                    // Save the mapped basic state for all active states.
                    // The special handling for WaitingForPrescription/Delivery is now in the MAPPINGS (Phase 1).
                    // The simulation system will now correctly receive BasicWaitingAtPrescriptionSpot or BasicWaitingForDeliverySim.
                    tiData.SetCurrentState(targetBasicState); // Save the mapped basic state
                                                              // Corrected syntax for the log message
                    Debug.Log($"PROXIMITY {tiData.Id}: Active state '{currentActiveState?.GetType().Name}.{currentActiveState?.ToString() ?? "NULL"}' maps to Basic State '{targetBasicState.GetType().Name}.{targetBasicState.ToString()}'. Saving this state to TiData for simulation.", runner.gameObject);


                    // --- MODIFIED: Save Path Progress if currently following a path OR was interrupted from path state ---
                    // We need to save the state as it was *before* any interruption if one is active.
                    // If not interrupted, save the current state of the PathFollowingHandler if it's active.
                    if (runner.wasInterruptedFromPathState) // Check the flag set by NpcInterruptionHandler
                    {
                         Debug.Log($"PROXIMITY {tiData.Id}: NPC was interrupted from PathState. Saving TEMPORARY path progress data to TiData for simulation.", runner.gameObject);
                         tiData.simulatedPathID = runner.interruptedPathID;
                         tiData.simulatedWaypointIndex = runner.interruptedWaypointIndex;
                         tiData.simulatedFollowReverse = runner.interruptedFollowReverse;
                         tiData.isFollowingPathBasic = true; // Flag that they were on a path simulation when deactivated

                         // Clear the temporary data on the runner now that it's saved to persistent TiData
                         runner.interruptedPathID = null;
                         runner.interruptedWaypointIndex = -1;
                         runner.interruptedFollowReverse = false;
                         runner.wasInterruptedFromPathState = false;

                         Debug.Log($"PROXIMITY {tiData.Id}: Saved TEMPORARY path progress: PathID='{tiData.simulatedPathID}', Index={tiData.simulatedWaypointIndex}, Reverse={tiData.simulatedFollowReverse}.", runner.gameObject);
                    }
                    else if (currentActiveState != null && currentActiveState.Equals(PathState.FollowPath) && runner.PathFollowingHandler != null && runner.PathFollowingHandler.IsFollowingPath)
                    {
                         // This case should ideally not happen if the NPC is interrupted, as PathState.OnExit stops the handler.
                         // But it handles cases where the NPC might be deactivated *while* in PathState without interruption logic triggering first.
                         Debug.Log($"PROXIMITY {tiData.Id}: Currently in PathState and following a path (not interrupted). Saving CURRENT path progress.", runner.gameObject);
                         tiData.simulatedPathID = runner.PathFollowingHandler.GetCurrentPathSO()?.PathID;
                         tiData.simulatedWaypointIndex = runner.PathFollowingHandler.GetCurrentTargetWaypointIndex(); // Save the index they were moving *towards*
                         tiData.simulatedFollowReverse = runner.PathFollowingHandler.GetFollowReverse();
                         tiData.isFollowingPathBasic = true; // Flag that they were on a path simulation when deactivated

                         Debug.Log($"PROXIMITY {tiData.Id}: Saved CURRENT path progress: PathID='{tiData.simulatedPathID}', Index={tiData.simulatedWaypointIndex}, Reverse={tiData.simulatedFollowReverse}.", runner.gameObject);
                    }
                    else
                    {
                         // Not following a path or not interrupted from path state, ensure path simulation data is cleared
                         // This ensures that if they were previously on a path simulation but are now in BasicPatrol simulation,
                         // the path data is correctly reset.
                         tiData.simulatedPathID = null;
                         tiData.simulatedWaypointIndex = -1;
                         tiData.simulatedFollowReverse = false;
                         tiData.isFollowingPathBasic = false;
                         // simulatedTargetPosition and simulatedStateTimer are handled by the target BasicStateSO.OnEnter
                    }
                    // --- END MODIFIED ---


                    // --- Call OnEnter for the target Basic State to initialize simulation data ---
                    // This is crucial to set up simulatedTargetPosition, simulatedStateTimer etc. for the *next* simulation tick.
                    // This call happens *after* saving path progress, so the BasicPathStateSO.OnEnter can read the saved data.
                    BasicNpcStateSO targetBasicStateSO = basicNpcStateManager?.GetBasicStateSO(targetBasicState);
                    if (targetBasicStateSO != null)
                    {
                         Debug.Log($"PROXIMITY {tiData.Id}: Calling OnEnter for Basic State '{targetBasicStateSO.name}' to initialize simulation data.", runner.gameObject);
                         targetBasicStateSO.OnEnter(tiData, basicNpcStateManager); // Pass the data and the manager
                    }
                    else
                    {
                         // This shouldn't happen if mapping and GetBasicStateSO work, but defensive
                         // Corrected syntax for the log message
                         Debug.LogError($"PROXIMITY {tiData.Id}: Could not get target Basic State SO for '{targetBasicState?.GetType().Name}.{targetBasicState?.ToString() ?? "NULL"}' during deactivation request. Cannot initialize simulation state data!", runner.gameObject);
                         // Data might be left in a bad state, but proceed with pooling.
                    }
                    // --- END Call OnEnter ---


                    // --- Trigger Deactivation Flow ---
                    Debug.Log($"PROXIMITY {tiData.Id}: TI NPC ready for deactivation. Triggering TransitionToState(ReturningToPool).", runner.gameObject);
                    // Transition the Runner to the ReturningToPool state.
                    // The Runner.TransitionToState handles calling Runner.Deactivate() *before* entering the state.
                    // Runner.Deactivate() will now save the *Basic State* we just set on tiData, and position/rotation.
                    // HandleTiNpcReturnToPool will be called later by the pooling event, clearing data link/flag and updating grid.
                    runner.TransitionToState(runner.GetStateSO(GeneralState.ReturningToPool));
                    // --- END Trigger ---

               }
               else
               {
                    // This shouldn't happen if GetBasicStateFromActiveState has a fallback, but defensive.
                    // Corrected syntax for the log message
                    Debug.LogError($"PROXIMITY {tiData.Id}: Could not determine a Basic State mapping for active state '{currentActiveState?.GetType().Name}.{currentActiveState?.ToString() ?? "NULL"}' during deactivation request. Cannot save state for simulation! Forcing cleanup.", runner.gameObject);
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
               TiNpcData deactivatedTiData = allTiNpcs.Values.FirstOrDefault(data => data.NpcGameObject == npcObject);

               if (deactivatedTiData != null)
               {
                    Debug.Log($"POOL TiNpcManager: Found TiNpcData for '{deactivatedTiData.Id}' linked to GameObject '{npcObject.name}'. Unlinking data and flags.");

                    // --- Clear the data link and flags ---
                    deactivatedTiData.UnlinkGameObject(); // Use helper to set NpcGameObject=null and isActiveGameObject=false
                                                          // --- END ---

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
          /// </summary>
          /// <param name="data">The TiNpcData associated with the active NPC.</param>
          /// <param name="oldPosition">The NPC's position before the change.</param>
          /// <param name="newPosition">The NPC's new position.</param>
          public void NotifyActiveNpcPositionChanged(TiNpcData data, Vector3 oldPosition, Vector3 newPosition)
          {
               if (gridManager != null)
               {
                    gridManager.UpdateItemPosition(data, oldPosition, newPosition);
                    // Debug.Log($"TiNpcManager: Notified of active NPC '{data.Id}' position change. Updated grid.", data.NpcGameObject); // Too noisy
               }
               else
               {
                    Debug.LogWarning($"TiNpcManager: GridManager is null! Cannot update grid position for active NPC '{data.Id}'.", data.NpcGameObject);
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
               return allTiNpcs.Values.Where(data => data.IsActiveGameObject).ToList();
          }

          /// <summary>
          /// Gets a list of all currently inactive TI NPC data records (filtered on the fly).
          /// </summary>
          public List<TiNpcData> GetInactiveTiNpcs()
          {
               // Filter the main collection when requested
               return allTiNpcs.Values.Where(data => !data.IsActiveGameObject).ToList();
          }

          /// <summary>
          /// Returns the total number of TI NPCs managed.
          /// </summary>
          public int GetTotalTiNpcCount()
          {
               return allTiNpcs.Count;
          }

          // --- NEW METHOD: Get list of all TI NPC IDs ---
          /// <summary>
          /// Gets a list of all unique IDs for the managed TI NPCs.
          /// </summary>
          public List<string> GetTiNpcIds()
          {
               // Return a new list containing all the keys (IDs) from the dictionary
               return allTiNpcs.Keys.ToList();
          }
          // --- END NEW METHOD ---


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
                    // --- END NEW ---


                    if (allTiNpcs.ContainsKey(entry.id))
                    {
                         Debug.LogWarning($"TiNpcManager: Skipping duplicate dummy NPC entry with ID '{entry.id}'.", this);
                         continue;
                    }

                    // --- MODIFIED: Pass the prefab from the dummy entry to the TiNpcData constructor ---
                    TiNpcData newNpcData = new TiNpcData(entry.id, entry.homePosition, entry.homeRotation, entry.prefab);
                    // --- END MODIFIED ---


                    // --- Assign schedule time ranges from dummy data ---
                    newNpcData.startDay = entry.startDay;
                    newNpcData.endDay = entry.endDay;

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
                                                                           // --- END NEW ---


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

                         if (newNpcData.startDay.IsWithinRange(currentTime))
                         {
                              // Day has started, determine initial state from the intended day start behavior
                              // Use the refined property getter which handles path vs state logic
                              Enum dayStartActiveState = newNpcData.DayStartActiveStateEnum; // <-- Use refined property

                              if (dayStartActiveState != null)
                              {
                                   initialBasicStateEnum = GetBasicStateFromActiveState(dayStartActiveState); // Map to Basic State
                                   Debug.Log($"TiNpcManager: Dummy NPC '{entry.id}' day has started ({newNpcData.startDay}, Current Time: {currentTime:HH:mm}). Initial Basic State is mapped from Day Start Active State: '{initialBasicStateEnum?.GetType().Name}.{initialBasicStateEnum?.ToString() ?? "NULL"}'.", this);
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
                              // Day has NOT started, initial state is BasicIdleAtHome
                              Debug.Log($"TiNpcManager: Dummy NPC '{entry.id}' day has NOT started ({newNpcData.startDay}, Current Time: {currentTime:HH:mm}). Initial Basic State is BasicIdleAtHome.", this);
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
                    // --- END MODIFIED INITIAL STATE LOGIC ---


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
                         Debug.Log($"TiNpcManager: Loaded dummy NPC '{newNpcData.Id}' using prefab '{newNpcData.Prefab?.name ?? "NULL"}' with initial Basic State '{initialBasicStateEnum}'. Schedule: {newNpcData.startDay} to {newNpcData.endDay}. Unique Options: {newNpcData.uniqueDecisionOptions.entries.Count}. Simulation timer initialized to {newNpcData.simulatedStateTimer:F2}s, Target: {newNpcData.simulatedTargetPosition?.ToString() ?? "NULL"}. Path Data: Following={newNpcData.isFollowingPathBasic}, ID='{newNpcData.simulatedPathID}', Index={newNpcData.simulatedWaypointIndex}, Reverse={newNpcData.simulatedFollowReverse}. Pending Prescription: {newNpcData.pendingPrescription}. Added to grid.", this); // Updated log
                    }
                    else
                    {
                         Debug.Log($"TiNpcManager: Loaded dummy NPC '{newNpcData.Id}' using prefab '{newNpcData.Prefab?.name ?? "NULL"}' with initial Basic State '{initialBasicStateEnum}'. Schedule: {newNpcData.startDay} to {newNpcData.endDay}. Unique Options: {newNpcData.uniqueDecisionOptions.entries.Count}. Simulation timer initialized to {newNpcData.simulatedStateTimer:F2}s, Target: {newNpcData.simulatedTargetPosition?.ToString() ?? "NULL"}. Path Data: Following={newNpcData.isFollowingPathBasic}, ID='{newNpcData.simulatedPathID}', Index={newNpcData.simulatedWaypointIndex}, Reverse={newNpcData.simulatedFollowReverse}. Pending Prescription: {newNpcData.pendingPrescription}. GridManager is null, NOT added to grid.", this); // Updated log
                    }
               }
          }
          // --- End Restored LoadDummyNpcData ---
     }
}