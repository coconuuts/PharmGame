using UnityEngine;
using System.Collections.Generic;
using Game.NPC.TI; // Needed for TiNpcData
using System.Linq; // Needed for LINQ (Where, FirstOrDefault)
using Game.NPC; // Needed for NpcStateMachineRunner, GeneralState, CustomerState enums
using Game.NPC.States; // Needed for State SOs (to check HandledState)
using Game.NPC.BasicStates; // Needed for BasicState enum, BasicNpcStateManager, BasicNpcStateSO
using Utils.Pooling; // Needed for PoolingManager
using System.Collections; // Needed for Coroutine
using System; // Needed for Enum and Type
using Game.NPC.Types; // Needed for TestState enum (Patrol)
using CustomerManagement; // Needed for CustomerManager
using Game.NPC.Handlers;

namespace Game.NPC.TI // Keep in the TI namespace
{
    /// <summary>
    /// Manages the persistent data for True Identity (TI) NPCs.
    /// Handles loading, storing, and tracking TI NPC data independent of their GameObjects.
    /// Implements off-screen simulation logic (delegating to BasicNpcStateManager)
    /// and proximity-based activation/deactivation.
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
        // Reference to BasicNpcStateManager (will be obtained in Awake/Start)
        private BasicNpcStateManager basicNpcStateManager; // <-- NEW Reference

        [Header("TI NPC Setup")]
        [Tooltip("List of NPC prefabs that this manager can pool and activate for TI NPCs.")]
        [SerializeField] private List<GameObject> tiNpcPrefabs;

