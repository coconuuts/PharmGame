// --- START OF FILE TiNpcSimulationManager.cs ---

using UnityEngine;
using System.Collections.Generic;
using System.Collections; // Needed for Coroutine
using System; // Needed for Enum and Type
using System.Linq; // Needed for LINQ (Where, FirstOrDefault, ToList)
using Game.NPC.TI; // Needed for TiNpcData, TiNpcManager (will be injected)
using Game.NPC.BasicStates; // Needed for BasicState enum, BasicPathState enum, BasicNpcStateManager (will be injected)
using Game.Spatial; // Needed for GridManager (will be injected)
using Game.Proximity; // Needed for ProximityManager (will be injected)
using Game.Navigation; // Needed for WaypointManager (will be injected)
using Game.Utilities; // Needed for TimeRange (will be accessed via TimeManager)

/// <summary>
/// Manages the simulation of True Identity (TI) NPCs when their GameObject is inactive.
/// Executes the logic defined in BasicNpcStateSO assets directly on TiNpcData.
/// Works in conjunction with TiNpcManager and other managers.
/// Also handles updating the grid position for active TI NPCs.
/// MODIFIED: Added check for 'canStartDay' flag when transitioning from BasicIdleAtHome.
/// </summary>
public class TiNpcSimulationManager : MonoBehaviour
{
    // --- Dependencies (Will be injected by TiNpcManager) ---
    private TiNpcManager tiNpcManager; // Reference back to the main manager
    private BasicNpcStateManager basicNpcStateManager;
    private GridManager gridManager;
    private ProximityManager proximityManager;
    private WaypointManager waypointManager;
    private TimeManager timeManager; // Need TimeManager for schedule checks
    private Transform playerTransform; // Need Player Transform for proximity query

    [Header("Simulation Settings")]
    [Tooltip("The interval (in seconds) between simulation ticks for inactive NPCs.")]
    [SerializeField] private float simulationTickInterval = 0.1f; // Process a batch every 0.1 seconds (10 Hz)
    [Tooltip("The maximum number of inactive NPCs to simulate per tick.")]
    [SerializeField] private int maxNpcsToSimulatePerTick = 10; // Process 10 NPCs per tick

    // Internal index for round-robin simulation batching
    private int simulationIndex = 0;

    // Coroutine references
    private Coroutine simulationCoroutine;

    // --- Grid Position Tracking for Active NPCs (Moved from TiNpcManager) ---
    // NOTE: These fields were temporarily here but are now moved to TiNpcData.
    // private Vector3 lastGridPosition; // REMOVED
    // private float timeSinceLastGridUpdate = 0f; // REMOVED
    private const float GridUpdateCheckInterval = 0.5f; // Check grid position every 0.5 seconds
    // --- END Moved Fields ---


    // Awake and Start methods will be used for initialization after dependencies are set
    // (Likely called by TiNpcManager after it gets singleton instances)

    void Awake()
    {
        // Dependencies are not set in Awake, they will be injected.
        // Basic setup if needed, but likely minimal.
    }

    void Start()
    {
        // Dependencies are not set in Start, they will be injected.
        // Basic setup if needed, but likely minimal.
    }

    /// <summary>
    /// Initializes the Simulation Manager with necessary external references.
    /// Called by the TiNpcManager after it has acquired singleton instances.
    /// </summary>
    public void Initialize(TiNpcManager tiNpcManager, BasicNpcStateManager basicNpcStateManager, GridManager gridManager, ProximityManager proximityManager, WaypointManager waypointManager, TimeManager timeManager, Transform playerTransform)
    {
        this.tiNpcManager = tiNpcManager;
        this.basicNpcStateManager = basicNpcStateManager;
        this.gridManager = gridManager;
        this.proximityManager = proximityManager;
        this.waypointManager = waypointManager;
        this.timeManager = timeManager;
        this.playerTransform = playerTransform;

        Debug.Log("TiNpcSimulationManager: Initialized with manager references.");

        // Initialize lastGridPosition here if needed, maybe based on player start pos or origin
        // lastGridPosition = Vector3.zero; // Or some default
    }


