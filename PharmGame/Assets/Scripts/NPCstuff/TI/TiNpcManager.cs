using UnityEngine;
using System.Collections.Generic;
using System.Linq; // Needed for LINQ (Where, FirstOrDefault)
using Game.NPC; // Needed for NpcStateMachineRunner, GeneralState, CustomerState enums, PathState enum
using Game.NPC.States; // Needed for State SOs (to check HandledState)
using Game.NPC.BasicStates; // Needed for BasicState enum, BasicPathState enum, BasicNpcStateManager, BasicNpcStateSO
using Utils.Pooling; // Needed for PoolingManager
using System.Collections; // Needed for Coroutine
using System; // Needed for Enum and Type
using Game.NPC.Types; // Needed for TestState enum (Patrol)
using CustomerManagement; // Needed for CustomerManager
using Game.NPC.Handlers;
using Game.Spatial; // Needed for GridManager
using Game.Proximity; // Needed for ProximityManager
using Game.Navigation; // Needed for WaypointManager (needed by BasicPathStateSO)

namespace Game.NPC.TI // Keep in the TI namespace
{
    /// <summary>
    /// Manages the persistent data for True Identity (TI) NPCs.
    /// Handles loading, storing, and tracking TI NPC data independent of their GameObjects.
    /// Implements off-screen simulation logic (delegating to BasicNpcStateManager)
    /// and provides methods for ProximityManager to trigger activation/deactivation.
    /// Uses a single collection of data records and filters on the fly.
    /// Now includes mappings for path following states.
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
        // Reference to GridManager (will be obtained in Awake/Start)
        private GridManager gridManager;
        // Reference to ProximityManager (will be obtained in Awake/Start)
        private ProximityManager proximityManager;
        // Reference to WaypointManager (will be obtained in Awake/Start)
        private WaypointManager waypointManager; // <-- NEW: Reference to WaypointManager


        [Header("TI NPC Setup")]
        [Tooltip("List of NPC prefabs that this manager can pool and activate for TI NPCs.")]
        [SerializeField] private List<GameObject> tiNpcPrefabs;

        [Header("Simulation Settings")] // Renamed header slightly
        [Tooltip("The interval (in seconds) between simulation ticks for inactive NPCs.")]
        [SerializeField] private float simulationTickInterval = 0.1f; // Process a batch every 0.1 seconds (10 Hz)
        [Tooltip("The maximum number of inactive NPCs to simulate per tick.")]
        [SerializeField] private int maxNpcsToSimulatePerTick = 10; // Process 10 NPCs per tick


