// --- START OF FILE TiNpcManager.cs ---

// --- Updated TiNpcManager.cs (Added Inactive Gizmo Visualization) ---

using UnityEngine;
using System.Collections.Generic;
using Game.NPC.TI; // Needed for TiNpcData
using System.Linq; // Needed for LINQ (Where, FirstOrDefault)
using Game.NPC; // Needed for NpcStateMachineRunner, GeneralState, CustomerState
using Game.NPC.States; // Needed for State SOs (to check HandledState)
using Utils.Pooling; // Needed for PoolingManager
using System.Collections; // Needed for Coroutine
using System; // Needed for Enum and Type

namespace Game.NPC.TI // Keep in the TI namespace
{
    /// <summary>
    /// Manages the persistent data for True Identity (TI) NPCs.
    /// Handles loading, storing, and tracking TI NPC data independent of their GameObjects.
    /// Implements off-screen simulation logic and proximity-based activation/deactivation.
    /// Uses a single collection of data records and filters on the fly.
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
         [Tooltip("Reference to the Player's Transform for proximity checks.")]
        [SerializeField] private Transform playerTransform; // Assign Player Transform

        [Header("TI NPC Setup")]
        [Tooltip("List of NPC prefabs that this manager can pool and activate for TI NPCs.")]
        [SerializeField] private List<GameObject> tiNpcPrefabs;

        [Header("Simulation Settings (Phase 4)")]
        [Tooltip("The interval (in seconds) between simulation ticks for inactive NPCs.")]
        [SerializeField] private float simulationTickInterval = 0.1f; // Process a batch every 0.1 seconds (10 Hz)
        [Tooltip("The maximum number of inactive NPCs to simulate per tick.")]
        [SerializeField] private int maxNpcsToSimulatePerTick = 10; // Process 10 NPCs per tick

        [Tooltip("Simulated speed for off-screen movement (units per second).")]
        [SerializeField] private float simulatedMovementSpeed = 3.5f; // Match NavMeshAgent speed roughly

         [Tooltip("Configured patrol area for simulating inactive Patrol state.")]
         [SerializeField] private Vector2 simulatedPatrolAreaMin = new Vector2(-10f, -10f); // Match PatrolStateSO
         [SerializeField] private Vector2 simulatedPatrolAreaMax = new Vector2(10f, 10f);
        [Tooltip("Minimum simulated wait time at a patrol point.")]
        [SerializeField] private float simulatedMinWaitTimeAtPoint = 1f; // Match PatrolStateSO
        [Tooltip("Maximum simulated wait time at a patrol point.")]
        [SerializeField] private float simulatedMaxWaitTimeAtPoint = 3f;
        [Tooltip("Simulated probability (0-1) of a TI NPC transitioning to LookingToShop when inactive after waiting at a patrol point.")]
        [Range(0f, 1f)][SerializeField] private float simulatedChanceToShop = 0.2f; // Match PatrolStateSO