    void OnEnable()
    {
        // Start simulation coroutine if dependencies are already initialized
        // This handles cases where the component is disabled and re-enabled after Start()
        if (tiNpcManager != null && basicNpcStateManager != null && gridManager != null && playerTransform != null && timeManager != null)
        {
             StartSimulation();
        } else {
             // Debug.Log("TiNpcSimulationManager: Dependencies not set on OnEnable. Simulation will not start until Initialize is called."); // Too noisy
        }
    }

    void OnDisable()
    {
        // Stop simulation coroutine
        if (simulationCoroutine != null)
        {
            Debug.Log("TiNpcSimulationManager: Stopping simulation coroutine on OnDisable.");
            StopCoroutine(simulationCoroutine);
            simulationCoroutine = null;
        }
    }

    // OnDestroy is not strictly needed here as this is a component, not a singleton manager of data.

    /// <summary>
    /// Starts the coroutine that simulates inactive NPCs.
    /// Called by TiNpcManager after initialization.
    /// </summary>
    public void StartSimulation()
    {
        // Check if dependencies are set before starting
        if (tiNpcManager == null || basicNpcStateManager == null || gridManager == null || playerTransform == null || timeManager == null)
        {
            Debug.LogError("TiNpcSimulationManager: Cannot start simulation! Dependencies are not initialized.", this);
            return;
        }

        if (simulationCoroutine == null)
        {
            simulationCoroutine = StartCoroutine(SimulateInactiveNpcsRoutine());
            Debug.Log("TiNpcSimulationManager: Simulation coroutine started.");
        } else {
             // Debug.Log("TiNpcSimulationManager: Simulation coroutine is already running."); // Too noisy
        }
    }


    // --- Simulation Orchestration Routine (Moved from TiNpcManager) ---
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

            // If any critical reference is missing (null or destroyed), try to find it again.       
            if (playerTransform == null || gridManager == null || timeManager == null || basicNpcStateManager == null)
            {
                RefreshSceneReferences();
            }

            // If still missing after attempted refresh, wait and try again next tick.
            if (basicNpcStateManager == null || gridManager == null || playerTransform == null || timeManager == null)
            {
                // Commented out Error Log to prevent spamming console during scene load transitions
                // Debug.LogWarning("SIM TiNpcSimulationManager: Waiting for scene dependencies...");
                yield return new WaitForSeconds(simulationTickInterval * 2); 
                continue;
            }

            DateTime currentTime = timeManager.CurrentGameTime; // Use injected field

            // --- Build the list of NPCs to simulate this tick ---
            List<TiNpcData> simulationCandidates = new List<TiNpcData>();
            HashSet<TiNpcData> addedToBatch = new HashSet<TiNpcData>(); // Use a set to prevent duplicates

            // 1. Find inactive NPCs near the player using the grid (Standard Proximity Simulation)
            // Query a radius slightly larger than the farthest zone radius to catch NPCs just outside.
            // Use injected ProximityManager reference
            float simulationQueryRadius = (proximityManager != null ? proximityManager.farRadius : 30f) + (gridManager != null ? gridManager.cellSize : 5f); // Query slightly beyond far radius
            Vector3 playerPosition = playerTransform.position; // Use injected field