        [Header("Dummy Data Loading (Phase 1 Test)")]
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
            public string id;
            public Vector3 homePosition;
            public Quaternion homeRotation = Quaternion.identity; // Default rotation
            [Tooltip("Initial Basic State for this dummy NPC.")]
            [SerializeField] public BasicState initialBasicState = BasicState.BasicPatrol; // Initial Basic State
            // --- NEW: Optional initial path data for dummy NPCs ---
            [Tooltip("Optional: Initial Basic Path State for this dummy NPC.")]
            [SerializeField] public BasicPathState initialBasicPathState = BasicPathState.None; // Use None as default
            [Tooltip("Optional: Path ID if starting in a Basic Path State.")]
            [SerializeField] public string initialPathID;
            [Tooltip("Optional: Start Index if starting in a Basic Path State.")]
            [SerializeField] public int initialStartIndex = 0;
            [Tooltip("Optional: Follow Reverse if starting in a Basic Path State.")]
            [SerializeField] public bool initialFollowReverse = false;
            // --- END NEW ---
        }

        // --- Persistent Data Storage ---
        // Use a single dictionary as the source of truth for all TI NPC data
        internal Dictionary<string, TiNpcData> allTiNpcs = new Dictionary<string, TiNpcData>();

        // Internal index for round-robin simulation batching
        private int simulationIndex = 0;

         // Coroutine references
         private Coroutine simulationCoroutine;
         // Removed proximityCheckCoroutine - ProximityManager handles this


        // --- State Mapping Dictionaries ---
        private Dictionary<Enum, Enum> activeToBaseStateMap;
        private Dictionary<Enum, Enum> basicToActiveStateMap;


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
             }
              if (customerManager == null)
              {
                  Debug.LogError("TiNpcManager: CustomerManager instance not found or not assigned! TI NPCs cannot activate into customer roles. Assign in Inspector or ensure it's a functioning Singleton.", this);
              }

             // Get reference to BasicNpcStateManager
             basicNpcStateManager = BasicNpcStateManager.Instance;
             if (basicNpcStateManager == null)
             {
                  Debug.LogError("TiNpcManager: BasicNpcStateManager instance not found! Cannot simulate inactive TI NPCs. Ensure BasicNpcStateManager is in the scene.", this);
                  // Do NOT disable manager entirely, just simulation won't work.
             }

             // Get reference to GridManager
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

             // --- NEW: Get reference to WaypointManager ---
             waypointManager = WaypointManager.Instance;
             if (waypointManager == null)
             {
                  Debug.LogError("TiNpcManager: WaypointManager instance not found! Cannot handle path following for TI NPCs. Ensure WaypointManager is in the scene.", this);
                  // Do NOT disable manager entirely, just path following won't work.
             }
             // --- END NEW ---


             // Validate Player Transform - Still needed for simulation logic that might use player position
             if (playerTransform == null)
             {
                  // Attempt to find Player by tag if not assigned
                  GameObject playerGO = GameObject.FindGameObjectWithTag("Player"); // Assumes Player has "Player" tag
                  if (playerGO != null) playerTransform = playerGO.transform;
                  else Debug.LogWarning("TiNpcManager: Player Transform not assigned and GameObject with tag 'Player' not found! Simulation logic that depends on player position may fail.", this); // Changed to Warning
             }


             // Validate TI Prefab List
             if (tiNpcPrefabs == null || tiNpcPrefabs.Count == 0 || tiNpcPrefabs.Any(prefab => prefab == null))
             {
                  Debug.LogError("TiNpcManager: TI NPC Prefab list is not assigned or contains null entries! Cannot activate TI NPCs.", this);
                  // Do NOT disable manager entirely, just activation won't work.
             }


            // Load Dummy Data
            LoadDummyNpcData();

            Debug.Log($"TiNpcManager: Started. Loaded {allTiNpcs.Count} TI NPCs.");

             Debug.Log($"TiNpcManager: Start completed.");

            // Start the simulation loop if BasicNpcStateManager and GridManager are available
             if (basicNpcStateManager != null && gridManager != null && playerTransform != null) // Simulation needs player position for query
             {
                simulationCoroutine = StartCoroutine(SimulateInactiveNpcsRoutine());
                Debug.Log("TiNpcManager: Simulation coroutine started.");
             } else {
                  Debug.LogWarning("TiNpcManager: BasicNpcStateManager, GridManager, or Player Transform is null. Cannot start simulation coroutine."); // Updated log
             }

            // Removed starting ProximityCheckRoutine - ProximityManager handles this
        }

        private void OnEnable()
        {
             // Restart simulation coroutine if manager was disabled and re-enabled AND dependencies exist
             if (simulationCoroutine == null && allTiNpcs.Count > 0 && basicNpcStateManager != null && gridManager != null && playerTransform != null) // Added dependencies
             {
                  Debug.Log("TiNpcManager: Restarting simulation coroutine on OnEnable.");
                  simulationCoroutine = StartCoroutine(SimulateInactiveNpcsRoutine());
             }
             // Removed restarting proximityCheckCoroutine
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
             // Removed stopping proximityCheckCoroutine
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
                     // try { UnityEditor.Handles.Label(tiData.CurrentWorldPosition + Vector3.up * (inactiveGizmoRadius + 0.1f), tiData.Id); } catch {}

                     // --- NEW DEBUG: Draw line to simulated target position if it exists ---
                     if (tiData.simulatedTargetPosition.HasValue)
                     {
                          Gizmos.color = Color.magenta; // Different color for target line
                          Gizmos.DrawLine(tiData.CurrentWorldPosition, tiData.simulatedTargetPosition.Value);
                          Gizmos.DrawSphere(tiData.simulatedTargetPosition.Value, inactiveGizmoRadius * 0.5f); // Draw a smaller sphere at the target
                          Gizmos.color = inactiveGizmoColor; // Restore color
                     }
                     // --- END NEW DEBUG ---
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

             // --- NEW: Path State Mapping ---
             activeToBaseStateMap[PathState.FollowPath] = BasicPathState.BasicFollowPath;
             // --- END NEW ---


             // Basic -> Active mappings
             basicToActiveStateMap[BasicState.BasicPatrol] = TestState.Patrol;
             basicToActiveStateMap[BasicState.BasicLookToShop] = CustomerState.LookingToShop;
             basicToActiveStateMap[BasicState.BasicEnteringStore] = CustomerState.Entering;
             basicToActiveStateMap[BasicState.BasicBrowse] = CustomerState.Browse;
             basicToActiveStateMap[BasicState.BasicWaitForCashier] = CustomerState.Queue; // This is the DEFAULT mapping, can be overridden during activation for specific logic.
             basicToActiveStateMap[BasicState.BasicExitingStore] = CustomerState.Exiting;
             // BasicState.None is not mapped back to an active state, handled by activation logic.

             // --- NEW: Basic Path State Mapping ---
             basicToActiveStateMap[BasicPathState.BasicFollowPath] = PathState.FollowPath;
             // --- END NEW ---


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
             Debug.LogWarning($"TiNpcManager: No Basic State mapping found for Active State '{activeStateEnum.GetType().Name}.{activeStateEnum.ToString()}'. Falling back to BasicPatrol.");
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
             Debug.LogError($"TiNpcManager: No Active State mapping found for Basic State '{basicStateEnum.GetType().Name}.{basicStateEnum.ToString()}'! Returning GeneralState.Idle as fallback. Review mappings!");
             return GeneralState.Idle; // Error fallback
        }


        /// <summary>
        /// The low-tick simulation routine. Iterates over inactive NPCs found near the player via the grid,
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
                    // --- NEW: Check for WaypointManager dependency for path simulation ---
                    if (waypointManager == null)
                    {
                         Debug.LogError("SIM TiNpcManager: WaypointManager is null! Cannot simulate path following for inactive NPCs.", this);
                         // Continue simulation for non-path states, but path states will fallback.
                         // No need to yield longer here, just log the specific issue.
                    }
                    // --- END NEW ---


                  // --- Use GridManager to find inactive NPCs near the player ---
                  // We simulate inactive NPCs that are *not* near enough to be activated,
                  // but still potentially within a larger simulation radius if needed.
                  // ProximityManager will define the exact radius for 'Far' NPCs.
                  // Using ProximityManager's farRadius + cellSize for the query radius.
                  float simulationQueryRadius = (proximityManager != null ? proximityManager.farRadius : 30f) + (gridManager != null ? gridManager.cellSize : 5f); // Query slightly beyond far radius

                  List<TiNpcData> potentialSimulationBatch = gridManager.QueryItemsInRadius(playerTransform.position, simulationQueryRadius);

                  // Filter for only inactive NPCs from the queried list
                  List<TiNpcData> inactiveBatch = potentialSimulationBatch.Where(data => !data.IsActiveGameObject).ToList();
                  // --- END NEW ---


                   int totalInactiveCount = inactiveBatch.Count;

                   if (totalInactiveCount == 0)
                   {
                        yield return new WaitForSeconds(simulationTickInterval * 2);
                        continue;
                   }

                   // Wrap the simulation index if it exceeds the total number of inactive NPCs
                   if (simulationIndex >= totalInactiveCount)
                   {
                        simulationIndex = 0;
                   }

                   // Get the batch from the list of inactive NPCs
                   // Ensure we don't try to Skip past the end of the list if totalInactiveCount is small
                   List<TiNpcData> currentBatch = inactiveBatch.Skip(simulationIndex).Take(maxNpcsToSimulatePerTick).ToList();


                  if (currentBatch.Count == 0)
                  {
                       // This can happen if simulationIndex is exactly totalInactiveCount and totalInactiveCount < maxNpcsToSimulatePerTick
                       // In this case, we processed the last batch in the previous tick, and simulationIndex wrapped to 0
                       // but the list is now empty. Just continue to the next wait period.
                       // Debug.LogWarning($"SIM TiNpcManager: Current batch is empty despite total inactive count being {totalInactiveCount}! Check maxNpcsToSimulatePerTick ({maxNpcsToSimulatePerTick}) or logic.");
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
                           Debug.LogError($"SIM TiNpcManager: Found ACTIVE NPC '{npcData.Id}' (GameObject '{npcData.NpcGameObject?.name ?? "NULL"}') in the INACTIVE batch after filtering! This should not happen. Skipping simulation for this NPC.", npcData.NpcGameObject);
                           countProcessedThisTick++; // Still count this entry as processed in the batch
                           continue; // Skip simulation logic for this NPC
                       }

                       // --- DELEGATE SIMULATION TO BASICNPCSTATEMANAGER ---
                       // BasicNpcStateManager will handle calling GridManager.UpdateItemPosition after simulation tick
                       basicNpcStateManager.SimulateTickForNpc(npcData, simulationTickInterval);
                       // --- END DELEGATION ---

                       countProcessedThisTick++;
                  }

                  // Advance the simulation index by the number of NPCs processed in this batch.
                  // This must be done *after* iterating the batch.
                  simulationIndex += batchSize;

                  // Wrap the simulation index should now wrap based on the total number of *inactive* NPCs found in the query,
                  // NOT the total number of all NPCs. Recalculate total inactive count from the queried list.
                   totalInactiveCount = inactiveBatch.Count; // Use the count from the list queried this tick
                  if (totalInactiveCount > 0) // Avoid division by zero or wrapping when list is empty
                  {
                      simulationIndex %= totalInactiveCount;
                  } else {
                       simulationIndex = 0; // Reset if no inactive NPCs left in the query area
                  }

                  // Log simulation tick summary (Optional, can be noisy)
                  // Debug.Log($"SIM TiNpcManager: Simulated {countProcessedThisTick} inactive NPCs this tick. Total inactive in query area: {totalInactiveCount}. Next batch starts at index {simulationIndex}.");
             }
        }


         /// <summary>
         /// Handles the process of activating a single TI NPC.
         /// Gets a pooled GameObject and calls the Runner's Activate method.
         /// Called by ProximityManager.
         /// </summary>
         /// <param name="tiData">The persistent data of the NPC to activate.</param>
         public void RequestActivateTiNpc(TiNpcData tiData) // <-- NEW Public Method
         {
               // Check if it's genuinely inactive before proceeding
              if (tiData == null || tiData.IsActiveGameObject || tiData.NpcGameObject != null)
              {
                   Debug.Log($"PROXIMITY TiNpcManager: Skipping activation attempt for '{tiData?.Id ?? "NULL"}'. Reason: tiData is null ({tiData == null}), IsActiveGameObject={tiData?.IsActiveGameObject}, NpcGameObject is null={(tiData?.NpcGameObject == null)}.");
                   return; // Already active or invalid
              }

              if (tiNpcPrefabs == null || tiNpcPrefabs.Count == 0 || poolingManager == null || customerManager == null || basicNpcStateManager == null || gridManager == null || waypointManager == null) // Added waypointManager check
              {
                   Debug.LogError("TiNpcManager: Cannot activate TI NPC. Required manager (TI Prefabs list, PoolingManager, CustomerManager, BasicNpcStateManager, GridManager, or WaypointManager) is null.", this); // Updated log
                   return;
              }

              GameObject prefabToUse = tiNpcPrefabs[UnityEngine.Random.Range(0, tiNpcPrefabs.Count)]; // Pick a random TI prefab
              GameObject npcObject = poolingManager.GetPooledObject(prefabToUse);

              if (npcObject != null)
              {
                   NpcStateMachineRunner runner = npcObject.GetComponent<NpcStateMachineRunner>();
                   if (runner != null)
                   {
                       Debug.Log($"PROXIMITY TiNpcManager: Activating TI NPC '{tiData.Id}'. Linking data to GameObject '{npcObject.name}'.");

                       // --- Store GameObject reference and update flags on TiNpcData ---
                       tiData.LinkGameObject(npcObject); // Use helper to set NpcGameObject and isActiveGameObject=true
                       // --- END ---

                       // --- Determine the starting state based on saved data (MODIFIED HERE) ---
                       Enum savedStateEnum = tiData.CurrentStateEnum;
                       Enum startingActiveStateEnum = null; // The active state we will transition to
                       bool handledActivationBySavedState = false; // Flag to know if we used the specific logic below

                         // --- Handle activation from BasicWaitForCashierState ---
                         if (savedStateEnum != null && savedStateEnum.Equals(BasicState.BasicWaitForCashier))
                         {
                              Debug.Log($"PROXIMITY {tiData.Id}: Saved state is BasicWaitForCashier. Checking live queue/register status.", npcObject);
                              handledActivationBySavedState = true; // We are handling this specific case

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
                                             Debug.Log($"PROXIMITY {tiData.Id}: Successfully joined main queue at spot {assignedSpotIndex}. Activating to Queue state.", npcObject);
                                             // Use the new SetupQueueSpot method to configure the handler and runner target
                                             queueHandler.SetupQueueSpot(assignedSpotTransform, assignedSpotIndex, QueueType.Main);
                                             startingActiveStateEnum = CustomerState.Queue;
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
                         // --- END Handle activation from BasicWaitForCashierState ---

                         // --- Handle activation from BasicBrowseState ---
                         else if (savedStateEnum != null && savedStateEnum.Equals(BasicState.BasicBrowse))
                         {
                              Debug.Log($"PROXIMITY {tiData.Id}: Saved state is BasicBrowse. Getting a new browse location from CustomerManager.", npcObject);
                              handledActivationBySavedState = true; // We are handling this specific case

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

                         // --- NEW: Handle activation from BasicPathState ---
                         else if (savedStateEnum != null && savedStateEnum.Equals(BasicPathState.BasicFollowPath))
                         {
                              Debug.Log($"PROXIMITY {tiData.Id}: Saved state is BasicFollowPath. Restoring path progress.", npcObject);
                              handledActivationBySavedState = true; // We are handling this specific case

                              // Get the corresponding active path state enum
                              startingActiveStateEnum = GetActiveStateFromBasicState(savedStateEnum); // Should map to PathState.FollowPath

                              // Check if path data is valid
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
                                   // Path data seems valid, restore it on the active handler *after* Runner.Activate
                                   // The Runner.Activate call will transition to the PathState.FollowPath.
                                   // The PathState.OnEnter will handle the initial NavMesh leg if needed,
                                   // or the PathFollowingHandler.RestorePathProgress (Substep 4.5) will be called
                                   // to skip the NavMesh leg if they were mid-path.

                                   // We need to pass the path data to the Runner/PathFollowingHandler.
                                   // The Runner.Activate method needs to be updated to accept this data,
                                   // or the PathFollowingHandler needs a public method to restore state.
                                   // Let's add a public method to PathFollowingHandler (Substep 4.5)
                                   // and call it *after* runner.Activate returns.

                                   // Clear simulation data as active state takes over
                                   tiData.simulatedTargetPosition = null; // Clear simulated target
                                   tiData.simulatedStateTimer = 0f; // Reset timer
                                   // Note: We do NOT clear simulatedPathID, simulatedWaypointIndex, simulatedFollowReverse, isFollowingPathBasic here.
                                   // These are needed by the PathFollowingHandler.RestorePathProgress method (Substep 4.5).
                                   // They will be cleared by the PathFollowingHandler itself when it stops following the path.
                              }
                         }
                         // --- END NEW ---

                         // --- Existing Logic (for states OTHER THAN BasicWaitForCashier, BasicBrowse, BasicPathState) ---
                         if (!handledActivationBySavedState) // Only run this if the specific BasicState cases were NOT handled
                         {
                              if (savedStateEnum != null && basicNpcStateManager.IsBasicState(savedStateEnum))
                              {
                                   // If the saved state is a BasicState (but NOT the special cases), map it to the corresponding Active State
                                   startingActiveStateEnum = GetActiveStateFromBasicState(savedStateEnum);
                                   Debug.Log($"PROXIMITY {tiData.Id}: Saved state '{savedStateEnum.GetType().Name}.{savedStateEnum.ToString()}' is a Basic State (generic mapping). Mapping to Active State '{startingActiveStateEnum?.GetType().Name}.{startingActiveStateEnum?.ToString() ?? "NULL"}'.", npcObject);
                                   // Reset simulation data when transitioning from simulation to active
                                   tiData.simulatedTargetPosition = null; // Clear simulated target
                                   tiData.simulatedStateTimer = 0f; // Reset timer on activation
                                   // Clear path simulation data
                                   tiData.simulatedPathID = null;
                                   tiData.simulatedWaypointIndex = -1;
                                   tiData.simulatedFollowReverse = false;
                                   tiData.isFollowingPathBasic = false;
                              }
                              else if (savedStateEnum != null)
                              {
                                   // If the saved state is NOT a BasicState (e.g., an old Active state saved directly),
                                   // try to use it directly as the starting active state.
                                   startingActiveStateEnum = savedStateEnum;
                                   Debug.Log($"PROXIMITY {tiData.Id}: Saved state '{savedStateEnum.GetType().Name}.{savedStateEnum.ToString()}' is NOT a Basic State. Attempting to use as Active starting state.", npcObject);
                                   // Clear existing simulation data anyway as it's old simulation data.
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
                                   // If no state was saved, or mapping failed, startingActiveStateEnum remains null.
                                   // The Runner will then fall back to its GetPrimaryStartingStateSO logic (which checks TypeDefs).
                                   Debug.Log($"PROXIMITY {tiData.Id}: No valid saved state found or mapped. Runner will determine primary starting state from TypeDefs.", npcObject);
                                   // Ensure simulation data is clean if no saved state existed
                                   tiData.simulatedTargetPosition = null;
                                   tiData.simulatedStateTimer = 0f;
                                   // Clear path simulation data
                                   tiData.simulatedPathID = null;
                                   tiData.simulatedWaypointIndex = -1;
                                   tiData.simulatedFollowReverse = false;
                                   tiData.isFollowingPathBasic = false;
                              }
                         }
                         // --- END Existing Logic ---

                         // Call the Runner's Activate method with the determined starting state override
                         // If startingActiveStateEnum is null, Runner.Activate will use GetPrimaryStartingStateSO().
                         runner.Activate(tiData, customerManager, startingActiveStateEnum); // <-- Pass determined override state Enum

                         Debug.Log($"PROXIMITY TiNpcManager: Activation initiated for TI NPC '{tiData.Id}' (GameObject '{npcObject.name}'). Runner.Activate called with override state: {startingActiveStateEnum?.GetType().Name}.{startingActiveStateEnum?.ToString() ?? "NULL"}");

                         // --- NEW: If activating into a PathState, restore path progress on the handler ---
                         // This happens *after* Runner.Activate has set up the runner and transitioned to the state.
                         // The PathState.OnEnter will run, but if the NPC was mid-path, we need to tell the handler
                         // to skip the NavMesh leg and resume path following from the saved index.
                         if (startingActiveStateEnum != null && startingActiveStateEnum.Equals(PathState.FollowPath))
                         {
                              // Check if the data indicates they were mid-path simulation
                              if (tiData.isFollowingPathBasic && !string.IsNullOrWhiteSpace(tiData.simulatedPathID) && tiData.simulatedWaypointIndex != -1)
                              {
                                   Debug.Log($"PROXIMITY {tiData.Id}: Activating into PathState. Restoring path progress on handler: PathID='{tiData.simulatedPathID}', Index={tiData.simulatedWaypointIndex}, Reverse={tiData.simulatedFollowReverse}.", npcObject);
                                   // Get the PathSO asset
                                   PathSO pathSOToRestore = waypointManager?.GetPath(tiData.simulatedPathID);
                                   if (pathSOToRestore != null)
                                   {
                                        // Call a new method on the PathFollowingHandler to restore state (Substep 4.5)
                                        runner.PathFollowingHandler?.RestorePathProgress(pathSOToRestore, tiData.simulatedWaypointIndex, tiData.simulatedFollowReverse);
                                   } else {
                                        Debug.LogError($"PROXIMITY {tiData.Id}: PathSO '{tiData.simulatedPathID}' not found via WaypointManager during PathState activation restore! Cannot restore path progress. NPC will likely start path from beginning.", npcObject);
                                        // The PathState.OnEnter will handle starting from the beginning (index 0) as a fallback.
                                   }
                              } else {
                                   // Not mid-path simulation, PathState.OnEnter will handle starting from the beginning (index 0)
                                   Debug.Log($"PROXIMITY {tiData.Id}: Activating into PathState, but not mid-path simulation. PathState.OnEnter will handle starting from beginning.", npcObject);
                              }
                         }
                         // --- END NEW ---


                   }
                   else
                   {
                       Debug.LogError($"TiNpcManager: Pooled object '{npcObject.name}' is missing NpcStateMachineRunner during activation! Returning invalid object to pool.", npcObject);
                        // Unlink data because activation failed
                        tiData.UnlinkGameObject(); // Use helper to clear data link and flags
                       poolingManager.ReturnPooledObject(npcObject); // Return invalid object
                   }
              }
              else
              {
                  Debug.LogError($"TiNpcManager: Failed to get a pooled TI NPC GameObject for activation of '{tiData.Id}'! Pool might be exhausted.", this);
              }
         }

         /// <summary>
         /// Handles the process of deactivating a single TI NPC.
         /// Determines the correct Basic State, saves data, and triggers the pooling flow.
         /// Called by ProximityManager.
         /// </summary>
         /// <param name="tiData">The persistent data of the NPC to deactivate.</param>
         /// <param name="runner">The active NpcStateMachineRunner for this NPC.</param>
         public void RequestDeactivateTiNpc(TiNpcData tiData, NpcStateMachineRunner runner) // <-- NEW Public Method
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
                  // Save the determined Basic State to the TiNpcData
                   tiData.SetCurrentState(targetBasicState);
                   Debug.Log($"PROXIMITY {tiData.Id}: Active state '{currentActiveState?.GetType().Name}.{currentActiveState?.ToString() ?? "NULL"}' maps to Basic State '{targetBasicState.GetType().Name}.{targetBasicState.ToString()}'. Saving this state to TiData.");

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
                         tiData.simulatedPathID = runner.PathFollowingHandler.GetCurrentPathID();
                         tiData.simulatedWaypointIndex = runner.PathFollowingHandler.GetCurrentTargetWaypointIndex(); // Save the index they were moving *towards*
                         tiData.simulatedFollowReverse = runner.PathFollowingHandler.GetFollowReverse();
                         tiData.isFollowingPathBasic = true; // Flag that they were on a path simulation when deactivated

                         Debug.Log($"PROXIMITY {tiData.Id}: Saved CURRENT path progress: PathID='{tiData.simulatedPathID}', Index={tiData.simulatedWaypointIndex}, Reverse={tiData.simulatedFollowReverse}.", runner.gameObject);
                    } else {
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
                  if(targetBasicStateSO != null)
                  {
                      Debug.Log($"PROXIMITY {tiData.Id}: Calling OnEnter for Basic State '{targetBasicStateSO.name}' to initialize simulation data.", runner.gameObject);
                      targetBasicStateSO.OnEnter(tiData, basicNpcStateManager); // Pass the data and the manager
                  } else
                  {
                      // This shouldn't happen if mapping and GetBasicStateSO work, but defensive
                      Debug.LogError($"PROXIMITY {tiData.Id}: Could not get target Basic State SO for '{targetBasicState.GetType().Name}.{targetBasicState.ToString()}' during deactivation request. Cannot initialize simulation state data!", runner.gameObject);
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
                   if(npcObject.GetComponent<PooledObjectInfo>() != null) poolingManager.ReturnPooledObject(npcObject);
                   else Destroy(npcObject); // Fallback
                  return;
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
                 } else {
                      Debug.LogWarning($"POOL TiNpcManager: GridManager is null! Cannot update grid position for '{deactivatedTiData.Id}' on return to pool.", npcObject);
                 }
                 // --- END NEW ---

                 // --- NEW: Remove the runner from ProximityManager's active lists ---
                 if (proximityManager != null)
                 {
                      Debug.Log($"POOL TiNpcManager: Removing runner '{runner.gameObject.name}' from ProximityManager active lists.", runner.gameObject);
                      proximityManager.RemoveRunnerFromActiveLists(runner); // Call the new method in ProximityManager
                 } else {
                      Debug.LogWarning($"POOL TiNpcManager: ProximityManager is null! Cannot remove runner '{runner.gameObject.name}' from active lists.", runner.gameObject);
                 }
                 // --- END NEW ---


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
                      } else {
                           Debug.LogError($"POOL TiNpcManager: Runner flagged as TI ({runner.IsTrueIdentityNpc}), but TiData link was lost, and TiData ID '{runner.TiData.Id}' lookup failed! Cannot perform data cleanup.", npcObject);
                      }
                 }
                 else if (runner.IsTrueIdentityNpc)
                 {
                      Debug.LogError($"POOL TiNpcManager: Runner flagged as TI ({runner.IsTrueIdentityNpc}), but TiData link was lost and TiData ID was null/empty! Cannot perform data cleanup.", npcObject);
                 }
                 // If runner is not flagged as TI, the CustomerManager should have handled it.
                 // If we somehow get a non-TI here, it's a flow error, but pool it anyway.

                 // --- NEW: Attempt to remove the runner from ProximityManager's active lists even if data link is broken ---
                 if (proximityManager != null)
                 {
                      Debug.LogWarning($"POOL TiNpcManager: Attempting to remove runner '{runner.gameObject.name}' from ProximityManager active lists despite data link issue.", runner.gameObject);
                      proximityManager.RemoveRunnerFromActiveLists(runner); // Call the new method in ProximityManager
                 } else {
                      Debug.LogWarning($"POOL TiNpcManager: ProximityManager is null! Cannot remove runner '{runner.gameObject.name}' from active lists.", runner.gameObject);
                 }
                 // --- END NEW ---
             }


             // Ensure Shopper Inventory is Cleared for safety, regardless of data link state
             // NOTE: If TI Shopper data needs to be persistent, this Reset should be more selective
             // or happen elsewhere (e.g., only reset transient parts).
             // For now, Shopper is marked for persistence in the Runner's ResetRunnerTransientData
             // by *not* resetting if IsTrueIdentityNpc is true. Clearing it here is a conflict.
             // Let's remove clearing it here and rely on the Runner's Reset logic.
             // if (runner.Shopper != null)
             // {
             //      runner.Shopper.Reset(); // REMOVED
             //      Debug.Log($"POOL TiNpcManager: Shopper inventory reset attempted for returning GameObject '{npcObject.name}'.", npcObject); // REMOVED
             // }

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
             } else {
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
             // --- NEW: Check WaypointManager for dummy path data initialization ---
             if (waypointManager == null)
             {
                  Debug.LogWarning("TiNpcManager: WaypointManager not found. Dummy NPCs configured with BasicPathState will not have their path data initialized correctly.", this);
                  // Continue loading, but path data will be invalid.
             }
             // --- END NEW ---


             foreach (var entry in dummyNpcData)
             {
                  if (string.IsNullOrWhiteSpace(entry.id))
                  {
                       Debug.LogWarning("TiNpcManager: Skipping dummy NPC entry with empty or whitespace ID.", this);
                       continue;
                  }

                  if (allTiNpcs.ContainsKey(entry.id))
                  {
                       Debug.LogWarning($"TiNpcManager: Skipping duplicate dummy NPC entry with ID '{entry.id}'.", this);
                       continue;
                  }

                  TiNpcData newNpcData = new TiNpcData(entry.id, entry.homePosition, entry.homeRotation);

                  newNpcData.CurrentWorldPosition = newNpcData.HomePosition;
                  newNpcData.CurrentWorldRotation = newNpcData.HomeRotation;

                  // --- Initialize with the specified Basic State from dummy data ---
                   Enum initialBasicStateEnum = entry.initialBasicState; // Default to BasicState enum

                   // --- NEW: Check if a BasicPathState is configured instead ---
                   if (entry.initialBasicPathState != BasicPathState.None)
                   {
                       initialBasicStateEnum = entry.initialBasicPathState; // Use the BasicPathState if specified
                       Debug.Log($"TiNpcManager: Dummy NPC '{entry.id}' configured with initial Basic Path State '{initialBasicStateEnum}'.");

                       // --- Initialize path simulation data if starting in a BasicPathState ---
                       if (waypointManager != null && !string.IsNullOrWhiteSpace(entry.initialPathID))
                       {
                           PathSO initialPathSO = waypointManager.GetPath(entry.initialPathID);
                           if (initialPathSO != null && entry.initialStartIndex >= 0 && entry.initialStartIndex < initialPathSO.WaypointCount)
                           {
                                newNpcData.simulatedPathID = entry.initialPathID;
                                newNpcData.simulatedWaypointIndex = entry.initialStartIndex; // Start *at* this index
                                newNpcData.simulatedFollowReverse = entry.initialFollowReverse;
                                newNpcData.isFollowingPathBasic = true; // Flag as following a path

                                // Set initial position to the start waypoint's position
                                string startWaypointID = initialPathSO.GetWaypointID(entry.initialStartIndex);
                                Transform startWaypointTransform = waypointManager.GetWaypointTransform(startWaypointID);
                                if (startWaypointTransform != null)
                                {
                                     newNpcData.CurrentWorldPosition = startWaypointTransform.position;
                                     // Simulate initial rotation towards the next waypoint
                                     // Need to get the BasicPathStateSO to call its rotation helper or duplicate logic
                                     // Let's duplicate the rotation logic here for initialization simplicity
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
                                }
                                else
                                {
                                     Debug.LogError($"TiNpcManager: Dummy NPC '{entry.id}' configured with initial path '{entry.initialPathID}' (index {entry.initialStartIndex}), but start waypoint '{startWaypointID}' not found! Cannot initialize position. Falling back to BasicPatrol.", this);
                                     initialBasicStateEnum = BasicState.BasicPatrol; // Fallback state
                                     // Clear invalid path data
                                     newNpcData.simulatedPathID = null;
                                     newNpcData.simulatedWaypointIndex = -1;
                                     newNpcData.simulatedFollowReverse = false;
                                     newNpcData.isFollowingPathBasic = false;
                                     newNpcData.CurrentWorldPosition = newNpcData.HomePosition; // Reset position
                                     newNpcData.CurrentWorldRotation = newNpcData.HomeRotation; // Reset rotation
                                }

                           } else {
                                Debug.LogError($"TiNpcManager: Dummy NPC '{entry.id}' configured with initial path '{entry.initialPathID}' (index {entry.initialStartIndex}), but PathSO not found or index invalid! Cannot initialize path data. Falling back to BasicPatrol.", this);
                                initialBasicStateEnum = BasicState.BasicPatrol; // Fallback state
                                // Clear invalid path data
                                newNpcData.simulatedPathID = null;
                                newNpcData.simulatedWaypointIndex = -1;
                                newNpcData.simulatedFollowReverse = false;
                                newNpcData.isFollowingPathBasic = false;
                           }
                       } else if (entry.initialBasicPathState != BasicPathState.None)
                       {
                            Debug.LogWarning($"TiNpcManager: Dummy NPC '{entry.id}' configured with BasicPathState '{initialBasicStateEnum}', but no initial Path ID provided or WaypointManager is null. Cannot initialize path data. Falling back to BasicPatrol.", this);
                            initialBasicStateEnum = BasicState.BasicPatrol; // Fallback state
                       }
                       // Note: If falling back to BasicPatrol, the BasicPatrolStateSO.OnEnter will handle its own initialization.
                   }
                   // --- END NEW ---


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
                   initialStateSO.OnEnter(newNpcData, basicNpcStateManager); // Pass the data and the manager

                  // Flags and GameObject link are initialized in the TiNpcData constructor (isActiveGameObject=false, NpcGameObject=null)

                  allTiNpcs.Add(newNpcData.Id, newNpcData);

                  // --- Add the newly loaded NPC data to the grid ---
                  if (gridManager != null)
                  {
                      gridManager.AddItem(newNpcData, newNpcData.CurrentWorldPosition);
                      Debug.Log($"TiNpcManager: Loaded dummy NPC '{newNpcData.Id}' with initial Basic State '{initialBasicStateEnum}'. Simulation timer initialized to {newNpcData.simulatedStateTimer:F2}s, Target: {newNpcData.simulatedTargetPosition?.ToString() ?? "NULL"}. Path Data: Following={newNpcData.isFollowingPathBasic}, ID='{newNpcData.simulatedPathID}', Index={newNpcData.simulatedWaypointIndex}, Reverse={newNpcData.simulatedFollowReverse}. Added to grid.", this); // Updated log
                  } else {
                       Debug.Log($"TiNpcManager: Loaded dummy NPC '{newNpcData.Id}' with initial Basic State '{initialBasicStateEnum}'. Simulation timer initialized to {newNpcData.simulatedStateTimer:F2}s, Target: {newNpcData.simulatedTargetPosition?.ToString() ?? "NULL"}. Path Data: Following={newNpcData.isFollowingPathBasic}, ID='{newNpcData.simulatedPathID}', Index={newNpcData.simulatedWaypointIndex}, Reverse={newNpcData.simulatedFollowReverse}. GridManager is null, NOT added to grid.", this); // Updated log
                  }
             }
        }
        // --- End Restored LoadDummyNpcData ---
    }
}