        [Header("Proximity Activation Settings (Phase 4)")]
        [Tooltip("The radius around the player within which inactive NPCs should be activated.")]
        [SerializeField] private float activationRadius = 20f; // Distance to activate
        [Tooltip("The radius around the player outside of which active NPCs should be deactivated.")]
        [SerializeField] private float deactivationRadius = 25f; // Distance to deactivate (should be > activationRadius)
        [Tooltip("The interval (in seconds) to check proximity for activation/deactivation.")]
        [SerializeField] private float proximityCheckInterval = 1.0f; // Check proximity every second


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
        }

        // --- Persistent Data Storage ---
        // Use a single dictionary as the source of truth for all TI NPC data
        private Dictionary<string, TiNpcData> allTiNpcs = new Dictionary<string, TiNpcData>();

        // REMOVED: activeTiNpcs and inactiveTiNpcs lists are no longer maintained.
        // Status is determined by the IsActiveGameObject flag on TiNpcData.

        // Internal index for round-robin simulation (iterating over the full collection)
        private int simulationIndex = 0;

         // Coroutine references
         private Coroutine simulationCoroutine;
         private Coroutine proximityCheckCoroutine;


        private void Awake()
        {
            // Implement singleton pattern
            if (Instance == null)
            {
                Instance = this;
                // Optional: DontDestroyOnLoad(gameObject);
            }
            else
            {
                Debug.LogWarning("TiNpcManager: Duplicate instance found. Destroying this one.", this);
                Destroy(gameObject);
                return;
            }

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

             // Validate Player Transform
             if (playerTransform == null)
             {
                  // Attempt to find Player by tag if not assigned
                  GameObject playerGO = GameObject.FindGameObjectWithTag("Player"); // Assumes Player has "Player" tag
                  if (playerGO != null) playerTransform = playerGO.transform;
                  else Debug.LogError("TiNpcManager: Player Transform not assigned and GameObject with tag 'Player' not found! Proximity checks will not work.", this);
             }
             // Validate activation/deactivation radii
             if (activationRadius >= deactivationRadius)
             {
                 Debug.LogWarning("TiNpcManager: Activation Radius should be smaller than Deactivation Radius for proper hysteresis. Setting deactivationRadius to activationRadius + 1.", this);
                 deactivationRadius = activationRadius + 1f;
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

            // REMOVED: Initial population of active/inactive lists is no longer needed.

             Debug.Log($"TiNpcManager: Start completed.");

            // Start the simulation loop
            simulationCoroutine = StartCoroutine(SimulateInactiveNpcsRoutine());

            // Start the proximity check coroutine
             if (playerTransform != null)
             {
                proximityCheckCoroutine = StartCoroutine(ProximityCheckRoutine());
                Debug.Log("TiNpcManager: Proximity check coroutine started.");
             }
             else
             {
                  Debug.LogWarning("TiNpcManager: Player Transform is null, cannot start proximity checks.");
             }
        }

        private void OnEnable()
        {
             // Restart simulation coroutine if manager was disabled and re-enabled
             if (simulationCoroutine == null && allTiNpcs.Count > 0) // Check total count now
             {
                  Debug.Log("TiNpcManager: Restarting simulation coroutine on OnEnable.");
                  simulationCoroutine = StartCoroutine(SimulateInactiveNpcsRoutine());
             }
             // Restart proximity check coroutine if needed
             if (proximityCheckCoroutine == null && playerTransform != null)
             {
                  Debug.Log("TiNpcManager: Restarting proximity check coroutine on OnEnable.");
                  proximityCheckCoroutine = StartCoroutine(ProximityCheckRoutine());
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
             // Stop proximity check coroutine
             if (proximityCheckCoroutine != null)
             {
                  Debug.Log("TiNpcManager: Stopping proximity check coroutine on OnDisable.");
                  StopCoroutine(proximityCheckCoroutine);
                  proximityCheckCoroutine = null;
             }
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                // TODO: In a real game, save all TiNpcData here before clearing.
                allTiNpcs.Clear();
                Instance = null;
                 Debug.Log("TiNpcManager: OnDestroy completed. Data cleared.");
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
                     // Optional: Draw the ID as text for easier identification
                     // Handles.Label(tiData.CurrentWorldPosition + Vector3.up * (inactiveGizmoRadius + 0.1f), tiData.Id); // Requires using UnityEditor; and Handles
                 }
             }
        }
        // --- END DEBUG ---


        /// <summary>
        /// The low-tick simulation routine. Iterates over all TI NPC data but only simulates inactive ones in batches.
        /// </summary>
        private IEnumerator SimulateInactiveNpcsRoutine()
        {
             while (true)
             {
                  yield return new WaitForSeconds(simulationTickInterval);

                  if (allTiNpcs.Count == 0) // Check total count now
                  {
                       // --- DEBUG: Log when skipping simulation because no NPCs ---
                       // Debug.Log("DEBUG Simulation Tick: No TI NPCs to simulate."); // Too noisy
                       // --- END DEBUG ---
                       yield return new WaitForSeconds(simulationTickInterval * 5); // Wait a bit longer if empty
                       continue;
                  }

                  // Get a list of inactive NPCs to process this tick (efficiently using LINQ)
                  // Skip using simulationIndex directly to avoid issues with list modification during iteration.
                  // Instead, filter all NPCs and take a batch starting from the current index.
                  List<TiNpcData> inactiveBatch = allTiNpcs.Values
                      .Where(data => !data.IsActiveGameObject) // Filter for inactive ones
                      // The simulationIndex should now advance across the *filtered* list of inactive NPCs.
                      // Let's get the list of all inactive NPCs first, then process a batch from that list.
                       .ToList(); // Convert to a list of *all* inactive NPCs

                   // Calculate the index within the *inactive list*
                   int totalInactiveCount = inactiveBatch.Count;

                   if (totalInactiveCount == 0)
                   {
                        // No inactive NPCs to simulate this tick
                        // Debug.Log("DEBUG Simulation Tick: No inactive NPCs currently."); // Too noisy
                        yield return new WaitForSeconds(simulationTickInterval * 2);
                        continue;
                   }

                   // Wrap the simulation index if it exceeds the total number of inactive NPCs
                   if (simulationIndex >= totalInactiveCount)
                   {
                        simulationIndex = 0;
                        // Debug.Log("DEBUG Simulation Tick: Wrapped simulation index.");
                   }

                   // Get the batch from the list of inactive NPCs
                   List<TiNpcData> currentBatch = inactiveBatch.Skip(simulationIndex).Take(maxNpcsToSimulatePerTick).ToList();


                  if (currentBatch.Count == 0)
                  {
                       // This should only happen if maxNpcsToSimulatePerTick is 0, or something is wrong.
                       Debug.LogWarning($"DEBUG Simulation Tick: Current batch is empty despite total inactive count being {totalInactiveCount}! Check maxNpcsToSimulatePerTick ({maxNpcsToSimulatePerTick}) or logic.");
                        yield return new WaitForSeconds(simulationTickInterval * 2);
                        continue;
                  }


                   // Process the batch
                  int countProcessedThisTick = 0;
                   int batchSize = currentBatch.Count; // Use batch size for index advancement

                  foreach (var npcData in currentBatch) // Iterate over the temporary batch list
                  {
                       // --- DEBUG: Check IsActiveGameObject *before* simulating ---
                       // This check is crucial to confirm if an active NPC is mistakenly in this list (should not happen with this approach)
                       // The Where clause already filtered for !data.IsActiveGameObject, so this check should always be false.
                       Debug.Log($"DEBUG Simulation Tick: Processing NPC '{npcData.Id}' (Data InstanceID: {npcData.GetHashCode()}). IsActiveGameObject: {npcData.IsActiveGameObject}. NpcGameObject is null: {(npcData.NpcGameObject == null)}.", npcData.NpcGameObject);
                       if (npcData.IsActiveGameObject)
                       {
                           // This is an error condition given the filtering, but log defensively.
                           Debug.LogError($"DEBUG Simulation Tick: Found ACTIVE NPC '{npcData.Id}' (GameObject '{npcData.NpcGameObject?.name ?? "NULL"}') in the INACTIVE batch after filtering! This should not happen. Skipping.", npcData.NpcGameObject);
                           countProcessedThisTick++; // Still count this entry as processed in the batch
                           continue; // Skip simulation logic for this NPC
                       }
                       // --- END DEBUG ---


                       // --- Simulate State Logic (Only runs if IsActiveGameObject is false) ---
                       System.Enum currentStateEnum = npcData.CurrentStateEnum;

                       if (currentStateEnum != null)
                       {
                            // Simulate Patrol State
                            if (currentStateEnum.Equals(TestState.Patrol))
                            {
                                 if (npcData.simulatedTargetPosition == null || Vector3.Distance(npcData.CurrentWorldPosition, npcData.simulatedTargetPosition.Value) < 0.1f)
                                 {
                                      // Reached previous target or no target, simulate waiting
                                      if (npcData.simulatedStateTimer <= 0)
                                      {
                                           npcData.simulatedStateTimer = UnityEngine.Random.Range(simulatedMinWaitTimeAtPoint, simulatedMaxWaitTimeAtPoint);
                                           // --- DEBUG: Log starting wait ---
                                           Debug.Log($"DEBUG Simulation Tick: NPC '{npcData.Id}' reached simulated patrol target. Starting simulated wait timer for {npcData.simulatedStateTimer:F2}s.", npcData.NpcGameObject);
                                           // --- END DEBUG ---
                                      }
                                      else
                                      {
                                           npcData.simulatedStateTimer -= simulationTickInterval;

                                           if (npcData.simulatedStateTimer <= 0)
                                           {
                                                bool decidedToShop = UnityEngine.Random.value <= simulatedChanceToShop;
                                                 // --- DEBUG: Log wait finish and decision ---
                                                 Debug.Log($"DEBUG Simulation Tick: NPC '{npcData.Id}' finished simulated wait. Decided to shop: {decidedToShop}.", npcData.NpcGameObject);
                                                 // --- END DEBUG ---

                                                if (decidedToShop)
                                                {
                                                     npcData.SetCurrentState(CustomerState.LookingToShop); // TiNpcData SetCurrentState logs
                                                     npcData.simulatedTargetPosition = null;
                                                }
                                                else
                                                {
                                                     Vector3 randomPoint = GetRandomPointInPatrolAreaSimulated();
                                                     npcData.simulatedTargetPosition = randomPoint;
                                                      // TiNpcData SetCurrentState logs state change, but we didn't change state here (still Patrol)
                                                      Debug.Log($"DEBUG Simulation Tick: NPC '{npcData.Id}' decided to continue patrolling. Setting new simulated patrol target: {randomPoint}.", npcData.NpcGameObject);
                                                }
                                                npcData.simulatedStateTimer = 0; // Reset timer
                                           }
                                            // else { Debug.Log($"DEBUG Simulation Tick: NPC '{npcData.Id}' waiting... {npcData.simulatedStateTimer:F2}s remaining."); } // Too noisy
                                      }
                                 }
                                 else // Moving towards target
                                 {
                                      Vector3 direction = (npcData.simulatedTargetPosition.Value - npcData.CurrentWorldPosition).normalized;
                                      float moveDistance = simulatedMovementSpeed * simulationTickInterval;
                                      npcData.CurrentWorldPosition += direction * moveDistance;
                                       // Debug.Log($"DEBUG Simulation Tick: NPC '{npcData.Id}' moving towards {npcData.simulatedTargetPosition.Value}. New Pos: {npcData.CurrentWorldPosition}."); // Too noisy
                                 }
                            }
                            // Simulate Other States (Simplifications for Phase 4)
                             // Added more specific simulation logic based on common states
                            else if (currentStateEnum.Equals(CustomerState.LookingToShop))
                            {
                                // Simulation cannot handle queues or store capacity.
                                // In simulation, LookingToShop could decide based on simple factors (e.g., always try to enter/shop, or always go back to patrol).
                                // Simplified: Always go back to Patrol in simulation for LookingToShop if they have no items. If they have items, simulate finishing shopping and exiting.
                                if (npcData.simulatedStateTimer <= 0) // If timer hasn't started
                                {
                                     // Start a short timer simulating the decision-making time
                                     npcData.simulatedStateTimer = UnityEngine.Random.Range(1f, 3f);
                                     Debug.Log($"DEBUG Simulation Tick: NPC '{npcData.Id}' in simulated LookingToShop. Starting decision timer for {npcData.simulatedStateTimer:F2}s.", npcData.NpcGameObject);
                                }
                                else
                                {
                                     npcData.simulatedStateTimer -= simulationTickInterval;
                                     if (npcData.simulatedStateTimer <= 0)
                                     {
                                          // Assuming Shopper.HasItems isn't easily simulated here without more data.
                                          // Simple Rule: Simulate trying to shop, then either exiting (if "bought" enough) or going back to patrol.
                                          // For simulation simplicity, just transition to Exiting or Patrol randomly after the timer.
                                           bool simulateFinishedShopping = UnityEngine.Random.value < 0.5f; // 50/50 chance to finish shopping flow
                                          if (simulateFinishedShopping)
                                          {
                                               Debug.Log($"DEBUG Simulation Tick: NPC '{npcData.Id}' finished simulated LookToShop decision. Simulating shopping complete -> exiting flow.", npcData.NpcGameObject);
                                               npcData.SetCurrentState(CustomerState.Exiting); // Transition to simulated exit
                                               npcData.simulatedTargetPosition = null;
                                               npcData.simulatedStateTimer = 0f;
                                          } else
                                          {
                                              Debug.Log($"DEBUG Simulation Tick: NPC '{npcData.Id}' finished simulated LookToShop decision. Simulating not shopping -> patrol.", npcData.NpcGameObject);
                                              npcData.SetCurrentState(TestState.Patrol); // Transition back to simulated patrol
                                              npcData.simulatedTargetPosition = null;
                                              npcData.simulatedStateTimer = 0f;
                                          }
                                     }
                                }
                            }
                             else if (currentStateEnum.Equals(CustomerState.Browse))
                             {
                                  // Simulate browsing time, then transition to LookingToShop (to simulate decision logic) or Exiting/Queue.
                                   if (npcData.simulatedStateTimer <= 0) // If timer hasn't started
                                   {
                                        // Start a browse timer
                                        npcData.simulatedStateTimer = UnityEngine.Random.Range(3f, 8f); // Match BrowseStateSO browseTimeRange
                                         Debug.Log($"DEBUG Simulation Tick: NPC '{npcData.Id}' in simulated Browse. Starting browse timer for {npcData.simulatedStateTimer:F2}s.", npcData.NpcGameObject);
                                   }
                                   else
                                   {
                                        npcData.simulatedStateTimer -= simulationTickInterval;
                                        if (npcData.simulatedStateTimer <= 0)
                                        {
                                             // After browsing, simulate making a decision (go to register/queue or another browse).
                                             // Simplification: Just go back to LookingToShop simulation for the next decision.
                                             Debug.Log($"DEBUG Simulation Tick: NPC '{npcData.Id}' finished simulated Browse. Transitioning to simulated LookingToShop for next decision.", npcData.NpcGameObject);
                                             npcData.SetCurrentState(CustomerState.LookingToShop); // Transition to simulated decision state
                                             npcData.simulatedTargetPosition = null;
                                             npcData.simulatedStateTimer = 0f;
                                        }
                                   }
                             }
                             else if (currentStateEnum.Equals(CustomerState.Queue) || currentStateEnum.Equals(CustomerState.SecondaryQueue) || currentStateEnum.Equals(CustomerState.WaitingAtRegister))
                             {
                                 // Inactive in queue/waiting state: just wait. No complex simulation of queue movement.
                                 // Simulate impatience eventually transitioning to Exiting.
                                  // Use a long wait timer simulating queue time + potential impatience
                                  if (npcData.simulatedStateTimer <= 0)
                                  {
                                       // Start a long wait timer simulating queue time + impatience
                                       npcData.simulatedStateTimer = UnityEngine.Random.Range(20f, 50f); // Example: 20-50s simulated time in queue/wait
                                        Debug.Log($"DEBUG Simulation Tick: NPC '{npcData.Id}' in simulated Queue/Waiting state. Starting long wait timer for {npcData.simulatedStateTimer:F2}s.", npcData.NpcGameObject);
                                  }
                                  else
                                  {
                                       npcData.simulatedStateTimer -= simulationTickInterval;
                                       if (npcData.simulatedStateTimer <= 0)
                                       {
                                            Debug.Log($"DEBUG Simulation Tick: NPC '{npcData.Id}' finished simulated queue/wait. Transitioning to simulated Exiting (simulated impatience or transaction finish).", npcData.NpcGameObject);
                                            npcData.SetCurrentState(CustomerState.Exiting); // Transition to simulated exit
                                            npcData.simulatedTargetPosition = null;
                                            npcData.simulatedStateTimer = 0f;
                                       }
                                  }

                             }
                            else if (currentStateEnum.Equals(CustomerState.Exiting))
                            {
                                 // Inactive in Exiting state: Simulate finishing instantly and return to patrol.
                                  Debug.Log($"DEBUG Simulation Tick: NPC '{npcData.Id}' in simulated Exiting state. Simulating instantaneous exit and return to patrol.", npcData.NpcGameObject);
                                  npcData.SetCurrentState(TestState.Patrol); // TiNpcData SetCurrentState logs
                                  npcData.simulatedTargetPosition = null;
                                   npcData.simulatedStateTimer = 0f;
                            }
                             else if (currentStateEnum.Equals(GeneralState.ReturningToPool) || currentStateEnum.Equals(GeneralState.Initializing) || currentStateEnum.Equals(GeneralState.Death))
                             {
                                  // These should be very short or terminal states. Assume instant transition to patrol in simulation.
                                  Debug.Log($"DEBUG Simulation Tick: NPC '{npcData.Id}' found in transient simulated state '{currentStateEnum.GetType().Name}.{currentStateEnum.ToString()}'. Forcing back to Patrol.", npcData.NpcGameObject);
                                  npcData.SetCurrentState(TestState.Patrol); // TiNpcData SetCurrentState logs
                                  npcData.simulatedTargetPosition = null;
                                  npcData.simulatedStateTimer = 0f;
                             }
                             else // Unhandled inactive state (MovingToRegister, TransactionActive, Combat, Social, Emoting)
                             {
                                  // Log a warning and transition to Patrol as a safe default in simulation.
                                  Debug.LogWarning($"DEBUG Simulation Tick: NPC '{npcData.Id}': Found in unhandled inactive state '{currentStateEnum.GetType().Name}.{currentStateEnum.ToString()}'. Transitioning to Patrol as fallback.", npcData.NpcGameObject);
                                  npcData.SetCurrentState(TestState.Patrol); // TiNpcData SetCurrentState logs
                                  npcData.simulatedTargetPosition = null;
                                  npcData.simulatedStateTimer = 0f; // Reset timer for unhandled states
                             }
                       }
                       else // Null state
                       {
                             Debug.LogWarning($"DEBUG Simulation Tick: NPC '{npcData.Id}': Found with null state. Transitioning to Patrol as fallback.", npcData.NpcGameObject);
                             npcData.SetCurrentState(TestState.Patrol); // TiNpcData SetCurrentState logs
                             npcData.simulatedTargetPosition = null;
                             npcData.simulatedStateTimer = 0f;
                       }
                       // --- End Simulate State Logic ---

                       countProcessedThisTick++;
                  }

                  // Advance the simulation index by the number of NPCs processed in this batch.
                  // This ensures round-robin processing across the *filtered list of inactive NPCs*.
                  // Use the size of the *currentBatch* to advance the index.
                  simulationIndex += batchSize;

                  // The simulation index should now wrap based on the total number of *inactive* NPCs,
                  // NOT the total number of all NPCs. Recalculate total inactive count after processing.
                   totalInactiveCount = allTiNpcs.Values.Count(data => !data.IsActiveGameObject);
                  if (simulationIndex >= totalInactiveCount && totalInactiveCount > 0)
                  {
                      simulationIndex = 0;
                  } else if (totalInactiveCount == 0)
                  {
                       simulationIndex = 0; // Reset if no inactive NPCs left
                  }


                  // Log simulation tick summary
                   Debug.Log($"DEBUG Simulation Tick: Processed {countProcessedThisTick} inactive NPCs this tick (batch size: {batchSize}). Total TI NPCs: {allTiNpcs.Count}. Total Inactive: {totalInactiveCount}. Next simulation index: {simulationIndex}.");
             }
        }

         /// <summary>
         /// Gets a random point within the defined XZ patrol area bounds (for simulation).
         /// Uses a fixed Y height (e.g., 0) for simplicity as NavMesh sampling is not available.
         /// </summary>
        private Vector3 GetRandomPointInPatrolAreaSimulated()
        {
             float randomX = UnityEngine.Random.Range(simulatedPatrolAreaMin.x, simulatedPatrolAreaMax.x);
             float randomZ = UnityEngine.Random.Range(simulatedPatrolAreaMin.y, simulatedPatrolAreaMax.y); // Note: using y for Z axis in Vector2
             return new Vector3(randomX, 0f, randomZ); // Assume ground is at Y=0 for simulation
        }
        // --- END PHASE 4, SUBSTEP 1 ---


        /// <summary>
        /// Proximity check routine. Iterates over all TI NPC data and triggers activation/deactivation.
        /// </summary>
         private IEnumerator ProximityCheckRoutine()
         {
              while (true) // Loop indefinitely
              {
                   yield return new WaitForSeconds(proximityCheckInterval);

                   if (playerTransform == null)
                   {
                        Debug.LogWarning("TiNpcManager: Player Transform is null, skipping proximity checks.");
                        yield return new WaitForSeconds(proximityCheckInterval * 5); // Wait longer if player is missing
                        continue; // Skip check logic
                   }

                   Vector3 playerPosition = playerTransform.position;

                   // --- Check for NPCs to ACTIVATE ---
                   List<TiNpcData> toActivate = new List<TiNpcData>();
                   // Iterate over all NPCs to find candidates for activation
                   foreach (var tiData in allTiNpcs.Values)
                   {
                       // Only consider for activation if genuinely inactive and has no GameObject link
                       if (!tiData.IsActiveGameObject && tiData.NpcGameObject == null)
                       {
                           float distanceSq = (tiData.CurrentWorldPosition - playerPosition).sqrMagnitude;
                           if (distanceSq <= activationRadius * activationRadius)
                           {
                                // --- DEBUG: Log NPC found for activation ---
                                Debug.Log($"DEBUG Proximity Check: Inactive NPC '{tiData.Id}' (Data InstanceID: {tiData.GetHashCode()}) within activation radius ({Mathf.Sqrt(distanceSq):F2}m). Adding to activation list.", tiData.NpcGameObject);
                                // --- END DEBUG ---
                                toActivate.Add(tiData);
                           }
                       }
                   }

                   if (toActivate.Count > 0)
                   {
                       Debug.Log($"DEBUG Proximity Check: Found {toActivate.Count} NPCs to activate this tick. Activating now...");
                        foreach (var tiData in toActivate)
                       {
                            ActivateTiNpc(tiData); // Call the activation method (links GameObject and sets flag)
                       }
                       // NO explicit list update needed here. The data is updated directly by LinkGameObject.
                   }


                   // --- Check for NPCs to DEACTIVATE ---
                   // Iterate over all NPCs to find candidates for deactivation.
                   // We need to iterate over a copy if triggering state transitions
                   // that might modify the collection during the loop, but iterating
                   // allTiNpcs.Values and triggering external events/calls is generally safe.
                   List<TiNpcData> allNpcsCopy = new List<TiNpcData>(allTiNpcs.Values);
                   foreach (var tiData in allNpcsCopy) // Iterate over a copy of all data
                   {
                        // Only consider for deactivation if genuinely active and has a GameObject link
                        if (tiData.IsActiveGameObject && tiData.NpcGameObject != null)
                        {
                           float distanceSq = (tiData.NpcGameObject.transform.position - playerPosition).sqrMagnitude; // Use GameObject position
                           if (distanceSq >= deactivationRadius * deactivationRadius)
                           {
                                // Check if the NPC is in a state that should prevent deactivation (e.g., Combat, Transaction)
                                NpcStateMachineRunner runner = tiData.NpcGameObject.GetComponent<NpcStateMachineRunner>();
                                if (runner != null)
                                {
                                     NpcStateSO currentStateSO = runner.GetCurrentState();
                                     if (currentStateSO != null && !currentStateSO.IsInterruptible) // Check if *current state* prevents interruption/deactivation
                                     {
                                          Debug.Log($"DEBUG Proximity Check: Deactivation check for '{tiData.Id}' (GameObject '{tiData.NpcGameObject.name}') failed. Current state '{currentStateSO.name}' is not interruptible. Skipping trigger this tick.", tiData.NpcGameObject);
                                          continue; // Skip deactivation attempt for this NPC this tick
                                     }

                                     // If the state IS interruptible (or no state/interruptible state) and outside radius, trigger deactivation
                                     Debug.Log($"DEBUG Proximity Check: TI NPC '{tiData.Id}' (GameObject '{tiData.NpcGameObject.name}', Data InstanceID: {tiData.GetHashCode()}) outside deactivation radius ({Mathf.Sqrt(distanceSq):F2}m). State is interruptible or null. Triggering TransitionToState(ReturningToPool).", tiData.NpcGameObject);
                                     // Transition the Runner to the ReturningToPool state.
                                     // This starts the entire deactivation/pooling flow.
                                     runner.TransitionToState(runner.GetStateSO(GeneralState.ReturningToPool));
                                     // The Runner.Deactivate() method will be called by TransitionToState before the state changes.
                                     // HandleTiNpcReturnToPool will be called later by the pooling event, clearing data link/flag.
                                }
                                else
                                {
                                     Debug.LogError($"TiNpcManager: Active TI NPC '{tiData.Id}' GameObject '{tiData.NpcGameObject.name}' missing Runner! Inconsistency detected. Forcing cleanup.", tiData.NpcGameObject);
                                     // Critical error - force clean up GameObject and update data state
                                      Destroy(tiData.NpcGameObject); // Destroy the GameObject
                                      tiData.UnlinkGameObject(); // Use helper to clear data link and flags
                                }
                           }
                        }
                   }
                   // NO explicit list update needed here. The data flags were updated directly by UnlinkGameObject.
              }
         }
        // --- END ProximityCheckRoutine ---


         /// <summary>
         /// Handles the process of activating a single TI NPC.
         /// Gets a pooled GameObject and calls the Runner's Activate method.
         /// </summary>
         /// <param name="tiData">The persistent data of the NPC to activate.</param>
         private void ActivateTiNpc(TiNpcData tiData)
         {
              // Check if it's genuinely inactive before proceeding
              if (tiData == null || tiData.IsActiveGameObject || tiData.NpcGameObject != null)
              {
                   // --- DEBUG: Log why activation was skipped ---
                   Debug.Log($"DEBUG TiNpcManager: Skipping activation attempt for '{tiData?.Id ?? "NULL"}' (Data InstanceID: {tiData?.GetHashCode() ?? 0}). Reason: tiData is null ({tiData == null}), IsActiveGameObject={tiData?.IsActiveGameObject}, NpcGameObject is null={(tiData?.NpcGameObject == null)}.", tiData?.NpcGameObject);
                   // --- END DEBUG ---
                   return; // Already active or invalid
              }

              if (tiNpcPrefabs == null || tiNpcPrefabs.Count == 0 || poolingManager == null || customerManager == null)
              {
                   Debug.LogError("TiNpcManager: Cannot activate TI NPC. TI Prefabs list, PoolingManager, or CustomerManager is null.", this);
                   return;
              }

              GameObject prefabToUse = tiNpcPrefabs[UnityEngine.Random.Range(0, tiNpcPrefabs.Count)]; // Pick a random TI prefab
              GameObject npcObject = poolingManager.GetPooledObject(prefabToUse);

              if (npcObject != null)
              {
                   NpcStateMachineRunner runner = npcObject.GetComponent<NpcStateMachineRunner>();
                   if (runner != null)
                   {
                       // --- DEBUG: Log flag status before linking ---
                        Debug.Log($"DEBUG TiNpcManager: ActivateTiNpc '{tiData.Id}' (Data InstanceID: {tiData.GetHashCode()}): Flag status BEFORE LinkGameObject: IsActiveGameObject={tiData.IsActiveGameObject}, NpcGameObject is null={(tiData.NpcGameObject == null)}.", npcObject);
                       // --- END DEBUG ---

                       // --- Store GameObject reference and update flags on TiNpcData ---
                       tiData.LinkGameObject(npcObject); // Use helper to set NpcGameObject and isActiveGameObject=true
                       // --- END ---

                       // --- DEBUG: Log flag status AFTER linking ---
                        Debug.Log($"DEBUG TiNpcManager: ActivateTiNpc '{tiData.Id}' (Data InstanceID: {tiData.GetHashCode()}): Flag status AFTER LinkGameObject: IsActiveGameObject={tiData.IsActiveGameObject}, NpcGameObject is null={(tiData.NpcGameObject == null)}.", npcObject);
                       // --- END DEBUG ---

                       // --- DEBUG: Assert that the flag is true immediately after setting ---
                        Debug.Assert(tiData.IsActiveGameObject, $"Assertion Failed: isActiveGameObject should be true immediately after LinkGameObject for '{tiData.Id}'!");
                       // --- END DEBUG ---


                        // Call the Runner's Activate method
                       runner.Activate(tiData, customerManager); // Pass the persistent data and CustomerManager
                        // Runner.Activate sets Runner.IsTrueIdentityNpc and Runner.TiData

                       // --- DEBUG: Log successful activation initiation ---
                       Debug.Log($"DEBUG TiNpcManager: Activation initiated for TI NPC '{tiData.Id}' (GameObject '{npcObject.name}'). Runner.Activate called.", npcObject);
                       // --- END DEBUG ---

                   }
                   else
                   {
                       Debug.LogError($"TiNpcManager: Pooled object '{npcObject.name}' is missing NpcStateMachineRunner during activation! Returning invalid object to pool.", npcObject);
                        // Unlink data because activation failed
                        tiData.UnlinkGameObject(); // Use helper to clear link and flags
                       poolingManager.ReturnPooledObject(npcObject); // Return invalid object
                   }
              }
              else
              {
                  Debug.LogError($"TiNpcManager: Failed to get a pooled TI NPC GameObject for activation of '{tiData.Id}'! Pool might be exhausted.", this);
              }
              // active/inactive lists are no longer managed here. Status is determined by IsActiveGameObject flag.
         }


        private void LoadDummyNpcData()
        {
             if (dummyNpcData == null || dummyNpcData.Count == 0)
             {
                  Debug.LogWarning("TiNpcManager: No dummy NPC data entries configured to load.", this);
                  return;
             }

             allTiNpcs.Clear();

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

                   newNpcData.SetCurrentState(TestState.Patrol); // TiNpcData SetCurrentState logs
                   newNpcData.simulatedTargetPosition = GetRandomPointInPatrolAreaSimulated(); // Give them a first target for simulation
                   newNpcData.simulatedStateTimer = 0f;

                  // Flags and GameObject link are initialized in the TiNpcData constructor now (isActiveGameObject=false, NpcGameObject=null)

                  allTiNpcs.Add(newNpcData.Id, newNpcData);
             }
        }

        // REMOVED: UpdateActiveInactiveLists is no longer needed.

        // --- Public Methods ---

        /// <summary>
        /// Called by CustomerManager when a TI NPC's GameObject is
        /// ready to be returned to the pool after deactivation.
        /// </summary>
        public void HandleTiNpcReturnToPool(GameObject npcObject)
        {
             // --- DEBUG: Log handler entry ---
             Debug.Log($"DEBUG TiNpcManager: HandleTiNpcReturnToPool received GameObject '{npcObject.name}'.", npcObject);
             // --- END DEBUG ---

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
                 // --- DEBUG: Log data found ---
                 Debug.Log($"DEBUG HandleTiNpcReturnToPool: Found TiNpcData for '{deactivatedTiData.Id}' linked to GameObject '{npcObject.name}' (Data InstanceID: {deactivatedTiData.GetHashCode()}). Unlinking data and flags.", npcObject);
                 // --- END DEBUG ---

                 // --- Clear the data link and flags ---
                 deactivatedTiData.UnlinkGameObject(); // Use helper to set NpcGameObject=null and isActiveGameObject=false
                 // --- END ---

                 // Ensure Runner's internal link is also cleared (should happen in Runner.Deactivate)
                 if (runner.TiData != null)
                 {
                     Debug.LogError($"DEBUG HandleTiNpcReturnToPool: Inconsistency! Runner for '{deactivatedTiData.Id}' still has TiData link! Clearing it.", npcObject);
                     runner.TiData = null;
                 }
                 if (runner.IsTrueIdentityNpc)
                 {
                      // This flag has a private setter, cannot clear directly, but indicates inconsistency
                      Debug.LogError($"DEBUG HandleTiNpcReturnToPool: Inconsistency! Runner for '{deactivatedTiData.Id}' still has IsTrueIdentityNpc=true!.", npcObject);
                 }

             }
             else
             {
                  // This warning indicates the NpcGameObject -> TiNpcData link was already broken before this handler was called.
                  // The runner's TiData should have been cleared in Runner.Deactivate.
                  // The IsActiveGameObject flag should also have been cleared by Runner.Deactivate.
                  // It might still be a TI NPC if it was pooled without a proper state transition.
                 Debug.LogWarning($"TiNpcManager: Could not find TiNpcData linked to returning GameObject '{npcObject.name}' in HandleTiNpcReturnToPool! Data link already lost or inconsistent.", npcObject);

                 // Defensive cleanup: If we couldn't find the linked data, how do we know it was a TI NPC?
                 // Rely on the Runner's flag set during activation, even if TiData link is broken.
                 if (runner.IsTrueIdentityNpc)
                 {
                      Debug.LogError($"TiNpcManager: GameObject '{npcObject.name}' is flagged as TI ({runner.IsTrueIdentityNpc}), but TiNpcData link was lost! Cannot save state. Forcing Shopper reset and pooling.", npcObject);
                       // Runner.IsTrueIdentityNpc = false; // Cannot set private setter directly
                 } else {
                      Debug.LogWarning($"TiNpcManager: Received GameObject '{npcObject.name}' flagged NOT as TI in HandleTiNpcReturnToPool, which is unexpected. Shopper reset and pooling anyway.", npcObject);
                 }

             }


             // Ensure Shopper Inventory is Cleared for safety, regardless of data link state
             if (runner.Shopper != null)
             {
                  runner.Shopper.Reset();
                  // --- DEBUG: Log Shopper reset ---
                  Debug.Log($"DEBUG HandleTiNpcReturnToPool: Cleared Shopper inventory for returning GameObject '{npcObject.name}'.", npcObject);
                  // --- END DEBUG ---
             }


             Debug.Log($"TiNpcManager: Returning TI NPC GameObject '{npcObject.name}' to pool.", npcObject);
             if (poolingManager != null)
             {
                 poolingManager.ReturnPooledObject(npcObject);
             }
             else
             {
                 Debug.LogError($"TiNpcManager: PoolingManager is null! Cannot return TI NPC GameObject '{npcObject.name}' to pool. Destroying.", this);
                 Destroy(npcObject);
             }

             // REMOVED: UpdateActiveInactiveLists is no longer called here.
             // The data flags were updated directly.
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
    }
}