            if (gridManager != null) // Use injected field
            {
                List<TiNpcData> nearbyInactiveNpcs = gridManager.QueryItemsInRadius(playerPosition, simulationQueryRadius) // Use injected field
                     .Where(data => data != null && !data.IsActiveGameObject) // Filter for inactive only, add null check
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
                Debug.LogError("SIM TiNpcSimulationManager: GridManager is null, falling back to inefficient distance check for nearby inactive NPCs!");
                // Access all data via injected TiNpcManager reference
                if (tiNpcManager != null && tiNpcManager.allTiNpcs != null) // Use injected field
                {
                    foreach (var data in tiNpcManager.allTiNpcs.Values) // Use injected field
                    {
                        if (data != null && !data.IsActiveGameObject) // Add null check
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
                else
                {
                    Debug.LogError("SIM TiNpcSimulationManager: TiNpcManager or its allTiNpcs collection is null! Cannot perform fallback simulation.");
                }
            }


            // 2. Find inactive NPCs that MUST be simulated (e.g., path following) regardless of proximity
            // Iterate over *all* known TI NPCs via injected TiNpcManager
            if (tiNpcManager != null && tiNpcManager.allTiNpcs != null) // Use injected field
            {
                foreach (var data in tiNpcManager.allTiNpcs.Values) // Use injected field
                {
                    if (data == null) continue; // Add null check

                    // Include any inactive NPC that has a simulated target position set.
                    bool isInactiveSimulatingMovement = !data.IsActiveGameObject && data.simulatedTargetPosition.HasValue;

                    // Also include Cashier NPCs who are processing checkout, regardless of proximity or movement target
                    bool isInactiveCashierProcessing = !data.IsActiveGameObject && data.CurrentStateEnum != null && data.CurrentStateEnum.Equals(BasicState.BasicCashierProcessingCheckout);


                    if (isInactiveSimulatingMovement || isInactiveCashierProcessing) // Include processing cashiers
                    {
                        // Add to the batch if not already added (e.g., was also nearby)
                        if (addedToBatch.Add(data))
                        {
                            simulationCandidates.Add(data);
                        }
                    }
                }
            }
            else
            {
                Debug.LogError("SIM TiNpcSimulationManager: TiNpcManager or its allTiNpcs collection is null! Cannot find path-following/processing NPCs for simulation.");
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
                Debug.LogWarning($"SIM TiNpcSimulationManager: Generated an empty batch ({currentBatch.Count}) despite {totalToSimulate} candidates available! Resetting index.");
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
                if (npcData == null) // Add null check for safety
                {
                    Debug.LogError("SIM TiNpcSimulationManager: Encountered null TiNpcData in simulation batch. Skipping.", this);
                    countProcessedThisTick++;
                    continue;
                }

                // This check is crucial to confirm if an active NPC is mistakenly in this list (should not happen with filtering)
                if (npcData.IsActiveGameObject)
                {
                    Debug.LogError($"SIM TiNpcSimulationManager: Found ACTIVE NPC '{npcData.Id}' (GameObject '{npcData.NpcGameObject?.name ?? "NULL"}') in the INACTIVE simulation batch! This should not happen. Skipping simulation for this NPC.", npcData.NpcGameObject);
                    countProcessedThisTick++; // Still count this entry as processed in the batch to advance index correctly
                    continue; // Skip simulation logic for this NPC
                }
                // Ensure the state is valid for simulation before ticking
                BasicNpcStateSO currentStateSO = basicNpcStateManager?.GetBasicStateSO(npcData.CurrentStateEnum); // Use injected field
                if (currentStateSO == null)
                {
                    // This is the error case we are fixing. The saved state is an Active state enum.
                    Debug.LogError($"SIM {npcData.Id}: Current Basic State SO not found for Enum '{npcData.CurrentStateEnum?.GetType().Name}.{npcData.CurrentStateEnum?.ToString() ?? "NULL"}' during simulation tick! This is likely an Active state enum saved incorrectly. Attempting to map to Basic State and transition.", npcData.NpcGameObject);

                    // Attempt to map the saved Active state enum to a Basic state enum
                    // Use injected TiNpcManager reference for mapping
                    Enum mappedBasicState = tiNpcManager?.GetBasicStateFromActiveState(npcData.CurrentStateEnum);

                    if (mappedBasicState != null)
                    {
                        Debug.LogWarning($"SIM {npcData.Id}: Successfully mapped saved Active state '{npcData.CurrentStateEnum?.ToString() ?? "NULL"}' to Basic state '{mappedBasicState.ToString()}'. Transitioning to mapped basic state.", npcData.NpcGameObject);
                        // Transition to the correctly mapped basic state
                        basicNpcStateManager.TransitionToBasicState(npcData, mappedBasicState); // Use injected field
                        // The new state's logic will run on the *next* simulation tick for this NPC.
                        countProcessedThisTick++; // Count this entry
                        continue; // Skip simulation logic for this NPC this tick
                    }
                    else
                    {
                        // Mapping failed, fallback to BasicPatrol
                        Debug.LogError($"SIM {npcData.Id}: Failed to map saved state '{npcData.CurrentStateEnum?.ToString() ?? "NULL"}' to any Basic state. Transitioning to BasicPatrol (fallback).", npcData.NpcGameObject);
                        basicNpcStateManager.TransitionToBasicState(npcData, BasicState.BasicPatrol); // Use injected field and BasicState enum directly
                                                                                                      // Note: TransitionToBasicState logs errors if BasicPatrol isn't found either.
                        countProcessedThisTick++; // Count this entry
                        continue; // Skip simulation logic for this NPC this tick
                    }
                }

                // --- Check if StartDay has begun AND NPC can start day, then transition from BasicIdleAtHome --- // <-- MODIFIED CONDITION
                if (currentStateSO.HandledBasicState.Equals(BasicState.BasicIdleAtHome)) // Check if currently in BasicIdleAtHome
                {
                    // Check both the schedule AND the canStartDay flag
                    if (npcData.startDay.IsWithinRange(currentTime) && npcData.canStartDay) // <-- ADDED npcData.canStartDay CHECK
                    {
                        Debug.Log($"SIM {npcData.Id}: StartDay has begun ({npcData.startDay}, Current Time: {currentTime:HH:mm}) AND can start day. Transitioning from BasicIdleAtHome to day start state.", npcData.NpcGameObject);

                        // Get the intended Active start state from TiData (uses the refined property)
                        Enum dayStartActiveState = npcData.DayStartActiveStateEnum;

                        if (dayStartActiveState != null)
                        {
                            // Map the Active state to its Basic equivalent for simulation
                            // Use injected TiNpcManager reference for mapping
                            Enum dayStartBasicState = tiNpcManager?.GetBasicStateFromActiveState(dayStartActiveState);

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
                                basicNpcStateManager.TransitionToBasicState(npcData, dayStartBasicState); // Use injected field

                                // Continue to the next NPC in the batch. The new state's logic
                                // will run on the *next* simulation tick for this NPC.
                                countProcessedThisTick++;
                                continue; // Skip the rest of the tick logic for this NPC this frame
                            }
                            else
                            {
                                Debug.LogError($"SIM {npcData.Id}: Could not map intended Day Start Active State '{dayStartActiveState.GetType().Name}.{dayStartActiveState.ToString() ?? "NULL"}' to a Basic State! Transitioning to BasicPatrol fallback.", npcData.NpcGameObject);
                                basicNpcStateManager.TransitionToBasicState(npcData, BasicState.BasicPatrol); // Use injected field
                                countProcessedThisTick++;
                                continue;
                            }
                        }
                        else
                        {
                            // This is the error case the user reported. The DayStartActiveStateEnum property is null.
                            // This happens if usePathForDayStart is false but key/type are empty, OR
                            // if usePathForDayStart is true but PathState.FollowPath cannot be parsed.
                            Debug.LogError($"SIM {npcData.Id}: Intended Day Start Active State is null or invalid! Cannot transition from BasicIdleAtHome. Falling back to BasicPatrol. Check Day Start configuration in Dummy Data.", npcData.NpcGameObject); // Added more specific error message
                            basicNpcStateManager.TransitionToBasicState(npcData, BasicState.BasicPatrol); // Use injected field
                            countProcessedThisTick++;
                            continue;
                        }
                    }
                    // If in BasicIdleAtHome but day hasn't started OR cannot start day, do nothing and continue to next NPC
                    countProcessedThisTick++; // Still count as processed in batch
                    continue; // Skip the rest of the tick logic for this NPC this frame
                }
                // --- END Check StartDay AND canStartDay ---


                // --- DELEGATE SIMULATION TO BASICNPCSTATEMANAGER ---
                // BasicNpcStateManager will handle calling GridManager.UpdateItemPosition after simulation tick
                // This only happens if the NPC was NOT in BasicIdleAtHome or transitioned out of it this tick.
                basicNpcStateManager.SimulateTickForNpc(npcData, simulationTickInterval); // Use injected field
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
            // Debug.Log($"SIM TiNpcSimulationManager: Simulated {countProcessedThisTick} inactive NPCs this tick from {totalToSimulate} candidates. Next batch starts at index {simulationIndex}.");
        }
    }