        [Header("Simulation Settings (Phase 4)")]
        [Tooltip("The interval (in seconds) between simulation ticks for inactive NPCs.")]
        [SerializeField] private float simulationTickInterval = 0.1f; // Process a batch every 0.1 seconds (10 Hz)
        [Tooltip("The maximum number of inactive NPCs to simulate per tick.")]
        [SerializeField] private int maxNpcsToSimulatePerTick = 10; // Process 10 NPCs per tick


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
            [Tooltip("Initial Basic State for this dummy NPC.")]
            [SerializeField] public BasicState initialBasicState = BasicState.BasicPatrol; // <-- NEW: Initial Basic State
        }

        // --- Persistent Data Storage ---
        // Use a single dictionary as the source of truth for all TI NPC data
        private Dictionary<string, TiNpcData> allTiNpcs = new Dictionary<string, TiNpcData>();

        // Internal index for round-robin simulation batching
        private int simulationIndex = 0;

         // Coroutine references
         private Coroutine simulationCoroutine;
         private Coroutine proximityCheckCoroutine;

        // --- State Mapping Dictionaries (NEW) ---
        private Dictionary<Enum, Enum> activeToBaseStateMap;
        private Dictionary<Enum, Enum> basicToActiveStateMap;
        // --- END NEW ---


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
            SetupStateMappings(); // <-- Call mapping setup

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

             // Get reference to BasicNpcStateManager (NEW)
             basicNpcStateManager = BasicNpcStateManager.Instance; // <-- Get manager instance
             if (basicNpcStateManager == null)
             {
                  Debug.LogError("TiNpcManager: BasicNpcStateManager instance not found! Cannot simulate inactive TI NPCs. Ensure BasicNpcStateManager is in the scene.", this);
                  // Do NOT disable manager entirely, just simulation won't work.
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

             Debug.Log($"TiNpcManager: Start completed.");

            // Start the simulation loop if BasicNpcStateManager is available
             if (basicNpcStateManager != null)
             {
                simulationCoroutine = StartCoroutine(SimulateInactiveNpcsRoutine());
                Debug.Log("TiNpcManager: Simulation coroutine started.");
             } else {
                  Debug.LogWarning("TiNpcManager: BasicNpcStateManager is null, cannot start simulation coroutine.");
             }


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
             // Restart simulation coroutine if manager was disabled and re-enabled AND the basic manager exists
             if (simulationCoroutine == null && allTiNpcs.Count > 0 && basicNpcStateManager != null)
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
                     // Optional: Draw the ID as text for easier identification (Requires UnityEditor and Handles)
                     // try { UnityEditor.Handles.Label(tiData.CurrentWorldPosition + Vector3.up * (inactiveGizmoRadius + 0.1f), tiData.Id); } catch {}
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


             // Basic -> Active mappings
             basicToActiveStateMap[BasicState.BasicPatrol] = TestState.Patrol;
             basicToActiveStateMap[BasicState.BasicLookToShop] = CustomerState.LookingToShop;
             basicToActiveStateMap[BasicState.BasicEnteringStore] = CustomerState.Entering;
             basicToActiveStateMap[BasicState.BasicBrowse] = CustomerState.Browse;
             basicToActiveStateMap[BasicState.BasicWaitForCashier] = CustomerState.Queue; // This is the DEFAULT mapping, can be overridden during activation for specific logic.
             basicToActiveStateMap[BasicState.BasicExitingStore] = CustomerState.Exiting;
             // BasicState.None is not mapped back to an active state, handled by activation logic.


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
        /// The low-tick simulation routine. Iterates over all TI NPC data but only simulates inactive ones in batches,
        /// delegating simulation logic to the BasicNpcStateManager.
        /// </summary>
        private IEnumerator SimulateInactiveNpcsRoutine()
        {
             while (true)
             {
                  yield return new WaitForSeconds(simulationTickInterval);

                  if (allTiNpcs.Count == 0) // Check total count now
                  {
                       yield return new WaitForSeconds(simulationTickInterval * 5); // Wait a bit longer if empty
                       continue;
                  }

                  // Get a list of inactive NPCs to process this tick (efficiently using LINQ)
                  List<TiNpcData> inactiveBatch = allTiNpcs.Values
                      .Where(data => !data.IsActiveGameObject) // Filter for inactive ones
                       .ToList(); // Convert to a list of *all* inactive NPCs

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

                       // --- DELEGATE SIMULATION TO BASICNPCSTATEMANAGER (NEW) ---
                       if (basicNpcStateManager != null)
                       {
                            // Ensure the data's current state is a BasicState before simulating.
                            // This should be set during deactivation, but defensive check/fallback.
                            if (npcData.CurrentStateEnum == null || !basicNpcStateManager.IsBasicState(npcData.CurrentStateEnum))
                            {
                                 Debug.LogWarning($"SIM TiNpcManager: NPC '{npcData.Id}' is inactive but its state '{npcData.CurrentStateEnum?.GetType().Name}.{npcData.CurrentStateEnum?.ToString() ?? "NULL"}' is not a BasicState (or null)! Assuming BasicPatrol and re-initializingsimulation state.", npcData.NpcGameObject);
                                 // Force a transition to BasicPatrol as a fallback if state is not a BasicState or is null
                                 basicNpcStateManager.TransitionToBasicState(npcData, BasicState.BasicPatrol);
                                 // The transition calls OnEnter for the new state. We don't need to call SimulateTick again in this same tick,
                                 // the next tick will handle it.
                                 // basicNpcStateManager.SimulateTickForNpc(npcData, simulationTickInterval); // REMOVED - avoids potential double simulation in one tick
                            } else {
                                // State is valid BasicState, simulate normally
                                basicNpcStateManager.SimulateTickForNpc(npcData, simulationTickInterval);
                            }
                       }
                       else
                       {
                            Debug.LogError("SIM TiNpcManager: BasicNpcStateManager is null! Cannot simulate inactive NPCs.", this);
                            // Break out of the foreach loop if manager is null to avoid repeated errors
                            break;
                       }
                       // --- END DELEGATION ---

                       countProcessedThisTick++;
                  }

                  // Advance the simulation index by the number of NPCs processed in this batch.
                  // This must be done *after* iterating the batch.
                  simulationIndex += batchSize;

                  // The simulation index should now wrap based on the total number of *inactive* NPCs,
                  // NOT the total number of all NPCs. Recalculate total inactive count.
                   totalInactiveCount = allTiNpcs.Values.Count(data => !data.IsActiveGameObject);
                  if (totalInactiveCount > 0) // Avoid division by zero or wrapping when list is empty
                  {
                      simulationIndex %= totalInactiveCount;
                  } else {
                       simulationIndex = 0; // Reset if no inactive NPCs left
                  }

                  // Log simulation tick summary (Optional, can be noisy)
                  // Debug.Log($"SIM TiNpcManager: Simulated {countProcessedThisTick} inactive NPCs this tick. Total inactive: {totalInactiveCount}. Next batch starts at index {simulationIndex}.");
             }
        }


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

                   if (basicNpcStateManager == null) // Added check for basic manager
                   {
                       Debug.LogError("TiNpcManager: BasicNpcStateManager is null. Cannot perform TI NPC deactivation/activation checks that require state mapping/initialization.", this);
                       yield return new WaitForSeconds(proximityCheckInterval * 5);
                       continue; // Cannot proceed without basic manager
                   }

                   Vector3 playerPosition = playerTransform.position;

                   // Create lists of data to process (avoid modifying collection while iterating)
                   List<TiNpcData> toActivate = new List<TiNpcData>();
                   List<TiNpcData> potentiallyToDeactivate = new List<TiNpcData>(); // Collect active NPCs to check

                   // --- Populate lists ---
                   foreach (var tiData in allTiNpcs.Values)
                   {
                       if (!tiData.IsActiveGameObject && tiData.NpcGameObject == null) // Genuinely inactive
                       {
                           float distanceSq = (tiData.CurrentWorldPosition - playerPosition).sqrMagnitude; // Use data position for inactive
                           if (distanceSq <= activationRadius * activationRadius)
                           {
                                Debug.Log($"PROXIMITY {tiData.Id}: Inactive NPC within activation radius ({Mathf.Sqrt(distanceSq):F2}m). Adding to activation list.");
                                toActivate.Add(tiData);
                           }
                       }
                       else if (tiData.IsActiveGameObject && tiData.NpcGameObject != null) // Genuinely active
                       {
                            potentiallyToDeactivate.Add(tiData);
                       } else {
                           // Inconsistent state (IsActiveGameObject true but NpcGameObject null, or vice versa)
                           Debug.LogError($"TiNpcManager: Inconsistent state for TI NPC '{tiData.Id}'. IsActiveGameObject={tiData.IsActiveGameObject}, NpcGameObject={(tiData.NpcGameObject != null ? tiData.NpcGameObject.name : "NULL")}. Forcing cleanup.", tiData.NpcGameObject);
                           // Attempt to force cleanup - unlink data, potentially destroy object if still exists
                           if (tiData.NpcGameObject != null) Destroy(tiData.NpcGameObject);
                           tiData.UnlinkGameObject();
                       }
                   }

                   // --- Process Activation List ---
                   if (toActivate.Count > 0)
                   {
                       Debug.Log($"PROXIMITY TiNpcManager: Found {toActivate.Count} NPCs to activate this tick. Activating now...");
                        foreach (var tiData in toActivate)
                       {
                            ActivateTiNpc(tiData); // Call the activation method
                       }
                   }

                   // --- Process Deactivation List ---
                   foreach (var tiData in potentiallyToDeactivate) // Iterate over the collected list
                   {
                        // Re-check if it's still active and has a GameObject link (defensive)
                        if (tiData.IsActiveGameObject && tiData.NpcGameObject != null)
                        {
                           float distanceSq = (tiData.NpcGameObject.transform.position - playerPosition).sqrMagnitude; // Use GameObject position for active
                           if (distanceSq >= deactivationRadius * deactivationRadius)
                           {
                                NpcStateMachineRunner runner = tiData.NpcGameObject.GetComponent<NpcStateMachineRunner>();
                                if (runner != null)
                                {
                                     NpcStateSO currentStateSO = runner.GetCurrentState();
                                     // Check if the NPC is in a state that should prevent deactivation (e.g., Combat, Transaction)
                                     // If the current state is NOT interruptible, we skip deactivation for this tick.
                                     if (currentStateSO != null && !currentStateSO.IsInterruptible)
                                     {
                                          Debug.Log($"PROXIMITY {tiData.Id}: Deactivation check skipped. Current state '{currentStateSO.name}' is not interruptible.");
                                          continue; // Skip deactivation attempt for this NPC this tick
                                     }

                                     // --- Determine and Save the Basic State before triggering Pooling ---
                                     Enum currentActiveState = currentStateSO?.HandledState; // Get the enum key of the current active state
                                     Enum targetBasicState = GetBasicStateFromActiveState(currentActiveState); // Map to the corresponding basic state

                                     if (targetBasicState != null)
                                     {
                                         // Save the determined Basic State to the TiNpcData
                                          tiData.SetCurrentState(targetBasicState);
                                          Debug.Log($"PROXIMITY {tiData.Id}: Active state '{currentActiveState?.GetType().Name}.{currentActiveState?.ToString() ?? "NULL"}' maps to Basic State '{targetBasicState.GetType().Name}.{targetBasicState.ToString()}'. Saving this state to TiData.");

                                         // --- NEW FIX: Call OnEnter for the target Basic State to initialize simulation data ---
                                         BasicNpcStateSO targetBasicStateSO = basicNpcStateManager.GetBasicStateSO(targetBasicState);
                                         if(targetBasicStateSO != null)
                                         {
                                             Debug.Log($"PROXIMITY {tiData.Id}: Calling OnEnter for Basic State '{targetBasicStateSO.name}' to initialize simulation data.");
                                             targetBasicStateSO.OnEnter(tiData, basicNpcStateManager); // Pass the data and the manager
                                         } else
                                         {
                                             // This shouldn't happen if mapping and GetBasicStateSO work, but defensive
                                             Debug.LogError($"PROXIMITY {tiData.Id}: Could not get target Basic State SO for '{targetBasicState.GetType().Name}.{targetBasicState.ToString()}' during deactivation. Cannot initialize simulation state data!", tiData.NpcGameObject);
                                             // Data might be left in a bad state, but proceed with pooling.
                                         }
                                         // --- END NEW FIX ---


                                         // --- Trigger Deactivation Flow ---
                                         Debug.Log($"PROXIMITY {tiData.Id}: TI NPC outside deactivation radius ({Mathf.Sqrt(distanceSq):F2}m). State is interruptible or null. Triggering TransitionToState(ReturningToPool).");
                                         // Transition the Runner to the ReturningToPool state.
                                         // The Runner.TransitionToState handles calling Runner.Deactivate() *before* entering the state.
                                         // Runner.Deactivate() will now save the *Basic State* we just set on tiData, and position/rotation.
                                         // HandleTiNpcReturnToPool will be called later by the pooling event, clearing data link/flag.
                                         runner.TransitionToState(runner.GetStateSO(GeneralState.ReturningToPool));
                                         // --- END Trigger ---

                                     }
                                     else
                                     {
                                         // This shouldn't happen if GetBasicStateFromActiveState has a fallback, but defensive.
                                         Debug.LogError($"PROXIMITY {tiData.Id}: Could not determine a Basic State mapping for active state '{currentActiveState?.GetType().Name}.{currentActiveState?.ToString() ?? "NULL"}'. Cannot save state for simulation! Forcing cleanup.", tiData.NpcGameObject);
                                          // Fallback: Destroy the GameObject and unlink the data without attempting to save a simulation state
                                          Destroy(tiData.NpcGameObject);
                                          tiData.UnlinkGameObject();
                                     }
                                }
                                else
                                {
                                     Debug.LogError($"TiNpcManager: Active TI NPC '{tiData.Id}' GameObject '{tiData.NpcGameObject?.name ?? "NULL"}' missing Runner! Inconsistency detected. Forcing cleanup.", tiData.NpcGameObject);
                                      Destroy(tiData.NpcGameObject); // Destroy the GameObject
                                      tiData.UnlinkGameObject(); // Use helper to clear data link and flags
                                }
                           }
                        }
                   }
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
                   Debug.Log($"PROXIMITY TiNpcManager: Skipping activation attempt for '{tiData?.Id ?? "NULL"}'. Reason: tiData is null ({tiData == null}), IsActiveGameObject={tiData?.IsActiveGameObject}, NpcGameObject is null={(tiData?.NpcGameObject == null)}.");
                   return; // Already active or invalid
              }

              if (tiNpcPrefabs == null || tiNpcPrefabs.Count == 0 || poolingManager == null || customerManager == null || basicNpcStateManager == null) // Added basicNpcStateManager check
              {
                   Debug.LogError("TiNpcManager: Cannot activate TI NPC. TI Prefabs list, PoolingManager, CustomerManager, or BasicNpcStateManager is null.", this);
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

                       // --- NEW FIX: Handle activation from BasicWaitForCashierState ---
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

                                     }
                                     else
                                     {
                                         // Main queue is full, cannot be a customer right now
                                         Debug.Log($"PROXIMITY {tiData.Id}: Main queue is full. Cannot join. Activating to Exiting state.", npcObject);
                                         startingActiveStateEnum = CustomerState.Exiting; // Give up on shopping
                                         // Clear simulation data as active state takes over
                                         tiData.simulatedTargetPosition = null;
                                         tiData.simulatedStateTimer = 0f;
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
                                }
                           }
                       }
                         // --- END NEW FIX ---
                       
                                             // --- Handle activation from BasicBrowseState (NEW FIX) ---
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
                              }
                              else
                              {
                                   Debug.LogError($"PROXIMITY {tiData.Id}: Could not get a valid browse location from CustomerManager during BasicBrowse activation! Activating to Exiting as fallback.", npcObject);
                                   startingActiveStateEnum = CustomerState.Exiting; // Fallback if cannot get browse location
                                   // Clear simulation data
                                   tiData.simulatedTargetPosition = null;
                                   tiData.simulatedStateTimer = 0f;
                              }
                         }
                         // --- END BasicBrowseState handling ---

                       // --- Existing Logic (for states OTHER THAN BasicWaitForCashier) ---
                         if (!handledActivationBySavedState) // Only run this if the BasicWaitForCashier case was NOT handled
                         {
                              if (savedStateEnum != null && basicNpcStateManager.IsBasicState(savedStateEnum))
                              {
                                   // If the saved state is a BasicState (but NOT BasicWaitForCashier), map it to the corresponding Active State
                                   startingActiveStateEnum = GetActiveStateFromBasicState(savedStateEnum);
                                   Debug.Log($"PROXIMITY {tiData.Id}: Saved state '{savedStateEnum.GetType().Name}.{savedStateEnum.ToString()}' is a Basic State. Mapping to Active State '{startingActiveStateEnum?.GetType().Name}.{startingActiveStateEnum?.ToString() ?? "NULL"}'.", npcObject);

                                   // Reset simulation data when transitioning from simulation to active
                                   tiData.simulatedTargetPosition = null; // Clear simulated target
                                   tiData.simulatedStateTimer = 0f; // Reset timer on activation

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
                              }
                              else
                              {
                                   // If no state was saved, or mapping failed, startingActiveStateEnum remains null.
                                   // The Runner will then fall back to its GetPrimaryStartingStateSO logic (which checks TypeDefs).
                                   Debug.Log($"PROXIMITY {tiData.Id}: No valid saved state found or mapped. Runner will determine primary starting state from TypeDefs.", npcObject);
                                   // Ensure simulation data is clean if no saved state existed
                                   tiData.simulatedTargetPosition = null;
                                   tiData.simulatedStateTimer = 0f;
                              }
                         }
                       // --- END Existing Logic ---

                       // Call the Runner's Activate method with the determined starting state override
                       // If startingActiveStateEnum is null, Runner.Activate will use GetPrimaryStartingStateSO().
                       runner.Activate(tiData, customerManager, startingActiveStateEnum); // <-- Pass determined override state Enum

                       Debug.Log($"PROXIMITY TiNpcManager: Activation initiated for TI NPC '{tiData.Id}' (GameObject '{npcObject.name}'). Runner.Activate called with override state: {startingActiveStateEnum?.GetType().Name}.{startingActiveStateEnum?.ToString() ?? "NULL"}");

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


        private void LoadDummyNpcData()
        {
             if (dummyNpcData == null || dummyNpcData.Count == 0)
             {
                  Debug.LogWarning("TiNpcManager: No dummy NPC data entries configured to load.", this);
                  return;
             }

             allTiNpcs.Clear();

             if (basicNpcStateManager == null)
             {
                  Debug.LogError("TiNpcManager: BasicNpcStateManager not found. Cannot initialize dummy TI NPC data with proper basic states or targets for simulation.", this);
                  return; // Cannot load dummy data correctly
             }

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

                  // --- Initialize with the specified Basic State from dummy data (NEW) ---
                   Enum initialBasicStateEnum = entry.initialBasicState;

                   // Validate the initial state exists as a Basic State SO
                   BasicNpcStateSO initialStateSO = basicNpcStateManager.GetBasicStateSO(initialBasicStateEnum);
                   if (initialStateSO == null)
                   {
                        Debug.LogError($"TiNpcManager: Dummy NPC '{entry.id}' configured with initial Basic State '{initialBasicStateEnum}', but no BasicStateSO asset found for this state! Skipping NPC load.", this);
                        continue; // Skip loading this NPC if its initial state is invalid
                   }

                   newNpcData.SetCurrentState(initialBasicStateEnum); // Set the initial Basic State

                   // Call the OnEnter for this initial basic state immediately to set up simulation data (timer, target)
                   // This replicates the OnEnter call that would happen if the NPC transitioned into this state.
                   initialStateSO.OnEnter(newNpcData, basicNpcStateManager); // Pass the data and the manager

                  // Flags and GameObject link are initialized in the TiNpcData constructor (isActiveGameObject=false, NpcGameObject=null)

                  allTiNpcs.Add(newNpcData.Id, newNpcData);
                   Debug.Log($"TiNpcManager: Loaded dummy NPC '{newNpcData.Id}' with initial Basic State '{initialBasicStateEnum}'. Simulation timer initialized to {newNpcData.simulatedStateTimer:F2}s, Target: {newNpcData.simulatedTargetPosition?.ToString() ?? "NULL"}.");
             }
        }

        // --- Public Methods ---

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
    }
}