        /// <summary>
        /// Helper method to check and notify GridManager of position changes for active TI NPCs.
        /// Runs periodically based on GridUpdateCheckInterval.
        /// Called by the NpcStateMachineRunner's Update loop for active TI NPCs.
        /// </summary>
        /// <param name="data">The TiNpcData associated with the active NPC.</param>
        /// <param name="currentPosition">The NPC's current GameObject position.</param>
        public void CheckGridPositionUpdate(TiNpcData data, Vector3 currentPosition) // Added parameters to pass data and current position
    {
        if (data == null || gridManager == null) // Use injected field
        {
            // Debug.LogWarning($"TiNpcSimulationManager: CheckGridPositionUpdate called with invalid data or dependencies. Data null: {data == null}, GridManager null: {gridManager == null}", data?.NpcGameObject); // Too noisy
            return;
        }

        // Use the TiNpcData instance to store the last tracked grid position and timer
        data.timeSinceLastGridUpdate += Time.deltaTime; // Use field on TiNpcData
        if (data.timeSinceLastGridUpdate >= GridUpdateCheckInterval) // Use field on TiNpcData
        {
            data.timeSinceLastGridUpdate -= GridUpdateCheckInterval; // Use field on TiNpcData
            if ((currentPosition - data.lastGridPosition).sqrMagnitude >= (gridManager.cellSize * gridManager.cellSize)) // Use field on TiNpcData and injected field
            {
                // --- Perform the actual grid update ---
                this.NotifyActiveNpcPositionChanged(data, data.lastGridPosition, currentPosition); // Call the method on THIS manager
                data.lastGridPosition = currentPosition; // Use field on TiNpcData
                                                         // --- End Perform ---

                // Debug.Log($"TiNpcSimulationManager: Significant move detected for active TI NPC '{data.Id}'. Grid updated.", data.NpcGameObject); // Too noisy
            }
        }
    }

    /// <summary>
    /// This method attempts to find scene references if they are missing.
    /// </summary>
    private void RefreshSceneReferences()
    {
        if (playerTransform == null) 
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) playerTransform = player.transform;
        }

        if (gridManager == null) gridManager = FindFirstObjectByType<GridManager>();
        if (proximityManager == null) proximityManager = FindFirstObjectByType<ProximityManager>();
        if (waypointManager == null) waypointManager = FindFirstObjectByType<WaypointManager>();
        if (timeManager == null) timeManager = TimeManager.Instance; // Singleton ref might be safer
        if (basicNpcStateManager == null) basicNpcStateManager = FindFirstObjectByType<BasicNpcStateManager>();
        if (tiNpcManager == null) tiNpcManager = FindFirstObjectByType<TiNpcManager>();
    }

        /// <summary>
        /// Called by an active NpcStateMachineRunner when its position changes significantly (e.g., enters a new grid cell).
        /// Updates the NPC's position in the GridManager.
        /// MOVED from TiNpcManager.
        /// </summary>
        /// <param name="data">The TiNpcData associated with the active NPC.</param>
        /// <param name="oldPosition">The NPC's position before the change.</param>
        /// <param name="newPosition">The NPC's new position.</param>
        public void NotifyActiveNpcPositionChanged(TiNpcData data, Vector3 oldPosition, Vector3 newPosition)
        {
             if (gridManager != null) // Use injected field
             {
                  gridManager.UpdateItemPosition(data, oldPosition, newPosition); // Use injected field
                  // Debug.Log($"TiNpcSimulationManager: Notified of active NPC '{data.Id}' position change. Updated grid.", data.NpcGameObject); // Too noisy
             }
             else
             {
                  Debug.LogWarning($"TiNpcSimulationManager: GridManager is null! Cannot update grid position for active NPC '{data.Id}'.", data.NpcGameObject); // Use injected field
             }
        }
        // --- END Moved Methods ---
    }